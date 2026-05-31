import { useState } from 'react';
import { keepPreviousData, useQuery } from '@tanstack/react-query';
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
  Eye,
  EyeOff,
  PencilLine,
  Plus,
  RefreshCcw,
  Save,
  Trash2,
} from 'lucide-react';
import { STUDENT_STATUS_LABELS } from '../../lib/constants';
import {
  GRADE_OPTIONS,
  areScenarioCoursesEqual,
  buildInitialScenarioCourses,
  buildScenarioTerms,
  createScenarioId,
  formatGradeOptionLabel,
  getGradeMeta,
  normalizeCourseCode,
  normalizeGrade,
  round2,
  type ScenarioCourse,
  type ScenarioGrade,
} from '../../lib/transcriptScenario';
import type {
  ScenarioComputedCourse,
  ScenarioCourseInput,
  ScenarioReport,
  ScenarioTermReport,
  TranscriptScenarioRequest,
} from '../../types/transcriptScenario';
import type { StudentTranscriptDto } from '../../types/transcript';
import type { CurriculumCourseStatusDto, CurriculumStatusDto } from '../../types/curriculum';

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
  // '' = not henüz seçilmedi → ortalamaya katılmaz.
  grade: ScenarioGrade | '';
  originalGrade: ScenarioGrade | null;
  excludeFromAverage: boolean;
  continuationCourseId: string;
  isAdded: boolean;
  isPlannedFromCurriculum: boolean;
  wasRepeated: boolean;
  sortOrder: number;
}

const SELF_CONTINUATION = '__self__';
const FUTURE_TERM_ID_OFFSET = 900_000;

interface PlannedScenarioCourse {
  scenarioId: string;
  courseId: number;
  termId: number;
  courseCode: string;
  courseName: string;
  credits: number;
  ects: number;
  isCompulsory: boolean;
  prerequisites: string[];
  prerequisitesMet: boolean;
}

interface TranscriptScenarioViewProps {
  /** Başlık/görüntüleme adı (transkript verisi gelmeden önce de gösterilebilir). */
  studentName: string;
  transcript: StudentTranscriptDto | undefined;
  curriculumStatus: CurriculumStatusDto | undefined;
  isLoading: boolean;
  isError: boolean;
  calculate: (request: TranscriptScenarioRequest) => Promise<ScenarioReport>;
  /**
   * Öğrenci sayfasına özgü chrome (danışman görünümünde transkript DTO'sunda yok).
   * Verilmezse "Danışmanı" satırı gizlenir.
   */
  advisorName?: string;
  /** Müfredat verisi hâlâ yükleniyor mu (gelecekteki dersler toggle'ı için). */
  isCurriculumLoading?: boolean;
}

