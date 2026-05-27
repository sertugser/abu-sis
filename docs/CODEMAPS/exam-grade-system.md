# Exam & Grade Computation System — Codemap

**Oluşturulma:** 2026-05-26  
**Branch:** `feat/sinav-mufredati-overhaul`

---

## Overview

**"Sınav Müfredatı"** = program-level `ExamRegulation` policy that governs per-course grade computation for every section finalized under that program+term.

- Admin page `/sinav-mufredati` → define regulations per program/TermType/year.
- Instructor page `/not-girisi/{sectionId}` → enter scores, mark exam attendance, finalize → harf notu.
- Replaced the old per-course `ExamCurriculumPage` (deleted). Regulations now live on the program, not the course.

---

## Entities

### `ExamRegulation` — `SIS.Domain/Entities/ExamRegulation.cs:5`

Unique key: `(ProgramId, DegreeLevel, TermType, Year)`.

| Field group | Fields |
|---|---|
| **Identity/scope** | `Id`, `ProgramId` → `AcademicProgram`, `DegreeLevel` (Bachelor/…), `TermType`, `Year`, `IsActive`, `Name` |
| **Exam counts** | `MidtermCount`, `FinalCount` |
| **Weight bounds** | `MinMidtermWeightPercent`, `MaxMidtermWeightPercent`, `MinFinalWeightPercent`, `MaxFinalWeightPercent` |
| **Passing/floors** | `PassingGrade`, `RawScoreFloor1`, `RawScoreFloor2`, `FinalExamFloor` |
| **Below-floor grade** | `BelowFloorGrade` (`GradeCode`, default `FF`) — grade assigned when any floor/passing check fails |
| **Absolute letter thresholds** | `AAMin`, `BAMin`, `BBMin`, `CBMin`, `CCMin`, `DCMin`, `DDMin` (lower-inclusive); `LetterRangesEditable` |
| **Relative grading** | `EvaluationType` (`Absolute`/`Relative`), `RelativeMinStudentCount`, `RelativeInclusionLimit`, `RelativeAbsoluteFloor`, T-score thresholds: `TAAMin`, `TBAMin`, `TBBMin`, `TCBMin`, `TCCMin`, `TDCMin`, `TDDMin` |
| **Exam-rule toggles (all opt-in, default false)** | `BlockFinalIfNoMidterm` (Rule A), `FailIfNoFinal` (Rule C), `BlockMakeupIfNoFinal` (Toggle B), `ZeroIfMissedMakeup` (Toggle D), `MakeupReplacesFinal` (Toggle E), `AutoTransferFailedToMakeup` (Toggle F), `AutoTransferExcusedToMakeup` (Toggle G) |
| **Audit** | `CreatedAt`, `UpdatedAt?` |

Global edit lock: `SystemConfig "ExamRegulation.GlobalEditLock"` (bool) — blocks mutations for non-SysAdmin.

### `GradeComponent` — `SIS.Domain/Entities/GradeComponent.cs:5`

Scoped to `(CourseId, AcademicTermId)` — shared across all sections of the same course in that term.

| Field | Notes |
|---|---|
| `Name` | Display label ("Vize", "Final", "Ödev 1") |
| `ExamType` | Legacy free-text — display/export only. Do NOT use for logic. |
| `Type` | `GradeComponentType` — authoritative for weight-bounds enforcement and Final-floor checks. Defaults to `Other` (legacy behavior: bounds check skipped). |
| `Weight` | Contribution percent (0–100). All components must sum to 100 before finalize. |
| `Order` | Display order |
| `IsRequired`, `MinimumScore?`, `RequiresAttendanceEligibility`, `PrerequisiteGradeComponentId?` | Extended rules (stored but not enforced by current finalize pipeline) |

### `StudentGrade` — `SIS.Domain/Entities/StudentGrade.cs:5`

One row per `(CourseRegistrationId, GradeComponentId)`.

