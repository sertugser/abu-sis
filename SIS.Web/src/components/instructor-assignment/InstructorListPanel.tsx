import { useMemo, useState } from 'react';
import { Input } from '@/components/ui/input';
import { ScrollArea } from '@/components/ui/scroll-area';
import { Search } from 'lucide-react';
import type { InstructorOptionDto, InstructorWorkloadDto } from '../../types/courseOpening';

interface Props {
  instructors: InstructorOptionDto[];
  workloads: Map<number, InstructorWorkloadDto>;
  selectedId: number | null;
  onSelect: (id: number) => void;
}

export function InstructorListPanel({ instructors, workloads, selectedId, onSelect }: Props) {
  const [search, setSearch] = useState('');

  const filtered = useMemo(() => {
    if (!search.trim()) return instructors;
    const q = search.toLowerCase();
    return instructors.filter(i => i.fullName.toLowerCase().includes(q));
  }, [instructors, search]);

  return (
    <div className="flex flex-col h-full border rounded-lg bg-white">
      <div className="p-3 border-b space-y-2">
        <h3 className="text-sm font-semibold">Ogretim Uyeleri ({instructors.length})</h3>
        <div className="relative">
          <Search className="absolute left-2.5 top-1/2 -translate-y-1/2 h-3.5 w-3.5 text-muted-foreground" />
          <Input
            placeholder="Ara..."
            className="h-7 pl-8 text-xs"
            value={search}
            onChange={e => setSearch(e.target.value)}
          />
        </div>
      </div>
      <ScrollArea className="flex-1">
        <div className="p-1.5 space-y-0.5">
          {filtered.map(inst => {
            const wl = workloads.get(inst.instructorId);
            const assigned = wl?.assignedWeeklyHours ?? 0;
            const max = wl?.maxWeeklyHours ?? 0;
            const pct = max > 0 ? Math.round((assigned / max) * 100) : 0;
            const barColor = pct >= 100 ? 'bg-red-500' : pct >= 80 ? 'bg-amber-500' : 'bg-emerald-500';
            const isSelected = selectedId === inst.instructorId;

            return (
              <button
                key={inst.instructorId}
                onClick={() => onSelect(inst.instructorId)}
                className={`w-full text-left px-3 py-2 rounded-md text-xs transition-colors ${
                  isSelected
                    ? 'bg-primary/10 border border-primary/30'
                    : 'hover:bg-accent border border-transparent'
                }`}
              >
                <div className="flex items-center justify-between mb-1">
                  <span className={`font-medium truncate ${isSelected ? 'text-primary' : ''}`}>
                    {inst.fullName}
                  </span>
                  {wl && (
                    <span className="text-[10px] text-muted-foreground ml-2 whitespace-nowrap">
                      {assigned}/{max}s
                    </span>
                  )}
                </div>
                {wl && (
                  <div className="h-1.5 bg-gray-100 rounded-full overflow-hidden">
                    <div
                      className={`h-full rounded-full ${barColor}`}
                      style={{ width: `${Math.min(pct, 100)}%` }}
                    />
                  </div>
                )}
              </button>
            );
          })}
          {filtered.length === 0 && (
            <p className="text-xs text-muted-foreground text-center py-4">Sonuc bulunamadi</p>
          )}
        </div>
      </ScrollArea>
    </div>
  );
}
