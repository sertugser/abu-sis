import { Badge } from '@/components/ui/badge';
import type { AnnounceStatus } from './examStatusUtils';

// ── announce-status cell ──────────────────────────────────────
export function AnnounceStatusCell({ status }: { status: AnnounceStatus }) {
  if (status.kind === 'announced') {
    return (
      <Badge variant="outline" className="bg-emerald-50 text-emerald-700 border-emerald-200">
        İlan Edildi
      </Badge>
    );
  }
  if (status.kind === 'overdue') {
    return (
      <Badge variant="outline" className="bg-red-50 text-red-700 border-red-200">
        Sınav İlan Süresi Geçti
      </Badge>
    );
  }
  return (
    <span className="text-xs text-amber-700">
      İlan için son {status.days}g {status.hours}s
    </span>
  );
}

// ── status legend (3-item) ────────────────────────────────────
export function ExamStatusLegend() {
  return (
    <div className="flex flex-wrap items-center gap-x-4 gap-y-1.5 border-t border-border/60 px-5 py-2.5 bg-muted/10">
      <span className="inline-flex items-center gap-1.5">
        <span className="inline-block h-2.5 w-2.5 rounded-full bg-emerald-500" />
        <span className="text-xs text-muted-foreground">İlan Edildi</span>
      </span>
      <span className="inline-flex items-center gap-1.5">
        <span className="inline-block h-2.5 w-2.5 rounded-full bg-rose-500" />
        <span className="text-xs text-muted-foreground">Sonuçlandırıldı</span>
      </span>
      <span className="inline-flex items-center gap-1.5">
        <span className="inline-block h-2.5 w-2.5 rounded-full bg-red-600" />
        <span className="text-xs text-muted-foreground">Sınav İlan Süresi Geçti</span>
      </span>
    </div>
  );
}
