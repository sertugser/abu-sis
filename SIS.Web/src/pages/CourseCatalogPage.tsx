import { useState, useEffect, useRef, useCallback } from 'react';
import { Skeleton } from '@/components/ui/skeleton';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
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
import ValidationErrors from '../components/registration/ValidationErrors';
import { RegistrationHeaderBar } from '../components/registration/v2/RegistrationHeaderBar';
import { SemesterClassTabs } from '../components/registration/v2/SemesterClassTabs';
import type { ParityFilter } from '../components/registration/v2/SemesterClassTabs';
import { CurriculumCourseGrid } from '../components/registration/v2/CurriculumCourseGrid';
import { SectionPickerDialog } from '../components/registration/v2/SectionPickerDialog';
import RetakeCoursesPanel from '../components/registration/v2/RetakeCoursesPanel';
import RegistrationStatusLine from '../components/registration/v2/RegistrationStatusLine';
import { SelectedCoursesGrid } from '../components/registration/v2/SelectedCoursesGrid';
import { RegistrationSummaryBar } from '../components/registration/v2/RegistrationSummaryBar';
import { useStudentRegistration } from '../hooks/useStudentRegistration';
import { useAddToPlan } from '../hooks/useAddToPlan';
import { useDropCourse } from '../hooks/useDropCourse';
import { useValidateRegistration } from '../hooks/useValidateRegistration';
import { useSubmitRegistration } from '../hooks/useSubmitRegistration';
import { ApiError } from '../lib/apiError';
import type { ValidationError } from '../types/registration';
import type { CatalogCourseRow } from '../types/registrationCatalog';
import {
  AlertTriangle, Calendar, CheckCircle2, Clock, Loader2, Send,
} from 'lucide-react';

function deriveDefaultParity(termType: string): ParityFilter {
  const t = termType.toLowerCase();
  if (t.includes('fall') || t.includes('guz') || t.includes('güz')) return 'Guz';
  if (t.includes('spring') || t.includes('bahar')) return 'Bahar';
  return 'Tumu';
}

