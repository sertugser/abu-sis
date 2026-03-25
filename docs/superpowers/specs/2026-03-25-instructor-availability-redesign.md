# Instructor Availability Page Redesign

## Problem

The current `InstructorSchedulingPage` ("Program Planlama") mixes three unrelated responsibilities:

1. **Availability management** — instructor defines weekly time windows (CRUD)
2. **Conflict analysis + schedule suggestion** — per-section validate/suggest/apply flow
3. **Weekly timetable** — visual read-only grid of all sections

With the Faculty Planning system now handling classroom/schedule assignment at the faculty level, responsibilities #2 is redundant. The page should be simplified to only what the instructor actually controls: their availability preferences.

## Solution

Simplify the page to **"Müsaitlik Yönetimi"** (Availability Management) with two sections:

1. **Availability CRUD card** — existing day-by-day grid with Add/Edit/Delete
2. **Weekly Timetable** — read-only `WeeklyTimetableGrid` showing all instructor sections with availability backgrounds

## What Gets Removed

### UI Components
- Section selector dropdown (no per-section operations)
- Tab system (`activePanel` state, analysis/suggestion/timetable tabs)
- Conflict Analysis panel (validate button, conflict table, validation results)
- Schedule Suggestion panel (suggest button, suggestion card, apply button)
- "Derslik ataması Fakülte Planlama sayfasından yapılmaktadır" info banner

### State & Logic
- `activePanel` state
- `selectedSectionId` / section selection via URL search params
- `validateMutation`, `suggestMutation`, `applyMutation` and all their error handling
- `resetValidation`, `resetSuggestion`, `resetApply` refs
- `handleValidate`, `handleSuggest`, `handleApplySuggestion` handlers
- All validation/suggestion/apply error message computations
- `CONFLICT_META` constant
- `PanelKey` type

### Sub-Components & Helpers to Remove
- `PanelButton` — tab button component
- `SummaryItem` — used only in suggestion panel
- `EmptyPanelState` — used only in analysis/suggestion panels
- `NotesAlert` — used only in analysis/suggestion panels
- `ConflictTable` — used only in analysis panel
- `AvailabilitySummaryCard` — used only in suggestion panel sidebar
- `formatConflictTime` — used only by ConflictTable
- `formatSlot` — used only in suggestion panel
- `formatWeeklyImpact` — used only in suggestion panel
- `collectConflictSectionIds` — used only for timetable conflict highlighting

### Imports to Remove
- `useValidateInstructorSchedule`, `useSuggestInstructorSchedule`, `useApplyInstructorSchedule` from hooks
- `useSearchParams` from react-router-dom (no longer tracking sectionId in URL). `useNavigate` stays (used in error/empty states).
- Icons no longer used after removal: `AlertCircle`, `CheckCircle2`, `Sparkles`, `Building2`, `Users`
- Type imports: `ScheduleSuggestion`, `SchedulingConflict` from types/scheduling. `ScheduleSlot` stays (used by timetable).

### Import that STAYS
- `getSchedulingErrorMessage` — used by availability error handling (create/update/delete/query), not just scheduling

## What Stays

### UI Components
- Page header: "Müsaitlik Yönetimi" with "+ Ekle" button
- Availability grid card — day columns, time slots, Uygun/Tercihli badges, Düzenle/Sil buttons
- Create/Edit availability dialog (day, start time, end time, isAvailable, isPreferred toggles)
- Delete confirmation dialog (AlertDialog)
- Weekly timetable — `WeeklyTimetableGrid` rendered directly (no tab), read-only, showing all sections with availability backgrounds
- Loading skeleton, error states for sections and availability queries

### State & Logic
- `isAvailabilityDialogOpen`, `editingAvailability`, `availabilityForm`, `availabilityFormError`
- `pendingDeleteAvailability`
- All availability CRUD mutations and handlers
- `useInstructorSections()` query (needed for timetable entries)
- `useInstructorAvailabilities()` query
- Availability mutations: `createAvailabilityMutation`, `updateAvailabilityMutation`, `deleteAvailabilityMutation`

