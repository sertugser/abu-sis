using Microsoft.EntityFrameworkCore;
using SIS.Application.AdvisorConstraints.DTOs;
using SIS.Application.AdvisorConstraints.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services;

public class AdvisorConstraintService : IAdvisorConstraintService
{
    private readonly AppDbContext _context;

    public AdvisorConstraintService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<AdvisorCourseConstraintDto>> GetStudentConstraintsAsync(
        int studentId,
        int? termId = null,
        CancellationToken cancellationToken = default)
    {
        var effectiveTermId = termId ?? await GetActiveTermIdAsync(cancellationToken);

        var constraints = await _context.AdvisorCourseConstraints
            .AsNoTracking()
            .Include(c => c.Student)
            .Include(c => c.Course)
            .Include(c => c.Term)
            .Where(c => c.StudentId == studentId && (!effectiveTermId.HasValue || c.TermId == effectiveTermId.Value))
            .OrderByDescending(c => c.IsActive)
            .ThenBy(c => c.Course.Code)
            .ToListAsync(cancellationToken);

        var student = await _context.Students
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == studentId, cancellationToken);

        string studentName = string.Empty;
        if (student != null)
        {
            studentName = await _context.Users
                .AsNoTracking()
                .Where(u => u.Id == student.UserId)
                .Select(u => $"{u.FirstName} {u.LastName}")
                .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;
        }

