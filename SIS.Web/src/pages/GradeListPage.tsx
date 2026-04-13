import { useEffect, useMemo, useState } from 'react';
import { useQueries } from '@tanstack/react-query';
import { AlertCircle, BarChart3, BookOpen, CalendarDays, Printer, Search } from 'lucide-react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
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
import { getStudentGradeStatistics } from '../api/student.api';
import { useCourseHistory } from '../hooks/useCourseHistory';
import { useStudentGradeStatistics } from '../hooks/useStudentGradeStatistics';
import { QUERY_KEYS } from '../lib/constants';
import type { CourseHistoryItemDto } from '../types/registration';
import type { StudentComponentStatisticsDto, StudentGradeStatisticsDto } from '../types/student';

type GradeListRow = CourseHistoryItemDto & {
  termId: number;
  termName: string;
  year: number;
  semester: string;
};

const GRADE_BADGE_CLASS: Record<string, string> = {
  AA: 'bg-emerald-600 text-white',
  BA: 'bg-emerald-600 text-white',
  BB: 'bg-emerald-500 text-white',
  CB: 'bg-teal-600 text-white',
  CC: 'bg-teal-600 text-white',
  DC: 'bg-amber-500 text-white',
  DD: 'bg-amber-500 text-white',
  FF: 'bg-red-600 text-white',
  NA: 'bg-red-600 text-white',
  W: 'bg-slate-200 text-slate-700',
  I: 'bg-slate-200 text-slate-700',
  S: 'bg-emerald-600 text-white',
  U: 'bg-red-600 text-white',
};

