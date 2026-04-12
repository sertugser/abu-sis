import { useState } from 'react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { cn, formatDate } from '@/lib/utils';
import {
  AlertCircle,
  PencilLine,
  Plus,
  RefreshCcw,
  Save,
  Trash2,
} from 'lucide-react';
import { useStudentInfo } from '../hooks/useStudentInfo';
import { useStudentTranscript } from '../hooks/useStudentTranscript';
import { STUDENT_STATUS_LABELS } from '../lib/constants';
import {
  GRADE_OPTIONS,
  areScenarioCoursesEqual,
  buildInitialScenarioCourses,
  buildScenarioTerms,
  calculateScenarioReport,
  createScenarioId,
  formatGradeOptionLabel,
  getGradeMeta,
  normalizeCourseCode,
  normalizeGrade,
  round2,
  type ScenarioCourse,
  type ScenarioGrade,
} from '../lib/transcriptScenario';

interface ScenarioCourseDraft {
  scenarioId: string | null;
  sourceRegistrationId: number | null;
  courseId: number | null;
  termId: string;
  courseCode: string;
  courseName: string;
  credits: string;
  ects: string;
  equivalentCourseIds: number[];
  averageScore: string;
  grade: ScenarioGrade;
  excludeFromAverage: boolean;
  continuationCourseId: string;
  isAdded: boolean;
  wasRepeated: boolean;
  sortOrder: number;
}

const SELF_CONTINUATION = '__self__';

