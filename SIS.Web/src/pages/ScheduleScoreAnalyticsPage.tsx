import { useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { ChevronLeft } from 'lucide-react';
import { Button } from '@/components/ui/button';
import ScoreBreakdownDashboard from '../components/constraint/ScoreBreakdownDashboard';
import { useScoreBreakdown } from '../hooks/useConstraints';
import { useAdminTerms } from '../hooks/useAdminTerms';
import {
  useTrendData,
  useHeatmap,
  useBottlenecks,
  useTakeSnapshot,
} from '../hooks/useConstraintAnalytics';

/**
 * Ders programı skor / ihlal analizi paneli.
 *
 * Burada admin "şu anki programın sağlığı nedir, neyi iyileştirmem lazım"
 * sorusunu cevaplar. Kısıt yönetiminden ayrı tutuldu: bu sayfa **bilgi/öngörü**
 * için, kısıt düzenleme için değil.
 */
export default function ScheduleScoreAnalyticsPage() {
  const { data: terms } = useAdminTerms();
  const [selectedTermId, setSelectedTermId] = useState<number | null>(null);

  const effectiveTermId = useMemo(() => {
    if (selectedTermId !== null) return selectedTermId;
    if (!terms || terms.length === 0) return null;
    const active = terms.find(t => t.isActive);
    return active ? active.id : terms[0].id;
  }, [selectedTermId, terms]);

  const { data: breakdown, isLoading: breakdownLoading } = useScoreBreakdown(effectiveTermId);
  const { data: trendData } = useTrendData(effectiveTermId);
  const { data: heatmapData } = useHeatmap(effectiveTermId);
  const { data: bottleneckData } = useBottlenecks(effectiveTermId);
  const takeSnapshotMut = useTakeSnapshot(effectiveTermId);

  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-center gap-3 rounded-xl border bg-card px-4 py-2.5">
        <Button asChild size="sm" variant="ghost" className="h-8 gap-1.5">
          <Link to="/ders-programi-kisitlamalari">
            <ChevronLeft className="h-3.5 w-3.5" />
            Kısıtlara Dön
          </Link>
        </Button>

        <div className="h-5 w-px bg-border" />

        <span className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">Dönem</span>
        <select
          className="h-8 min-w-[200px] rounded-md border bg-muted/50 px-2.5 text-xs"
          value={effectiveTermId ?? ''}
          onChange={(e) => setSelectedTermId(Number(e.target.value))}
        >
          {(terms ?? []).map(t => (
            <option key={t.id} value={t.id}>
              {t.name} {t.isActive ? '(Aktif)' : ''}
            </option>
          ))}
        </select>

        <div className="ml-auto text-xs text-muted-foreground">
          Bu sayfa salt-okunur — kısıt değiştirmek için
          {' '}
          <Link to="/ders-programi-kisitlamalari" className="font-medium underline underline-offset-2">
            Kısıt Yönetimi
          </Link>
          'ne dön.
        </div>
      </div>

      {effectiveTermId == null ? (
        <div className="rounded-xl border border-dashed p-8 text-center text-sm text-muted-foreground">
          Önce bir dönem seçin.
        </div>
      ) : (
        <ScoreBreakdownDashboard
          breakdown={breakdown}
          isLoading={breakdownLoading}
          trendData={trendData}
          heatmapData={heatmapData}
          bottleneckData={bottleneckData}
          onTakeSnapshot={() => takeSnapshotMut.mutate()}
          snapshotPending={takeSnapshotMut.isPending}
        />
      )}
    </div>
  );
}
