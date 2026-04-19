using Microsoft.EntityFrameworkCore;
using SIS.Application.CourseEvaluation.DTOs;
using SIS.Application.CourseEvaluation.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services.CourseEvaluation;

public class CourseEvaluationService : ICourseEvaluationService
{
    private const int MinResponsesForVisibility = 5;
    private readonly AppDbContext _context;

    public CourseEvaluationService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<EvaluationPeriodDto?> GetActivePeriodAsync(CancellationToken ct = default)
    {
        var period = await _context.EvaluationPeriods
            .AsNoTracking()
            .Include(p => p.Term)
            .Where(p => p.Status == EvaluationPeriodStatus.Open)
            .OrderByDescending(p => p.OpenedAt)
            .FirstOrDefaultAsync(ct);

        if (period == null)
            return null;

        var now = DateTime.UtcNow;
        var isCurrentlyOpen = period.Status == EvaluationPeriodStatus.Open
            && period.OpenedAt <= now
            && period.ClosesAt > now;

        return new EvaluationPeriodDto(
            period.Id,
            period.TermId,
            period.Term.Name,
            period.OpenedAt,
            period.ClosesAt,
            period.Status,
            isCurrentlyOpen);
    }

    public async Task<List<EvaluableCourseDto>> GetMyCoursesAsync(int userId, CancellationToken ct = default)
    {
        var activePeriod = await GetOpenPeriodEntityAsync(ct);
        if (activePeriod == null)
            return new List<EvaluableCourseDto>();

        var student = await _context.Students
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == userId, ct);

        if (student == null)
            return new List<EvaluableCourseDto>();

        var registrations = await _context.CourseRegistrations
            .AsNoTracking()
            .Include(cr => cr.CourseSection)
                .ThenInclude(cs => cs.Course)
            .Include(cr => cr.CourseSection)
                .ThenInclude(cs => cs.Instructor)
            .Where(cr => cr.StudentId == student.Id
                && cr.CourseSection.TermId == activePeriod.TermId
                && (cr.Status == RegistrationStatus.Active
                    || cr.Status == RegistrationStatus.Completed))
            .ToListAsync(ct);

        var responseMap = await _context.EvaluationResponses
            .AsNoTracking()
            .Where(r => r.EvaluationPeriodId == activePeriod.Id
                && r.StudentId == student.Id)
            .Select(r => new { r.CourseSectionId, r.SubmittedAt })
            .ToDictionaryAsync(r => r.CourseSectionId, r => (DateTime?)r.SubmittedAt, ct);

        var instructorUserIds = registrations
            .Where(r => r.CourseSection.Instructor != null)
            .Select(r => r.CourseSection.Instructor!.UserId)
            .Distinct()
            .ToList();