        return constraints.Select(c => new AdvisorCourseConstraintDto
        {
            Id = c.Id,
            StudentId = c.StudentId,
            StudentNumber = student?.StudentNumber ?? string.Empty,
            StudentName = studentName,
            CourseId = c.CourseId,
            CourseCode = c.Course.Code,
            CourseName = c.Course.Name,
            TermId = c.TermId,
            TermName = c.Term.Name,
            AdvisorId = c.AdvisorId,
            ConstraintType = c.ConstraintType,
            Note = c.Note,
            IsActive = c.IsActive,
            CreatedAt = c.CreatedAt,
            UpdatedAt = c.UpdatedAt
        }).ToList();
    }

    public async Task<AdvisorCourseConstraintDto> UpsertConstraintAsync(
        int advisorInstructorId,
        UpsertAdvisorCourseConstraintRequest request,
        CancellationToken cancellationToken = default)
    {
        var termId = request.TermId ?? await GetRequiredActiveTermIdAsync(cancellationToken);
        await EnsureAdvisorOwnsStudentAsync(advisorInstructorId, request.StudentId, cancellationToken);

        var student = await _context.Students
            .FirstOrDefaultAsync(s => s.Id == request.StudentId, cancellationToken)
            ?? throw new InvalidOperationException("Ogrenci bulunamadi.");

        var course = await _context.Courses
            .FirstOrDefaultAsync(c => c.Id == request.CourseId, cancellationToken)
            ?? throw new InvalidOperationException("Ders bulunamadi.");

        var term = await _context.AcademicTerms
            .FirstOrDefaultAsync(t => t.Id == termId, cancellationToken)
            ?? throw new InvalidOperationException("Donem bulunamadi.");

        var existing = await _context.AdvisorCourseConstraints
            .FirstOrDefaultAsync(c =>
                c.StudentId == request.StudentId
                && c.CourseId == request.CourseId
                && c.TermId == termId,
                cancellationToken);

        if (existing == null)
        {
            existing = new SIS.Domain.Entities.AdvisorCourseConstraint
            {
                StudentId = request.StudentId,
                CourseId = request.CourseId,
                TermId = termId,
                AdvisorId = advisorInstructorId,
                ConstraintType = request.ConstraintType,
                Note = NormalizeNote(request.Note),
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            _context.AdvisorCourseConstraints.Add(existing);
        }
        else
        {
            existing.AdvisorId = advisorInstructorId;
            existing.ConstraintType = request.ConstraintType;
            existing.Note = NormalizeNote(request.Note);
            existing.IsActive = true;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);

        return await BuildDtoAsync(existing, student, course, term, cancellationToken);
    }

    public async Task<AdvisorCourseConstraintDto?> UpdateConstraintAsync(
        int advisorInstructorId,
        int constraintId,
        UpdateAdvisorCourseConstraintRequest request,
        CancellationToken cancellationToken = default)
    {
        var constraint = await _context.AdvisorCourseConstraints
            .Include(c => c.Student)
            .Include(c => c.Course)
            .Include(c => c.Term)
            .FirstOrDefaultAsync(c => c.Id == constraintId, cancellationToken);

        if (constraint == null)
            return null;

        await EnsureAdvisorOwnsStudentAsync(advisorInstructorId, constraint.StudentId, cancellationToken);

        constraint.ConstraintType = request.ConstraintType;
        constraint.Note = NormalizeNote(request.Note);
        constraint.IsActive = request.IsActive;
        constraint.AdvisorId = advisorInstructorId;
        constraint.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return await BuildDtoAsync(constraint, constraint.Student, constraint.Course, constraint.Term, cancellationToken);
    }

    public async Task<bool> DeleteConstraintAsync(
        int advisorInstructorId,
        int constraintId,
        CancellationToken cancellationToken = default)
    {
        var constraint = await _context.AdvisorCourseConstraints
            .FirstOrDefaultAsync(c => c.Id == constraintId, cancellationToken);

        if (constraint == null)
            return false;

        await EnsureAdvisorOwnsStudentAsync(advisorInstructorId, constraint.StudentId, cancellationToken);

        _context.AdvisorCourseConstraints.Remove(constraint);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<EffectiveAdvisorConstraintDto?> GetEffectiveConstraintAsync(
        int studentId,
        int courseId,
        int termId,
        CancellationToken cancellationToken = default)
    {
        var constraint = await _context.AdvisorCourseConstraints
            .AsNoTracking()
            .Where(c =>
                c.StudentId == studentId
                && c.CourseId == courseId
                && c.TermId == termId
                && c.IsActive)
            .OrderByDescending(c => c.UpdatedAt ?? c.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return constraint == null
            ? null
            : new EffectiveAdvisorConstraintDto
            {
                StudentId = constraint.StudentId,
                CourseId = constraint.CourseId,
                TermId = constraint.TermId,
                ConstraintType = constraint.ConstraintType,
                Note = constraint.Note
            };
    }

    public async Task<Dictionary<int, EffectiveAdvisorConstraintDto>> GetEffectiveConstraintsAsync(
        int studentId,
        IEnumerable<int> courseIds,
        int termId,
        CancellationToken cancellationToken = default)
    {
        var distinctCourseIds = courseIds.Distinct().ToList();
        if (distinctCourseIds.Count == 0)
            return new Dictionary<int, EffectiveAdvisorConstraintDto>();

        var constraints = await _context.AdvisorCourseConstraints
            .AsNoTracking()
            .Where(c =>
                c.StudentId == studentId
                && c.TermId == termId
                && c.IsActive
                && distinctCourseIds.Contains(c.CourseId))
            .OrderByDescending(c => c.UpdatedAt ?? c.CreatedAt)
            .ToListAsync(cancellationToken);

        return constraints
            .GroupBy(c => c.CourseId)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var item = g.First();
                    return new EffectiveAdvisorConstraintDto
                    {
                        StudentId = item.StudentId,
                        CourseId = item.CourseId,
                        TermId = item.TermId,
                        ConstraintType = item.ConstraintType,
                        Note = item.Note
                    };
                });
    }

    public async Task<HashSet<int>> GetBlockedCourseIdsAsync(
        int studentId,
        int termId,
        CancellationToken cancellationToken = default)
    {
        var courseIds = await _context.AdvisorCourseConstraints
            .AsNoTracking()
            .Where(c =>
                c.StudentId == studentId
                && c.TermId == termId
                && c.IsActive
                && c.ConstraintType == SIS.Domain.Enums.AdvisorConstraintType.Block)
            .Select(c => c.CourseId)
            .ToListAsync(cancellationToken);

        return courseIds.ToHashSet();
    }

    private async Task EnsureAdvisorOwnsStudentAsync(
        int advisorInstructorId,
        int studentId,
        CancellationToken cancellationToken)
    {
        var studentAdvisorId = await _context.Students
            .AsNoTracking()
            .Where(s => s.Id == studentId)
            .Select(s => s.AdvisorId)
            .FirstOrDefaultAsync(cancellationToken);

        if (studentAdvisorId == null)
            throw new InvalidOperationException("Ogrencinin aktif danismani bulunamadi.");

        if (studentAdvisorId != advisorInstructorId)
            throw new InvalidOperationException("Bu ogrencinin danismani degilsiniz.");
    }

    private async Task<AdvisorCourseConstraintDto> BuildDtoAsync(
        SIS.Domain.Entities.AdvisorCourseConstraint constraint,
        SIS.Domain.Entities.Student student,
        SIS.Domain.Entities.Course course,
        SIS.Domain.Entities.AcademicTerm term,
        CancellationToken cancellationToken)
    {
        var studentName = await _context.Users
            .AsNoTracking()
            .Where(u => u.Id == student.UserId)
            .Select(u => $"{u.FirstName} {u.LastName}")
            .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

        return new AdvisorCourseConstraintDto
        {
            Id = constraint.Id,
            StudentId = student.Id,
            StudentNumber = student.StudentNumber,
            StudentName = studentName,
            CourseId = course.Id,
            CourseCode = course.Code,
            CourseName = course.Name,
            TermId = term.Id,
            TermName = term.Name,
            AdvisorId = constraint.AdvisorId,
            ConstraintType = constraint.ConstraintType,
            Note = constraint.Note,
            IsActive = constraint.IsActive,
            CreatedAt = constraint.CreatedAt,
            UpdatedAt = constraint.UpdatedAt
        };
    }

    private async Task<int> GetRequiredActiveTermIdAsync(CancellationToken cancellationToken)
    {
        return await GetActiveTermIdAsync(cancellationToken)
            ?? throw new InvalidOperationException("Aktif donem bulunamadi.");
    }

    private async Task<int?> GetActiveTermIdAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        return await _context.AcademicTerms
            .AsNoTracking()
            .ActiveTerm()
            .Select(t => (int?)t.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static string? NormalizeNote(string? note)
    {
        return string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }
}
