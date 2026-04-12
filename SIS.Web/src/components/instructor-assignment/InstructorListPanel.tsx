import { useMemo, useState } from 'react';
import { Input } from '@/components/ui/input';
import { ScrollArea } from '@/components/ui/scroll-area';
import { Skeleton } from '@/components/ui/skeleton';
import { Search } from 'lucide-react';
import type { InstructorOptionDto } from '../../types/courseOpening';

type TitleFilter = 'all' | 'prof' | 'doc' | 'dr' | 'ars';

const TITLE_FILTERS: { key: TitleFilter; label: string; match: (name: string) => boolean }[] = [
  { key: 'all', label: 'Tümü', match: () => true },
  { key: 'prof', label: 'Prof.', match: n => n.startsWith('Prof.') },
  { key: 'doc', label: 'Doç.', match: n => n.startsWith('Doç.') },
  { key: 'dr', label: 'Dr.', match: n => n.startsWith('Dr.') },
  { key: 'ars', label: 'Arş.Gör.', match: n => n.startsWith('Arş.') },
];

interface Props {
  instructors: InstructorOptionDto[];
  selectedId: number | null;
  onSelect: (id: number | null) => void;
  isLoading?: boolean;
  // Faculty/Department controls
  faculties: { id: number; name: string }[];
  selectedFacultyId: number | null;
  onFacultyChange: (id: number | null) => void;
  departments: { departmentId: number; departmentCode: string; departmentName: string }[];
  selectedDeptId: number | null;
  onDeptChange: (id: number) => void;
}

export function InstructorListPanel({
  instructors, selectedId, onSelect, isLoading,
  faculties, selectedFacultyId, onFacultyChange,
  departments, selectedDeptId, onDeptChange,
}: Props) {
  const [search, setSearch] = useState('');
  const [titleFilter, setTitleFilter] = useState<TitleFilter>('all');

  const filtered = useMemo(() => {
    let list = instructors;
    if (titleFilter !== 'all') {
      const matcher = TITLE_FILTERS.find(f => f.key === titleFilter)!.match;
      list = list.filter(i => matcher(i.fullName));
    }
    if (search.trim()) {
      const q = search.toLowerCase();
      list = list.filter(i => i.fullName.toLowerCase().includes(q));
    }
    return list;
  }, [instructors, search, titleFilter]);

  return (
    <div className="flex flex-col h-full border rounded-lg bg-white overflow-hidden">
      {/* Header */}
      <div className="p-3 border-b space-y-2 shrink-0">
        {/* Faculty / Department selectors */}
        <div className="flex gap-2">
          <select
            className="h-7 flex-1 rounded-md border border-input bg-background px-2 text-xs"
            value={selectedFacultyId ?? ''}
            onChange={e => onFacultyChange(e.target.value ? Number(e.target.value) : null)}
          >
            <option value="">Tüm Fakülteler</option>
            {faculties.map(f => (
              <option key={f.id} value={f.id}>{f.name}</option>
            ))}
          </select>
          <select
            className="h-7 flex-1 rounded-md border border-input bg-background px-2 text-xs"
            value={selectedDeptId ?? ''}
            onChange={e => onDeptChange(Number(e.target.value))}
          >
            {departments.map(d => (
              <option key={d.departmentId} value={d.departmentId}>
                {d.departmentCode} - {d.departmentName}
              </option>
            ))}
          </select>
        </div>

        {/* Title + filter chips */}
        <div className="flex items-center justify-between">
          <h3 className="text-sm font-semibold">Öğretim Üyeleri ({filtered.length})</h3>
          <div className="flex gap-1">
            {TITLE_FILTERS.map(f => (
              <button
                key={f.key}
                onClick={() => setTitleFilter(f.key)}
                className={`h-6 px-1.5 text-[11px] rounded border transition-colors ${
                  titleFilter === f.key
                    ? 'bg-primary text-primary-foreground border-primary'
                    : 'bg-background border-input hover:bg-accent'
                }`}
              >
                {f.label}
              </button>
            ))}
          </div>
        </div>

        {/* Search */}
        <div className="relative">
          <Search className="absolute left-2.5 top-1/2 -translate-y-1/2 h-3.5 w-3.5 text-muted-foreground" />
          <Input
            placeholder="İsim ara..."
            className="h-7 pl-8 text-xs"
            value={search}
            onChange={e => setSearch(e.target.value)}
          />
        </div>
      </div>

      {/* Scrollable list — contained within panel */}
      <ScrollArea className="flex-1 min-h-0">
        {isLoading ? (
          <div className="p-3 space-y-2">
            {Array.from({ length: 6 }).map((_, i) => (
              <div key={i} className="space-y-1.5 px-3 py-2">
                <Skeleton className="h-3.5 w-3/4" />
                <Skeleton className="h-1.5 w-full" />
              </div>
            ))}
          </div>
        ) : (
        <div className="p-1.5 space-y-0.5">
          {filtered.map(inst => {
            const assigned = inst.assignedWeeklyHours;
            const max = inst.maxWeeklyHours;
            const pct = max > 0 ? Math.round((assigned / max) * 100) : 0;
            const barColor = pct >= 100 ? 'bg-red-500' : pct >= 80 ? 'bg-amber-500' : 'bg-emerald-500';
            const isSelected = selectedId === inst.instructorId;

            return (
              <button
                key={inst.instructorId}
                onClick={() => onSelect(isSelected ? null : inst.instructorId)}
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
                  {max > 0 && (
                    <span className="text-[10px] text-muted-foreground ml-2 whitespace-nowrap">
                      {assigned}/{max}s
                    </span>
                  )}
                </div>
                {max > 0 && (
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
            <p className="text-xs text-muted-foreground text-center py-4">Sonuç bulunamadı</p>
          )}
        </div>
        )}
      </ScrollArea>
    </div>
  );
}
