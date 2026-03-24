import { useState } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { cn } from '@/lib/utils';

/* ── Tipler ── */
export interface HeatmapCell {
  day: number;       // 0 = Pazartesi … 4 = Cuma
  hour: number;      // 8 … 17
  occupancy: number; // 0.0 – 1.0 doluluk oranı
  /** Hover'da gösterilecek ek bilgi */
  details?: string;
}

interface HeatmapGridProps {
  data: HeatmapCell[];
  title?: string;
  className?: string;
}

/* ── Sabitler ── */
const DAYS = ['Pazartesi', 'Salı', 'Çarşamba', 'Perşembe', 'Cuma'];
const HOURS = Array.from({ length: 10 }, (_, i) => i + 8); // 08:00 – 17:00

function getHeatColor(v: number): string {
  if (v <= 0) return 'bg-slate-50';
  if (v < 0.25) return 'bg-emerald-100';
  if (v < 0.50) return 'bg-emerald-300';
  if (v < 0.75) return 'bg-amber-300';
  return 'bg-red-400';
}

function getTextColor(v: number): string {
  if (v >= 0.75) return 'text-white';
  return 'text-foreground/70';
}

/* ────────────────────── Bileşen ────────────────────── */

export default function HeatmapGrid({ data, title = 'Haftalık Doluluk Haritası', className }: HeatmapGridProps) {
  const [hovered, setHovered] = useState<{ day: number; hour: number } | null>(null);

  // Hızlı erişim için map
  const cellMap = new Map<string, HeatmapCell>();
  data.forEach(c => cellMap.set(`${c.day}-${c.hour}`, c));

  return (
    <Card className={className}>
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <span>🗓️</span> {title}
        </CardTitle>
      </CardHeader>
      <CardContent className="overflow-x-auto">
        <div className="min-w-[600px]">
          {/* Başlık satırı */}
          <div className="grid grid-cols-[80px_repeat(5,1fr)] gap-1 mb-1">
            <div className="text-xs font-medium text-muted-foreground text-center">Saat</div>
            {DAYS.map(d => (
              <div key={d} className="text-xs font-semibold text-center">{d}</div>
            ))}
          </div>

          {/* Grid gövde */}
          {HOURS.map(hour => (
            <div key={hour} className="grid grid-cols-[80px_repeat(5,1fr)] gap-1 mb-1">
              <div className="text-xs text-muted-foreground flex items-center justify-center">
                {String(hour).padStart(2, '0')}:00
              </div>
              {DAYS.map((_, dayIdx) => {
                const cell = cellMap.get(`${dayIdx}-${hour}`);
                const occ = cell?.occupancy ?? 0;
                const isHov = hovered?.day === dayIdx && hovered?.hour === hour;

                return (
                  <div
                    key={`${dayIdx}-${hour}`}
                    className={cn(
                      'relative rounded-md h-9 flex items-center justify-center text-[11px] font-medium cursor-default transition-all duration-150',
                      getHeatColor(occ),
                      getTextColor(occ),
                      isHov && 'ring-2 ring-primary shadow-md scale-105 z-10',
                    )}
                    onMouseEnter={() => setHovered({ day: dayIdx, hour })}
                    onMouseLeave={() => setHovered(null)}
                  >
                    %{Math.round(occ * 100)}

                    {/* Hover tooltip — beyaz popup */}
                    {isHov && cell?.details && (
                      <div className="absolute bottom-full mb-3 left-1/2 -translate-x-1/2 z-50 pointer-events-none">
                        <div className="bg-white text-gray-800 border border-gray-200 rounded-lg shadow-xl px-3 py-2 text-xs font-medium whitespace-nowrap">
                          {cell.details}
                        </div>
                        {/* Arrow */}
                        <div className="absolute top-full left-1/2 -translate-x-1/2 -mt-[1px]">
                          <div className="w-0 h-0 border-l-[6px] border-l-transparent border-r-[6px] border-r-transparent border-t-[6px] border-t-white drop-shadow-sm" />
                        </div>
                      </div>
                    )}
                  </div>
                );
              })}
            </div>
          ))}

          {/* Renk lejantı */}
          <div className="flex items-center gap-4 mt-4 pt-3 border-t border-border text-xs text-muted-foreground">
            <span>Doluluk:</span>
            {[
              { color: 'bg-slate-50 border border-border', label: '%0' },
              { color: 'bg-emerald-100', label: '%1-25' },
              { color: 'bg-emerald-300', label: '%25-50' },
              { color: 'bg-amber-300', label: '%50-75' },
              { color: 'bg-red-400', label: '%75+' },
            ].map(l => (
              <div key={l.label} className="flex items-center gap-1.5">
                <div className={cn('w-4 h-3 rounded-sm', l.color)} />
                <span>{l.label}</span>
              </div>
            ))}
          </div>
        </div>
      </CardContent>
    </Card>
  );
}
