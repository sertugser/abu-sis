// SIS.Web/src/components/faculty-planning/AutoScheduleTab.tsx
import { useCallback, useEffect, useState } from 'react';
import { createPortal } from 'react-dom';
import { SlotAssignDialog } from './SlotAssignDialog';
import { Button } from '@/components/ui/button';
import { DelayedConfirmDialog } from '@/components/ui/delayed-confirm-dialog';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { ScrollArea } from '@/components/ui/scroll-area';
import {
  CheckCircle2, XCircle,
  RotateCcw, Trash2,
} from 'lucide-react';
import {
  useApplySchedule, useAutoSchedulePreview,
  useBatchUnassignSections, useFacultyPlanningOverview, useFacultyPlanningSections,
  useFacultyPlanningTimeSlots, useResetAllSchedules, useScheduleRun, useScheduleRuns,
} from '../../hooks/useFacultyPlanning';
import type { AutoSchedulePreview, FacultyPlanningTimeSlot } from '../../types/facultyPlanning';
import { CalendarGrid } from './CalendarGrid';
import { AutoScheduleProgressModal } from './AutoScheduleProgressModal';
import InlineConfirmButton from './InlineConfirmButton';

function extractError(error: unknown, fallback: string) {
  const data = (error as { response?: { data?: { error?: string; message?: string; title?: string } } })?.response?.data;
  return data?.error ?? data?.message ?? data?.title ?? fallback;
}

interface AutoScheduleTabProps {
  facultyId: number;
  termId: number | null;
  filterDept?: number;
  /** Parent'tan gelen "şimdi çalıştır" sinyali; her artış yeni bir run başlatır. */
  triggerRun?: number;
  /**
   * Parent state'inde tutulan "son işlenen trigger" değeri. Tab değişiminde bu komponent
   * unmount + remount olabiliyor; consumed bilgisi local state'te tutulursa kayboluyor ve
   * remount'ta useEffect aynı triggerRun'ı yeniden tetikleyip "iptal ettim ama tab dönünce
   * kendiliğinden başladı" bug'ına yol açıyor. Parent state'i kalıcı → sorun yok.
   */
  consumedTriggerRun?: number;
  /** Trigger işlendiğinde parent'ı bilgilendir — parent <c>consumedTriggerRun</c>'u günceller. */
  onTriggerConsumed?: (value: number) => void;
  onRunStateChange?: (isPending: boolean) => void;
  actionBarPortal?: HTMLElement | null;
}

