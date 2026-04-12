import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent } from '@/components/ui/card';
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
import { AlertCircle, BarChart3, TrendingUp } from 'lucide-react';
import { useStudentInfo } from '../hooks/useStudentInfo';
import { useStudentTranscript } from '../hooks/useStudentTranscript';
import { useTermGpaHistory } from '../hooks/useTermGpaHistory';
import type { StudentTermGpaDto } from '../types/student';

const CHART_HEIGHT = 260;
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
  const transcriptQuery = useStudentTranscript();
  const historyQuery = useTermGpaHistory();

  if (studentQuery.isLoading || transcriptQuery.isLoading || historyQuery.isLoading) {
    return (
      <div className="space-y-5">
        <Skeleton className="h-[340px] rounded-xl" />
        <Skeleton className="h-[360px] rounded-xl" />
        <Skeleton className="h-[360px] rounded-xl" />
      </div>
    );
  }

  if (studentQuery.isError || transcriptQuery.isError || historyQuery.isError || !studentQuery.data || !transcriptQuery.data) {
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
  const transcript = transcriptQuery.data;
  const history = historyQuery.data ?? [];

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
    <div className="space-y-5">
      <Card className="overflow-hidden border border-slate-300 shadow-sm">
        <div className="border-b bg-white px-4 py-3">
          <h3 className="text-[18px] font-medium text-slate-700">
            Dönem Ortalamaları
          </h3>
        </div>
        <CardContent className="p-0">
          <div className="overflow-x-auto">
            <Table>
              <TableHeader>
                <TableRow className="border-b-0 bg-[#003366] hover:bg-[#003366]">
                  <TableHead className="h-10 pl-2 text-[15px] font-semibold text-white">Dönem Adı</TableHead>
                  <TableHead className="h-10 text-center text-[15px] font-semibold text-white">Aldığı Ders Sayısı</TableHead>
                  <TableHead className="h-10 text-center text-[15px] font-semibold text-white">Toplam Kredi</TableHead>
                  <TableHead className="h-10 text-center text-[15px] font-semibold text-white">Toplam AKTS</TableHead>
                  <TableHead className="h-10 text-center text-[15px] font-semibold text-white">Dönem Ortalaması</TableHead>
                  <TableHead className="h-10 pr-2 text-center text-[15px] font-semibold text-white">Genel Not Ortalaması</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {history.map(term => (
                  <TableRow
                    key={term.termId}
                    className={cn('border-b border-slate-200 transition-colors hover:bg-[#dce9ff]')}
                  >
                    <TableCell className="py-2 pl-2 text-[16px] font-medium text-slate-700">
                      {formatTermName(term.termName)}
                    </TableCell>
                    <TableCell className="py-2 text-center text-[16px] font-medium text-slate-700">{term.courseCount}</TableCell>
                    <TableCell className="py-2 text-center text-[16px] font-medium text-slate-700">{term.totalCredits}</TableCell>
                    <TableCell className="py-2 text-center text-[16px] font-medium text-slate-700">{term.totalEcts}</TableCell>
                    <TableCell className="py-2 text-center text-[16px] font-medium text-slate-700">{formatDecimal(term.sgpa)}</TableCell>
                    <TableCell className="py-2 pr-2 text-center text-[16px] font-medium text-slate-700">{formatDecimal(term.cgpa)}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>

          <div className="border-t bg-white">
            <SummaryFootRow
              description="Dersin Başarı ve Sonuçlanma Durumuna Bakılmaksızın (Ders tekrarlarında son alınan ders, kredi toplamına dahildir)"
              valueLabel="Toplam Kredi"
              value={transcript.totalAttemptedCredits}
            />
            <SummaryFootRow
              description="Dersin Başarı ve Sonuçlanma Durumuna Bakılmaksızın (Ders tekrarlarında son alınan ders, AKTS toplamına dahildir)"
              valueLabel="Toplam AKTS"
              value={transcript.totalAttemptedEcts}
              bordered
            />
          </div>
        </CardContent>
      </Card>

      <ReportChartSection
        title="ANO Grafiği (Dönem Not Ortalaması)"
        legendLabel="ANO"
        legendTone="sgpa"
        history={history}
        metric="sgpa"
      />

      <ReportChartSection
        title="AGNO Grafiği (Genel Not Ortalaması)"
        legendLabel="AGNO"
        legendTone="cgpa"
        history={history}
        metric="cgpa"
      />
    </div>
  );
}

function SummaryFootRow({
  description,
  valueLabel,
  value,
  bordered = false,
}: {
  description: string;
  valueLabel: string;
  value: number;
  bordered?: boolean;
}) {
  return (
    <div className={cn('flex flex-col gap-2 px-3 py-3 text-[14px] md:flex-row md:items-center md:justify-between', bordered && 'border-t')}>
      <p className="text-slate-600">{description} [{valueLabel}] :</p>
      <div className="flex items-center gap-3 font-semibold text-slate-700">
        <span className="min-w-10 text-right">{value}</span>
      </div>
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
    <Card className="overflow-hidden border border-[#003366]/20 shadow-md">
      <div className="border-b border-white/10 bg-[#003366] px-5 py-3">
        <div className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
          <h3 className="font-heading text-sm font-extrabold uppercase tracking-tight text-white">
            {title}
          </h3>
          <div className="flex flex-wrap gap-2">
            <LegendBadge label={legendLabel} tone={legendTone} />
            <Badge variant="secondary" className="gap-1.5 border-white/20 bg-white/10 text-white hover:bg-white/20">
              <TrendingUp className="h-3.5 w-3.5" />
              Son değer: {latestValue.toFixed(2)}
            </Badge>
          </div>
        </div>
      </div>
      <CardContent className="p-5">
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
  const dotColor = tone === 'sgpa' ? '#60a5fa' : '#34d399';

  return (
    <span className="inline-flex items-center gap-2 rounded-full border border-white/20 bg-white/10 px-3 py-1 text-xs font-bold text-white shadow-sm">
      <span
        className="h-2 w-2 rounded-full shadow-[0_0_8px_rgba(255,255,255,0.4)]"
        style={{ backgroundColor: dotColor }}
      />
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
    <svg viewBox={`0 0 900 ${CHART_HEIGHT}`} className="w-full" role="img" aria-label={ariaLabel}>
      {GRID_VALUES.map(value => {
        const y = getY(value);
        return (
          <g key={value}>
            <line x1={CHART_PADDING.left} x2={900 - CHART_PADDING.right} y1={y} y2={y} stroke="rgba(148,163,184,0.18)" />
            <text x={CHART_PADDING.left - 8} y={y + 4} textAnchor="end" fontSize="11" fill="#94a3b8">{value.toFixed(1)}</text>
          </g>
        );
      })}

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
              x={x}
              y={y}
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

function formatTermName(termName: string) {
  return termName.endsWith('Dönemi') ? termName : `${termName} Dönemi`;
}

function formatDecimal(value: number) {
  return value.toFixed(2).replace('.', ',');
}
