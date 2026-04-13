import { Star } from 'lucide-react';
import type { ScheduleRunSummary } from '../../../types/facultyPlanning';

interface RunChipBarProps {
  runs: ScheduleRunSummary[];
  activeRunId: number | null;
  onSelect: (id: number) => void;
}

function fmtChipDate(s: string): string {
  return new Date(s).toLocaleString('tr-TR', {
    day: '2-digit', month: 'short', hour: '2-digit', minute: '2-digit',
  });
}

export function RunChipBar({ runs, activeRunId, onSelect }: RunChipBarProps) {
  if (runs.length === 0) return null;

  // En iyi çalıştırma: en yüksek başarı oranı
  const bestId = runs.reduce((best, r) => {
    const total = r.successCount + r.failedCount;
    const bestTotal = best.successCount + best.failedCount;
    const rate = total > 0 ? r.successCount / total : 0;
    const bestRate = bestTotal > 0 ? best.successCount / bestTotal : 0;
    return rate > bestRate ? r : best;
  }, runs[0]).id;

  const visible = runs.slice(0, 5);
  const remaining = runs.length - 5;

  return (
    <div className="flex items-center gap-2">
      <span className="text-[11px] text-muted-foreground shrink-0">Çalıştırmalar:</span>
      {visible.map(r => {
        const total = r.successCount + r.failedCount;
        const pct = total > 0 ? Math.round((r.successCount / total) * 100) : 0;
        const isActive = r.id === activeRunId;
        const isBest = r.id === bestId;

        return (
          <button
            key={r.id}
            type="button"
            onClick={() => onSelect(r.id)}
            className={`flex items-center gap-1.5 px-3 py-1 rounded-full text-xs font-medium transition-colors ${
              isActive
                ? 'bg-indigo-600 text-white'
                : 'bg-muted/50 border border-border/50 text-muted-foreground hover:border-indigo-500/30'
            }`}
          >
            {isBest && <Star className="h-3 w-3" />}
            {fmtChipDate(r.createdAt)} — %{pct}
          </button>
        );
      })}
      {remaining > 0 && (
        <span className="text-[11px] text-muted-foreground">+{remaining}</span>
      )}
    </div>
  );
}
