import { Badge } from '@/components/ui/badge';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import type { PhaseKey } from '@/hooks/useAcademicCalendar';

interface PhaseCardProps {
  title: string;
  phase: PhaseKey;
  start: string;
  end: string;
  override: boolean | null;
  isOpenComputed: boolean;
  onOverrideChange: (value: boolean | null) => void;
  disabled?: boolean;
}

function fmtDate(iso: string) {
  if (!iso) return '—';
  return new Date(iso).toLocaleDateString('tr-TR', { day: '2-digit', month: '2-digit', year: 'numeric' });
}

export function PhaseCard({ title, start, end, override, isOpenComputed, onOverrideChange, disabled }: PhaseCardProps) {
  const overrideValue = override === null ? 'auto' : override ? 'open' : 'closed';

  return (
    <div className="rounded-xl border p-4 bg-white shadow-sm">
      <div className="flex items-center justify-between mb-2">
        <h3 className="font-semibold text-slate-800">{title}</h3>
        {isOpenComputed
          ? <Badge className="bg-emerald-100 text-emerald-700 border-emerald-300">Açık</Badge>
          : <Badge className="bg-slate-100 text-slate-600 border-slate-300">Kapalı</Badge>}
      </div>
      <div className="text-sm text-slate-600 mb-3">
        {fmtDate(start)} — {fmtDate(end)}
      </div>
      <div className="flex items-center gap-2">
        <span className="text-xs text-slate-500">Override:</span>
        <Select
          value={overrideValue}
          onValueChange={(v) => onOverrideChange(v === 'auto' ? null : v === 'open')}
          disabled={disabled}
        >
          <SelectTrigger className="w-40 h-8 text-xs">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="auto">Takvime göre</SelectItem>
            <SelectItem value="open">Zorla aç</SelectItem>
            <SelectItem value="closed">Zorla kapat</SelectItem>
          </SelectContent>
        </Select>
      </div>
    </div>
  );
}
