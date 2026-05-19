using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SIS.Application.AdvisorConstraints.Services;
using SIS.Application.Common.Time;
using SIS.Application.CourseRegistration.DTOs;
using SIS.Application.CourseRegistration.Services;
using SIS.Application.Common.Services;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services;

/// <summary>
/// Öğrenci ders kayıt servisi. Partial class:
///   - CourseRegistrationService.cs              (ctor, alanlar, sorgular, küçük operasyonlar)
///   - CourseRegistrationService.PlanAdd.cs      (AddToPlanAsync — büyük plan ekleme akışı)
///   - CourseRegistrationService.Submit.cs       (SubmitRegistrationAsync — gönderme akışı)
///   - CourseRegistrationService.Approval.cs     (danışman onay/red akışları)
/// </summary>
public partial class CourseRegistrationService : ICourseRegistrationService
{
    private readonly AppDbContext _context;
    private readonly IRegistrationValidationService _validationService;
    private readonly ISystemConfigProvider _config;
    private readonly IAdvisorConstraintService? _advisorConstraintService;
    private readonly IDateTimeProvider _clock;
    private readonly IWebModuleSettingsProvider _webSettings;
    private readonly IWebModuleControlService _webModuleControls;
    private static readonly TimeZoneInfo TurkeyTimeZone = ResolveTurkeyTimeZone();

    public CourseRegistrationService(
        AppDbContext context,
        IRegistrationValidationService validationService,
        IDateTimeProvider clock,
        ISystemConfigProvider? config = null,
        IAdvisorConstraintService? advisorConstraintService = null,
        IWebModuleSettingsProvider? webSettings = null,
        IWebModuleControlService? webModuleControls = null)
    {
        _context = context;
        _validationService = validationService;
        _clock = clock;
        _config = config ?? new NullSystemConfigProvider();
        _advisorConstraintService = advisorConstraintService;
        _webSettings = webSettings ?? new NullWebModuleSettingsProvider();
        _webModuleControls = webModuleControls ?? new NullWebModuleControlService();
    }

