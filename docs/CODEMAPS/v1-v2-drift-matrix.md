# V1 Evaluator ↔ V2 Encoder Drift Matrix

> **Plan 11 Faz 0 audit artifact.** Auto-schedule (V2 CP-SAT) ile runtime check (V1 evaluator) arasındaki tüm hard-constraint kuralların kanıt-zincirli karşılaştırması. Faz 3 boyunca her satır için failing-first parity test'i yazılır; Faz 5 sonunda V1 silinince bu matrix "RESOLVED" başlığıyla arşivlenir.

Son güncelleme: 2026-05-15 (Plan 12 Faz 0-7)
Status: **HARD RESOLVED / SOFT PARTIAL** — Plan 11 hard tamamlandı, Plan 12 soft mimari birleştirme tamam, V2 encoder alignment Faz 9'a kaldı.

### Plan 11 (hard, RESOLVED)
- ✅ **Faz 5:** V1 hard evaluator (17 dosya) + DI registration + feature flag silindi. **Drift fiziksel olarak imkansız.**

### Plan 12 (soft, PARTIAL)
- ✅ **Faz 1-2:** ISoftRulePredicate + 10 soft+hybrid predicate port edildi (S1, S2, S3, S4, S5, S7, S8, S12, S20, DIST).
- ✅ **Faz 3:** RuntimeSnapshotAdapter 3 soft alan propage ediyor.
- ✅ **Faz 4:** RuleEngine soft dispatch + EvaluateWithBreakdownAsync gerçek çalışıyor.
- ✅ **Faz 5-6:** SchedulingService + SimulationService RuleEngine'a delege.
- ✅ **Faz 7:** V1 ConstraintEvaluationService + 11 evaluator dosyası silindi. IConstraintStateLoader extract.
- ⏳ **Faz 8:** RuntimeSnapshotFactory (TermSnapshot tipleri kaldırılır) — sonraki seans.
- ⏳ **Faz 9:** V2 encoder ↔ V1 numerik divergence düzeltmesi (5 encoder) — kullanıcı "birebir tutsun" kararı, sonraki seans.

- Detay: [rule-engine.md](rule-engine.md), `project_plan12_progress.md` memory

---

## Karşılaştırma Tablosu

