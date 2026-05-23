// AcademicCalendarPage'in taslak düzenleme beyni: sürükle-bırak ile etkinlik taşıma,
// taslak/geri-al yığını, kaydet ve bildirim/hata durumu. Sayfadan ayrıldı ki sayfa
// yalnızca sunum + veri çekme ile ilgilensin (refactor: 778 satırlık sayfayı böldük).
import { useCallback, useEffect, useMemo, useState } from 'react';
import type { DropFeedback, DropFeedbackRequest, VisualizerEvent } from '@/components/CalendarVisualizer';
import {
  usePreviewAcademicCalendarEventMove,
  useSaveAcademicCalendarChanges,
  type StudentAcademicCalendarDto,
} from '@/hooks/useAcademicCalendar';
import {
  EXAM_EVENT_TYPES,
  MONDAY_START_EVENT_TYPES,
  calendarEditWindow,
  canDragCalendarEvent,
  cloneCalendar,
  cloneDraftCalendarMap,
  dayDifference,
  fmtDate,
  getErrorMessage,
  isMondayDate,
  isWeekendDate,
  isoToDateStr,
  parseVisualizerEventId,
  schedulingRuleEventType,
  shiftDateValue,
  type DraftCalendarMap,
} from '@/lib/academicCalendarHelpers';

interface EventDropArgs {
  eventId: VisualizerEvent['id'];
  targetDate: string;
  sourceStartDate: string;
  sourceEndDate: string;
  dragAnchorDate: string;
}

export interface CalendarDraftEditor {
  calendars: StudentAcademicCalendarDto[];
  hasDraftChanges: boolean;
  isCalendarMutating: boolean;
  canUndo: boolean;
  isSaving: boolean;
  notice: string | null;
  errorNotice: string | null;
  setNotice: (value: string | null) => void;
  setErrorNotice: (value: string | null) => void;
  handleEventDrop: (args: EventDropArgs) => void;
  getDropFeedback: (request: DropFeedbackRequest) => DropFeedback;
  handleInvalidEventDrop: (request: DropFeedbackRequest & { reason?: string }) => void;
  handleUndoDraftChange: () => void;
  handleCancelDraftChanges: () => void;
  handleSaveDraftChanges: () => void;
}

/**
 * Sunucudan gelen takvimler üzerine taslak değişiklikleri uygular; sürükle-bırak
 * önizlemesini (preview-move) ve toplu kaydı yönetir.
 *
 * @param serverCalendars Seçili akademik yılın sunucu takvimleri.
 * @param resetKey Değişince taslak/geçmiş sıfırlanır (akademik yıl seçimi).
 *
 * Not: Düzenleme yetkisi (canEditCalendar) gating'i çağıran tarafta JSX'te yapılır
 * (onEventDrop/getDropFeedback yetki yoksa hiç bağlanmaz), bu yüzden hook'a geçilmez.
 */
