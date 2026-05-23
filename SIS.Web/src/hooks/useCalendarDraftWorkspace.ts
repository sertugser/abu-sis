import { useMemo, useState } from 'react';
import {
  academicYearStart,
  dateInput,
  dayDifference,
  fmt,
  shiftDateValue,
  utcDate,
} from '@/lib/calendarGenerator/dates';
import {
  normalizeSemesters,
  semesterListText,
  termTypeToSemester,
} from '@/lib/calendarGenerator/semesters';
import {
  daySpan,
  eventCategoryValue,
  getDraftId,
  getErrorMessage,
  isAtaturkMemorialHoliday,
  isCriticalEvent,
  isEditableDraftEvent,
  trimTrailingWeekendEnd,
  validateDraftEventConstraints,
  validateDraftEventDateEdit,
  validateDraftEventHolidayOverlap,
} from '@/lib/calendarGenerator/eventRules';
import {
  CATEGORY_BADGE_VARIANTS,
  CATEGORY_FILTER_ORDER,
  CATEGORY_LABELS,
  displayText,
  type CalendarView,
} from '@/lib/calendarGenerator/types';
import type { CalendarEventCategory } from '@/lib/calendarEventTypes';
import type { DropFeedback, DropFeedbackRequest, VisualizerEvent } from '@/components/CalendarVisualizer';
import {
  type CalendarDraftSummaryDto,
  type GenerateCalendarResultDto,
  type GeneratedCalendarEventDto,
  type SemesterType,
  useApplyDraft,
  useCalendarDraft,
  useCalendarDrafts,
  useDeleteCalendarDraft,
  useGenerateCalendar,
  useHolidayDefinitions,
  useRollbackDraft,
  useUpdateDraftEvent,
  useWhatIfCalendar,
} from './useCalendarGenerator';
import { useAdminTerms } from './useAdminTerms';
import type { CalendarNotices } from './useCalendarNotices';
import type { CalendarGeneratorForm } from './useCalendarGeneratorForm';

/**
 * Takslak çalışma alanı: queries, seçili taslak/etkinlik durumu, etkinlik
 * düzenleme + sürükle-bırak, what-if önizleme ve üret/uygula/geri al/sil
 * yaşam döngüsü. Notice altyapısını ([[useCalendarNotices]]) ve üret-tab
 * formunu ([[useCalendarGeneratorForm]]) parametre olarak alır.
 */
