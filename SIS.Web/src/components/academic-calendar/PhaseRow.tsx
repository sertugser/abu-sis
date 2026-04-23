import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { SELECT_CLS } from '@/components/academic-structure/shared-constants';
import type { PhaseKey } from '@/hooks/useAcademicCalendar';

interface PhaseRowProps {
  title: string;
  phase: PhaseKey;
  startDate: string;
  endDate: string;
  onStartChange?: (value: string) => void;
  onEndChange: (value: string) => void;
  startLabel?: string;
  endLabel?: string;
  startReadonly?: boolean;
  startHint?: string;
  override: boolean | null;
  isOpenComputed: boolean;
  onOverrideChange: (value: boolean | null) => void;
  disabled?: boolean;
}

export function PhaseRow({
  title,
  startDate,
  endDate,
  onStartChange,
  onEndChange,
  startLabel = 'Başlangıç',
  endLabel = 'Bitiş',
  startReadonly = false,
  startHint,
  override,
  isOpenComputed,
  onOverrideChange,
  disabled = false,
}: PhaseRowProps) {
  const overrideValue = override === null ? 'auto' : override ? 'open' : 'closed';
  const isOverridden = override !== null;

  const status = resolveStatus({ isOpen: isOpenComputed, override });

  return (
    <div className="rounded-lg border border-border bg-card">
      {/* Header */}
      <div className="flex items-center justify-between px-4 py-2.5 border-b border-border bg-muted/30">
        <h3 className="text-sm font-semibold text-foreground">{title}</h3>
        <Badge className={`${status.className} text-[11px] font-medium`}>
          <span className={`w-1.5 h-1.5 rounded-full ${status.dotClass} mr-1.5`} />
          {status.label}
        </Badge>
      </div>

      {/* Content */}
      <div className="p-4 grid grid-cols-1 md:grid-cols-12 gap-3 md:items-end">
        {/* Start */}
        <div className="md:col-span-4 space-y-1">
          <label className="text-xs font-medium text-muted-foreground">{startLabel}</label>
          <Input
            type="date"
            value={startDate}
            readOnly={startReadonly}
            disabled={disabled || startReadonly}
            onChange={e => onStartChange?.(e.target.value)}
            className={startReadonly ? 'cursor-default opacity-75' : ''}
          />
          {startHint && <p className="text-[11px] text-muted-foreground leading-tight">{startHint}</p>}
        </div>

        {/* End */}
        <div className="md:col-span-4 space-y-1">
          <label className="text-xs font-medium text-muted-foreground">{endLabel}</label>
          <Input
            type="date"
            value={endDate}
            disabled={disabled}
            onChange={e => onEndChange(e.target.value)}
          />
        </div>

        {/* Override */}
        <div className="md:col-span-4 space-y-1">
          <label className="text-xs font-medium text-muted-foreground flex items-center gap-1.5">
            Override
            {isOverridden && (
              <span className="inline-block px-1 py-0 text-[9px] font-semibold uppercase bg-amber-100 text-amber-800 rounded">
                Manuel
              </span>
            )}
          </label>
          <Select
            value={overrideValue}
            onValueChange={(v) => onOverrideChange(v === 'auto' ? null : v === 'open')}
            disabled={disabled}
          >
            <SelectTrigger className={SELECT_CLS + ' h-9'}>
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
    </div>
  );
}

// ─── Status resolver ─────────────────────────────────────────────
// Override durumu badge'ine yansıtılır — "neden" açıklayıcı.

interface StatusInfo {
  label: string;
  className: string;
  dotClass: string;
}

function resolveStatus({ isOpen, override }: { isOpen: boolean; override: boolean | null }): StatusInfo {
  if (override === true) {
    return {
      label: 'Manuel olarak açık',
      className: 'bg-amber-100 text-amber-800 border-amber-300 hover:bg-amber-100',
      dotClass: 'bg-amber-500',
    };
  }
  if (override === false) {
    return {
      label: 'Manuel olarak kapalı',
      className: 'bg-amber-100 text-amber-800 border-amber-300 hover:bg-amber-100',
      dotClass: 'bg-amber-500',
    };
  }
  if (isOpen) {
    return {
      label: 'Takvime göre açık',
      className: 'bg-emerald-100 text-emerald-700 border-emerald-300 hover:bg-emerald-100',
      dotClass: 'bg-emerald-500',
    };
  }
  return {
    label: 'Kapalı',
    className: 'bg-muted text-muted-foreground border-border hover:bg-muted',
    dotClass: 'bg-muted-foreground/50',
  };
}
