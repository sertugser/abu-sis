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

            // Yayın kilidi: fakülte programı onaylı (yayında) ise değişiklik yapılamaz.
            await EnsureScheduleNotPublishedAsync(section, ct);

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

                // H17 inline check kaldırıldı (Override flow için uniform akış): V2 RuleEngine
                // (SchedulingService.Validate) H17_FaceToFaceSlotRule ile aynı kontrolü yapıyor;
                // o conflict listesine düşer, Force=false → dialog, Force=true → warning.
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

                // K1 (Daily Load) inline check kaldırıldı (Override flow için uniform akış):
                // V2 RuleEngine HDL_DailyLoadRule ile aynı limiti kontrol eder. Conflict listesine
                // düşer, Force=false → dialog, Force=true → warning olarak yazılır.
            }

            // Plan 10 — V2 apply path SkipHardValidation=true geçerse evaluator bypass.
            // V2 zaten encoder'larla enforce etti; paralel-lab section'larında V1 evaluator
            // section-level ClassroomId üzerinden bakıp false-positive "çakışma" üretiyor.
            //
            // Faz: Force=false (varsayılan) ile conflict varsa atama yazılmaz, response'ta
            // RequiresConfirmation=true ve conflict listesi döndürülür → frontend dialog gösterir,
            // kullanıcı override ederse aynı request Force=true ile tekrar yollanır. Force=true
            // ile çağrılırsa conflict'ler warnings olarak yansır ve atama yine yazılır.
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

                if (validation.Conflicts.Count > 0 && !request.Force)
                {
                    // Override akışı — transaction'ı geri al, conflict'leri döndür.
                    if (transaction != null)
                        await transaction.RollbackAsync(ct);

                    return new AssignmentResultDto
                    {
                        Success = false,
                        RequiresConfirmation = true,
                        Conflicts = validation.Conflicts,
                        Warnings = new List<string>(),
                    };
                }

                // Force=true (kullanıcı override etti) — conflict mesajlarını warning olarak ekle,
                // ama atamaya devam et. Section.Status doğal olarak "Conflicted" olur (status hesabı
                // FacultyPlanningService'te conflict tablosundan bakar).
                if (validation.Conflicts.Count > 0 && request.Force)
                {
                    foreach (var conflict in validation.Conflicts)
                    {
                        result.Warnings.Add($"[Override] {conflict.Message}");
                    }
                }
            }

            // Online dersler için classroomId null kabul edilir (slot kayıtları classroom'suz yazılır).
            // TeachingMethodPolicy.RequiresClassroom tek otorite — Online ise classroomsuz da yaz.
            var hasSlotLevelClassrooms = request.ScheduleSlots?.Any(s => s.ClassroomId.HasValue) == true;
            var isOnlineCourse = !Scheduling.TeachingMethodPolicy.RequiresClassroom(section.Course.TeachingMethod);
            if ((request.ClassroomId.HasValue || hasSlotLevelClassrooms || isOnlineCourse) && request.ScheduleSlots is { Count: > 0 })
            {
                // WriteAsync clear-before-write idempotent (ScheduleWriter.cs:33). Eski iç guard
                // (isNewSchedule || classroomChanged || isOnlineCourse) "aynı classroom + yeni slot"
                // taşıma senaryosunu kaçırıyordu: endpoint 200 dönüyor, SectionSchedules güncellenmiyordu.
                // Dış wrapper koşulu zaten "yazma niyetini" doğruluyor — koşulsuz çağırmak güvenli.
                await _scheduleWriter.WriteAsync(
                    section.Id,
                    section.TermId,
                    request.ClassroomId ?? section.ClassroomId,
                    request.ScheduleSlots,
                    ct);
            }

            // Plan 11 Faz 1.3 — V2 apply path SkipHardValidation=true ise V1 evaluator
            // zincirini de atla (paralel-lab ve V4 Amphi gibi drift kaynaklarına yol açıyordu).
            // V2 encoder zaten tüm hard kuralları enforce etti; manuel atama (Skip=false) için
            // tam V1 conflict listesi korunur.
            List<SchedulingConflictDto> sectionConflicts;
            if (request.SkipHardValidation)
            {
                sectionConflicts = new List<SchedulingConflictDto>();
            }
            else
            {
                var conflicts = await _schedulingService.GetConflictsAsync(section.TermId, ct);
                sectionConflicts = conflicts.Where(c => c.SectionId == section.Id || c.RelatedSectionId == section.Id).ToList();
            }
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

        // Yayın kilidi: fakülte programı onaylı (yayında) ise değişiklik yapılamaz.
        await EnsureScheduleNotPublishedAsync(section, ct);

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
        // Yayın kilidi: fakülte programı onaylı (yayında) ise toplu sıfırlama yapılamaz.
        if (await _context.FacultyScheduleApprovals.AnyAsync(a => a.FacultyId == facultyId && a.TermId == termId, ct))
            throw new InvalidOperationException("Bu fakültenin programı onaylanmış (yayında). Sıfırlamak için önce Program Onayını geri alın.");

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

        // Atomiklik: schedule silme + classroom temizleme + audit tek transaction'da.
        // (Eskiden transaction yoktu → kısmi başarısızlıkta yarı-sıfırlanmış durum kalabiliyordu.)
        await using var transaction = await _context.Database.BeginTransactionAsync(ct);
        try
        {
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

            // Audit: her section için ayrı kayıt — eski tek-satır (yalnız sectionIds[0]) bulk
            // sıfırlamayı yanıltıcı şekilde tek section'a bağlıyordu.
            foreach (var sid in sectionIds)
            {
                _context.SchedulingAuditLogs.Add(new SchedulingAuditLog
                {
                    SectionId = sid,
                    Action = "ResetAll",
                    UserId = userId,
                });
            }

            await _context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return sections.Count;
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
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

    /// <summary>
    /// Yayın kilidi: section'ın fakültesi o dönem için onaylanmış (yayında) ise
    /// InvalidOperationException fırlatır. Onaylı programda değişiklik için önce Program
    /// Onayı geri alınmalıdır. Ortak dersler (Course.DepartmentId == null) onaya tabi değildir.
    /// </summary>
    private async Task EnsureScheduleNotPublishedAsync(CourseSection section, CancellationToken ct)
    {
        // Faz 3b: "Düzenlemek için durdurulmuş" (suspended) section yayın sonrası düzenlemeye
        // açıktır — kilit yalnızca bu section için gevşer. Diğer onaylı section'lar kilitli kalır.
        if (section.IsEnrollmentSuspended) return;
        if (section.Course?.DepartmentId == null) return;
        var facultyId = await _context.Departments
            .Where(d => d.Id == section.Course.DepartmentId.Value)
            .Select(d => d.FacultyId)
            .FirstOrDefaultAsync(ct);
        var approved = await _context.FacultyScheduleApprovals
            .AnyAsync(a => a.FacultyId == facultyId && a.TermId == section.TermId, ct);
        if (approved)
            throw new InvalidOperationException("Bu fakültenin programı onaylanmış (yayında). Bu şubeyi değiştirmek için önce \"Düzenlemek için durdur\" yapın, ya da tüm programı düzenlemek için Program Onayını geri alın.");
    }
}
