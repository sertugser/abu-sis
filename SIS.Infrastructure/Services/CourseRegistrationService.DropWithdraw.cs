using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SIS.Application.AdvisorConstraints.Services;
using SIS.Application.CourseRegistration.DTOs;
using SIS.Domain.Enums;

namespace SIS.Infrastructure.Services;

/// <summary>
/// CourseRegistrationService — Drop / Withdraw akışları.
/// Drop: Add/Drop fazında ders bırakma (kayıttan tamamen kaldır).
/// Withdraw: çekilme (W harfi notu, transcript'te kalır).
/// </summary>
public partial class CourseRegistrationService
{
    public async Task<RegistrationResponseDto> DropCourseAsync(int studentId, int registrationId)
    {
        var response = new RegistrationResponseDto();
        var webModuleGate = await ValidateStudentRegistrationWebModuleAsync();
        if (webModuleGate is not null)
        {
            response.Success = false;
            response.ValidationResult = webModuleGate;
            return response;
        }

        var registration = await _context.CourseRegistrations
            .Include(cr => cr.CourseSection)
                .ThenInclude(cs => cs.Course)
            .Include(cr => cr.CourseSection)
                .ThenInclude(cs => cs.Term)
                    .ThenInclude(t => t.AcademicCalendar)
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

        // Guard: dönem AddDrop aşamasında olmalı
        var dropTerm = registration.CourseSection.Term;
        if (!dropTerm.IsAddDropOpen(_clock.UtcNow) && !dropTerm.IsRegistrationOpen(_clock.UtcNow))
        {
            response.Success = false;
            response.ValidationResult = new ValidationResultDto
            {
                IsValid = false,
                Errors = new List<ValidationError>
                {
                    new() { Rule = "TermStatus", Message = $"Ders bırakma sadece kayıt veya ekleme/bırakma döneminde yapılabilir." }
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
        if (!term.IsAddDropOpen(now))
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
                            Message = "Ders bırakma dönemi dışında. Ders bırakma dönemi kapalı."
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
                    .ThenInclude(t => t.AcademicCalendar)
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

        // Guard: dönem WithdrawalPeriod aşamasında olmalı
        var withdrawTerm = registration.CourseSection.Term;
        if (!withdrawTerm.IsWithdrawalOpen(_clock.UtcNow))
        {
            response.Success = false;
            response.ValidationResult = new ValidationResultDto
            {
                IsValid = false,
                Errors = new List<ValidationError>
                {
                    new() { Rule = "TermStatus", Message = $"Ders çekilme sadece çekilme döneminde yapılabilir." }
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
        if (!term.IsWithdrawalOpen(DateTime.UtcNow))
        {
            errors.Add(new ValidationError
            {
                Rule = "WithdrawalDeadline",
                Message = "Ders çekilme dönemi kapalı. Çekilme süresi dolmuş veya henüz başlamamış."
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
            var isAssociate = mainProg.Program.DegreeLevel == DegreeLevel.Associate;
            var maxWithdrawalsKey = isAssociate
                ? "Withdrawal.MaxCount.Associate"
                : "Withdrawal.MaxCount.Undergraduate";
            int maxWithdrawals = await _config.GetIntAsync(maxWithdrawalsKey, isAssociate ? 3 : 6);
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
}
