import { useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import { CheckSquare, Square, Search, Link2, Pencil } from 'lucide-react';
import { Button } from '@/components/ui/button';
import type { PreviousTermCourseDto } from '../../../types/courseOpening';
import { semesterToClassLevel } from '../types';
import { TablePagination } from './TablePagination';

interface TransferTableProps {
  courses: PreviousTermCourseDto[];
  selectedIds: Set<number>;
  onToggle: (course: PreviousTermCourseDto) => void;
  onEdit: (course: PreviousTermCourseDto) => void;
  /** Yeni: header checkbox — filtrelenenleri tek tıkla seç/kaldır. */
  onToggleSelectAll?: () => void;
  /** none: hiçbiri · some: bazısı (indeterminate) · all: hepsi seçili */
  selectAllState?: 'none' | 'some' | 'all';
  /** Pagination satırının sayaç-kontrol arası ek bilgi alanı (seçim özeti, toggle vb.). */
  paginationExtraInfo?: ReactNode;
  /** Pagination satırının sağ ucuna eklenecek ana aksiyon (Aktar butonu vb.). */
  paginationRightAction?: ReactNode;
}

export function TransferTable({
  courses,
  selectedIds,
  onToggle,
  onEdit,
  onToggleSelectAll,
  selectAllState = 'none',
  paginationExtraInfo,
  paginationRightAction,
}: TransferTableProps) {
  const headerCheckboxRef = useRef<HTMLInputElement>(null);
  // indeterminate (bazısı seçili) durumu sadece DOM property — class değil
  useEffect(() => {
    if (headerCheckboxRef.current) {
      headerCheckboxRef.current.indeterminate = selectAllState === 'some';
    }
  }, [selectAllState]);

  // ── Sayfalama ────────────────────────────────────────────────────────────
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(25);
  // Filtre değişip toplam azalınca page clamp'lenir, ama state'i de düşür
  const totalPages = Math.max(1, Math.ceil(courses.length / pageSize));
  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    if (page > totalPages) setPage(totalPages);
  }, [page, totalPages]);
  const safePage = Math.min(page, totalPages);
  const pageItems = useMemo(
    () => courses.slice((safePage - 1) * pageSize, safePage * pageSize),
    [courses, safePage, pageSize],
  );

  return (
    <div className="flex flex-col flex-1 min-h-0">
      <div className="flex-1 min-h-0 overflow-auto">
        <table className="w-full text-xs">
          <thead className="bg-muted/50 sticky top-0 z-10">
            <tr>
              <th className="px-2 py-2 w-8 text-center">
                {onToggleSelectAll && (
                  <input
                    ref={headerCheckboxRef}
                    type="checkbox"
                    className="h-3.5 w-3.5 cursor-pointer"
                    checked={selectAllState === 'all'}
                    onChange={onToggleSelectAll}
                    title={
                      selectAllState === 'all'
                        ? 'Tümünün seçimini kaldır'
                        : 'Filtreyle eşleşen tüm aktarılabilir dersleri seç'
                    }
                  />
                )}
              </th>
              <th className="px-2 py-2 text-left font-semibold">Kod</th>
              <th className="px-2 py-2 text-left font-semibold">Ad</th>
              <th className="px-1 py-2 text-center font-semibold w-9" title="Zorunlu / Seçmeli">Z/S</th>
              <th className="px-1 py-2 text-center font-semibold w-9" title="Sınıf">Sn</th>
              <th className="px-1 py-2 text-center font-semibold w-10">Krd</th>
              <th className="px-1 py-2 text-center font-semibold w-12">AKTS</th>
              <th className="px-1 py-2 text-right font-semibold w-14" />
            </tr>
          </thead>
          <tbody>
            {pageItems.map((c, idx) => {
              const sel = selectedIds.has(c.courseId);
              const classLvl = semesterToClassLevel(c.suggestedSemester);
              // Zebra striping — göz takibi için tek/çift satır farklı arka plan
              const zebra = idx % 2 === 1 ? 'bg-muted/20' : '';
              return (
                <tr
                  key={c.courseId}
                  className={`group border-t transition-colors ${c.alreadyOpened ? 'opacity-50' : 'hover:bg-muted/40'} ${sel ? 'bg-blue-50 dark:bg-blue-950/20' : zebra}`}
                >
                  <td className="px-2 py-2 text-center cursor-pointer" onClick={() => onToggle(c)}>
                    {sel ? (
                      <CheckSquare className="h-3.5 w-3.5 text-primary mx-auto" />
                    ) : (
                      <Square className="h-3.5 w-3.5 text-muted-foreground mx-auto" />
                    )}
                  </td>
                  <td className="px-2 py-2 font-mono font-medium cursor-pointer" onClick={() => onToggle(c)}>
                    <div className="flex items-center gap-1">
                      {c.courseCode}
                      {c.hasCrossLists && <Link2 className="h-3 w-3 text-blue-500" aria-label="Cross-list" />}
                    </div>
                  </td>
                  <td
                    className="px-2 py-2 cursor-pointer truncate max-w-[280px]"
                    onClick={() => onToggle(c)}
                    title={c.courseName}
                  >
                    {c.courseName}
                  </td>
                  <td className="px-1 py-2 text-center cursor-pointer" onClick={() => onToggle(c)}>
                    <span
                      className={`font-bold ${c.isCompulsory ? 'text-red-600' : 'text-blue-600'}`}
                      title={c.isCompulsory ? 'Zorunlu' : 'Seçmeli'}
                    >
                      {c.isCompulsory ? 'Z' : 'S'}
                    </span>
                  </td>
                  <td className="px-1 py-2 text-center cursor-pointer text-muted-foreground" onClick={() => onToggle(c)}>
                    {classLvl || '—'}
                  </td>
                  <td className="px-1 py-2 text-center cursor-pointer" onClick={() => onToggle(c)}>
                    {c.credits}
                  </td>
                  <td className="px-1 py-2 text-center cursor-pointer" onClick={() => onToggle(c)}>
                    {c.ects}
                  </td>
                  <td className="px-1 py-2 text-right">
                    {!c.alreadyOpened && (
                      <Button
                        variant="ghost"
                        size="sm"
                        className="h-6 px-1.5 text-xs opacity-0 group-hover:opacity-100 transition-opacity"
                        onClick={() => onEdit(c)}
                        title="Şube, kontenjan, hoca düzenle"
                      >
                        <Pencil className="h-3 w-3 mr-0.5" />
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
        <div className="flex-1 flex flex-col items-center justify-center py-12">
          <Search className="h-9 w-9 text-muted-foreground/30 mb-2" />
          <p className="text-sm text-muted-foreground">Filtrelerle eşleşen ders yok.</p>
        </div>
      )}

      {/* Sayfalama — daima dipte (sticky bottom). Veri yoksa da dipte küçük placeholder. */}
      <div className="shrink-0 mt-auto">
        <TablePagination
          totalCount={courses.length}
          page={safePage}
          pageSize={pageSize}
          onPageChange={setPage}
          onPageSizeChange={(s) => {
            setPageSize(s);
            setPage(1);
          }}
          itemLabel="ders"
          extraInfo={paginationExtraInfo}
          rightAction={paginationRightAction}
        />
      </div>
    </div>
  );
}