| Field | Notes |
|---|---|
| `Score?` | Raw score 0–100; null = not yet entered |
| `MakeupScore?` | Bütünleme/mazeret score stored here. Original `Score` is preserved. When non-null and `useMakeupScore=true`, this value is used in place of `Score`. |
| `ExamAttendance` | `NotMarked` / `Present` / `Absent` (deliberate 0) / `Excused` (→ Grade=I at finalize) |

### `GradingCriteria` — `SIS.Domain/Entities/GradingCriteria.cs:5`

Scoped to `(CourseId, AcademicTermId)`. Stores instructor-editable letter thresholds (`AAMin`…`DDMin`) + `ResolveGrade(int)` method (`GradeController.cs:28`). Used as fallback when regulation thresholds are not locked or differ across programs. Regulation thresholds take precedence when `LetterRangesEditable=false` OR no `GradingCriteria` row exists.

---

## Enums — `SIS.Domain/Enums/`

| Enum | Values | File |
|---|---|---|
| `GradeEvaluationType` | `Absolute=0`, `Relative=1` | `GradeEvaluationType.cs:3` |
| `GradeComponentType` | `Other=0`, `Midterm=1`, `Final=2` | `GradeComponentType.cs:3` |
| `ExamAttendance` | `NotMarked=0`, `Present=1`, `Absent=2`, `Excused=3` | `ExamAttendance.cs:3` |
| `AttendanceMethod` | `Separate=0`, `TotalHours=1`, `Passive=2`, `MarkOnly=3` | `AttendanceMethod.cs:8` |
| `GradeCode` | `AA BA BB CB CC DC DD FF W I NI NA S U` | `GradeCode.cs:3` — note: `NA`=devamsızlıktan kalma (FF equiv. in GPA), `I`=Incomplete (excused), `FF`=fail |

---

## Resolver — `IExamRegulationResolver`

**Interface:** `SIS.Application/Grading/IExamRegulationResolver.cs:6`  
**Implementation:** `SIS.Infrastructure/Services/ExamRegulationResolver.cs:9`

| Method | Purpose | Query strategy |
|---|---|---|
| `ResolveForStudentAsync(studentId, termId)` | Per-student resolution (2 queries) | term → student's Main+active program → matching regulation |
| `ResolveForStudentsAsync(studentIds, termId)` | Bulk variant (3 queries, no N+1) | term once → all student programs → all matching regulations |
| `ResolveCourseWeightBoundsAsync(courseId, termId)` | Returns `WeightBounds` record: strictest intersection across programs | Calls `GetCourseRegulationsAsync` |
| `ResolveCourseLetterPolicyAsync(courseId, termId)` | Returns `LetterPolicy`: editable flag + unique-thresholds flag | Calls `GetCourseRegulationsAsync` |
| `ResolveCourseRelativePolicyAsync(courseId, termId)` | Returns `RelativePolicy`: EvaluationType + T-score thresholds + BelowFloorGrade + conflict flag | Calls `GetCourseRegulationsAsync` |

**Shared private helper** `GetCourseRegulationsAsync` (`ExamRegulationResolver.cs:175`): fetches all active, DegreeLevel-matched regulations for every program whose curriculum includes `courseId` in the given term. Returns `null` when term/programs/regulations are absent.

**Result records** (defined in `IExamRegulationResolver.cs:39`):
- `WeightBounds(MinMidterm, MaxMidterm, MinFinal, MaxFinal, IsEmpty)` — `IsEmpty=true` when min > max (conflicting programs)
- `LetterPolicy(AAMin…DDMin, Editable, HasUniqueThresholds)`
- `RelativePolicy(EvaluationType, MinStudentCount, InclusionLimit, AbsoluteFloor, TAAMin…TDDMin, BelowFloorGrade, IsConflicting)`

---

## Grade Computation Pipeline — `GradeController.Finalize`

**File:** `SIS.API/Controllers/GradeController.cs:974`  
**Endpoint:** `POST api/instructor/section/{sectionId}/finalize`  
**Permission:** `menu.not-girisi.finalize`

