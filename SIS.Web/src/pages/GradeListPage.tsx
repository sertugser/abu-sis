import { useEffect, useMemo, useState } from 'react';
import { useQueries } from '@tanstack/react-query';
import { AlertCircle, BarChart3, BookOpen, Info, Maximize2, Minimize2, Printer, RefreshCw, Search, X } from 'lucide-react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogClose,
  DialogContent,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetHeader,
  SheetTitle,
} from '@/components/ui/sheet';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { cn } from '@/lib/utils';
import { getGradeBadgeClass } from '@/lib/gradeUtils';
import { getStudentGradeStatistics } from '../api/student.api';
import { useCourseHistory } from '../hooks/useCourseHistory';
import { useStudentGradeStatistics } from '../hooks/useStudentGradeStatistics';
import { useTermGpaHistory } from '../hooks/useTermGpaHistory';
import { QUERY_KEYS } from '../lib/constants';
import { TermPickerCombobox } from '@/components/my-courses/TermPickerCombobox';
import { NotlarInfoBanner } from '@/components/my-courses/NotlarInfoBanner';
import { formatGpa } from '@/lib/gpaFormat';
import { Link } from 'react-router-dom';
import type { CourseHistoryItemDto } from '../types/registration';
import type { StudentComponentStatisticsDto, StudentGradeStatisticsDto } from '../types/student';

type GradeListRow = CourseHistoryItemDto & {
  termId: number;
  termName: string;
  year: number;
  semester: string;
};

type GradeListRowWithStatistics = GradeListRow & {
  statistics: StudentGradeStatisticsDto | null;
  statisticsLoading: boolean;
};


