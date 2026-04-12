import { useCallback, useMemo, useState } from 'react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { ScrollArea } from '@/components/ui/scroll-area';
import { Badge } from '@/components/ui/badge';
import { Skeleton } from '@/components/ui/skeleton';
import { UserPlus, Copy, Search } from 'lucide-react';
import type { OpenedCourseSectionDto, InstructorOptionDto } from '../../types/courseOpening';

type AssignFilter = 'all' | 'unassigned' | 'assigned';

interface Props {
  sections: OpenedCourseSectionDto[];
  instructors: InstructorOptionDto[];
  selectedInstructorId: number | null;
  inlineAssignments: Map<number, number>;
  onInlineAssign: (sectionId: number, instructorId: number) => void;
  selectedSectionIds: Set<number>;
  onClearInvisibleSelections: (visibleIds: number[]) => void;
  onToggleSection: (sectionId: number) => void;
  onToggleAll: (visibleIds: number[]) => void;
  onBulkAssign: () => void;
  onCopyPreviousTerm: () => void;
  bulkAssigning: boolean;
  isLoading?: boolean;
}

export function SectionAssignmentTable({
  sections,
  instructors,
  selectedInstructorId,
  inlineAssignments,
  onInlineAssign,
  selectedSectionIds,
  onClearInvisibleSelections,
  onToggleSection,
  onToggleAll,
  onBulkAssign,
  onCopyPreviousTerm,
  bulkAssigning,
  isLoading,
}: Props) {
  const [filter, setFilterRaw] = useState<AssignFilter>('all');
  const [search, setSearch] = useState('');

  const setFilter = useCallback((f: AssignFilter) => {
    setFilterRaw(f);
    const nextVisible = f === 'all'
      ? sections
      : f === 'unassigned'
        ? sections.filter(s => !s.instructorId)
        : sections.filter(s => !!s.instructorId);
    onClearInvisibleSelections(nextVisible.map(s => s.sectionId));
  }, [sections, onClearInvisibleSelections]);

  const filtered = useMemo(() => {
    let list = sections;
    if (filter === 'unassigned') list = list.filter(s => !s.instructorId);
    if (filter === 'assigned') list = list.filter(s => !!s.instructorId);
    if (search.trim()) {
      const q = search.toLowerCase();
      list = list.filter(s =>
        s.courseCode.toLowerCase().includes(q) ||
        s.courseName.toLowerCase().includes(q) ||
        (s.instructorName && s.instructorName.toLowerCase().includes(q))
      );
    }
    return list;
  }, [sections, filter, search]);

  const unassignedCount = useMemo(() => sections.filter(s => !s.instructorId).length, [sections]);

  const filteredIds = useMemo(() => filtered.map(s => s.sectionId), [filtered]);
  const allChecked = filtered.length > 0 && filtered.every(s => selectedSectionIds.has(s.sectionId));

  return (
    <div className="flex flex-col h-full border rounded-lg bg-white overflow-hidden">
      {/* Header — symmetric with InstructorListPanel */}
      <div className="p-3 border-b space-y-2 shrink-0">
        <div className="flex items-center justify-between">
          <h3 className="text-sm font-semibold">
            Ders–Şube Tablosu ({sections.length})
            {unassignedCount > 0 && (
              <Badge variant="warning" className="ml-2 text-[10px]">
                {unassignedCount} atanmamış
              </Badge>
            )}
          </h3>
          <div className="flex gap-1">
            {(['all', 'unassigned', 'assigned'] as const).map(f => (
              <button
                key={f}
                onClick={() => setFilter(f)}
                className={`h-6 px-2 text-[11px] rounded border transition-colors ${
                  filter === f
                    ? 'bg-primary text-primary-foreground border-primary'
                    : 'bg-background border-input hover:bg-accent'
                }`}
              >
                {f === 'all' ? 'Tümü' : f === 'unassigned' ? 'Atanmamış' : 'Atanmış'}
              </button>
            ))}
          </div>
        </div>
        <div className="relative">
          <Search className="absolute left-2.5 top-1/2 -translate-y-1/2 h-3.5 w-3.5 text-muted-foreground" />
          <Input
            placeholder="Ders kodu veya adı ara..."
            className="h-7 pl-8 text-xs"
            value={search}
            onChange={e => setSearch(e.target.value)}
          />
        </div>
      </div>

      {/* Table */}
      <ScrollArea className="flex-1 min-h-0">
        <div className="overflow-x-auto">
        <table className="w-full text-xs min-w-[640px]">
          <thead className="sticky top-0 bg-muted/50 z-10">
            <tr className="border-b">
              <th className="p-2 w-8">
                <input
                  type="checkbox"
                  checked={allChecked}
                  onChange={() => onToggleAll(filteredIds)}
                  className="rounded border-gray-300"
                />
              </th>
              <th className="p-2 text-left font-medium">Ders Kodu</th>
              <th className="p-2 text-left font-medium">Ders Adı</th>
              <th className="p-2 text-center font-medium">Şube</th>
              <th className="p-2 text-center font-medium">T+U</th>
              <th className="p-2 text-left font-medium">Mevcut Hoca</th>
              <th className="p-2 text-left font-medium min-w-[180px]">Yeni Hoca</th>
            </tr>
          </thead>
          <tbody>
            {filtered.map(section => {
              const isChecked = selectedSectionIds.has(section.sectionId);
              const noInstructor = !section.instructorId;
              const inlineVal = inlineAssignments.get(section.sectionId) ?? '';

              return (
                <tr
                  key={section.sectionId}
                  className={`border-b transition-colors ${
                    noInstructor ? 'bg-amber-50/60' : ''
                  } ${isChecked ? 'bg-primary/5' : 'hover:bg-accent/50'}`}
                >
                  <td className="p-2 text-center">
                    <input
                      type="checkbox"
                      checked={isChecked}
                      onChange={() => onToggleSection(section.sectionId)}
                      className="rounded border-gray-300"
                    />
                  </td>
                  <td className="p-2 font-mono">{section.courseCode}</td>
                  <td className="p-2 truncate max-w-[200px]">{section.courseName}</td>
                  <td className="p-2 text-center">{section.sectionNumber}</td>
                  <td className="p-2 text-center">
                    {section.theoryHours}+{section.labHours}
                  </td>
                  <td className="p-2">
                    {section.instructorName ? (
                      <span className="text-muted-foreground">{section.instructorName}</span>
                    ) : (
                      <span className="text-amber-600 italic">Atanmamış</span>
                    )}
                  </td>
                  <td className="p-2">
                    <select
                      className="h-7 w-full rounded-md border border-input bg-background px-2 text-xs"
                      value={inlineVal}
                      onChange={e => {
                        if (e.target.value) onInlineAssign(section.sectionId, Number(e.target.value));
                      }}
                    >
                      <option value="">-- Seç --</option>
                      {instructors.map(i => (
                        <option key={i.instructorId} value={i.instructorId}>
                          {i.fullName}
                        </option>
                      ))}
                    </select>
                  </td>
                </tr>
              );
            })}
            {isLoading && filtered.length === 0 && (
              Array.from({ length: 5 }).map((_, i) => (
                <tr key={`skel-${i}`} className="border-b">
                  <td className="p-2"><Skeleton className="h-4 w-4" /></td>
                  <td className="p-2"><Skeleton className="h-3.5 w-16" /></td>
                  <td className="p-2"><Skeleton className="h-3.5 w-32" /></td>
                  <td className="p-2"><Skeleton className="h-3.5 w-6 mx-auto" /></td>
                  <td className="p-2"><Skeleton className="h-3.5 w-10 mx-auto" /></td>
                  <td className="p-2"><Skeleton className="h-3.5 w-28" /></td>
                  <td className="p-2"><Skeleton className="h-7 w-full" /></td>
                </tr>
              ))
            )}
            {!isLoading && filtered.length === 0 && (
              <tr>
                <td colSpan={7} className="p-8 text-center text-muted-foreground">
                  <p className="text-sm">Gösterilecek şube bulunamadı</p>
                  <p className="text-[11px] mt-1">Bu bölüm ve dönem için henüz ders şubesi açılmamış olabilir.</p>
                </td>
              </tr>
            )}
          </tbody>
        </table>
        </div>
      </ScrollArea>

      {/* Footer Actions */}
      <div className="p-3 border-t flex flex-wrap items-center gap-2 shrink-0">
        <Button
          size="sm"
          className="text-xs h-8"
          disabled={selectedSectionIds.size === 0 || !selectedInstructorId || bulkAssigning}
          onClick={onBulkAssign}
        >
          <UserPlus className="h-3.5 w-3.5 mr-1.5" />
          Seçilenlere Ata ({selectedSectionIds.size})
        </Button>
        <Button size="sm" variant="outline" className="text-xs h-8" onClick={onCopyPreviousTerm} disabled title="Yakın zamanda eklenecek">
          <Copy className="h-3.5 w-3.5 mr-1.5" />
          Önceki Dönemden Kopyala
        </Button>
        {selectedInstructorId && (
          <span className="text-[11px] text-muted-foreground ml-auto">
            Seçili öğretim üyesi ile toplu atama yapılacak
          </span>
        )}
      </div>
    </div>
  );
}