    public async Task<List<StudentRegistrationDto>> GetStudentRegistrationsAsync(int studentId)
    {
        var now = _clock.UtcNow;
        var activeTerm = await _context.AcademicTerms.FirstOrDefaultAsync(t => !t.IsCancelled && t.TermStart <= now && t.TermEnd >= now);
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

        // Load SectionSchedule records for schedule display
        var regSectionIds = registrations.Select(r => r.CourseSectionId).Distinct().ToList();
        var sectionSchedules = await _context.SectionSchedules
            .AsNoTracking()
            .Include(ss => ss.TimeSlot)
            .Where(ss => regSectionIds.Contains(ss.CourseSectionId))
            .ToListAsync();
        var schedulesBySectionId = sectionSchedules
            .GroupBy(ss => ss.CourseSectionId)
            .ToDictionary(g => g.Key, g => ScheduleHelper.FormatSchedule(g));

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
                Schedule = schedulesBySectionId.GetValueOrDefault(r.CourseSectionId, string.Empty),
                Status = r.Status.ToString(),
                AdvisorNote = r.AdvisorNote,
                IsCompulsory = isCompulsory,
                SuggestedSemester = semester,
                IsRepeated = r.IsRepeated,
                TakeType = takeType
            };
        }).ToList();
    }

    public async Task<StudentAttendanceStatusDto?> GetStudentAttendanceStatusAsync(int studentId, int? termId = null)
    {
        var registeredTermRows = await _context.CourseRegistrations
            .AsNoTracking()
            .Where(cr => cr.StudentId == studentId
                && cr.Status != RegistrationStatus.Draft
                && cr.Status != RegistrationStatus.Dropped)
            .Select(cr => new
            {
                TermId = cr.CourseSection.Term.Id,
                TermName = cr.CourseSection.Term.Name,
                cr.CourseSection.Term.Year,
                cr.CourseSection.Term.TermType,
                IsActive = !cr.CourseSection.Term.IsCancelled
                    && cr.CourseSection.Term.TermStart <= DateTime.UtcNow
                    && cr.CourseSection.Term.TermEnd >= DateTime.UtcNow
            })
            .Distinct()
            .ToListAsync();

        var availableTerms = registeredTermRows
            .GroupBy(term => term.TermId)
            .Select(group =>
            {
                var term = group.First();
                return new StudentAttendanceTermDto
                {
                    TermId = term.TermId,
                    TermName = term.TermName,
                    Year = term.Year,
                    Semester = term.TermType.ToString(),
                    IsActive = term.IsActive
                };
            })
            .OrderByDescending(term => term.IsActive)
            .ThenByDescending(term => term.Year)
            .ThenByDescending(term => term.TermId)
            .ToList();

        var selectedTerm = termId.HasValue
            ? availableTerms.FirstOrDefault(term => term.TermId == termId.Value)
            : availableTerms.FirstOrDefault(term => term.IsActive) ?? availableTerms.FirstOrDefault();

        if (selectedTerm == null && termId.HasValue)
            return null;

        if (selectedTerm == null)
        {
            var nowAttendance = _clock.UtcNow;
            var activeTerm = await _context.AcademicTerms
                .AsNoTracking()
                .FirstOrDefaultAsync(t => !t.IsCancelled && t.TermStart <= nowAttendance && t.TermEnd >= nowAttendance);

            if (activeTerm == null)
                return null;

            selectedTerm = new StudentAttendanceTermDto
            {
                TermId = activeTerm.Id,
                TermName = activeTerm.Name,
                Year = activeTerm.Year,
                Semester = activeTerm.TermType.ToString(),
                IsActive = activeTerm.IsActive(nowAttendance)
            };
            availableTerms.Add(selectedTerm);
        }

        var weekCount = Math.Max(1, await _config.GetIntAsync("Attendance.TermWeekCount", 14));
        var theoryLimitPercent = ClampPercent(await _config.GetDecimalAsync("Attendance.TheoryAbsenceLimitPercent", 30m));
        var labLimitPercent = ClampPercent(await _config.GetDecimalAsync("Attendance.LabAbsenceLimitPercent", 20m));

        var student = await _context.Students
            .AsNoTracking()
            .Include(s => s.StudentPrograms)
                .ThenInclude(sp => sp.Program)
                    .ThenInclude(p => p.Department)
            .FirstOrDefaultAsync(s => s.Id == studentId);

        var activeStudentProgram = student?.StudentPrograms
            .FirstOrDefault(sp => sp.Type == StudentProgramType.Main && sp.IsActive);
        var activeProgramId = activeStudentProgram?.ProgramId;
        var activeProgramName = activeStudentProgram?.Program?.Name ?? string.Empty;

        var curriculumMap = new Dictionary<int, int>();
        if (activeProgramId.HasValue)
        {
            var curriculumCourses = await _context.CurriculumCourses
                .AsNoTracking()
                .Include(cc => cc.Curriculum)
                .Where(cc => cc.Curriculum.ProgramId == activeProgramId.Value && cc.Curriculum.IsActive)
                .Select(cc => new { cc.CourseId, cc.SuggestedSemester })
                .ToListAsync();

            curriculumMap = curriculumCourses
                .GroupBy(cc => cc.CourseId)
                .ToDictionary(group => group.Key, group => group.First().SuggestedSemester);
        }

        var registrations = await _context.CourseRegistrations
            .AsNoTracking()
            .Include(cr => cr.CourseSection)
                .ThenInclude(cs => cs.Course)
                    .ThenInclude(c => c.Department)
            .Where(cr => cr.StudentId == studentId
                && cr.CourseSection.TermId == selectedTerm.TermId
                && cr.Status != RegistrationStatus.Draft
                && cr.Status != RegistrationStatus.Dropped)
            .OrderBy(cr => cr.CourseSection.Course.Code)
            .ThenBy(cr => cr.CourseSection.SectionNumber)
            .ToListAsync();

        var registrationIds = registrations.Select(registration => registration.Id).ToList();
        var attendanceRecords = registrationIds.Count == 0
            ? new List<SIS.Domain.Entities.AttendanceRecord>()
            : await _context.AttendanceRecords
                .AsNoTracking()
                .Where(record => registrationIds.Contains(record.CourseRegistrationId))
                .ToListAsync();

        var attendanceByRegistration = attendanceRecords
            .GroupBy(record => record.CourseRegistrationId)
            .ToDictionary(group => group.Key, group => group.ToList());

        return new StudentAttendanceStatusDto
        {
            TermId = selectedTerm.TermId,
            TermName = selectedTerm.TermName,
            Year = selectedTerm.Year,
            Semester = selectedTerm.Semester,
            AvailableTerms = availableTerms,
            Policy = new StudentAttendancePolicyDto
            {
                TermWeekCount = weekCount,
                TheoryAbsenceLimitPercent = theoryLimitPercent,
                LabAbsenceLimitPercent = labLimitPercent
            },
            Courses = registrations.Select(registration =>
            {
                var course = registration.CourseSection.Course;
                var theoryTotalHours = weekCount * Math.Max(0, course.TheoryHours);
                var labTotalHours = weekCount * Math.Max(0, course.LabHours);
                var recordsForRegistration = attendanceByRegistration.GetValueOrDefault(registration.Id) ?? [];
                var absentSessions = recordsForRegistration.Count(record => record.Status == AttendanceStatus.Absent);
                var theoryAbsenceHours = absentSessions * Math.Max(0, course.TheoryHours);
                var labAbsenceHours = absentSessions * Math.Max(0, course.LabHours);
                var totalAbsenceHours = theoryAbsenceHours + labAbsenceHours;
                var isInStudentProgram = curriculumMap.TryGetValue(course.Id, out var suggestedSemester);
                var programName = isInStudentProgram && !string.IsNullOrWhiteSpace(activeProgramName)
                    ? activeProgramName
                    : course.Department?.Name ?? "Ortak Dersler";

                return new StudentAttendanceCourseDto
                {
                    RegistrationId = registration.Id,
                    CourseSectionId = registration.CourseSectionId,
                    SectionNumber = registration.CourseSection.SectionNumber,
                    CourseCode = course.Code,
                    CourseName = course.Name,
                    Ects = course.Ects,
                    NationalCredit = course.NationalCredit,
                    SuggestedSemester = suggestedSemester,
                    ProgramName = programName,
                    TheoryHours = course.TheoryHours,
                    LabHours = course.LabHours,
                    MandatoryAttendance = registration.MandatoryAttendance,
                    AttendanceEntered = recordsForRegistration.Count > 0,
                    TheoryAbsenceHours = recordsForRegistration.Count > 0 ? theoryAbsenceHours : null,
                    LabAbsenceHours = recordsForRegistration.Count > 0 ? labAbsenceHours : null,
                    TotalAbsenceHours = recordsForRegistration.Count > 0 ? totalAbsenceHours : null,
                    TotalTheoryHours = theoryTotalHours,
                    TotalLabHours = labTotalHours,
                    TheoryAbsenceLimitHours = CalculateLimitHours(theoryTotalHours, theoryLimitPercent),
                    LabAbsenceLimitHours = CalculateLimitHours(labTotalHours, labLimitPercent),
                    AttendanceStatusLabel = recordsForRegistration.Count > 0
                        ? $"{totalAbsenceHours} Saat"
                        : "Yoklama Girişi Yapılmamış"
                };
            }).ToList()
        };
    }

    public async Task<List<CourseHistoryTermDto>> GetCourseHistoryAsync(int studentId)
    {
        var (terms, _) = await LoadCourseHistoryCoreAsync(studentId);
        return terms;
    }

    /// <summary>
    /// "Alınan Dersler" PDF raporu için öğrenci bilgi bloğu + dönem listesi + alt toplam.
    /// </summary>
    public async Task<CourseHistoryReportDto?> GetCourseHistoryReportAsync(int studentId)
    {
        var (terms, studentInfo) = await LoadCourseHistoryCoreAsync(studentId);
        if (studentInfo == null)
            return null;

        // Alt toplam — tüm dönemler birleşik
        var allCourses = terms.SelectMany(t => t.Courses).ToList();

        return new CourseHistoryReportDto
        {
            StudentNumber = studentInfo.StudentNumber,
            FullName = studentInfo.FullName,
            CurrentClassYear = studentInfo.CurrentClassYear,
            FacultyName = studentInfo.FacultyName,
            ProgramName = studentInfo.ProgramName,
            AdvisorName = studentInfo.AdvisorName,
            ActiveTermName = studentInfo.ActiveTermName,
            Terms = terms,
            TotalCourses = allCourses.Count,
            TotalTheoryHours = allCourses.Sum(c => c.TheoryHours + c.LabHours + (c.ApplicationHours ?? 0)),
            TotalCredit = allCourses.Sum(c => c.NationalCredit),
            TotalEcts = allCourses.Sum(c => c.Ects)
        };
    }

    /// <summary>
    /// İçi: GetCourseHistoryAsync + GetCourseHistoryReportAsync ortak akışı.
    /// Tüm join'ler ve AcquisitionType hesabı tek yerde toplanır.
    /// </summary>
    private async Task<(List<CourseHistoryTermDto> Terms, CourseHistoryStudentInfo? StudentInfo)>
        LoadCourseHistoryCoreAsync(int studentId)
    {
        // Öğrenci + program + curriculum bilgisi (rapor üst bloğu + ClassYear hesabı için)
        var student = await _context.Students
            .AsNoTracking()
            .Include(s => s.StudentPrograms)
                .ThenInclude(sp => sp.Program)
                    .ThenInclude(p => p.Department)
                        .ThenInclude(d => d.Faculty)
            .Include(s => s.Advisor)
            .FirstOrDefaultAsync(s => s.Id == studentId);

        if (student == null)
            return (new List<CourseHistoryTermDto>(), null);

        var activeProgram = student.StudentPrograms.FirstOrDefault(sp => sp.IsActive);

        // Aktif curriculum'daki CurriculumCourse → ClassYear hesabı için
        Dictionary<int, CurriculumCourseInfo> curriculumMap = new();
        if (activeProgram != null)
        {
            curriculumMap = await _context.CurriculumCourses
                .AsNoTracking()
                .Where(cc => cc.CurriculumId == activeProgram.CurriculumId)
                .Select(cc => new CurriculumCourseInfo
                {
                    CourseId = cc.CourseId,
                    IsCompulsory = cc.IsCompulsory,
                    SuggestedSemester = cc.SuggestedSemester
                })
                .ToDictionaryAsync(x => x.CourseId);
        }

        // Tüm kayıtlar (Department/Course.Department dahil)
        var registrations = await _context.CourseRegistrations
            .AsNoTracking()
            .Include(cr => cr.CourseSection)
                .ThenInclude(cs => cs.Course)
                    .ThenInclude(c => c.Department)
            .Include(cr => cr.CourseSection)
                .ThenInclude(cs => cs.Instructor)
            .Include(cr => cr.CourseSection)
                .ThenInclude(cs => cs.Term)
            .Where(cr => cr.StudentId == studentId
                && cr.Status != RegistrationStatus.Draft
                && cr.Status != RegistrationStatus.Dropped)
            .ToListAsync();

        // Instructor user adları
        var instructorUserIds = registrations
            .Where(r => r.CourseSection.Instructor != null)
            .Select(r => r.CourseSection.Instructor!.UserId)
            .Distinct()
            .ToList();

        var userNames = await _context.Users
            .Where(u => instructorUserIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => $"{u.FirstName} {u.LastName}");

        // CourseId bazında önceki başarısızlıkları say (AcquisitionType için)
        // Sıra: Term.Year → Term.TermType (kronolojik) → Id (deterministik fallback)
        var registrationsByCourse = registrations
            .GroupBy(r => r.CourseSection.CourseId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(r => r.CourseSection.Term.Year)
                      .ThenBy(r => r.CourseSection.Term.TermType)
                      .ThenBy(r => r.Id)
                      .ToList());

        // Advisor adı (rapor üst bloğu)
        string? advisorName = null;
        if (student.Advisor != null)
        {
            var advisorUser = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == student.Advisor.UserId);
            if (advisorUser != null)
                advisorName = $"{student.Advisor.Title} {advisorUser.FirstName} {advisorUser.LastName}".Trim();
        }

        // Aktif dönem (rapor üst bloğu)
        var nowReport = _clock.UtcNow;
        var activeTermName = await _context.AcademicTerms
            .AsNoTracking()
            .Where(t => !t.IsCancelled && t.TermStart <= nowReport && t.TermEnd >= nowReport)
            .Select(t => t.Name)
            .FirstOrDefaultAsync();

        var studentInfo = new CourseHistoryStudentInfo
        {
            StudentNumber = student.StudentNumber,
            FullName = await ResolveStudentFullNameAsync(student.UserId),
            CurrentClassYear = (student.CompletedSemesters / 2) + 1,
            FacultyName = activeProgram?.Program.Department.Faculty.Name,
            ProgramName = activeProgram?.Program.Name,
            AdvisorName = advisorName,
            ActiveTermName = activeTermName
        };

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
                    Semester = term.TermType.ToString(),
                    Courses = g.Select(r =>
                    {
                        var course = r.CourseSection.Course;
                        curriculumMap.TryGetValue(course.Id, out var ccInfo);

                        return new CourseHistoryItemDto
                        {
                            RegistrationId = r.Id,
                            CourseSectionId = r.CourseSectionId,
                            CourseId = course.Id,
                            SectionNumber = r.CourseSection.SectionNumber,
                            CourseCode = course.Code,
                            CourseName = course.Name,
                            Ects = r.EctsSnapshot ?? course.Ects,
                            NationalCredit = r.CreditSnapshot ?? course.NationalCredit,
                            TheoryHours = course.TheoryHours,
                            LabHours = course.LabHours,
                            ApplicationHours = course.ApplicationHours,
                            ClassYear = ccInfo != null && ccInfo.SuggestedSemester > 0
                                ? ((ccInfo.SuggestedSemester - 1) / 2) + 1
                                : (int?)null,
                            AcquisitionType = ComputeAcquisitionType(r, registrationsByCourse[course.Id], ccInfo),
                            OwnerDepartmentName = course.Department?.Name,
                            LanguageDisplay = FormatCourseLanguage(course.Language),
                            InstructorName = r.CourseSection.Instructor != null
                                && userNames.TryGetValue(r.CourseSection.Instructor.UserId, out var name)
                                    ? $"{r.CourseSection.Instructor.Title} {name}".Trim()
                                    : string.Empty,
                            Grade = r.Grade?.ToString(),
                            Status = r.Status.ToString(),
                            IsRepeated = r.IsRepeated
                        };
                    }).OrderBy(c => c.CourseCode).ToList()
                };
            })
            .OrderByDescending(t => t.Year)
                .ThenByDescending(t => t.Semester)
            .ToList();

        return (grouped, studentInfo);
    }

    private async Task<string> ResolveStudentFullNameAsync(int userId)
    {
        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId);
        return user == null ? string.Empty : $"{user.FirstName} {user.LastName}".Trim();
    }

    /// <summary>
    /// "Alış Şekli" hesabı. Müfredattaki zorunluluk + önceki FF/NA sayısı + IsRepeated bayrağı.
    /// </summary>
    private static string ComputeAcquisitionType(
        Domain.Entities.CourseRegistration current,
        List<Domain.Entities.CourseRegistration> allRegistrationsForCourse,
        CurriculumCourseInfo? curriculumInfo)
    {
        // Mevcut kayıttan önceki başarısızlıklar (kronolojik)
        var currentIndex = allRegistrationsForCourse.FindIndex(r => r.Id == current.Id);
        var priorRegistrations = currentIndex > 0
            ? allRegistrationsForCourse.Take(currentIndex)
            : Enumerable.Empty<Domain.Entities.CourseRegistration>();

        var priorFFCount = priorRegistrations.Count(r => r.Grade == GradeCode.FF);
        var priorNACount = priorRegistrations.Count(r => r.Grade == GradeCode.NA);
        var totalPriorFails = priorFFCount + priorNACount;

        // İlk kez alıyor → müfredattaki tipe göre
        if (totalPriorFails == 0)
        {
            if (curriculumInfo == null)
                return "Seçmeli"; // müfredat dışı = seçmeli kabul
            return curriculumInfo.IsCompulsory ? "Zorunlu" : "Seçmeli";
        }

        // Önceden başarısız almış, tekrar alıyor
        // Hangi başarısızlık daha baskınsa onu yansıt
        var dominantFailGrade = priorFFCount >= priorNACount ? "FF" : "NA";

        // Birden fazla başarısızlık → "Devamlı"
        return totalPriorFails >= 2
            ? $"Devamlı Alttan {dominantFailGrade}"
            : $"Alttan {dominantFailGrade}";
    }

    private static string FormatCourseLanguage(Domain.Enums.CourseLanguage language) => language switch
    {
        Domain.Enums.CourseLanguage.Turkish => "Türkçe",
        Domain.Enums.CourseLanguage.English => "İngilizce",
        Domain.Enums.CourseLanguage.French  => "Fransızca",
        Domain.Enums.CourseLanguage.German  => "Almanca",
        Domain.Enums.CourseLanguage.Arabic  => "Arapça",
        _ => language.ToString()
    };

    private sealed class CourseHistoryStudentInfo
    {
        public string StudentNumber { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public int? CurrentClassYear { get; set; }
        public string? FacultyName { get; set; }
        public string? ProgramName { get; set; }
        public string? AdvisorName { get; set; }
        public string? ActiveTermName { get; set; }
    }

    private sealed class CurriculumCourseInfo
    {
        public int CourseId { get; set; }
        public bool IsCompulsory { get; set; }
        public int SuggestedSemester { get; set; }
    }



    private static decimal ClampPercent(decimal value)
    {
        if (value < 0m) return 0m;
        if (value > 100m) return 100m;
        return value;
    }

    private static int CalculateLimitHours(int totalHours, decimal limitPercent)
    {
        if (totalHours <= 0 || limitPercent <= 0m)
            return 0;

        return (int)Math.Ceiling(totalHours * limitPercent / 100m);
    }

    private async Task<ValidationResultDto?> ValidateStudentRegistrationWebModuleAsync(int studentId, int? termId = null)
    {
        var controlCode = await ResolveStudentRegistrationControlCodeAsync(studentId, termId);
        return await ValidateModuleAsync(controlCode, termId, "WebModuleRegistrationClosed", "Ders kayıt işlemleri şu anda kullanılamıyor.");
    }

    private async Task<ValidationResultDto?> ValidateStudentAddDropWebModuleAsync(int studentId, int? termId = null)
    {
        var controlCode = await ResolveStudentAddDropControlCodeAsync(studentId);
        return await ValidateModuleAsync(controlCode, termId, "WebModuleAddDropClosed", "Ders ekle/bırak işlemleri şu anda kullanılamıyor.");
    }

    private async Task<ValidationResultDto?> ValidateStudentWithdrawalWebModuleAsync(int studentId, int? termId = null)
    {
        return await ValidateModuleAsync("student-withdrawal", termId, "WebModuleWithdrawalClosed", "Dersten çekilme işlemleri şu anda kullanılamıyor.");
    }

    private async Task<string> ResolveStudentRegistrationControlCodeAsync(int studentId, int? termId)
    {
        var student = await LoadStudentModuleProfileAsync(studentId);
        if (student is null)
            return "student-course-registration";

        if (student.CompletedSemesters == 0)
            return "student-new-course-registration";

        return IsOverNormalStudyDuration(student)
            ? "student-extended-course-registration"
            : "student-course-registration";
    }

    private async Task<string> ResolveStudentAddDropControlCodeAsync(int studentId)
    {
        var student = await LoadStudentModuleProfileAsync(studentId);
        return student?.CompletedSemesters == 0
            ? "student-new-add-drop"
            : "student-add-drop";
    }

    private async Task<string> ResolveAdvisorApprovalControlCodeAsync(int studentId, int termId, DateTime nowUtc)
    {
        var term = await _context.AcademicTerms
            .AsNoTracking()
            .Include(item => item.AcademicCalendar)
            .FirstOrDefaultAsync(item => item.Id == termId);

        if (term?.IsAddDropOpen(nowUtc) == true)
            return "advisor-add-drop-approval";

        var student = await LoadStudentModuleProfileAsync(studentId);
        return student?.CompletedSemesters == 0
            ? "advisor-new-student-registration-approval"
            : "advisor-registration-approval";
    }

    private async Task<Domain.Entities.Student?> LoadStudentModuleProfileAsync(int studentId)
    {
        return await _context.Students
            .Include(student => student.StudentPrograms)
                .ThenInclude(program => program.Program)
            .FirstOrDefaultAsync(student => student.Id == studentId);
    }

    private static bool IsOverNormalStudyDuration(Domain.Entities.Student student)
    {
        var activeProgram = student.StudentPrograms
            .FirstOrDefault(program => program.Type == StudentProgramType.Main && program.IsActive);

        if (activeProgram?.Program == null)
            return false;

        var normalDurationSemesters = activeProgram.Program.DegreeLevel switch
        {
            DegreeLevel.Associate => 4,
            DegreeLevel.Bachelor => 8,
            DegreeLevel.Master => 4,
            DegreeLevel.Doctorate => 8,
            _ => 8
        };

        return student.CompletedSemesters >= normalDurationSemesters;
    }

    private async Task<ValidationResultDto?> ValidateModuleAsync(
        string controlCode,
        int? termId,
        string rule,
        string fallbackMessage)
    {
        var access = await _webModuleControls.EvaluateAsync(controlCode, termId);
        if (access.IsOpen)
            return null;

        return CreateWebModuleError(rule, access.Message ?? fallbackMessage);
    }

    private static ValidationResultDto CreateWebModuleError(string rule, string message)
    {
        return new ValidationResultDto
        {
            IsValid = false,
            Errors = new List<ValidationError>
            {
                new() { Rule = rule, Message = message }
            }
        };
    }

    private static TimeZoneInfo ResolveTurkeyTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Local;
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Local;
        }
    }

    private async Task<ValidationResultDto> EvaluateAdvisorConstraintsAsync(
        int studentId,
        IEnumerable<AdvisorConstraintSectionInfo> sections)
    {
        var result = new ValidationResultDto { IsValid = true };

        if (_advisorConstraintService == null)
            return result;

        foreach (var section in sections
            .GroupBy(s => new { s.CourseSectionId, s.CourseId, s.TermId })
            .Select(g => g.First()))
        {
            var effectiveConstraint = await _advisorConstraintService.GetEffectiveConstraintAsync(
                studentId,
                section.CourseId,
                section.TermId);

            if (effectiveConstraint == null)
                continue;

            if (effectiveConstraint.ConstraintType == SIS.Domain.Enums.AdvisorConstraintType.Block)
            {
                result.IsValid = false;
                result.Errors.Add(new ValidationError
                {
                    Rule = "AdvisorConstraintBlock",
                    Message = string.IsNullOrWhiteSpace(effectiveConstraint.Note)
                        ? $"Danisman kisiti nedeniyle '{section.CourseCode} {section.CourseName}' dersi secilemez."
                        : $"Danisman kisiti nedeniyle '{section.CourseCode} {section.CourseName}' dersi secilemez: {effectiveConstraint.Note}",
                    CourseSectionId = section.CourseSectionId
                });
                continue;
            }

            result.Warnings.Add(new ValidationError
            {
                Rule = "AdvisorConstraintWarning",
                Message = string.IsNullOrWhiteSpace(effectiveConstraint.Note)
                    ? $"Danismaniniz '{section.CourseCode} {section.CourseName}' dersini almanizi onermemektedir."
                    : $"Danismaniniz '{section.CourseCode} {section.CourseName}' dersi icin uyari koydu: {effectiveConstraint.Note}",
                CourseSectionId = section.CourseSectionId
            });
        }

        return result;
    }

    private sealed class AdvisorConstraintSectionInfo
    {
        public int CourseSectionId { get; init; }
        public int CourseId { get; init; }
        public int TermId { get; init; }
        public string CourseCode { get; init; } = string.Empty;
        public string CourseName { get; init; } = string.Empty;
    }

    private static string GetTermStatusLabel(TermStatus s) => s switch
    {
        TermStatus.Inactive   => "Pasif",
        TermStatus.Active     => "Aktif",
        TermStatus.Cancelled  => "İptal",
        _                     => s.ToString()
    };

    /// <summary>
    /// Bir dersin öğrencinin müfredatındaki AKTS + ulusal kredi değerini, (varsa) hangi havuza sayılacağını,
    /// ve (Faz 4) müfredat satırına atanmış Açma Nedeni id'sini çözer.
    /// Zorunlu/tekil seçmeli → katalog AKTS + katalog kredi + null havuz + müfredat satırının OpeningReasonId'si,
    /// havuz üyesi → havuzun EctsPerCourse (varsa) + katalog kredi + havuz id + null reason,
    /// müfredat dışı → katalog AKTS + katalog kredi + null havuz + null reason.
    /// Kayıt anında bu dört değer EctsSnapshot, CreditSnapshot, AppliedElectivePoolId, OpeningReasonIdSnapshot alanlarına donar.
    /// </summary>
    private async Task<(int? ectsSnapshot, int? creditSnapshot, int? appliedPoolId, int? openingReasonIdSnapshot)> ResolveCourseSnapshotsAsync(
        int studentId, int courseId, CancellationToken ct = default)
    {
        var activeProgram = await _context.StudentPrograms
            .AsNoTracking()
            .Where(sp => sp.StudentId == studentId && sp.Type == StudentProgramType.Main && sp.IsActive)
            .FirstOrDefaultAsync(ct);

        var catalogCourse = await _context.Courses.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == courseId, ct);
        var catalogEcts = catalogCourse?.Ects ?? 0;
        var catalogCredit = catalogCourse?.NationalCredit;

        if (activeProgram == null)
            return (catalogEcts, catalogCredit, null, null);

        // 1) Direkt müfredat ataması (zorunlu veya tekil seçmeli)
        var direct = await _context.CurriculumCourses.AsNoTracking()
            .Where(cc => cc.CurriculumId == activeProgram.CurriculumId && cc.CourseId == courseId)
            .Select(cc => new { cc.OpeningReasonId })
            .FirstOrDefaultAsync(ct);
        if (direct != null)
            return (catalogEcts, catalogCredit, null, direct.OpeningReasonId);

        // 2) Müfredatın havuzlarından birinde mi?
        var poolHit = await _context.CurriculumElectivePools.AsNoTracking()
            .Where(p => p.CurriculumId == activeProgram.CurriculumId
                && p.ElectiveGroup.Courses.Any(egc => egc.CourseId == courseId))
            .Select(p => new { p.Id, p.EctsPerCourse })
            .FirstOrDefaultAsync(ct);
        if (poolHit != null)
        {
            var ects = poolHit.EctsPerCourse ?? catalogEcts;
            return (ects, catalogCredit, poolHit.Id, null);
        }

        // 3) Müfredat dışı (warning olarak raporlanmıştı) — katalog AKTS + katalog kredi
        return (catalogEcts, catalogCredit, null, null);
    }
}
