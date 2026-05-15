using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SIS.Application.FacultyPlanning.DTOs;
using SIS.Application.Scheduling.DTOs;
using SIS.Domain.Entities;
using SIS.Domain.Enums;

namespace SIS.Infrastructure.Services;

/// <summary>
/// FacultyPlanningService — atama operasyonları (assign / unassign / apply / reset).
/// Bu partial dosya schedule yazma ve transaction yönetimini izole eder.
/// Sorgu/dashboard/faculty-access metotları ana FacultyPlanningService.cs'de.
/// </summary>
public partial class FacultyPlanningService
{
    public async Task<AssignmentResultDto> AssignSectionAsync(AssignSectionRequest request, string userId, CancellationToken ct = default)
    {
        // B14: wrap entire method in a single transaction so section update, schedule write,
        // and audit logs are all committed atomically. If we are already inside an outer
        // transaction (e.g. called from ApplyScheduleAsync) we reuse it instead of nesting.
        var ownsTransaction = _context.Database.CurrentTransaction == null;
        await using var transaction = ownsTransaction
            ? await _context.Database.BeginTransactionAsync(ct)
            : null;
        try
        {
            var section = await _context.CourseSections
                .Include(cs => cs.Course)
                .FirstOrDefaultAsync(cs => cs.Id == request.SectionId, ct)
                ?? throw new KeyNotFoundException("Sube bulunamadi.");

            var term = await _context.AcademicTerms.AsNoTracking().FirstOrDefaultAsync(t => t.Id == section.TermId, ct);
            if (term != null && term.IsCancelled)
                throw new InvalidOperationException("Hoca/derslik/zaman ataması için dönem iptal edilmemiş olmalıdır.");

            if (section.IsFinalized)
                throw new InvalidOperationException("Bu sube kesinlesmis, degisiklik yapilamaz.");

            var userDeptId = await ResolveDepartmentForUserAsync(userId, ct);
            if (userDeptId.HasValue && section.Course.DepartmentId != userDeptId.Value)
                throw new UnauthorizedAccessException("Sadece kendi bolumunuzun subelerine atama yapabilirsiniz.");

            var result = new AssignmentResultDto { Success = true };
            var previousClassroomId = section.ClassroomId;

            if (request.ClassroomId.HasValue)
            {
                if (request.ScheduleSlots == null || request.ScheduleSlots.Count == 0)
                    throw new ArgumentException("Derslik atanirken zaman slotlari da belirtilmelidir.");

                var classroom = await _context.Classrooms
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == request.ClassroomId.Value, ct)
                    ?? throw new KeyNotFoundException("Derslik bulunamadi.");

                if (classroom.Capacity < section.Capacity)
                    result.Warnings.Add($"Derslik kapasitesi ({classroom.Capacity}) sube kapasitesinden ({section.Capacity}) dusuk.");

                var expectedType = section.Course.LabHours > 0 && section.Course.TheoryHours == 0
                    ? ClassroomType.Lab : ClassroomType.Lecture;
                if (classroom.Type != expectedType)
                    result.Warnings.Add($"Derslik tipi uyumsuz: beklenen {expectedType}, atanan {classroom.Type}.");

                // H17 koruması: ders öğretim yöntemi ile slot AllowedMode uyumlu olmalı.
                // TeachingMethodPolicy + SlotAllowedModePolicy üzerinden tek otorite kontrol.
                if (!Scheduling.TeachingMethodPolicy.CanUseOnlineOnlySlots(section.Course.TeachingMethod)
                    && request.ScheduleSlots is { Count: > 0 })
                {
                    var requestedSlotKeys = request.ScheduleSlots
                        .Select(s => new { Day = (DayOfWeek)s.Day, s.StartTime })
                        .ToList();

                    var onlineOnlySlots = await _context.TimeSlots
                        .AsNoTracking()
                        .Where(ts => ts.TermId == section.TermId && ts.AllowedMode == TimeSlotAllowedMode.OnlineOnly)
                        .Select(ts => new { ts.DayOfWeek, ts.StartTime })
                        .ToListAsync(ct);

                    var onlineOnlyLookup = onlineOnlySlots
                        .Select(s => (s.DayOfWeek, s.StartTime))
                        .ToHashSet();

                    var violatingSlots = requestedSlotKeys
                        .Where(k => onlineOnlyLookup.Contains((k.Day, k.StartTime)))
                        .ToList();

                    if (violatingSlots.Count > 0)
                    {
                        var ruleConfig = await _context.SystemConfigs
                            .AsNoTracking()
                            .Where(c => c.Key == "Schedule.FaceToFaceCutoffTime" || c.Key == "Schedule.WeekendOnlineOnly")
                            .ToDictionaryAsync(c => c.Key, c => c.Value, ct);

                        var cutoff = ruleConfig.TryGetValue("Schedule.FaceToFaceCutoffTime", out var cv) ? cv : "(config yok)";
                        var weekendOnlyRaw = ruleConfig.TryGetValue("Schedule.WeekendOnlineOnly", out var wv) ? wv : "true";
                        var weekendNote = bool.TryParse(weekendOnlyRaw, out var wob) && wob
                            ? " ve hafta sonu slotlari"
                            : string.Empty;

                        var slotSummary = string.Join(", ", violatingSlots
                            .Select(v => $"{v.Day} {v.StartTime:HH\\:mm}"));
                        throw new InvalidOperationException(
                            $"Yuz-yuze ders ({section.Course.Code}) OnlineOnly damgali slot'a yerlestirilemez: {slotSummary}. " +
                            $"Kural (config'ten): {cutoff} sonrasi{weekendNote} yalnizca online/hybrid dersler icindir. " +
                            $"Admin slot AllowedMode'unu Both veya FaceToFaceOnly yaparsa kural gevşer.");
                    }
                }

                section.ClassroomId = request.ClassroomId.Value;
            }
            else if (request.ScheduleSlots is { Count: > 0 })
            {
                // Online dersler için classroom YOK — atama serbest. Diğerleri için zorunlu.
                var slotLevelHasClassroom = request.ScheduleSlots.Any(s => s.ClassroomId.HasValue);
                if (Scheduling.TeachingMethodPolicy.RequiresClassroom(section.Course.TeachingMethod)
                    && !slotLevelHasClassroom)
                {
                    throw new ArgumentException("Zaman slotu atanirken derslik de belirtilmelidir.");
                }
            }

            if (request.InstructorId.HasValue)
            {
                var instructorExists = await _context.Instructors.AnyAsync(i => i.Id == request.InstructorId.Value, ct);
                if (!instructorExists)
                    throw new KeyNotFoundException("Ogretim uyesi bulunamadi.");
                section.InstructorId = request.InstructorId.Value;

                // K1: Hoca günlük yük limiti kontrolü.
                // Plan 11 Faz 1.1 — hardcoded 9 yerine SystemConfig'ten okur (V2 DailyLoadEncoder ile parite).
                // Admin Schedule.MaxDailyHoursPerInstructor'ı değiştirince hem V2 atama hem K1 runtime check
                // aynı eşiği kullanır, drift kapanır.
                if (request.ScheduleSlots is { Count: > 0 })
                {
                    var maxDailyHours = await _systemConfig.GetIntAsync(
                        "Schedule.MaxDailyHoursPerInstructor", 9, ct);

                    var instrId = request.InstructorId.Value;
                    var requestDays = request.ScheduleSlots.Select(s => s.Day).Distinct();
                    var existingInstructorSchedules = await _context.SectionSchedules
                        .AsNoTracking()
                        .Include(ss => ss.TimeSlot)
                        .Where(ss => ss.CourseSection.InstructorId == instrId
                            && ss.CourseSection.TermId == section.TermId
                            && ss.CourseSectionId != section.Id)
                        .ToListAsync(ct);

                    foreach (var day in requestDays)
                    {
                        var existingHours = existingInstructorSchedules
                            .Where(ss => (int)ss.TimeSlot.DayOfWeek == day)
                            .Sum(ss => (ss.TimeSlot.EndTime - ss.TimeSlot.StartTime).TotalHours);
                        var newHours = request.ScheduleSlots
                            .Where(s => s.Day == day)
                            .Sum(s => (s.EndTime - s.StartTime).TotalHours);
                        if (existingHours + newHours > maxDailyHours)
                            throw new InvalidOperationException(
                                $"Ogretim uyesinin {day} gunundeki toplam yuku {existingHours + newHours:F1} saate ulasacak (limit: {maxDailyHours} saat). Atama reddedildi.");
                    }
                }
            }

            // Plan 10 — V2 apply path SkipHardValidation=true geçerse evaluator bypass.
            // V2 zaten encoder'larla enforce etti; paralel-lab section'larında V1 evaluator
            // section-level ClassroomId üzerinden bakıp false-positive "çakışma" üretiyor.
            if (!request.SkipHardValidation
                && (request.ClassroomId.HasValue || request.InstructorId.HasValue || request.ScheduleSlots is { Count: > 0 }))
            {
                var validation = await _schedulingService.ValidateAsync(new ScheduleValidationRequestDto
                {
                    SectionId = request.SectionId,
                    ClassroomId = request.ClassroomId,
                    InstructorId = request.InstructorId,
                    ScheduleSlots = request.ScheduleSlots
                }, ct);

                if (validation.Conflicts.Count > 0)
                {
                    var firstConflict = validation.Conflicts[0].Message;
                    throw new InvalidOperationException(
                        $"Atama sert kisit ihlali nedeniyle reddedildi: {firstConflict}");
                }
            }

            // Write schedule records only if this is a NEW schedule assignment.
            // Online dersler için classroomId null kabul edilir (slot kayıtları classroom'suz yazılır).
            // TeachingMethodPolicy.RequiresClassroom tek otorite — Online ise classroomsuz da yaz.
            var hasSlotLevelClassrooms = request.ScheduleSlots?.Any(s => s.ClassroomId.HasValue) == true;
            var isOnlineCourse = !Scheduling.TeachingMethodPolicy.RequiresClassroom(section.Course.TeachingMethod);
            if ((request.ClassroomId.HasValue || hasSlotLevelClassrooms || isOnlineCourse) && request.ScheduleSlots is { Count: > 0 })
            {
                var existingScheduleCount = await _context.SectionSchedules
                    .CountAsync(ss => ss.CourseSectionId == section.Id, ct);

                var classroomChanged = request.ClassroomId.HasValue && previousClassroomId != request.ClassroomId.Value;
                var isNewSchedule = existingScheduleCount == 0;

                if (isNewSchedule || classroomChanged || isOnlineCourse)
                {
                    await _scheduleWriter.WriteAsync(
                        section.Id,
                        section.TermId,
                        request.ClassroomId ?? section.ClassroomId,
                        request.ScheduleSlots,
                        ct);
                }
            }

            var conflicts = await _schedulingService.GetConflictsAsync(section.TermId, ct);
            var sectionConflicts = conflicts.Where(c => c.SectionId == section.Id || c.RelatedSectionId == section.Id).ToList();
            result.Conflicts = sectionConflicts;
            result.Success = sectionConflicts.Count == 0;

            _context.SchedulingAuditLogs.Add(new SchedulingAuditLog
            {
                SectionId = section.Id,
                Action = "Assign",
                UserId = userId,
                ClassroomId = request.ClassroomId,
                InstructorId = request.InstructorId
            });
            _context.AuditLogs.Add(new AuditLog
            {
                UserId = TryParseUserId(userId),
                Action = "ScheduleAssigned",
                EntityType = nameof(CourseSection),
                EntityId = section.Id.ToString(),
                NewValue = JsonSerializer.Serialize(new
                {
                    classroomId = request.ClassroomId ?? section.ClassroomId,
                    instructorId = request.InstructorId ?? section.InstructorId,
                    slots = request.ScheduleSlots?.Select(slot => new
                    {
                        day = slot.Day.ToString(),
                        startTime = slot.StartTime.ToString("HH\\:mm"),
                        endTime = slot.EndTime.ToString("HH\\:mm"),
                        classroomId = slot.ClassroomId ?? request.ClassroomId ?? section.ClassroomId,
                    }),
                }, JsonOptions),
                Timestamp = DateTime.UtcNow,
                IpAddress = GetCurrentIpAddress(),
            });

            try
            {
                await _context.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new InvalidOperationException("Bu sube baska bir kullanici tarafindan guncellendi. Lutfen yenileyip tekrar deneyin.");
            }

            if (ownsTransaction)
                await transaction!.CommitAsync(ct);

            return result;
        }
        catch
        {
            if (ownsTransaction && transaction != null)
                await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task UnassignSectionAsync(int sectionId, string userId, CancellationToken ct = default)
    {
        var section = await _context.CourseSections
            .Include(cs => cs.Course)
            .FirstOrDefaultAsync(cs => cs.Id == sectionId, ct)
            ?? throw new KeyNotFoundException("Sube bulunamadi.");

        var unassignTerm = await _context.AcademicTerms.AsNoTracking().FirstOrDefaultAsync(t => t.Id == section.TermId, ct);
        if (unassignTerm != null && unassignTerm.IsCancelled)
            throw new InvalidOperationException("Atama kaldırma sadece iptal edilmemiş dönemlerde yapılabilir.");

        if (section.IsFinalized)
            throw new InvalidOperationException("Bu sube kesinlesmis, degisiklik yapilamaz.");

        var userDeptId = await ResolveDepartmentForUserAsync(userId, ct);
        if (userDeptId.HasValue && section.Course.DepartmentId != userDeptId.Value)
            throw new UnauthorizedAccessException("Sadece kendi bolumunuzun subelerinin atamasini kaldirabilirsiniz.");

        var previousClassroomId = section.ClassroomId;
        var previousInstructorId = section.InstructorId;
        var previousSlots = await GetCurrentScheduleSlotPayloadAsync(sectionId, ct);

        await ClearSectionScheduleRecordsAsync(sectionId, ct);
        section.ClassroomId = null;
        section.InstructorId = null;

        _context.SchedulingAuditLogs.Add(new SchedulingAuditLog
        {
            SectionId = sectionId,
            Action = "Unassign",
            UserId = userId
        });
        _context.AuditLogs.Add(new AuditLog
        {
            UserId = TryParseUserId(userId),
            Action = "ScheduleUnassigned",
            EntityType = nameof(CourseSection),
            EntityId = sectionId.ToString(),
            NewValue = JsonSerializer.Serialize(new
            {
                previousClassroomId,
                previousInstructorId,
                slots = previousSlots,
                note = "Assignment removed",
            }, JsonOptions),
            Timestamp = DateTime.UtcNow,
            IpAddress = GetCurrentIpAddress(),
        });

        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new InvalidOperationException("Bu sube baska bir kullanici tarafindan guncellendi. Lutfen yenileyip tekrar deneyin.");
        }
    }

