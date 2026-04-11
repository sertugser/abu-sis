import { useMemo, useState } from 'react';
import { Button } from '@/components/ui/button';
import { ScrollArea } from '@/components/ui/scroll-area';
import { Badge } from '@/components/ui/badge';
import { UserPlus, Copy } from 'lucide-react';
import type { OpenedCourseSectionDto, InstructorOptionDto } from '../../types/courseOpening';

type AssignFilter = 'all' | 'unassigned' | 'assigned';

interface Props {
  sections: OpenedCourseSectionDto[];
  instructors: InstructorOptionDto[];
  selectedInstructorId: number | null;
  /** Tracks per-row inline dropdown overrides: sectionId -> instructorId */
  inlineAssignments: Map<number, number>;
  onInlineAssign: (sectionId: number, instructorId: number) => void;
  selectedSectionIds: Set<number>;
  onToggleSection: (sectionId: number) => void;
  onToggleAll: () => void;
  onBulkAssign: () => void;
  onCopyPreviousTerm: () => void;
  bulkAssigning: boolean;
}

export function SectionAssignmentTable({
  sections,
  instructors,
  selectedInstructorId,
  inlineAssignments,
  onInlineAssign,
  selectedSectionIds,
  onToggleSection,
  onToggleAll,
  onBulkAssign,
  onCopyPreviousTerm,
  bulkAssigning,
}: Props) {
  const [filter, setFilter] = useState<AssignFilter>('all');

  const filtered = useMemo(() => {
    if (filter === 'unassigned') return sections.filter(s => !s.instructorId);
    if (filter === 'assigned') return sections.filter(s => !!s.instructorId);
    return sections;
  }, [sections, filter]);

  const unassignedCount = useMemo(() => sections.filter(s => !s.instructorId).length, [sections]);

  const allChecked = filtered.length > 0 && filtered.every(s => selectedSectionIds.has(s.sectionId));

  return (
    <div className="flex flex-col h-full border rounded-lg bg-white">
      {/* Header */}
      <div className="p-3 border-b space-y-2">
        <div className="flex items-center justify-between">
          <h3 className="text-sm font-semibold">
            Ders-Sube Tablosu ({sections.length})
            {unassignedCount > 0 && (
              <Badge variant="warning" className="ml-2 text-[10px]">
                {unassignedCount} atanmamis
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
                {f === 'all' ? 'Tumu' : f === 'unassigned' ? 'Atanmamis' : 'Atanmis'}
              </button>
            ))}
          </div>
        </div>
      </div>

      {/* Table */}
      <ScrollArea className="flex-1">
        <div className="overflow-x-auto">
        <table className="w-full text-xs min-w-[640px]">
          <thead className="sticky top-0 bg-muted/50 z-10">
            <tr className="border-b">
              <th className="p-2 w-8">
                <input
                  type="checkbox"
                  checked={allChecked}
                  onChange={onToggleAll}
                  className="rounded border-gray-300"
                />
              </th>
              <th className="p-2 text-left font-medium">Ders Kodu</th>
              <th className="p-2 text-left font-medium">Ders Adi</th>
              <th className="p-2 text-center font-medium">Sube</th>
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
                      <span className="text-amber-600 italic">Atanmamis</span>
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
                      <option value="">-- Sec --</option>
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
            {filtered.length === 0 && (
              <tr>
                <td colSpan={7} className="p-6 text-center text-muted-foreground">
                  Gosterilecek sube bulunamadi
                </td>
              </tr>
            )}
          </tbody>
        </table>
        </div>
      </ScrollArea>

      {/* Footer Actions */}
      <div className="p-3 border-t flex flex-wrap items-center gap-2">
        <Button
          size="sm"
          className="text-xs h-8"
          disabled={selectedSectionIds.size === 0 || !selectedInstructorId || bulkAssigning}
          onClick={onBulkAssign}
        >
          <UserPlus className="h-3.5 w-3.5 mr-1.5" />
          Secilenlere Ata ({selectedSectionIds.size})
        </Button>
        <Button size="sm" variant="outline" className="text-xs h-8" onClick={onCopyPreviousTerm}>
          <Copy className="h-3.5 w-3.5 mr-1.5" />
          Onceki Donemden Kopyala
        </Button>
        {selectedInstructorId && (
          <span className="text-[11px] text-muted-foreground ml-auto">
            Secili ogretim uyesi ile toplu atama yapilacak
          </span>
        )}
      </div>
    </div>
  );
}
