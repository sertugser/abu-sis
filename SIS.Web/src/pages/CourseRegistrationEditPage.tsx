import { useState, useEffect, useRef, useCallback } from 'react';
import { Check, X } from 'lucide-react';
import { Skeleton } from '@/components/ui/skeleton';
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog';
import { cn } from '@/lib/utils';
import { prolizButtonClass, prolizPrimaryButtonClass } from '@/lib/prolizButton';
import { RegistrationHeaderBar } from '../components/registration/v2/RegistrationHeaderBar';
import { SemesterClassTabs } from '../components/registration/v2/SemesterClassTabs';
import type { ParityFilter } from '../components/registration/v2/SemesterClassTabs';
import { CurriculumCourseGrid } from '../components/registration/v2/CurriculumCourseGrid';
import { SectionPickerDialog } from '../components/registration/v2/SectionPickerDialog';
import RetakeCoursesPanel from '../components/registration/v2/RetakeCoursesPanel';
import RegistrationStatusLine from '../components/registration/v2/RegistrationStatusLine';
import { SelectedCoursesGrid } from '../components/registration/v2/SelectedCoursesGrid';
import { RegistrationSummaryBar } from '../components/registration/v2/RegistrationSummaryBar';
import { useAdvisorStudents } from '../hooks/useAdvisor';
import { useAdvisorRegistrationCatalog } from '../hooks/useAdvisorRegistrationCatalog';
import {
  useAdvisorAddCourse,
  useAdvisorDropCourse,
  useApproveStudent,
  useRejectStudent,
} from '../hooks/useAdvisor';
import type { CatalogCourseRow } from '../types/registrationCatalog';

function deriveDefaultParity(termType: string): ParityFilter {
  const t = termType.toLowerCase();
  if (t.includes('fall') || t.includes('guz') || t.includes('güz')) return 'Guz';
  if (t.includes('spring') || t.includes('bahar')) return 'Bahar';
  return 'Tumu';
}