Order of operations:

1. **Weight-sum check** (`GradeController.cs:1003`): components must sum to exactly 100.
2. **Weight-bounds enforcement** (`GradeController.cs:1007`): `ResolveCourseWeightBoundsAsync` → validates midterm+final totals vs. regulation bounds. Skipped when all components are `Type=Other` (legacy unclassified courses).
3. **Effective criteria resolution** (`ResolveEffectiveCriteriaAsync`, `GradeController.cs:2509`): priority — (a) regulation unique locked thresholds → transient `GradingCriteria`; (b) section `GradingCriteria` row; (c) error (conflicting locked policy or no criteria at all).
4. **Attendance NA** (`GradeController.cs:1056`): builds absence hours via `AttendanceAbsenceCalculator.CalculateAbsenceHours` + `IsFailedByAbsence` (method-aware). Students over limit → `GradeCode.NA`; excluded from subsequent steps.
5. **Excused → Incomplete split** (`GradeController.cs:1136`): any non-NA student with `ExamAttendance==Excused` on any component → `GradeCode.I`; excluded from scoring.
6. **Missing-grades guard** (`GradeController.cs:1154`): graded (non-NA, non-I) students must have a score or `Absent` mark for every component — 400 error otherwise.
7. **Pre-pass: `ComputeStudentWeightedScore`** (`GradeController.cs:2268`, `useMakeupScore=false`): for each graded student, computes `(weightedScore, roundedScore)`. Rule A (`BlockFinalIfNoMidterm`): if enabled and student attended no midterm → Final-type component scores forced to 0.
8. **Relative curve** (`GradeController.cs:1214`): `ResolveCourseRelativePolicyAsync` → if `Relative` and not conflicting → `RelativeGradingService.Evaluate` → `relativeLetters` dict. Falls back to absolute if `FellBackToAbsolute=true`.
9. **Per-student letter assign** (`GradeController.cs:1244`): base letter from `relativeLetters[regId]` or `effectiveCriteria.ResolveGrade(roundedScore)`, then `ApplyGradeRules`.
10. **`ApplyGradeRules`** (`GradeController.cs:2376`): final-floor check (`FinalExamFloor` vs. raw Final average → `BelowFloorGrade`), passing-grade check (`PassingGrade` vs. `roundedScore` → `BelowFloorGrade`), Rule C (`FailIfNoFinal`: Absent from Final → `BelowFloorGrade`).
11. **Finalize**: `section.IsFinalized=true`, save, then `GpaCalculationService.RecalculateAndUpdateAsync` for each student.

---

## Shared Computation Helpers

### `ComputeStudentWeightedScore` — `GradeController.cs:2268`
Pure static. Handles: Absent→0, useMakeupScore flag, Rule A (BlockFinalIfNoMidterm). Throws `InvalidOperationException` if a grade row is missing (by design — fails loud).

### `ComputeStudentFinalRaw` — `GradeController.cs:2339`
Pure static. Returns average raw score of all `Final`-type components (null if none). Handles Absent→0 and MakeupScore override.

### `ApplyGradeRules` — `GradeController.cs:2376`
Pure static. Applies `FinalExamFloor`, `PassingGrade`, Rule C on top of a base letter. Returns final `GradeCode`.

All three helpers are reused identically by `Finalize`, `FinalizeButunleme`, and `FinalizeMazeret`.

---

## Shared Services

### `RelativeGradingService` — `SIS.Application/Grading/RelativeGradingService.cs:84`

Pure stateless static class. No DB, no HTTP. Deterministic and unit-tested.

