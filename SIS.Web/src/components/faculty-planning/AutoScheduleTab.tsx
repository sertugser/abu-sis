// SIS.Web/src/components/faculty-planning/AutoScheduleTab.tsx
import { useCallback, useEffect, useMemo, useState } from 'react';
import { createPortal } from 'react-dom';
import { SlotAssignDialog } from './SlotAssignDialog';
import { Button } from '@/components/ui/button';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { ScrollArea } from '@/components/ui/scroll-area';
import {
  CheckCircle2, XCircle, ChevronDown,
  RotateCcw, Trash2,
} from 'lucide-react';
import {
  useApplySchedule, useAutoSchedulePreview,
  useBatchUnassignSections, useFacultyPlanningOverview, useFacultyPlanningSections,
  useFacultyPlanningTimeSlots, useResetAllSchedules, useScheduleRun, useScheduleRuns,
} from '../../hooks/useFacultyPlanning';
import type { AutoSchedulePreview, FacultyPlanningTimeSlot } from '../../types/facultyPlanning';
import { CalendarGrid } from './CalendarGrid';
import { ScheduleResultModal } from './ScheduleResultModal';
import { AutoScheduleProgressModal } from './AutoScheduleProgressModal';
import InlineConfirmButton from './InlineConfirmButton';
import { cn } from '@/lib/utils';

interface AutoScheduleTabProps {
  facultyId: number;
  termId: number | null;
  filterDept?: number;
  triggerRun?: number;
  onRunStateChange?: (isPending: boolean) => void;
  actionBarPortal?: HTMLElement | null;
}

