import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/components/ui/select';
import { ChevronDown, ChevronRight, Info, Loader2, Pencil, Plus, Trash2, Check, X } from 'lucide-react';
import { useState } from 'react';
import type { CurriculumCourseItemDto } from '../../types/curriculumManagement';

// Seçmeli ders kodlarını okunabilir slot ismine çevirir: TE-I → "Technical Elective I"
const ELECTIVE_PREFIX_MAP: Record<string, string> = {
  'TE': 'Teknik Seçmeli',
  'NDE': 'Bölüm Dışı Seçmeli',
  'OC': 'Ortak Seçmeli',
};

function getElectiveSlotName(courseCode: string): string | null {
  const match = courseCode.match(/^(TE|NDE|OC)-(.+)$/);
  if (!match) return null;
  const prefix = ELECTIVE_PREFIX_MAP[match[1]];
  return prefix ? `${prefix} ${match[2]}` : null;
}

interface Props {
  semester: number;
  courses: CurriculumCourseItemDto[];
  onUpdateCourse: (courseId: number, req: { suggestedSemester?: number; isCompulsory?: boolean }) => void;
  onRemoveCourse: (courseId: number) => void;
  onAddCourse: () => void;
  onShowCourseDetail?: (courseId: number) => void;
  isUpdating: boolean;
  isRemoving: boolean;
}