**Algorithm:**
1. Build "included" set: students with `RawScore >= InclusionLimit`.
2. If `included.Count < MinStudentCount` → fallback (`FellBackToAbsolute=true`).
3. Population mean μ = avg(included scores).
4. Population stddev σ = sqrt(Σ(x−μ)² / n) — population (÷n), not sample (÷n−1).
5. If σ < 0.0001 → fallback (all identical scores).
6. Per student: (a) `AbsoluteFloor > 0 && score < floor` → `BelowFloorGrade`; (b) not in included set → `BelowFloorGrade`; (c) T = 50 + 10*(score − μ)/σ → map via T-threshold table. Students scoring below `TDDMin` in step (c) receive `FF` (not `BelowFloorGrade` — that is reserved for (a)/(b) only).

Input: `RelativeGradeInput(RegistrationId, RawScore)` list + `RelativeGradeParams`.  
Output: `RelativeGradeOutcome(FellBackToAbsolute, Letters: dict<RegistrationId, GradeCode>)`.

### `AttendanceAbsenceCalculator` — `SIS.Application/Grading/AttendanceAbsenceCalculator.cs:36`

Pure static. Single source of truth for absence math shared between `GradeController.Finalize` (NA assignment) and `CourseRegistrationService` (student-facing status display).

| Method | Signature | Purpose |
|---|---|---|
| `CalculateAbsenceHours` | `(records, theoryHours, labHours, appHours, scheduleSlots?)` → `(int TheoryHours, int LabHours)` | Computes per-type absence hours; falls back to inferred slot-block when schedule not available |
| `CalculateLimitHours` | `(totalHours, limitPercent)` → `int` | Ceiling division; threshold is strictly-greater-than |
| `IsFailedByAbsence` (overload 1) | `(theory, lab, theoryLimit, labLimit)` | Legacy Separate-only check |
| `IsFailedByAbsence` (overload 2) | `(method, theory, lab, theoryLimit, labLimit, totalLimit)` | Method-aware: `Passive`/`MarkOnly`→never fail; `TotalHours`→combined check; `Separate`→per-type check |

`AttendanceMethodExtensions.ParseAttendanceMethod` parses `SystemConfig` string to enum (case-insensitive, defaults to `Separate`).

---

## Makeup Flows

### Bütünleme (Resit — FF students)

| Endpoint | Method | Permission | Purpose |
|---|---|---|---|
| `api/instructor/section/{id}/makeup/butunleme` | GET | `menu.not-girisi.view` | Roster of FF students eligible for resit. Filters: Toggle F (`AutoTransferFailedToMakeup`) must be on; Toggle B (`BlockMakeupIfNoFinal`) skips students Absent from Final. |
| `api/instructor/section/{id}/makeup/butunleme-scores` | PUT | `menu.not-girisi.edit` | Saves `MakeupScore` on Final-type `StudentGrade` rows. Original `Score` unchanged. |
| `api/instructor/section/{id}/makeup/butunleme-finalize` | POST | `menu.not-girisi.edit` | Recomputes grade for eligible FF students. Toggle E (`MakeupReplacesFinal`) must be on; Toggle D (`ZeroIfMissedMakeup`) forces 0 for missing makeup scores. Uses `useMakeupScore=true` + absolute `effectiveCriteria` (relative curve NOT re-run). Updates `CourseRegistration.Grade` + GPA. |

**File:** `GradeController.cs:1345–1763`

### Mazeret (Excuse — Incomplete students)

| Endpoint | Method | Permission | Purpose |
|---|---|---|---|
| `api/instructor/section/{id}/makeup/mazeret` | GET | `menu.not-girisi.view` | Roster of `Grade==I` students where Toggle G (`AutoTransferExcusedToMakeup`) is on. Returns excused-component list with original Score + MakeupScore. |
| `api/instructor/section/{id}/makeup/mazeret-scores` | PUT | `menu.not-girisi.edit` | Saves `MakeupScore` on the Excused component's `StudentGrade` row; can span any component type. |
| `api/instructor/section/{id}/makeup/mazeret-finalize` | POST | `menu.not-girisi.edit` | Readiness check (all components must have a usable score: Absent→0, Excused→MakeupScore present, normal→Score present). Ready students recomputed `useMakeupScore=true` + absolute criteria. Grade changes from I to final letter. GPA updated. |