export default function GradeListPage() {
  const { data: history, isLoading, error } = useCourseHistory();
  const [selectedTermId, setSelectedTermId] = useState<number | null>(null);
  const [searchTerm, setSearchTerm] = useState('');
  const [selectedRegistrationId, setSelectedRegistrationId] = useState<number | null>(null);

  const terms = useMemo(() => history ?? [], [history]);

  useEffect(() => {
    if (terms.length === 0) {
      setSelectedTermId(null);
      return;
    }

    if (!selectedTermId || !terms.some(term => term.termId === selectedTermId)) {
      setSelectedTermId(terms[0].termId);
    }
  }, [selectedTermId, terms]);

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

  const rowsWithStatistics = rows.map((row, index) => ({
    ...row,
    statistics: gradeStatisticQueries[index]?.data ?? null,
    statisticsLoading: gradeStatisticQueries[index]?.isLoading ?? false,
  }));

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
        <AlertDescription>Not listesi yüklenirken hata oluştu.</AlertDescription>
      </Alert>
    );
  }

  return (
    <div className="space-y-5">
      <Card className="overflow-hidden border border-[#003366]/20 shadow-sm">
        <CardContent className="p-0">
          <div className="bg-[#003366] px-5 py-4 text-white">
            <p className="text-xs font-semibold uppercase tracking-wider text-white/60">Ders ve Dönem İşlemleri</p>
            <h1 className="font-heading text-xl font-extrabold">Not Listesi</h1>
          </div>
          <div className="grid grid-cols-2 gap-px bg-border md:grid-cols-4">
            <SummaryCell label="Dönem" value={selectedTerm?.termName ?? '-'} />
            <SummaryCell label="Ders" value={String(rows.length)} />
            <SummaryCell label="Notlanan" value={String(rowsWithStatistics.filter(row => shouldShowLetterGrade(row.statistics)).length)} />
            <SummaryCell label="Tekrar" value={String(rows.filter(row => row.isRepeated).length)} />
          </div>
        </CardContent>
      </Card>

      <Card className="border-border shadow-sm">
        <CardContent className="p-4">
          <div className="grid gap-3 md:grid-cols-[minmax(240px,340px)_1fr]">
            <div className="relative">
              <select
                value={selectedTerm?.termId ?? ''}
                onChange={event => setSelectedTermId(Number(event.target.value))}
                className="h-10 w-full appearance-none rounded-md border border-border bg-card px-3 pr-9 text-sm font-semibold text-[#003366] shadow-sm outline-none transition-colors hover:border-[#003366]/40 focus:border-[#003366] focus:ring-2 focus:ring-[#003366]/10"
              >
                {terms.map(term => (
                  <option key={term.termId} value={term.termId}>{term.termName}</option>
                ))}
              </select>
              <CalendarDays className="pointer-events-none absolute right-3 top-1/2 h-4 w-4 -translate-y-1/2 text-[#003366]" />
            </div>

            <div className="relative">
              <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
              <Input
                value={searchTerm}
                onChange={event => setSearchTerm(event.target.value)}
                placeholder="Ders kodu, ders adı veya öğretim elemanı ile ara"
                className="pl-9"
              />
            </div>
          </div>
        </CardContent>
      </Card>

      <Card className="overflow-hidden border border-[#003366]/20 shadow-md">
        <CardContent className="p-0">
          <div className="overflow-x-auto">
            <Table>
              <TableHeader>
                <TableRow className="border-b-0 bg-[#003366] hover:bg-[#003366]">
                  <TableHead className="w-16 pl-5 text-center text-white">Şb</TableHead>
                  <TableHead className="w-32 text-white">Ders Kodu</TableHead>
                  <TableHead className="min-w-[260px] text-white">Ders Adı</TableHead>
                  <TableHead className="min-w-48 text-white">Öğretim Elemanı</TableHead>
                  <TableHead className="min-w-36 text-center text-white">Sonuç Durumu</TableHead>
                  <TableHead className="min-w-[260px] text-white">Sınav Notları</TableHead>
                  <TableHead className="w-24 text-center text-white">Ort</TableHead>
                  <TableHead className="text-center text-white">AKTS</TableHead>
                  <TableHead className="text-center text-white">Not</TableHead>
                  <TableHead className="min-w-28 text-center text-white">Durumu</TableHead>
                  <TableHead className="pr-5 text-right text-white">İşlem</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {filteredRows.map((row, index) => (
                  <TableRow key={row.registrationId} className={index % 2 === 1 ? 'bg-muted/10' : undefined}>
                    <TableCell className="pl-5 text-center font-semibold">{row.sectionNumber}</TableCell>
                    <TableCell className="font-mono text-sm font-bold text-[#003366]">
                      {row.courseCode}
                    </TableCell>
                    <TableCell className="font-medium text-foreground">
                      {row.courseName}
                      {row.isRepeated && <span className="ml-2 text-xs text-amber-600">(Tekrar)</span>}
                    </TableCell>
                    <TableCell className="text-sm text-muted-foreground">{row.instructorName || '-'}</TableCell>
                    <TableCell className="text-center">
                      <ResultStatusBadge statistics={row.statistics} isLoading={row.statisticsLoading} />
                    </TableCell>
                    <TableCell>
                      <ExamScores statistics={row.statistics} isLoading={row.statisticsLoading} />
                    </TableCell>
                    <TableCell className="text-center font-mono text-sm">
                      {row.statisticsLoading ? '...' : formatWeightedAverage(row.statistics)}
                    </TableCell>
                    <TableCell className="text-center">
                      <Badge variant="secondary" className="font-mono">{row.ects}</Badge>
                    </TableCell>
                    <TableCell className="text-center">
                      {shouldShowLetterGrade(row.statistics) ? (
                        <span className={cn('inline-flex min-w-10 justify-center rounded-md px-2 py-0.5 text-xs font-bold', GRADE_BADGE_CLASS[row.statistics!.studentLetterGrade!] ?? 'bg-slate-200 text-slate-700')}>
                          {row.statistics!.studentLetterGrade}
                        </span>
                      ) : (
                        <span className="text-xs text-muted-foreground">-</span>
                      )}
                    </TableCell>
                    <TableCell className="text-center">
                      <CourseResultBadge statistics={row.statistics} isLoading={row.statisticsLoading} />
                    </TableCell>
                    <TableCell className="pr-5 text-right">
                      <Button
                        type="button"
                        variant="outline"
                        size="sm"
                        onClick={() => setSelectedRegistrationId(row.registrationId)}
                        className="h-8 gap-1.5"
                      >
                        <BarChart3 className="h-3.5 w-3.5" />
                        İstatistik
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
        </CardContent>
      </Card>

      <Dialog open={selectedRegistrationId !== null} onOpenChange={open => { if (!open) setSelectedRegistrationId(null); }}>
        <DialogContent className="max-h-[92vh] max-w-[min(1280px,calc(100vw-2rem))] overflow-hidden p-0">
          <DialogHeader className="sr-only">
            <DialogTitle>Sınav İstatistikleri</DialogTitle>
          </DialogHeader>
          {selectedRegistrationId !== null && (
            <GradeStatisticsPopup
              registrationId={selectedRegistrationId}
              fallbackTitle={selectedRow ? `${selectedRow.courseCode} - ${selectedRow.courseName}` : undefined}
            />
          )}
        </DialogContent>
      </Dialog>
    </div>
  );
}

function ResultStatusBadge({
  statistics,
  isLoading,
}: {
  statistics: StudentGradeStatisticsDto | null | undefined;
  isLoading: boolean;
}) {
  if (isLoading) return <Badge variant="outline">Yükleniyor</Badge>;

  return isGradeFinalized(statistics)
    ? <Badge variant="success">Sonuçlandırıldı</Badge>
    : <Badge variant="warning">Sonuçlandırılmadı</Badge>;
}

function ExamScores({
  statistics,
  isLoading,
}: {
  statistics: StudentGradeStatisticsDto | null | undefined;
  isLoading: boolean;
}) {
  if (isLoading) {
    return <span className="text-xs text-muted-foreground">Yükleniyor</span>;
  }

  const scores = statistics?.components
    .filter(component => component.studentScore != null) ?? [];

  if (scores.length === 0) {
    return (
      <span className="inline-flex rounded-md border border-amber-300 bg-amber-50 px-2 py-0.5 text-xs font-semibold text-amber-700">
        Not girişi yapılmamış
      </span>
    );
  }

  return (
    <div className="flex flex-wrap gap-1.5">
      {scores.map(component => (
        <span
          key={component.componentId}
          className="inline-flex rounded-md border border-slate-200 bg-slate-50 px-2 py-0.5 text-xs text-slate-700"
        >
          {component.name}: <strong className="ml-1 text-slate-900">{formatScore(component.studentScore!)}</strong>
        </span>
      ))}
    </div>
  );
}

function CourseResultBadge({
  statistics,
  isLoading,
}: {
  statistics: StudentGradeStatisticsDto | null | undefined;
  isLoading: boolean;
}) {
  if (isLoading) return <span className="text-xs text-muted-foreground">...</span>;
  if (!shouldShowLetterGrade(statistics)) return <span className="text-xs text-muted-foreground">-</span>;

  const grade = statistics!.studentLetterGrade!;
  const result = getCourseResult(grade);
  return <Badge variant={result.variant}>{result.label}</Badge>;
}

function SummaryCell({ label, value }: { label: string; value: string }) {
  return (
    <div className="bg-card px-5 py-4">
      <p className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">{label}</p>
      <p className="mt-1 font-heading text-xl font-extrabold text-foreground">{value}</p>
    </div>
  );
}

function GradeStatisticsPopup({ registrationId, fallbackTitle }: { registrationId: number; fallbackTitle?: string }) {
  const [commonEvaluation, setCommonEvaluation] = useState(false);
  const { data, isLoading, isError } = useStudentGradeStatistics(registrationId, commonEvaluation);

  useEffect(() => {
    setCommonEvaluation(false);
  }, [registrationId]);

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
          <AlertDescription>Sınav istatistikleri alınamadı.</AlertDescription>
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
    />
  );
}

function ExamStatisticsPanel({
  data,
  fallbackTitle,
  commonEvaluation,
  onCommonEvaluationChange,
}: {
  data: StudentGradeStatisticsDto;
  fallbackTitle?: string;
  commonEvaluation: boolean;
  onCommonEvaluationChange: (value: boolean) => void;
}) {
  const visibleComponents = data.components.filter(component =>
    component.enteredStudentCount > 0 || component.studentScore != null
  );

  return (
    <div className="max-h-[92vh] overflow-auto bg-[#f3f3f3] text-[13px] text-slate-900">
      <div className="sticky top-0 z-10 flex items-center justify-between border-b border-slate-400 bg-[#e9e9e9] px-2 py-1.5">
        <div className="font-medium">Sınav İstatistikleri</div>
        <button
          type="button"
          onClick={() => window.print()}
          className="inline-flex h-7 min-w-56 items-center justify-center gap-1 rounded border border-slate-300 bg-white px-3 text-xs text-slate-600 hover:bg-slate-50"
        >
          <Printer className="h-3.5 w-3.5 text-cyan-500" />
          Yazdır
        </button>
      </div>

      <div className="border-b border-slate-300 bg-white">
        <InfoRow label="Fakülte Program" value={data.facultyProgram || '-'} />
        <InfoRow label="Öğretim Elemanı" value={data.instructorName || '-'} />
        <InfoRow label="Ders Kodu" value={data.courseCode} />
        <InfoRow label="Ders Adı" value={data.courseName || fallbackTitle || '-'} />
      </div>

      <div className="grid gap-px bg-slate-300 lg:grid-cols-[1fr_1fr]">
        <GradeRangeDistribution data={data} />
        <ResultOverview data={data} />
      </div>

      <section className="mt-4 border border-slate-300 bg-white">
        <div className="flex items-center gap-2 border-b border-slate-300 bg-[#f7f7f7] px-2 py-1">
          <span>Sınav Öğrenci Sayıları</span>
          <label className="inline-flex items-center gap-1 text-xs text-slate-700">
            <input
              type="checkbox"
              checked={commonEvaluation}
              onChange={event => onCommonEvaluationChange(event.target.checked)}
              className="h-3.5 w-3.5"
            />
            Ortak Değerlendirme Sonucu
          </label>
        </div>

        {visibleComponents.length === 0 ? (
          <p className="px-2 py-6 text-center text-sm text-slate-500">Bu ders için henüz not girişi yapılmamış.</p>
        ) : (
          <div className="divide-y divide-slate-200">
            {visibleComponents.map(component => (
              <ComponentStatisticsBlock key={component.componentId} component={component} />
            ))}
          </div>
        )}
      </section>
    </div>
  );
}

function GradeRangeDistribution({ data }: { data: StudentGradeStatisticsDto }) {
  if (!data.hasGradingCriteria) {
    return (
      <section className="bg-white">
        <SectionTitle>Harf Aralıkları Dağılımı</SectionTitle>
        <p className="px-3 py-8 text-center text-sm text-slate-500">
          Harf notu aralıkları henüz girilmediği için dağılım grafiği oluşturulmadı.
        </p>
      </section>
    );
  }

  const showDistribution = data.isResultFinalized && data.summary.gradedStudentCount > 0;
  const maxGradeCount = Math.max(1, ...data.gradeRanges.map(range => range.studentCount));

  return (
    <section className="bg-white">
      <SectionTitle>Harf Aralıkları Dağılımı</SectionTitle>
      <table className="w-full border-collapse text-xs">
        <thead>
          <tr className="bg-[#666] text-white">
            <th className="px-2 py-1 text-left font-medium">Harf</th>
            <th className="px-2 py-1 text-left font-medium">Baş.</th>
            <th className="px-2 py-1 text-left font-medium">Bit.</th>
            <th className="px-2 py-1 text-left font-medium">Öğrenci Sayıları</th>
          </tr>
        </thead>
        <tbody>
          {data.gradeRanges.map(range => (
            <tr key={range.grade} className={cn('border-b border-slate-200', range.grade === data.studentLetterGrade && 'bg-blue-100')}>
              <td className="px-2 py-1 font-mono">{range.grade}</td>
              <td className="px-2 py-1">{formatScore(range.minScore)}</td>
              <td className="px-2 py-1">{formatScore(range.maxScore)}</td>
              <td className="px-2 py-1">
                {showDistribution ? (
                  <div className="flex items-center gap-2">
                    <span className="w-8 text-right">{range.studentCount}</span>
                    <div className="h-4 bg-green-700" style={{ width: `${Math.max(4, (range.studentCount / maxGradeCount) * 120)}px` }} />
                  </div>
                ) : (
                  <span className="text-slate-500">Harf notları atanmadı</span>
                )}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </section>
  );
}

function ResultOverview({ data }: { data: StudentGradeStatisticsDto }) {
  return (
    <section className="bg-white">
      <SectionTitle>Sonuç</SectionTitle>
      <div className="border-b border-slate-200">
        <InfoRow label="Sonuç Durumu" value={data.result.status} strongValue tone={data.isResultFinalized ? 'blue' : 'red'} />
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
          <InfoRow label="Sınıf Ortalaması" value={formatNullable(data.summary.classAverage)} />
        )}
        {shouldShowLetterGrade(data) && (
          <InfoRow label="Harf Notunuz" value={data.studentLetterGrade!} strongValue tone="blue" />
        )}
        {canShowWeightedAverage(data) && (
          <InfoRow label="Ağırlıklı Ortalamanız" value={formatNullable(data.studentWeightedAverage)} />
        )}
      </div>
    </section>
  );
}

function ComponentStatisticsBlock({ component }: { component: StudentComponentStatisticsDto }) {
  return (
    <div>
      <div className="bg-white px-2 py-2 font-semibold text-blue-700">
        {component.name}(%{formatScore(component.weight)})
      </div>
      <InfoRow label="Sınav listesinde yer alan toplam öğrenci sayısı" value={String(component.totalStudentCount)} alignRight />
      <InfoRow label="Sınava giren öğrenci sayısı" value={`${component.enteredStudentCount} (%${component.totalStudentCount > 0 ? formatScore((component.enteredStudentCount / component.totalStudentCount) * 100) : '0'})`} alignRight />
      <InfoRow label="Sınava girmeyen öğrenci sayısı" value={`${component.missingStudentCount} (%${component.totalStudentCount > 0 ? formatScore((component.missingStudentCount / component.totalStudentCount) * 100) : '0'})`} alignRight />
      <InfoRow label="Sınava giren öğrencilerin not ortalaması" value={formatNullable(component.averageScore)} alignRight highlighted />
      <InfoRow label="Sizin notunuz" value={formatNullable(component.studentScore)} alignRight />
    </div>
  );
}

function SectionTitle({ children }: { children: React.ReactNode }) {
  return <div className="border-b border-slate-300 bg-[#f7f7f7] px-2 py-1 font-medium">{children}</div>;
}

function InfoRow({
  label,
  value,
  strongValue = false,
  tone,
  alignRight = false,
  highlighted = false,
}: {
  label: string;
  value: string;
  strongValue?: boolean;
  tone?: 'red' | 'blue';
  alignRight?: boolean;
  highlighted?: boolean;
}) {
  return (
    <div className={cn('grid grid-cols-[minmax(180px,310px)_1fr] border-b border-slate-200 last:border-b-0', highlighted && 'bg-blue-100')}>
      <div className="px-2 py-1">{label}</div>
      <div className={cn(
        'px-2 py-1',
        alignRight && 'text-right',
        strongValue && 'font-semibold',
        tone === 'red' && 'text-red-600',
        tone === 'blue' && 'text-blue-700'
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
