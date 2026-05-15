# Rule Engine — Runtime Hard-Constraint Evaluation

> **Plan 11 (Faz 0-4 tamamlandı).** Auto-schedule (V2 CP-SAT) ile manuel runtime check arasındaki silent drift'leri kapatmak için unified hard-rule predicate katmanı. Faz 5 (V1 evaluator silme) production'da `Schedule.UseV2RuntimeRules` = true ile 1 hafta gözlem sonrası uygulanır.

Son güncelleme: 2026-05-15

---

## Mimari Genel Bakış

```
┌─────────────────────────────────────────────────────────────────┐
│                  Frontend (React + TypeScript)                  │
└──────────────────────────────┬──────────────────────────────────┘
                               │
                               │ /api/scheduling/validate
                               │ /api/scheduling/conflicts
                               │ /api/faculty-planning/apply-schedule
                               ▼
┌─────────────────────────────────────────────────────────────────┐
│              SchedulingService (Infrastructure)                 │
│  - ValidateAsync(request, ct)                                   │
│  - GetConflictsAsync(termId, ct)                                │
│                                                                 │
│  if SystemConfig.Schedule.UseV2RuntimeRules == true:            │
│      ├─ BuildTermSnapshotAsync (DB query)                       │
│      ├─ RuntimeSnapshotAdapter.FromTermSnapshot                 │
│      ├─ IRuleEngine.EvaluateCandidateAsync /                    │
│      │  IRuleEngine.EvaluateAllExistingAsync                    │
│      └─ Return SchedulingConflictDto[]  ← UI compat             │
│                                                                 │
│  else (default Faz 0-4 — V1 path):                              │
│      ├─ ConstraintEvaluationService.EvaluateCandidate /         │
│      │  EvaluateAllExisting                                     │
│      ├─ V1 evaluators (17) çalıştırılır                         │
│      └─ Return SchedulingConflictDto[]                          │
└─────────────────────────────────────────────────────────────────┘
                               ▲
                               │
                  (Faz 5 sonu V1 path silinir, flag kaldırılır)
                               │
                               ▼
┌─────────────────────────────────────────────────────────────────┐
│              RuleEngine (Infrastructure/Rules)                  │
│  - 17 IRulePredicate inject edilir (DI scoped)                  │
│  - Plan 10 disabled/exempt/category orchestration               │
│  - Bidirectional conflict dedup                                 │
│  - RuleViolation → SchedulingConflictDto mapping                │
└─────────────────────────────────────────────────────────────────┘
                               │
                               ▼ (her predicate ayrı çalışır)
┌─────────────────────────────────────────────────────────────────┐
│           IRulePredicate (17 implementation)                    │
│  H1 ClassroomConflict   H2 InstructorConflict                   │
│  H3 Capacity            H4 ClassroomType (Amphi flag-gated)     │
│  H5 CurriculumConflict  H6 TheoryLabComponent                   │
│  H8 SameCourseSection   H10 FacultyClassroom                    │
│  H11 Corequisite        H12 CreditHour                          │
│  H13 LunchBreak         H15 MaxConsecutiveHours                 │
│  H16 TheoryLabDaySplit  H17 FaceToFaceSlot                      │
│  H-DL DailyLoad         H-DO NoDayOff                           │
│  H-WL WeeklyLoad                                                │
└─────────────────────────────────────────────────────────────────┘
```

---

## Tipler

### `IRulePredicate` ([SIS.Application/Scheduling/Rules/IRulePredicate.cs](../../SIS.Application/Scheduling/Rules/IRulePredicate.cs))

Tek source-of-truth hard-rule predicate. V2 encoder mantığının saf C# portu; CP-SAT modellemesi olmadan.

```csharp
public interface IRulePredicate
{
    string Code { get; }                                // "H1", "H-DL" vb.
    RuleCategory DefaultCategory { get; }               // Hard | Soft (Plan 10 DB override)
    IReadOnlyList<RuleViolation> Evaluate(RuleContext ctx);
}
```

**Implementation kuralları:**
- Sync (manuel atama hot-path; async I/O yok).
- Throw etmemeli; ihlaller listede döner.
- Self-skip (paralel-lab): aynı `SectionId`'nin kendi block'ları çakışma sayılmaz.

### `RuleContext` ([SIS.Application/Scheduling/Rules/RuleContext.cs](../../SIS.Application/Scheduling/Rules/RuleContext.cs))

```csharp
public sealed class RuleContext
{
    public required RuntimeCandidate Candidate { get; init; }
    public required RuntimeSnapshot Snapshot { get; init; }
    public required SchedulingConstraintConfig Config { get; init; }
    public ConstraintState? State { get; init; }
}
```

### `RuntimeCandidate` ([SIS.Application/Scheduling/Rules/RuntimeCandidate.cs](../../SIS.Application/Scheduling/Rules/RuntimeCandidate.cs))

V1 `SectionSnapshot`'ın yeni adı — hafif, CP-SAT olmayan saf veri. Blocks paralel-split aware (aynı pencere birden fazla ClassroomId).