**File:** `GradeController.cs:1765–2135`

---

## Attendance (Global SystemConfig)

Attendance limits are university-wide (not per-regulation). Read from `SystemConfig` via `ISystemConfigProvider`:

| Key | Default | Purpose |
|---|---|---|
| `Attendance.TermWeekCount` | `14` | Weeks per term — multiplied by course hours to get total hours |
| `Attendance.TheoryAbsenceLimitPercent` | `30` | Theory absence ceiling (%) |
| `Attendance.LabAbsenceLimitPercent` | `20` | Lab absence ceiling (%) |
| `Attendance.Method` | `"Separate"` | `AttendanceMethod` enum string — controls how `IsFailedByAbsence` evaluates |

Exam-attendance marking endpoint: `PUT api/instructor/section/{id}/exam-attendance` (`GradeController.cs:880`) — sets `StudentGrade.ExamAttendance` per component; creates grade row if absent.

**Method options:**
- `Separate` (default): fails if theoryAbs > theoryLimit OR labAbs > labLimit
- `TotalHours`: fails if (theoryAbs + labAbs) > totalLimit (uses theory% as general percent)
- `Passive`: never fails
- `MarkOnly`: never fails (informational)

---

## Controller — `ExamRegulationController`

**File:** `SIS.API/Controllers/ExamRegulationController.cs:13`  
**Route:** `api/admin/exam-regulations`

| Endpoint | Method | Permission | Notes |
|---|---|---|---|
| `GET /` | List | `menu.sinav-mufredati.view` | Optional `?programId` filter |
| `GET /{id}` | Get | `menu.sinav-mufredati.view` | |
| `POST /` | Create | `menu.sinav-mufredati.create` | Scope uniqueness enforced: `(ProgramId, DegreeLevel, TermType, Year)` |
| `PUT /{id}` | Update | `menu.sinav-mufredati.edit` | |
| `DELETE /{id}` | Delete | `menu.sinav-mufredati.delete` | |
| `POST /{id}/clone` | Clone | `menu.sinav-mufredati.create` | Target year required; target programId optional (defaults to source) |
| `GET /attendance-policy` | Read attendance config | `menu.sinav-mufredati.view` | Returns `AttendancePolicyDto` from SystemConfig |

All mutations check `ExamRegulation.GlobalEditLock` SystemConfig (non-SysAdmin blocked when true).

**DTO records** (same file, line 300+): `ExamRegulationDto`, `SaveExamRegulationRequest`, `CloneExamRegulationRequest`, `AttendancePolicyDto`.

---

## Controller — `GradeController`

**File:** `SIS.API/Controllers/GradeController.cs:22`  
**Route prefix:** `api/instructor`  
**Constructor dependencies:** `AppDbContext`, `IGpaCalculationService`, `IDateTimeProvider`, `IExamRegulationResolver`, `ISystemConfigProvider`, `IWebModuleControlService`

