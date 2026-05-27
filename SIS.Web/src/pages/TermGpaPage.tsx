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
import { formatGpa } from '@/lib/gpaFormat';
import { getTermTypeLabel } from '@/lib/termTypes';
import { AlertCircle, BarChart3, TrendingUp } from 'lucide-react';
import { useTermGpaHistory } from '../hooks/useTermGpaHistory';
import type { StudentTermGpaDto } from '../types/student';

const CHART_HEIGHT = 260;
const CHART_PADDING = { top: 20, right: 20, bottom: 80, left: 48 };
const GRID_VALUES = [0, 0.5, 1.0, 1.5, 2.0, 2.5, 3.0, 3.5, 4.0];
const REPORT_COLORS = {
  sgpa: {
    base: '#4f6ea7',
    latest: '#1d3669',
    surface: 'rgba(39,69,127,0.05)',
    border: 'rgba(39,69,127,0.10)',
    chip: '#edf4ff',
    chipText: '#294b86',
    accent: '#8eb7ff',
    areaTop: 'rgba(79,110,167,0.07)',
    areaMid: 'rgba(79,110,167,0.03)',
    axis: 'rgba(148,163,184,0.24)',
  },
  cgpa: {
    base: '#63ac9d',
    latest: '#2c7a6d',
    surface: 'rgba(26,155,132,0.05)',
    border: 'rgba(26,155,132,0.10)',
    chip: '#ecfbf7',
    chipText: '#1f7467',
    accent: '#8be5c4',
    areaTop: 'rgba(99,172,157,0.07)',
    areaMid: 'rgba(99,172,157,0.03)',
    axis: 'rgba(148,163,184,0.24)',
  },
};

