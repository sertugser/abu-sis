import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { AlertCircle, BarChart3, TrendingUp } from 'lucide-react';
import { useStudentInfo } from '../hooks/useStudentInfo';
import { useTermGpaHistory } from '../hooks/useTermGpaHistory';
import type { StudentTermGpaDto } from '../types/student';

const CHART_HEIGHT = 300;
const CHART_PADDING = { top: 20, right: 20, bottom: 80, left: 48 };
const GRID_VALUES = [0, 0.5, 1.0, 1.5, 2.0, 2.5, 3.0, 3.5, 4.0];
const REPORT_COLORS = {
  sgpa: {
    base: '#27457f',
    latest: '#1b315d',
    surface: 'rgba(39,69,127,0.05)',
    border: 'rgba(39,69,127,0.10)',
  },
  cgpa: {
    base: '#1a9b84',
    latest: '#137564',
    surface: 'rgba(26,155,132,0.05)',
    border: 'rgba(26,155,132,0.10)',
  },
};

export default function TermGpaPage() {
  const studentQuery = useStudentInfo();
  const historyQuery = useTermGpaHistory();

  if (studentQuery.isLoading || historyQuery.isLoading) {
    return (
      <div className="space-y-6">
        <div className="grid gap-4 md:grid-cols-4">
          {[1, 2, 3, 4].map(i => <Skeleton key={i} className="h-24 rounded-xl" />)}
        </div>
        <Skeleton className="h-[380px] rounded-xl" />
        <Skeleton className="h-[380px] rounded-xl" />
        <Skeleton className="h-[280px] rounded-xl" />
      </div>
    );
  }

  if (studentQuery.isError || historyQuery.isError || !studentQuery.data) {
    return (
      <Alert variant="destructive">
        <AlertCircle className="h-4 w-4" />
        <AlertTitle>Dönem ortalamaları yüklenemedi</AlertTitle>
        <AlertDescription>
          ANO ve AGNO geçmişi alınırken bir hata oluştu. Lütfen tekrar deneyin.
        </AlertDescription>
      </Alert>
    );
  }

  const student = studentQuery.data;
  const history = historyQuery.data ?? [];
  const latestTerm = history.length > 0 ? history[history.length - 1] : null;
  const totalCourses = history.reduce((sum, term) => sum + term.courseCount, 0);
  const totalEcts = history.reduce((sum, term) => sum + term.totalEcts, 0);
  const bestSgpa = history.length > 0 ? Math.max(...history.map(term => term.sgpa)) : 0;

  if (history.length === 0) {
    return (
      <Card>
        <CardContent className="py-20 text-center">
          <BarChart3 className="mx-auto mb-4 h-12 w-12 text-muted-foreground/40" />
          <h3 className="font-heading text-lg font-semibold text-foreground">Henüz dönem verisi yok</h3>
          <p className="mt-1 text-muted-foreground">
            {student.firstName} {student.lastName} için harf notu oluşmuş dönem verisi bulunmuyor.
          </p>
        </CardContent>
      </Card>
    );
  }

  return (
    <div className="space-y-6">
      {/* Summary banner */}
      <Card className="overflow-hidden border-0 shadow-[0_20px_50px_rgba(15,23,42,0.08)]">
        <CardContent className="relative p-0">
          <div className="absolute inset-0 bg-[radial-gradient(circle_at_top_right,rgba(20,184,166,0.14),transparent_24%),linear-gradient(135deg,rgba(19,35,63,1)_0%,rgba(24,43,75,0.98)_58%,rgba(17,32,58,1)_100%)]" />
          <div className="relative px-8 py-6">
            <div className="grid gap-0 overflow-hidden rounded-2xl border border-white/10 bg-white/[0.06] backdrop-blur-sm lg:grid-cols-4">
              <SummaryStripItem label="Güncel AGNO" value={student.cgpa.toFixed(2)} note="Genel not ortalaması" emphasize />
              <SummaryStripItem label="Son Dönem ANO" value={latestTerm ? latestTerm.sgpa.toFixed(2) : '0.00'} note={latestTerm?.termName ?? '—'} />
              <SummaryStripItem label="Toplam AKTS" value={String(totalEcts)} note={`${totalCourses} ders işlendi`} />
              <SummaryStripItem label="İzlenen Dönem" value={String(history.length)} note={`En yüksek ANO: ${bestSgpa.toFixed(2)}`} />
            </div>
          </div>
        </CardContent>
      </Card>

      {/* ANO chart */}
      <ReportChartSection
        title="ANO Grafiği (Dönem Not Ortalaması)"
        legendLabel="ANO"
        legendTone="sgpa"
        history={history}
        metric="sgpa"
      />

      {/* AGNO chart */}
      <ReportChartSection
        title="AGNO Grafiği (Genel Not Ortalaması)"
        legendLabel="AGNO"
        legendTone="cgpa"
        history={history}
        metric="cgpa"
      />

      {/* Detail table */}
      <Card className="overflow-hidden">
        <CardHeader className="border-b border-border/70 bg-gradient-to-r from-primary/[0.04] to-transparent">
          <CardTitle>Dönem Bazlı Özet</CardTitle>
        </CardHeader>
        <CardContent className="p-0">
          <div className="overflow-x-auto">
            <Table>
              <TableHeader>
                <TableRow className="bg-muted/40 hover:bg-muted/40">
                  <TableHead className="pl-6">Dönem</TableHead>
                  <TableHead className="text-center">Ders Sayısı</TableHead>
                  <TableHead className="text-center">Toplam Kredi</TableHead>
                  <TableHead className="text-center">Toplam AKTS</TableHead>
                  <TableHead className="text-center">ANO</TableHead>
                  <TableHead className="text-center pr-6">AGNO</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {history.map((term, index) => (
                  <TableRow key={term.termId} className={index % 2 === 1 ? 'bg-muted/10' : undefined}>
                    <TableCell className="pl-6">
                      <div className="space-y-1">
                        <p className="font-medium text-foreground">{term.termName}</p>
                        <p className="text-xs text-muted-foreground">{formatSemester(term.semester)} dönemi</p>
                      </div>
                    </TableCell>
                    <TableCell className="text-center">{term.courseCount}</TableCell>
                    <TableCell className="text-center">{term.totalCredits}</TableCell>
                    <TableCell className="text-center">{term.totalEcts}</TableCell>
                    <TableCell className="text-center">
                      <Badge variant="outline" className="font-mono">
                        {term.sgpa.toFixed(2)}
                      </Badge>
                    </TableCell>
                    <TableCell className="pr-6 text-center">
                      <Badge variant="secondary" className="font-mono">
                        {term.cgpa.toFixed(2)}
                      </Badge>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}

function SummaryStripItem({
  label,
  value,
  note,
  emphasize = false,
}: {
  label: string;
  value: string;
  note: string;
  emphasize?: boolean;
}) {
  return (
    <div className="border-b border-white/8 px-5 py-5 lg:border-b-0 lg:border-r lg:border-white/8 last:border-r-0">
      <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-slate-300">{label}</p>
      <p className={`mt-3 font-heading text-4xl font-bold ${emphasize ? 'text-white' : 'text-slate-50'}`}>{value}</p>
      <p className="mt-2 text-sm text-slate-300">{note}</p>
    </div>
  );
}

function ReportChartSection({
  title,
  legendLabel,
  legendTone,
  history,
  metric,
}: {
  title: string;
  legendLabel: string;
  legendTone: 'sgpa' | 'cgpa';
  history: StudentTermGpaDto[];
  metric: 'sgpa' | 'cgpa';
}) {
  const latestValue = history[history.length - 1]?.[metric] ?? 0;

  return (
    <Card className="overflow-hidden">
      <CardHeader className="border-b border-border/70 bg-gradient-to-r from-muted/30 to-transparent">
        <div className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
          <CardTitle>{title}</CardTitle>
          <div className="flex flex-wrap gap-2">
            <LegendBadge label={legendLabel} tone={legendTone} />
            <Badge variant="secondary" className="gap-1.5">
              <TrendingUp className="h-3.5 w-3.5" />
              Son değer: {latestValue.toFixed(2)}
            </Badge>
          </div>
        </div>
      </CardHeader>
      <CardContent className="p-6">
        <div
          className="rounded-xl border px-4 py-4 md:px-6 md:py-5"
          style={{
            backgroundColor: REPORT_COLORS[legendTone].surface,
            borderColor: REPORT_COLORS[legendTone].border,
          }}
        >
          <TermGpaBarChart history={history} metric={metric} ariaLabel={`${title} sütun grafiği`} />
        </div>
      </CardContent>
    </Card>
  );
}

function LegendBadge({ label, tone }: { label: string; tone: 'sgpa' | 'cgpa' }) {
  const color = REPORT_COLORS[tone].base;

  return (
    <span
      className="inline-flex items-center gap-2 rounded-full border px-3 py-1 text-xs font-medium"
      style={{
        color,
        borderColor: REPORT_COLORS[tone].border,
        backgroundColor: REPORT_COLORS[tone].surface,
      }}
    >
      <span className="h-2.5 w-2.5 rounded-full" style={{ backgroundColor: color }} />
      {label}
    </span>
  );
}

function TermGpaBarChart({
  history,
  metric,
  ariaLabel,
}: {
  history: StudentTermGpaDto[];
  metric: 'sgpa' | 'cgpa';
  ariaLabel: string;
}) {
  const barCount = Math.max(history.length, 1);
  const innerWidth = 900 - CHART_PADDING.left - CHART_PADDING.right;
  const innerHeight = CHART_HEIGHT - CHART_PADDING.top - CHART_PADDING.bottom;
  const columnWidth = innerWidth / barCount;
  const barWidth = Math.min(64, Math.max(32, columnWidth * 0.55));
  const palette = REPORT_COLORS[metric];
  const labelY = CHART_HEIGHT - CHART_PADDING.bottom + 16;

  const getY = (val: number) => CHART_PADDING.top + ((4 - val) / 4) * innerHeight;

  return (
    <svg
      viewBox={`0 0 900 ${CHART_HEIGHT}`}
      className="w-full"
      role="img"
      aria-label={ariaLabel}
    >
      {/* Grid lines + Y axis labels */}
      {GRID_VALUES.map(value => {
        const y = getY(value);
        return (
          <g key={value}>
            <line x1={CHART_PADDING.left} x2={900 - CHART_PADDING.right} y1={y} y2={y} stroke="rgba(148,163,184,0.18)" />
            <text x={CHART_PADDING.left - 8} y={y + 4} textAnchor="end" fontSize="11" fill="#94a3b8">{value.toFixed(1)}</text>
          </g>
        );
      })}

      {/* Bars */}
      {history.map((term, index) => {
        const value = metric === 'sgpa' ? term.sgpa : term.cgpa;
        const cx = CHART_PADDING.left + index * columnWidth + columnWidth / 2;
        const x = cx - barWidth / 2;
        const y = getY(value);
        const barHeight = innerHeight - ((4 - value) / 4) * innerHeight;
        const isLast = index === history.length - 1;

        return (
          <g key={term.termId}>
            <rect
              x={x} y={y}
              width={barWidth}
              height={Math.max(barHeight, 0)}
              rx={5}
              fill={isLast ? palette.latest : palette.base}
              fillOpacity={isLast ? 0.96 : 0.78}
            />
            {barHeight > 28 && (
              <text x={cx} y={y + barHeight / 2 + 5} textAnchor="middle" fontSize="13" fontWeight="700" fill="#fff">
                {value.toFixed(2)}
              </text>
            )}
            {/* Term label — two lines: year + semester */}
            <text x={cx} y={labelY} textAnchor="middle" fontSize="11" fontWeight="600" fill="#475569">
              {String(term.year).slice(-2)}-{String(term.year + 1).slice(-2)}
            </text>
            <text x={cx} y={labelY + 14} textAnchor="middle" fontSize="10" fill="#94a3b8">
              {term.semester === 'Fall' ? 'Güz' : term.semester === 'Spring' ? 'Bahar' : 'Yaz'}
            </text>
          </g>
        );
      })}
    </svg>
  );
}

function getTermAxisLabel(term: StudentTermGpaDto) {
  const firstYear = String(term.year).slice(-2);
  const secondYear = String(term.year + 1).slice(-2);
  const semesterLabel =
    term.semester === 'Fall' ? 'Güz' : term.semester === 'Spring' ? 'Bahar' : 'Yaz';

  return `${firstYear}-${secondYear} ${semesterLabel}`;
}

function formatSemester(semester: string) {
  if (semester === 'Fall') return 'Güz';
  if (semester === 'Spring') return 'Bahar';
  if (semester === 'Summer') return 'Yaz';
  return semester;
}