export default function CourseRegistrationEditPage() {
  const { data: students = [], isLoading: studentsLoading } = useAdvisorStudents();
  const [selectedStudentId, setSelectedStudentId] = useState<number | null>(null);

  const { data: catalog, isLoading: catalogLoading } = useAdvisorRegistrationCatalog(selectedStudentId);

  const [selectedClassYear, setSelectedClassYear] = useState<number | null>(null);
  const [parity, setParity] = useState<ParityFilter>('Tumu');
  const parityInitialized = useRef(false);
  const [pickerCourse, setPickerCourse] = useState<CatalogCourseRow | null>(null);

  // On first catalog load for a student, default selectedClassYear + parity
  useEffect(() => {
    if (!catalog || selectedClassYear !== null) return;
    const years = catalog.classYears.map((y) => y.classYear);
    if (years.length === 0) return;
    const target = catalog.student.classYear;
    const clamped = years.includes(target) ? target : years[0];
    setSelectedClassYear(clamped);
  }, [catalog, selectedClassYear]);

  useEffect(() => {
    if (!catalog || parityInitialized.current) return;
    setParity(deriveDefaultParity(catalog.term.termType));
    parityInitialized.current = true;
  }, [catalog]);

  // Reset derived state when student changes
  useEffect(() => {
    parityInitialized.current = false;
    setSelectedClassYear(null);
    setParity('Tumu');
    setPickerCourse(null);
  }, [selectedStudentId]);

  // Advisor mutations
  const advisorAdd = useAdvisorAddCourse();
  const advisorDrop = useAdvisorDropCourse();
  const approve = useApproveStudent();
  const reject = useRejectStudent();

  const [addError, setAddError] = useState<string | null>(null);
  const [removingId, setRemovingId] = useState<number | null>(null);
  const [removeError, setRemoveError] = useState<string | null>(null);
  const [rejectNote, setRejectNote] = useState('');
  const [rejectOpen, setRejectOpen] = useState(false);

  const handleAddSection = useCallback(async (sectionId: number): Promise<void> => {
    if (selectedStudentId === null) return;
    setAddError(null);
    return new Promise<void>((resolve, reject_) => {
      advisorAdd.mutate(
        { studentId: selectedStudentId, courseSectionId: sectionId },
        {
          onSuccess: () => resolve(),
          onError: (e) => {
            setAddError(e instanceof Error ? e.message : 'Eklenemedi.');
            reject_(e);
          },
        },
      );
    });
  }, [advisorAdd, selectedStudentId]);

  const handleRemove = useCallback(async (registrationId: number): Promise<void> => {
    if (selectedStudentId === null) return;
    setRemovingId(registrationId);
    setRemoveError(null);
    try {
      await new Promise<void>((resolve, reject_) => {
        advisorDrop.mutate(
          { studentId: selectedStudentId, registrationId },
          {
            onSuccess: () => resolve(),
            onError: (e) => {
              setRemoveError(e instanceof Error ? e.message : 'Çıkarılamadı.');
              reject_(e);
            },
          },
        );
      });
    } finally {
      setRemovingId(null);
    }
  }, [advisorDrop, selectedStudentId]);

  // Derived values from catalog
  const classYearList = catalog?.classYears.map((y) => y.classYear) ?? [];
  const effectiveClassYear = selectedClassYear ?? classYearList[0] ?? 1;
  const selectedYearData = catalog?.classYears.find((y) => y.classYear === effectiveClassYear) ?? null;
  const registeredSectionIds = catalog?.selectedCourses.map((s) => s.courseSectionId) ?? [];
  const totalEcts = catalog?.selectedCourses.reduce((sum, s) => sum + s.ects, 0) ?? 0;
  const totalHours = catalog?.selectedCourses.reduce((sum, s) => sum + s.theoryHours + s.labHours, 0) ?? 0;
  const courseCount = catalog?.selectedCourses.length ?? 0;

  return (
    <div className="grid grid-cols-1 md:grid-cols-[280px_1fr] gap-3 p-3">
      {/* LEFT: advisee list */}
      <aside className="overflow-hidden rounded-lg border border-slate-300 bg-white shadow-sm self-start">
        <div className="border-b border-slate-200 bg-slate-50 px-3 py-1.5 text-[12px] font-semibold text-slate-700">
          Danışmanlık Öğrencileri
        </div>
        {studentsLoading ? (
          <div className="p-2 space-y-1.5">
            <Skeleton className="h-10 w-full" />
            <Skeleton className="h-10 w-full" />
            <Skeleton className="h-10 w-full" />
          </div>
        ) : students.length === 0 ? (
          <div className="px-3 py-4 text-xs text-slate-500 italic">Danışmanlık öğrenciniz bulunmuyor.</div>
        ) : (
          <ul className="divide-y divide-slate-200 max-h-[80vh] overflow-y-auto">
            {students.map((s) => (
              <li key={s.studentId}>
                <button
                  type="button"
                  onClick={() => setSelectedStudentId(s.studentId)}
                  className={cn(
                    'w-full text-left px-3 py-2 hover:bg-slate-50 transition-colors',
                    selectedStudentId === s.studentId && 'bg-blue-50',
                  )}
                >
                  <div className="text-[12px] font-medium text-slate-900">{s.studentName}</div>
                  <div className="text-[11px] text-slate-500">
                    {s.studentNumber} · {s.planStatus ?? '—'}
                  </div>
                </button>
              </li>
            ))}
          </ul>
        )}
      </aside>

      {/* RIGHT: catalog (reuses v2 components) */}
      <main>
        {selectedStudentId === null ? (
          <div className="rounded-lg border border-slate-200 bg-white p-10 text-center text-slate-500 text-sm">
            Soldan bir öğrenci seçin.
          </div>
        ) : catalogLoading ? (
          <div className="space-y-2">
            <Skeleton className="h-20 w-full" />
            <Skeleton className="h-8 w-full" />
            <Skeleton className="h-64 w-full" />
          </div>
        ) : catalog ? (
          <>
            <div className="overflow-hidden rounded-lg border border-slate-300 bg-white shadow-sm">
              <RegistrationHeaderBar
                student={catalog.student}
                onDownloadPdf={() => { /* TODO: advisor PDF */ }}
                pdfLoading={false}
              />

              <SemesterClassTabs
                classYears={classYearList}
                selectedClassYear={effectiveClassYear}
                onClassYearChange={setSelectedClassYear}
                parity={parity}
                onParityChange={setParity}
              />

              <CurriculumCourseGrid
                classYearData={selectedYearData}
                parityFilter={parity}
                onPickSection={setPickerCourse}
                registrationActionsOpen={true}
              />

              {catalog.retakeCourses.length > 0 && (
                <RetakeCoursesPanel
                  retakeCourses={catalog.retakeCourses}
                  onCheck={() => { /* advisor does not run validation locally */ }}
                  checkLoading={false}
                  validationSummary={null}
                />
              )}

              <RegistrationStatusLine selectedCourses={catalog.selectedCourses} />

              <SelectedCoursesGrid
                selectedCourses={catalog.selectedCourses}
                registrationActionsOpen={true}
                onRemove={handleRemove}
                removingId={removingId}
                removeError={removeError}
              />

              <RegistrationSummaryBar
                totalCredit={0}
                totalEcts={totalEcts}
                ectsLimit={catalog.student.ectsLimit}
                totalHours={totalHours}
                courseCount={courseCount}
              />

              {/* Advisor action bar: Onayla + Reddet */}
              <div className="border-t border-slate-200 bg-slate-50 px-4 py-1.5 flex justify-end gap-2">
                <button
                  type="button"
                  className={prolizButtonClass}
                  onClick={() => setRejectOpen(true)}
                  disabled={reject.isPending}
                >
                  <X className="h-3 w-3 text-rose-600" />
                  Reddet
                </button>
                <button
                  type="button"
                  className={prolizPrimaryButtonClass}
                  onClick={() => approve.mutate({ studentId: selectedStudentId })}
                  disabled={approve.isPending}
                >
                  <Check className="h-3 w-3" />
                  Onayla
                </button>
              </div>
            </div>

            <SectionPickerDialog
              course={pickerCourse}
              open={pickerCourse !== null}
              onOpenChange={(o) => { if (!o) setPickerCourse(null); }}
              registeredSectionIds={registeredSectionIds}
              registrationActionsOpen={true}
              onAddSection={handleAddSection}
              isAdding={advisorAdd.isPending}
              addError={addError}
            />

            {/* Reject dialog */}
            <AlertDialog open={rejectOpen} onOpenChange={setRejectOpen}>
              <AlertDialogContent>
                <AlertDialogHeader>
                  <AlertDialogTitle>Kaydı Reddet</AlertDialogTitle>
                  <AlertDialogDescription>
                    Öğrenciye iletilecek bir not bırakın.
                  </AlertDialogDescription>
                </AlertDialogHeader>
                <textarea
                  className="w-full border rounded p-2 text-sm"
                  rows={3}
                  value={rejectNote}
                  onChange={(e) => setRejectNote(e.target.value)}
                  placeholder="Red gerekçesi..."
                />
                <AlertDialogFooter>
                  <AlertDialogCancel>İptal</AlertDialogCancel>
                  <AlertDialogAction
                    onClick={() => {
                      reject.mutate({ studentId: selectedStudentId, note: rejectNote });
                      setRejectOpen(false);
                      setRejectNote('');
                    }}
                  >
                    Reddet
                  </AlertDialogAction>
                </AlertDialogFooter>
              </AlertDialogContent>
            </AlertDialog>
          </>
        ) : null}
      </main>
    </div>
  );
}