| Kod | V1 Evaluator (dosya:satır) | V2 Encoder (dosya:satır) | Drift? | Apply path etkisi | Karar (Plan 11) |
|---|---|---|---|---|---|
| **H1** Derslik Çakışması | `Evaluators/Hard/ClassroomConflictEvaluator.cs:9-71` — Plan 10 paralel-lab self-skip aktif (satır 45) | `OrTools/Constraints/Hard/ClassroomConflictEncoder.cs` — Per-interval AddNoOverlap, paralel-lab aware | ❌ Yok | — | V2 mantığı port |
| **H2** Hoca Çakışması | `Evaluators/Hard/InstructorConflictEvaluator.cs:6-42` — TimeBlock overlap | `OrTools/Constraints/Hard/InstructorConflictEncoder.cs` — Per-instructor AddNoOverlap | ❌ Yok (`EvaluateAllExisting` filteredSnapshot.Sections.Where SectionId != section.SectionId çıkartır) | — | V2 mantığı port |
| **H3** Kapasite | `Evaluators/Hard/CapacityEvaluator.cs:5-28` — `c.ClassroomCapacity < c.Capacity` (paralel-split combined capacity, `SectionSnapshotFactory.cs:198-205`) | `OrTools/Constraints/Hard/CapacityEncoder.cs:34-72` — Lecture: section.Capacity > classroom.Capacity → forbid. Lab: minCapPerGroup = ⌈Capacity/groupCount⌉ → per-classroom forbid | ⚠️ Fonksiyonel olarak eşdeğer — V2 izin verirse V1 izin verir | Yok | V2 mantığı port |
| **H4** Derslik Tipi (Lab) | `Evaluators/Hard/ClassroomTypeEvaluator.cs:21-31` — Lab dersi → Lab/Computer her zaman | `OrTools/Constraints/Hard/ClassroomTypeEncoder.cs:56-90` — Default Lab/Computer, `RequiredLabClassroomType` override `ClassroomTypeRequirementEnabled` flag-gated | ❌ Default davranış aynı | — | V2 mantığı port (flag desteği eklenir) |
| **H4** Derslik Tipi (Theory→LabCategory veto) | `Evaluators/Hard/ClassroomTypeEvaluator.cs:35-45` — Theory → LabCategory yasak her zaman | `OrTools/Constraints/Hard/ClassroomTypeEncoder.cs:108-122` — `lectureOnly = !typeRequired` her zaman | ❌ Aynı | — | V2 mantığı port |
| **H4** Amfi (Capacity ≥ AmphiThreshold) | `Evaluators/Hard/ClassroomTypeEvaluator.cs:48-58` — Capacity ≥ AmphiThreshold → Amphitheater zorunlu, **her zaman** | `OrTools/Constraints/Hard/ClassroomTypeEncoder.cs:105` — `amphiRequired = ctx.AmphiThresholdEnabled && ...`, flag-gated, default **false** | 🔴 **GERÇEK DRIFT** | **Olası fail** | V2 mantığı port (flag-gate); Faz 1.4 acil patch evaluator'da geçici uygulanır |
| **H5** Müfredat Çakışması | `Evaluators/Hard/CurriculumConflictEvaluator.cs:6-110` — 3 mod: enrollment/cohort/curriculum | `OrTools/Constraints/Hard/CurriculumConflictEncoder.cs:32-46` — cohort/curriculum (enrollment → throws NotSupportedException) | ⚠️ Production'da `cohort` aktif, aynı | — | V2 mantığı port |
| **H6** Ders Bileşen Çakışması | `Evaluators/Hard/CourseComponentEvaluator.cs:6-47` — Different-section lecture vs lab pattern | `OrTools/Constraints/Hard/TheoryLabComponentEncoder.cs:31-94` — Plan 9 Task 11: Same-section + Cross-section AddNoOverlap | ⚠️ V2 daha kapsamlı (same-section), V1 subset | V2 sıkı, V1 izin verir → V2 atadığı V1 OK | V2 mantığı port |
| **H8** Aynı Ders Şube | `Evaluators/Hard/SameCourseSectionEvaluator.cs:6-42` — `AllowSameCourseSectionOverlap` flag, per-pair overlap | `OrTools/Constraints/Hard/SameCourseSectionEncoder.cs:30,38-79` — Aynı flag, AddNoOverlap per course (lecture+lab ayrı) | ❌ Aynı | — | V2 mantığı port |
| **H10** Fakülte Derslik | `Evaluators/Hard/FacultyClassroomEvaluator.cs:5-39` — ClassroomTermAssignments üzerinden | `OrTools/Constraints/Hard/FacultyClassroomEncoder.cs` — okumadım, aynı varsayılır | ⚠️ Doğrulanmamış (Faz 3.10'da semantic match testi yapılacak) | — | Karşılaştırma Faz 3.10'da netleşir |
| **H11** Eşkoşul | `Evaluators/Hard/CorequisiteEvaluator.cs:5-60` — Real enforcement: "non-conflicting pair exists" | `OrTools/Constraints/Hard/CorequisiteEncoder.cs:30-35` — **Real enforcement YOK, DB'de pair varsa NotSupportedException throws** | 🟡 V2 başlamaz (silent değil) — production'da DB'de 0 pair olduğu için sessiz | Apply başlamaz (V2 önceden patlar) | V1 mantığını port (predicate runtime'da enforce eder); V2 encoder Plan 6.5'a ertelendi |
| **H12** Kredi-Saat Uyumu | `Evaluators/Hard/CreditHourEvaluator.cs:5-35` — AssignedTimeSlotCount == TheoryHours + LabHours | **V2 encoder YOK** | ⚠️ V2 atama doğal olarak TheoryHours+LabHours saat üretir → V1 her zaman pass | Yok | V1 mantığını port (manuel atama farklı slot sayısı üretebilir → runtime kontrol değerli) |
| **H13** Öğle Arası | `Evaluators/Hard/LunchBreakEvaluator.cs:6-40` — `LunchBreakIsHard` config | `OrTools/Constraints/Hard/LunchBreakEncoder.cs` — DI factory ile aynı SystemConfig keys (`LunchBreakStart`, `LunchBreakEnd`) | ❌ Aynı | — | V2 mantığı port |
| **H15** Maks Ardışık Ders | `Evaluators/Hard/MaxConsecutiveHoursEvaluator.cs:6-75` — `config.MaxConsecutiveHours`, `config.BreakDurationMinutes` | `OrTools/Constraints/Hard/MaxConsecutiveHoursEncoder.cs` — DI factory ile aynı keys (`Schedule.MaxConsecutiveHoursPerDay`, `Schedule.BreakBetweenSlotsMinutes`) | ❌ Aynı | — | V2 mantığı port |
| **H16** Teori-Lab Sıra (revize 2026-05) | `Evaluators/Hard/TheoryLabDaySplitEvaluator.cs:7-12` — "Lab teoriden SONRA aynı günde olabilir" | `OrTools/Constraints/Hard/TheoryLabDaySplitEncoder.cs:53-64` — "`theoryDay != labDay` (farklı gün zorunlu)" | 🔴 **DRIFT** — V2 sıkı, V1 permissive | V2 izin verirse V1 izin verir → apply'ı etkilemez (V2 zaten farklı gün atadı) | **V2 mantığı seçilir** (kullanıcı 2026-05-15 onayladı; V1 permissive davranış silinir) |
| **H17** F2F/OnlineOnly Slot Koruması | `Evaluators/Hard/FaceToFaceSlotRestrictionEvaluator.cs:6-71` — SlotAllowedModes ile çift yönlü ihlal | `OrTools/Constraints/Hard/FaceToFaceSlotEncoder.cs` — Aynı semantik (her ikisi de TimeSlot.AllowedMode bakar) | ❌ Aynı | — | V2 mantığı port |
| **H-DL** Günlük Hoca Yükü | `Evaluators/Hard/DailyLoadEvaluator.cs:34` — **Hardcoded `<= 9`** | `OrTools/Constraints/Hard/DailyLoadEncoder.cs` — Factory: `Schedule.MaxDailyHoursPerInstructor` config, default 9 | 🔴 **GERÇEK DRIFT** | **Olası fail** (admin config değiştirdiyse) | V2 mantığı port (config-driven); Faz 1.2 acil patch evaluator'da config okur |
| **H-DO** Boş Gün | `Evaluators/Hard/NoDayOffEvaluator.cs:30` — Hardcoded `>= 5` | `OrTools/Constraints/Hard/NoDayOffEncoder.cs:108` — `usedDays ≤ workdays.Count - 1` generalize | ⚠️ 5-günlük workday için aynı; 6+ workday (Cmt slot eklenince) V2 izin verir V1 reddeder | Term Cmt slot eklerse olası | V2 mantığı port (generalize) |
| **H-WL** Haftalık Hoca Yükü | `Evaluators/Hard/WeeklyLoadEvaluator.cs:16` — `c.InstructorMaxWeeklyHours <= 0` → disabled | `OrTools/Constraints/Hard/WeeklyLoadEncoder.cs:43` — `overrideMax > 0 ? overrideMax : DefaultInstructorMaxWeeklyHours (default 30)` | ⚠️ V2 daha sıkı (default 30), V1 disabled | V2 izin verirse V1 izin verir → apply'ı etkilemez | V2 mantığı port (default fallback aktif) |

