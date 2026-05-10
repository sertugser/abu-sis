# Scheduling — DRY ve Tek-Otorite Kuralları

Bu döküman, Scheduling alanında bu hafta yaşadığımız bug sınıfının tekrarını
önlemek için **kod yazarken / kod review yaparken** uygulanması gereken
kuralları toplar. Her kural concrete bir geçmiş bug ile bağlanıyor.

## 1. SectionSnapshot oluşturma — TEK OTORİTE

**Kural:** Yeni `SectionSnapshot { ... }` literal'ı yazma. Daima
`SectionSnapshotFactory.FromCourseSection(...)` veya `WithBlocks(...)` kullan.

**Neden:** Bu hafta 4 ayrı bug aynı pattern'den çıktı:
- `ConstructionSolver` paralel-split kapasitesi yanlış (eski oturum)
- `FailureAnalyzer.BuildTestSnapshot` `ClassroomCapacity = 0` → CapacityConflict false-positive
- `FailureAnalyzer` `AssignedTimeSlotCount` paralel-split blokları çift sayıyor → CreditHourMismatch
- `BuildExistingSectionSnapshot` `TeachingMethod` set etmiyor → H17 online dersi yanlış FaceToFace görüyor

Hepsi snapshot'ı manuel inşa eden 6 farklı yerden tek birinin senkron olmamasıyla.

**Dosya:** `SIS.Infrastructure/Services/Scheduling/SectionSnapshotFactory.cs`

## 2. CurriculumConflictMode — TEK OTORİTE

**Kural:** `Schedule.CurriculumConflictMode` config'ine göre `CohortConflictPairs`
veya `EnrollmentConflicts` set etmek için **daima** `ConflictPairsBuilder.ApplyToSnapshotAsync(...)` kullan.

**Neden:** AutoScheduleEngine `cohort` mode kullanıyordu, manuel atama yolu
(`SchedulingService.BuildTermSnapshotAsync`) hiç okumuyordu — eski "curriculum"
branch'ine düşüyordu. Preview "OK" dedi, Apply "ihlal" dedi → 39 atama rollback.

**Dosya:** `SIS.Infrastructure/Services/Scheduling/ConflictPairsBuilder.cs`

## 3. TeachingMethod kontrolleri — TEK OTORİTE

**Kural:** `if (course.TeachingMethod == TeachingMethod.X)` yazma. Daima:
- `TeachingMethodPolicy.RequiresClassroom(method)` — derslik zorunlu mu
- `TeachingMethodPolicy.CanUseOnlineOnlySlots(method)` — OnlineOnly slota girebilir mi
- `TeachingMethodPolicy.CanUseFaceToFaceOnlySlots(method)` — F2F-Only slota girebilir mi
- `TeachingMethodPolicy.IsSlotModeCompatible(method, slotMode)` — combine

**Neden:** TeachingMethod kontrolleri 5 yere dağılmıştı (FacultyPlanningService,
AutoScheduleEngine, FaceToFaceSlotRestrictionEvaluator, CandidateGenerator,
SchedulingService). H17 evaluator bunlardan biriyle senkron değildi → online ders
yanlış reddediliyordu. Apply 4 ayrı yerde online ders için classroom kontrolü
yapıyordu, biri bug'lı.

**Dosya:** `SIS.Infrastructure/Services/Scheduling/TeachingMethodPolicy.cs`

## 4. Slot AllowedMode kontrolleri — TEK OTORİTE

**Kural:** `slot.AllowedMode == TimeSlotAllowedMode.X` direct karşılaştırması yapma.
`SlotAllowedModePolicy.IsAllowedForCourse(slotMode, method)` veya
`FilterIncompatibleSlots(slots, method)` kullan.

**Dosya:** `SIS.Infrastructure/Services/Scheduling/SlotAllowedModePolicy.cs`

## 5. Service dosya boyutu — 800 satır limit

**Kural:** Bir service dosyası 800 satırı geçerse partial class'a böl. Sorumluluk
bazlı dosya isimlendir:
- `<Service>.cs` — constructor, alanlar, ana sorumluluk
- `<Service>.<Sorumluluk>.cs` — ek partial dosyalar

**Mevcut bölmeler:**
- `SchedulingService.cs` (2150) + `SchedulingService.Snapshot.cs` (304) + `SchedulingService.Validation.cs` (139)
- `FacultyPlanningService.cs` (671) + `FacultyPlanningService.Assignment.cs` (410)

