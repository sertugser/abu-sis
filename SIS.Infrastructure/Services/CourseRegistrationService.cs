using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SIS.Application.CourseRegistration.DTOs;
using SIS.Application.CourseRegistration.Services;
using SIS.Domain.Enums;
using SIS.Domain.ValueObjects;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services;

public class CourseRegistrationService : ICourseRegistrationService
{
    private readonly AppDbContext _context;
    private readonly IRegistrationValidationService _validationService;

    public CourseRegistrationService(AppDbContext context, IRegistrationValidationService validationService)
    {
        _context = context;
        _validationService = validationService;
    }

    public async Task<RegistrationResponseDto> AddToPlanAsync(int studentId, int courseSectionId)
    {
        var response = new RegistrationResponseDto();

        var section = await _context.CourseSections
            .Include(cs => cs.Course)
            .FirstOrDefaultAsync(cs => cs.Id == courseSectionId);

        if (section == null)
        {
            response.Success = false;
            response.ValidationResult = new ValidationResultDto
            {
                IsValid = false,
                Errors = new List<ValidationError>
                {
                    new() { Rule = "SectionExists", Message = "Ders sectionu bulunamadi." }
                }
            };
            return response;
        }

        // Guard: kayıt dönemi kontrolü
        var term = await _context.AcademicTerms.AsNoTracking().FirstOrDefaultAsync(t => t.Id == section.TermId);
        if (term != null && term.Status != TermStatus.RegistrationOpen && term.Status != TermStatus.AddDrop)
        {
            response.Success = false;
            response.ValidationResult = new ValidationResultDto
            {
                IsValid = false,
                Errors = new List<ValidationError>
                {
                    new() { Rule = "RegistrationClosed", Message = $"Ders kayıt dönemi açık değil. Dönem durumu: {GetTermStatusLabel(term.Status)}" }
                }
            };
            return response;
        }

        // Check if already in plan (same section)
        var existingForSection = await _context.CourseRegistrations
            .FirstOrDefaultAsync(cr => cr.StudentId == studentId
                && cr.CourseSectionId == courseSectionId
                && (cr.Status == RegistrationStatus.Draft
                    || cr.Status == RegistrationStatus.Active
                    || cr.Status == RegistrationStatus.PendingAdvisorApproval));

        if (existingForSection != null)
        {
            response.Success = false;
            response.ValidationResult = new ValidationResultDto
            {
                IsValid = false,
                Errors = new List<ValidationError>
                {
                    new() { Rule = "AlreadyInPlan", Message = "Bu ders zaten planda veya kayitli.", CourseSectionId = courseSectionId }
                }
            };
            return response;
        }

        // BUG FIX: Check if already in plan for the same COURSE but different section
        var existingForCourse = await _context.CourseRegistrations
            .Include(cr => cr.CourseSection)
            .FirstOrDefaultAsync(cr => cr.StudentId == studentId
                && cr.CourseSection.CourseId == section.CourseId
                && cr.CourseSectionId != courseSectionId
                && (cr.Status == RegistrationStatus.Draft
                    || cr.Status == RegistrationStatus.Active
                    || cr.Status == RegistrationStatus.PendingAdvisorApproval));

        if (existingForCourse != null)
        {
            response.Success = false;
            response.ValidationResult = new ValidationResultDto
            {
                IsValid = false,
                Errors = new List<ValidationError>
                {
                    new() { Rule = "CourseAlreadySelected", Message = "Bu dersin baska bir sectionu zaten planda veya kayitli.", CourseSectionId = courseSectionId }
                }
            };
            return response;
        }

        // Prerequisite check — block if prerequisites are not met
        var prereqs = await _context.CoursePrerequisites
            .Where(p => p.CourseId == section.CourseId)
            .Select(p => p.RequiredCourseId)
            .ToListAsync();

        if (prereqs.Count > 0)
        {
            // Passed courses: grade is not FF/NA/U/W/I
            var passedCourseIds = await _context.CourseRegistrations
                .Include(cr => cr.CourseSection)
                .Where(cr => cr.StudentId == studentId
                    && cr.Grade != null
                    && cr.Grade != GradeCode.FF && cr.Grade != GradeCode.NA
                    && cr.Grade != GradeCode.U && cr.Grade != GradeCode.W
                    && cr.Grade != GradeCode.I
                    && cr.Status != RegistrationStatus.Dropped)
                .Select(cr => cr.CourseSection.CourseId)
                .Distinct()
                .ToListAsync();

            var passedSet = passedCourseIds.ToHashSet();

            // Check equivalencies
            var equivMap = (await _context.CourseEquivalencies.ToListAsync())
                .GroupBy(e => e.CourseId)
                .ToDictionary(g => g.Key, g => g.Select(e => e.EquivalentCourseId).ToHashSet());

            foreach (var reqId in prereqs)
            {
                var met = passedSet.Contains(reqId);
                if (!met && equivMap.TryGetValue(reqId, out var equivIds))
                    met = equivIds.Any(eid => passedSet.Contains(eid));

                if (!met)
                {
                    var reqCourse = await _context.Courses.FindAsync(reqId);
                    response.Success = false;
                    response.ValidationResult = new ValidationResultDto
                    {
                        IsValid = false,
                        Errors = new List<ValidationError>
                        {
                            new() { Rule = "PrerequisiteNotMet", Message = $"Onkosul karsilanmamistir: {reqCourse?.Code ?? "?"} dersi gecilmeli.", CourseSectionId = courseSectionId }
                        }
                    };
                    return response;
                }
            }
        }

        // BUG FIX: Check for previously dropped/withdrawn record (unique index on StudentId+CourseSectionId)
        var droppedRecord = await _context.CourseRegistrations
            .FirstOrDefaultAsync(cr => cr.StudentId == studentId
                && cr.CourseSectionId == courseSectionId
                && (cr.Status == RegistrationStatus.Dropped
                    || cr.Status == RegistrationStatus.Withdrawn));

        Domain.Entities.CourseRegistration registration;

        if (droppedRecord != null)
        {
            // Reactivate existing record instead of creating new (unique constraint)
            droppedRecord.Status = RegistrationStatus.Draft;
            droppedRecord.UpdatedAt = DateTime.UtcNow;
            registration = droppedRecord;
        }
        else
        {
            // Create new CourseRegistration with Status = Draft (plan/soft hold - FR-44)
            registration = new Domain.Entities.CourseRegistration
            {
                StudentId = studentId,
                CourseSectionId = courseSectionId,
                Status = RegistrationStatus.Draft,
                CreatedAt = DateTime.UtcNow
            };
            _context.CourseRegistrations.Add(registration);
        }

        // FR-56C: Mark as repeated if student already passed this course (grade improvement)
        registration.IsRepeated = await _context.CourseRegistrations
            .Include(cr => cr.CourseSection)
            .AnyAsync(cr => cr.StudentId == studentId
                && cr.CourseSection.CourseId == section.CourseId
                && RegistrationRulePolicy.IsPassedGrade(cr.Grade));

        // Increment section.EnrolledCount (soft hold - FR-44)
        section.EnrolledCount++;

        await _context.SaveChangesAsync();

        response.Success = true;
        response.Registrations = new List<RegistrationItemDto>
        {
            new()
            {
                Id = registration.Id,
                CourseSectionId = section.Id,
                CourseCode = section.Course.Code,
                CourseName = section.Course.Name,
                Ects = section.Course.Ects,
                Status = registration.Status.ToString()
            }
        };

        return response;
    }