export function useCalendarDraftWorkspace(notices: CalendarNotices, form: CalendarGeneratorForm) {
  const { askConfirm, setNotice, showSuccessNotice, showErrorNotice } = notices;
  const { academicYear, fallStartDate, springStartDate, includeSummer, constraints, durationOverrides } = form;

  const [activeTab, setActiveTab] = useState('generator');
  const [forceOverwrite, setForceOverwrite] = useState(false);
  const [applySemesters, setApplySemesters] = useState<SemesterType[]>([]);
  const [appliedSemesterKey, setAppliedSemesterKey] = useState('');
  const [selectedSemester, setSelectedSemester] = useState<SemesterType>('Fall');
  const [selectedEventTypeFilters, setSelectedEventTypeFilters] = useState<CalendarEventCategory[]>([]);
  const [selectedDraftId, setSelectedDraftId] = useState<number | null>(null);
  const [generated, setGenerated] = useState<GenerateCalendarResultDto | null>(null);
  const [selectedEventId, setSelectedEventId] = useState<string | null>(null);
  const [editForm, setEditForm] = useState({ title: '', startDate: '', endDate: '', note: '' });
  const [showDraftHistory, setShowDraftHistory] = useState(false);

  const holidays = useHolidayDefinitions();
  const drafts = useCalendarDrafts(showDraftHistory);
  const draft = useCalendarDraft(selectedDraftId);
  const generate = useGenerateCalendar();
  const updateDraftEvent = useUpdateDraftEvent();
  const whatIf = useWhatIfCalendar();
  const applyDraft = useApplyDraft();
  const rollbackDraft = useRollbackDraft();
  const deleteDraft = useDeleteCalendarDraft();
  const adminTerms = useAdminTerms();

  const calendar = (draft.data ?? generated) as CalendarView | null;
  const activeDraftId = getDraftId(calendar, selectedDraftId);
  const visibleHolidayDefinitions = useMemo(
    () => (holidays.data ?? []).filter(holiday => !isAtaturkMemorialHoliday(holiday)),
    [holidays.data],
  );
  const termAlignment = useMemo(() => {
    if (!calendar || !adminTerms.data) {
      return {
        hasMismatch: false,
        expectedSemesters: [] as SemesterType[],
        existingSemesters: [] as SemesterType[],
        message: null as string | null,
      };
    }

    const expectedSemesters = normalizeSemesters(calendar.termSummaries.map(summary => summary.semester));
    const existingSemesters = normalizeSemesters(
      adminTerms.data
        .filter(term => term.year === academicYearStart(calendar.academicYear))
        .map(term => termTypeToSemester(term.termType))
        .filter((s): s is SemesterType => s !== null),
    );

    const hasMismatch = existingSemesters.length > 0 && (
      expectedSemesters.length !== existingSemesters.length
      || expectedSemesters.some((semester, index) => semester !== existingSemesters[index])
    );

    return {
      hasMismatch,
      expectedSemesters,
      existingSemesters,
      message: hasMismatch
        ? `${calendar.academicYear} taslağı Dönem İşleri sayfasındaki dönemlerle uyuşmuyor. Beklenen: ${semesterListText(expectedSemesters)}. Bulunan: ${semesterListText(existingSemesters)}. Önce Dönem İşleri sayfasındaki dönemleri düzeltin.`
        : null,
    };
  }, [adminTerms.data, calendar]);
  // Yarıyıl SETİ (değer) değiştiğinde Uygula panelini varsayılana sıfırla. expectedSemesters
  // her render'da yeni dizi referansı ürettiği için effect+referans bağımlılığı kullanıcının
  // seçimini gereksiz re-render'larda (ör. taslak event düzenleme refetch'i) ezerdi. React'in
  // önerdiği "değişimde state sıfırla" desenini render sırasında prev-key ile uyguluyoruz.
  const expectedSemesterKey = termAlignment.expectedSemesters.join('|');
  if (expectedSemesterKey !== appliedSemesterKey) {
    setAppliedSemesterKey(expectedSemesterKey);
    setApplySemesters(termAlignment.expectedSemesters);
  }

  const semesterEvents = useMemo(
    () => (calendar?.events ?? []).filter(e => e.semester === selectedSemester).sort((a, b) => a.startDate.localeCompare(b.startDate)),
    [calendar, selectedSemester],
  );

  const eventTypeFilterOptions = useMemo(() => {
    const counts = new Map<CalendarEventCategory, number>();

    for (const event of semesterEvents) {
      const category = eventCategoryValue(event);
      counts.set(category, (counts.get(category) ?? 0) + 1);
    }

    return CATEGORY_FILTER_ORDER
      .filter(category => counts.has(category))
      .map(category => ({
        value: category,
        label: CATEGORY_LABELS[category] ?? 'Akademik',
        badgeVariant: CATEGORY_BADGE_VARIANTS[category],
        count: counts.get(category) ?? 0,
      }));
  }, [semesterEvents]);

  const filteredSemesterEvents = useMemo(
    () => selectedEventTypeFilters.length === 0
      ? semesterEvents
      : semesterEvents.filter(event => selectedEventTypeFilters.includes(eventCategoryValue(event))),
    [selectedEventTypeFilters, semesterEvents],
  );
  const hasActiveEventTypeFilter = selectedEventTypeFilters.length > 0;
  const eventTypeFilterSummary = selectedEventTypeFilters.length === 1
    ? eventTypeFilterOptions.find(option => option.value === selectedEventTypeFilters[0])?.label ?? 'Tür'
    : hasActiveEventTypeFilter
      ? `${selectedEventTypeFilters.length} tür`
      : 'Tür';

  const selectedEvent = useMemo(
    () => filteredSemesterEvents.find(e => e.draftEventId === selectedEventId) ?? null,
    [filteredSemesterEvents, selectedEventId],
  );
  const canEditSelectedEvent = isEditableDraftEvent(selectedEvent);

  const whatIfDiffs = useMemo(() => {
    if (!whatIf.data || !calendar) return [];
    const current = new Map(calendar.events.map(e => [e.draftEventId, e]));
    return whatIf.data.events
      .map(next => ({ next, prev: current.get(next.draftEventId) }))
      .filter(({ next, prev }) => prev && (dateInput(prev.startDate) !== dateInput(next.startDate) || dateInput(prev.endDate) !== dateInput(next.endDate)))
      .slice(0, 12);
  }, [calendar, whatIf.data]);

  // Filtre seçeneklerinden çıkarılan event türlerini filtrelenmiş listeden temizle
  const availableEventTypeFilterValues = useMemo(
    () => new Set(eventTypeFilterOptions.map(option => option.value)),
    [eventTypeFilterOptions],
  );
  const filteredSelectedEventTypeFilters = selectedEventTypeFilters.filter(value =>
    availableEventTypeFilterValues.has(value),
  );
  if (filteredSelectedEventTypeFilters.length !== selectedEventTypeFilters.length) {
    setSelectedEventTypeFilters(filteredSelectedEventTypeFilters);
  }

  function eventDatesChanged(ev: GeneratedCalendarEventDto | null, nextStartDate = editForm.startDate, nextEndDate = editForm.endDate || editForm.startDate) {
    if (!ev) return false;
    const oldStart = dateInput(ev.startDate);
    const oldEnd = dateInput(ev.endDate) || oldStart;
    return oldStart !== nextStartDate || oldEnd !== nextEndDate;
  }

  function toggleEventTypeFilter(value: CalendarEventCategory, checked: boolean) {
    setSelectedEventTypeFilters(current => {
      if (checked)
        return current.includes(value) ? current : [...current, value];

      return current.filter(item => item !== value);
    });
  }

  async function askCascadeShiftPreference(ev: GeneratedCalendarEventDto, actionLabel: string, nextStartDate = editForm.startDate, nextEndDate = editForm.endDate || editForm.startDate) {
    if (!eventDatesChanged(ev, nextStartDate, nextEndDate))
      return false;

    if (isCriticalEvent(ev)) {
      await askConfirm({
        title: 'Kritik olay',
        message: `${actionLabel}: Bu kritik akademik olay değiştiğinde takvim sırası korunmak için bağımlı sonraki olaylar da kaydırılır.`,
        confirmLabel: 'Tamam',
        showCancel: false,
        tone: 'info',
      });
      return true;
    }

    return askConfirm({
      title: 'Zincirleme kaydırma uygulansın mı?',
      message:
        `${actionLabel}: ${displayText(ev.title)} tarihi ${daySpan(ev)} -> ${fmt(utcDate(nextStartDate))}${nextEndDate && nextEndDate !== nextStartDate ? ` - ${fmt(utcDate(nextEndDate))}` : ''} olacak.\n\n` +
        'Bağımlı sonraki olaylar da aynı fark kadar kaydırılsın mı?',
      confirmLabel: 'Evet, zincirleme kaydır',
      cancelLabel: 'Hayır, sadece bu etkinlik',
      tone: 'info',
    });
  }

  function draftEventToEditForm(ev: GeneratedCalendarEventDto) {
    const startDate = dateInput(ev.startDate);
    return {
      title: displayText(ev.title),
      startDate,
      endDate: dateInput(ev.endDate) || startDate,
      note: ev.note ?? '',
    };
  }

  function selectEvent(ev: GeneratedCalendarEventDto) {
    setSelectedEventId(ev.draftEventId);
    setEditForm(draftEventToEditForm(ev));
    whatIf.reset();
  }

  function findDraftEvent(eventId: VisualizerEvent['id']) {
    return calendar?.events.find(event => event.draftEventId === String(eventId)) ?? null;
  }

  function resolveDroppedDates(ev: GeneratedCalendarEventDto, targetDate: string, sourceStartDate: string, sourceEndDate: string, dragAnchorDate: string) {
    const shiftDays = dayDifference(targetDate, dragAnchorDate);
    if (shiftDays === null)
      return null;

    const nextStartDate = shiftDateValue(sourceStartDate, shiftDays);
    const nextEndDate = shiftDateValue(sourceEndDate, shiftDays);
    if (!nextStartDate || !nextEndDate)
      return null;

    return { ...trimTrailingWeekendEnd(ev, nextStartDate, nextEndDate), shiftDays };
  }

  async function updateDraftEventDates(ev: GeneratedCalendarEventDto, nextStartDate: string, nextEndDate: string, actionLabel: string) {
    if (!activeDraftId || updateDraftEvent.isPending)
      return;

    if (!isEditableDraftEvent(ev)) {
      setNotice('Tatil ve bloklayici ozel gunler tarih duzenleme ile degistirilemez.');
      return;
    }

    const validationError = validateDraftEventDateEdit(ev, nextStartDate, nextEndDate);
    if (validationError) {
      setNotice(validationError);
      return;
    }

    const constraintError = validateDraftEventConstraints(ev, nextStartDate, nextEndDate, constraints);
    if (constraintError) {
      setNotice(constraintError);
      return;
    }

    const holidayOverlapError = calendar
      ? validateDraftEventHolidayOverlap(ev, nextStartDate, nextEndDate, calendar.holidays)
      : null;
    if (holidayOverlapError) {
      setNotice(holidayOverlapError);
      return;
    }

    setSelectedSemester(ev.semester);
    setSelectedEventId(ev.draftEventId);
    setEditForm({
      title: displayText(ev.title),
      startDate: nextStartDate,
      endDate: nextEndDate,
      note: ev.note ?? '',
    });
    whatIf.reset();

    if (!eventDatesChanged(ev, nextStartDate, nextEndDate)) {
      setNotice('Etkinlik zaten bu tarihte.');
      return;
    }

    const propagateChanges = await askCascadeShiftPreference(ev, actionLabel, nextStartDate, nextEndDate);
    const shouldUpdate = await askConfirm({
      title: 'Taslağa yazılsın mı?',
      message:
        `${displayText(ev.title)} taslakta guncellenecek.\n\n` +
        `Yeni tarih: ${nextStartDate}${nextEndDate !== nextStartDate ? ` - ${nextEndDate}` : ''}\n` +
        `Zincirleme kaydirma: ${propagateChanges ? 'uygulanacak' : 'uygulanmayacak'}\n\n` +
        'Bu islem taslagi degistirir ama gercek akademik takvime henuz yazmaz. Devam edilsin mi?',
      confirmLabel: 'Evet, taslağa yaz',
      cancelLabel: 'Vazgeç',
    });
    if (!shouldUpdate) {
      setNotice('Taslak guncelleme islemi iptal edildi.');
      return;
    }

    updateDraftEvent.mutate({
      draftId: activeDraftId,
      eventId: ev.draftEventId,
      dto: {
        startDate: utcDate(nextStartDate) ?? ev.startDate,
        endDate: nextEndDate === nextStartDate ? null : utcDate(nextEndDate),
        title: displayText(ev.title),
        note: ev.note || null,
        propagateChanges,
      },
    }, {
      onSuccess: data => {
        const updatedEvent = data.events.find(event => event.draftEventId === ev.draftEventId);
        setGenerated(null);
        setSelectedDraftId(data.id);
        setSelectedSemester(updatedEvent?.semester ?? ev.semester);
        setSelectedEventId(ev.draftEventId);
        setEditForm(updatedEvent ? draftEventToEditForm(updatedEvent) : {
          title: displayText(ev.title),
          startDate: nextStartDate,
          endDate: nextEndDate,
          note: ev.note ?? '',
        });
        showSuccessNotice(propagateChanges
          ? 'Taslak etkinligi guncellendi; bagimli tarihler yeniden hesaplandi.'
          : 'Taslak etkinligi guncellendi; zincirleme kaydirma uygulanmadi, sadece secili etkinlik degisti.');
      },
      onError: error => showErrorNotice(getErrorMessage(error)),
    });
  }

  async function handleDraftEventDrop({ eventId, targetDate, sourceStartDate, sourceEndDate, dragAnchorDate }: {
    eventId: VisualizerEvent['id'];
    targetDate: string;
    sourceStartDate: string;
    sourceEndDate: string;
    dragAnchorDate: string;
  }) {
    const ev = findDraftEvent(eventId);
    if (!ev) {
      setNotice('Tasınan etkinlik kimliği çözümlenemedi.');
      return;
    }

    const resolved = resolveDroppedDates(ev, targetDate, sourceStartDate, sourceEndDate, dragAnchorDate);
    if (!resolved) {
      setNotice('Bırakılan tarih okunamadı.');
      return;
    }

    if (resolved.shiftDays === 0)
      return;

    await updateDraftEventDates(ev, resolved.nextStartDate, resolved.nextEndDate, 'Sürükle-bırak ile tarih düzenleme');
  }

  function getDraftDropFeedback({ eventId, targetDate, sourceStartDate, sourceEndDate, dragAnchorDate }: DropFeedbackRequest): DropFeedback {
    if (!activeDraftId)
      return { allowed: false, reason: 'Açık taslak bulunamadı.' };

    const ev = findDraftEvent(eventId);
    if (!ev)
      return { allowed: false, reason: 'Etkinlik bulunamadı.' };

    if (!isEditableDraftEvent(ev))
      return { allowed: false, reason: 'Bu etkinlik sürükle-bırak ile değiştirilemez.' };

    const resolved = resolveDroppedDates(ev, targetDate, sourceStartDate, sourceEndDate, dragAnchorDate);
    if (!resolved)
      return { allowed: false, reason: 'Bırakılan tarih okunamadı.' };

    if (resolved.shiftDays === 0)
      return { allowed: false, reason: 'Etkinlik zaten bu tarihte.' };

    const validationError = validateDraftEventDateEdit(ev, resolved.nextStartDate, resolved.nextEndDate);
    if (validationError)
      return { allowed: false, reason: validationError };

    const constraintError = validateDraftEventConstraints(ev, resolved.nextStartDate, resolved.nextEndDate, constraints);
    if (constraintError)
      return { allowed: false, reason: constraintError };

    const holidayOverlapError = calendar
      ? validateDraftEventHolidayOverlap(ev, resolved.nextStartDate, resolved.nextEndDate, calendar.holidays)
      : null;
    if (holidayOverlapError)
      return { allowed: false, reason: holidayOverlapError };

    return { allowed: true, reason: 'Uygun hedef. Bırakınca taslak güncellemesi onaylanır.' };
  }

  function handleInvalidDraftEventDrop({ reason }: DropFeedbackRequest & { reason?: string }) {
    showErrorNotice(reason ?? 'Bu alana bırakma takvim kuralları nedeniyle uygulanamaz.');
  }

  function openDraftFromHistory(summary: CalendarDraftSummaryDto) {
    setGenerated(null);
    setSelectedDraftId(summary.id);
    setSelectedEventId(null);
    setSelectedSemester('Fall');
    setActiveTab('draft');
    setShowDraftHistory(false);
    setNotice(`Taslak #${summary.id} açıldı.`);
  }

  async function deleteDraftFromHistory(summary: CalendarDraftSummaryDto) {
    const shouldDelete = await askConfirm({
      title: 'Taslak silinsin mi?',
      message:
        `Taslak #${summary.id} (${summary.academicYear}) silinecek.\n\n` +
        'Bu işlem sadece taslak kaydını kaldırır. Daha önce gerçek akademik takvime uygulanmış etkinlikler silinmez; ancak bu taslak üzerinden geri alma yapılamaz.',
      confirmLabel: 'Evet, sil',
      cancelLabel: 'Vazgeç',
      tone: 'danger',
    });
    if (!shouldDelete)
      return;

    deleteDraft.mutate(summary.id, {
      onSuccess: () => {
        if (activeDraftId === summary.id) {
          setGenerated(null);
          setSelectedDraftId(null);
          setSelectedEventId(null);
        }
        setNotice(`Taslak #${summary.id} silindi.`);
      },
      onError: error => setNotice(getErrorMessage(error)),
    });
  }

  async function deleteAllDraftsFromHistory() {
    const summaries = drafts.data ?? [];
    if (summaries.length === 0)
      return;

    const shouldDelete = await askConfirm({
      title: 'Tüm taslaklar silinsin mi?',
      message:
        `${summaries.length} taslak silinecek.\n\n` +
        'Bu işlem sadece taslak kayıtlarını kaldırır. Daha önce gerçek akademik takvime uygulanmış etkinlikler silinmez; ancak silinen taslaklar üzerinden geri alma yapılamaz.',
      confirmLabel: 'Evet, tümünü sil',
      cancelLabel: 'Vazgeç',
      tone: 'danger',
    });
    if (!shouldDelete)
      return;

    try {
      for (const summary of summaries) {
        await deleteDraft.mutateAsync(summary.id);
      }

      if (activeDraftId && summaries.some(summary => summary.id === activeDraftId)) {
        setGenerated(null);
        setSelectedDraftId(null);
        setSelectedEventId(null);
      }

      setNotice(`${summaries.length} taslak silindi.`);
      await drafts.refetch();
    } catch (error) {
      setNotice(getErrorMessage(error));
    }
  }

  async function handleGenerate() {
    setNotice(null);
    const durationOverrideCount = Object.keys(durationOverrides).length;
    const shouldGenerate = await askConfirm({
      title: 'Taslak üretilecek',
      message:
        `${academicYear} akademik yılı için yeni bir taslak üretilecek.\n\n` +
        `Güz başlangıcı: ${fallStartDate}\n` +
        `Bahar başlangıcı: ${springStartDate}\n` +
        `Yaz okulu: ${includeSummer ? 'dahil' : 'dahil değil'}\n` +
        `Ek kısıt: ${constraints.length}\n` +
        `Süre ayarı: ${durationOverrideCount}\n\n` +
        'Bu işlem gerçek akademik takvime yazmaz; sadece 30 gün saklanan bir taslak oluşturur. Devam edilsin mi?',
      confirmLabel: 'Evet, üret',
      cancelLabel: 'Vazgeç',
      tone: 'info',
    });
    if (!shouldGenerate) {
      setNotice('Taslak üretme işlemi iptal edildi.');
      return;
    }
    generate.mutate({
      academicYear,
      fallStartDate: utcDate(fallStartDate),
      springStartDate: utcDate(springStartDate),
      includeSummer,
      constraints,
      templateDurationOverrides: durationOverrideCount > 0 ? durationOverrides : null,
    }, {
      onSuccess: data => {
        const failedEventCount = data.events.filter(event => event.isFailed).length;
        setGenerated(data);
        setSelectedDraftId(data.draftId);
        setSelectedSemester('Fall');
        setSelectedEventId(null);
        setActiveTab('draft');
        const successNotice = `Taslak #${data.draftId} oluşturuldu. Bu aşamada akademik takvim sayfasına yazılmaz; aktarmak için "Onayla ve kaydet" kullanın.`;
        setNotice(
          failedEventCount > 0
            ? `Taslak #${data.draftId} oluşturuldu ancak program seçtiğiniz kısıtlardan dolayı oluşturulamadı. Yerleşemeyen ${failedEventCount} etkinlik için taslak uyarılarını kontrol edin.`
            : successNotice,
        );
      },
      onError: error => setNotice(getErrorMessage(error)),
    });
  }

  async function handleWhatIf() {
    if (!activeDraftId || !selectedEvent) return;
    if (!canEditSelectedEvent) {
      setNotice('Tatil ve bloklayici ozel gunler tarih duzenleme ile degistirilemez.');
      return;
    }
    const validationError = validateDraftEventDateEdit(selectedEvent, editForm.startDate, editForm.endDate || editForm.startDate);
    if (validationError) {
      setNotice(validationError);
      return;
    }

    const propagateChanges = await askCascadeShiftPreference(selectedEvent, 'Zincirleme kaydırma ön izlemesi');
    whatIf.mutate({
      draftId: activeDraftId,
      dto: {
        draftEventId: selectedEvent.draftEventId,
        startDate: utcDate(editForm.startDate) ?? selectedEvent.startDate,
        endDate: utcDate(editForm.endDate || editForm.startDate),
        propagateChanges,
      },
    }, {
      onSuccess: () => setNotice(`What-if hesaplandı. ${propagateChanges ? 'Zincirleme kaydırma dahil edildi.' : 'Sadece seçili etkinlik denendi.'}`),
      onError: error => setNotice(getErrorMessage(error)),
    });
  }

  async function handleUpdateEvent() {
    if (!activeDraftId || !selectedEvent) return;
    if (!canEditSelectedEvent) {
      setNotice('Tatil ve bloklayici ozel gunler tarih duzenleme ile degistirilemez.');
      return;
    }
    const validationError = validateDraftEventDateEdit(selectedEvent, editForm.startDate, editForm.endDate || editForm.startDate);
    if (validationError) {
      setNotice(validationError);
      return;
    }

    const propagateChanges = await askCascadeShiftPreference(selectedEvent, 'Taslağa yaz');
    const shouldUpdate = await askConfirm({
      title: 'Taslağa yazılsın mı?',
      message:
        `${displayText(selectedEvent.title)} taslakta güncellenecek.\n\n` +
        `Yeni tarih: ${editForm.startDate}${editForm.endDate && editForm.endDate !== editForm.startDate ? ` - ${editForm.endDate}` : ''}\n` +
        `Zincirleme kaydırma: ${propagateChanges ? 'uygulanacak' : 'uygulanmayacak'}\n\n` +
        'Bu işlem taslağı değiştirir ama gerçek akademik takvime henüz yazmaz. Devam edilsin mi?',
      confirmLabel: 'Evet, taslağa yaz',
      cancelLabel: 'Vazgeç',
    });
    if (!shouldUpdate) {
      setNotice('Taslak güncelleme işlemi iptal edildi.');
      return;
    }
    updateDraftEvent.mutate({
      draftId: activeDraftId,
      eventId: selectedEvent.draftEventId,
      dto: {
        startDate: utcDate(editForm.startDate) ?? selectedEvent.startDate,
        endDate: utcDate(editForm.endDate || editForm.startDate),
        title: editForm.title,
        note: editForm.note || null,
        propagateChanges,
      },
    }, {
      onSuccess: data => {
        const updatedEvent = data.events.find(event => event.draftEventId === selectedEvent.draftEventId);
        setGenerated(null);
        setSelectedDraftId(data.id);
        setSelectedSemester(updatedEvent?.semester ?? selectedEvent.semester);
        setSelectedEventId(selectedEvent.draftEventId);
        setEditForm(updatedEvent ? draftEventToEditForm(updatedEvent) : {
          title: editForm.title,
          startDate: editForm.startDate,
          endDate: editForm.endDate || editForm.startDate,
          note: editForm.note,
        });
        showSuccessNotice(propagateChanges
          ? 'Taslak etkinliği güncellendi; bağımlı tarihler yeniden hesaplandı.'
          : 'Taslak etkinliği güncellendi; zincirleme kaydırma uygulanmadı, sadece seçili etkinlik değişti.');
      },
      onError: error => showErrorNotice(getErrorMessage(error)),
    });
  }

  async function handleApply() {
    if (!activeDraftId) return;
    if (adminTerms.isLoading) {
      setNotice('Dönem uyumu kontrol ediliyor. Lütfen birkaç saniye sonra tekrar deneyin.');
      return;
    }
    if (applySemesters.length === 0) {
      setNotice('En az bir yarıyıl seçmelisiniz.');
      return;
    }
    const willCreate = applySemesters.filter(s => !termAlignment.existingSemesters.includes(s));
    const shouldApply = await askConfirm({
      title: 'Akademik takvime kaydedilsin mi?',
      message:
        `Taslak #${activeDraftId} gerçek akademik takvime uygulanacak.\n\n` +
        `Uygulanacak yarıyıllar: ${semesterListText(applySemesters)}. İşaretlenmeyen yarıyıllara dokunulmaz.\n` +
        (willCreate.length > 0 ? `Yeni açılacak dönem(ler): ${semesterListText(willCreate)}.\n` : '') +
        'Sonuç: ilgili dönemlerin mevcut takvim işaretleri temizlenir ve bu taslaktaki etkinlikler yazılır.\n' +
        `${forceOverwrite ? 'Yayınlanmış dönemlerin üzerine yazma açık; sistem sadece kapalı/arşivlenmiş dönemleri engeller.' : 'Yayınlanmış veya daha ileri durumda bir dönem varsa işlem reddedilir; önce üzerine yazma izni açılmalıdır.'}\n\n` +
        'Devam etmek istiyor musunuz?',
      confirmLabel: 'Evet, onayla ve kaydet',
      cancelLabel: 'Vazgeç',
      tone: forceOverwrite ? 'danger' : 'default',
    });
    if (!shouldApply) {
      setNotice('Onayla ve kaydet işlemi iptal edildi.');
      return;
    }
    applyDraft.mutate({ draftId: activeDraftId, forceOverwrite, semesters: applySemesters }, {
      onSuccess: result => {
        const terms = result.termIds.length > 0 ? result.termIds.join(', ') : 'yok';
        showSuccessNotice(`${result.message} Akademik takvim sayfası yenilendi. Güncellenen dönemler: ${terms}.`);
      },
      onError: error => showErrorNotice(getErrorMessage(error)),
    });
  }

  async function handleRollback() {
    if (!activeDraftId) return;
    const shouldRollback = await askConfirm({
      title: 'Geri alınsın mı?',
      message:
        `Taslak #${activeDraftId} geri alınacak.\n\n` +
        'Sonuç: uygulanmış taslağın önceki snapshot verisi geri yüklenir. Kapalı dönemler değiştirilmez.\n\n' +
        'Devam etmek istiyor musunuz?',
      confirmLabel: 'Evet, geri al',
      cancelLabel: 'Vazgeç',
      tone: 'danger',
    });
    if (!shouldRollback) {
      setNotice('Geri alma işlemi iptal edildi.');
      return;
    }
    rollbackDraft.mutate(activeDraftId, {
      onSuccess: result => setNotice(`${result.message} Dönemler: ${result.termIds.join(', ')}`),
      onError: error => setNotice(getErrorMessage(error)),
    });
  }

  return {
    activeTab,
    setActiveTab,
    forceOverwrite,
    setForceOverwrite,
    applySemesters,
    setApplySemesters,
    selectedSemester,
    setSelectedSemester,
    selectedEventTypeFilters,
    setSelectedEventTypeFilters,
    selectedEventId,
    setSelectedEventId,
    editForm,
    setEditForm,
    showDraftHistory,
    setShowDraftHistory,
    holidays,
    drafts,
    generate,
    updateDraftEvent,
    whatIf,
    applyDraft,
    rollbackDraft,
    deleteDraft,
    adminTerms,
    calendar,
    activeDraftId,
    visibleHolidayDefinitions,
    termAlignment,
    semesterEvents,
    eventTypeFilterOptions,
    filteredSemesterEvents,
    hasActiveEventTypeFilter,
    eventTypeFilterSummary,
    selectedEvent,
    canEditSelectedEvent,
    whatIfDiffs,
    toggleEventTypeFilter,
    selectEvent,
    handleDraftEventDrop,
    getDraftDropFeedback,
    handleInvalidDraftEventDrop,
    openDraftFromHistory,
    deleteDraftFromHistory,
    deleteAllDraftsFromHistory,
    handleGenerate,
    handleWhatIf,
    handleUpdateEvent,
    handleApply,
    handleRollback,
  };
}