| Endpoint | Method | Permission | Purpose |
|---|---|---|---|
| `GET section/{id}/components` | List | `.view` | Grade components for course/term |
| `POST section/{id}/components` | Upsert | `.edit` | Add or update a component (Type: Other/Midterm/Final) |
| `DELETE section/{id}/components/{cId}` | Delete | `.edit` | Removes component + all its StudentGrade rows |
| `GET section/{id}/grades` | List | `.view` | All student scores + weighted averages + attendance |
| `POST section/{id}/grades` | Submit | `.submit` | Bulk score entry for one component |
| `GET section/{id}/grades/export-template` | Export | `.export` | .xlsx template with current scores |
| `POST section/{id}/grades/import-preview` | Preview | `.import` | Parse .xlsx, return diff preview |
| `POST section/{id}/grades/import-apply` | Apply | `.import` | Apply .xlsx after preview confirmation |
| `PUT section/{id}/exam-attendance` | Set | `.edit` | Bulk mark ExamAttendance per component |
| `POST section/{id}/finalize` | Finalize | `.finalize` | Full grade computation pipeline |
| `POST section/{id}/unfinalize` | Unfinalize | `.unfinalize` | Clears grades, recalculates GPA |
| `GET section/{id}/grading-criteria` | Get | `.view` | Letter thresholds (shows effective: regulation or instructor) |
| `PUT section/{id}/grading-criteria` | Update | `.edit` | Update instructor-level thresholds (locked if `LetterRangesEditable=false`) |
| `GET section/{id}/makeup/butunleme` | List | `.view` | FF roster eligible for resit |
| `PUT section/{id}/makeup/butunleme-scores` | Set | `.edit` | Save resit scores |
| `POST section/{id}/makeup/butunleme-finalize` | Finalize | `.edit` | Apply resit, recompute grades |
| `GET section/{id}/makeup/mazeret` | List | `.view` | Incomplete roster eligible for excuse makeup |
| `PUT section/{id}/makeup/mazeret-scores` | Set | `.edit` | Save excuse makeup scores |
| `POST section/{id}/makeup/mazeret-finalize` | Finalize | `.edit` | Apply excuse makeup, recompute grades |

Permission prefix: `menu.not-girisi.` (e.g. `menu.not-girisi.view`, `.edit`, `.submit`, `.finalize`, `.unfinalize`, `.export`, `.import`)

Grade entry is gated by `IWebModuleControlService.EvaluateAsync("instructor-grade-entry", termId)`. Component-name-based control codes also resolve: `midterm-grade-entry`, `final-grade-entry`, `makeup-grade-entry`, `midterm-excuse-grade-entry` (`GradeController.cs:2455`).

---

## Frontend

### Admin page — `ExamRegulationManagementPage`

| | |
|---|---|
| **Route** | `/sinav-mufredati` |
| **File** | `SIS.Web/src/pages/ExamRegulationManagementPage.tsx` (125 lines) |
| **Hooks** | `useAdminPrograms`, `useExamRegulations(programId?)`, `useDeleteExamRegulation`, `useCloneExamRegulation`, `useAttendancePolicy` |
| **Dialog** | `ExamRegulationFormDialog` (`SIS.Web/src/components/exam-regulation/ExamRegulationFormDialog.tsx`, 246 lines) — create/edit all fields including letter thresholds, relative grading, exam toggles, BelowFloorGrade |
| **Labels** | `SIS.Web/src/lib/examRegulationLabels.ts` — `TERM_LABELS`, `LEVEL_LABELS` |
| **API module** | `SIS.Web/src/api/examRegulation.api.ts` |
| **Hooks file** | `SIS.Web/src/hooks/useExamRegulations.ts` — `useExamRegulations`, `useCreateExamRegulation`, `useUpdateExamRegulation`, `useDeleteExamRegulation`, `useCloneExamRegulation`, `useAttendancePolicy` |

### Instructor page — `GradeEntryPage`

| | |
|---|---|
| **Route** | `/not-girisi/:sectionId` |
| **File** | `SIS.Web/src/pages/GradeEntryPage.tsx` (1257 lines) |
| **Features** | Component type badges (Other/Midterm/Final), per-component `ExamAttendance` dropdown (NotMarked/Present/Absent/Excused), live weighted-average preview, class-stats panel (`computeScoreStats`, `letterDistribution`, `passFailCounts` from `lib/gradeStats`), Excel export/import preview+apply, finalize/unfinalize, `ButunlemePanel` + `MazeretPanel` |
| **Hooks** | `useGrades`, `useComponents`, `useGradingCriteria`, `useAddComponent`, `useDeleteComponent`, `useSubmitGrades`, `useFinalize`, `useUnfinalize`, `useUpdateGradingCriteria`, `useDownloadTemplate`, `usePreviewImport`, `useApplyImport`, `useSetExamAttendance` — all in `SIS.Web/src/hooks/useGradeData.ts` |
| **Makeup components** | `SIS.Web/src/components/grade/MakeupSections.tsx` — `ButunlemePanel`, `MazeretPanel` |
| **API module** | `SIS.Web/src/api/grade.api.ts` |