### `RuntimeSnapshot` ([SIS.Application/Scheduling/Rules/RuntimeSnapshot.cs](../../SIS.Application/Scheduling/Rules/RuntimeSnapshot.cs))

V1 `TermSnapshot`'ın yeni adı — Sections + ClassroomAssignments + Corequisites + SlotAllowedModes + Cohort/Enrollment pairs.

### `RuleEngine` ([SIS.Infrastructure/Services/Scheduling/Rules/RuleEngine.cs](../../SIS.Infrastructure/Services/Scheduling/Rules/RuleEngine.cs))

Orchestrator. Plan 10 davranışları:
- `state.IsDisabled(predicate.Code)` → skip
- `state.IsExempt(predicate.Code, ...)` → skip
- `ResolveCategory(predicate, state)` Soft ise → predicate çağrılmaz (hard-only kapsam)
- Bidirectional dedup: `BuildDedupKey(conflict)` ile aynı çift tek violation

---

## Plan 11 Drift Kararları (Faz 3'te uygulandı)

| Kural | V1 davranışı | V2/Predicate davranışı | Karar |
|---|---|---|---|
| **H4 Amphi** | Her zaman | `AmphiThresholdEnabled` flag-gated, default false | V2 mantığı (predicate de flag'e saygılı) |
| **H-DL DailyLoad** | Hardcoded > 9 | `config.MaxDailyHours` (default 9) | V2 (config-driven) |
| **H-DO NoDayOff** | Hardcoded ≥ 5 | `usedDays ≤ workdays-1` (generalize) | V2 (generalize) |
| **H-WL WeeklyLoad** | InstructorMaxWeeklyHours=0 → disabled | Override > 0 ? override : default 30 (Faz 4'te SystemConfig fallback) | V2 (default uygulanır) |
| **H16 TheoryLabDaySplit** | Aynı gün OK (lab sonra) | `theoryDay != labDay` (farklı gün zorunlu) | **V2 sıkı seçildi** (kullanıcı 2026-05-15 onayladı) |
| **H11 Corequisite** | "non-conflicting pair exists" enforce | V2 yok, predicate **V1 mantığını alır** | V1 enforce (V2 Plan 6.5'a ertelendi) |
| **H12 CreditHour** | AssignedTimeSlotCount == T+L | V2 encoder yok | V1 enforce (V2 doğal pass eder) |

---

## Rollout Planı (Plan 11 Faz 4.7 + 5)

### Mevcut durum (Faz 4 sonu)
- Feature flag `Schedule.UseV2RuntimeRules` = **false** (DB default).
- V1 evaluator path aktif.
- RuleEngine ve 17 predicate hazır + DI register'lı + H17 unit test pass.

### Aktivasyon adımları
1. **Test ortamında flag'i aç:**
   ```sql
   UPDATE "SystemConfigs" SET "Value" = 'true' WHERE "Key" = 'Schedule.UseV2RuntimeRules';
   ```
2. **Smoke test** (7 nokta — bkz. Plan 11 Faz 6):
   - Manuel atama (admin elle section atar)
   - Schedule editor drag-drop
   - Dashboard çakışma listesi
   - ConflictBar uyarı çubuğu
   - SimulationService what-if
   - Score breakdown
   - Auto-schedule apply
3. **Production'da flag'i aç + 1 hafta gözlem** (Plan 11 Faz 4.7).
4. **0 incident** → Faz 5 başlat: V1 evaluator + ConstraintEvaluationService sil.
5. Flag silindikten sonra RuleEngine tek path.

### Rollback
Sorun çıkarsa:
```sql
UPDATE "SystemConfigs" SET "Value" = 'false' WHERE "Key" = 'Schedule.UseV2RuntimeRules';
```
V1 path anında geri aktif. Migration revert gerekmez.

---

## Bekleyen İş

- **Faz 3 kalan:** 16 kural için unit test (H17 dışında). Pattern: [H17_FaceToFaceSlotRuleTests](../../SIS.Tests/Scheduling/Rules/Hard/H17_FaceToFaceSlotRuleTests.cs).
- **Faz 4.6:** RuleEngine path için performance benchmark (V1 baseline ile karşılaştırma).
- **Faz 5:** V1 evaluator + ConstraintEvaluationService + SchedulingConstraintContext silme.
- **HWL default fallback:** SystemConfig `Schedule.DefaultInstructorMaxWeeklyHours`'ı predicate'a inject etme (şu an V1 paritesinde — override=0 ise disable).

---

## Referanslar

- Plan 11 master: [docs/superpowers/plans/2026-05-15-plan-11-v1-evaluator-elimination.md](../superpowers/plans/2026-05-15-plan-11-v1-evaluator-elimination.md)
- Drift matrix: [docs/CODEMAPS/v1-v2-drift-matrix.md](v1-v2-drift-matrix.md)
- Perf baseline placeholder: [docs/CODEMAPS/perf-baseline-plan11.md](perf-baseline-plan11.md)