**Hâlâ büyük olanlar (gelecek refactor):**
- `EnrollmentForecastService.cs` (2116)
- `CourseRegistrationService.cs` (1857)
- `SchedulingService.cs` (hâlâ 2150 — ileride ClassroomQueries, BatchAutoSchedule, ScoreBreakdown ayrı dosyalara)

## 6. Apply / atama akışı — section-bazlı atomicity

**Kural:** Toplu atamalarda "tek fail → tüm rollback" YAPMA. Her section kendi
transaction'ında atomik; bir fail diğerlerini engellemez. Fail listesi `errors` array'inde
döner, başarılılar `successCount`'ta.

**Neden:** Auto-schedule preview zaten `successCount` / `failedCount` ayırt ediyor.
Apply'ın aynı semantiği takip etmesi UX tutarlılığı için kritik. Eski "all-or-nothing"
davranışı: 39 atamadan biri H17'de fail oldu → 38 atama rollback → kullanıcı 0 yansıma görüyor.

**Yer:** `FacultyPlanningService.Assignment.cs::ApplyScheduleAsync`

## 7. Seed data — UI değişikliklerini override ETME

**Kural:** Seed kodu "varsa atla" mantığı kullanmalı, "upsert" yapmamalı.
Admin UI'dan değiştirilen değerler reseed sonrası korunmalı.

**Yer:** `SeedData.AutoScheduleTestData.EnsureCoursesAsync` (revert edildi).

**Hardcoded data ANTI-PATTERN:**
- `SeedData.Registrations.cs` round-robin instructor atama (revert edildi: `InstructorId = null`)
- Test data hocaları, dersleri, dersliklerini production'a göndermeden önce seed
  guard ile sarmala (örn. `if (env.IsDevelopment())`)

## 8. Online ders davranışı — özel kurallar

Online ders **classroom-suz** kabul edilir:

| İşlem | Online ders | F2F ders | Hybrid |
|---|---|---|---|
| `Course.LabClassroomType` zorunlu | hayır | evet | evet |
| `AssignSection` `ClassroomId` parametresi zorunlu | hayır | evet | evet |
| `WriteSectionScheduleRecords` çağırılır | evet (classroomId null OK) | evet | evet |
| Status hesabı: schedule var = "Scheduled" | evet (classroom YOK) | classroom + schedule | classroom + schedule |
| `OnlineOnly` slotta yerleşebilir | evet | hayır | evet |
| `FaceToFaceOnly` slotta yerleşebilir | hayır | evet | evet |

**Yer:** `TeachingMethodPolicy`, `SlotAllowedModePolicy`,
`FacultyPlanningService.Assignment.cs::AssignSectionAsync`,
`FacultyPlanningService.cs::GetSectionsAsync` (status logic).

## 9. Code review checklist (yeni feature / fix için)

- [ ] Yeni `SectionSnapshot { ... }` literal'i var mı? → `SectionSnapshotFactory` kullan
- [ ] Yeni `CurriculumConflictMode` branching var mı? → `ConflictPairsBuilder` kullan
- [ ] Yeni `TeachingMethod` if-else var mı? → `TeachingMethodPolicy` kullan
- [ ] Yeni `slot.AllowedMode == ...` var mı? → `SlotAllowedModePolicy` kullan
- [ ] Service dosyası 800 satırı geçti mi? → Partial class'a böl
- [ ] Toplu işlem var mı? → Section-bazlı atomicity (tek fail diğerlerini engellemesin)
- [ ] Online ders edge case'i test edildi mi? → `RequiresClassroom` muafiyeti

## 10. "Sıfırla" semantiği

`ResetAllSchedulesAsync` **sadece**:
- `SectionSchedules` kayıtlarını siler
- `Section.ClassroomId = null` set eder

**Dokunmadığı alanlar:** `InstructorId`, `IsFinalized`, `IsPinned`, `IsScheduleLocked`.

Bu kasıtlı bir tasarım kararı: **hoca atama admin'in işi**, otomatik programlama
sıfırlaması o kararı silmez. Seed'in otomatik instructor ataması bu kararı ihlal ediyordu
(round-robin) — kaldırıldı (`SeedData.Registrations.cs::InstructorId = null`).