---

## SkipHardValidation Kapsam Audit (Plan 10)

`AssignSectionRequest.SkipHardValidation = true` flag'inin AssignSectionAsync ([SIS.Infrastructure/Services/FacultyPlanningService.Assignment.cs](../../SIS.Infrastructure/Services/FacultyPlanningService.Assignment.cs)) içinde **3 kapı atlanmıyor**:

| Kapı | Konum | SkipHardValidation atlatıyor mu? | Drift kaynağı? |
|---|---|---|---|
| `_schedulingService.ValidateAsync` çağrısı | satır 161-178 | ✅ Atlıyor (Plan 10) | — |
| H17 manuel kontrol | satır 67-108 | ❌ **Atlamıyor** | V2 ile aynı semantik (defansif duplicate) ama kontrol bypass'sız çalışıyor |
| K1 hardcoded 9 saat | satır 130-155 | ❌ **Atlamıyor** | 🔴 V1 hardcoded vs V2 config |
| `GetConflictsAsync` post-write | satır 204-207 | ❌ **Atlamıyor** | 🔴 Tüm V1 evaluator zincirini tetikler |

Faz 1.3'te 3 kapı `SkipHardValidation=true` ise bypass'lanacak şekilde patch'lenir.

---

## ConstraintException Tablosu Audit (2026-05-15)