export default function SemesterGroup({
  semester,
  courses,
  onUpdateCourse,
  onRemoveCourse,
  onAddCourse,
  onShowCourseDetail,
  isUpdating,
  isRemoving,
}: Props) {
  const [editingCourseId, setEditingCourseId] = useState<number | null>(null);
  const [editSemester, setEditSemester] = useState(semester);
  const [collapsed, setCollapsed] = useState(false);

  // Bulk selection state
  const [selectedIds, setSelectedIds] = useState<Set<number>>(new Set());
  const [bulkTargetSemester, setBulkTargetSemester] = useState<string>('');

  const totalEcts = courses.reduce((sum, c) => sum + c.ects, 0);
  const totalTheory = courses.reduce((sum, c) => sum + c.theoryHours, 0);
  const totalLab = courses.reduce((sum, c) => sum + c.labHours, 0);


  function handleSaveSemester(courseId: number) {
    if (editSemester !== semester) {
      onUpdateCourse(courseId, { suggestedSemester: editSemester });
    }
    setEditingCourseId(null);
  }

  function toggleSelect(courseId: number) {
    setSelectedIds(prev => {
      const next = new Set(prev);
      if (next.has(courseId)) next.delete(courseId);
      else next.add(courseId);
      return next;
    });
  }

  function toggleSelectAll() {
    if (selectedIds.size === courses.length) {
      setSelectedIds(new Set());
    } else {
      setSelectedIds(new Set(courses.map(c => c.courseId)));
    }
  }

  function handleBulkMove() {
    const target = Number(bulkTargetSemester);
    if (!target || target === semester) return;
    for (const courseId of selectedIds) {
      onUpdateCourse(courseId, { suggestedSemester: target });
    }
    setSelectedIds(new Set());
    setBulkTargetSemester('');
  }

  const allSelected = courses.length > 0 && selectedIds.size === courses.length;
  const someSelected = selectedIds.size > 0;

  return (
    <div className="mb-6">
      {/* Collapsible header */}
      <button
        className="flex items-center gap-2 mb-2 w-full text-left hover:opacity-80 transition-opacity"
        onClick={() => setCollapsed(v => !v)}
      >
        {collapsed
          ? <ChevronRight className="h-4 w-4 text-muted-foreground shrink-0" />
          : <ChevronDown className="h-4 w-4 text-muted-foreground shrink-0" />}
        <span className="text-sm">
          <span className="font-semibold">{semester}. Yarıyıl</span>
          <span className="text-muted-foreground ml-2 font-normal">
            {totalEcts} AKTS
          </span>
        </span>
      </button>

      {!collapsed && (
        <div className="border rounded-md overflow-hidden overflow-x-auto">
          <table className="w-full text-sm min-w-[640px]">
            <thead>
              <tr className="bg-muted/50 border-b">
                <th className="text-center px-2 py-1.5 w-10">
                  <input
                    type="checkbox"
                    checked={allSelected}
                    ref={el => { if (el) el.indeterminate = someSelected && !allSelected; }}
                    onChange={toggleSelectAll}
                    className="h-3.5 w-3.5 rounded border-border accent-primary cursor-pointer"
                  />
                </th>
                <th className="text-left px-3 py-1.5 font-medium w-28">Ders Kodu</th>
                <th className="text-left px-3 py-1.5 font-medium">Ders Adı</th>
                <th className="text-center px-3 py-1.5 font-medium w-16">AKTS</th>
                <th className="text-center px-3 py-1.5 font-medium w-16">T+U</th>
                <th className="text-center px-3 py-1.5 font-medium w-16">Yarıyıl</th>
                <th className="text-center px-3 py-1.5 font-medium w-24">Tür</th>
                <th className="text-center px-3 py-1.5 font-medium w-24">İşlem</th>
              </tr>
            </thead>
            <tbody>
              {courses.map(c => (
                <tr
                  key={c.courseId}
                  className={`border-b last:border-b-0 hover:bg-muted/30 transition-colors group ${
                    selectedIds.has(c.courseId) ? 'bg-primary/5' : ''
                  }`}
                >
                  <td className="text-center px-2 py-1.5 w-10">
                    <input
                      type="checkbox"
                      checked={selectedIds.has(c.courseId)}
                      onChange={() => toggleSelect(c.courseId)}
                      className="h-3.5 w-3.5 rounded border-border accent-primary cursor-pointer"
                    />
                  </td>
                  <td className="px-3 py-1.5 font-mono text-xs w-28">
                    {!c.isCompulsory && getElectiveSlotName(c.courseCode) ? '—' : c.courseCode}
                  </td>
                  <td className={`px-3 py-1.5 ${!c.isCompulsory && getElectiveSlotName(c.courseCode) ? 'italic text-muted-foreground' : ''}`}>
                    {getElectiveSlotName(c.courseCode) && !c.isCompulsory
                      ? getElectiveSlotName(c.courseCode)
                      : c.courseName}
                  </td>
                  <td className="text-center px-3 py-1.5 w-16">{c.ects}</td>
                  <td className="text-center px-3 py-1.5 w-16">{c.theoryHours}+{c.labHours}</td>
                  <td className="text-center px-3 py-1.5 w-16">
                    {editingCourseId === c.courseId ? (
                      <div className="flex items-center gap-1 justify-center">
                        <Input
                          type="number"
                          min={1}
                          max={12}
                          value={editSemester}
                          onChange={e => setEditSemester(Number(e.target.value))}
                          className="h-7 w-14 text-center text-xs"
                        />
                        <Button
                          size="icon"
                          variant="ghost"
                          className="h-6 w-6"
                          onClick={() => handleSaveSemester(c.courseId)}
                          disabled={isUpdating}
                        >
                          {isUpdating
                            ? <Loader2 className="h-3 w-3 animate-spin" />
                            : <Check className="h-3 w-3" />}
                        </Button>
                        <Button
                          size="icon"
                          variant="ghost"
                          className="h-6 w-6"
                          onClick={() => setEditingCourseId(null)}
                        >
                          <X className="h-3 w-3" />
                        </Button>
                      </div>
                    ) : (
                      c.suggestedSemester
                    )}
                  </td>
                  <td className="text-center px-3 py-1.5 w-24">
                    <button
                      onClick={() => onUpdateCourse(c.courseId, { isCompulsory: !c.isCompulsory })}
                      disabled={isUpdating}
                      className="cursor-pointer"
                    >
                      <Badge variant={c.isCompulsory ? 'default' : 'outline'} className="text-[10px]">
                        {c.isCompulsory ? 'Zorunlu' : 'Seçmeli'}
                      </Badge>
                    </button>
                  </td>
                  <td className="text-center px-3 py-1.5 w-24">
                    <div className="flex items-center justify-center gap-1">
                      {onShowCourseDetail && (
                        <Button
                          size="icon"
                          variant="ghost"
                          className="h-6 w-6"
                          onClick={() => onShowCourseDetail(c.courseId)}
                          title="Detay"
                        >
                          <Info className="h-3 w-3 text-muted-foreground" />
                        </Button>
                      )}
                      <div className="flex gap-0.5 opacity-0 group-hover:opacity-100 transition-opacity">
                        <Button
                          size="icon"
                          variant="ghost"
                          className="h-6 w-6 text-muted-foreground hover:text-foreground"
                          onClick={() => {
                            setEditingCourseId(c.courseId);
                            setEditSemester(c.suggestedSemester);
                          }}
                          title="Yarıyıl Değiştir"
                        >
                          <Pencil className="h-3 w-3" />
                        </Button>
                        <Button
                          size="icon"
                          variant="ghost"
                          className="h-6 w-6 text-destructive hover:text-destructive"
                          onClick={() => onRemoveCourse(c.courseId)}
                          disabled={isRemoving}
                          title="Dersi Çıkar"
                        >
                          {isRemoving
                            ? <Loader2 className="h-3 w-3 animate-spin" />
                            : <Trash2 className="h-3 w-3" />}
                        </Button>
                      </div>
                    </div>
                  </td>
                </tr>
              ))}
              {/* + Ders Ekle per semester */}
              <tr>
                <td colSpan={8} className="px-3 py-1.5">
                  <button
                    onClick={onAddCourse}
                    className="text-xs text-primary hover:underline flex items-center gap-1 font-medium py-1.5"
                  >
                    <Plus className="h-3.5 w-3.5" /> Bu yarıyıla ders ekle
                  </button>
                </td>
              </tr>
            </tbody>
            <tfoot>
              <tr className="bg-muted/50 font-semibold text-xs border-t-2">
                <td className="px-2 py-1.5" />
                <td className="px-3 py-1.5" colSpan={2}>Toplam: {courses.length} ders</td>
                <td className="text-center px-3 py-1.5">{totalEcts}</td>
                <td className="text-center px-3 py-1.5">{totalTheory}+{totalLab}</td>
                <td colSpan={3} />
              </tr>
            </tfoot>
          </table>

          {/* Bulk move action bar */}
          {someSelected && (
            <div className="flex items-center gap-3 px-3 py-2.5 bg-primary/5 border-t text-sm">
              <span className="font-medium">{selectedIds.size} ders seçili</span>
              <div className="flex items-center gap-2">
                <span className="text-muted-foreground">Yarıyıl:</span>
                <Select value={bulkTargetSemester} onValueChange={setBulkTargetSemester}>
                  <SelectTrigger className="h-8 w-24">
                    <SelectValue placeholder="Seç" />
                  </SelectTrigger>
                  <SelectContent>
                    {Array.from({ length: 8 }, (_, i) => i + 1).map(s => (
                      <SelectItem key={s} value={s.toString()}>{s}. Yarıyıl</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                <Button
                  size="sm"
                  className="h-8"
                  onClick={handleBulkMove}
                  disabled={!bulkTargetSemester || Number(bulkTargetSemester) === semester || isUpdating}
                >
                  {isUpdating ? <Loader2 className="h-3.5 w-3.5 animate-spin mr-1" /> : null}
                  Taşı
                </Button>
                <Button
                  size="sm"
                  variant="ghost"
                  className="h-8"
                  onClick={() => { setSelectedIds(new Set()); setBulkTargetSemester(''); }}
                >
                  İptal
                </Button>
              </div>
            </div>
          )}
        </div>
      )}
    </div>
  );
}