export function useCalendarDraftEditor(
  serverCalendars: StudentAcademicCalendarDto[],
  resetKey: number | null,
): CalendarDraftEditor {
  const previewCalendarEventMove = usePreviewAcademicCalendarEventMove();
  const saveCalendarChanges = useSaveAcademicCalendarChanges();
  const [notice, setNotice] = useState<string | null>(null);
  const [errorNotice, setErrorNotice] = useState<string | null>(null);
  const [draftCalendars, setDraftCalendars] = useState<DraftCalendarMap>({});
  const [draftHistory, setDraftHistory] = useState<DraftCalendarMap[]>([]);

  // Akademik yıl değişince taslakları temizle — bir yılın taslağı diğerine taşmasın.
  useEffect(() => {
    setDraftCalendars({});
    setDraftHistory([]);
  }, [resetKey]);

  const calendars = useMemo(
    () => serverCalendars.map(calendar => draftCalendars[calendar.termId] ?? calendar),
    [draftCalendars, serverCalendars],
  );
  const hasDraftChanges = Object.keys(draftCalendars).length > 0;
  const isCalendarMutating = previewCalendarEventMove.isPending || saveCalendarChanges.isPending;

  const showErrorNotice = useCallback((message: string) => {
    setNotice(null);
    setErrorNotice(message);
  }, []);

  const handleEventDrop = useCallback(({ eventId, targetDate, sourceStartDate, sourceEndDate, dragAnchorDate }: EventDropArgs) => {
    if (isCalendarMutating)
      return;

    const parsed = parseVisualizerEventId(eventId);
    if (!parsed) {
      showErrorNotice('Tasınan etkinlik kimligi çözümlenemedi.');
      return;
    }

    const calendar = calendars.find(item => item.termId === parsed.termId);
    if (!calendar) {
      showErrorNotice('Takvim bulunamadi.');
      return;
    }

    if (calendar.isCancelled) {
      showErrorNotice('İptal edilmiş dönemde etkinlik taşınamaz.');
      return;
    }

    const shiftDays = dayDifference(targetDate, dragAnchorDate);
    if (shiftDays === 0)
      return;

    const nextStartDate = shiftDateValue(sourceStartDate, shiftDays);
    const nextEndDate = shiftDateValue(sourceEndDate, shiftDays);

    setNotice(null);
    previewCalendarEventMove.mutate({
      termId: parsed.termId,
      eventId: parsed.eventId,
      dto: {
        events: calendar.events,
        startDate: `${nextStartDate}T00:00:00.000Z`,
        endDate: nextEndDate === nextStartDate ? null : `${nextEndDate}T00:00:00.000Z`,
        propagateChanges: true,
      },
    }, {
      onSuccess: updatedCalendar => {
        const movedEvent = updatedCalendar.events.find(item => item.id === parsed.eventId);
        const actualStartDate = isoToDateStr(movedEvent?.startDate) ?? nextStartDate;
        const changedEventCount = updatedCalendar.events.filter(item => {
          const previous = calendar.events.find(event => event.id === item.id);
          return previous
            && (isoToDateStr(previous.startDate) !== isoToDateStr(item.startDate)
              || isoToDateStr(previous.endDate) !== isoToDateStr(item.endDate));
        }).length;
        setDraftHistory(prev => [...prev, cloneDraftCalendarMap(draftCalendars)]);
        setDraftCalendars(prev => ({ ...prev, [updatedCalendar.termId]: cloneCalendar(updatedCalendar) }));
        setErrorNotice(null);
        setNotice(changedEventCount > 1
          ? `Etkinlik ${fmtDate(`${actualStartDate}T00:00:00.000Z`)} tarihine taslakta tasindi; bagli etkinlikler kurallara gore ayarlandi. Kaydetmeden akademik takvime yazilmaz.`
          : `Etkinlik ${fmtDate(`${actualStartDate}T00:00:00.000Z`)} tarihine taslakta tasindi. Kaydetmeden akademik takvime yazilmaz.`);
      },
      onError: error => showErrorNotice(getErrorMessage(error)),
    });
  }, [calendars, draftCalendars, isCalendarMutating, previewCalendarEventMove, showErrorNotice]);

  const getDropFeedback = useCallback(({ eventId, targetDate, sourceStartDate, sourceEndDate, dragAnchorDate }: DropFeedbackRequest): DropFeedback => {
    const parsed = parseVisualizerEventId(eventId);
    if (!parsed)
      return { allowed: false, reason: 'Etkinlik kimliği çözümlenemedi.' };

    const calendar = calendars.find(item => item.termId === parsed.termId);
    const event = calendar?.events.find(item => item.id === parsed.eventId);
    if (!calendar || !event)
      return { allowed: false, reason: 'Etkinlik bulunamadı.' };

    if (calendar.isCancelled)
      return { allowed: false, reason: 'İptal edilmiş dönemde etkinlik taşınamaz.' };

    if (!canDragCalendarEvent(event))
      return { allowed: false, reason: 'Bu etkinlik sürükle-bırak ile değiştirilemez.' };

    const shiftDays = dayDifference(targetDate, dragAnchorDate);
    const nextStartDate = shiftDateValue(sourceStartDate, shiftDays);
    const nextEndDate = shiftDateValue(sourceEndDate, shiftDays);
    const window = calendarEditWindow(calendar);
    const ruleEventType = schedulingRuleEventType(event);

    if (nextStartDate < window.start || nextEndDate > window.end)
      return { allowed: false, reason: 'Akademik yıl düzenleme aralığı dışında.' };

    if (MONDAY_START_EVENT_TYPES.has(ruleEventType) && !isMondayDate(nextStartDate))
      return { allowed: false, reason: 'Bu etkinlik Pazartesi günü başlamalıdır.' };

    if (EXAM_EVENT_TYPES.has(ruleEventType) && isWeekendDate(nextStartDate))
      return { allowed: false, reason: 'Sınav etkinlikleri hafta sonu başlayamaz.' };

    return { allowed: true, reason: 'Uygun hedef. Bırakınca taslak kurallarıyla yeniden hesaplanır.' };
  }, [calendars]);

  const handleInvalidEventDrop = useCallback(({ reason }: DropFeedbackRequest & { reason?: string }) => {
    showErrorNotice(reason ?? 'Bu alana bırakma takvim kuralları nedeniyle uygulanamaz.');
  }, [showErrorNotice]);

  const handleUndoDraftChange = useCallback(() => {
    setDraftHistory(prev => {
      const last = prev[prev.length - 1];
      if (!last)
        return prev;

      setDraftCalendars(cloneDraftCalendarMap(last));
      return prev.slice(0, -1);
    });
    setNotice('Son taslak degisikligi geri alindi.');
  }, []);

  const handleCancelDraftChanges = useCallback(() => {
    setDraftCalendars({});
    setDraftHistory([]);
    setNotice('Taslak degisiklikleri iptal edildi.');
  }, []);

  const handleSaveDraftChanges = useCallback(() => {
    const stagedCalendars = Object.values(draftCalendars);
    if (stagedCalendars.length === 0)
      return;

    saveCalendarChanges.mutate({
      calendars: stagedCalendars.map(calendar => ({
        termId: calendar.termId,
        events: calendar.events,
      })),
    }, {
      onSuccess: () => {
        setDraftCalendars({});
        setDraftHistory([]);
        setErrorNotice(null);
        setNotice('Akademik takvim kaydedildi.');
      },
      onError: error => showErrorNotice(getErrorMessage(error)),
    });
  }, [draftCalendars, saveCalendarChanges, showErrorNotice]);

  return {
    calendars,
    hasDraftChanges,
    isCalendarMutating,
    canUndo: draftHistory.length > 0,
    isSaving: saveCalendarChanges.isPending,
    notice,
    errorNotice,
    setNotice,
    setErrorNotice,
    handleEventDrop,
    getDropFeedback,
    handleInvalidEventDrop,
    handleUndoDraftChange,
    handleCancelDraftChanges,
    handleSaveDraftChanges,
  };
}