    public async Task<RegistrationResponseDto> SubmitRegistrationAsync(int studentId, RegistrationSubmitDto dto)
    {
        var response = new RegistrationResponseDto();

        // Validate the registration
        var validationResult = await _validationService.ValidateRegistrationAsync(studentId, dto.CourseSectionIds);

        if (!validationResult.IsValid)
        {
            response.Success = false;
            response.ValidationResult = validationResult;
            return response;
        }

        // Check if student is scholarship (FR-52: skip payment confirmation)
        var student = await _context.Students.FindAsync(studentId);
        if (student == null)
        {
            response.Success = false;
            response.ValidationResult = new ValidationResultDto
            {
                IsValid = false,
                Errors = new List<ValidationError>
                {
                    new() { Rule = "StudentExists", Message = "Ogrenci bulunamadi." }
                }
            };
            return response;
        }

        var targetStatus = student.IsScholarshipStudent
            ? RegistrationStatus.Active
            : RegistrationStatus.PendingAdvisorApproval;

        // Load or create registrations for the submitted sections
        var registrations = await _context.CourseRegistrations
            .Include(cr => cr.CourseSection)
                .ThenInclude(cs => cs.Course)
            .Where(cr => cr.StudentId == studentId
                && dto.CourseSectionIds.Contains(cr.CourseSectionId)
                && cr.Status == RegistrationStatus.Draft)
            .ToListAsync();

        // For sections that don't have a Draft registration yet, create them
        var existingCourseSectionIds = registrations.Select(r => r.CourseSectionId).ToHashSet();
        var missingSectionIds = dto.CourseSectionIds.Where(id => !existingCourseSectionIds.Contains(id)).ToList();

        if (missingSectionIds.Any())
        {
            var missingSections = await _context.CourseSections
                .Include(cs => cs.Course)
                .Where(cs => missingSectionIds.Contains(cs.Id))
                .ToListAsync();

            foreach (var section in missingSections)
            {
                var newReg = new Domain.Entities.CourseRegistration
                {
                    StudentId = studentId,
                    CourseSectionId = section.Id,
                    Status = RegistrationStatus.Draft,
                    CreatedAt = DateTime.UtcNow
                };
                _context.CourseRegistrations.Add(newReg);
                section.EnrolledCount++;
                registrations.Add(newReg);
            }
            await _context.SaveChangesAsync();

            // Reload to get navigation properties
            registrations = await _context.CourseRegistrations
                .Include(cr => cr.CourseSection)
                    .ThenInclude(cs => cs.Course)
                .Where(cr => cr.StudentId == studentId
                    && dto.CourseSectionIds.Contains(cr.CourseSectionId)
                    && cr.Status == RegistrationStatus.Draft)
                .ToListAsync();
        }

        // Update all Draft -> target status
        foreach (var reg in registrations)
        {
            reg.Status = targetStatus;
            reg.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        response.Success = true;
        response.ValidationResult = validationResult; // May contain NeedsApproval warnings
        response.Registrations = registrations.Select(r => new RegistrationItemDto
        {
            Id = r.Id,
            CourseSectionId = r.CourseSectionId,
            CourseCode = r.CourseSection.Course.Code,
            CourseName = r.CourseSection.Course.Name,
            Ects = r.CourseSection.Course.Ects,
            Status = r.Status.ToString()
        }).ToList();

        return response;
    }

    public async Task<List<StudentRegistrationDto>> GetStudentRegistrationsAsync(int studentId)
    {
        var activeTerm = await _context.AcademicTerms.FirstOrDefaultAsync(t => t.IsActive);
        if (activeTerm == null)
            return new List<StudentRegistrationDto>();

        var registrations = await _context.CourseRegistrations
            .Include(cr => cr.CourseSection)
                .ThenInclude(cs => cs.Course)
            .Include(cr => cr.CourseSection)
                .ThenInclude(cs => cs.Instructor)
            .Where(cr => cr.StudentId == studentId
                && cr.CourseSection.TermId == activeTerm.Id)
            .ToListAsync();

        // Load instructor user names
        var instructorUserIds = registrations
            .Where(r => r.CourseSection.Instructor != null)
            .Select(r => r.CourseSection.Instructor!.UserId)
            .Distinct()
            .ToList();

        var userNames = await _context.Users
            .Where(u => instructorUserIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => $"{u.FirstName} {u.LastName}");

        // Load curriculum course info for compulsory/semester
        var student = await _context.Students
            .Include(s => s.StudentPrograms)
            .FirstOrDefaultAsync(s => s.Id == studentId);

        var mainProgramId = student?.StudentPrograms
            .FirstOrDefault(sp => sp.Type == StudentProgramType.Main && sp.IsActive)?.ProgramId;

        Dictionary<int, (bool IsCompulsory, int Semester)>? curriculumMap = null;
        if (mainProgramId.HasValue)
        {
            var curriculumCourses = await _context.CurriculumCourses
                .Include(cc => cc.Curriculum)
                .Where(cc => cc.Curriculum.ProgramId == mainProgramId.Value && cc.Curriculum.IsActive)
                .ToListAsync();

            curriculumMap = curriculumCourses.ToDictionary(
                cc => cc.CourseId,
                cc => (cc.IsCompulsory, cc.SuggestedSemester));
        }

        // Check for past failures to determine take type
        var courseIds = registrations.Select(r => r.CourseSection.CourseId).Distinct().ToList();
        var pastGrades = await _context.CourseRegistrations
            .Include(cr => cr.CourseSection)
            .Where(cr => cr.StudentId == studentId
                && cr.Grade != null
                && courseIds.Contains(cr.CourseSection.CourseId))
            .Select(cr => new { cr.CourseSection.CourseId, cr.Grade })
            .ToListAsync();

        var failedCourseIds = pastGrades
            .Where(g => g.Grade == GradeCode.FF || g.Grade == GradeCode.NA || g.Grade == GradeCode.U)
            .Select(g => g.CourseId)
            .ToHashSet();

        return registrations.Select(r =>
        {
            var courseId = r.CourseSection.CourseId;
            var isCompulsory = curriculumMap != null
                && curriculumMap.TryGetValue(courseId, out var info) && info.IsCompulsory;
            var semester = curriculumMap != null
                && curriculumMap.TryGetValue(courseId, out var info2) ? info2.Semester : 0;

            // Determine take type (Alış)
            string takeType;
            if (r.IsRepeated)
                takeType = "Yukseltme";
            else if (failedCourseIds.Contains(courseId))
                takeType = "Alttan";
            else if (isCompulsory)
                takeType = "Zorunlu";
            else
                takeType = "Secmeli";

            return new StudentRegistrationDto
            {
                RegistrationId = r.Id,
                CourseSectionId = r.CourseSectionId,
                CourseCode = r.CourseSection.Course.Code,
                CourseName = r.CourseSection.Course.Name,
                Ects = r.CourseSection.Course.Ects,
                TheoryHours = r.CourseSection.Course.TheoryHours,
                LabHours = r.CourseSection.Course.LabHours,
                InstructorName = r.CourseSection.Instructor != null
                    && userNames.TryGetValue(r.CourseSection.Instructor.UserId, out var name)
                        ? $"{r.CourseSection.Instructor.Title} {name}".Trim()
                        : string.Empty,
                Schedule = r.CourseSection.ScheduleJson,
                Status = r.Status.ToString(),
                IsCompulsory = isCompulsory,
                SuggestedSemester = semester,
                IsRepeated = r.IsRepeated,
                TakeType = takeType
            };
        }).ToList();
    }

    public async Task<List<CourseHistoryTermDto>> GetCourseHistoryAsync(int studentId)
    {
        var registrations = await _context.CourseRegistrations
            .Include(cr => cr.CourseSection)
                .ThenInclude(cs => cs.Course)
            .Include(cr => cr.CourseSection)
                .ThenInclude(cs => cs.Instructor)
            .Include(cr => cr.CourseSection)
                .ThenInclude(cs => cs.Term)
            .Where(cr => cr.StudentId == studentId
                && cr.Status != RegistrationStatus.Draft
                && cr.Status != RegistrationStatus.Dropped)
            .ToListAsync();

        // Load instructor user names
        var instructorUserIds = registrations
            .Where(r => r.CourseSection.Instructor != null)
            .Select(r => r.CourseSection.Instructor!.UserId)
            .Distinct()
            .ToList();

        var userNames = await _context.Users
            .Where(u => instructorUserIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => $"{u.FirstName} {u.LastName}");

        var grouped = registrations
            .GroupBy(r => r.CourseSection.TermId)
            .Select(g =>
            {
                var term = g.First().CourseSection.Term;
                return new CourseHistoryTermDto
                {
                    TermId = term.Id,
                    TermName = term.Name,
                    Year = term.Year,
                    Semester = term.Semester.ToString(),
                    Courses = g.Select(r => new CourseHistoryItemDto
                    {
                        RegistrationId = r.Id,
                        CourseCode = r.CourseSection.Course.Code,
                        CourseName = r.CourseSection.Course.Name,
                        Ects = r.CourseSection.Course.Ects,
                        InstructorName = r.CourseSection.Instructor != null
                            && userNames.TryGetValue(r.CourseSection.Instructor.UserId, out var name)
                                ? $"{r.CourseSection.Instructor.Title} {name}".Trim()
                                : string.Empty,
                        Grade = r.Grade?.ToString(),
                        Status = r.Status.ToString(),
                        IsRepeated = r.IsRepeated
                    }).OrderBy(c => c.CourseCode).ToList()
                };
            })
            .OrderByDescending(t => t.Year)
                .ThenByDescending(t => t.Semester)
            .ToList();

        return grouped;
    }

    public async Task<RegistrationResponseDto> DropCourseAsync(int studentId, int registrationId)
    {
        var response = new RegistrationResponseDto();

        var registration = await _context.CourseRegistrations
            .Include(cr => cr.CourseSection)
                .ThenInclude(cs => cs.Course)
            .Include(cr => cr.CourseSection)
                .ThenInclude(cs => cs.Term)
            .FirstOrDefaultAsync(cr => cr.Id == registrationId && cr.StudentId == studentId);

        if (registration == null)
        {
            response.Success = false;
            response.ValidationResult = new ValidationResultDto
            {
                IsValid = false,
                Errors = new List<ValidationError>
                {
                    new() { Rule = "RegistrationExists", Message = "Kayit bulunamadi." }
                }
            };
            return response;
        }

        // Check status is droppable
        if (registration.Status != RegistrationStatus.Active
            && registration.Status != RegistrationStatus.PendingAdvisorApproval
            && registration.Status != RegistrationStatus.Draft)
        {
            response.Success = false;
            response.ValidationResult = new ValidationResultDto
            {
                IsValid = false,
                Errors = new List<ValidationError>
                {
                    new() { Rule = "DropStatus", Message = $"'{registration.Status}' durumundaki ders birakılamaz." }
                }
            };
            return response;
        }

        // Check add-drop period (FR-48)
        var term = registration.CourseSection.Term;
        var now = DateTime.UtcNow;
        if (now < term.AddDropStart || now > term.AddDropEnd)
        {
            // Allow dropping Draft items anytime, but Active/PendingAdvisorApproval only during add-drop
            if (registration.Status != RegistrationStatus.Draft)
            {
                response.Success = false;
                response.ValidationResult = new ValidationResultDto
                {
                    IsValid = false,
                    Errors = new List<ValidationError>
                    {
                        new()
                        {
                            Rule = "AddDropPeriod",
                            Message = $"Ders birakma donemi disinda. Ders birakma donemi: {term.AddDropStart:dd.MM.yyyy} - {term.AddDropEnd:dd.MM.yyyy}"
                        }
                    }
                };
                return response;
            }
        }

        // Set status to Dropped
        registration.Status = RegistrationStatus.Dropped;
        registration.UpdatedAt = DateTime.UtcNow;

        // Decrement section.EnrolledCount
        var section = registration.CourseSection;
        if (section.EnrolledCount > 0)
            section.EnrolledCount--;

        await _context.SaveChangesAsync();

        response.Success = true;
        response.Registrations = new List<RegistrationItemDto>
        {
            new()
            {
                Id = registration.Id,
                CourseSectionId = section.Id,
                CourseCode = section.Course.Code,
                CourseName = section.Course.Name,
                Ects = section.Course.Ects,
                Status = registration.Status.ToString()
            }
        };

        return response;
    }

    public async Task<RegistrationResponseDto> WithdrawCourseAsync(int studentId, int registrationId)
    {
        var response = new RegistrationResponseDto();
        var errors = new List<ValidationError>();

        var registration = await _context.CourseRegistrations
            .Include(cr => cr.CourseSection)
                .ThenInclude(cs => cs.Course)
            .Include(cr => cr.CourseSection)
                .ThenInclude(cs => cs.Term)
            .FirstOrDefaultAsync(cr => cr.Id == registrationId && cr.StudentId == studentId);

        if (registration == null)
        {
            response.Success = false;
            response.ValidationResult = new ValidationResultDto
            {
                IsValid = false,
                Errors = new List<ValidationError>
                {
                    new() { Rule = "RegistrationExists", Message = "Kayit bulunamadi." }
                }
            };
            return response;
        }

        // Only active registrations can be withdrawn
        if (registration.Status != RegistrationStatus.Active)
        {
            response.Success = false;
            response.ValidationResult = new ValidationResultDto
            {
                IsValid = false,
                Errors = new List<ValidationError>
                {
                    new() { Rule = "WithdrawStatus", Message = "Sadece 'Active' durumundaki derslerden cekilebilir." }
                }
            };
            return response;
        }

        var term = registration.CourseSection.Term;
        var student = await _context.Students
            .Include(s => s.StudentPrograms)
                .ThenInclude(sp => sp.Program)
            .FirstOrDefaultAsync(s => s.Id == studentId);

        if (student == null)
        {
            response.Success = false;
            response.ValidationResult = new ValidationResultDto
            {
                IsValid = false,
                Errors = new List<ValidationError>
                {
                    new() { Rule = "StudentExists", Message = "Ogrenci bulunamadi." }
                }
            };
            return response;
        }

        // FR-57: Check withdrawal deadline
        if (DateTime.UtcNow > term.WithdrawalDeadline)
        {
            errors.Add(new ValidationError
            {
                Rule = "WithdrawalDeadline",
                Message = $"Ders çekilme süresi doldu. Son çekilme tarihi: {term.WithdrawalDeadline:dd.MM.yyyy}."
            });
        }

        // FR-58: Cannot withdraw if it's the only course (BR-11)
        var activeRegistrationsCount = await _context.CourseRegistrations
            .CountAsync(cr => cr.StudentId == studentId
                && cr.CourseSection.TermId == term.Id
                && cr.Status == RegistrationStatus.Active);

        if (activeRegistrationsCount <= 1)
        {
            errors.Add(new ValidationError
            {
                Rule = "LastCourse",
                Message = "Son dersinizden cekilemezsiniz. En az bir dersiniz aktif olmalidir."
            });
        }

        // FR-59: Cannot withdraw from the same course more than once
        if (registration.CourseWithdrawalCount >= 1)
        {
            errors.Add(new ValidationError
            {
                Rule = "DuplicateWithdrawal",
                Message = "Ayni dersten birden fazla cekilemezsiniz."
            });
        }

        // FR-60: Cannot withdraw in first two semesters (undergraduate)
        if (student.CompletedSemesters < 2)
        {
            var mainProgram = student.StudentPrograms.FirstOrDefault(sp => sp.Type == StudentProgramType.Main && sp.IsActive);
            if (mainProgram?.Program != null &&
                (mainProgram.Program.DegreeLevel == DegreeLevel.Bachelor || mainProgram.Program.DegreeLevel == DegreeLevel.Associate))
            {
                errors.Add(new ValidationError
                {
                    Rule = "FirstTwoSemesters",
                    Message = "Ilk iki donemde ders cekilme islemi yapilamaz."
                });
            }
        }

        // FR-61: Cannot withdraw from mandatory retake courses
        var allPastRegistrations = await _context.CourseRegistrations
            .Include(cr => cr.CourseSection)
                .ThenInclude(cs => cs.Course)
            .Where(cr => cr.StudentId == studentId && cr.Grade != null)
            .ToListAsync();

        var courseId = registration.CourseSection.CourseId;
        var isFailedRetake = allPastRegistrations.Any(r =>
            r.CourseSection.CourseId == courseId &&
            (r.Grade == GradeCode.FF || r.Grade == GradeCode.NA || r.Grade == GradeCode.U));

        // Check if it was subsequently passed
        var wasSubsequentlyPassed = allPastRegistrations.Any(r =>
            r.CourseSection.CourseId == courseId &&
            r.Grade != null &&
            r.Grade != GradeCode.FF &&
            r.Grade != GradeCode.NA &&
            r.Grade != GradeCode.U &&
            r.Grade != GradeCode.W &&
            r.Grade != GradeCode.I);

        if (isFailedRetake && !wasSubsequentlyPassed)
        {
            errors.Add(new ValidationError
            {
                Rule = "MandatoryRetake",
                Message = "Basarisiz oldugunuz zorunlu tekrar derslerden cekilemezsiniz."
            });
        }

        // FR-62: Max withdrawals: 6 for bachelor, 3 for associate
        var mainProg = student.StudentPrograms.FirstOrDefault(sp => sp.Type == StudentProgramType.Main && sp.IsActive);
        if (mainProg?.Program != null)
        {
            int maxWithdrawals = mainProg.Program.DegreeLevel == DegreeLevel.Associate ? 3 : 6;
            if (student.WithdrawalCount >= maxWithdrawals)
            {
                errors.Add(new ValidationError
                {
                    Rule = "MaxWithdrawals",
                    Message = $"Maksimum ders cekilme hakkini ({maxWithdrawals}) kullandiniz."
                });
            }
        }

        if (errors.Any())
        {
            response.Success = false;
            response.ValidationResult = new ValidationResultDto
            {
                IsValid = false,
                Errors = errors
            };
            return response;
        }

        // Set status to Withdrawn, assign W grade
        registration.Status = RegistrationStatus.Withdrawn;
        registration.Grade = GradeCode.W;
        registration.CourseWithdrawalCount++;
        registration.UpdatedAt = DateTime.UtcNow;

        // Decrement section enrolled count
        var sec = registration.CourseSection;
        if (sec.EnrolledCount > 0)
            sec.EnrolledCount--;

        // Increment student's cumulative withdrawal count
        student.WithdrawalCount++;

        await _context.SaveChangesAsync();

        response.Success = true;
        response.Registrations = new List<RegistrationItemDto>
        {
            new()
            {
                Id = registration.Id,
                CourseSectionId = sec.Id,
                CourseCode = sec.Course.Code,
                CourseName = sec.Course.Name,
                Ects = sec.Course.Ects,
                Status = registration.Status.ToString()
            }
        };

        return response;
    }

    public async Task<List<PendingApprovalDto>> GetPendingApprovalsAsync(int advisorInstructorId)
    {
        var activeTerm = await _context.AcademicTerms.FirstOrDefaultAsync(t => t.IsActive);
        if (activeTerm == null)
            return new List<PendingApprovalDto>();

        // Find active students whose advisor is this instructor
        var advisorStudentIds = await _context.Students
            .Where(s => s.AdvisorId == advisorInstructorId && s.Status == StudentStatus.Active)
            .Select(s => s.Id)
            .ToListAsync();

        if (!advisorStudentIds.Any())
            return new List<PendingApprovalDto>();

        // Get their PendingAdvisorApproval registrations for the active term
        var registrations = await _context.CourseRegistrations
            .Include(cr => cr.CourseSection)
                .ThenInclude(cs => cs.Course)
            .Include(cr => cr.CourseSection)
                .ThenInclude(cs => cs.Instructor)
            .Where(cr => advisorStudentIds.Contains(cr.StudentId)
                && cr.CourseSection.TermId == activeTerm.Id
                && cr.Status == RegistrationStatus.PendingAdvisorApproval)
            .ToListAsync();

        if (!registrations.Any())
            return new List<PendingApprovalDto>();

        // Load student user names
        var studentIds = registrations.Select(r => r.StudentId).Distinct().ToList();
        var students = await _context.Students
            .Where(s => studentIds.Contains(s.Id))
            .ToListAsync();

        var studentUserIds = students.Select(s => s.UserId).Distinct().ToList();

        // Load instructor user ids from the sections
        var instructorUserIds = registrations
            .Where(r => r.CourseSection.Instructor != null)
            .Select(r => r.CourseSection.Instructor!.UserId)
            .Distinct()
            .ToList();

        var allUserIds = studentUserIds.Union(instructorUserIds).ToList();
        var users = await _context.Users
            .Where(u => allUserIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => $"{u.FirstName} {u.LastName}");

        var studentDict = students.ToDictionary(s => s.Id);

        // Group by student, build PendingApprovalDto
        var result = registrations
            .GroupBy(r => r.StudentId)
            .Select(g =>
            {
                var student = studentDict[g.Key];
                users.TryGetValue(student.UserId, out var studentName);

                var regItems = g.Select(r =>
                {
                    var instructor = r.CourseSection.Instructor;
                    var instructorName = string.Empty;
                    if (instructor != null && users.TryGetValue(instructor.UserId, out var iName))
                        instructorName = $"{instructor.Title} {iName}".Trim();

                    return new PendingRegistrationItemDto
                    {
                        RegistrationId = r.Id,
                        CourseSectionId = r.CourseSectionId,
                        CourseCode = r.CourseSection.Course.Code,
                        CourseName = r.CourseSection.Course.Name,
                        Ects = r.CourseSection.Course.Ects,
                        Schedule = r.CourseSection.ScheduleJson,
                        InstructorName = instructorName,
                        IsRepeated = r.IsRepeated,
                        Status = r.Status.ToString()
                    };
                }).ToList();

                // Compute HasScheduleConflict between each pair of pending registrations
                var scheduleSlots = regItems
                    .Select(r => new { Item = r, Slots = JsonSerializer.Deserialize<List<ScheduleSlot>>(r.Schedule) ?? new List<ScheduleSlot>() })
                    .ToList();

                for (int i = 0; i < scheduleSlots.Count; i++)
                {
                    for (int j = i + 1; j < scheduleSlots.Count; j++)
                    {
                        if (RegistrationRulePolicy.CheckScheduleOverlap(scheduleSlots[i].Slots, scheduleSlots[j].Slots) != ConflictOverlapType.None)
                        {
                            scheduleSlots[i].Item.HasScheduleConflict = true;
                            scheduleSlots[j].Item.HasScheduleConflict = true;
                        }
                    }
                }

                return new PendingApprovalDto
                {
                    StudentId = student.Id,
                    StudentNumber = student.StudentNumber,
                    StudentName = studentName ?? string.Empty,
                    Cgpa = student.Cgpa,
                    CompletedSemesters = student.CompletedSemesters,
                    IsScholarshipStudent = student.IsScholarshipStudent,
                    Registrations = regItems,
                    TotalEcts = regItems.Sum(r => r.Ects),
                    SubmittedAt = g.Max(r => r.UpdatedAt ?? r.CreatedAt)
                };
            })
            .OrderBy(dto => dto.StudentName)
            .ToList();

        return result;
    }

    public async Task<ApprovalResponseDto> ApproveStudentRegistrationsAsync(int advisorInstructorId, ApprovalActionDto dto)
    {
        // Verify the student's advisor is this instructor
        var student = await _context.Students.FindAsync(dto.StudentId);
        if (student == null || student.AdvisorId != advisorInstructorId)
        {
            return new ApprovalResponseDto
            {
                Success = false,
                Message = "Bu ogrencinin danismani degilsiniz veya ogrenci bulunamadi.",
                AffectedRegistrations = 0
            };
        }

        var activeTerm = await _context.AcademicTerms.FirstOrDefaultAsync(t => t.IsActive);
        if (activeTerm == null)
        {
            return new ApprovalResponseDto
            {
                Success = false,
                Message = "Aktif donem bulunamadi.",
                AffectedRegistrations = 0
            };
        }

        // Find PendingAdvisorApproval registrations for this student in active term
        var registrations = await _context.CourseRegistrations
            .Include(cr => cr.CourseSection)
            .Where(cr => cr.StudentId == dto.StudentId
                && cr.CourseSection.TermId == activeTerm.Id
                && cr.Status == RegistrationStatus.PendingAdvisorApproval)
            .ToListAsync();

        foreach (var reg in registrations)
        {
            reg.Status = RegistrationStatus.Active;
            reg.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        return new ApprovalResponseDto
        {
            Success = true,
            Message = $"{registrations.Count} kayit onaylandi.",
            AffectedRegistrations = registrations.Count
        };
    }

    public async Task<ApprovalResponseDto> RejectStudentRegistrationsAsync(int advisorInstructorId, ApprovalActionDto dto)
    {
        // Verify the student's advisor is this instructor
        var student = await _context.Students.FindAsync(dto.StudentId);
        if (student == null || student.AdvisorId != advisorInstructorId)
        {
            return new ApprovalResponseDto
            {
                Success = false,
                Message = "Bu ogrencinin danismani degilsiniz veya ogrenci bulunamadi.",
                AffectedRegistrations = 0
            };
        }

        var activeTerm = await _context.AcademicTerms.FirstOrDefaultAsync(t => t.IsActive);
        if (activeTerm == null)
        {
            return new ApprovalResponseDto
            {
                Success = false,
                Message = "Aktif donem bulunamadi.",
                AffectedRegistrations = 0
            };
        }

        // Find PendingAdvisorApproval registrations (Include CourseSection for EnrolledCount)
        var registrations = await _context.CourseRegistrations
            .Include(cr => cr.CourseSection)
            .Where(cr => cr.StudentId == dto.StudentId
                && cr.CourseSection.TermId == activeTerm.Id
                && cr.Status == RegistrationStatus.PendingAdvisorApproval)
            .ToListAsync();

        foreach (var reg in registrations)
        {
            reg.Status = RegistrationStatus.Dropped;
            reg.UpdatedAt = DateTime.UtcNow;
            reg.CourseSection.EnrolledCount = Math.Max(0, reg.CourseSection.EnrolledCount - 1);
        }

        await _context.SaveChangesAsync();

        return new ApprovalResponseDto
        {
            Success = true,
            Message = $"{registrations.Count} kayit reddedildi.",
            AffectedRegistrations = registrations.Count
        };
    }

    private static string GetTermStatusLabel(TermStatus s) => s switch
    {
        TermStatus.Draft            => "Taslak",
        TermStatus.Planning         => "Planlama",
        TermStatus.Scheduling       => "Programlama",
        TermStatus.Published        => "Yayınlandı",
        TermStatus.RegistrationOpen => "Kayıt Açık",
        TermStatus.AddDrop          => "Ders Ekle/Bırak",
        TermStatus.WithdrawalPeriod => "Çekilme Dönemi",
        TermStatus.GradeEntry       => "Not Girişi",
        TermStatus.Closed           => "Kapalı",
        TermStatus.Archived         => "Arşivlendi",
        _                           => s.ToString()
    };
}
