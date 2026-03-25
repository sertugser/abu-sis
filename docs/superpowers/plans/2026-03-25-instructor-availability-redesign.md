# Instructor Availability Page Redesign — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Simplify InstructorSchedulingPage from a mixed scheduling/availability page to a focused "Müsaitlik Yönetimi" page with only availability CRUD and a read-only weekly timetable.

**Architecture:** Pure frontend refactor — strip scheduling logic (validate/suggest/apply, conflict analysis, section selector, tabs) from InstructorSchedulingPage.tsx, keep availability management and WeeklyTimetableGrid, update route/sidebar/constants.

**Tech Stack:** React 18, TypeScript, TanStack Query, shadcn/ui, Tailwind CSS

**Spec:** `docs/superpowers/specs/2026-03-25-instructor-availability-redesign.md`

---

## File Map

| File | Action | Responsibility |
|------|--------|---------------|
| `SIS.Web/src/pages/InstructorSchedulingPage.tsx` | Major edit | Remove ~700 lines of scheduling UI, keep ~700 lines of availability + timetable |
| `SIS.Web/src/lib/constants.ts` | Edit | Update route path and IMPLEMENTED_ROUTES |
| `SIS.Web/src/components/layout/SidebarMenuGroups.ts` | Edit | Update sidebar label |
| `SIS.Web/src/App.tsx` | Edit | Update route path |

---

### Task 1: Update route, constants, and sidebar

**Files:**
- Modify: `SIS.Web/src/lib/constants.ts:13` and `:80`
- Modify: `SIS.Web/src/App.tsx:56`
- Modify: `SIS.Web/src/components/layout/SidebarMenuGroups.ts:120`

- [ ] **Step 1: Update ROUTES.INSTRUCTOR_SCHEDULING in constants.ts**

Change line 13:
```typescript
// Before:
INSTRUCTOR_SCHEDULING: '/program-planlama',
// After:
INSTRUCTOR_SCHEDULING: '/musaitlik-yonetimi',
```

- [ ] **Step 2: Update IMPLEMENTED_ROUTES in constants.ts**

Change line 80:
```typescript
// Before:
'/program-planlama',
// After:
'/musaitlik-yonetimi',
```

- [ ] **Step 3: Update route path in App.tsx**

Change line 56:
```tsx
// Before:
<Route path="program-planlama" element={<InstructorSchedulingPage />} />
// After:
<Route path="musaitlik-yonetimi" element={<InstructorSchedulingPage />} />
```

- [ ] **Step 4: Update sidebar label in SidebarMenuGroups.ts**

Change line 120:
```typescript
// Before:
...(includeScheduling ? [{ to: ROUTES.INSTRUCTOR_SCHEDULING, label: 'Program Planlama' }] : []),
// After:
...(includeScheduling ? [{ to: ROUTES.INSTRUCTOR_SCHEDULING, label: 'Müsaitlik Yönetimi' }] : []),
```

- [ ] **Step 5: Verify frontend builds**

Run: `cd SIS.Web && npx tsc --noEmit`
Expected: 0 errors

- [ ] **Step 6: Commit**

```bash
git add SIS.Web/src/lib/constants.ts SIS.Web/src/App.tsx SIS.Web/src/components/layout/SidebarMenuGroups.ts
git commit -m "feat: rename instructor scheduling route and sidebar to Müsaitlik Yönetimi"
```

---

### Task 2: Simplify InstructorSchedulingPage — remove scheduling imports and state

This is the main task. We will edit `SIS.Web/src/pages/InstructorSchedulingPage.tsx` to remove all scheduling-related code while preserving availability management and the weekly timetable.

**Files:**
- Modify: `SIS.Web/src/pages/InstructorSchedulingPage.tsx`

- [ ] **Step 1: Remove scheduling-related imports**

Remove these imports (keep all others):