        var instructorNames = await _context.Users
            .AsNoTracking()
            .Where(u => instructorUserIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FirstName, u.LastName })
            .ToDictionaryAsync(u => u.Id, u => $"{u.FirstName} {u.LastName}".Trim(), ct);

        return registrations.Select(r =>
        {
            var section = r.CourseSection;
            var instructorName = section.Instructor != null
                && instructorNames.TryGetValue(section.Instructor.UserId, out var name)
                ? $"{section.Instructor.Title} {name}".Trim()
                : "Atanmamış";

            var isCompleted = responseMap.TryGetValue(section.Id, out var submittedAt);

            return new EvaluableCourseDto(
                section.Id,
                section.Course.Code,
                section.Course.Name,
                instructorName,
                isCompleted,
                submittedAt);
        }).ToList();
    }

    public async Task<List<EvaluationQuestionDto>> GetActiveQuestionsAsync(CancellationToken ct = default)
    {
        return await _context.EvaluationQuestions
            .AsNoTracking()
            .Where(q => q.IsActive)
            .OrderBy(q => q.Category)
            .ThenBy(q => q.SortOrder)
            .Select(q => new EvaluationQuestionDto(q.Id, q.Text, q.QuestionType, q.Category, q.SortOrder, q.IsActive))
            .ToListAsync(ct);
    }

    public async Task<(bool Success, string? ErrorMessage)> SubmitEvaluationAsync(int userId, SubmitEvaluationDto dto, CancellationToken ct = default)
    {
        var activePeriod = await GetOpenPeriodEntityAsync(ct);
        if (activePeriod == null)
            return (false, "Degerlendirme donemi su anda acik degil.");

        var now = DateTime.UtcNow;
        if (activePeriod.ClosesAt <= now)
            return (false, "Degerlendirme donemi kapandi.");

        var student = await _context.Students
            .FirstOrDefaultAsync(s => s.UserId == userId, ct);
        if (student == null)
            return (false, "Ogrenci bulunamadi.");

        var section = await _context.CourseSections
            .FirstOrDefaultAsync(cs => cs.Id == dto.CourseSectionId, ct);
        if (section == null)
            return (false, "Ders bulunamadi.");

        if (section.TermId != activePeriod.TermId)
            return (false, "Bu ders aktif degerlendirme donemine ait degil.");

        var hasRegistration = await _context.CourseRegistrations.AnyAsync(cr =>
            cr.StudentId == student.Id
            && cr.CourseSectionId == dto.CourseSectionId
            && (cr.Status == RegistrationStatus.Active
                || cr.Status == RegistrationStatus.Completed), ct);

        if (!hasRegistration)
            return (false, "Bu dersi degerlendirme yetkiniz yok.");

        var alreadyExists = await _context.EvaluationResponses.AnyAsync(r =>
            r.EvaluationPeriodId == activePeriod.Id
            && r.CourseSectionId == dto.CourseSectionId
            && r.StudentId == student.Id, ct);

        if (alreadyExists)
            return (false, "Bu dersi daha once degerlendirdiniz.");

        var questionIds = dto.Answers.Select(a => a.QuestionId).ToList();
        var questions = await _context.EvaluationQuestions
            .Where(q => questionIds.Contains(q.Id) && q.IsActive)
            .ToListAsync(ct);

        if (questions.Count != questionIds.Distinct().Count())
            return (false, "Gecersiz soru icerigi.");

        foreach (var answer in dto.Answers)
        {
            var question = questions.FirstOrDefault(q => q.Id == answer.QuestionId);
            if (question == null) continue;

            if (question.QuestionType == EvaluationQuestionType.Rating)
            {
                if (answer.RatingValue is not (>= 1 and <= 5))
                    return (false, "Puanlar 1-5 arasinda olmalidir.");
            }
        }

        var response = new EvaluationResponse
        {
            EvaluationPeriodId = activePeriod.Id,
            CourseSectionId = dto.CourseSectionId,
            StudentId = student.Id,
            SubmittedAt = DateTime.UtcNow,
            Answers = dto.Answers.Select(a => new EvaluationAnswer
            {
                QuestionId = a.QuestionId,
                RatingValue = a.RatingValue,
                TextValue = string.IsNullOrWhiteSpace(a.TextValue) ? null : a.TextValue.Trim()
            }).ToList()
        };

        _context.EvaluationResponses.Add(response);
        await _context.SaveChangesAsync(ct);

        return (true, null);
    }

    public async Task<SectionEvaluationResultDto?> GetSectionResultsAsync(int courseSectionId, CancellationToken ct = default)
    {
        var section = await _context.CourseSections
            .AsNoTracking()
            .Include(cs => cs.Course)
            .Include(cs => cs.Instructor)
            .FirstOrDefaultAsync(cs => cs.Id == courseSectionId, ct);

        if (section == null)
            return null;

        var instructorName = await ResolveInstructorNameAsync(section.Instructor, ct);

        var responses = await _context.EvaluationResponses
            .AsNoTracking()
            .Include(r => r.Answers)
                .ThenInclude(a => a.Question)
            .Where(r => r.CourseSectionId == courseSectionId)
            .ToListAsync(ct);

        var totalResponses = responses.Count;
        var enrolledCount = section.EnrolledCount > 0 ? section.EnrolledCount : 1;
        var responseRate = totalResponses == 0 ? 0m : Math.Round((decimal)totalResponses / enrolledCount * 100m, 1);
        var isVisible = totalResponses >= MinResponsesForVisibility;

        var questions = await _context.EvaluationQuestions
            .AsNoTracking()
            .OrderBy(q => q.Category)
            .ThenBy(q => q.SortOrder)
            .ToListAsync(ct);

        var questionResults = new List<QuestionResultDto>();
        var allRatings = new List<int>();

        foreach (var question in questions)
        {
            var answersForQuestion = responses
                .SelectMany(r => r.Answers)
                .Where(a => a.QuestionId == question.Id)
                .ToList();

            if (!answersForQuestion.Any())
                continue;

            if (question.QuestionType == EvaluationQuestionType.Rating)
            {
                var ratings = answersForQuestion
                    .Where(a => a.RatingValue.HasValue)
                    .Select(a => a.RatingValue!.Value)
                    .ToList();

                decimal? average = ratings.Any()
                    ? Math.Round((decimal)ratings.Average(), 2)
                    : null;

                Dictionary<int, int>? distribution = null;
                if (isVisible)
                {
                    distribution = new Dictionary<int, int>();
                    for (var i = 1; i <= 5; i++)
                        distribution[i] = ratings.Count(r => r == i);
                    allRatings.AddRange(ratings);
                }

                questionResults.Add(new QuestionResultDto(
                    question.Id,
                    question.Text,
                    question.Category,
                    question.QuestionType,
                    isVisible ? average : null,
                    distribution,
                    null));
            }
            else
            {
                List<string>? textResponses = null;
                if (isVisible)
                {
                    textResponses = answersForQuestion
                        .Where(a => !string.IsNullOrWhiteSpace(a.TextValue))
                        .Select(a => a.TextValue!.Trim())
                        .ToList();
                }

                questionResults.Add(new QuestionResultDto(
                    question.Id,
                    question.Text,
                    question.Category,
                    question.QuestionType,
                    null,
                    null,
                    textResponses));
            }
        }

        decimal? overallAverage = isVisible && allRatings.Any()
            ? Math.Round((decimal)allRatings.Average(), 2)
            : null;

        return new SectionEvaluationResultDto(
            section.Id,
            section.Course.Code,
            section.Course.Name,
            instructorName,
            totalResponses,
            section.EnrolledCount,
            responseRate,
            isVisible,
            overallAverage,
            questionResults);
    }

    public async Task<DepartmentEvaluationSummaryDto?> GetDepartmentSummaryAsync(int departmentId, int? termId, CancellationToken ct = default)
    {
        var department = await _context.Departments
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == departmentId, ct);
        if (department == null)
            return null;

        var term = termId.HasValue
            ? await _context.AcademicTerms.AsNoTracking().FirstOrDefaultAsync(t => t.Id == termId.Value, ct)
            : await _context.AcademicTerms.AsNoTracking().FirstOrDefaultAsync(t => t.IsActive, ct);

        if (term == null)
            return null;

        // NOT: cs.IsActive filtresi kaldırıldı.
        // Geçmiş/arşivlenmiş dönemlerde sections IsActive=false olur; değerlendirme
        // sonuçları o dönemler için de erişilebilir olmalı (salt-okunur tarihsel kayıt).
        var sections = await _context.CourseSections
            .AsNoTracking()
            .Include(cs => cs.Course)
            .Include(cs => cs.Instructor)
            .Where(cs => cs.TermId == term.Id
                && cs.Course.DepartmentId == departmentId)
            .ToListAsync(ct);

        if (!sections.Any())
            return new DepartmentEvaluationSummaryDto(
                department.Id,
                department.Name,
                term.Id,
                term.Name,
                0, 0, 0, null,
                new List<SectionSummaryDto>());

        var sectionIds = sections.Select(s => s.Id).ToList();

        var responsesBySection = await _context.EvaluationResponses
            .AsNoTracking()
            .Where(r => sectionIds.Contains(r.CourseSectionId))
            .GroupBy(r => r.CourseSectionId)
            .Select(g => new { SectionId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.SectionId, x => x.Count, ct);

        var ratingsBySection = await _context.EvaluationAnswers
            .AsNoTracking()
            .Include(a => a.Response)
            .Include(a => a.Question)
            .Where(a => sectionIds.Contains(a.Response.CourseSectionId)
                && a.Question.QuestionType == EvaluationQuestionType.Rating
                && a.RatingValue.HasValue)
            .GroupBy(a => a.Response.CourseSectionId)
            .Select(g => new { SectionId = g.Key, Average = g.Average(a => (double)a.RatingValue!.Value) })
            .ToDictionaryAsync(x => x.SectionId, x => (decimal?)Math.Round((decimal)x.Average, 2), ct);

        var instructorIds = sections.Where(s => s.Instructor != null).Select(s => s.Instructor!.UserId).Distinct().ToList();
        var instructorNames = await _context.Users
            .AsNoTracking()
            .Where(u => instructorIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FirstName, u.LastName })
            .ToDictionaryAsync(u => u.Id, u => $"{u.FirstName} {u.LastName}".Trim(), ct);

        var summaries = sections.Select(s =>
        {
            var responseCount = responsesBySection.GetValueOrDefault(s.Id, 0);
            var enrolledCount = s.EnrolledCount > 0 ? s.EnrolledCount : 1;
            var responseRate = responseCount == 0 ? 0m : Math.Round((decimal)responseCount / enrolledCount * 100m, 1);
            var isVisible = responseCount >= MinResponsesForVisibility;
            var avg = ratingsBySection.GetValueOrDefault(s.Id);
            var instructorName = s.Instructor != null && instructorNames.TryGetValue(s.Instructor.UserId, out var n)
                ? $"{s.Instructor.Title} {n}".Trim()
                : "Atanmamış";

            return new SectionSummaryDto(
                s.Id,
                s.Course.Code,
                s.Course.Name,
                instructorName,
                responseCount,
                s.EnrolledCount,
                responseRate,
                isVisible ? avg : null,
                isVisible);
        })
        .OrderBy(s => s.CourseCode)
        .ToList();

        var totalEnrolled = sections.Sum(s => s.EnrolledCount);
        var totalResponded = summaries.Sum(s => s.ResponseCount);
        var overallResponseRate = totalEnrolled == 0 ? 0m : Math.Round((decimal)totalResponded / totalEnrolled * 100m, 1);
        var visibleSummaries = summaries.Where(s => s.IsVisible && s.AverageRating.HasValue).ToList();
        var overallAverage = visibleSummaries.Any()
            ? Math.Round(visibleSummaries.Average(s => s.AverageRating!.Value), 2)
            : (decimal?)null;

        return new DepartmentEvaluationSummaryDto(
            department.Id,
            department.Name,
            term.Id,
            term.Name,
            sections.Count,
            summaries.Count(s => s.ResponseCount > 0),
            overallResponseRate,
            overallAverage,
            summaries);
    }

    public async Task<EvaluationPeriodDto> OpenPeriodAsync(int termId, DateTime closesAt, string openedBy, CancellationToken ct = default)
    {
        var term = await _context.AcademicTerms.FirstOrDefaultAsync(t => t.Id == termId, ct)
            ?? throw new InvalidOperationException("Donem bulunamadi.");

        var existing = await _context.EvaluationPeriods
            .FirstOrDefaultAsync(p => p.TermId == termId, ct);

        if (existing != null)
        {
            existing.Status = EvaluationPeriodStatus.Open;
            existing.ClosesAt = closesAt;
            existing.OpenedAt = DateTime.UtcNow;
            existing.OpenedBy = openedBy;
            await _context.SaveChangesAsync(ct);
            return new EvaluationPeriodDto(existing.Id, existing.TermId, term.Name, existing.OpenedAt, existing.ClosesAt, existing.Status, true);
        }

        var period = new EvaluationPeriod
        {
            TermId = termId,
            Status = EvaluationPeriodStatus.Open,
            OpenedAt = DateTime.UtcNow,
            ClosesAt = closesAt,
            OpenedBy = openedBy
        };

        _context.EvaluationPeriods.Add(period);
        await _context.SaveChangesAsync(ct);

        return new EvaluationPeriodDto(period.Id, period.TermId, term.Name, period.OpenedAt, period.ClosesAt, period.Status, true);
    }

    public async Task<EvaluationPeriodDto> ClosePeriodAsync(int periodId, CancellationToken ct = default)
    {
        var period = await _context.EvaluationPeriods
            .Include(p => p.Term)
            .FirstOrDefaultAsync(p => p.Id == periodId, ct)
            ?? throw new InvalidOperationException("Degerlendirme donemi bulunamadi.");

        period.Status = EvaluationPeriodStatus.Closed;
        await _context.SaveChangesAsync(ct);

        return new EvaluationPeriodDto(period.Id, period.TermId, period.Term.Name, period.OpenedAt, period.ClosesAt, period.Status, false);
    }

    public async Task<List<EvaluationQuestionDto>> GetAllQuestionsAsync(CancellationToken ct = default)
    {
        return await _context.EvaluationQuestions
            .AsNoTracking()
            .OrderBy(q => q.Category)
            .ThenBy(q => q.SortOrder)
            .Select(q => new EvaluationQuestionDto(q.Id, q.Text, q.QuestionType, q.Category, q.SortOrder, q.IsActive))
            .ToListAsync(ct);
    }

    public async Task<EvaluationQuestionDto> CreateQuestionAsync(CreateEvaluationQuestionDto dto, CancellationToken ct = default)
    {
        var question = new EvaluationQuestion
        {
            Text = dto.Text.Trim(),
            QuestionType = dto.QuestionType,
            Category = dto.Category,
            SortOrder = dto.SortOrder,
            IsActive = true
        };
        _context.EvaluationQuestions.Add(question);
        await _context.SaveChangesAsync(ct);

        return new EvaluationQuestionDto(question.Id, question.Text, question.QuestionType, question.Category, question.SortOrder, question.IsActive);
    }

    public async Task<EvaluationQuestionDto?> UpdateQuestionAsync(int id, UpdateEvaluationQuestionDto dto, CancellationToken ct = default)
    {
        var question = await _context.EvaluationQuestions.FirstOrDefaultAsync(q => q.Id == id, ct);
        if (question == null) return null;

        question.Text = dto.Text.Trim();
        question.QuestionType = dto.QuestionType;
        question.Category = dto.Category;
        question.SortOrder = dto.SortOrder;
        question.IsActive = dto.IsActive;
        await _context.SaveChangesAsync(ct);

        return new EvaluationQuestionDto(question.Id, question.Text, question.QuestionType, question.Category, question.SortOrder, question.IsActive);
    }

    public async Task<bool> DeleteQuestionAsync(int id, CancellationToken ct = default)
    {
        var question = await _context.EvaluationQuestions.FirstOrDefaultAsync(q => q.Id == id, ct);
        if (question == null) return false;

        question.IsActive = false;
        await _context.SaveChangesAsync(ct);
        return true;
    }

    private async Task<EvaluationPeriod?> GetOpenPeriodEntityAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        return await _context.EvaluationPeriods
            .Where(p => p.Status == EvaluationPeriodStatus.Open && p.ClosesAt > now)
            .OrderByDescending(p => p.OpenedAt)
            .FirstOrDefaultAsync(ct);
    }

    private async Task<string> ResolveInstructorNameAsync(Instructor? instructor, CancellationToken ct)
    {
        if (instructor == null)
            return "Atanmamış";

        var user = await _context.Users
            .AsNoTracking()
            .Where(u => u.Id == instructor.UserId)
            .Select(u => new { u.FirstName, u.LastName })
            .FirstOrDefaultAsync(ct);

        if (user == null)
            return "Atanmamış";

        return $"{instructor.Title} {user.FirstName} {user.LastName}".Trim();
    }
}
