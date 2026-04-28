import { CheckSquare, Square, Search, Link2, Pencil } from 'lucide-react';
import { Button } from '@/components/ui/button';
import type { PreviousTermCourseDto } from '../../../types/courseOpening';
import { semesterToClassLevel } from '../types';

interface TransferTableProps {
  courses: PreviousTermCourseDto[];
  selectedIds: Set<number>;
  onToggle: (course: PreviousTermCourseDto) => void;
  onEdit: (course: PreviousTermCourseDto) => void;
}

function occupancyColor(enrolled: number, capacity: number): { ratio: number; color: string } {
  if (capacity === 0) return { ratio: 0, color: 'text-muted-foreground' };
  const ratio = Math.round((enrolled / capacity) * 100);
  const color = ratio >= 90 ? 'text-red-600' : ratio >= 70 ? 'text-amber-600' : 'text-emerald-600';
  return { ratio, color };
}

export function TransferTable({ courses, selectedIds, onToggle, onEdit }: TransferTableProps) {
  return (
    <>
      <div className="overflow-auto" style={{ maxHeight: '60vh' }}>
        <table className="w-full text-sm">
          <thead className="bg-muted/50 sticky top-0 z-10">
            <tr>
              <th className="px-2 py-2 w-9" />
              <th className="px-3 py-2 text-left font-semibold">Kod</th>
              <th className="px-3 py-2 text-left font-semibold">Ad</th>
              <th className="px-2 py-2 text-center font-semibold w-10" title="Zorunlu / Seçmeli">Z/S</th>
              <th className="px-2 py-2 text-center font-semibold w-10" title="Sınıf">Sn</th>
              <th className="px-2 py-2 text-center font-semibold w-12">Krd</th>
              <th className="px-2 py-2 text-center font-semibold w-14">AKTS</th>
              <th className="px-2 py-2 text-center font-semibold w-20">Doluluk</th>
              <th className="px-2 py-2 text-right font-semibold w-16" />
            </tr>
          </thead>
          <tbody>
            {courses.map((c) => {
              const sel = selectedIds.has(c.courseId);
              const occ = occupancyColor(c.totalEnrolled, c.totalCapacity);
              const classLvl = semesterToClassLevel(c.suggestedSemester);
              return (
                <tr
                  key={c.courseId}
                  className={`group border-t transition-colors ${c.alreadyOpened ? 'opacity-50' : 'hover:bg-muted/40'} ${sel ? 'bg-blue-50 dark:bg-blue-950/20' : ''}`}
                >
                  <td className="px-2 py-2 text-center cursor-pointer" onClick={() => onToggle(c)}>
                    {sel ? (
                      <CheckSquare className="h-4 w-4 text-primary mx-auto" />
                    ) : (
                      <Square className="h-4 w-4 text-muted-foreground mx-auto" />
                    )}
                  </td>
                  <td className="px-3 py-2 font-mono font-medium cursor-pointer" onClick={() => onToggle(c)}>
                    <div className="flex items-center gap-1">
                      {c.courseCode}
                      {c.hasCrossLists && <Link2 className="h-3 w-3 text-blue-500" aria-label="Cross-list" />}
                    </div>
                  </td>
                  <td
                    className="px-3 py-2 cursor-pointer truncate max-w-[280px]"
                    onClick={() => onToggle(c)}
                    title={c.courseName}
                  >
                    {c.courseName}
                  </td>
                  <td className="px-2 py-2 text-center cursor-pointer" onClick={() => onToggle(c)}>
                    <span
                      className={`text-xs font-bold ${c.isCompulsory ? 'text-red-600' : 'text-blue-600'}`}
                      title={c.isCompulsory ? 'Zorunlu' : 'Seçmeli'}
                    >
                      {c.isCompulsory ? 'Z' : 'S'}
                    </span>
                  </td>
                  <td className="px-2 py-2 text-center cursor-pointer text-muted-foreground" onClick={() => onToggle(c)}>
                    {classLvl || '—'}
                  </td>
                  <td className="px-2 py-2 text-center cursor-pointer" onClick={() => onToggle(c)}>
                    {c.credits}
                  </td>
                  <td className="px-2 py-2 text-center cursor-pointer" onClick={() => onToggle(c)}>
                    {c.ects}
                  </td>
                  <td className="px-2 py-2 text-center cursor-pointer" onClick={() => onToggle(c)}>
                    <div className={`text-xs font-medium ${occ.color}`}>
                      {c.totalEnrolled}/{c.totalCapacity}
                    </div>
                    <div className="text-[10px] text-muted-foreground">%{occ.ratio}</div>
                  </td>
                  <td className="px-2 py-2 text-right">
                    {!c.alreadyOpened && (
                      <Button
                        variant="ghost"
                        size="sm"
                        className="h-7 px-2 text-xs opacity-0 group-hover:opacity-100 transition-opacity"
                        onClick={() => onEdit(c)}
                        title="Şube, kontenjan, hoca düzenle"
                      >
                        <Pencil className="h-3 w-3 mr-1" />
                        Düzenle
                      </Button>
                    )}
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>

      {courses.length === 0 && (
        <div className="py-12 text-center">
          <Search className="h-9 w-9 text-muted-foreground/30 mx-auto mb-2" />
          <p className="text-sm text-muted-foreground">Filtrelerle eşleşen ders yok.</p>
        </div>
      )}
    </>
  );
}