```typescript
// Line 1: REMOVE useEffect, ChangeEvent, ReactNode from react import. KEEP useState.
// Result: import { useState } from 'react';

// Line 2: REMOVE useSearchParams from react-router-dom. KEEP useNavigate (still used in error state).
// Result: import { useNavigate } from 'react-router-dom';

// KEEP this import — used by availability error handling too:
// import { getSchedulingErrorMessage } from '../api/instructorScheduling.api';

// REMOVE these from useInstructorScheduling import:
useApplyInstructorSchedule,
useSuggestInstructorSchedule,
useValidateInstructorSchedule,

// REMOVE these from the scheduling types import:
ScheduleSuggestion,
SchedulingConflict,
// KEEP: InstructorAvailability, ScheduleSlot, UpsertInstructorAvailabilityRequest

// REMOVE these lucide-react icons:
AlertCircle, Building2, CheckCircle2, Sparkles, Users
// KEEP: AlertTriangle, Calendar, Clock3, Pencil, Plus, RefreshCcw, Trash2

// REMOVE the entire Table component import (lines 29-35) — only used by ConflictTable which is being deleted:
// import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
```

- [ ] **Step 2: Remove scheduling-related types and constants**

Remove:
```typescript
// Remove PanelKey type (line 73):
type PanelKey = 'analysis' | 'suggestion' | 'timetable';

// Remove CONFLICT_META constant (lines 101-108):
const CONFLICT_META: Record<string, { ... }> = { ... };
```

- [ ] **Step 3: Remove scheduling state and mutations from main component**

Inside `export default function InstructorSchedulingPage()`, remove:

```typescript
// Remove useSearchParams usage:
const [searchParams, setSearchParams] = useSearchParams();
const [activePanel, setActivePanel] = useState<PanelKey>('analysis');

// Remove these mutation hooks:
const validateMutation = useValidateInstructorSchedule();
const suggestMutation = useSuggestInstructorSchedule();
const applyMutation = useApplyInstructorSchedule();

// Remove these derived values:
const selectedSectionId = Number(searchParams.get('sectionId'));
const selectedSection = sections.find(section => section.sectionId === selectedSectionId) ?? sections[0] ?? null;

// Remove these reset refs:
const resetValidation = validateMutation.reset;
const resetSuggestion = suggestMutation.reset;
const resetApply = applyMutation.reset;

// Remove the useEffect that syncs sectionId to URL (lines 147-160)
// Remove the useEffect that resets mutations on section change (lines 162-166)

// Remove error message computations for scheduling:
const sectionsErrorMessage = ...  // KEEP this one — used for error state
const validationErrorMessage = ...  // REMOVE
const suggestionErrorMessage = ...  // REMOVE
const applyErrorMessage = ...  // REMOVE

// Remove handlers:
const handleSectionChange = ...  // REMOVE (line 192)
const handleValidate = ...  // REMOVE (line 198)
const handleSuggest = ...  // REMOVE (line 207)
const handleApplySuggestion = ...  // REMOVE (line 216)

// Remove applyErrorMessage (line 238-240) — separate from other error computations:
const applyErrorMessage = ...  // REMOVE
```

- [ ] **Step 4: Clean up availability handlers that reference removed mutations**

In `handleAvailabilitySubmit` (around line 298-302), the `onSuccess` callback calls:
```typescript
resetValidation();
resetSuggestion();
```
Remove both lines. The `onSuccess` should only call `handleAvailabilityDialogChange(false)`.

In `handleDeleteAvailability` (around line 326-327), the `onSuccess` callback calls:
```typescript
resetValidation();
resetSuggestion();
```
Remove both lines. The `onSuccess` should only call `setPendingDeleteAvailability(null)`.

- [ ] **Step 5: Rewrite the main return JSX**

Replace the entire return block (from `return (<>` to the closing `</>)`) with:

```tsx
return (
  <>
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold tracking-tight">Müsaitlik Yönetimi</h1>
      </div>

      {/* Availability CRUD Card */}
      <AvailabilityManagerCard
        availabilities={availabilities}
        isLoading={availabilityQuery.isLoading}
        errorMessage={availabilityErrorMessage}
        mutationErrorMessage={availabilityMutationErrorMessage}
        onRetry={() => availabilityQuery.refetch()}
        onCreate={openCreateAvailabilityDialog}
        onEdit={openEditAvailabilityDialog}
        onDeleteRequest={setPendingDeleteAvailability}
      />

      {/* Weekly Timetable (read-only) */}
      <WeeklyTimetableGrid
        entries={buildTimetableEntries(sections, new Set())}
        availabilities={availabilities}
        title="Haftalık Ders Programı"
      />
    </div>

    <AvailabilityDialog
      isOpen={isAvailabilityDialogOpen}
      isPending={isSavingAvailability}
      errorMessage={availabilityFormError ?? availabilityMutationErrorMessage}
      isEditing={Boolean(editingAvailability)}
      form={availabilityForm}
      onOpenChange={handleAvailabilityDialogChange}
      onFieldChange={handleAvailabilityFieldChange}
      onSubmit={handleAvailabilitySubmit}
    />

    <AlertDialog
      open={Boolean(pendingDeleteAvailability)}
      onOpenChange={(open) => {
        if (!open && !isDeletingAvailability) {
          setPendingDeleteAvailability(null);
        }
      }}
    >
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>Müsaitlik kaydı silinsin mi?</AlertDialogTitle>
          <AlertDialogDescription>
            {pendingDeleteAvailability
              ? `${formatDay(pendingDeleteAvailability.dayOfWeek)} ${formatTimeRange(
                pendingDeleteAvailability.startTime,
                pendingDeleteAvailability.endTime,
              )} penceresi kaldırılacak.`
              : 'Bu işlem geri alınamaz.'}
          </AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel disabled={isDeletingAvailability}>İptal</AlertDialogCancel>
          <AlertDialogAction onClick={handleDeleteAvailability} disabled={isDeletingAvailability}>
            {isDeletingAvailability ? 'Siliniyor' : 'Sil'}
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  </>
);
```

Key changes:
- Removed blue banner ("Derslik ataması Fakülte Planlama...")
- Removed section selector dropdown and badges
- Removed tab system (PanelButton area)
- Removed Conflict Analysis panel
- Removed Schedule Suggestion panel
- WeeklyTimetableGrid rendered directly (not inside tab), with `new Set()` for conflictSectionIds
- Added page title "Müsaitlik Yönetimi"

Also update the empty-sections state message (around line 379):
```tsx
// Before:
<p className="max-w-xl text-sm text-muted-foreground">
  Bu dönem size atanmış section olmadığı için program analizi veya öneri üretimi yapılamıyor.
</p>
// After:
<p className="max-w-xl text-sm text-muted-foreground">
  Bu dönem size atanmış ders bulunamadı. Müsaitlik bilgilerinizi yine de tanımlayabilirsiniz.
</p>
```

And update the empty-state to still show availability card (not just return early). The empty state should be:
```tsx
if (!sectionsData || sections.length === 0) {
  return (
    <>
      <div className="space-y-6">
        <div className="flex items-center justify-between">
          <h1 className="text-2xl font-bold tracking-tight">Müsaitlik Yönetimi</h1>
        </div>

        <Card>
          <CardContent className="flex flex-col items-center justify-center gap-4 py-14 text-center">
            <div className="flex h-14 w-14 items-center justify-center rounded-2xl bg-muted">
              <Calendar className="h-7 w-7 text-muted-foreground" />
            </div>
            <div className="space-y-1">
              <h2 className="font-heading text-xl font-semibold text-foreground">Atanmış ders bulunamadı</h2>
              <p className="max-w-xl text-sm text-muted-foreground">
                Bu dönem size atanmış ders bulunamadı. Müsaitlik bilgilerinizi yine de tanımlayabilirsiniz.
              </p>
            </div>
          </CardContent>
        </Card>

        <AvailabilityManagerCard
          availabilities={availabilities}
          isLoading={availabilityQuery.isLoading}
          errorMessage={availabilityErrorMessage}
          mutationErrorMessage={availabilityMutationErrorMessage}
          onRetry={() => availabilityQuery.refetch()}
          onCreate={openCreateAvailabilityDialog}
          onEdit={openEditAvailabilityDialog}
          onDeleteRequest={setPendingDeleteAvailability}
        />
      </div>

      <AvailabilityDialog
        isOpen={isAvailabilityDialogOpen}
        isPending={isSavingAvailability}
        errorMessage={availabilityFormError ?? availabilityMutationErrorMessage}
        isEditing={Boolean(editingAvailability)}
        form={availabilityForm}
        onOpenChange={handleAvailabilityDialogChange}
        onFieldChange={handleAvailabilityFieldChange}
        onSubmit={handleAvailabilitySubmit}
      />

      <AlertDialog
        open={Boolean(pendingDeleteAvailability)}
        onOpenChange={(open) => {
          if (!open && !isDeletingAvailability) {
            setPendingDeleteAvailability(null);
          }
        }}
      >
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Müsaitlik kaydı silinsin mi?</AlertDialogTitle>
            <AlertDialogDescription>
              {pendingDeleteAvailability
                ? `${formatDay(pendingDeleteAvailability.dayOfWeek)} ${formatTimeRange(
                  pendingDeleteAvailability.startTime,
                  pendingDeleteAvailability.endTime,
                )} penceresi kaldırılacak.`
                : 'Bu işlem geri alınamaz.'}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel disabled={isDeletingAvailability}>İptal</AlertDialogCancel>
            <AlertDialogAction onClick={handleDeleteAvailability} disabled={isDeletingAvailability}>
              {isDeletingAvailability ? 'Siliniyor' : 'Sil'}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </>
  );
}
```