export default function TermGpaPage() {
  const historyQuery = useTermGpaHistory();

  if (historyQuery.isLoading) {
    return (
      <div className="space-y-5">
        <Skeleton className="h-[340px] rounded-xl" />
        <Skeleton className="h-[360px] rounded-xl" />
        <Skeleton className="h-[360px] rounded-xl" />
      </div>
    );
  }

  if (historyQuery.isError || !historyQuery.data) {
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

  const data = historyQuery.data;
  const history = data.terms;

  if (history.length === 0) {
    return (
      <Card>
        <CardContent className="py-20 text-center">
          <BarChart3 className="mx-auto mb-4 h-12 w-12 text-muted-foreground/40" />
          <h3 className="font-heading text-lg font-semibold text-foreground">Henüz dönem verisi yok</h3>
          <p className="mt-1 text-muted-foreground">
            Henüz harf notu oluşmuş dönem veriniz bulunmuyor.
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
                    <TableCell className="py-2 text-center text-[16px] font-medium text-slate-700">{formatGpa(term.sgpa)}</TableCell>
                    <TableCell className="py-2 pr-2 text-center text-[16px] font-medium text-slate-700">{formatGpa(term.cgpa)}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>

          <div className="border-t bg-white">
            <SummaryFootRow
              description="Dersin Başarı ve Sonuçlanma Durumuna Bakılmaksızın (Ders tekrarlarında son alınan ders, kredi toplamına dahildir)"
              valueLabel="Toplam Kredi"
              value={data.totalAttemptedCredits}
            />
            <SummaryFootRow
              description="Dersin Başarı ve Sonuçlanma Durumuna Bakılmaksızın (Ders tekrarlarında son alınan ders, AKTS toplamına dahildir)"
              valueLabel="Toplam AKTS"
              value={data.totalAttemptedEcts}
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
  const values = history.map(item => item[metric]).filter((value): value is number => value != null);
  const peakValue = values.length > 0 ? Math.max(...values) : null;
  const latestValue = history[history.length - 1]?.[metric] ?? null;
  const palette = REPORT_COLORS[legendTone];

  return (
    <Card className="overflow-hidden border border-slate-200/90 shadow-[0_12px_30px_rgba(15,23,42,0.08)]">
      <div
        className="border-b border-white/10 px-5 py-3.5"
        style={{
          backgroundImage: `linear-gradient(135deg, ${palette.latest} 0%, ${palette.base} 100%)`,
        }}
      >
        <div className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
          <div>
            <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-white/55">
              4.00 üzerinden akademik görünüm
            </p>
            <h3 className="font-heading text-sm font-extrabold uppercase tracking-tight text-white">
              {title}
            </h3>
          </div>
          <div className="flex flex-wrap gap-2">
            <LegendBadge label={legendLabel} tone={legendTone} />
            <Badge variant="secondary" className="gap-1.5 border-white/20 bg-white/10 text-white hover:bg-white/20">
              Zirve: {formatGpa(peakValue)}
            </Badge>
            <Badge variant="secondary" className="gap-1.5 border-white/20 bg-white/10 text-white hover:bg-white/20">
              <TrendingUp className="h-3.5 w-3.5" />
              Son değer: {formatGpa(latestValue)}
            </Badge>
          </div>
        </div>
      </div>
      <CardContent className="p-5">
        <div
          className="rounded-[22px] border px-4 py-4 shadow-[inset_0_1px_0_rgba(255,255,255,0.8)] md:px-6 md:py-5"
          style={{
            backgroundImage: `radial-gradient(circle at top right, ${palette.areaTop}, transparent 28%), linear-gradient(180deg, #ffffff 0%, ${palette.areaMid} 100%)`,
            borderColor: palette.border,
          }}
        >
          <TermGpaBarChart history={history} metric={metric} ariaLabel={`${title} sütun grafiği`} />
        </div>
      </CardContent>
    </Card>
  );
}

function LegendBadge({ label, tone }: { label: string; tone: 'sgpa' | 'cgpa' }) {
  const dotColor = tone === 'sgpa' ? '#8eb7ff' : '#8be5c4';

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
  const thresholdY = CHART_PADDING.top + ((4 - 2) / 4) * innerHeight;

  const getY = (val: number) => CHART_PADDING.top + ((4 - val) / 4) * innerHeight;

  return (
    <svg viewBox={`0 0 900 ${CHART_HEIGHT}`} width="900" height={CHART_HEIGHT} className="w-full h-auto" role="img" aria-label={ariaLabel}>
      <defs>
        <linearGradient id={`${metric}-bar-gradient`} x1="0" y1="0" x2="0" y2="1">
          <stop offset="0%" stopColor={palette.base} />
          <stop offset="100%" stopColor={palette.latest} />
        </linearGradient>
        <filter id={`${metric}-bar-shadow`} x="-30%" y="-30%" width="160%" height="180%">
          <feDropShadow dx="0" dy="8" stdDeviation="10" floodColor={palette.latest} floodOpacity="0.18" />
        </filter>
      </defs>

      <rect
        x={CHART_PADDING.left}
        y={CHART_PADDING.top}
        width={innerWidth}
        height={getY(3) - CHART_PADDING.top}
        rx={18}
        fill={palette.areaTop}
      />
      <rect
        x={CHART_PADDING.left}
        y={getY(3)}
        width={innerWidth}
        height={getY(2) - getY(3)}
        fill={palette.areaMid}
      />
      <rect
        x={CHART_PADDING.left}
        y={getY(2)}
        width={innerWidth}
        height={CHART_PADDING.top + innerHeight - getY(2)}
        rx={18}
        fill="rgba(255,255,255,0.55)"
      />

      {GRID_VALUES.map(value => {
        const y = getY(value);
        return (
          <g key={value}>
            <line x1={CHART_PADDING.left} x2={900 - CHART_PADDING.right} y1={y} y2={y} stroke={palette.axis} strokeDasharray="4 6" />
            <text x={CHART_PADDING.left - 8} y={y + 4} textAnchor="end" fontSize="11" fill="#94a3b8">{value.toFixed(1)}</text>
          </g>
        );
      })}

      <line
        x1={CHART_PADDING.left}
        x2={900 - CHART_PADDING.right}
        y1={thresholdY}
        y2={thresholdY}
        stroke="rgba(245,158,11,0.75)"
        strokeWidth="1.5"
        strokeDasharray="7 7"
      />
      <text
        x={900 - CHART_PADDING.right}
        y={thresholdY - 8}
        textAnchor="end"
        fontSize="10"
        fontWeight="700"
        fill="#b45309"
      >
        2.00 eşik
      </text>

      {history.map((term, index) => {
        const value = metric === 'sgpa' ? term.sgpa : term.cgpa;
        const cx = CHART_PADDING.left + index * columnWidth + columnWidth / 2;
        const x = cx - barWidth / 2;
        const hasBar = value != null && value > 0;
        const y = getY(value ?? 0);
        const barHeight = innerHeight - ((4 - (value ?? 0)) / 4) * innerHeight;
        const isLast = index === history.length - 1;
        const pillWidth = 46;
        const pillHeight = 22;
        const pillY = Math.max(CHART_PADDING.top + 6, y - 30);

        return (
          <g key={term.termId}>
            {hasBar ? (
              <>
                <rect
                  x={x}
                  y={y}
                  width={barWidth}
                  height={Math.max(barHeight, 0)}
                  rx={8}
                  fill={isLast ? palette.latest : `url(#${metric}-bar-gradient)`}
                  fillOpacity={isLast ? 0.98 : 0.9}
                  filter={`url(#${metric}-bar-shadow)`}
                />
                <rect
                  x={x + 4}
                  y={y + 5}
                  width={Math.max(barWidth - 8, 0)}
                  height={6}
                  rx={3}
                  fill="rgba(255,255,255,0.18)"
                />
                <rect
                  x={cx - pillWidth / 2}
                  y={pillY}
                  width={pillWidth}
                  height={pillHeight}
                  rx={11}
                  fill={palette.chip}
                  stroke={palette.border}
                />
                <text x={cx} y={pillY + 14} textAnchor="middle" fontSize="11" fontWeight="700" fill={palette.chipText}>
                  {formatGpa(value)}
                </text>
              </>
            ) : (
              <circle cx={cx} cy={CHART_PADDING.top + innerHeight} r={5} fill="rgba(148,163,184,0.18)" />
            )}

            {isLast && hasBar && (
              <circle cx={cx} cy={y} r={6} fill={palette.accent} stroke="#fff" strokeWidth="2" />
            )}
            <text x={cx} y={labelY} textAnchor="middle" fontSize="11" fontWeight="600" fill="#475569">
              {String(term.year).slice(-2)}-{String(term.year + 1).slice(-2)}
            </text>
            <text x={cx} y={labelY + 14} textAnchor="middle" fontSize="10" fill="#94a3b8">
              {getTermTypeLabel(term.semester)}
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