    public async Task<int> ResetAllSchedulesAsync(int facultyId, int termId, string userId, CancellationToken ct = default)
    {
        var departmentIds = await _context.Departments
            .AsNoTracking()
            .Where(d => d.FacultyId == facultyId)
            .Select(d => d.Id)
            .ToListAsync(ct);

        var sectionIds = await _context.CourseSections
            .AsNoTracking()
            .Where(cs => cs.TermId == termId
                && cs.Status == CourseSectionStatus.Active && !cs.IsFinalized && !cs.IsPinned && !cs.IsScheduleLocked
                && cs.Course.DepartmentId.HasValue
                && departmentIds.Contains(cs.Course.DepartmentId.Value))
            .Select(cs => cs.Id)
            .ToListAsync(ct);

        if (sectionIds.Count == 0) return 0;

        // Remove all SectionSchedule records
        var schedules = await _context.SectionSchedules
            .Where(ss => sectionIds.Contains(ss.CourseSectionId))
            .ToListAsync(ct);
        _context.SectionSchedules.RemoveRange(schedules);

        // Clear ClassroomId on sections
        var sections = await _context.CourseSections
            .Where(cs => sectionIds.Contains(cs.Id))
            .ToListAsync(ct);
        foreach (var s in sections)
            s.ClassroomId = null;

        _context.SchedulingAuditLogs.Add(new SchedulingAuditLog
        {
            SectionId = sectionIds[0],
            Action = "ResetAll",
            UserId = userId
        });

        await _context.SaveChangesAsync(ct);
        return sections.Count;
    }