```sql
SELECT cd."Code", COUNT(*) FROM "ConstraintExceptions" ce
JOIN "ConstraintDefinitions" cd ON cd."Id" = ce."ConstraintDefinitionId"
GROUP BY cd."Code";
```

**Sonuç:** 0 satır. Production'da hiçbir kural başına istisna kaydı yok.

| Toplam | Not |
|---|---|
| **0** | Faz 3'te `IRulePredicate` exempt davranışı sentetik test verisi ile doğrulanır. Migration veya backfill ihtiyacı yok. |

---

## Plan 10 Category Toggle Davranışı

V1 evaluator zincirinde Plan 10 toggle [`ConstraintEvaluationService.cs:330-339` `ResolveCategory`](../../SIS.Infrastructure/Services/Scheduling/ConstraintEvaluationService.cs) üzerinden çalışıyor:
- ConstraintDefinitions tablosunda Category = "Soft" ise evaluator hard conflict üretmez, soft penalty'e döner.
- ConstraintFlags `IsEnabled = false` ise [satır 134-138](../../SIS.Infrastructure/Services/Scheduling/ConstraintEvaluationService.cs#L134-L138) skip edilir.
- Synthetic hard conflict (Soft→Hard upgrade) [satır 401-402](../../SIS.Infrastructure/Services/Scheduling/ConstraintEvaluationService.cs#L401-L402) `BuildSyntheticHardConflict` ile.

Bu davranış RuleEngine'a port edilir (Faz 3.18).

---

## Bilinen 5 Memory Drift Notu

`project_known_encoder_evaluator_drifts.md` memory'sindeki belgelenmiş drift'ler:

1. **S2/S5 Code drift** — ✅ Plan 12 Faz 9 partial commit `824e8bb`: predicate Code'ları "H13"/"H-DL" oldu, Plan 10 Category toggle artık fiilen çalışıyor.
2. **Plan 10 STUB soft encoder'lar (H4/H10 soft)** — Plan 12 kapsamı dışı (ayrı mini-plan; CP-SAT domain expansion gerektirir).
3. **DIST per-row pattern** — ✅ Plan 12 Faz 2.10'da DIST_DistributionRule hybrid predicate olarak port edildi (RESOLVED).
4. **H12/Capacity preview-only** — Plan 11 Faz 3 izole testlerle korundu.
5. **Soft numerik divergence (5 encoder)** — Karışık durum (2026-05-15 güncel):
   - ✅ **StudentGapEncoder (S7)**: multiplier `(gap − cap + 1) × Weight` Plan 13 commit `728a4eb`'de port edildi. V1 ↔ V2 birebir aynı penalty üretiyor.
   - ✅ **WeekdayBalanceEncoder (S12)**: threshold guard `diff × 700 > sumOfDays × percent` Plan 12 commit `b29bb0e`'de port edildi.
   - ✅ **AdjacentYearConflictEncoder (S20)**: "double weight" drift'i **gerçek değil** — `ConstraintController.UpdateWeight` dual-write yapıyor (ConstraintDefinitions.Weight + SystemConfigs.Schedule.S20Weight senkron). Manuel DB değişikliği dışında drift yok.
   - ⏳ DailyLoadSoftEncoder (S2): credit-hour → clock-hour — Plan 13'e ertelendi (block-level CP-SAT model genişletmesi)
   - ⏳ InstructorDaySpreadEncoder (S8): credit-hour → clock-hour — aynı genişletme

   **3/5 RESOLVED**, 2/5 (S2/S8) clock-hour migration kaldı.

---

## Karar Özeti

- **2 gerçek hard drift kanıtlandı:** H4 Amphi, H-DL DailyLoad.
- **3 V1-only / V2-only durum:** H11 Corequisite (V1 enforce, V2 throw), H12 CreditHour (V1 only), H16 (V1 permissive, V2 sıkı).
- **Genel yön:** Plan 11'de tüm runtime check'ler V2 mantığını baz alır; H11 ve H12 için V1 mantığı port edilir çünkü V2 yok.
- **AssignSectionAsync** 3 manuel kapı `SkipHardValidation=true` flag'ine bağlanır (Faz 1.3) ve Faz 4'te tamamen kaldırılır (RuleEngine V2 mantığını runtime'da enforce eder).