- [ ] **Step 6: Remove dead sub-components and helper functions**

Delete these entire function definitions from the file:

```typescript
// Sub-components to DELETE:
function PanelButton({ ... }) { ... }           // ~lines 777-799
function SummaryItem({ ... }) { ... }           // ~lines 802-822
function EmptyPanelState({ ... }) { ... }       // ~lines 825-831
function NotesAlert({ ... }) { ... }            // ~lines 834-852
function ConflictTable({ ... }) { ... }         // ~lines 854-907
function AvailabilitySummaryCard({ ... }) { ... } // ~lines 1078-1153

// Helper functions to DELETE:
function formatConflictTime(...) { ... }        // ~lines 1281-1287
function formatSlot(...) { ... }                // ~lines 1289-1291
function formatWeeklyImpact(...) { ... }        // ~lines 1313-1324
function collectConflictSectionIds(...) { ... } // ~lines 1388-1402
```

Keep these functions (still used):
- `AvailabilityManagerCard` — availability grid
- `AvailabilityDialog` — create/edit dialog
- `formatDay` — used by availability display
- `formatTimeRange` — used by availability display
- `formatTime` — used by formatTimeRange
- `normalizeTimeForInput` — used by edit dialog
- `toAvailabilityPayload` — used by form submission
- `buildTimetableEntries` — used by timetable (pass `new Set()` instead of conflict IDs)

- [ ] **Step 7: Verify TypeScript compiles**

Run: `cd SIS.Web && npx tsc --noEmit`
Expected: 0 errors

- [ ] **Step 8: Run ESLint**

Run: `cd SIS.Web && npx eslint src/pages/InstructorSchedulingPage.tsx`
Expected: 0 errors. Fix any unused import/variable warnings.

- [ ] **Step 9: Commit**

```bash
git add SIS.Web/src/pages/InstructorSchedulingPage.tsx
git commit -m "feat: simplify InstructorSchedulingPage to availability-only"
```

---

### Task 3: Final verification

- [ ] **Step 1: Full frontend build**

Run: `cd SIS.Web && npm run build`
Expected: Build succeeds with 0 errors.

- [ ] **Step 2: Full ESLint check**

Run: `cd SIS.Web && npx eslint .`
Expected: 0 errors, 0 warnings related to changed files.

- [ ] **Step 3: Verify no broken references**

Search for any remaining references to old route or removed components:
```bash
grep -r "program-planlama" SIS.Web/src/
grep -r "PanelButton\|SummaryItem\|EmptyPanelState\|NotesAlert\|ConflictTable\|AvailabilitySummaryCard" SIS.Web/src/ --include="*.tsx" --include="*.ts"
```
Expected: No results (all references removed).

- [ ] **Step 4: Commit if any fixes needed, then push**

```bash
git push origin feat/faculty-planning
```