### Hooks & API (no changes)
- `useInstructorAvailabilities`, `useCreateInstructorAvailability`, `useUpdateInstructorAvailability`, `useDeleteInstructorAvailability` — all stay
- `useInstructorSections` — stays (feeds timetable)
- Backend availability endpoints — no changes

## Page Layout

```
┌───────────────────────────────────────────┐
│ Müsaitlik Yönetimi              [+ Ekle] │
├───────────────────────────────────────────┤
│ Haftalık Müsaitlik                        │
│ ┌────────┬────────┬────────┬─────┬─────┐ │
│ │ Pzt    │ Salı   │ Çarş   │ Per │ Cum │ │
│ │ 09-12  │ 09-12  │ 09-12  │ ... │ ... │ │
│ │ [Uygun]│ [Terc] │ [Uygun]│     │     │ │
│ │ Düz|Sil│ Düz|Sil│ Düz|Sil│     │     │ │
│ │ 13-17  │        │ 13-17  │     │     │ │
│ │ [Terc] │        │ [Uygun]│     │     │ │
│ │ Düz|Sil│        │ Düz|Sil│     │     │ │
│ │ +Ekle  │ +Ekle  │ +Ekle  │+Ekle│+Ekle│ │
│ └────────┴────────┴────────┴─────┴─────┘ │
├───────────────────────────────────────────┤
│ Haftalık Görünüm                          │
│ ┌───────────────────────────────────────┐ │
│ │ WeeklyTimetableGrid (read-only)       │ │
│ │ - All instructor sections displayed   │ │
│ │ - Availability shown as backgrounds   │ │
│ │ - Green = preferred, Yellow = available│ │
│ └───────────────────────────────────────┘ │
└───────────────────────────────────────────┘
```

## Route & Navigation Changes

| Item | Before | After |
|------|--------|-------|
| Route path | `/program-planlama` | `/musaitlik-yonetimi` |
| Page title | Program Planlama | Müsaitlik Yönetimi |
| Sidebar label | Program Planlama | Müsaitlik Yönetimi |
| Constants key | `INSTRUCTOR_SCHEDULING: '/program-planlama'` | `INSTRUCTOR_SCHEDULING: '/musaitlik-yonetimi'` |

## Files Modified

| File | Change |
|------|--------|
| `SIS.Web/src/pages/InstructorSchedulingPage.tsx` | Remove scheduling logic, tabs, section selector; keep availability + timetable |
| `SIS.Web/src/lib/constants.ts` | Update `ROUTES.INSTRUCTOR_SCHEDULING` path and `IMPLEMENTED_ROUTES` set |
| `SIS.Web/src/components/layout/SidebarMenuGroups.ts` | Update label |
| `SIS.Web/src/App.tsx` | Update route path from `program-planlama` to `musaitlik-yonetimi` |

## Files NOT Modified

- Backend controllers — no changes
- Hook files — unused hooks stay in file (may be used elsewhere or cleaned up later)
- API client files — same
- Types — same
- WeeklyTimetableGrid component — same

## Timetable Data Flow

The timetable needs all instructor sections to display. Current flow stays the same:

1. `useInstructorSections()` fetches all sections for the instructor's active term
2. Each section's `scheduleJson` is parsed into `ScheduleSlot[]` via `parseScheduleJson()`
3. Sections are mapped to `TimetableEntry[]` for the grid
4. `useInstructorAvailabilities()` provides availability backgrounds
5. Grid renders read-only — no click handlers, no apply buttons
6. `buildTimetableEntries` called without conflict data (empty set for `conflictSectionIds`) since validate is removed

## Cleanup Notes

- `handleAvailabilitySubmit` and `handleDeleteAvailability` onSuccess callbacks currently call `resetValidation()` and `resetSuggestion()` — these lines must be removed
- Empty-sections message ("program analizi veya öneri üretimi yapılamıyor") must be updated to reference availability management instead
- The `includeScheduling` conditional flag in sidebar stays as-is (controls whether the menu item appears at all)