export function AutoScheduleTab({
  facultyId, termId, filterDept,
  triggerRun, consumedTriggerRun, onTriggerConsumed,
  onRunStateChange, actionBarPortal,
}: AutoScheduleTabProps) {
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
  /** "Tümünü Sıfırla" onay dialog'u açık mı — TÜM atamaları siler, en kritik yıkıcı işlem. */
  const [isResetAllOpen, setIsResetAllOpen] = useState(false);
  const [lastAppliedIds, setLastAppliedIds] = useState<number[]>([]);
  const [showResultModal, setShowResultModal] = useState(false);
  const [feedback, setFeedback] = useState<{ kind: 'success' | 'error'; message: string } | null>(null);
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

  const successCount = previewData?.assignments.length ?? 0;
  const failureCount = previewData?.failures.length ?? 0;

  // Notify parent about pending state
  useEffect(() => {
    onRunStateChange?.(isPending);
  }, [isPending, onRunStateChange]);

  // Parent triggered run.
  // "Consumed" bilgisi parent state'inde (consumedTriggerRun) — bu komponentin unmount/remount
  // döngüsünden sağ çıkar. Tab değişimlerinde useEffect aynı triggerRun'ı tekrar tetiklemez.
  /* eslint-disable react-hooks/set-state-in-effect -- resetting local state when parent triggers a new run; intentional sync */
  useEffect(() => {
    if (triggerRun && triggerRun !== consumedTriggerRun && termId && !previewMutation.isPending) {
      onTriggerConsumed?.(triggerRun);
      setUserSelectedRunId(null);
      setLastAppliedIds([]);
      applyMutation.reset();
      previewMutation.mutate({ facultyId, termId }, {
        onSuccess: () => setShowResultModal(true),
        onError: (error: unknown) => setFeedback({ kind: 'error', message: extractError(error, 'Otomatik programlama başarısız.') }),
      });
    }
  }, [triggerRun, consumedTriggerRun, onTriggerConsumed, termId, facultyId, previewMutation, applyMutation]);
  /* eslint-enable react-hooks/set-state-in-effect */

  // Handlers
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
        onError: (error: unknown) => setFeedback({ kind: 'error', message: extractError(error, 'Program uygulanamadı.') }),
      });
    }
  }, [previewData, applyMutation]);

  const clearPreview = useCallback(() => {
    previewMutation.reset();
    setUserSelectedRunId(null);
    setLastAppliedIds([]);
    applyMutation.reset();
    setShowResultModal(false);
  }, [previewMutation, applyMutation]);

  if (!termId) return null;

  const actionBarVisible = hasPreview || lastAppliedIds.length > 0 || (ov?.scheduledSections ?? 0) > 0;

  const actionBar = actionBarVisible ? (
        <div className="flex items-center gap-2">
          {/* Önizleme rozeti — tıkla → sonuç modal'ı aç. Kısmi seçim popover'u kaldırıldı:
              pratikte kimse 34 şubeyi tek tek seçmiyor; "Tümünü Uygula" zaten ana eylem. */}
          {hasPreview && (
            <Button
              variant="outline"
              size="sm"
              onClick={() => setShowResultModal(true)}
              className="h-7 text-[11px] gap-1.5"
              title="Sonuç özetini aç"
            >
              <CheckCircle2 className="h-3 w-3 text-emerald-500" />
              {successCount} önerildi
              {failureCount > 0 && <span className="text-red-500">· {failureCount} atanamadı</span>}
            </Button>
          )}

          {/* Apply sonucu — hata varsa errors listesi popover ile yüzeylenir
              ("neden yazılamadı" cevabı kullanıcıya buradan ulaşır). */}
          {applyMutation.data && (
            applyMutation.data.failedCount > 0 ? (
              <Popover>
                <PopoverTrigger asChild>
                  <button
                    type="button"
                    className="text-[11px] text-amber-600 hover:text-amber-700 underline underline-offset-2 decoration-amber-300 hover:decoration-amber-500 transition-colors"
                    aria-label="Uygulanamayan şubelerin sebeplerini göster"
                  >
                    {applyMutation.data.successCount} uygulandı · {applyMutation.data.failedCount} hata
                  </button>
                </PopoverTrigger>
                <PopoverContent className="w-96 p-3" align="start" sideOffset={4}>
                  <p className="text-xs font-semibold text-slate-700 mb-2">Uygulanamayan şubeler</p>
                  <ScrollArea className="max-h-60">
                    <ul className="space-y-1.5">
                      {(applyMutation.data.errors ?? []).map((err, i) => (
                        <li key={i} className="text-[11px] text-slate-600 flex items-start gap-1.5">
                          <XCircle className="h-3 w-3 text-red-400 shrink-0 mt-0.5" />
                          <span>{err}</span>
                        </li>
                      ))}
                    </ul>
                  </ScrollArea>
                </PopoverContent>
              </Popover>
            ) : (
              <span className="text-[11px] text-emerald-600">
                {applyMutation.data.successCount} uygulandı
              </span>
            )
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
                  onError: (error: unknown) => setFeedback({ kind: 'error', message: extractError(error, 'Geri alma başarısız.') }),
                })}
                className="h-7 text-[11px]"
              />
            );
          })()}

          <div className="flex-1" />

          {/* Tümünü Sıfırla — TÜM atamaları silen yıkıcı işlem; 5sn delayed onay zorunlu. */}
          {termId && (ov?.scheduledSections ?? 0) > 0 && (
            <Button
              variant="ghost"
              size="sm"
              disabled={resetAll.isPending}
              onClick={() => setIsResetAllOpen(true)}
              className="h-7 text-[11px] text-red-600 hover:text-red-700"
            >
              <Trash2 className="h-3 w-3 mr-1" />
              {resetAll.isPending ? 'Sıfırlanıyor...' : 'Tümünü Sıfırla'}
            </Button>
          )}
        </div>
  ) : null;

  return (
    <div className="flex flex-col h-full">

      {feedback && (
        <div
          className={`mx-2 mt-1 flex items-center justify-between gap-2 rounded-md px-3 py-1.5 text-xs ${
            feedback.kind === 'error'
              ? 'bg-red-50 text-red-700 border border-red-200'
              : 'bg-emerald-50 text-emerald-700 border border-emerald-200'
          }`}
        >
          <span>{feedback.message}</span>
          <button onClick={() => setFeedback(null)} className="shrink-0 underline underline-offset-2">Kapat</button>
        </div>
      )}

      {/* Aksiyon bar: portal varsa toolbar'a, yoksa yerinde */}
      {actionBar && (actionBarPortal ? createPortal(actionBar, actionBarPortal) : actionBar)}

      {/* ── Takvim — tam alan ──
          overflow-auto: CalendarGrid içerik-bazlı boyutlanıyor; uzun gridlerde dikey scroll
          burada sağlanır (CalendarGrid'in kendi içinde scroll yok — dnd-kit measuring stratejisi
          için tek-katman scroll). */}
      <div className="flex-1 min-h-0 relative overflow-auto">
        <CalendarGrid
          sections={sections}
          timeSlots={timeSlots}
          cardDensity="normal"
          maxBlocksPerCell={4}
          onEmptyCellClick={(day, slotOrder, startTime, endTime, allowedMode) => {
            setAssignSlot({ day, slotOrder, startTime, endTime, allowedMode });
          }}
        />
      </div>

      {/* ── Otomatik Programlama modalı ──
          Tek modal iki state'i kapsar:
            - isPending → progress UI (stage indicator + animated bar)
            - complete + previewData → sonuç paneli (yerleştirilen/yerleşemeyen + apply/view butonları)
          Eski ScheduleResultModal birleştirildi; iki ardışık dialog → tek dialog.
      */}
      <AutoScheduleProgressModal
        open={isPending || (showResultModal && !!previewData)}
        isPending={isPending}
        onCancel={() => {
          // Backend Hub.Cancel() zaten CancellationTokenSource'u tetikledi.
          // Mutation'ı reset edip UI state'i temizliyoruz; preview hata olarak
          // OperationCanceledException dönecek ve isPending false olacak.
          previewMutation.reset();
        }}
        previewData={previewData ?? null}
        onApplyAll={handleApplyAll}
        applyPending={applyMutation.isPending}
        onDismiss={() => setShowResultModal(false)}
      />

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
          facultyId={facultyId}
          sections={sections}
        />
      )}

      {/* Tümünü Sıfırla onay dialog'u — TÜM section atamalarını silen en yıkıcı işlem.
          5sn delay zorunlu; preview olarak etkilenecek section sayısı + scheduled olanların ders kodu listesi. */}
      {termId && (
        <DelayedConfirmDialog
          open={isResetAllOpen}
          onOpenChange={setIsResetAllOpen}
          title={`Tüm program atamaları sıfırlanacak (${ov?.scheduledSections ?? 0} şube)`}
          description="Bu fakültenin aktif dönemdeki TÜM section'larının hoca/derslik/zaman atamaları silinecek. Pinli olanlar dahildir."
          items={sections.filter(s => s.status !== 'Unscheduled').slice(0, 50).map(s => ({
            key: s.sectionId,
            label: `${s.courseCode} -${s.sectionIndex} · ${s.courseName} · ${s.scheduleSummary || '(zamansız)'}`,
          }))}
          footnote={
            (ov?.scheduledSections ?? 0) > 50
              ? `Bu işlem geri alınamaz. (Yukarıda ilk 50 şube gösteriliyor — toplam ${ov?.scheduledSections} şube etkilenecek.)`
              : 'Bu işlem geri alınamaz. Sıfırlama sonrası tüm şubeleri yeniden atamanız gerekecek.'
          }
          confirmLabel="Tüm atamaları sıfırla"
          delaySeconds={5}
          severity="danger"
          onConfirm={() => {
            setIsResetAllOpen(false);
            resetAll.mutate({ facultyId, termId }, {
              onSuccess: (data) => {
                clearPreview();
                setFeedback({ kind: 'success', message: `${data.resetCount} şubenin ataması sıfırlandı.` });
              },
              onError: (error: unknown) => setFeedback({ kind: 'error', message: extractError(error, 'Sıfırlama başarısız.') }),
            });
          }}
        />
      )}
    </div>
  );
}
