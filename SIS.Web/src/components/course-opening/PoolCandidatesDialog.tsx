import { useMemo, useState } from 'react';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { cn } from '@/lib/utils';
import { AlertCircle, CheckCircle2, Lock } from 'lucide-react';
import { getSubtypeColorByCode } from '@/lib/electiveSubtypeUtils';
import { usePoolCandidates } from '../../hooks/useCourseOpening';
import type { PoolSlotForOpeningDto, PoolCandidateDto, BulkOpenCoursesRequest } from '../../types/courseOpening';

interface Props {
  slot: PoolSlotForOpeningDto | null;
  termId: number | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onBulkOpen: (request: BulkOpenCoursesRequest) => Promise<unknown>;
  isOpening?: boolean;
  defaultCapacity?: number;
}

/**
 * Havuz aday derslerini listeleyen modal. Kullanıcı seçim yapar → "Seçilenleri Aç" ile bulkOpenCourses çağırılır.
 * Açılmış / kilitli / sahipsiz dersler için ayrı durum ikonu gösterilir.
 */
export function PoolCandidatesDialog({
  slot,
  termId,
  open,
  onOpenChange,
  onBulkOpen,
  isOpening = false,
  defaultCapacity = 40,
}: Props) {
  const candidatesQuery = usePoolCandidates(open ? slot?.poolId ?? null : null, termId);
  const [selectedIds, setSelectedIds] = useState<Set<number>>(new Set());

  const candidates = useMemo(() => candidatesQuery.data ?? [], [candidatesQuery.data]);

  const toggle = (c: PoolCandidateDto) => {
    if (!c.canOpen || c.alreadyOpened) return;
    setSelectedIds((prev) => {
      const next = new Set(prev);
      if (next.has(c.courseId)) next.delete(c.courseId);
      else next.add(c.courseId);
      return next;
    });
  };

  const handleOpenSelected = async () => {
    if (selectedIds.size === 0) return;
    await onBulkOpen({
      courses: [...selectedIds].map((courseId) => ({
        courseId,
        sectionCount: 1,
        capacityPerSection: defaultCapacity,
      })),
    });
    setSelectedIds(new Set());
    onOpenChange(false);
  };

  const handleClose = (next: boolean) => {
    if (!next) setSelectedIds(new Set());
    onOpenChange(next);
  };

  if (!slot) return null;

  const subtypeColor = getSubtypeColorByCode(slot.subtypeCode);

  return (
    <Dialog open={open} onOpenChange={handleClose}>
      <DialogContent className="max-w-2xl">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <span className="font-mono text-violet-800">{slot.slotName}</span>
            <span className="text-gray-400 font-normal">—</span>
            <span>{slot.electiveGroupName}</span>
            {slot.subtypeCode && (
              <span
                className={cn(
                  'px-1.5 py-0 rounded border text-[10px] font-semibold ml-1',
                  subtypeColor,
                )}
              >
                {slot.subtypeCode}
              </span>
            )}
          </DialogTitle>
          <DialogDescription className="text-xs">
            Sınıf {slot.suggestedSemesterStart > 0 ? Math.ceil(slot.suggestedSemesterStart / 2) : '—'} •{' '}
            {slot.ectsPerCourse != null ? `Havuz AKTS: ${slot.ectsPerCourse}` : 'AKTS: aday dersinin katalog değerinden'} •{' '}
            <strong>{slot.openedCandidateCount}</strong> / {slot.candidateCount} aday ders açılmış
          </DialogDescription>
        </DialogHeader>

        <div className="max-h-[55vh] overflow-y-auto space-y-1 -mx-1 px-1">
          {candidatesQuery.isLoading ? (
            <div className="space-y-2">
              {Array.from({ length: 5 }).map((_, i) => (
                <Skeleton key={i} className="h-9 rounded" />
              ))}
            </div>
          ) : candidates.length === 0 ? (
            <Alert>
              <AlertCircle className="h-4 w-4" />
              <AlertTitle>Aday ders yok</AlertTitle>
              <AlertDescription>
                Bu havuz grubunda tanımlı aday ders bulunmuyor. Havuz tanımına ders ekleyin.
              </AlertDescription>
            </Alert>
          ) : (
            candidates.map((c) => {
              const credit = c.nationalCredit ?? c.theoryHours + c.labHours;
              const disabled = !c.canOpen || c.alreadyOpened;
              const selected = selectedIds.has(c.courseId);
              return (
                <div
                  key={c.courseId}
                  className={cn(
                    'grid grid-cols-[28px_110px_1fr_50px_50px_36px] items-center gap-2 px-2 py-2 rounded border text-sm transition-colors',
                    disabled
                      ? 'opacity-60 border-transparent'
                      : selected
                      ? 'bg-amber-50 border-amber-200'
                      : 'border-gray-100 hover:bg-gray-50 cursor-pointer',
                  )}
                  onClick={() => toggle(c)}
                >
                  <input
                    type="checkbox"
                    checked={selected}
                    disabled={disabled}
                    onChange={() => toggle(c)}
                    onClick={(e) => e.stopPropagation()}
                    className="h-4 w-4 rounded border-gray-300 text-blue-600 focus:ring-1 focus:ring-blue-300"
                  />
                  <span className="font-mono text-slate-800 font-semibold truncate">{c.courseCode}</span>
                  <span className="text-gray-800 truncate" title={c.courseName}>
                    {c.courseName}
                  </span>
                  <span className="text-center tabular-nums text-gray-700">{credit}</span>
                  <span className="text-center tabular-nums text-gray-700 font-medium">{c.ects}</span>
                  <div className="flex justify-center">
                    {c.alreadyOpened ? (
                      <span title={`${c.existingSectionCount} şube açılmış`}>
                        <CheckCircle2 className="h-4 w-4 text-emerald-600" />
                      </span>
                    ) : !c.canOpen ? (
                      <span
                        title={
                          c.blockReason === 'TermNotInPlanning'
                            ? 'Dönem aktif değil'
                            : c.blockReason === 'CourseUnassigned'
                            ? 'Sahip bölüm atanmamış'
                            : 'Sahibi başka bölüm — açma yetkisi onlarda'
                        }
                      >
                        {c.blockReason === 'TermNotInPlanning' ? (
                          <Lock className="h-4 w-4 text-amber-600" />
                        ) : (
                          <AlertCircle className="h-4 w-4 text-gray-400" />
                        )}
                      </span>
                    ) : null}
                  </div>
                </div>
              );
            })
          )}
        </div>

        <DialogFooter className="border-t pt-3 flex-row items-center justify-between gap-2">
          <span className="text-xs text-gray-500">
            <strong>{selectedIds.size}</strong> ders seçili
          </span>
          <div className="flex gap-2">
            <Button variant="outline" size="sm" onClick={() => handleClose(false)}>
              Kapat
            </Button>
            <Button
              size="sm"
              disabled={selectedIds.size === 0 || isOpening}
              onClick={handleOpenSelected}
            >
              {isOpening ? 'Açılıyor...' : `Seçilenleri Aç (${selectedIds.size})`}
            </Button>
          </div>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
