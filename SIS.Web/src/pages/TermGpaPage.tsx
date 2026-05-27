import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
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
import { AlignJustify, AlertCircle, BarChart3 } from 'lucide-react';
import type { ReactNode } from 'react';
import { useTermGpaHistory } from '../hooks/useTermGpaHistory';
import type { StudentTermGpaDto } from '../types/student';

// Her dönem (kategori) için sabit, düz renk — iki grafikte de aynı index aynı renk (Proliz tarzı).
const CATEGORY_COLORS = ['#4a9fe0', '#46c9a0', '#f5a623', '#ee5f73', '#8b7fd4', '#3aa0ff', '#2ec4b6', '#e0a800', '#d633ff'];
const colorAt = (index: number) => CATEGORY_COLORS[index % CATEGORY_COLORS.length];

// 0.00 → 4.00 arası 0.40 adımlı ızgara değerleri.
const GRID_VALUES = Array.from({ length: 11 }, (_, i) => Math.round(i * 0.4 * 10) / 10);

export default function TermGpaPage() {
  const historyQuery = useTermGpaHistory();

  if (historyQuery.isLoading) {
    return (
      <div className="space-y-5">
        <Skeleton className="h-[820px] rounded-xl" />
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
    <div className="overflow-hidden rounded-lg border border-slate-300 bg-white shadow-sm">
      {/* ── Tablo ── */}
      <div className="border-b border-slate-200 bg-slate-50 px-4 py-2.5">
        <h3 className="text-[15px] font-semibold text-slate-700">Dönem Ortalamaları</h3>
      </div>
      <div className="overflow-x-auto">
        <Table>
          <TableHeader>
            <TableRow className="border-b-0 bg-[#003366] hover:bg-[#003366]">
              <TableHead className="h-10 pl-3 text-[14px] font-semibold text-white">Dönem Adı</TableHead>
              <TableHead className="h-10 text-center text-[14px] font-semibold text-white">Aldığı Ders Sayısı</TableHead>
              <TableHead className="h-10 text-center text-[14px] font-semibold text-white">Toplam Kredi</TableHead>
              <TableHead className="h-10 text-center text-[14px] font-semibold text-white">Toplam AKTS</TableHead>
              <TableHead className="h-10 text-center text-[14px] font-semibold text-white">Dönem Ortalaması</TableHead>
              <TableHead className="h-10 pr-3 text-center text-[14px] font-semibold text-white">Genel Not Ortalaması</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {history.map((term, index) => (
              <TableRow
                key={term.termId}
                className={cn('border-b border-slate-200 transition-colors hover:bg-[#dce9ff]', index % 2 === 1 && 'bg-slate-50/60')}
              >
                <TableCell className="py-2 pl-3 text-[15px] font-medium text-slate-700">{formatTermName(term.termName)}</TableCell>
                <TableCell className="py-2 text-center text-[15px] text-slate-700">{term.courseCount}</TableCell>
                <TableCell className="py-2 text-center text-[15px] text-slate-700">{term.totalCredits}</TableCell>
                <TableCell className="py-2 text-center text-[15px] text-slate-700">{term.totalEcts}</TableCell>
                <TableCell className="py-2 text-center text-[15px] font-medium text-slate-700">{formatGpa(term.sgpa)}</TableCell>
                <TableCell className="py-2 pr-3 text-center text-[15px] font-medium text-slate-700">{formatGpa(term.cgpa)}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </div>

      {/* ── Özet ── */}
      <div className="border-t border-slate-200 bg-slate-50/40">
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

      {/* ── Grafikler ── */}
      <ChartSection title="Dönem Ortalamaları">
        <TermGpaBarChart history={history} metric="sgpa" ariaLabel="Dönem ortalaması (ANO) sütun grafiği" />
      </ChartSection>
      <ChartSection title="Genel Ortalamalar">
        <TermGpaBarChart history={history} metric="cgpa" ariaLabel="Genel not ortalaması (AGNO) sütun grafiği" />
      </ChartSection>
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
    <div className={cn('flex flex-col gap-1 px-4 py-2.5 text-[13px] md:flex-row md:items-center md:justify-between', bordered && 'border-t border-slate-200')}>
      <p className="text-slate-600">{description} [{valueLabel}] :</p>
      <span className="min-w-10 text-right font-semibold text-slate-700">{value}</span>
    </div>
  );
}

function ChartSection({ title, children }: { title: string; children: ReactNode }) {
  return (
    <section className="border-t border-slate-200">
      <div className="flex items-center justify-between px-5 pt-4">
        <h3 className="text-[15px] font-bold text-slate-800">{title}</h3>
        <AlignJustify className="h-4 w-4 text-slate-300" aria-hidden />
      </div>
      <div className="px-3 pb-5 pt-2 md:px-5">{children}</div>
    </section>
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
  const width = 960;
  const height = 360;
  const pad = { top: 16, right: 16, bottom: 56, left: 44 };
  const innerWidth = width - pad.left - pad.right;
  const innerHeight = height - pad.top - pad.bottom;
  const baselineY = pad.top + innerHeight;
  // Sabit maksimum slot genişliği: az dönemde barlar ORTALANMAZ, soldan sağa dizilir;
  // çok dönemde slot küçülüp tüm genişliğe sığar.
  const columnWidth = Math.min(112, innerWidth / Math.max(history.length, 1));
  const barWidth = Math.min(58, Math.max(26, columnWidth * 0.5));

  const yFor = (value: number) => pad.top + (1 - value / 4) * innerHeight;

  return (
    <svg viewBox={`0 0 ${width} ${height}`} width={width} height={height} className="h-auto w-full" role="img" aria-label={ariaLabel}>
      {/* Izgara + sol eksen */}
      {GRID_VALUES.map(value => {
        const y = yFor(value);
        return (
          <g key={value}>
            <line x1={pad.left} x2={width - pad.right} y1={y} y2={y} stroke="#eef2f6" strokeWidth={1} />
            <text x={pad.left - 8} y={y + 4} textAnchor="end" fontSize="11" fill="#94a3b8">{value.toFixed(2)}</text>
          </g>
        );
      })}
      {/* Taban çizgisi */}
      <line x1={pad.left} x2={width - pad.right} y1={baselineY} y2={baselineY} stroke="#cbd5e1" strokeWidth={1} />

      {history.map((term, index) => {
        const value = metric === 'sgpa' ? term.sgpa : term.cgpa;
        const cx = pad.left + index * columnWidth + columnWidth / 2;
        const color = colorAt(index);
        const hasBar = value != null && value > 0;
        const top = hasBar ? yFor(value) : baselineY;
        const barHeight = baselineY - top;
        const yy = String(term.year).slice(-2);
        const yyNext = String(term.year + 1).slice(-2);

        return (
          <g key={term.termId}>
            {hasBar && (
              <>
                <rect x={cx - barWidth / 2} y={top} width={barWidth} height={barHeight} rx={3} fill={color} />
                {barHeight >= 26 ? (
                  <text x={cx} y={baselineY - 12} textAnchor="middle" fontSize="13" fontWeight="700" fill="#ffffff">
                    {formatGpa(value)}
                  </text>
                ) : (
                  <text x={cx} y={top - 6} textAnchor="middle" fontSize="12" fontWeight="700" fill={color}>
                    {formatGpa(value)}
                  </text>
                )}
              </>
            )}
            {/* Renkli kategori etiketi (iki satır) */}
            <text x={cx} y={baselineY + 18} textAnchor="middle" fontSize="11" fontWeight="600" fill={color}>
              {yy}-{yyNext}
            </text>
            <text x={cx} y={baselineY + 32} textAnchor="middle" fontSize="11" fill={color}>
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