---

## Permissions

| Key | Scope |
|---|---|
| `menu.sinav-mufredati.view` | List + get exam regulations, read attendance policy |
| `menu.sinav-mufredati.create` | Create + clone exam regulations |
| `menu.sinav-mufredati.edit` | Update exam regulations |
| `menu.sinav-mufredati.delete` | Delete exam regulations |
| `menu.not-girisi.view` | View grades, components, criteria, makeup rosters |
| `menu.not-girisi.edit` | Upsert/delete components, update criteria, set exam attendance, save makeup scores, makeup-finalize, set grades |
| `menu.not-girisi.submit` | Bulk grade submission |
| `menu.not-girisi.finalize` | Finalize section |
| `menu.not-girisi.unfinalize` | Unfinalize section |
| `menu.not-girisi.export` | Export Excel template |
| `menu.not-girisi.import` | Import from Excel |

---

## Migrations

All on branch `feat/sinav-mufredati-overhaul` (all 2026-05-25):

| Migration | File | What |
|---|---|---|
| `AddExamRegulation` | `20260525141452_AddExamRegulation.cs` | Initial `ExamRegulations` table |
| `AddExamRegulationLetterRanges` | `20260525165119_AddExamRegulationLetterRanges.cs` | `AAMin`…`DDMin`, `LetterRangesEditable` |
| `BackfillExamRegulationLetterRangeDefaults` | `20260525165644_BackfillExamRegulationLetterRangeDefaults.cs` | Backfill defaults for existing rows |
| `AddExamRegulationBelowFloorGrade` | `20260525175355_AddExamRegulationBelowFloorGrade.cs` | `BelowFloorGrade` column |
| `AddExamRegulationRelativeGrading` | `20260525183907_AddExamRegulationRelativeGrading.cs` | `EvaluationType`, `RelativeMinStudentCount`, `RelativeInclusionLimit`, `RelativeAbsoluteFloor`, T-score thresholds |
| `AddExamRegulationExamRules` | `20260525205548_AddExamRegulationExamRules.cs` | 7 exam-rule toggle columns |

---

## Tests

| File | Coverage |
|---|---|
| `SIS.Tests/Application/ExamRegulationResolverTests.cs` | Resolver single/bulk/course methods |
| `SIS.Tests/Application/RelativeGradingServiceTests.cs` | Bell-curve engine (fallback, inclusion, T-score mapping) |
| `SIS.Tests/Application/AttendanceAbsenceCalculatorTests.cs` | Absence hours + method-aware fail check |
| `SIS.Tests/API/ExamRegulationControllerTests.cs` | CRUD + clone + attendance-policy endpoints |

---

## Known Limitations / Deferred

| Item | Status |
|---|---|
| `RawScoreFloor1`, `RawScoreFloor2` | Stored in DB, exposed in UI — **not enforced** by `ApplyGradeRules` (only `FinalExamFloor` and `PassingGrade` are enforced). Deferred to a future phase. |
| Single-Final assumption | `ComputeStudentFinalRaw` averages all `Final`-type components. With multiple finals the "final floor" check uses the average, not each independently. |
| `GradeComponent.ExamType` (string) vs `Type` (enum) | `ExamType` is legacy free-text kept for display/export. `Type` is the authoritative enum. The two fields may diverge on pre-existing rows. |
| Relative extras (Faz 3b) | Tolerances, 2nd-range, TSKOR display — not yet built. `EvaluationType` and T-score thresholds are stored; the pipeline supports them. |
| Relative curve on makeup | Curve is intentionally NOT re-run for bütünleme/mazeret. Absolute `effectiveCriteria` always used. |
| `GradeComponent.IsRequired`, `MinimumScore`, `RequiresAttendanceEligibility`, `PrerequisiteGradeComponentId` | Stored, not enforced by the current finalize pipeline. |