    public async Task<ApplyScheduleResultDto> ApplyScheduleAsync(ApplyScheduleRequest request, string userId, CancellationToken ct = default)
    {
        var result = new ApplyScheduleResultDto();

        // Section-bazlı atama: her atama kendi içinde atomik (AssignSectionAsync kendi
        // transaction'ını yönetir). Bir section fail olursa SADECE o atlanır, diğer
        // atamalar yazılmaya devam eder. Eski davranış (tek fail → tüm rollback) auto-
        // schedule akışı için yıkıcıydı: preview "39 başarılı, 5 fail" derken apply'da
        // tek bir constraint ihlali tüm 38 atamayı geri alıyordu.
        foreach (var item in request.Assignments)
        {
            try
            {
                var assignResult = await AssignSectionAsync(new AssignSectionRequest
                {
                    SectionId = item.SectionId,
                    ClassroomId = item.ClassroomId,
                    ScheduleSlots = item.ScheduleSlots,
                    InstructorId = item.InstructorId,
                    // Plan 10 — V2 auto-schedule output'u apply ederken V1 evaluator
                    // bypass: V2 encoder'ları zaten hard kısıtları enforce etmişti.
                    SkipHardValidation = true,
                }, userId, ct);

                if (assignResult.Conflicts.Count > 0)
                {
                    result.FailedCount++;
                    var conflictMsg = assignResult.Conflicts[0].Message;
                    result.Errors.Add($"Sube {item.SectionId}: {conflictMsg}");
                    continue;
                }

                if (assignResult.Success)
                {
                    result.SuccessCount++;
                }
                else
                {
                    result.FailedCount++;
                    result.Errors.Add($"Sube {item.SectionId}: Atama basarisiz.");
                }
            }
            catch (Exception ex)
            {
                // AssignSectionAsync içinde throw oldu (validation/H17/transaction);
                // o atama kendi transaction'ında zaten rollback olmuş. Diğerlerini
                // etkilemez — sayım ve hata mesajı ekle, devam et.
                result.FailedCount++;
                result.Errors.Add($"Sube {item.SectionId}: {ex.Message}");
            }
        }

        return result;
    }
}