export default function TranscriptScenarioPage() {
  const transcriptQuery = useStudentTranscript();
  const studentQuery = useStudentInfo();
  const [scenarioCourses, setScenarioCourses] = useState<ScenarioCourse[] | null>(null);
  const [draft, setDraft] = useState<ScenarioCourseDraft | null>(null);
  const [dialogError, setDialogError] = useState<string | null>(null);

  const transcript = transcriptQuery.data;
  const student = studentQuery.data;
  const terms = transcript ? buildScenarioTerms(transcript) : [];
  const initialCourses = transcript ? buildInitialScenarioCourses(transcript) : [];

  if (transcript && scenarioCourses === null) {
    setScenarioCourses(buildInitialScenarioCourses(transcript));
  }

  if (transcriptQuery.isLoading || studentQuery.isLoading || (transcript && scenarioCourses === null)) {
    return <TranscriptScenarioLoadingState />;
  }

  if (transcriptQuery.isError || studentQuery.isError || !transcript || !student || !scenarioCourses) {
    return (
      <Alert variant="destructive">
        <AlertCircle className="h-4 w-4" />
        <AlertTitle>Transkript senaryosu yüklenemedi</AlertTitle>
        <AlertDescription>
          Öğrencinin senaryo bilgileri hazırlanırken bir hata oluştu. Lütfen tekrar deneyin.
        </AlertDescription>
      </Alert>
    );
  }

  const scenarioReport = calculateScenarioReport(terms, scenarioCourses);
  const hasScenarioChanges = !areScenarioCoursesEqual(scenarioCourses, initialCourses);
  const scenarioDelta = round2(scenarioReport.currentCgpa - transcript.currentCgpa);
  const advisorName = student.advisorName
    ? `${student.advisorTitle ? `${student.advisorTitle} ` : ''}${student.advisorName}`.trim()
    : 'Atanmamış';
  const continuationOptions = [...scenarioCourses].sort((left, right) => left.sortOrder - right.sortOrder);

  const handleReset = () => {
    setScenarioCourses(buildInitialScenarioCourses(transcript));
    setDraft(null);
    setDialogError(null);
  };

  const handleAddCourse = () => {
    const lastTerm = terms[terms.length - 1];
    setDialogError(null);
    setDraft({
      scenarioId: null,
      sourceRegistrationId: null,
      courseId: null,
      termId: lastTerm ? String(lastTerm.termId) : '',
      courseCode: '',
      courseName: '',
      credits: '3',
      ects: '5',
      equivalentCourseIds: [],
      averageScore: '',
      grade: 'BB',
      excludeFromAverage: false,
      continuationCourseId: SELF_CONTINUATION,
      isAdded: true,
      wasRepeated: false,
      sortOrder: getNextSortOrder(scenarioCourses),
    });
  };

  const handleEditCourse = (course: ScenarioCourse) => {
    setDialogError(null);
    setDraft({
      scenarioId: course.scenarioId,
      sourceRegistrationId: course.sourceRegistrationId,
      courseId: course.courseId,
      termId: String(course.termId),
      courseCode: course.courseCode,
      courseName: course.courseName,
      credits: String(course.credits),
      ects: String(course.ects),
      equivalentCourseIds: course.equivalentCourseIds,
      averageScore: course.averageScore == null ? '' : String(course.averageScore),
      grade: course.grade,
      excludeFromAverage: course.excludeFromAverage,
      continuationCourseId: course.continuationCourseId === course.scenarioId ? course.scenarioId : course.continuationCourseId,
      isAdded: course.isAdded,
      wasRepeated: course.wasRepeated,
      sortOrder: course.sortOrder,
    });
  };

  const handleSaveDraft = () => {
    if (!draft) {
      return;
    }

    const termId = Number(draft.termId);
    const selectedTerm = terms.find(term => term.termId === termId);
    const credits = Number(draft.credits);
    const ects = Number(draft.ects);
    const averageScore = draft.averageScore.trim() === '' ? null : Number(draft.averageScore);
    const scenarioId = draft.scenarioId ?? createScenarioId();
    const selectedContinuationCourse = continuationOptions.find(course => course.scenarioId === draft.continuationCourseId);
    const continuationCourseId = draft.continuationCourseId === SELF_CONTINUATION || !selectedContinuationCourse
      ? scenarioId
      : selectedContinuationCourse.continuationCourseId;

    if (!selectedTerm) {
      setDialogError('Geçerli bir dönem seçilmelidir.');
      return;
    }

    if (!draft.courseCode.trim()) {
      setDialogError('Ders kodu boş bırakılamaz.');
      return;
    }

    if (!draft.courseName.trim()) {
      setDialogError('Ders adı boş bırakılamaz.');
      return;
    }

    if (!Number.isFinite(credits) || credits < 0) {
      setDialogError('Kredi alanı 0 veya daha büyük bir sayı olmalıdır.');
      return;
    }

    if (!Number.isFinite(ects) || ects < 0) {
      setDialogError('AKTS alanı 0 veya daha büyük bir sayı olmalıdır.');
      return;
    }

    if (averageScore != null && (!Number.isFinite(averageScore) || averageScore < 0 || averageScore > 100)) {
      setDialogError('Ortalama alanı 0 ile 100 arasında olmalıdır.');
      return;
    }

    const nextCourse: ScenarioCourse = {
      scenarioId,
      sourceRegistrationId: draft.sourceRegistrationId,
      courseId: draft.courseId,
      termId: selectedTerm.termId,
      courseCode: normalizeCourseCode(draft.courseCode),
      courseName: draft.courseName.trim(),
      credits: Math.round(credits),
      ects: Math.round(ects),
      equivalentCourseIds: draft.equivalentCourseIds,
      averageScore: averageScore == null ? null : round2(averageScore),
      grade: normalizeGrade(draft.grade),
      excludeFromAverage: draft.excludeFromAverage,
      continuationCourseId,
      isAdded: draft.isAdded,
      wasRepeated: continuationCourseId !== scenarioId,
      sortOrder: draft.sortOrder,
    };

    setScenarioCourses(prevCourses => {
      if (!prevCourses) {
        return [nextCourse];
      }

      if (!draft.scenarioId) {
        return [...prevCourses, nextCourse];
      }

      return prevCourses.map(course => course.scenarioId === draft.scenarioId ? nextCourse : course);
    });

    setDraft(null);
    setDialogError(null);
  };

  const handleDeleteAddedCourse = () => {
    if (!draft?.scenarioId || !draft.isAdded) {
      return;
    }

    setScenarioCourses(prevCourses => {
      if (!prevCourses) {
        return prevCourses;
      }

      return removeScenarioCourse(prevCourses, draft.scenarioId!);
    });

    setDraft(null);
    setDialogError(null);
  };

  const selectedDraftGrade = draft ? getGradeMeta(draft.grade) : null;

  return (
    <div className="space-y-6">
      <div className="rounded-2xl bg-[#d14b51] px-5 py-4 text-white shadow-[0_18px_40px_rgba(209,75,81,0.24)]">
        <div className="flex flex-col gap-2 lg:flex-row lg:items-start lg:justify-between">
          <div className="space-y-2">
            <div className="font-heading text-xl font-extrabold">Bilgi Notu</div>
            <p className="max-w-5xl text-sm leading-6 text-white/95">
              Bu ekrandaki işlemler sistem üzerinde herhangi bir değişiklik yapılmadan, sadece bilgilendirme amaçlı
              olarak yapılmaktadır. Veriler üzerinde herhangi bir etkisi yoktur. Gerçek not ortalamanızı transkript
              üzerinden kontrol ediniz.
            </p>
            <div className="text-sm font-medium text-white/90">
              Ortalama hesapları AKTS ağırlığına göre ve aynı dersin son denemesi esas alınarak yapılmaktadır.
            </div>
          </div>

          <div className="rounded-xl bg-white/10 px-4 py-3 text-sm">
            <div className="font-semibold">Gerçek AGNO: {formatNumber(transcript.currentCgpa)}</div>
            <div className="mt-1 font-semibold">Senaryo AGNO: {formatNumber(scenarioReport.currentCgpa)}</div>
            <div className={cn('mt-1 font-semibold', scenarioDelta >= 0 ? 'text-emerald-100' : 'text-rose-100')}>
              Fark: {scenarioDelta >= 0 ? '+' : ''}{formatNumber(scenarioDelta)}
            </div>
          </div>
        </div>
      </div>

      <Card className="border border-[#003366]/15 shadow-[0_18px_36px_rgba(15,23,42,0.08)]">
        <CardContent className="grid gap-6 p-6 lg:grid-cols-[minmax(0,1fr)_280px]">
          <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
            <ScenarioInfoRow label="Adı Soyadı" value={transcript.fullName} />
            <ScenarioInfoRow label="Fakülte" value={transcript.facultyName} />
            <ScenarioInfoRow label="Öğrenci No" value={transcript.studentNumber} />
            <ScenarioInfoRow label="Program" value={transcript.programName} />
            <ScenarioInfoRow label="Kayıt Tarihi" value={formatDate(transcript.enrollmentDate)} />
            <ScenarioInfoRow label="Danışmanı" value={advisorName} />
            <ScenarioInfoRow label="Hesaplanan Genel Ort." value={formatNumber(scenarioReport.currentCgpa)} emphasize />
            <ScenarioInfoRow label="Durum" value={STUDENT_STATUS_LABELS[transcript.studentStatus] || transcript.studentStatus} />
            <ScenarioInfoRow label="Toplam Geçilen AKTS" value={String(scenarioReport.totalEarnedEcts)} />
          </div>

          <div className="flex flex-col gap-3">
            <Button type="button" variant="outline" onClick={handleReset} disabled={!hasScenarioChanges}>
              <RefreshCcw className="h-4 w-4" />
              Başlangıç Durumuna Getir
            </Button>
            <Button type="button" onClick={handleAddCourse} disabled={terms.length === 0}>
              <Plus className="h-4 w-4" />
              Yeni Ders Ekle
            </Button>
            <div className="rounded-xl border border-emerald-500/20 bg-emerald-500/5 px-4 py-3 text-sm text-emerald-700">
              {hasScenarioChanges
                ? 'Senaryo güncel. Tüm hesaplamalar ekrandaki son değişikliklere göre yenilendi.'
                : 'Transkript senaryosu başlangıç durumunda hazır. Notları dilediğiniz gibi simüle edebilirsiniz.'}
            </div>
          </div>
        </CardContent>
      </Card>

      {scenarioReport.terms.length === 0 ? (
        <Card>
          <CardContent className="py-16 text-center text-muted-foreground">
            Senaryo için gösterilecek ders kaydı bulunmuyor.
          </CardContent>
        </Card>
      ) : (
        <div className="grid gap-5 xl:grid-cols-2">
          {scenarioReport.terms.map(term => (
            <Card key={term.termId} className="overflow-hidden rounded-[10px] border border-slate-300 bg-white shadow-[0_10px_24px_rgba(15,23,42,0.10)]">
              <div className="bg-[#6f7073] px-3 py-2 text-[15px] font-bold text-white">
                {term.termName}
              </div>

              <CardContent className="p-0">
                <table className="w-full table-fixed text-[13px]">
                  <colgroup>
                    <col className="w-[17%]" />
                    <col className="w-[44%]" />
                    <col className="w-[7%]" />
                    <col className="w-[7%]" />
                    <col className="w-[7%]" />
                    <col className="w-[7%]" />
                    <col className="w-[5%]" />
                    <col className="w-[6%]" />
                  </colgroup>
                  <thead>
                    <tr className="border-b border-slate-200 bg-slate-50/70 text-slate-600">
                      <th className="px-2 py-2 text-left font-semibold">Ders Kodu</th>
                      <th className="px-2 py-2 text-left font-semibold">Ders Adı</th>
                      <th className="px-1 py-2 text-center font-semibold">Kredi</th>
                      <th className="px-1 py-2 text-center font-semibold">AKTS</th>
                      <th className="px-1 py-2 text-center font-semibold">Ort</th>
                      <th className="px-1 py-2 text-center font-semibold">Harf</th>
                      <th className="px-1 py-2 text-center font-semibold">GPA</th>
                      <th className="px-1 py-2 text-center font-semibold"></th>
                    </tr>
                  </thead>
                  <tbody>
                    {term.courses.map((course, index) => (
                      <tr key={course.scenarioId} className={cn('border-b border-slate-200/90', index % 2 === 1 && 'bg-slate-50/30')}>
                        <td className="px-2 py-2 align-top font-mono text-[12.5px] font-semibold text-[#234983]">
                          <div className="flex flex-wrap items-center gap-1">
                            <span>{course.courseCode}</span>
                            {course.isAdded && (
                              <span className="rounded-full bg-primary/10 px-1.5 py-0.5 text-[9px] font-semibold uppercase tracking-[0.04em] text-primary">
                                Yeni
                              </span>
                            )}
                            {course.isRepeated && (
                              <span className="rounded-full bg-amber-500/10 px-1.5 py-0.5 text-[9px] font-semibold text-amber-700">
                                Tekrar
                              </span>
                            )}
                          </div>
                        </td>
                        <td className={cn('px-2 py-2 align-top text-[12.5px] leading-5 text-slate-700 break-words', !course.isPassed && course.countsTowardsGpa && 'text-destructive')}>
                          {course.courseName}
                        </td>
                        <td className="px-1 py-2 text-center align-top font-medium text-slate-700">{course.credits}</td>
                        <td className="px-1 py-2 text-center align-top font-medium text-slate-700">{course.ects}</td>
                        <td className="px-1 py-2 text-center align-top font-medium text-slate-700">
                          {course.averageScore == null ? '—' : formatCompactNumber(course.averageScore)}
                        </td>
                        <td className={cn(
                          'px-1 py-2 text-center align-top font-mono font-bold',
                          course.isPassed ? 'text-emerald-700' : course.countsTowardsGpa ? 'text-destructive' : 'text-slate-600',
                        )}>
                          {course.grade}
                        </td>
                        <td className="px-1 py-2 text-center align-top text-[12px] font-semibold text-slate-600">
                          {course.countsTowardsGpa ? 'D' : '-'}
                        </td>
                        <td className="px-1 py-2 text-center align-top">
                          <button
                            type="button"
                            onClick={() => handleEditCourse(course)}
                            className="inline-flex h-7 w-7 items-center justify-center rounded-md border border-sky-300 bg-sky-50 text-sky-600 transition-colors hover:bg-sky-100"
                            title="Dersi düzenle"
                          >
                            <PencilLine className="h-3.5 w-3.5" />
                          </button>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>

                <div className="flex flex-wrap gap-x-4 gap-y-1 border-t border-slate-200 bg-slate-50/40 px-3 py-3 text-[13px] font-semibold text-[#5b84bf]">
                  <span>AGNO: {formatNumber(term.cgpa)}</span>
                  <span>Toplam AKTS: {term.cumulativeGpaEcts}</span>
                  <span>ANO: {formatNumber(term.sgpa)}</span>
                  <span>Dönem AKTS: {term.attemptedEcts}</span>
                </div>
              </CardContent>
            </Card>
          ))}
        </div>
      )}

      <ScenarioEditorDialog
        draft={draft}
        terms={terms}
        courses={continuationOptions}
        selectedDraftGrade={selectedDraftGrade?.point}
        error={dialogError}
        onClose={() => {
          setDraft(null);
          setDialogError(null);
        }}
        onDelete={handleDeleteAddedCourse}
        onSave={handleSaveDraft}
        onChange={setDraft}
      />
    </div>
  );
}

function ScenarioEditorDialog({
  draft,
  terms,
  courses,
  selectedDraftGrade,
  error,
  onClose,
  onDelete,
  onSave,
  onChange,
}: {
  draft: ScenarioCourseDraft | null;
  terms: ReturnType<typeof buildScenarioTerms>;
  courses: ScenarioCourse[];
  selectedDraftGrade: number | null | undefined;
  error: string | null;
  onClose: () => void;
  onDelete: () => void;
  onSave: () => void;
  onChange: React.Dispatch<React.SetStateAction<ScenarioCourseDraft | null>>;
}) {
  return (
    <Dialog open={!!draft} onOpenChange={open => !open && onClose()}>
      <DialogContent className="max-w-3xl gap-0 overflow-hidden p-0">
        {draft && (
          <>
            <DialogHeader className="border-b border-border/70 px-6 py-5">
              <DialogTitle className="text-[2rem] font-normal tracking-tight">
                {draft.scenarioId ? 'Değiştiriliyor...' : 'Yeni Ders Ekleniyor...'}
              </DialogTitle>
              <DialogDescription>
                Senaryo üzerindeki bu değişiklik sadece hesaplamalara yansır, sistemde kalıcı not değişikliği yapmaz.
              </DialogDescription>
            </DialogHeader>

            <div className="space-y-5 px-6 py-5">
              {error && (
                <Alert variant="destructive">
                  <AlertCircle className="h-4 w-4" />
                  <AlertDescription>{error}</AlertDescription>
                </Alert>
              )}

              <div className="grid gap-x-6 gap-y-4 md:grid-cols-[170px_minmax(0,1fr)] md:items-center">
                <FormLabel label="Dönem" />
                <Select
                  value={draft.termId}
                  onValueChange={value => onChange(current => current ? { ...current, termId: value } : current)}
                >
                  <SelectTrigger>
                    <SelectValue placeholder="Dönem seçin" />
                  </SelectTrigger>
                  <SelectContent>
                    {terms.map(term => (
                      <SelectItem key={term.termId} value={String(term.termId)}>
                        {term.termName}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>

                <FormLabel label="Ders Kodu" />
                <Input
                  value={draft.courseCode}
                  onChange={event => onChange(current => current ? { ...current, courseCode: event.target.value.toUpperCase() } : current)}
                />

                <FormLabel label="Ders Adı" />
                <Input
                  value={draft.courseName}
                  onChange={event => onChange(current => current ? { ...current, courseName: event.target.value } : current)}
                />

                <FormLabel label="Kredi" />
                <Input
                  type="number"
                  min={0}
                  step={1}
                  value={draft.credits}
                  onChange={event => onChange(current => current ? { ...current, credits: event.target.value } : current)}
                />

                <FormLabel label="AKTS" />
                <Input
                  type="number"
                  min={0}
                  step={1}
                  value={draft.ects}
                  onChange={event => onChange(current => current ? { ...current, ects: event.target.value } : current)}
                />

                <FormLabel label="Ort" />
                <Input
                  type="number"
                  min={0}
                  max={100}
                  step={0.01}
                  value={draft.averageScore}
                  onChange={event => onChange(current => current ? { ...current, averageScore: event.target.value } : current)}
                />

                <FormLabel label="Harf" />
                <div className="grid gap-3 md:grid-cols-[minmax(0,1fr)_90px]">
                  <Select
                    value={draft.grade}
                    onValueChange={value => onChange(current => current ? { ...current, grade: normalizeGrade(value) } : current)}
                  >
                    <SelectTrigger>
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      {GRADE_OPTIONS.map(option => (
                        <SelectItem key={option.value} value={option.value}>
                          {formatGradeOptionLabel(option.value)}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>

                  <Input
                    value={selectedDraftGrade == null ? 'GPA Dışı' : formatNumber(selectedDraftGrade)}
                    readOnly
                    className="bg-amber-50 font-semibold text-amber-700"
                  />
                </div>

                <FormLabel label="" />
                <label className="flex items-center gap-3 text-sm font-medium text-foreground">
                  <input
                    type="checkbox"
                    checked={draft.excludeFromAverage}
                    onChange={event => onChange(current => current ? { ...current, excludeFromAverage: event.target.checked } : current)}
                    className="h-5 w-5 rounded border border-input"
                  />
                  Ortalamaya Katılmaz
                </label>

                <FormLabel label="Ders Devamı" />
                <Select
                  value={draft.continuationCourseId}
                  onValueChange={value => onChange(current => current ? { ...current, continuationCourseId: value } : current)}
                >
                  <SelectTrigger>
                    <SelectValue placeholder="Ders devamı seçin" />
                  </SelectTrigger>
                  <SelectContent>
                    {!draft.scenarioId && (
                      <SelectItem value={SELF_CONTINUATION}>Bağımsız Ders</SelectItem>
                    )}
                    {courses.map(course => (
                      <SelectItem key={course.scenarioId} value={course.scenarioId}>
                        ({findTermName(terms, course.termId)}) {course.courseCode}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            </div>

            <DialogFooter className="border-t border-border/70 px-6 py-4 sm:justify-between sm:space-x-0">
              <div>
                {draft.isAdded && (
                  <Button type="button" variant="destructive" onClick={onDelete}>
                    <Trash2 className="h-4 w-4" />
                    Bu Dersi Sil
                  </Button>
                )}
              </div>

              <div className="flex flex-col-reverse gap-3 sm:flex-row">
                <Button type="button" variant="outline" onClick={onClose}>
                  Kapat
                </Button>
                <Button type="button" onClick={onSave}>
                  <Save className="h-4 w-4" />
                  Kaydet
                </Button>
              </div>
            </DialogFooter>
          </>
        )}
      </DialogContent>
    </Dialog>
  );
}

function TranscriptScenarioLoadingState() {
  return (
    <div className="space-y-6">
      <Skeleton className="h-32 rounded-2xl" />
      <Skeleton className="h-40 rounded-2xl" />
      <div className="grid gap-6 xl:grid-cols-2">
        <Skeleton className="h-[420px] rounded-2xl" />
        <Skeleton className="h-[420px] rounded-2xl" />
      </div>
    </div>
  );
}

function ScenarioInfoRow({ label, value, emphasize = false }: { label: string; value: string; emphasize?: boolean }) {
  return (
    <div className="grid grid-cols-[132px_minmax(0,1fr)] gap-3 text-sm">
      <div className="font-medium text-muted-foreground">{label}</div>
      <div className={cn('font-semibold text-foreground', emphasize && 'text-primary')}>{value}</div>
    </div>
  );
}

function FormLabel({ label }: { label: string }) {
  if (!label) {
    return <div aria-hidden="true" />;
  }

  return <div className="text-[15px] font-medium text-foreground">{label}</div>;
}

function findTermName(terms: ReturnType<typeof buildScenarioTerms>, termId: number) {
  return terms.find(term => term.termId === termId)?.termName ?? 'Bilinmeyen Dönem';
}

function formatNumber(value: number) {
  return value.toLocaleString('tr-TR', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  });
}

function formatCompactNumber(value: number) {
  return value.toLocaleString('tr-TR', {
    minimumFractionDigits: 0,
    maximumFractionDigits: 2,
  });
}

function getNextSortOrder(courses: ScenarioCourse[]) {
  return courses.length === 0 ? 0 : Math.max(...courses.map(course => course.sortOrder)) + 1;
}

function removeScenarioCourse(courses: ScenarioCourse[], scenarioId: string) {
  const removedCourse = courses.find(course => course.scenarioId === scenarioId);
  const remainingCourses = courses.filter(course => course.scenarioId !== scenarioId);
  const continuationTarget = removedCourse && removedCourse.continuationCourseId !== removedCourse.scenarioId
    ? removedCourse.continuationCourseId
    : null;

  return remainingCourses.map(course => {
    if (course.continuationCourseId !== scenarioId) {
      return course;
    }

    const nextContinuationCourseId = continuationTarget && remainingCourses.some(item => item.scenarioId === continuationTarget)
      ? continuationTarget
      : course.scenarioId;

    return {
      ...course,
      continuationCourseId: nextContinuationCourseId,
      wasRepeated: nextContinuationCourseId !== course.scenarioId,
    };
  });
}
