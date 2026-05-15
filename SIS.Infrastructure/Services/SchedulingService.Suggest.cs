using Microsoft.EntityFrameworkCore;
using SIS.Application.Scheduling.DTOs;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Services.Scheduling;

namespace SIS.Infrastructure.Services;

/// <summary>
/// SchedulingService — "Öneri" akışı (eski). Kullanıcı bir section için
/// uygun derslik+slot kombinasyonu sorduğunda buradan cevap üretilir.
/// SuggestAsync: aday derslikleri filtrele, çakışmasız ilk kombinasyonu döndür.
/// </summary>
public partial class SchedulingService
{
    public async Task<ScheduleSuggestionDto> SuggestAsync(ScheduleSuggestionRequestDto request, CancellationToken cancellationToken = default)
    {
        var response = new ScheduleSuggestionDto
        {
            IsFeasible = false,
            SectionId = request.SectionId
        };

        if (request.SectionId <= 0)
        {
            response.Notes.Add("Geçerli bir section ID gönderilmelidir.");
            return response;
        }

        var section = await _context.CourseSections
            .AsNoTracking()
            .Include(cs => cs.Course)
            .Include(cs => cs.Instructor)
            .Include(cs => cs.Classroom)
            .FirstOrDefaultAsync(cs => cs.Id == request.SectionId, cancellationToken);

        if (section == null)
        {
            response.Notes.Add("Section bulunamadı.");
            return response;
        }

        var instructorId = request.InstructorId ?? section.InstructorId;
        if (!instructorId.HasValue)
        {
            response.Notes.Add("Öneri üretilebilmesi için section üzerinde bir öğretim üyesi tanımlı olmalı veya istek ile instructorId gönderilmelidir.");
            return response;
        }

        var instructor = await _context.Instructors
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == instructorId.Value, cancellationToken);

        if (instructor == null)
        {
            response.Notes.Add("Öğretim üyesi bulunamadı.");
            return response;
        }

        var instructorName = await GetInstructorNameAsync(instructor, cancellationToken);

        // Load TimeSlots for this term
        var timeSlots = await _context.TimeSlots
            .AsNoTracking()
            .Where(ts => ts.TermId == section.TermId)
            .OrderBy(ts => ts.DayOfWeek)
            .ThenBy(ts => ts.SlotOrder)
            .ToListAsync(cancellationToken);

        response.InstructorId = instructor.Id;
        response.InstructorName = instructorName;

        if (timeSlots.Count == 0)
        {
            response.Notes.Add("Bu dönem için zaman slotu tanımı bulunmadığından öneri üretilemedi.");
            return response;
        }

        var existingSnapshot = await BuildTermSnapshotAsync(section.TermId, section.Id, null, cancellationToken);
        var softConfig = await LoadSoftConstraintConfigAsync(cancellationToken);
        var hardConfig = await LoadHardConstraintConfigAsync(cancellationToken);
        var constraintState = await _constraintStateLoader.LoadConstraintStateAsync(
            instructorId, section.Course.DepartmentId, section.Id, section.TermId, cancellationToken);
        var candidateCourseMetadata = await LoadCourseSchedulingMetadataAsync(section.TermId, [section.CourseId], cancellationToken);
        var currentBlocks = await LoadSectionTimeBlocksAsync(section.Id, cancellationToken);
        var requiredHours = ResolveTotalRequiredHours(section);
        var slotCombinations = BuildTimeSlotCombinations(requiredHours, currentBlocks, timeSlots);

        if (slotCombinations.Count == 0)
        {
            response.Notes.Add("Zaman grid'inde uygun slot kombinasyonu üretilemedi.");
            return response;
        }

        var requiredCapacity = Math.Max(section.Capacity, 0);

        // Fakülteye atanmış dersliklerden seç (H10 ön-filtre)
        var courseDeptId = section.Course.DepartmentId;
        int? facultyId = null;
        if (courseDeptId.HasValue)
        {
            facultyId = await _context.Departments.AsNoTracking()
                .Where(d => d.Id == courseDeptId.Value)
                .Select(d => d.FacultyId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var assignedClassroomIds = await _context.ClassroomTermAssignments
            .AsNoTracking()
            .Where(cta => cta.TermId == section.TermId
                && (!facultyId.HasValue || cta.FacultyId == facultyId.Value))
            .Select(cta => cta.ClassroomId)
            .ToListAsync(cancellationToken);

        IQueryable<Classroom> classroomQuery;
        if (assignedClassroomIds.Count > 0)
        {
            // Fakülteye atanmış dersliklerden seç
            classroomQuery = _context.Classrooms.AsNoTracking()
                .Where(c => assignedClassroomIds.Contains(c.Id) && c.Capacity >= requiredCapacity);
        }
        else
        {
            // Henüz atama yoksa tüm dersliklerden seç (fallback)
            classroomQuery = _context.Classrooms.AsNoTracking()
                .Where(c => c.Capacity > 0 && c.Capacity >= requiredCapacity);
        }

        var classrooms = await classroomQuery
            .OrderBy(c => c.Capacity - requiredCapacity)
            .ThenBy(c => c.Id == section.ClassroomId ? 0 : 1)
            .ThenBy(c => c.Name)
            .ThenBy(c => c.Id)
            .ToListAsync(cancellationToken);

        if (classrooms.Count == 0)
        {
            response.Notes.Add("Section kontenjanını karşılayan uygun bir derslik bulunamadı.");
            return response;
        }

        var currentWeeklyHours = ResolveWeeklyHours(currentBlocks, section.Course);

        foreach (var slotCombination in slotCombinations)
        {
            foreach (var classroom in classrooms)
            {
                var candidate = BuildCandidateSnapshot(
                    section,
                    instructor,
                    instructorName,
                    classroom,
                    slotCombination.Slots,
                    requiredHours,
                    new List<string>(),
                    suggestedSemester: candidateCourseMetadata.MinSuggestedSemesters.TryGetValue(section.CourseId, out var suggestedSemester)
                        ? suggestedSemester
                        : null,
                    suggestedSemestersByDepartment: GetSuggestedSemestersByDepartment(section.CourseId, candidateCourseMetadata.SuggestedSemestersByDepartment),
                    accessibleDepartmentIds: GetAccessibleDepartments(section.CourseId, section.Course.DepartmentId, candidateCourseMetadata.AccessibleDepartments));

                var (conflicts, softNotes) = EvaluateCandidateConflicts(candidate, existingSnapshot, softConfig, hardConfig, constraintState);
                if (conflicts.Count > 0)
                {
                    continue;
                }

                var candidateWeeklyHours = ResolveWeeklyHours(candidate.TimeBlocks, section.Course);
                response.IsFeasible = true;
                response.SuggestedClassroomId = classroom.Id;
                response.SuggestedClassroomName = classroom.Name;
                response.SuggestedSlots = candidate.TimeBlocks.Select(MapTimeBlock).ToList();
                response.EstimatedWeeklyHoursImpact = candidateWeeklyHours - currentWeeklyHours;
                response.Notes.AddRange(candidate.Notes);
                response.Notes.AddRange(softNotes);
                response.Notes.Add("Slotlar zaman grid'inden uygun kombinasyon olarak seçildi.");
                response.Notes.Add($"Derslik secimi kapasite uyumu esas alinarak yapildi ({classroom.Capacity}/{requiredCapacity}).");
                return response;
            }
        }

        response.Notes.Add("Çakışma olmayan derslik + slot kombinasyonu bulunamadı.");
        return response;
    }
}