export function TranscriptScenarioView({
  studentName,
  transcript,
  curriculumStatus,
  isLoading,
  isError,
  calculate,
  advisorName,
  isCurriculumLoading = false,
}: TranscriptScenarioViewProps) {
  const [scenarioCourses, setScenarioCourses] = useState<ScenarioCourse[] | null>(null);
  const [draft, setDraft] = useState<ScenarioCourseDraft | null>(null);
  const [dialogError, setDialogError] = useState<string | null>(null);
  const [showFutureCourses, setShowFutureCourses] = useState(false);

  const transcriptTerms = transcript ? buildScenarioTerms(transcript) : [];
  const futureTerms = transcript && curriculumStatus ? buildFutureScenarioTerms(transcriptTerms, curriculumStatus) : [];
  const terms = [...transcriptTerms, ...futureTerms];
  const initialCourses = transcript ? buildInitialScenarioCourses(transcript) : [];

  if (transcript && scenarioCourses === null) {
    setScenarioCourses(buildInitialScenarioCourses(transcript));
  }

  // Senaryo hesabı backend'de TEK kaynaktan yapılır (GradePointMap + GpaMath). Her
  // düzenlemede istek yenilenir; keepPreviousData ile ekran titremez.
  const scenarioRequest: TranscriptScenarioRequest | null = scenarioCourses
    ? {
        terms: terms.map(term => ({
          termId: term.termId,
          termName: term.termName,
          year: term.year,
          semester: term.semester,
          order: term.order,
        })),
        courses: scenarioCourses.map(toScenarioCourseInput),
      }
    : null;

  const scenarioReportQuery = useQuery({
    queryKey: ['transcriptScenario', scenarioRequest],
    queryFn: () => calculate(scenarioRequest!),
    enabled: scenarioRequest !== null && terms.length > 0,
    placeholderData: keepPreviousData,
  });

  if (isLoading || (transcript && scenarioCourses === null)) {
    return <TranscriptScenarioLoadingState />;
  }

  if (isError || !transcript || !scenarioCourses) {
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

  const scenarioReport = scenarioReportQuery.data;
  if (scenarioReportQuery.isError) {
    return (
      <Alert variant="destructive">
        <AlertCircle className="h-4 w-4" />
        <AlertTitle>Senaryo hesaplanamadı</AlertTitle>
        <AlertDescription>
          Senaryo ortalaması hesaplanırken bir hata oluştu. Lütfen tekrar deneyin.
        </AlertDescription>
      </Alert>
    );
  }
  if (!scenarioReport) {
    return <TranscriptScenarioLoadingState />;
  }

  const hasScenarioChanges = !areScenarioCoursesEqual(scenarioCourses, initialCourses);
  const scenarioDelta = scenarioReport.currentCgpa == null
    ? null
    : round2(scenarioReport.currentCgpa - transcript.currentCgpa);
  const plannedFutureCourses = curriculumStatus
    ? buildPlannedFutureCourses(curriculumStatus, scenarioCourses)
    : [];
  const plannedCoursesByTermId = groupPlannedCoursesByTermId(plannedFutureCourses);
  const visibleTermReports = scenarioReport.terms.filter(term =>
    term.courses.length > 0 || (showFutureCourses && (plannedCoursesByTermId.get(term.termId)?.length ?? 0) > 0)
  );
  const continuationOptions = [...scenarioCourses].sort((left, right) => left.sortOrder - right.sortOrder);

  const handleReset = () => {
    setScenarioCourses(buildInitialScenarioCourses(transcript));
    setDraft(null);
    setDialogError(null);
  };

  const handleAddCourse = () => {
    const addCourseTerms = showFutureCourses ? terms : transcriptTerms.length > 0 ? transcriptTerms : terms;
    const lastTerm = addCourseTerms[addCourseTerms.length - 1];
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
      grade: '',
      originalGrade: null,
      excludeFromAverage: false,
      continuationCourseId: SELF_CONTINUATION,
      isAdded: true,
      isPlannedFromCurriculum: false,
      wasRepeated: false,
      sortOrder: getNextSortOrder(scenarioCourses),
    });
  };

  const handleOpenPlannedCourse = (course: PlannedScenarioCourse) => {
    setDialogError(null);
    setDraft({
      scenarioId: null,
      sourceRegistrationId: null,
      courseId: course.courseId,
      termId: String(course.termId),
      courseCode: course.courseCode,
      courseName: course.courseName,
      credits: String(course.credits),
      ects: String(course.ects),
      equivalentCourseIds: [],
      averageScore: '',
      grade: '',
      originalGrade: null,
      excludeFromAverage: false,
      continuationCourseId: SELF_CONTINUATION,
      isAdded: true,
      isPlannedFromCurriculum: true,
      wasRepeated: false,
      sortOrder: getNextSortOrder(scenarioCourses),
    });
  };

  // Satır backend'in hesapladığı (computed) veriyi gösterir; DÜZENLEME ise her zaman
  // yerel girdi state'inden (scenarioCourses) okunur — tek kaynak ayrımı korunur.
  const handleEditByScenarioId = (scenarioId: string) => {
    const course = scenarioCourses?.find(item => item.scenarioId === scenarioId);
    if (course) {
      handleEditCourse(course);
    }
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
      originalGrade: course.originalGrade,
      excludeFromAverage: course.excludeFromAverage,
      continuationCourseId: course.continuationCourseId === course.scenarioId ? SELF_CONTINUATION : course.continuationCourseId,
      isAdded: course.isAdded,
      isPlannedFromCurriculum: course.isPlannedFromCurriculum,
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
    const grade = normalizeGrade(draft.grade);
    const gradeMeta = getGradeMeta(grade);
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
      grade,
      originalGrade: draft.originalGrade,
      excludeFromAverage: gradeMeta.countsTowardsGpa ? draft.excludeFromAverage : true,
      continuationCourseId,
      isAdded: draft.isAdded,
      isPlannedFromCurriculum: draft.isPlannedFromCurriculum,
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
      <Card className="overflow-hidden border border-[#003366]/15 shadow-[0_10px_28px_rgba(15,23,42,0.10)]">
        {/* ── İnce kırmızı bilgi şeridi (Proliz tarzı) + sağda AGNO özeti ── */}
        <div className="flex flex-col gap-3 bg-[#d14b51] px-4 py-2.5 text-white lg:flex-row lg:items-center lg:justify-between">
          <div className="min-w-0">
            <p className="text-[12.5px] leading-snug">
              <span className="font-bold">Bilgi Notu</span>{' '}
              Bu ekrandaki işlemler sistem üzerinde herhangi bir değişiklik yapılmadan, sadece bilgilendirme amaçlı
              olarak yapılmaktadır. Veriler üzerinde herhangi bir etkisi yoktur. Gerçek not ortalamanızı transkript
              üzerinden kontrol ediniz.
            </p>
            <p className="mt-1 text-[12px] text-white/85">
              Ortalamalar AKTS ağırlığına ve aynı dersin son denemesine göre hesaplanır. Aktif dönemde kayıtlı, notu
              henüz girilmemiş dersler "Projeksiyon" etiketiyle eklenir; harf seçtikçe ortalama güncellenir.
            </p>
          </div>

          {/* AGNO özeti — kompakt, kırmızı şeridin sağında (eski sade ayrı görünüm) */}
          <div className="flex shrink-0 items-center gap-4 rounded-lg bg-white/10 px-3.5 py-2 text-[13px]">
            <div className="text-center">
              <div className="text-[10px] uppercase tracking-wide text-white/70">Gerçek</div>
              <div className="font-bold tabular-nums">{formatNumber(transcript.currentCgpa)}</div>
            </div>
            <div className="text-center">
              <div className="text-[10px] uppercase tracking-wide text-white/70">Senaryo</div>
              <div className="font-bold tabular-nums">{formatNumber(scenarioReport.currentCgpa)}</div>
            </div>
            <div className="text-center">
              <div className="text-[10px] uppercase tracking-wide text-white/70">Fark</div>
              <div className={cn('font-bold tabular-nums', scenarioDelta == null ? 'text-white/80' : scenarioDelta >= 0 ? 'text-emerald-100' : 'text-rose-100')}>
                {scenarioDelta == null ? '—' : `${scenarioDelta >= 0 ? '+' : ''}${formatNumber(scenarioDelta)}`}
              </div>
            </div>
          </div>
        </div>

        {/* ── Öğrenci bilgileri + aksiyonlar: TEK grid, her satır hizalı (bilgi satırı ↔ buton) ── */}
        <CardContent className="grid items-center gap-x-8 gap-y-3.5 p-5 text-[13px] lg:grid-cols-[repeat(3,minmax(0,1fr))_240px]">
          {/* Satır 1 */}
          <ScenarioInfoRow label="Adı Soyadı" value={transcript.fullName || studentName} />
          <ScenarioInfoRow label="Fakülte" value={transcript.facultyName} />
          <ScenarioInfoRow label="Öğrenci No" value={transcript.studentNumber} />
          <Button type="button" variant="outline" size="sm" className="w-full lg:border-l-0" onClick={handleReset} disabled={!hasScenarioChanges}>
            <RefreshCcw className="h-4 w-4" />
            Başlangıç Durumuna Getir
          </Button>

          {/* Satır 2 */}
          <ScenarioInfoRow label="Program" value={transcript.programName} />
          <ScenarioInfoRow label="Kayıt Tarihi" value={formatDate(transcript.enrollmentDate)} />
          {advisorName != null ? (
            <ScenarioInfoRow label="Danışmanı" value={advisorName} />
          ) : (
            <div aria-hidden="true" />
          )}
          <Button
            type="button"
            size="sm"
            className="w-full"
            variant={showFutureCourses ? 'default' : 'outline'}
            onClick={() => setShowFutureCourses(value => !value)}
            disabled={isCurriculumLoading || plannedFutureCourses.length === 0}
          >
            {showFutureCourses ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
            {isCurriculumLoading
              ? 'Müfredat Yükleniyor'
              : showFutureCourses
                ? 'Sonraki Dersleri Gizle'
                : `Sonraki Dersleri Aç (${plannedFutureCourses.length})`}
          </Button>

          {/* Satır 3 */}
          <ScenarioInfoRow label="Durum" value={STUDENT_STATUS_LABELS[transcript.studentStatus] || transcript.studentStatus} />
          <ScenarioInfoRow label="Hesaplanan Genel Ort." value={formatNumber(scenarioReport.currentCgpa)} emphasize />
          <ScenarioInfoRow label="Toplam Geçilen AKTS" value={String(scenarioReport.totalEarnedEcts)} />
          <Button type="button" size="sm" className="w-full" onClick={handleAddCourse} disabled={terms.length === 0}>
            <Plus className="h-4 w-4" />
            Yeni Ders Ekle
          </Button>
        </CardContent>
      </Card>

      {visibleTermReports.length === 0 ? (
        <Card>
          <CardContent className="py-16 text-center text-muted-foreground">
            Senaryo için gösterilecek ders kaydı bulunmuyor.
          </CardContent>
        </Card>
      ) : (
        <div className="grid items-stretch gap-5 xl:grid-cols-2">
          {visibleTermReports.map(term => (
            <Card key={term.termId} className="flex h-full flex-col overflow-hidden rounded-[10px] border border-slate-300 bg-white shadow-[0_10px_24px_rgba(15,23,42,0.10)]">
              <div className="bg-[#6f7073] px-3 py-2 text-[15px] font-bold text-white">
                {term.termName}
              </div>

              <CardContent className="flex flex-1 flex-col p-0">
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
                    {buildVisibleRowsForTerm(term, showFutureCourses ? plannedCoursesByTermId.get(term.termId) ?? [] : []).map((row, index) =>
                      row.kind === 'scenario' ? (
                        <ScenarioCourseRow
                          key={row.course.scenarioId}
                          course={row.course}
                          isStriped={index % 2 === 1}
                          onEdit={() => handleEditByScenarioId(row.course.scenarioId)}
                        />
                      ) : (
                        <PlannedCourseRow
                          key={row.course.scenarioId}
                          course={row.course}
                          isStriped={index % 2 === 1}
                          onOpen={() => handleOpenPlannedCourse(row.course)}
                        />
                      )
                    )}
                  </tbody>
                </table>

                <div className="mt-auto flex flex-wrap gap-x-4 gap-y-1 border-t border-slate-200 bg-slate-50/40 px-3 py-3 text-[13px] font-semibold text-[#5b84bf]">
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
                    onValueChange={value => onChange(current => {
                      if (!current) {
                        return current;
                      }

                      const nextGrade = normalizeGrade(value);
                      return {
                        ...current,
                        grade: nextGrade,
                        excludeFromAverage: getGradeMeta(nextGrade).countsTowardsGpa
                          ? false
                          : true,
                      };
                    })}
                  >
                    <SelectTrigger>
                      <SelectValue placeholder="Not seçiniz" />
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
                    <SelectItem value={SELF_CONTINUATION}>Bağımsız Ders</SelectItem>
                    {courses.filter(course => course.scenarioId !== draft.scenarioId).map(course => (
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

function ScenarioCourseRow({
  course,
  isStriped,
  onEdit,
}: {
  course: ScenarioComputedCourse;
  isStriped: boolean;
  onEdit: () => void;
}) {
  return (
    <tr className={cn('border-b border-slate-200/90', isStriped && 'bg-slate-50/30')}>
      <td className="px-2 py-2 align-top font-mono text-[12.5px] font-semibold text-[#234983]">
        <div className="flex flex-wrap items-center gap-1">
          <span>{course.courseCode}</span>
          {course.isAdded && (
            <span className="rounded-full bg-primary/10 px-1.5 py-0.5 text-[9px] font-semibold uppercase tracking-[0.04em] text-primary">
              {course.isPlannedFromCurriculum ? 'Plan' : 'Yeni'}
            </span>
          )}
          {!course.isAdded && course.originalGrade == null && (
            <span className="rounded-full bg-sky-500/10 px-1.5 py-0.5 text-[9px] font-semibold uppercase tracking-[0.04em] text-sky-700">
              Projeksiyon
            </span>
          )}
          {course.isRepeated && (
            <span className="rounded-full bg-amber-500/10 px-1.5 py-0.5 text-[9px] font-semibold text-amber-700">
              Tekrar
            </span>
          )}
        </div>
      </td>
      <td className={cn('break-words px-2 py-2 align-top text-[12.5px] leading-5 text-slate-700', !course.isPassed && course.countsTowardsGpa && 'text-destructive')}>
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
        {course.grade || '—'}
      </td>
      <td className="px-1 py-2 text-center align-top text-[12px] font-semibold text-slate-600">
        {course.countsTowardsGpa ? 'D' : '-'}
      </td>
      <td className="px-1 py-2 text-center align-top">
        <button
          type="button"
          onClick={onEdit}
          className="inline-flex h-7 w-7 items-center justify-center rounded-md border border-sky-300 bg-sky-50 text-sky-600 transition-colors hover:bg-sky-100"
          title="Dersi düzenle"
        >
          <PencilLine className="h-3.5 w-3.5" />
        </button>
      </td>
    </tr>
  );
}

function PlannedCourseRow({
  course,
  isStriped,
  onOpen,
}: {
  course: PlannedScenarioCourse;
  isStriped: boolean;
  onOpen: () => void;
}) {
  return (
    <tr className={cn('border-b border-slate-200/90 bg-slate-50/70 opacity-60', isStriped && 'bg-slate-100/70')}>
      <td className="px-2 py-2 align-top font-mono text-[12.5px] font-semibold text-[#234983]">
        <div className="flex flex-wrap items-center gap-1">
          <span>{course.courseCode}</span>
          <span className="rounded-full bg-slate-300/60 px-1.5 py-0.5 text-[9px] font-semibold uppercase tracking-[0.04em] text-slate-700">
            Plan
          </span>
        </div>
      </td>
      <td className="break-words px-2 py-2 align-top text-[12.5px] leading-5 text-slate-500">
        {course.courseName}
        {!course.prerequisitesMet && course.prerequisites.length > 0 && (
          <span className="ml-1 text-[11px] text-amber-700">
            ({course.prerequisites.join(', ')})
          </span>
        )}
      </td>
      <td className="px-1 py-2 text-center align-top font-medium text-slate-500">{course.credits}</td>
      <td className="px-1 py-2 text-center align-top font-medium text-slate-500">{course.ects}</td>
      <td className="px-1 py-2 text-center align-top font-medium text-slate-400">—</td>
      <td className="px-1 py-2 text-center align-top font-mono font-bold text-slate-400">—</td>
      <td className="px-1 py-2 text-center align-top text-[12px] font-semibold text-slate-400">-</td>
      <td className="px-1 py-2 text-center align-top">
        <button
          type="button"
          onClick={onOpen}
          className="inline-flex h-7 w-7 items-center justify-center rounded-md border border-slate-300 bg-white text-slate-600 transition-colors hover:bg-slate-100"
          title="Dersi senaryoya aç"
        >
          <Plus className="h-3.5 w-3.5" />
        </button>
      </td>
    </tr>
  );
}

function TranscriptScenarioLoadingState() {
  return (
    <div className="space-y-6">
      <Skeleton className="h-64 rounded-2xl" />
      <div className="grid gap-5 xl:grid-cols-2">
        <Skeleton className="h-[420px] rounded-2xl" />
        <Skeleton className="h-[420px] rounded-2xl" />
      </div>
    </div>
  );
}

function ScenarioInfoRow({ label, value, emphasize = false }: { label: string; value: string; emphasize?: boolean }) {
  return (
    <div className="flex items-baseline gap-2 leading-tight">
      <span className="shrink-0 text-muted-foreground">{label}:</span>
      <span className={cn('truncate font-semibold text-foreground', emphasize && 'text-primary')} title={value}>{value}</span>
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

function buildFutureScenarioTerms(
  transcriptTerms: ReturnType<typeof buildScenarioTerms>,
  curriculumStatus: CurriculumStatusDto,
) {
  const semestersWithFutureCourses = curriculumStatus.semesters.filter(semester =>
    semester.courses.some(course => !course.takenTermLabel)
  );
  const baseYear = transcriptTerms.at(-1)?.year ?? new Date().getFullYear();

  return semestersWithFutureCourses.map((semester, index) => ({
    termId: getFutureTermId(semester.semesterNumber),
    termName: `Planlanan ${semester.label}`,
    year: baseYear + Math.floor(index / 2),
    semester: semester.label,
    order: transcriptTerms.length + index,
  }));
}

function buildPlannedFutureCourses(
  curriculumStatus: CurriculumStatusDto,
  scenarioCourses: ScenarioCourse[],
): PlannedScenarioCourse[] {
  const activeCourseIds = new Set(
    scenarioCourses
      .map(course => course.courseId)
      .filter((courseId): courseId is number => courseId != null),
  );
  const activeCourseCodes = new Set(scenarioCourses.map(course => normalizeCourseCode(course.courseCode)));

  return curriculumStatus.semesters.flatMap(semester =>
    semester.courses
      .filter(course => isFutureCurriculumCourse(course, activeCourseIds, activeCourseCodes))
      .map(course => ({
        scenarioId: `planned-${semester.semesterNumber}-${course.courseId}`,
        courseId: course.courseId,
        termId: getFutureTermId(semester.semesterNumber),
        courseCode: course.code,
        courseName: course.name,
        credits: course.credits,
        ects: course.ects,
        isCompulsory: course.isCompulsory,
        prerequisites: course.prerequisites,
        prerequisitesMet: course.prerequisitesMet,
      }))
  );
}

function isFutureCurriculumCourse(
  course: CurriculumCourseStatusDto,
  activeCourseIds: Set<number>,
  activeCourseCodes: Set<string>,
) {
  return !course.takenTermLabel
    && !activeCourseIds.has(course.courseId)
    && !activeCourseCodes.has(normalizeCourseCode(course.code));
}

function groupPlannedCoursesByTermId(courses: PlannedScenarioCourse[]) {
  const grouped = new Map<number, PlannedScenarioCourse[]>();
  for (const course of courses) {
    const current = grouped.get(course.termId) ?? [];
    current.push(course);
    grouped.set(course.termId, current);
  }

  for (const [termId, termCourses] of grouped) {
    grouped.set(termId, termCourses.sort((left, right) => left.courseCode.localeCompare(right.courseCode, 'tr-TR')));
  }

  return grouped;
}

function buildVisibleRowsForTerm(term: ScenarioTermReport, plannedCourses: PlannedScenarioCourse[]) {
  return [
    ...term.courses.map(course => ({ kind: 'scenario' as const, course })),
    ...plannedCourses.map(course => ({ kind: 'planned' as const, course })),
  ];
}

function getFutureTermId(semesterNumber: number) {
  return FUTURE_TERM_ID_OFFSET + semesterNumber;
}

function formatNumber(value: number | null | undefined) {
  // GPA'ya giren ders yoksa backend null döner → "—" (sahte 0,00 değil).
  if (value == null) {
    return '—';
  }
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

// Düzenlenebilir yerel ders → backend istek girdisi. equivalentCourseIds backend hesabında
// kullanılmaz (zincir continuationCourseId üzerinden çözülür); bilinçli olarak gönderilmez.
function toScenarioCourseInput(course: ScenarioCourse): ScenarioCourseInput {
  return {
    scenarioId: course.scenarioId,
    sourceRegistrationId: course.sourceRegistrationId,
    courseId: course.courseId,
    termId: course.termId,
    courseCode: course.courseCode,
    courseName: course.courseName,
    credits: course.credits,
    ects: course.ects,
    averageScore: course.averageScore,
    grade: course.grade,
    originalGrade: course.originalGrade,
    excludeFromAverage: course.excludeFromAverage,
    continuationCourseId: course.continuationCourseId,
    isAdded: course.isAdded,
    isPlannedFromCurriculum: course.isPlannedFromCurriculum,
    wasRepeated: course.wasRepeated,
    sortOrder: course.sortOrder,
  };
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