export default function CourseCatalogPage() {
  const { data: catalog, isLoading, error } = useStudentRegistration();

  const [selectedClassYear, setSelectedClassYear] = useState<number | null>(null);
  const [parity, setParity] = useState<ParityFilter>('Tumu');
  const parityInitialized = useRef(false);
  const [pickerCourse, setPickerCourse] = useState<CatalogCourseRow | null>(null);
  const [submitSuccess, setSubmitSuccess] = useState(false);
  const [validationErrors, setValidationErrors] = useState<ValidationError[]>([]);
  const [validationWarnings, setValidationWarnings] = useState<ValidationError[]>([]);
  const [emptySlotsConfirmOpen, setEmptySlotsConfirmOpen] = useState(false);

  const validate = useValidateRegistration();
  const submit = useSubmitRegistration();

  const addToPlanMutation = useAddToPlan();
  const [addError, setAddError] = useState<string | null>(null);
  const handleAddSection = useCallback(async (sectionId: number): Promise<void> => {
    setAddError(null);
    setSubmitSuccess(false);   // plan değişti → eski başarı mesajı geçersiz
    validate.reset();          // plan değişti → eski "Kontrol Et" sonucu geçersiz
    return new Promise<void>((resolve, reject) => {
      addToPlanMutation.mutate(sectionId, {
        onSuccess: () => resolve(),
        onError: (err) => {
          setAddError(err instanceof Error ? err.message : 'Eklenemedi.');
          reject(err);
        },
      });
    });
  }, [addToPlanMutation]);

  const dropMutation = useDropCourse();
  const [removingId, setRemovingId] = useState<number | null>(null);
  const [removeError, setRemoveError] = useState<string | null>(null);
  const handleRemove = useCallback(async (registrationId: number): Promise<void> => {
    setRemovingId(registrationId);
    setRemoveError(null);
    setSubmitSuccess(false);   // plan değişti → eski başarı mesajı geçersiz
    validate.reset();          // plan değişti → eski "Kontrol Et" sonucu geçersiz
    try {
      await new Promise<void>((resolve, reject) => {
        dropMutation.mutate(registrationId, {
          onSuccess: () => resolve(),
          onError: (err) => {
            setRemoveError(err instanceof Error ? err.message : 'Çıkarılamadı.');
            reject(err);
          },
        });
      });
    } finally {
      setRemovingId(null);
    }
  }, [dropMutation]);

  // On first catalog load, default selectedClassYear to student.classYear (clamped to available years).
  useEffect(() => {
    if (!catalog || selectedClassYear !== null) return;
    const years = catalog.classYears.map((y) => y.classYear);
    if (years.length === 0) return;
    const target = catalog.student.classYear;
    const clamped = years.includes(target) ? target : years[0];
    setSelectedClassYear(clamped);
  }, [catalog, selectedClassYear]);

  // Set default parity from active term type — run only once on first catalog load.
  useEffect(() => {
    if (!catalog || parityInitialized.current) return;
    setParity(deriveDefaultParity(catalog.term.termType));
    parityInitialized.current = true;
  }, [catalog]);

  // ── Loading ─────────────────────────────────────────────────────────────────

  if (isLoading) {
    return (
      <div className="space-y-2">
        <Skeleton className="h-20 w-full" />
        <Skeleton className="h-8 w-full" />
        <Skeleton className="h-8 w-full" />
        <Skeleton className="h-64 w-full" />
      </div>
    );
  }

  // ── Error ────────────────────────────────────────────────────────────────────

  if (error) {
    const errorCode = error instanceof ApiError ? error.code : undefined;
    const errMsg = error instanceof Error ? error.message : '';

    if (errorCode === 'ActiveTermNotFound') {
      return (
        <Alert>
          <Calendar className="h-4 w-4" />
          <AlertDescription>
            <p className="font-semibold">Aktif akademik dönem bulunmuyor</p>
            <p className="mt-1 text-sm text-muted-foreground">
              Yeni dönem açıldığında ders kataloğu burada görünecek.
            </p>
          </AlertDescription>
        </Alert>
      );
    }

    if (errorCode === 'ActiveProgramNotFound') {
      return (
        <Alert variant="destructive">
          <AlertTriangle className="h-4 w-4" />
          <AlertDescription>
            <p className="font-semibold">Aktif programınız bulunamadı</p>
            <p className="mt-1 text-sm">Lütfen öğrenci işleri birimi ile iletişime geçin.</p>
          </AlertDescription>
        </Alert>
      );
    }

    if (errorCode === 'StudentNotFound') {
      return (
        <Alert variant="destructive">
          <AlertTriangle className="h-4 w-4" />
          <AlertDescription>
            <p className="font-semibold">Öğrenci kaydına erişilemiyor</p>
            <p className="mt-1 text-sm">
              Hesabınızla ilişkili öğrenci kaydı bulunamadı. Lütfen öğrenci işleri birimi ile
              iletişime geçin.
            </p>
          </AlertDescription>
        </Alert>
      );
    }

    return (
      <Alert variant="destructive">
        <AlertTriangle className="h-4 w-4" />
        <AlertDescription>
          <p className="font-semibold">Dersler yüklenirken hata oluştu</p>
          {errMsg ? (
            <p className="mt-1 text-sm">{errMsg}</p>
          ) : (
            <p className="mt-1 text-sm">Lütfen sayfayı yenileyip tekrar deneyin.</p>
          )}
        </AlertDescription>
      </Alert>
    );
  }

  if (!catalog) return null;

  const { student, term, classYears, retakeCourses, electivePoolSlots, selectedCourses } = catalog;

  // ── Derived values ───────────────────────────────────────────────────────────

  const classYearList = classYears.map((y) => y.classYear);
  const effectiveClassYear = selectedClassYear ?? classYearList[0] ?? 1;
  const selectedYearData = classYears.find((y) => y.classYear === effectiveClassYear) ?? null;

  const registrationActionsOpen = Boolean(term.isRegistrationOpen || term.isAddDropOpen);

  const planItems = selectedCourses.filter(
    (s) => s.status === 'Draft' || s.status === 'Rejected',
  );
  const totalEcts = selectedCourses.reduce((sum, s) => sum + s.ects, 0);
  const totalHours = selectedCourses.reduce((sum, s) => sum + s.theoryHours + s.labHours, 0);
  const totalCredit = selectedCourses.reduce((sum, s) => sum + s.nationalCredit, 0);
  const courseCount = selectedCourses.length;

  const registeredSectionIds = selectedCourses.map((s) => s.courseSectionId);
  const emptySlots = electivePoolSlots.filter((p) => !p.isFulfilled);

  const validationSummary = validate.data
    ? { errorCount: validate.data.errors.length, warningCount: validate.data.warnings.length }
    : null;

  // ── Top banner ───────────────────────────────────────────────────────────────

  const topBanner = (() => {
    if (!term.hasAcademicCalendar) {
      return (
        <Alert variant="warning">
          <AlertTriangle className="h-4 w-4" />
          <AlertDescription>
            Aktif dönemin akademik takvimi henüz tanımlanmamış.
          </AlertDescription>
        </Alert>
      );
    }
    if (!registrationActionsOpen) {
      return (
        <Alert>
          <Clock className="h-4 w-4" />
          <AlertDescription>
            {term.registrationMessage ?? 'Kayıt dönemi şu anda kapalıdır.'}
          </AlertDescription>
        </Alert>
      );
    }
    return null;
  })();

  // ── Submit handlers ──────────────────────────────────────────────────────────

  function performSubmit() {
    setValidationErrors([]);
    setValidationWarnings([]);
    setSubmitSuccess(false);
    const sectionIds = planItems.map((r) => r.courseSectionId);
    submit.mutate(sectionIds, {
      onSuccess: (data) => {
        if (data.success) {
          setSubmitSuccess(true);
          setValidationWarnings(data.validationResult?.warnings ?? []);
        }
      },
      onError: (err) => {
        if (err instanceof ApiError && err.validationErrors.length > 0) {
          setValidationErrors(err.validationErrors);
          setValidationWarnings(err.validationWarnings);
        } else {
          setValidationErrors([
            {
              rule: 'System',
              message:
                err instanceof Error ? err.message : 'Kayıt sırasında bir hata oluştu.',
            },
          ]);
        }
      },
    });
  }

  function handleSubmit() {
    if (!registrationActionsOpen) return;
    if (emptySlots.length > 0) {
      setEmptySlotsConfirmOpen(true);
      return;
    }
    performSubmit();
  }

  function confirmSubmitDespiteEmptySlots() {
    setEmptySlotsConfirmOpen(false);
    performSubmit();
  }

  // ── Render ───────────────────────────────────────────────────────────────────

  return (
    <>
      {/* Top banner lives OUTSIDE the white card */}
      {topBanner && <div className="mb-2">{topBanner}</div>}

      <div className="space-y-4">
        {/* ── Kart 1: Öğrenci başlığı ── */}
        <div className="overflow-hidden rounded-xl border bg-card shadow-sm">
          <RegistrationHeaderBar student={student} />
        </div>

        {/* Inline alerts */}
        {submitSuccess && (
          <Alert variant="success">
            <CheckCircle2 className="h-4 w-4" />
            <AlertDescription>Kayıt başarıyla kesinleştirildi.</AlertDescription>
          </Alert>
        )}
        {(validationErrors.length > 0 || validationWarnings.length > 0) && (
          <ValidationErrors errors={validationErrors} warnings={validationWarnings} />
        )}

        {/* ── Kart 2: Müfredat dersleri ── */}
        <div className="overflow-hidden rounded-xl border bg-card shadow-sm">
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
            registrationActionsOpen={registrationActionsOpen}
          />

          {retakeCourses.length > 0 && (
            <RetakeCoursesPanel
              retakeCourses={retakeCourses}
              onCheck={() =>
                validate.mutate(selectedCourses.map((s) => s.courseSectionId))
              }
              checkLoading={validate.isPending}
              validationSummary={validationSummary}
            />
          )}
        </div>

        {/* ── Kart 3: Seçilen dersler + kesinleştirme ── */}
        <div className="overflow-hidden rounded-xl border bg-card shadow-sm">
          <RegistrationStatusLine selectedCourses={selectedCourses} />

          <SelectedCoursesGrid
            selectedCourses={selectedCourses}
            registrationActionsOpen={registrationActionsOpen}
            onRemove={handleRemove}
            removingId={removingId}
            removeError={removeError}
          />

          <RegistrationSummaryBar
            totalCredit={totalCredit}
            totalEcts={totalEcts}
            ectsLimit={student.ectsLimit}
            totalHours={totalHours}
            courseCount={courseCount}
          />

          {planItems.length > 0 && (
            <div className="border-t bg-muted/30 px-5 py-3 flex justify-end">
              <Button
                type="button"
                size="lg"
                onClick={handleSubmit}
                disabled={submit.isPending || !registrationActionsOpen}
              >
                {submit.isPending ? (
                  <Loader2 className="h-5 w-5 animate-spin" />
                ) : (
                  <Send className="h-5 w-5" />
                )}
                Kaydı Kesinleştir
              </Button>
            </div>
          )}
        </div>
      </div>

      {/* Section picker dialog */}
      <SectionPickerDialog
        course={pickerCourse}
        open={pickerCourse !== null}
        onOpenChange={(o) => {
          if (!o) setPickerCourse(null);
        }}
        registeredSectionIds={registeredSectionIds}
        registrationActionsOpen={registrationActionsOpen}
        onAddSection={handleAddSection}
        isAdding={addToPlanMutation.isPending}
        addError={addError}
        onAdded={() => { /* catalog refetch is automatic via cache invalidation */ }}
      />

      {/* Empty-slot warning AlertDialog */}
      <AlertDialog open={emptySlotsConfirmOpen} onOpenChange={setEmptySlotsConfirmOpen}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Eksik müfredat slotları var</AlertDialogTitle>
            <AlertDialogDescription>
              Aşağıdaki seçmeli grup slotları için yeterli ders seçmediniz. Yine de kaydı
              gönderebilirsiniz; eksiklikler danışmanınızla görüşülerek değerlendirilecektir.
            </AlertDialogDescription>
          </AlertDialogHeader>

          <div className="max-h-40 overflow-y-auto rounded-md bg-muted/40 p-2 text-xs space-y-1">
            {emptySlots.map((s) => (
              <div key={s.poolId}>
                <span className="font-mono font-semibold">{s.groupCode}</span>
                {s.slotName && (
                  <span className="text-violet-700"> / {s.slotName}</span>
                )}
                <span className="text-muted-foreground">
                  {' — '}
                  {s.requiredCourseCount > 0 &&
                    `${s.completedCourseCount}/${s.requiredCourseCount} ders`}
                  {s.requiredCourseCount > 0 && s.requiredEcts > 0 && ', '}
                  {s.requiredEcts > 0 && `${s.earnedEcts}/${s.requiredEcts} AKTS`}
                </span>
              </div>
            ))}
          </div>

          <AlertDialogFooter>
            <AlertDialogCancel>İptal</AlertDialogCancel>
            <AlertDialogAction onClick={confirmSubmitDespiteEmptySlots} disabled={submit.isPending}>
              Yine de gönder
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </>
  );
}