export default function GradeListPage() {
  const { data: history, isLoading, error, refetch } = useCourseHistory();
  const [selectedTermId, setSelectedTermId] = useState<number | null>(null);
  const [searchTerm, setSearchTerm] = useState('');
  const [selectedRegistrationId, setSelectedRegistrationId] = useState<number | null>(null);
  const [popupFullscreen, setPopupFullscreen] = useState(false);
  const [summaryOpen, setSummaryOpen] = useState(false);

  const terms = useMemo(() => history ?? [], [history]);

  // Varsayılan dönem: backend listesinin ilki (en yeni). MyCoursesPage ile aynı pattern.
  useEffect(() => {
    if (selectedTermId === null && terms.length > 0) {
      setSelectedTermId(terms[0].termId);
    }
  }, [terms, selectedTermId]);

  // Dönem değişince arama temizlensin — eski filtre yeni dönemde "ders bulunamadı" tuzağı yaratır.
  const handleTermSelect = (termId: number) => {
    setSelectedTermId(termId);
    setSearchTerm('');
  };

  const selectedTerm = terms.find(term => term.termId === selectedTermId) ?? terms[0] ?? null;

  const rows = useMemo<GradeListRow[]>(() => {
    if (!selectedTerm) return [];

    return selectedTerm.courses.map(course => ({
      ...course,
      termId: selectedTerm.termId,
      termName: selectedTerm.termName,
      year: selectedTerm.year,
      semester: selectedTerm.semester,
    }));
  }, [selectedTerm]);

  const gradeStatisticQueries = useQueries({
    queries: rows.map(row => ({
      queryKey: QUERY_KEYS.studentGradeStatistics(row.registrationId),
      queryFn: () => getStudentGradeStatistics(row.registrationId),
      staleTime: 2 * 60 * 1000,
    })),
  });

  const rowsWithStatistics = useMemo<GradeListRowWithStatistics[]>(() => rows.map((row, index) => ({
    ...row,
    statistics: gradeStatisticQueries[index]?.data ?? null,
    statisticsLoading: gradeStatisticQueries[index]?.isLoading ?? false,
  })), [gradeStatisticQueries, rows]);

  const filteredRows = useMemo(() => {
    const query = searchTerm.trim().toLocaleLowerCase('tr-TR');
    if (!query) return rowsWithStatistics;

    return rowsWithStatistics.filter(row =>
      `${row.courseCode} ${row.courseName} ${row.instructorName}`
        .toLocaleLowerCase('tr-TR')
        .includes(query)
    );
  }, [rowsWithStatistics, searchTerm]);

  const selectedRow = rows.find(row => row.registrationId === selectedRegistrationId);

  if (isLoading) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-24 rounded-xl" />
        <Skeleton className="h-12 rounded-xl" />
        <Skeleton className="h-96 rounded-xl" />
      </div>
    );
  }

  if (error) {
    return (
      <Alert variant="destructive">
        <AlertCircle className="h-4 w-4" />
        <AlertDescription className="flex items-center justify-between gap-4">
          <span>Not listesi yüklenirken hata oluştu.</span>
          <Button size="sm" variant="outline" onClick={() => refetch()}>
            Yeniden Dene
          </Button>
        </AlertDescription>
      </Alert>
    );
  }

  const notlananCount = rowsWithStatistics.filter(row => shouldShowLetterGrade(row.statistics)).length;
  const tekrarCount = rows.filter(row => row.isRepeated).length;

  return (
    <div className="flex h-full min-w-0 flex-1 flex-col">
      {/* Proliz-tarzı tek entegre konteyner — sayfa-boyu, kompakt toolbar + temiz tablo + info footer */}
      <div className="flex flex-1 flex-col overflow-hidden rounded-lg border border-border bg-card shadow-sm">

        {/* Tek toolbar — dönem + arama + kompakt istatistikler + Özet Not Durumu butonu */}
        <div className="flex flex-wrap items-center gap-x-4 gap-y-2 border-b border-border bg-accent/30 px-5 py-2.5">
          <div className="flex items-center gap-2">
            <span className="text-[11px] font-semibold uppercase tracking-wide text-muted-foreground">
              Dönem
            </span>
            <TermPickerCombobox
              terms={terms}
              selectedTermId={selectedTerm?.termId ?? null}
              onSelect={handleTermSelect}
              className="h-8 w-[220px] max-w-none"
            />
          </div>
          <div className="relative min-w-[220px] flex-1">
            <Search className="absolute left-2.5 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-muted-foreground" />
            <Input
              value={searchTerm}
              onChange={event => setSearchTerm(event.target.value)}
              placeholder="Ders kodu, ders adı veya öğretim elemanı ile ara"
              className="h-8 pl-8 text-sm"
            />
          </div>
          <div className="ml-auto flex items-center gap-3 text-xs">
            <SummaryInline label="Ders" value={String(rows.length)} />
            <span className="text-border" aria-hidden>•</span>
            <SummaryInline label="Notlanan" value={String(notlananCount)} />
            <span className="text-border" aria-hidden>•</span>
            <SummaryInline label="Tekrar" value={String(tekrarCount)} />
          </div>
          <span className="h-5 w-px bg-border" aria-hidden />
          <Button
            size="sm"
            onClick={() => setSummaryOpen(true)}
            disabled={!selectedTerm}
            className="h-8 gap-1.5 bg-[#003366] text-white shadow-sm transition-colors hover:bg-[#003366]/90"
          >
            <BarChart3 className="h-3.5 w-3.5" />
            Özet Not Durumu
          </Button>
        </div>

        {/* Tablo (xl+) veya kart liste (altında) — flex-1 ile kalan sayfayı doldur, footer alta yapışsın */}
        <div className="flex flex-1 flex-col overflow-auto">
        <div className="divide-y divide-border xl:hidden">
          {filteredRows.map(row => (
            <GradeListCourseCard
              key={row.registrationId}
              row={row}
              onOpenStatistics={() => setSelectedRegistrationId(row.registrationId)}
            />
          ))}
        </div>

        <div className="hidden overflow-x-auto xl:block">
          <Table className="min-w-[1180px] text-[13px]">
            <TableHeader>
              <TableRow className="border-b border-slate-300 bg-slate-100 hover:bg-slate-100">
                <TableHead className="h-9 w-12 px-3 text-center text-[11px] font-semibold uppercase tracking-wide text-slate-700">Şb</TableHead>
                <TableHead className="h-9 w-24 px-3 text-[11px] font-semibold uppercase tracking-wide text-slate-700">Ders Kodu</TableHead>
                <TableHead className="h-9 px-3 text-[11px] font-semibold uppercase tracking-wide text-slate-700">Ders Adı</TableHead>
                <TableHead className="h-9 w-44 px-3 text-[11px] font-semibold uppercase tracking-wide text-slate-700">Öğretim Elemanı</TableHead>
                <TableHead className="h-9 w-36 px-3 text-center text-[11px] font-semibold uppercase tracking-wide text-slate-700">Sonuç Durumu</TableHead>
                <TableHead className="h-9 px-3 text-[11px] font-semibold uppercase tracking-wide text-slate-700">Sınav Notları</TableHead>
                <TableHead className="h-9 w-14 px-3 text-center text-[11px] font-semibold uppercase tracking-wide text-slate-700">Ort</TableHead>
                <TableHead className="h-9 w-14 px-3 text-center text-[11px] font-semibold uppercase tracking-wide text-slate-700">AKTS</TableHead>
                <TableHead className="h-9 w-14 px-3 text-center text-[11px] font-semibold uppercase tracking-wide text-slate-700">Not</TableHead>
                <TableHead className="h-9 w-20 px-3 text-center text-[11px] font-semibold uppercase tracking-wide text-slate-700">Durumu</TableHead>
                <TableHead className="h-9 w-14 px-3 text-right text-[11px] font-semibold uppercase tracking-wide text-slate-700">İşlem</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {filteredRows.map((row, index) => (
                <TableRow
                  key={row.registrationId}
                  className={cn(
                    'border-b border-slate-200 transition-colors hover:bg-[#003366]/5',
                    index % 2 === 1 && 'bg-slate-50/50'
                  )}
                >
                  <TableCell className="px-3 py-2 text-center font-mono text-[12px] font-semibold">{row.sectionNumber}</TableCell>
                  <TableCell className="px-3 py-2 font-mono text-[13px] font-bold text-[#003366]">
                    {row.courseCode}
                  </TableCell>
                  <TableCell className="px-3 py-2 font-medium leading-snug text-foreground">
                    {row.courseName}
                    {row.isRepeated && (
                      <span className="ml-1.5 inline-flex items-center rounded border border-amber-400/60 px-1 py-0 text-[10px] font-semibold uppercase tracking-wide text-amber-700">
                        Tekrar
                      </span>
                    )}
                  </TableCell>
                  <TableCell className="break-words px-3 py-2 text-[12px] leading-snug text-muted-foreground">{row.instructorName || '-'}</TableCell>
                  <TableCell className="px-3 py-2 text-center">
                    <ResultStatusText statistics={row.statistics} isLoading={row.statisticsLoading} />
                  </TableCell>
                  <TableCell className="px-3 py-2">
                    <ExamScores statistics={row.statistics} isLoading={row.statisticsLoading} />
                  </TableCell>
                  <TableCell className="px-3 py-2 text-center font-mono text-[12px]">
                    {row.statisticsLoading ? '…' : formatWeightedAverage(row.statistics)}
                  </TableCell>
                  <TableCell className="px-3 py-2 text-center font-mono text-[12px] text-muted-foreground">{row.ects}</TableCell>
                  <TableCell className="px-3 py-2 text-center">
                    {shouldShowLetterGrade(row.statistics) ? (
                      <span className={cn('inline-flex min-w-8 justify-center rounded px-1.5 py-0.5 font-mono text-[11px] font-bold', getGradeBadgeClass(row.statistics!.studentLetterGrade))}>
                        {row.statistics!.studentLetterGrade}
                      </span>
                    ) : (
                      <span className="text-xs text-muted-foreground">—</span>
                    )}
                  </TableCell>
                  <TableCell className="px-3 py-2 text-center">
                    <CourseResultText statistics={row.statistics} isLoading={row.statisticsLoading} />
                  </TableCell>
                  <TableCell className="px-3 py-2 text-right">
                    <Button
                      type="button"
                      variant="ghost"
                      size="sm"
                      onClick={() => setSelectedRegistrationId(row.registrationId)}
                      aria-label={`${row.courseCode} sınav istatistiklerini aç`}
                      title="İstatistik"
                      className="h-7 w-7 px-0 text-muted-foreground hover:bg-[#003366]/10 hover:text-[#003366]"
                    >
                      <BarChart3 className="h-3.5 w-3.5" />
                      <span className="sr-only">İstatistik</span>
                    </Button>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>

        {filteredRows.length === 0 && (
          <div className="py-16 text-center">
            <BookOpen className="mx-auto mb-3 h-10 w-10 text-muted-foreground/40" />
            <h3 className="font-heading text-lg font-semibold text-foreground">
              {rows.length === 0 ? 'Seçilen dönemde ders kaydı bulunamadı' : 'Ders bulunamadı'}
            </h3>
            <p className="mt-1 text-sm text-muted-foreground">
              {rows.length === 0
                ? 'Bu dönem için öğrencinin ders kayıt işleminden gelen ders kaydı yok.'
                : 'Arama kriterini değiştirerek tekrar deneyin.'}
            </p>
          </div>
        )}
        </div>

        {/* Footer — açıklama paneli (sade kart-içi, gri zemin yok) */}
        <div className="border-t border-border bg-card px-5 py-3">
          <div className="mb-1.5 flex items-center gap-1.5 text-[11px] font-semibold uppercase tracking-wide text-muted-foreground">
            <Info className="h-3.5 w-3.5" />
            <span>Notlar</span>
          </div>
          <NotlarInfoBanner />
        </div>
      </div>

      <Sheet open={summaryOpen} onOpenChange={setSummaryOpen}>
        <SheetContent side="right" className="w-full overflow-y-auto p-0 sm:max-w-md">
          <SheetHeader className="border-b border-border bg-[#003366] px-5 py-4 text-white">
            <SheetTitle className="text-white">Özet Not Durumu</SheetTitle>
            <SheetDescription className="text-white/70">
              {selectedTerm?.termName ?? '-'}
            </SheetDescription>
          </SheetHeader>
          {selectedTerm && (
            <TermGpaSummary termId={selectedTerm.termId} termName={selectedTerm.termName} />
          )}
        </SheetContent>
      </Sheet>

      <Dialog
        open={selectedRegistrationId !== null}
        onOpenChange={open => {
          if (!open) {
            setSelectedRegistrationId(null);
            setPopupFullscreen(false);
          }
        }}
      >
        <DialogContent
          className={cn(
            // Default shadcn X butonunu tamamen gizle — kendi X'imizi header'a ekleyeceğiz (diğer butonlarla aynı boy/stil)
            'overflow-hidden p-0 [&>button.absolute]:hidden',
            popupFullscreen
              ? 'h-[98vh] w-[98vw] max-h-[98vh] max-w-[98vw]'
              : 'max-h-[92vh] w-[calc(100vw-1rem)] max-w-[min(1280px,calc(100vw-2rem))]'
          )}
        >
          <DialogHeader className="sr-only">
            <DialogTitle>Sınav İstatistikleri</DialogTitle>
          </DialogHeader>
          {selectedRegistrationId !== null && (
            <GradeStatisticsPopup
              key={selectedRegistrationId}
              registrationId={selectedRegistrationId}
              fallbackTitle={selectedRow ? `${selectedRow.courseCode} - ${selectedRow.courseName}` : undefined}
              isFullscreen={popupFullscreen}
              onToggleFullscreen={() => setPopupFullscreen(value => !value)}
            />
          )}
        </DialogContent>
      </Dialog>
    </div>
  );
}

function GradeListCourseCard({
  row,
  onOpenStatistics,
}: {
  row: GradeListRowWithStatistics;
  onOpenStatistics: () => void;
}) {
  return (
    <article className="min-w-0 space-y-2 bg-card p-4 transition-colors hover:bg-accent/20">
      <div className="flex min-w-0 items-start justify-between gap-3">
        <div className="min-w-0">
          <div className="flex flex-wrap items-baseline gap-x-2 gap-y-1">
            <span className="font-mono text-sm font-bold text-[#003366]">{row.courseCode}</span>
            <span className="text-[11px] font-mono text-muted-foreground">Şb {row.sectionNumber}</span>
            {row.isRepeated && (
              <span className="inline-flex items-center rounded border border-amber-400/60 px-1 py-0 text-[10px] font-semibold uppercase tracking-wide text-amber-700">
                Tekrar
              </span>
            )}
          </div>
          <h2 className="mt-1 break-words text-sm font-semibold leading-snug text-foreground">
            {row.courseName}
          </h2>
          <p className="mt-0.5 break-words text-xs leading-snug text-muted-foreground">
            {row.instructorName || '-'}
          </p>
        </div>
        <Button
          type="button"
          variant="ghost"
          size="icon"
          onClick={onOpenStatistics}
          aria-label={`${row.courseCode} sınav istatistiklerini aç`}
          title="İstatistik"
          className="h-8 w-8 shrink-0 text-muted-foreground hover:bg-[#003366]/10 hover:text-[#003366]"
        >
          <BarChart3 className="h-4 w-4" />
        </Button>
      </div>

      <div className="grid min-w-0 gap-x-4 gap-y-1.5 text-[12px] sm:grid-cols-2">
        <GradeListInfoItem label="Sonuç Durumu">
          <ResultStatusText statistics={row.statistics} isLoading={row.statisticsLoading} />
        </GradeListInfoItem>
        <GradeListInfoItem label="Durumu">
          <CourseResultText statistics={row.statistics} isLoading={row.statisticsLoading} />
        </GradeListInfoItem>
        <GradeListInfoItem label="Ort">
          <span className="font-mono">
            {row.statisticsLoading ? '…' : formatWeightedAverage(row.statistics)}
          </span>
        </GradeListInfoItem>
        <GradeListInfoItem label="AKTS">
          <span className="font-mono text-muted-foreground">{row.ects}</span>
        </GradeListInfoItem>
        <GradeListInfoItem label="Not">
          {shouldShowLetterGrade(row.statistics) ? (
            <span className={cn('inline-flex min-w-8 justify-center rounded px-1.5 py-0.5 font-mono text-[11px] font-bold', getGradeBadgeClass(row.statistics!.studentLetterGrade))}>
              {row.statistics!.studentLetterGrade}
            </span>
          ) : (
            <span className="text-xs text-muted-foreground">—</span>
          )}
        </GradeListInfoItem>
        <GradeListInfoItem label="Sınav Notları" className="sm:col-span-2">
          <ExamScores statistics={row.statistics} isLoading={row.statisticsLoading} />
        </GradeListInfoItem>
      </div>
    </article>
  );
}

function GradeListInfoItem({
  label,
  className,
  children,
}: {
  label: string;
  className?: string;
  children: React.ReactNode;
}) {
  return (
    <div className={cn('flex min-w-0 items-baseline gap-2', className)}>
      <span className="shrink-0 text-[11px] font-semibold uppercase tracking-wide text-muted-foreground">
        {label}:
      </span>
      <span className="min-w-0 flex-1">{children}</span>
    </div>
  );
}

/**
 * Proliz tarzı sade text gösterimi: badge yerine kırmızı/yeşil renkli yazı.
 * "Sonuçlandırılmadı" kırmızı (uyarı tonu), "Sonuçlandırıldı" yeşil.
 */
function ResultStatusText({
  statistics,
  isLoading,
}: {
  statistics: StudentGradeStatisticsDto | null | undefined;
  isLoading: boolean;
}) {
  if (isLoading) {
    return <span className="text-[12px] text-muted-foreground">Yükleniyor</span>;
  }

  return isGradeFinalized(statistics)
    ? <span className="text-[12px] font-medium text-emerald-600">Sonuçlandırıldı</span>
    : <span className="text-[12px] font-medium text-red-600">Sonuçlandırılmadı</span>;
}

/**
 * Proliz tarzı sade inline metin: "Vize : 90 · Final : —"
 * Puan yoksa öğrencinin attendance'ına göre "G" (girmedi) veya "M" (mazeret).
 */
function ExamScores({
  statistics,
  isLoading,
}: {
  statistics: StudentGradeStatisticsDto | null | undefined;
  isLoading: boolean;
}) {
  if (isLoading) {
    return <span className="text-[12px] text-muted-foreground">Yükleniyor…</span>;
  }

  const visible = statistics?.components.filter(component =>
    component.studentScore != null
    || component.studentExamAttendance === 'Absent'
    || component.studentExamAttendance === 'Excused'
  ) ?? [];

  if (visible.length === 0) {
    return <span className="text-[12px] font-medium text-amber-600">Not girişi yapılmamış</span>;
  }

  return (
    <div className="flex flex-wrap items-baseline gap-x-3 gap-y-0.5 text-[12px] leading-tight">
      {visible.map((component, idx) => {
        const value = component.studentScore != null
          ? formatScore(component.studentScore)
          : component.studentExamAttendance === 'Excused' ? 'M' : 'G';
        const isFlag = component.studentScore == null;
        return (
          <span
            key={component.componentId}
            className="inline-flex items-baseline gap-1"
            title={isFlag
              ? (component.studentExamAttendance === 'Excused' ? 'Mazeretli — sınava girmedi' : 'Girmedi (devamsız)')
              : undefined}
          >
            <span className="text-muted-foreground">{component.name}:</span>
            <strong className={cn('font-semibold', isFlag ? 'text-amber-600' : 'text-foreground')}>
              {value}
            </strong>
            {idx < visible.length - 1 && <span className="text-muted-foreground/40">·</span>}
          </span>
        );
      })}
    </div>
  );
}

/**
 * Proliz tarzı sade text: badge yerine renkli yazı.
 * Geçti = yeşil, Kaldı = kırmızı, Çekildi = gri, Eksik = amber.
 */
function CourseResultText({
  statistics,
  isLoading,
}: {
  statistics: StudentGradeStatisticsDto | null | undefined;
  isLoading: boolean;
}) {
  if (isLoading) return <span className="text-xs text-muted-foreground">…</span>;
  if (!shouldShowLetterGrade(statistics)) return <span className="text-xs text-muted-foreground">—</span>;

  const grade = statistics!.studentLetterGrade!;
  const result = getCourseResult(grade);
  return <span className={cn('text-[12px] font-medium', RESULT_TEXT_TONE[result.variant])}>{result.label}</span>;
}

const RESULT_TEXT_TONE: Record<'success' | 'destructive' | 'warning' | 'outline' | 'secondary', string> = {
  success: 'text-emerald-600',
  destructive: 'text-red-600',
  warning: 'text-amber-600',
  outline: 'text-muted-foreground',
  secondary: 'text-muted-foreground',
};

/**
 * "Özet Not Durumu" drawer içeriği. Veri kaynağı: useTermGpaHistory (TermGpaPage'in
 * de kullandığı hook). Hesap kodu kopyalanmaz — aynı backend endpoint okunur.
 * İkibaşlılığı önlemek için bu component yalnız GÖRÜNÜM; rakamlar TermGpaPage ile birebir aynı.
 */
function TermGpaSummary({ termId, termName }: { termId: number; termName: string }) {
  const { data, isLoading, isError } = useTermGpaHistory();

  if (isLoading) {
    return (
      <div className="space-y-3 p-5">
        <Skeleton className="h-6 rounded" />
        <Skeleton className="h-16 rounded" />
        <Skeleton className="h-16 rounded" />
      </div>
    );
  }

  if (isError || !data) {
    return (
      <div className="p-5">
        <Alert variant="destructive">
          <AlertCircle className="h-4 w-4" />
          <AlertDescription>Özet bilgisi alınamadı.</AlertDescription>
        </Alert>
      </div>
    );
  }

  const term = data.terms.find(t => t.termId === termId);
  if (!term) {
    return (
      <div className="p-6 text-center text-sm text-muted-foreground">
        <p>"{termName}" için henüz dönem ortalaması hesaplanmadı.</p>
        <p className="mt-2">Harf notu ilan edilmemiş veya tüm dersler sonuçlandırılmamış olabilir.</p>
      </div>
    );
  }

  return (
    <div className="space-y-4 p-5">
      <div className="rounded-lg border border-border bg-card p-4">
        <p className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">Dönem Ortalaması (DNO)</p>
        <p className="mt-1 font-mono text-3xl font-bold text-[#003366]">{formatGpa(term.sgpa)}</p>
      </div>
      <div className="rounded-lg border border-border bg-card p-4">
        <p className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">Genel Not Ortalaması (AGNO)</p>
        <p className="mt-1 font-mono text-3xl font-bold text-[#003366]">{formatGpa(term.cgpa)}</p>
      </div>
      <div className="grid grid-cols-3 gap-2 text-center">
        <div className="rounded-md border border-border bg-muted/30 p-3">
          <p className="text-[11px] font-semibold uppercase tracking-wide text-muted-foreground">Ders</p>
          <p className="mt-0.5 font-mono text-lg font-bold">{term.courseCount}</p>
        </div>
        <div className="rounded-md border border-border bg-muted/30 p-3">
          <p className="text-[11px] font-semibold uppercase tracking-wide text-muted-foreground">Kredi</p>
          <p className="mt-0.5 font-mono text-lg font-bold">{term.totalCredits}</p>
        </div>
        <div className="rounded-md border border-border bg-muted/30 p-3">
          <p className="text-[11px] font-semibold uppercase tracking-wide text-muted-foreground">AKTS</p>
          <p className="mt-0.5 font-mono text-lg font-bold">{term.totalEcts}</p>
        </div>
      </div>
      <div className="pt-2">
        <Button asChild variant="outline" size="sm" className="w-full">
          <Link to="/donem-ortalamalari">Tüm dönemleri görüntüle →</Link>
        </Button>
      </div>
    </div>
  );
}

/** Toolbar altında kompakt inline metrik: "Ders: 4 • Notlanan: 0 • Tekrar: 2" */
function SummaryInline({ label, value }: { label: string; value: string }) {
  return (
    <span className="inline-flex items-baseline gap-1.5">
      <span className="text-[11px] font-semibold uppercase tracking-wide text-muted-foreground">
        {label}:
      </span>
      <span className="font-mono text-sm font-bold text-foreground">{value}</span>
    </span>
  );
}

function GradeStatisticsPopup({
  registrationId,
  fallbackTitle,
  isFullscreen,
  onToggleFullscreen,
}: {
  registrationId: number;
  fallbackTitle?: string;
  isFullscreen: boolean;
  onToggleFullscreen: () => void;
}) {
  const [commonEvaluation, setCommonEvaluation] = useState(false);
  const { data, isLoading, isError, refetch, isFetching } = useStudentGradeStatistics(registrationId, commonEvaluation);

  if (isLoading) {
    return (
      <div className="space-y-3 p-5">
        <Skeleton className="h-10 rounded-md" />
        <Skeleton className="h-56 rounded-md" />
        <Skeleton className="h-44 rounded-md" />
      </div>
    );
  }

  if (isError || !data) {
    return (
      <div className="p-5">
        <Alert variant="destructive">
          <AlertCircle className="h-4 w-4" />
          <AlertDescription className="flex items-center justify-between gap-3">
            <span>Sınav istatistikleri alınamadı.</span>
            <Button size="sm" variant="outline" onClick={() => refetch()}>Yeniden Dene</Button>
          </AlertDescription>
        </Alert>
      </div>
    );
  }

  return (
    <ExamStatisticsPanel
      data={data}
      fallbackTitle={fallbackTitle}
      commonEvaluation={commonEvaluation}
      onCommonEvaluationChange={setCommonEvaluation}
      isFullscreen={isFullscreen}
      onToggleFullscreen={onToggleFullscreen}
      onRefresh={() => refetch()}
      isRefreshing={isFetching}
    />
  );
}

function ExamStatisticsPanel({
  data,
  fallbackTitle,
  commonEvaluation,
  onCommonEvaluationChange,
  isFullscreen,
  onToggleFullscreen,
  onRefresh,
  isRefreshing,
}: {
  data: StudentGradeStatisticsDto;
  fallbackTitle?: string;
  commonEvaluation: boolean;
  onCommonEvaluationChange: (value: boolean) => void;
  isFullscreen: boolean;
  onToggleFullscreen: () => void;
  onRefresh: () => void;
  isRefreshing: boolean;
}) {
  const visibleComponents = data.components.filter(component =>
    component.enteredStudentCount > 0 || component.studentScore != null
  );

  return (
    <div className={cn('flex flex-col bg-card text-sm', isFullscreen ? 'max-h-[98vh]' : 'max-h-[92vh]')}>
      {/* Sticky header — sade beyaz zemin, sol başlık + sağ toolbar (X butonu da aynı stilde, ayraçla ayrılmış) */}
      <div className="sticky top-0 z-10 flex items-center justify-between gap-3 border-b border-border bg-card/95 px-5 py-3 backdrop-blur supports-[backdrop-filter]:bg-card/80">
        <div className="flex items-baseline gap-2">
          <BarChart3 className="h-4 w-4 text-[#003366]" />
          <h2 className="font-heading text-base font-bold text-[#003366]">Sınav İstatistikleri</h2>
          <span className="hidden text-xs text-muted-foreground sm:inline">• {data.courseCode}</span>
        </div>
        <div className="flex items-center gap-1">
          <Button
            type="button"
            variant="outline"
            size="sm"
            onClick={onRefresh}
            disabled={isRefreshing}
            className="h-8 gap-1.5"
            title="Yenile"
          >
            <RefreshCw className={cn('h-3.5 w-3.5', isRefreshing && 'animate-spin')} />
            <span className="hidden sm:inline">Yenile</span>
          </Button>
          <Button
            type="button"
            variant="outline"
            size="sm"
            onClick={onToggleFullscreen}
            className="h-8 gap-1.5"
            title={isFullscreen ? 'Pencereyi Daralt' : 'Tam Ekran'}
          >
            {isFullscreen ? <Minimize2 className="h-3.5 w-3.5" /> : <Maximize2 className="h-3.5 w-3.5" />}
            <span className="hidden sm:inline">{isFullscreen ? 'Daralt' : 'Tam Ekran'}</span>
          </Button>
          <Button
            type="button"
            variant="outline"
            size="sm"
            onClick={() => window.print()}
            className="h-8 gap-1.5"
            title="Yazdır"
          >
            <Printer className="h-3.5 w-3.5" />
            <span className="hidden sm:inline">Yazdır</span>
          </Button>
          {/* Görsel ayraç + Kapat butonu (diğer 3 buton ile aynı h-8 + outline; hover'da kırmızı vurgu) */}
          <span className="mx-1 h-5 w-px bg-border" aria-hidden />
          <DialogClose asChild>
            <Button
              type="button"
              variant="outline"
              size="sm"
              className="h-8 gap-1.5 hover:border-red-300 hover:bg-red-50 hover:text-red-600"
              title="Kapat (Esc)"
              aria-label="Kapat"
            >
              <X className="h-3.5 w-3.5" />
              <span className="hidden sm:inline">Kapat</span>
            </Button>
          </DialogClose>
        </div>
      </div>

      {/* İçerik scroll */}
      <div className="min-w-0 flex-1 overflow-y-auto">
        {/* Ders kimlik bilgileri — 2 kolon kart grid */}
        <div className="grid gap-px bg-border sm:grid-cols-2">
          <InfoCell label="Fakülte / Program" value={data.facultyProgram || '-'} />
          <InfoCell label="Öğretim Elemanı" value={data.instructorName || '-'} />
          <InfoCell label="Ders Kodu" value={data.courseCode} mono />
          <InfoCell label="Ders Adı" value={data.courseName || fallbackTitle || '-'} />
        </div>

        {/* Harf Dağılımı + Sonuç — yan yana */}
        <div className="grid gap-px border-t border-border bg-border lg:grid-cols-2">
          <GradeRangeDistribution data={data} />
          <ResultOverview data={data} />
        </div>

        {/* Sınav Öğrenci Sayıları — modern toggle, kompakt kartlar */}
        <section className="border-t border-border bg-card">
          <div className="flex flex-wrap items-center justify-between gap-3 border-b border-border bg-muted/30 px-5 py-2.5">
            <h3 className="text-sm font-semibold text-[#003366]">Sınav Öğrenci Sayıları</h3>
            <label className="inline-flex cursor-pointer items-center gap-2 text-xs text-muted-foreground hover:text-foreground">
              <input
                type="checkbox"
                checked={commonEvaluation}
                onChange={event => onCommonEvaluationChange(event.target.checked)}
                className="h-3.5 w-3.5 rounded border-border accent-[#003366]"
              />
              Ortak Değerlendirme Sonucu
            </label>
          </div>

          {visibleComponents.length === 0 ? (
            <p className="px-5 py-10 text-center text-sm text-muted-foreground">
              Bu ders için henüz not girişi yapılmamış.
            </p>
          ) : (
            <div className="divide-y divide-border">
              {visibleComponents.map(component => (
                <ComponentStatisticsBlock key={component.componentId} component={component} />
              ))}
            </div>
          )}
        </section>
      </div>
    </div>
  );
}

/** Üst ders kimlik bilgileri için 2-kolon hücre. Label üstte, value altta. */
function InfoCell({ label, value, mono = false }: { label: string; value: string; mono?: boolean }) {
  return (
    <div className="bg-card px-5 py-2.5">
      <p className="text-[10px] font-semibold uppercase tracking-wider text-muted-foreground">{label}</p>
      <p className={cn('mt-0.5 text-sm font-medium text-foreground', mono && 'font-mono')}>{value}</p>
    </div>
  );
}

type GradeRangeTab = 'final' | 'makeup';

function GradeRangeDistribution({ data }: { data: StudentGradeStatisticsDto }) {
  const [tab, setTab] = useState<GradeRangeTab>('final');

  // Karar 2026-05-30: aynı GradingCriteria aralıkları, sadece sayım kaynağı değişir.
  const activeRanges = tab === 'final' ? data.gradeRanges : data.makeupGradeRanges;
  const makeupTotal = data.makeupGradeRanges.reduce((sum, r) => sum + r.studentCount, 0);
  const showDistribution = data.isResultFinalized
    && (tab === 'final' ? data.summary.gradedStudentCount > 0 : makeupTotal > 0);
  const maxGradeCount = Math.max(1, ...activeRanges.map(range => range.studentCount));

  return (
    <section className="bg-card">
      <SectionTitle>Harf Aralıkları</SectionTitle>
      {/* Tab bar — sade segmented */}
      <div className="flex border-b border-border bg-card px-2 pt-2 text-xs">
        <button
          type="button"
          onClick={() => setTab('final')}
          className={cn(
            'rounded-t-md px-3 py-1.5 font-medium transition-colors',
            tab === 'final'
              ? 'border border-b-0 border-border bg-card text-[#003366] shadow-sm'
              : 'text-muted-foreground hover:text-foreground'
          )}
        >
          Final
        </button>
        <button
          type="button"
          onClick={() => setTab('makeup')}
          className={cn(
            'ml-1 rounded-t-md px-3 py-1.5 font-medium transition-colors',
            tab === 'makeup'
              ? 'border border-b-0 border-border bg-card text-[#003366] shadow-sm'
              : 'text-muted-foreground hover:text-foreground'
          )}
        >
          Bütünleme
        </button>
        <div className="flex-1 border-b border-border" />
      </div>

      {!data.hasGradingCriteria ? (
        <p className="px-5 py-10 text-center text-sm text-muted-foreground">
          Harf notu aralıkları henüz girilmediği için dağılım grafiği oluşturulmadı.
        </p>
      ) : (
        <table className="w-full border-collapse text-xs">
          <thead>
            <tr className="border-b border-border bg-muted/40 text-[10px] uppercase tracking-wide text-muted-foreground">
              <th className="px-3 py-2 text-left font-semibold">Harf</th>
              <th className="px-3 py-2 text-right font-semibold">Baş.</th>
              <th className="px-3 py-2 text-right font-semibold">Bit.</th>
              <th className="px-3 py-2 text-left font-semibold">Öğrenci Sayıları</th>
            </tr>
          </thead>
          <tbody>
            {activeRanges.map(range => (
              <tr
                key={range.grade}
                className={cn(
                  'border-b border-border/60 last:border-b-0 transition-colors hover:bg-muted/30',
                  range.grade === data.studentLetterGrade && 'bg-[#003366]/5'
                )}
              >
                <td className="px-3 py-1.5 font-mono font-semibold text-foreground">{range.grade}</td>
                <td className="px-3 py-1.5 text-right font-mono text-muted-foreground">{formatScore(range.minScore)}</td>
                <td className="px-3 py-1.5 text-right font-mono text-muted-foreground">{formatScore(range.maxScore)}</td>
                <td className="px-3 py-1.5">
                  {showDistribution ? (
                    <div className="flex items-center gap-2">
                      <span className="inline-block w-8 text-right font-mono text-foreground">{range.studentCount}</span>
                      <div
                        className="h-3 rounded-sm bg-emerald-500/80"
                        style={{ width: `${Math.max(4, (range.studentCount / maxGradeCount) * 120)}px` }}
                      />
                    </div>
                  ) : (
                    <span className="text-muted-foreground/70">
                      {tab === 'makeup' ? 'Bütünleme sonucu yok' : 'Harf notları atanmadı'}
                    </span>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </section>
  );
}

function ResultOverview({ data }: { data: StudentGradeStatisticsDto }) {
  return (
    <section className="bg-card">
      <SectionTitle>Sonuç</SectionTitle>
      <div>
        <InfoRow
          label="Sonuç Durumu"
          value={data.result.status}
          tone={data.isResultFinalized ? 'success' : 'danger'}
        />
        <InfoRow label="Değerlendirme Kapsamı" value={data.isCommonEvaluation ? 'Ortak Değerlendirme' : 'Şube Bazlı'} />
        <InfoRow label="Not Girişleri" value={getGradeEntryStatus(data)} />
        <InfoRow label="Harf Notu Kriterleri" value={data.hasGradingCriteria ? 'Girildi' : 'Bekleniyor'} />
        {data.result.statusDate && (
          <InfoRow label="Sonuç Durum Tarihi" value={formatDate(data.result.statusDate)} />
        )}
        {data.result.evaluationMethod && (
          <InfoRow label="Değerlendirme Şekli" value={data.result.evaluationMethod} />
        )}
        <InfoRow label="Dönem" value={data.termName} />
        <InfoRow label="Şube" value={String(data.sectionNumber)} />
        {data.summary.classAverage != null && (
          <InfoRow label="Sınıf Ortalaması" value={formatNullable(data.summary.classAverage)} mono />
        )}
        {shouldShowLetterGrade(data) && (
          <InfoRow label="Harf Notunuz" value={data.studentLetterGrade!} tone="primary" mono />
        )}
        {canShowWeightedAverage(data) && (
          <InfoRow label="Ağırlıklı Ortalamanız" value={formatNullable(data.studentWeightedAverage)} mono />
        )}
      </div>
    </section>
  );
}

function ComponentStatisticsBlock({ component }: { component: StudentComponentStatisticsDto }) {
  const enteredPct = component.totalStudentCount > 0
    ? formatScore((component.enteredStudentCount / component.totalStudentCount) * 100)
    : '0';
  const missingPct = component.totalStudentCount > 0
    ? formatScore((component.missingStudentCount / component.totalStudentCount) * 100)
    : '0';
  const absentPct = component.totalStudentCount > 0
    ? formatScore((component.absentByAttendanceCount / component.totalStudentCount) * 100)
    : '0';

  const ownDisplay = component.studentScore != null
    ? formatNullable(component.studentScore)
    : component.studentExamAttendance === 'Absent' ? 'G (Girmedi)'
    : component.studentExamAttendance === 'Excused' ? 'M (Mazeret)'
    : '—';

  return (
    <div className="bg-card">
      {/* Komponent başlığı — isim + ağırlık + ilan tarihi */}
      <div className="flex flex-wrap items-baseline justify-between gap-x-3 gap-y-1 border-b border-border/60 bg-[#003366]/[0.04] px-5 py-2">
        <div className="flex items-baseline gap-2">
          <span className="text-sm font-semibold text-[#003366]">{component.name}</span>
          <span className="font-mono text-xs text-muted-foreground">%{formatScore(component.weight)}</span>
        </div>
        {component.publishedAt && (
          <span className="text-[11px] text-muted-foreground">
            İlan: <span className="font-medium text-foreground">{new Date(component.publishedAt).toLocaleString('tr-TR')}</span>
          </span>
        )}
      </div>
      {/* İstatistik satırları */}
      <div>
        <InfoRow label="Sınav listesinde yer alan toplam öğrenci sayısı" value={String(component.totalStudentCount)} mono />
        <InfoRow label="Sınava giren öğrenci sayısı" value={`${component.enteredStudentCount} (%${enteredPct})`} mono />
        <InfoRow label="Sınava girmeyen öğrenci sayısı" value={`${component.missingStudentCount} (%${missingPct})`} mono />
        <InfoRow label="Sınavda Devamsızlıktan Kaldı seçilen öğrenci sayısı" value={`${component.absentByAttendanceCount} (%${absentPct})`} mono />
        <InfoRow label="Sınava giren öğrencilerin not ortalaması" value={formatNullable(component.averageScore)} mono highlighted />
        <InfoRow label="Sizin notunuz" value={ownDisplay} mono tone="primary" />
      </div>
    </div>
  );
}

/** Bölüm başlığı: ince üst başlık, kurumsal mavi tonu. */
function SectionTitle({ children }: { children: React.ReactNode }) {
  return (
    <div className="border-b border-border bg-muted/30 px-5 py-2 text-xs font-semibold uppercase tracking-wide text-[#003366]">
      {children}
    </div>
  );
}

type InfoRowTone = 'primary' | 'success' | 'danger';

const INFO_ROW_TONE_CLASS: Record<InfoRowTone, string> = {
  primary: 'text-[#003366] font-semibold',
  success: 'text-emerald-600 font-semibold',
  danger: 'text-red-600 font-semibold',
};

/** Label : value satırı — sade, ince border, hover ile vurgulanır. */
function InfoRow({
  label,
  value,
  tone,
  mono = false,
  highlighted = false,
}: {
  label: string;
  value: string;
  tone?: InfoRowTone;
  mono?: boolean;
  highlighted?: boolean;
}) {
  return (
    <div className={cn(
      'grid grid-cols-1 border-b border-border/60 transition-colors last:border-b-0 sm:grid-cols-[minmax(200px,360px)_1fr]',
      highlighted ? 'bg-[#003366]/[0.06]' : 'hover:bg-muted/30'
    )}>
      <div className="px-5 py-1.5 text-xs text-muted-foreground sm:text-sm">{label}</div>
      <div className={cn(
        'px-5 py-1.5 text-sm sm:text-right',
        mono && 'font-mono',
        tone ? INFO_ROW_TONE_CLASS[tone] : 'text-foreground'
      )}>
        {value}
      </div>
    </div>
  );
}

function formatDate(value: string | null) {
  if (!value) return '-';
  return new Date(value).toLocaleDateString('tr-TR');
}

function shouldShowLetterGrade(statistics: StudentGradeStatisticsDto | null | undefined) {
  return isGradeFinalized(statistics) && Boolean(statistics?.studentLetterGrade);
}

function isGradeFinalized(statistics: StudentGradeStatisticsDto | null | undefined) {
  if (!statistics) return false;
  if (typeof statistics.isResultFinalized === 'boolean') {
    return statistics.isResultFinalized;
  }

  const status = statistics.result.status.toLocaleLowerCase('tr-TR');
  if (status.includes('sonuçlandırılmam') || status.includes('sonuclandirilmam'))
    return false;

  return status.includes('sonuçlandırıl') || status.includes('sonuclandiril') || Boolean(statistics.studentLetterGrade);
}

function canShowWeightedAverage(statistics: StudentGradeStatisticsDto | null | undefined) {
  return Boolean(statistics?.hasAllComponentScores && statistics.studentWeightedAverage != null);
}

function formatWeightedAverage(statistics: StudentGradeStatisticsDto | null | undefined) {
  return canShowWeightedAverage(statistics)
    ? formatNullable(statistics!.studentWeightedAverage!)
    : '-';
}

function getGradeEntryStatus(statistics: StudentGradeStatisticsDto) {
  if (statistics.hasAllComponentScores) return 'Tüm notlar girildi';
  if (statistics.hasAnyComponentScore) return 'Kısmi not girişi yapıldı';
  return 'Not girişi yapılmamış';
}

function getCourseResult(grade: string): { label: string; variant: 'success' | 'destructive' | 'warning' | 'outline' | 'secondary' } {
  if (['AA', 'BA', 'BB', 'CB', 'CC', 'DC', 'DD', 'S'].includes(grade))
    return { label: 'Geçti', variant: 'success' };

  if (['FF', 'NA', 'U'].includes(grade))
    return { label: 'Kaldı', variant: 'destructive' };

  if (grade === 'W')
    return { label: 'Çekildi', variant: 'outline' };

  if (grade === 'I')
    return { label: 'Eksik', variant: 'warning' };

  return { label: '-', variant: 'secondary' };
}

function formatNullable(value: number | null) {
  return value == null ? '-' : formatScore(value);
}

function formatScore(value: number) {
  return Number.isInteger(value)
    ? String(value)
    : value.toFixed(2).replace('.', ',');
}
