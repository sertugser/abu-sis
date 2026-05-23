import type { ComponentType, ReactNode } from 'react';
import { CheckCircle2, XCircle } from 'lucide-react';

export type PanelTone = 'indigo' | 'violet' | 'emerald' | 'amber' | 'blue';

const TONE: Record<PanelTone, { bg: string; text: string }> = {
  indigo: { bg: 'bg-indigo-50', text: 'text-indigo-600' },
  violet: { bg: 'bg-violet-50', text: 'text-violet-600' },
  emerald: { bg: 'bg-emerald-50', text: 'text-emerald-600' },
  amber: { bg: 'bg-amber-50', text: 'text-amber-600' },
  blue: { bg: 'bg-blue-50', text: 'text-blue-600' },
};

/** Dengeli KPI kutusu: ikon solda, sayı+etiket sağda (geniş kutuda sola yapışmaz). */
export function KpiCard({
  icon: Icon,
  label,
  value,
  tone,
}: {
  icon: ComponentType<{ className?: string }>;
  label: string;
  value: number;
  tone: PanelTone;
}) {
  const t = TONE[tone];
  return (
    <div className="flex items-center gap-3 rounded-xl border border-slate-200 bg-white px-4 py-3">
      <span className={`inline-flex h-10 w-10 shrink-0 items-center justify-center rounded-lg ${t.bg} ${t.text}`}>
        <Icon className="h-5 w-5" />
      </span>
      <div className="min-w-0">
        <div className="text-2xl font-semibold leading-none tabular-nums text-foreground">{value}</div>
        <div className="mt-1 truncate text-xs text-muted-foreground">{label}</div>
      </div>
    </div>
  );
}

export function SectionCard({ title, hint, children }: { title: string; hint?: string; children: ReactNode }) {
  return (
    <div className="rounded-xl border border-slate-200 bg-white p-4">
      <div className="mb-3">
        <h3 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">{title}</h3>
        {hint && <p className="text-[11px] text-muted-foreground/80">{hint}</p>}
      </div>
      {children}
    </div>
  );
}

/** Harekete geçirici uyarı satırı: 0 ise yeşil onay, >0 ise renkli sayı. */
export function AlertStat({ label, count, total, critical }: { label: string; count: number; total?: number; critical?: boolean }) {
  const ok = count === 0;
  const color = ok ? 'text-emerald-600' : critical ? 'text-red-600' : 'text-amber-600';
  return (
    <div className="flex items-center justify-between gap-2 rounded-lg border border-slate-200 bg-white px-3 py-2">
      <div className="flex items-center gap-2 text-sm">
        {ok ? <CheckCircle2 className="h-4 w-4 text-emerald-500" /> : <XCircle className={`h-4 w-4 ${color}`} />}
        <span className="text-foreground">{label}</span>
      </div>
      <span className={`text-sm font-semibold tabular-nums ${color}`}>
        {count}{total !== undefined ? ` / ${total}` : ''}
      </span>
    </div>
  );
}