export function AutoScheduleTab({ facultyId, termId, filterDept, triggerRun, onRunStateChange, actionBarPortal }: AutoScheduleTabProps) {
  const previewMutation = useAutoSchedulePreview();
  const applyMutation = useApplySchedule();
  const batchUnassign = useBatchUnassignSections();
  const resetAll = useResetAllSchedules();

  const runsQuery = useScheduleRuns(facultyId, termId);
  const overviewQuery = useFacultyPlanningOverview(facultyId, termId);

  const sectionsQuery = useFacultyPlanningSections(facultyId, termId, filterDept, undefined, 1, 200);
  const timeSlotsQuery = useFacultyPlanningTimeSlots(termId);

  const sections = sectionsQuery.data?.items ?? [];
  const timeSlots: FacultyPlanningTimeSlot[] = timeSlotsQuery.data ?? [];

  const [userSelectedRunId, setUserSelectedRunId] = useState<number | null>(null);
  const [selectedIds, setSelectedIds] = useState<Set<number>>(new Set());
  const [lastAppliedIds, setLastAppliedIds] = useState<number[]>([]);
  const [lastTrigger, setLastTrigger] = useState(0);
  const [showResultModal, setShowResultModal] = useState(false);
  const [assignSlot, setAssignSlot] = useState<{
    day: number;
    slotOrder: number;
    startTime: string;
    endTime: string;
    allowedMode: FacultyPlanningTimeSlot['allowedMode'];
  } | null>(null);

  const selectedRunId = previewMutation.data
    ? null
    : userSelectedRunId ?? runsQuery.data?.[0]?.id ?? null;

  const runDetailQuery = useScheduleRun(selectedRunId);

  const previewData: AutoSchedulePreview | undefined =
    previewMutation.data ?? runDetailQuery.data?.result;

  const ov = overviewQuery.data;
  const isPending = previewMutation.isPending;
  const hasPreview = !!previewData;

  const previewPct = useMemo(() => {
    if (!previewData) return 0;
    const total = previewData.assignments.length + previewData.failures.length;
    return total > 0 ? Math.round((previewData.assignments.length / total) * 100) : 0;
  }, [previewData]);

  const successCount = previewData?.assignments.length ?? 0;
  const failureCount = previewData?.failures.length ?? 0;

  const allSelected = previewData
    ? previewData.assignments.length > 0 && previewData.assignments.every(a => selectedIds.has(a.sectionId))
    : false;

  const runHistory = useMemo(() => {
    if (!runsQuery.data || runsQuery.data.length <= 1) return [];
    return runsQuery.data.slice(0, 3).map(r => {
      const total = r.successCount + r.failedCount;
      return {
        id: r.id,
        pct: total > 0 ? Math.round((r.successCount / total) * 100) : 0,
        isActive: r.id === selectedRunId,
      };
    });
  }, [runsQuery.data, selectedRunId]);

  // Notify parent about pending state
  useEffect(() => {
    onRunStateChange?.(isPending);
  }, [isPending, onRunStateChange]);

  // Parent triggered run
  /* eslint-disable react-hooks/set-state-in-effect -- resetting local state when parent triggers a new run; intentional sync */
  useEffect(() => {
    if (triggerRun && triggerRun !== lastTrigger && termId && !previewMutation.isPending) {
      setLastTrigger(triggerRun);
      setUserSelectedRunId(null);
      setSelectedIds(new Set());
      setLastAppliedIds([]);
      applyMutation.reset();
      previewMutation.mutate({ facultyId, termId }, {
        onSuccess: () => setShowResultModal(true),
      });
    }
  }, [triggerRun, lastTrigger, termId, facultyId, previewMutation, applyMutation]);
  /* eslint-enable react-hooks/set-state-in-effect */

  // Handlers
  const handleRunSelect = useCallback((id: number) => {
    setUserSelectedRunId(id);
    previewMutation.reset();
    setSelectedIds(new Set());
    setLastAppliedIds([]);
    applyMutation.reset();
  }, [previewMutation, applyMutation]);

  const handleApplyAll = useCallback(() => {
    const items = (previewData?.assignments ?? []).map(a => ({
      sectionId: a.sectionId,
      classroomId: a.classroomId,
      scheduleSlots: a.scheduleSlots,
      instructorId: a.instructorId ?? undefined,
    }));
    if (items.length > 0) {
      applyMutation.mutate({ assignments: items }, {
        onSuccess: () => {
          setLastAppliedIds(items.map(a => a.sectionId));
          setShowResultModal(false);
        },
      });
    }
  }, [previewData, applyMutation]);

  const handleApplySelected = useCallback(() => {
    const source = previewData?.assignments ?? [];
    const items = (selectedIds.size > 0
      ? source.filter(a => selectedIds.has(a.sectionId))
      : source
    ).map(a => ({
      sectionId: a.sectionId,
      classroomId: a.classroomId,
      scheduleSlots: a.scheduleSlots,
      instructorId: a.instructorId ?? undefined,
    }));
    if (items.length > 0) {
      applyMutation.mutate({ assignments: items }, {
        onSuccess: () => setLastAppliedIds(items.map(a => a.sectionId)),
      });
    }
  }, [previewData, selectedIds, applyMutation]);

  const handleToggleAll = useCallback(() => {
    if (!previewData) return;
    const allIds = previewData.assignments.map(a => a.sectionId);
    const allSel = allIds.every(id => selectedIds.has(id));
    setSelectedIds(allSel ? new Set() : new Set(allIds));
  }, [previewData, selectedIds]);

  const toggleCheck = useCallback((id: number) => {
    setSelectedIds(prev => {
      const n = new Set(prev);
      if (n.has(id)) n.delete(id); else n.add(id);
      return n;
    });
  }, []);

  const clearPreview = useCallback(() => {
    previewMutation.reset();
    setUserSelectedRunId(null);
    setSelectedIds(new Set());
    setLastAppliedIds([]);
    applyMutation.reset();
    setShowResultModal(false);
  }, [previewMutation, applyMutation]);

  if (!termId) return null;

  const actionBarVisible = hasPreview || lastAppliedIds.length > 0 || (ov?.scheduledSections ?? 0) > 0;

  const actionBar = actionBarVisible ? (
        <div className="flex items-center gap-2">
          {/* Sonuç: popover ile seçici uygulama */}
          {hasPreview && (
            <Popover>
              <PopoverTrigger asChild>
                <Button variant="outline" size="sm" className="h-7 text-[11px] gap-1.5">
                  <CheckCircle2 className="h-3 w-3 text-emerald-500" />
                  {successCount} başarılı
                  {failureCount > 0 && <span className="text-red-500">· {failureCount} başarısız</span>}
                  <ChevronDown className="h-3 w-3 text-slate-400" />
                </Button>
              </PopoverTrigger>
              <PopoverContent className="w-[520px] p-0" align="start" sideOffset={4}>
                <div className="flex items-center gap-3 px-4 py-2.5 border-b border-slate-100">
                  <span className="text-xs font-medium text-slate-600">
                    %{previewPct} başarı · {successCount} atanabilir · {failureCount} başarısız
                  </span>
                  {runHistory.length > 0 && (
                    <>
                      <div className="flex-1" />
                      <div className="flex items-center gap-1">
                        {runHistory.map(r => (
                          <button
                            key={r.id}
                            type="button"
                            onClick={() => handleRunSelect(r.id)}
                            className={cn(
                              'px-1.5 py-0.5 rounded text-[9px] font-medium transition-colors',
                              r.isActive
                                ? 'bg-[#003366] text-white'
                                : 'bg-slate-100 text-slate-400 hover:bg-slate-200',
                            )}
                          >
                            %{r.pct}
                          </button>
                        ))}
                      </div>
                    </>
                  )}
                </div>

                <ScrollArea className="max-h-[300px]">
                  <div className="flex flex-wrap gap-1.5 p-3">
                    {(previewData?.assignments ?? []).map(a => (
                      <label
                        key={a.sectionId}
                        className={cn(
                          'flex items-center gap-1.5 px-2 py-1 rounded-md border text-xs cursor-pointer transition-colors',
                          selectedIds.has(a.sectionId)
                            ? 'bg-emerald-50 border-emerald-300'
                            : 'bg-white border-slate-200 hover:border-emerald-300',
                        )}
                      >
                        <input
                          type="checkbox"
                          checked={selectedIds.has(a.sectionId)}
                          onChange={() => toggleCheck(a.sectionId)}
                          className="rounded accent-[#003366] h-3 w-3"
                        />
                        <CheckCircle2 className="h-3 w-3 text-emerald-500 shrink-0" />
                        <span className="font-semibold text-slate-700">{a.courseCode}</span>
                        <span className="text-slate-400 text-[10px]">{a.classroomName}</span>
                      </label>
                    ))}
                    {(previewData?.failures ?? []).map(f => (
                      <div
                        key={f.sectionId}
                        className="flex items-center gap-1.5 px-2 py-1 rounded-md border border-red-200 bg-red-50/50 text-xs opacity-70"
                      >
                        <XCircle className="h-3 w-3 text-red-400 shrink-0" />
                        <span className="font-semibold text-slate-500">{f.courseCode}</span>
                        <span className="text-red-400 text-[10px]">Atanamadı</span>
                      </div>
                    ))}
                  </div>
                </ScrollArea>

                <div className="flex items-center justify-between px-4 py-2 border-t border-slate-100">
                  <label className="flex items-center gap-1.5 cursor-pointer">
                    <input
                      type="checkbox"
                      checked={allSelected}
                      onChange={handleToggleAll}
                      className="rounded accent-[#003366] h-3.5 w-3.5"
                    />
                    <span className="text-xs text-slate-500">Tümünü Seç</span>
                  </label>
                  <div className="flex items-center gap-2">
                    <Button
                      variant="ghost"
                      size="sm"
                      className="h-7 text-[11px]"
                      onClick={() => setShowResultModal(true)}
                    >
                      Detaylı Rapor
                    </Button>
                    <Button
                      size="sm"
                      onClick={handleApplySelected}
                      disabled={applyMutation.isPending}
                      className="h-7 text-[11px] px-4 bg-[#003366] hover:bg-[#002244]"
                    >
                      {applyMutation.isPending
                        ? 'Uygulanıyor...'
                        : selectedIds.size > 0
                          ? `Uygula (${selectedIds.size})`
                          : 'Tümünü Uygula'}
                    </Button>
                  </div>
                </div>
              </PopoverContent>
            </Popover>
          )}

          {/* Uygulama sonucu */}
          {applyMutation.data && (
            <span className={cn(
              'text-[11px]',
              applyMutation.data.failedCount > 0 ? 'text-amber-600' : 'text-emerald-600',
            )}>
              {applyMutation.data.successCount} başarılı
              {applyMutation.data.failedCount > 0 && `, ${applyMutation.data.failedCount} başarısız`}
            </span>
          )}

          {/* Geri al */}
          {lastAppliedIds.length > 0 && (() => {
            const unpinnedIds = lastAppliedIds.filter(id => {
              const section = sections.find(s => s.sectionId === id);
              return !section?.isPinned;
            });
            if (unpinnedIds.length === 0) return null;
            return (
              <InlineConfirmButton
                label={batchUnassign.isPending ? 'Geri alınıyor...' : `Geri Al (${unpinnedIds.length})`}
                confirmLabel={`${unpinnedIds.length} şubeyi geri al? Onayla`}
                variant="outline"
                icon={<RotateCcw className="h-3 w-3 mr-1" />}
                disabled={batchUnassign.isPending}
                onConfirm={() => batchUnassign.mutate(unpinnedIds, {
                  onSuccess: () => {
                    setLastAppliedIds([]);
                    applyMutation.reset();
                  },
                })}
                className="h-7 text-[11px]"
              />
            );
          })()}

          <div className="flex-1" />

          {/* Tümünü Sıfırla */}
          {termId && (ov?.scheduledSections ?? 0) > 0 && (
            <InlineConfirmButton
              label={resetAll.isPending ? 'Sıfırlanıyor...' : 'Tümünü Sıfırla'}
              confirmLabel="TÜM atamaları sıfırla? Onayla"
              variant="ghost"
              confirmVariant="destructive"
              icon={<Trash2 className="h-3 w-3 mr-1" />}
              disabled={resetAll.isPending}
              onConfirm={() => resetAll.mutate({ facultyId, termId }, {
                onSuccess: (data) => {
                  clearPreview();
                  alert(`${data.resetCount} şubenin ataması sıfırlandı.`);
                },
              })}
              className="h-7 text-[11px] text-red-600 hover:text-red-700"
            />
          )}
        </div>
  ) : null;

  return (
    <div className="flex flex-col h-full">

      {/* Aksiyon bar: portal varsa toolbar'a, yoksa yerinde */}
      {actionBar && (actionBarPortal ? createPortal(actionBar, actionBarPortal) : actionBar)}

      {/* ── Takvim — tam alan ── */}
      <div className="flex-1 min-h-0 relative">
        <CalendarGrid
          sections={sections}
          timeSlots={timeSlots}
          onEmptyCellClick={(day, slotOrder, startTime, endTime, allowedMode) => {
            setAssignSlot({ day, slotOrder, startTime, endTime, allowedMode });
          }}
        />
      </div>

      {/* ── Canlı progress modalı — algoritma çalışırken gösterilir ── */}
      <AutoScheduleProgressModal
        open={isPending}
        onCancel={() => {
          // Backend Hub.Cancel() zaten CancellationTokenSource'u tetikledi.
          // Mutation'ı reset edip UI state'i temizliyoruz; preview hata olarak
          // OperationCanceledException dönecek ve isPending false olacak.
          previewMutation.reset();
        }}
      />

      {/* ── Sonuç Modalı — hesaplama bitince otomatik açılır ── */}
      {previewData && (
        <ScheduleResultModal
          open={showResultModal}
          onClose={() => setShowResultModal(false)}
          previewData={previewData}
          onApplyAll={handleApplyAll}
          onViewCalendar={() => setShowResultModal(false)}
          applyPending={applyMutation.isPending}
        />
      )}

      {assignSlot && termId && (
        <SlotAssignDialog
          open={!!assignSlot}
          onOpenChange={(open) => { if (!open) setAssignSlot(null); }}
          day={assignSlot.day}
          slotOrder={assignSlot.slotOrder}
          startTime={assignSlot.startTime}
          endTime={assignSlot.endTime}
          allowedMode={assignSlot.allowedMode}
          termId={termId}
          sections={sections}
        />
      )}
    </div>
  );
}
