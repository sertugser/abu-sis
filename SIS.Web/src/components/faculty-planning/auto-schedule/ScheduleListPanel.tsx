import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Search } from 'lucide-react';
import { ScrollArea } from '@/components/ui/scroll-area';
import type { AutoSchedulePreview, FacultyPlanningSection } from '../../../types/facultyPlanning';
import { ScheduleListItem } from './ScheduleListItem';
import type { ListItemData } from './ScheduleListItem';

type FilterMode = 'all' | 'success' | 'failure';

interface ScheduleListPanelProps {
  previewData: AutoSchedulePreview | undefined;
  unscheduledSections: FacultyPlanningSection[] | undefined;
  selectedItemId: number | null;
  onSelectItem: (sectionId: number) => void;
  selectedIds: Set<number>;
  onToggleAll: () => void;
  onApply: (onlySelected: boolean) => void;
  applyPending: boolean;
}

export function ScheduleListPanel({
  previewData,
  unscheduledSections,
  selectedItemId,
  onSelectItem,
  selectedIds,
  onToggleAll,
  onApply,
  applyPending,
}: ScheduleListPanelProps) {
  const [filter, setFilter] = useState<FilterMode>('all');
  const [search, setSearch] = useState('');

  const searchLower = search.toLowerCase();

  // Pre-run: zamanlanmamış şubeleri göster
  if (!previewData) {
    const filtered = (unscheduledSections ?? []).filter(s =>
      !search || s.courseCode.toLowerCase().includes(searchLower) || s.courseName.toLowerCase().includes(searchLower),
    );

    return (
      <div className="w-[360px] border-r border-border/50 flex flex-col shrink-0">
        <div className="px-3.5 py-2.5 border-b border-border/50 flex items-center justify-between">
          <span className="text-xs font-semibold text-foreground">Zamanlanmamış Şubeler</span>
          <Badge variant="outline" className="text-[10px]">{filtered.length}</Badge>
        </div>
        <div className="px-3.5 py-2 border-b border-border/50">
          <div className="flex items-center gap-2 bg-muted/50 border border-border/50 rounded-md px-2.5 py-1.5">
            <Search className="h-3.5 w-3.5 text-muted-foreground" />
            <input
              type="text"
              placeholder="Ders veya bölüm ara..."
              className="bg-transparent text-xs outline-none flex-1 text-foreground placeholder:text-muted-foreground"
              value={search}
              onChange={e => setSearch(e.target.value)}
            />
          </div>
        </div>
        <ScrollArea className="flex-1">
          {filtered.map(s => (
            <ScheduleListItem
              key={s.sectionId}
              data={{ kind: 'unscheduled', item: s }}
              isSelected={selectedItemId === s.sectionId}
              onClick={() => onSelectItem(s.sectionId)}
            />
          ))}
        </ScrollArea>
      </div>
    );
  }

  // Post-run: sonuç listesi
  const items: ListItemData[] = [];

  if (filter !== 'failure') {
    for (const a of previewData.assignments) {
      if (search && !a.courseCode.toLowerCase().includes(searchLower) && !a.courseName.toLowerCase().includes(searchLower)) continue;
      items.push({ kind: 'success', item: a });
    }
  }

  if (filter !== 'success') {
    for (const f of previewData.failures) {
      if (search && !f.courseCode.toLowerCase().includes(searchLower) && !f.courseName.toLowerCase().includes(searchLower)) continue;
      items.push({ kind: 'failure', item: f });
    }
  }

  const allSelected = previewData.assignments.length > 0 && previewData.assignments.every(a => selectedIds.has(a.sectionId));

  return (
    <div className="w-[360px] border-r border-border/50 flex flex-col shrink-0">
      {/* Filters */}
      <div className="px-3.5 py-2.5 border-b border-border/50 flex gap-1.5">
        <button
          type="button"
          onClick={() => setFilter('all')}
          className={`px-2.5 py-1 rounded-full text-[11px] font-medium transition-colors ${
            filter === 'all' ? 'bg-muted text-foreground' : 'text-muted-foreground hover:text-foreground'
          }`}
        >
          Tümü ({previewData.assignments.length + previewData.failures.length})
        </button>
        <button
          type="button"
          onClick={() => setFilter('success')}
          className={`px-2.5 py-1 rounded-full text-[11px] font-medium transition-colors ${
            filter === 'success' ? 'bg-emerald-500/15 text-emerald-600' : 'text-muted-foreground hover:text-foreground'
          }`}
        >
          ✓ Başarılı ({previewData.assignments.length})
        </button>
        <button
          type="button"
          onClick={() => setFilter('failure')}
          className={`px-2.5 py-1 rounded-full text-[11px] font-medium transition-colors ${
            filter === 'failure' ? 'bg-red-500/15 text-red-600' : 'text-muted-foreground hover:text-foreground'
          }`}
        >
          ✗ Başarısız ({previewData.failures.length})
        </button>
      </div>

      {/* Search */}
      <div className="px-3.5 py-2 border-b border-border/50">
        <div className="flex items-center gap-2 bg-muted/50 border border-border/50 rounded-md px-2.5 py-1.5">
          <Search className="h-3.5 w-3.5 text-muted-foreground" />
          <input
            type="text"
            placeholder="Ders veya bölüm ara..."
            className="bg-transparent text-xs outline-none flex-1 text-foreground placeholder:text-muted-foreground"
            value={search}
            onChange={e => setSearch(e.target.value)}
          />
        </div>
      </div>

      {/* List */}
      <ScrollArea className="flex-1">
        {items.map(d => (
          <ScheduleListItem
            key={d.item.sectionId}
            data={d}
            isSelected={selectedItemId === d.item.sectionId}
            onClick={() => onSelectItem(d.item.sectionId)}
          />
        ))}
      </ScrollArea>

      {/* Bottom bar: Select all + Apply */}
      {previewData.assignments.length > 0 && (
        <div className="px-3.5 py-2.5 border-t border-border/50 flex items-center justify-between">
          <label className="flex items-center gap-2 cursor-pointer">
            <input
              type="checkbox"
              checked={allSelected}
              onChange={onToggleAll}
              className="rounded accent-indigo-600"
            />
            <span className="text-xs text-muted-foreground">Tümünü Seç</span>
          </label>
          <Button
            size="sm"
            onClick={() => onApply(selectedIds.size > 0 && selectedIds.size < previewData.assignments.length)}
            disabled={applyPending}
            className="text-xs"
          >
            {selectedIds.size > 0
              ? `Seçilenleri Uygula (${selectedIds.size})`
              : `Tümünü Uygula (${previewData.assignments.length})`}
          </Button>
        </div>
      )}
    </div>
  );
}
