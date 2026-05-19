import { useMemo } from 'react';
import type { LucideIcon } from 'lucide-react';
import { AlertTriangle, CheckCircle2, EyeOff, Megaphone } from 'lucide-react';
import type { AnnouncementList } from '../../types/announcements';

interface AnnouncementStatsBarProps {
  items: AnnouncementList[];
}

/**
 * Yönetim sayfası başlık şeridinin solunda yer alan kompakt sayaçlar.
 * Ayrı kart kullanmaz; tek satırda chip benzeri özet sunar.
 */
export default function AnnouncementStatsBar({ items }: AnnouncementStatsBarProps) {
  const stats = useMemo(() => {
    const total   = items.length;
    const active  = items.filter((a) => a.isActive).length;
    const passive = total - active;
    const popup   = items.filter((a) => a.isPopup).length;
    return { total, active, passive, popup };
  }, [items]);

  const chips: ChipProps[] = [
    { label: 'Toplam',  value: stats.total,   icon: Megaphone,      tone: 'primary' },
    { label: 'Yayında', value: stats.active,  icon: CheckCircle2,   tone: 'emerald' },
    { label: 'Pasif',   value: stats.passive, icon: EyeOff,         tone: 'slate'   },
    { label: 'Pop-up',  value: stats.popup,   icon: AlertTriangle,  tone: 'amber'   },
  ];

  return (
    <div className="flex flex-wrap items-center gap-x-5 gap-y-2">
      {chips.map((c, idx) => (
        <span key={c.label} className="flex items-center gap-2">
          <Chip {...c} />
          {idx < chips.length - 1 && (
            <span aria-hidden className="hidden md:inline h-5 w-px bg-border/70" />
          )}
        </span>
      ))}
    </div>
  );
}

type Tone = 'primary' | 'emerald' | 'slate' | 'amber';

interface ChipProps {
  label: string;
  value: number;
  icon: LucideIcon;
  tone: Tone;
}

const TONE_STYLES: Record<Tone, { iconBg: string; iconFg: string; value: string }> = {
  primary: { iconBg: 'bg-primary/12',    iconFg: 'text-primary',       value: 'text-foreground' },
  emerald: { iconBg: 'bg-emerald-500/12', iconFg: 'text-emerald-600',  value: 'text-emerald-700' },
  slate:   { iconBg: 'bg-slate-500/12',  iconFg: 'text-slate-600',     value: 'text-slate-700'  },
  amber:   { iconBg: 'bg-amber-500/15',  iconFg: 'text-amber-600',     value: 'text-amber-700'  },
};

function Chip({ label, value, icon: Icon, tone }: ChipProps) {
  const t = TONE_STYLES[tone];
  return (
    <span className="inline-flex items-center gap-2">
      <span className={`flex h-7 w-7 items-center justify-center rounded-lg ${t.iconBg}`}>
        <Icon className={`h-3.5 w-3.5 ${t.iconFg}`} />
      </span>
      <span className="flex items-baseline gap-1.5">
        <span className={`font-heading text-lg font-bold tabular-nums leading-none ${t.value}`}>
          {value}
        </span>
        <span className="text-xs text-muted-foreground">{label}</span>
      </span>
    </span>
  );
}
