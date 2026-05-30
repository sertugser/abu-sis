// SIS.Web/src/components/grade/GradeStatsPanel.tsx
// Sınıf istatistik paneli + harf dağılımı çubuğu + kriter önizleme bloğu.

import { useCallback, useMemo } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { BarChart2 } from 'lucide-react';
import { computeScoreStats, letterDistribution, passFailCounts } from '@/lib/gradeStats';
import { GRADE_STYLE, LETTER_ORDER, resolveGrade } from './gradeDisplay';
import type { GradingCriteria } from '../../types/grade';

const HISTOGRAM_BUCKETS: { label: string; min: number; max: number }[] = [
  { label: '0–49', min: 0, max: 49 },
  { label: '50–59', min: 50, max: 59 },
  { label: '60–69', min: 60, max: 69 },
  { label: '70–79', min: 70, max: 79 },
  { label: '80–89', min: 80, max: 89 },
  { label: '90–100', min: 90, max: 100 },
];

export function LetterDistBar({ dist, total }: { dist: Record<string, number>; total: number }) {
  return (
    <div className="flex flex-wrap gap-1.5">
      {LETTER_ORDER.map((letter) => {
        const n = dist[letter] ?? 0;
        if (n === 0) return null;
        const style = GRADE_STYLE[letter];
        return (
          <span
            key={letter}
            className={`inline-flex items-center gap-1 px-2 py-0.5 rounded-md text-xs font-semibold font-mono ${style?.bg ?? 'bg-muted'} ${style?.text ?? 'text-foreground'}`}
            title={`${Math.round((n / total) * 100)}%`}
          >
            {letter}
            <span className="opacity-75">{n}</span>
          </span>
        );
      })}
    </div>
  );
}

interface ClassStatsPanelProps {
  scores: number[];
  resolve: (score: number) => string;
}

export function ClassStatsPanel({ scores, resolve }: ClassStatsPanelProps) {
  const stats = useMemo(() => computeScoreStats(scores), [scores]);
  const dist = useMemo(() => letterDistribution(scores, resolve), [scores, resolve]);
  const pf = useMemo(() => passFailCounts(scores, resolve), [scores, resolve]);

  if (!stats) return null;

  const bucketCounts = HISTOGRAM_BUCKETS.map((b) => ({
    label: b.label,
    count: scores.filter((s) => s >= b.min && s <= b.max).length,
  }));
  const maxBucketCount = Math.max(...bucketCounts.map((b) => b.count), 1);
  const passRate = stats.count > 0 ? Math.round((pf.pass / stats.count) * 100) : 0;

  return (
    <Card className="mb-4">
      <CardHeader className="pb-2 pt-4 px-4">
        <CardTitle className="text-sm font-semibold flex items-center gap-2 text-foreground">
          <BarChart2 className="h-4 w-4 text-primary" />
          Sınıf İstatistikleri
          <span className="ml-auto text-xs text-muted-foreground font-normal">
            {stats.count} notlu öğrenci
          </span>
        </CardTitle>
      </CardHeader>
      <CardContent className="px-4 pb-4 space-y-4">
        <div className="grid grid-cols-3 gap-3 sm:grid-cols-6">
          {[
            { label: 'Ortalama', value: stats.mean.toFixed(1) },
            { label: 'Std. Sapma', value: stats.stdDev.toFixed(1) },
            { label: 'Medyan', value: stats.median.toFixed(1) },
            { label: 'En Düşük', value: stats.min.toFixed(0) },
            { label: 'En Yüksek', value: stats.max.toFixed(0) },
            { label: 'Geçme %', value: `%${passRate}` },
          ].map(({ label, value }) => (
            <div key={label} className="rounded-lg bg-muted/40 px-3 py-2 text-center">
              <div className="text-[11px] text-muted-foreground leading-none mb-1">{label}</div>
              <div className="text-sm font-bold font-mono text-foreground">{value}</div>
            </div>
          ))}
        </div>

        <div>
          <p className="text-[11px] text-muted-foreground mb-1.5 uppercase tracking-wide">Puan Dağılımı</p>
          <div className="flex items-end gap-1.5 h-16">
            {bucketCounts.map(({ label, count }) => {
              const heightPct = Math.round((count / maxBucketCount) * 100);
              return (
                <div key={label} className="flex flex-col items-center gap-0.5 flex-1">
                  <span className="text-[10px] text-muted-foreground font-mono leading-none">{count > 0 ? count : ''}</span>
                  <div className="w-full rounded-sm bg-primary/20 flex items-end" style={{ height: '40px' }}>
                    <div className="w-full rounded-sm bg-primary/60 transition-all" style={{ height: `${heightPct}%` }} />
                  </div>
                  <span className="text-[9px] text-muted-foreground leading-none">{label}</span>
                </div>
              );
            })}
          </div>
        </div>

        <div>
          <p className="text-[11px] text-muted-foreground mb-1.5 uppercase tracking-wide">Harf Dağılımı</p>
          <LetterDistBar dist={dist} total={stats.count} />
          <div className="mt-2 flex items-center gap-3 text-xs text-muted-foreground">
            <span className="text-emerald-600 font-medium">Geçen: {pf.pass}</span>
            <span className="text-red-600 font-medium">Kalan: {pf.fail}</span>
          </div>
        </div>
      </CardContent>
    </Card>
  );
}

interface CriteriaPreviewProps {
  scores: number[];
  criteria: GradingCriteria;
}

export function CriteriaPreview({ scores, criteria }: CriteriaPreviewProps) {
  const resolve = useCallback((s: number) => resolveGrade(s, criteria), [criteria]);
  const dist = useMemo(() => letterDistribution(scores, resolve), [scores, resolve]);
  const pf = useMemo(() => passFailCounts(scores, resolve), [scores, resolve]);
  const total = scores.length;

  if (total === 0) return null;

  return (
    <div className="rounded-md border bg-muted/30 px-4 py-3 space-y-2">
      <p className="text-xs font-medium text-muted-foreground uppercase tracking-wide">
        Bu Eşiklerle Dağılım Önizlemesi ({total} öğrenci)
      </p>
      <LetterDistBar dist={dist} total={total} />
      <div className="flex items-center gap-3 text-xs">
        <span className="text-emerald-600 font-semibold">Geçen: {pf.pass}</span>
        <span className="text-red-600 font-semibold">Kalan: {pf.fail}</span>
        {total > 0 && (
          <span className="text-muted-foreground ml-auto">
            Geçme oranı: %{Math.round((pf.pass / total) * 100)}
          </span>
        )}
      </div>
    </div>
  );
}
