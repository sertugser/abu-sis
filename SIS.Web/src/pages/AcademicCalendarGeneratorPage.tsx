import { useEffect, useMemo, useState } from 'react';
import { CheckCircle2, ChevronDown, ChevronLeft, ChevronRight, GitBranch, RefreshCw, RotateCcw, Save, Trash2, Wand2 } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { type DropFeedback, type DropFeedbackRequest, type VisualizerEvent } from '@/components/CalendarVisualizer';
import {
  DropdownMenu,
  DropdownMenuCheckboxItem,
  DropdownMenuContent,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { Input } from '@/components/ui/input';
import { Switch } from '@/components/ui/switch';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Textarea } from '@/components/ui/textarea';
import { cn } from '@/lib/utils';
import {
  CALENDAR_EVENT_LABELS,
  calendarEventBadgeVariant,
  calendarEventCategory,
  type CalendarEventCategory,
} from '@/lib/calendarEventTypes';
import {
  academicYearStart,
  buildAcademicYearSelectionRange,
  buildSemesterSelectionRange,
  dateInput,
  dayDifference,
  defaultAcademicYear,
  fmt,
  isDateValueWithinRange,
  isWeekend,
  maxDateValue,
  nearestMondayDateValue,
  parseUtcDisplayDate,
  shiftAcademicYear,
  shiftDateValue,
  utcDate,
} from '@/lib/calendarGenerator/dates';
import {
  SEMESTERS,
  isSemesterType,
  normalizeSemesters,
  semesterLabel,
  semesterListText,
  termTypeToSemester,
} from '@/lib/calendarGenerator/semesters';
import { useAdminTerms } from '../hooks/useAdminTerms';
import {
  type CalendarConstraintInputDto,
  type CalendarDraftSummaryDto,
  type GenerateCalendarResultDto,
  type GeneratedCalendarEventDto,
  type SemesterType,
  useApplyDraft,
  useCalendarDraft,
  useCalendarDrafts,
  useCalendarTemplates,
  useDeleteCalendarDraft,
  useGenerateCalendar,
  useHolidayDefinitions,
  useRollbackDraft,
  useUpdateDraftEvent,
  useWhatIfCalendar,
} from '../hooks/useCalendarGenerator';
import {
  CATEGORY_BADGE_VARIANTS,
  CATEGORY_FILTER_ORDER,
  CATEGORY_LABELS,
  NOTICE_AUTO_DISMISS_MS,
  displayText,
  type ActiveGeneratorSetting,
  type CalendarProcessOption,
  type CalendarView,
  type ConfirmOptions,
  type NoticeTone,
  type PendingConfirm,
} from '@/lib/calendarGenerator/types';
import {
  daySpan,
  eventCategoryLabel,
  eventCategoryValue,
  getDraftId,
  getErrorMessage,
  isAtaturkMemorialHoliday,
  isCriticalEvent,
  isEditableDraftEvent,
  parseConstraintParameters,
  readString,
  trimTrailingWeekendEnd,
  validateDraftEventConstraints,
  validateDraftEventDateEdit,
  validateDraftEventHolidayOverlap,
} from '@/lib/calendarGenerator/eventRules';
import { processOptionLabel } from '@/lib/calendarGenerator/processOptions';
import { SearchableProcessSelect } from '@/components/calendar-generator/SearchableProcessSelect';
import { TermSummaryCards } from '@/components/calendar-generator/TermSummaryCards';
import { WarningsPanel } from '@/components/calendar-generator/WarningsPanel';
import { HolidaysTab } from '@/components/calendar-generator/HolidaysTab';
import { DraftMiniCalendar } from '@/components/calendar-generator/DraftMiniCalendar';
import { DraftHistoryDialog } from '@/components/calendar-generator/DraftHistoryDialog';
import { ConfirmDialog } from '@/components/calendar-generator/ConfirmDialog';
import { ErrorDialog } from '@/components/calendar-generator/ErrorDialog';
import { NoticeToast } from '@/components/calendar-generator/NoticeToast';

export default function AcademicCalendarGeneratorPage() {
  const [activeTab, setActiveTab] = useState('generator');
  const [academicYear, setAcademicYear] = useState(defaultAcademicYear);
  const [includeSummer, setIncludeSummer] = useState(true);
  const [forceOverwrite, setForceOverwrite] = useState(false);
  // Uygula adımında hangi yarıyılların uygulanacağı (boş → henüz hesaplanmadı; varsayılan render sırasında dolar).
  const [applySemesters, setApplySemesters] = useState<SemesterType[]>([]);
  // Hangi yarıyıl seti için varsayılanın uygulandığını izler; set değişince (yeni taslak) sıfırlanır.
  const [appliedSemesterKey, setAppliedSemesterKey] = useState('');
  const [durationOverrides, setDurationOverrides] = useState<Record<string, number>>({});
  const [durationEventKey, setDurationEventKey] = useState('');
  const [durationDays, setDurationDays] = useState('');
  const [pinEventKey, setPinEventKey] = useState('');
  const [constraints, setConstraints] = useState<CalendarConstraintInputDto[]>([]);
  const [pinDate, setPinDate] = useState('');
  const [pinEndDate, setPinEndDate] = useState('');
  const [selectedSemester, setSelectedSemester] = useState<SemesterType>('Fall');
  const [selectedEventTypeFilters, setSelectedEventTypeFilters] = useState<CalendarEventCategory[]>([]);
  const [selectedDraftId, setSelectedDraftId] = useState<number | null>(null);
  const [generated, setGenerated] = useState<GenerateCalendarResultDto | null>(null);
  const [selectedEventId, setSelectedEventId] = useState<string | null>(null);
  const [editForm, setEditForm] = useState({ title: '', startDate: '', endDate: '', note: '' });
  const [notice, setNoticeMessage] = useState<string | null>(null);
  const [noticeTone, setNoticeTone] = useState<NoticeTone>('info');
  const [errorNotice, setErrorNotice] = useState<string | null>(null);
  const [pendingConfirm, setPendingConfirm] = useState<PendingConfirm | null>(null);
  const [showDraftHistory, setShowDraftHistory] = useState(false);

  useEffect(() => {
    if (!notice)
      return;

    const timer = window.setTimeout(() => {
      setNoticeMessage(null);
    }, NOTICE_AUTO_DISMISS_MS);

    return () => window.clearTimeout(timer);
  }, [notice]);

  const defaultStartDates = useMemo(() => {
    const parsedStart = academicYearStart(academicYear);
    return {
      fallStartDate: nearestMondayDateValue(parsedStart, 9, 22),
      springStartDate: nearestMondayDateValue(parsedStart + 1, 2, 23),
    };
  }, [academicYear]);
  const [customStartDates, setCustomStartDates] = useState<{
    academicYear: string;
    fallStartDate: string;
    springStartDate: string;
  } | null>(null);
  const hasCustomStartDates = customStartDates?.academicYear === academicYear;
  const fallStartDate = hasCustomStartDates ? customStartDates.fallStartDate : defaultStartDates.fallStartDate;
  const springStartDate = hasCustomStartDates ? customStartDates.springStartDate : defaultStartDates.springStartDate;

  function handleAcademicYearShift(offset: number) {
    setAcademicYear(current => shiftAcademicYear(current, offset));
  }

  const holidays = useHolidayDefinitions();
  const templates = useCalendarTemplates();
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
  const durationOptions = useMemo(() => {
    const options: CalendarProcessOption[] = [];

    for (const template of templates.data ?? []) {
      for (const item of template.items) {
        for (const semester of item.applicableSemesters) {
          options.push({
            key: `${item.key}:${semester}`,
            templateItemKey: item.key,
            eventType: item.eventType,
            eventTypeLabel: CALENDAR_EVENT_LABELS[item.eventType] ?? item.eventType,
            title: item.title,
            defaultDurationDays: item.defaultDurationDays,
            semester,
            chainCode: template.chainCode,
          });
        }
      }
    }

    return options.sort((a, b) => processOptionLabel(a).localeCompare(processOptionLabel(b), 'tr'));
  }, [templates.data]);
  const durationOptionKeys = useMemo(() => new Set(durationOptions.map(option => option.key)), [durationOptions]);
  const activeDurationEventKey = durationOptionKeys.has(durationEventKey) ? durationEventKey : '';
  const activePinEventKey = durationOptionKeys.has(pinEventKey) ? pinEventKey : '';

  const selectedDurationOption = useMemo(
    () => durationOptions.find(option => option.key === activeDurationEventKey) ?? null,
    [activeDurationEventKey, durationOptions],
  );

  const selectedPinOption = useMemo(
    () => durationOptions.find(option => option.key === activePinEventKey) ?? null,
    [activePinEventKey, durationOptions],
  );
  const academicYearSelectionRange = useMemo(
    () => buildAcademicYearSelectionRange(academicYear, includeSummer),
    [academicYear, includeSummer],
  );
  const pinSelectionRange = useMemo(
    () => selectedPinOption
      ? buildSemesterSelectionRange(academicYear, selectedPinOption.semester)
      : academicYearSelectionRange,
    [academicYear, academicYearSelectionRange, selectedPinOption],
  );
  const pinEndDateMin = pinDate ? maxDateValue(pinSelectionRange.min, pinDate) : pinSelectionRange.min;
  const activeGeneratorSettings = useMemo(() => {
    const settings: ActiveGeneratorSetting[] = constraints.map((constraint, index) => {
      const parameters = parseConstraintParameters(constraint.parameterJson);
      const semesterValue = readString(parameters?.semester);
      const semester = semesterValue && isSemesterType(semesterValue) ? semesterValue : null;
      const templateItemKey = readString(parameters?.templateItemKey);
      const option = semester && templateItemKey
        ? durationOptions.find(item => item.key === `${templateItemKey}:${semester}`) ?? null
        : null;
      const startDate = readString(parameters?.startDate);
      const endDate = readString(parameters?.endDate) ?? startDate;
      const title = option
        ? processOptionLabel(option)
        : constraint.description ?? CALENDAR_EVENT_LABELS[constraint.targetEventType ?? ''] ?? 'Kısıt';
      const dateSummary = startDate
        ? dateInput(startDate) === dateInput(endDate)
          ? fmt(startDate)
          : `${fmt(startDate)} - ${fmt(endDate)}`
        : null;

      return {
        key: `constraint:${index}`,
        badgeLabel: constraint.constraintType,
        badgeVariant: constraint.isHard ? 'destructive' : 'secondary',
        title,
        summary: constraint.description ?? `${title} için kısıt tanımlandı.`,
        detailLines: [
          option ? `Süreç: ${processOptionLabel(option)}` : null,
          semester ? `Dönem: ${semesterLabel(semester)}` : null,
          dateSummary ? `Sabit aralık: ${dateSummary}` : null,
          constraint.targetChainCode ? `Zincir: ${constraint.targetChainCode}` : null,
          constraint.targetEventType ? `Etkinlik türü: ${CALENDAR_EVENT_LABELS[constraint.targetEventType] ?? constraint.targetEventType}` : null,
        ].filter((line): line is string => !!line),
        removeKind: 'constraint',
        removeIndex: index,
      };
    });

    Object.entries(durationOverrides).forEach(([key, days]) => {
      const option = durationOptions.find(item => item.key === key) ?? null;
      const delta = option ? days - option.defaultDurationDays : null;

      settings.push({
        key: `duration:${key}`,
        badgeLabel: 'Süre',
        badgeVariant: 'info',
        title: option ? processOptionLabel(option) : key,
        summary: `${days} gün olarak ayarlandı.`,
        detailLines: [
          option ? `Süreç: ${processOptionLabel(option)}` : null,
          option ? `Varsayılan süre: ${option.defaultDurationDays} gün` : null,
          `Yeni süre: ${days} gün`,
          delta !== null ? `Değişim: ${delta > 0 ? `+${delta}` : delta} gün` : null,
        ].filter((line): line is string => !!line),
        removeKind: 'duration',
        removeKey: key,
      });
    });

    return settings;
  }, [constraints, durationOptions, durationOverrides]);

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

  function eventDatesChanged(ev: GeneratedCalendarEventDto | null, nextStartDate = editForm.startDate, nextEndDate = editForm.endDate || editForm.startDate) {
    if (!ev) return false;
    const oldStart = dateInput(ev.startDate);
    const oldEnd = dateInput(ev.endDate) || oldStart;
    return oldStart !== nextStartDate || oldEnd !== nextEndDate;
  }

  // PinDate range dışındaysa temizle (adjust state during render)
  if (pinDate && !isDateValueWithinRange(pinDate, pinSelectionRange)) {
    setPinDate('');
  }

  // PinEndDate range dışındaysa temizle; PinDate'den önceyse PinDate'e eşitle
  if (pinEndDate) {
    if (!isDateValueWithinRange(pinEndDate, pinSelectionRange)) {
      setPinEndDate('');
    } else if (pinDate && pinEndDate < pinDate) {
      setPinEndDate(pinDate);
    }
  }

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

  function askConfirm(options: ConfirmOptions) {
    return new Promise<boolean>(resolve => {
      setPendingConfirm({ ...options, resolve });
    });
  }

  function closeConfirm(value: boolean) {
    const resolver = pendingConfirm?.resolve;
    setPendingConfirm(null);
    resolver?.(value);
  }

  function setFallStartDate(value: string) {
    setCustomStartDates(current => ({
      academicYear,
      fallStartDate: value,
      springStartDate: current?.academicYear === academicYear
        ? current.springStartDate
        : defaultStartDates.springStartDate,
    }));
  }

  function setSpringStartDate(value: string) {
    setCustomStartDates(current => ({
      academicYear,
      fallStartDate: current?.academicYear === academicYear
        ? current.fallStartDate
        : defaultStartDates.fallStartDate,
      springStartDate: value,
    }));
  }

  function handleDurationEventChange(key: string) {
    const option = durationOptions.find(item => item.key === key);
    setDurationEventKey(key);
    setDurationDays(String(durationOverrides[key] ?? option?.defaultDurationDays ?? 1));
  }

  function handlePinEventChange(key: string) {
    setPinEventKey(key);
  }

  function handlePinDateChange(value: string) {
    setPinDate(value);
    if (pinEndDate && value && pinEndDate < value)
      setPinEndDate(value);
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

  function setNotice(message: string | null) {
    setNoticeTone('info');
    setNoticeMessage(message);
  }

  function showNotice(message: string, tone: NoticeTone = 'info') {
    setNoticeTone(tone);
    setNoticeMessage(message);
    if (tone !== 'error')
      setErrorNotice(null);
  }

  function showSuccessNotice(message: string) {
    showNotice(message, 'success');
  }

  function showErrorNotice(message: string, showDialog = true) {
    showNotice(message, 'error');
    if (showDialog)
      setErrorNotice(message);
  }

  function handleInvalidDraftEventDrop({ reason }: DropFeedbackRequest & { reason?: string }) {
    showErrorNotice(reason ?? 'Bu alana bırakma takvim kuralları nedeniyle uygulanamaz.');
  }

  async function addPinConstraint() {
    if (!pinDate || !selectedPinOption) return;
    const resolvedEndDate = pinEndDate || pinDate;
    if (resolvedEndDate < pinDate) {
      setNotice('Bitiş tarihi başlangıç tarihinden önce olamaz.');
      return;
    }
    if (!isDateValueWithinRange(pinDate, pinSelectionRange) || !isDateValueWithinRange(resolvedEndDate, pinSelectionRange)) {
      setNotice(`Seçilen süreç için izin verilen tarih aralığı ${pinSelectionRange.label}.`);
      return;
    }
    const label = processOptionLabel(selectedPinOption);

    const isExam = calendarEventCategory(selectedPinOption.eventType, selectedPinOption.title, selectedPinOption.templateItemKey) === 'exam';
    if (isExam) {
      const parsedStart = parseUtcDisplayDate(pinDate);
      const parsedEnd = parseUtcDisplayDate(resolvedEndDate);
      if (parsedStart && isWeekend(parsedStart)) {
        setNotice('Sınav başlangıç tarihi hafta sonuna denk gelemez.');
        return;
      }
      if (parsedEnd && isWeekend(parsedEnd)) {
        setNotice('Sinav bitis tarihi hafta sonuna denk gelemez.');
        return;
      }
    }
    const shouldAdd = await askConfirm({
      title: 'Tarih sabitlensin mi?',
      message: `${label} etkinliği ${pinDate}${pinEndDate ? ` - ${pinEndDate}` : ''} tarihine sabitlenecek.\n\nBu kısıt bir sonraki taslak üretiminde dikkate alınacak. Devam edilsin mi?`,
      confirmLabel: 'Evet, ekle',
      cancelLabel: 'Vazgeç',
    });
    if (!shouldAdd)
      return;

    setConstraints(prev => [...prev, {
      constraintType: 'Pin',
      targetEventType: selectedPinOption.eventType,
      targetChainCode: selectedPinOption.chainCode,
      parameterJson: JSON.stringify({
        startDate: utcDate(pinDate),
        endDate: utcDate(resolvedEndDate),
        templateItemKey: selectedPinOption.templateItemKey,
        semester: selectedPinOption.semester,
      }),
      isHard: true,
      priority: 1,
      description: `${label} ${pinDate}${resolvedEndDate !== pinDate ? ` - ${resolvedEndDate}` : ''} aralığına sabitlendi`,
    }]);
    setPinDate('');
    setPinEndDate('');
    setNotice(`${label} için tarih sabitleme kısıtı eklendi.`);
  }

  async function addDurationOverride() {
    if (!selectedDurationOption) return;

    const days = Number(durationDays);
    if (!Number.isInteger(days) || days <= 0) {
      setNotice('Süre ayarı en az 1 tam gün olmalı.');
      return;
    }

    const label = processOptionLabel(selectedDurationOption);
    const shouldAdd = await askConfirm({
      title: 'Süre ayarı eklensin mi?',
      message:
        `${label} süresi bu taslak üretiminde ${selectedDurationOption.defaultDurationDays} gün yerine ${days} gün olacak.\n\n` +
        'Bu ayar sadece yeni taslak üretirken kullanılır; mevcut takvime hemen yazılmaz.',
      confirmLabel: 'Evet, süreyi ayarla',
      cancelLabel: 'Vazgeç',
      tone: 'info',
    });
    if (!shouldAdd)
      return;

    setDurationOverrides(prev => ({ ...prev, [selectedDurationOption.key]: days }));
    setNotice(`${label} için süre ayarı ${days} gün olarak eklendi.`);
  }

  function removeDurationOverride(key: string) {
    setDurationOverrides(prev => {
      const next = { ...prev };
      delete next[key];
      return next;
    });
    setNotice('Süre ayarı kaldırıldı.');
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

  function renderGeneratorTab() {
    return (
      <div className="grid gap-3 xl:grid-cols-[1.2fr_0.8fr]">
          <div className="rounded-lg border border-slate-200 bg-white p-3 shadow-sm">
            <div className="grid gap-3 md:grid-cols-4">
            <label className="space-y-0.5 text-sm font-medium text-slate-700">
              Akademik yıl
              <div className="flex items-center gap-2">
                <Button type="button" variant="outline" size="icon" onClick={() => handleAcademicYearShift(-1)} aria-label="Önceki akademik yıl">
                  <ChevronLeft className="h-4 w-4" />
                </Button>
                <Input value={academicYear} onChange={e => setAcademicYear(e.target.value)} placeholder="2025-2026" />
                <Button type="button" variant="outline" size="icon" onClick={() => handleAcademicYearShift(1)} aria-label="Sonraki akademik yıl">
                  <ChevronRight className="h-4 w-4" />
                </Button>
              </div>
            </label>
            <label className="space-y-0.5 text-sm font-medium text-slate-700">
              Güz ders başlangıcı
              <Input type="date" value={fallStartDate} onChange={e => setFallStartDate(e.target.value)} />
            </label>
            <label className="space-y-0.5 text-sm font-medium text-slate-700">
              Bahar ders başlangıcı
              <Input type="date" value={springStartDate} onChange={e => setSpringStartDate(e.target.value)} />
            </label>
            <div className="flex items-end">
              <label className="flex h-10 items-center gap-3 rounded-lg border border-slate-200 px-3 text-sm font-medium text-slate-700">
                <Switch checked={includeSummer} onCheckedChange={setIncludeSummer} />
                Yaz okulu dahil
              </label>
            </div>
          </div>

          <div className="mt-3">
            <div className="rounded-lg border border-slate-200 p-2.5">
              <div className="mb-2 flex items-center gap-2 text-sm font-semibold text-slate-800">
                <GitBranch className="h-3.5 w-3.5" />
                Tarih Sabitleme
              </div>
              <p className="text-xs text-slate-500">
                Belirli bir etkinliğin tarihini sabitlemek için seçin ve başlangıç / bitiş tarihini girin.
              </p>
              <div className="mt-2 grid gap-2 md:grid-cols-[minmax(0,1.5fr)_140px_140px]">
                <SearchableProcessSelect
                  options={durationOptions}
                  selectedKey={activePinEventKey}
                  onChange={handlePinEventChange}
                  disabled={templates.isLoading || durationOptions.length === 0}
                  placeholder={templates.isLoading ? 'Yükleniyor...' : 'Etkinlik seçin'}
                />
                <Input
                  type="date"
                  value={pinDate}
                  min={pinSelectionRange.min}
                  max={pinSelectionRange.max}
                  onChange={e => handlePinDateChange(e.target.value)}
                />
                <Input
                  type="date"
                  value={pinEndDate}
                  min={pinEndDateMin}
                  max={pinSelectionRange.max}
                  onChange={e => setPinEndDate(e.target.value)}
                />
              </div>
              <p className="mt-1 text-xs text-slate-500">
                Seçilebilir tarih aralığı: {pinSelectionRange.label}
              </p>
              {selectedPinOption && (
                <p className="mt-1 text-xs font-medium text-slate-700">
                  Seçilen: {processOptionLabel(selectedPinOption)}. Yalnızca seçilen dönem için.
                </p>
              )}
              <Button type="button" size="sm" className="mt-2" onClick={addPinConstraint} disabled={!activePinEventKey || !selectedPinOption}>
                Kısıt Ekle
              </Button>
            </div>
          </div>

          <div className="mt-3 rounded-lg border border-slate-200 p-2.5">
            <div className="text-sm font-semibold text-slate-800">Süre Ayarları</div>
            <p className="text-xs text-slate-500">
              Bir etkinliğin varsayılan süresini değiştirmek için seçin ve yeni gün sayısını girin.
            </p>

            <div className="mt-2 grid gap-2 md:grid-cols-[minmax(0,1.5fr)_140px_auto]">
              <SearchableProcessSelect
                options={durationOptions}
                selectedKey={activeDurationEventKey}
                onChange={handleDurationEventChange}
                disabled={templates.isLoading || durationOptions.length === 0}
                placeholder={templates.isLoading ? 'Yükleniyor...' : 'Etkinlik seçin'}
              />
              <Input
                type="number"
                min={1}
                value={durationDays}
                onChange={e => setDurationDays(e.target.value)}
                placeholder="Gün"
              />
              <Button type="button" size="sm" onClick={addDurationOverride} disabled={!selectedDurationOption}>
                Süre ayarı ekle
              </Button>
            </div>
            {selectedDurationOption && (
              <p className="mt-1 text-xs font-medium text-slate-700">
                Seçilen: {processOptionLabel(selectedDurationOption)}. Varsayılan: {selectedDurationOption.defaultDurationDays} gün.
              </p>
            )}

            <div className="mt-2 space-y-1.5">
              {Object.keys(durationOverrides).length === 0 ? (
                <p className="text-xs text-slate-500">Henüz süre ayarı yok.</p>
              ) : Object.entries(durationOverrides).map(([key, days]) => {
                const option = durationOptions.find(item => item.key === key);
                return (
                  <div key={key} className="flex items-center justify-between gap-3 rounded-lg border border-slate-200 px-3 py-2 text-sm">
                    <div>
                      <div className="font-medium text-slate-800">{option ? processOptionLabel(option) : key}</div>
                      <div className="text-xs text-slate-500">
                        {option ? `${option.defaultDurationDays} gün yerine ` : ''}{days} gün
                      </div>
                    </div>
                    <Button type="button" size="sm" variant="ghost" onClick={() => removeDurationOverride(key)}>
                      Sil
                    </Button>
                  </div>
                );
              })}
            </div>
          </div>

          <div className="mt-3 flex flex-wrap items-center gap-2">
            <Button size="sm" onClick={handleGenerate} disabled={generate.isPending}>
              {generate.isPending ? <RefreshCw className="h-4 w-4 animate-spin" /> : <Wand2 className="h-4 w-4" />}
              Taslak Üret
            </Button>
            <Button type="button" size="sm" variant="outline" onClick={() => {
              setConstraints([]);
              setDurationOverrides({});
              setDurationEventKey('');
              setDurationDays('');
              setPinEventKey('');
              setPinDate('');
              setPinEndDate('');
              setNotice('Tüm kısıtlar ve süre ayarları sıfırlandı.');
            }}>
              <Trash2 className="h-4 w-4" />
              Sıfırla
            </Button>
            <span className="text-xs text-slate-500">Taslaklar 30 gün saklanır.</span>
          </div>
        </div>

        <div className="rounded-lg border border-slate-200 bg-white p-3 shadow-sm">
          <h2 className="text-sm font-semibold text-slate-900">Aktif Kısıtlar</h2>
          <div className="mt-2 space-y-1.5">
            {activeGeneratorSettings.length === 0 ? (
              <p className="text-sm text-slate-500">Henüz ek kısıt yok.</p>
            ) : activeGeneratorSettings.map(setting => (
              <div key={setting.key} className="rounded-lg border border-slate-200 px-2.5 py-2">
                <div className="flex items-start justify-between gap-2">
                  <div className="min-w-0">
                    <Badge variant={setting.badgeVariant}>{setting.badgeLabel}</Badge>
                    <p className="mt-1 text-xs font-semibold text-slate-800">{setting.title}</p>
                    <p className="text-xs text-slate-700">{setting.summary}</p>
                    {setting.detailLines.length > 0 && (
                      <div className="mt-1 grid gap-0.5 text-[11px] text-slate-500">
                        {setting.detailLines.map(line => (
                          <div key={`${setting.key}:${line}`}>{line}</div>
                        ))}
                      </div>
                    )}
                  </div>
                  <Button
                    type="button"
                    size="sm"
                    variant="ghost"
                    onClick={() => {
                      if (setting.removeKind === 'constraint') {
                        setConstraints(prev => prev.filter((_, i) => i !== setting.removeIndex));
                        return;
                      }
                      if (setting.removeKey)
                        removeDurationOverride(setting.removeKey);
                    }}
                  >
                    Sil
                  </Button>
                </div>
              </div>
            ))}
          </div>

          <div className="mt-3 border-t border-cyan-200 pt-2 text-sm text-cyan-900">
            <div className="text-xs font-semibold">Yönetmelik Kontrolü</div>
            <div className="mt-1 space-y-0.5 text-[11px]">
              <div>Normal: 14 hafta ders, sınavlarla 16 hafta. Yaz: 7 hafta.</div>
              <div>Çekilme/dondurma: 7. hafta. Sınav sonuçları: 10 iş günü.</div>
            </div>
          </div>
        </div>
      </div>
    );
  }

  function renderDraftTab() {
    if (!calendar) {
      return (
        <div className="rounded-lg border border-dashed border-slate-300 bg-white p-8 text-center text-sm text-slate-500">
          Henüz taslak yok. "Kısıtlar & Üret" sekmesinden bir taslak oluşturun.
        </div>
      );
    }

    return (
      <div className="space-y-3">
        <WarningsPanel current={calendar} />

        <div className="grid gap-3 lg:grid-cols-3 items-start">
          <div className="space-y-3 lg:col-span-2">
            <div className="flex flex-col rounded-lg border border-slate-200 bg-white p-3 shadow-sm">
              <div className="flex shrink-0 flex-wrap items-center justify-between gap-2">
                <div>
                  <h2 className="text-sm font-semibold text-slate-900">Taslak #{activeDraftId}</h2>
                  <p className="text-xs text-slate-500">
                    {calendar.academicYear} için {calendar.events.length} etkinlik üretildi.
                    {hasActiveEventTypeFilter ? ` Görünen: ${filteredSemesterEvents.length}/${semesterEvents.length}.` : ''}
                  </p>
                </div>
                <div className="flex flex-wrap gap-2">
                  {SEMESTERS.map(tab => (
                    <Button
                      key={tab.value}
                      type="button"
                      size="sm"
                      variant={selectedSemester === tab.value ? 'default' : 'outline'}
                      onClick={() => {
                        setSelectedSemester(tab.value);
                        setSelectedEventId(null);
                        whatIf.reset();
                      }}
                    >
                      {tab.label}
                    </Button>
                  ))}
                </div>
              </div>

              <div className="mt-3 flex flex-1 flex-col overflow-hidden rounded-lg border border-slate-200">
                <div className="shrink-0 grid grid-cols-[minmax(0,1fr)_96px_120px] items-center bg-slate-50 px-2 py-1.5 text-[11px] font-semibold uppercase text-slate-500 sm:grid-cols-[minmax(0,1.5fr)_130px_140px]">
                  <span>Etkinlik</span>
                  <span className="text-right sm:text-left">Tarih</span>
                  <div className="flex justify-end sm:justify-start">
                    <DropdownMenu>
                      <DropdownMenuTrigger asChild>
                        <button
                          type="button"
                          className="inline-flex items-center gap-1 rounded-md px-2 py-1 text-[11px] font-semibold uppercase text-slate-500 transition hover:bg-slate-100 hover:text-slate-700"
                        >
                          {eventTypeFilterSummary}
                          <ChevronDown className="h-3.5 w-3.5" />
                        </button>
                      </DropdownMenuTrigger>
                      <DropdownMenuContent align="end" className="w-56">
                        <DropdownMenuLabel>Tür filtresi</DropdownMenuLabel>
                        <DropdownMenuCheckboxItem checked={!hasActiveEventTypeFilter} onCheckedChange={() => setSelectedEventTypeFilters([])}>
                          Tüm türler
                        </DropdownMenuCheckboxItem>
                        <DropdownMenuSeparator />
                        {eventTypeFilterOptions.map(option => (
                          <DropdownMenuCheckboxItem
                            key={option.value}
                            checked={selectedEventTypeFilters.includes(option.value)}
                            onCheckedChange={checked => toggleEventTypeFilter(option.value, checked === true)}
                          >
                            <div className="flex w-full items-center justify-between gap-3">
                              <Badge variant={option.badgeVariant}>{option.label}</Badge>
                              <span className="text-xs text-slate-500">{option.count}</span>
                            </div>
                          </DropdownMenuCheckboxItem>
                        ))}
                      </DropdownMenuContent>
                    </DropdownMenu>
                  </div>
                </div>
                <div className="flex-1 overflow-auto min-h-[320px]">
                  {filteredSemesterEvents.length === 0 ? (
                    <div className="px-4 py-10 text-center text-sm text-slate-500">
                      <p>{hasActiveEventTypeFilter ? 'Seçilen tür filtresine uygun etkinlik bulunamadı.' : 'Bu dönem için etkinlik yok.'}</p>
                      {hasActiveEventTypeFilter && (
                        <div className="mt-3">
                          <Button type="button" size="sm" variant="outline" onClick={() => setSelectedEventTypeFilters([])}>
                            Filtreyi Temizle
                          </Button>
                        </div>
                      )}
                    </div>
                  ) : filteredSemesterEvents.map(ev => {
                    const isSelected = selectedEventId === ev.draftEventId;
                    return (
                      <button
                        key={ev.draftEventId}
                        type="button"
                        onClick={() => selectEvent(ev)}
                        className={cn(
                          'relative grid w-full grid-cols-[minmax(0,1fr)_96px_120px] items-center gap-1 border-t px-2 py-1.5 text-left text-sm transition-all duration-150 hover:bg-slate-100 sm:grid-cols-[minmax(0,1.5fr)_130px_140px]',
                          isSelected
                            ? 'z-10 border-transparent bg-primary/10 ring-2 ring-inset ring-primary shadow-sm'
                            : 'border-slate-100',
                        )}
                        aria-pressed={isSelected}
                      >
                        <span className="min-w-0">
                          <span className={cn('block truncate font-medium text-slate-800', isSelected && 'text-primary')}>{displayText(ev.title)}</span>
                        </span>
                        <span className={cn('text-right text-xs text-slate-600 sm:text-left', isSelected && 'font-semibold text-primary')}>
                          {daySpan(ev)}
                        </span>
                        <span className="flex flex-wrap justify-end gap-1 sm:justify-start">
                          <Badge variant={calendarEventBadgeVariant(ev.eventType, ev.title, ev.templateItemKey)}>
                            {eventCategoryLabel(ev.eventType, ev.isHoliday, ev.title, ev.templateItemKey)}
                          </Badge>
                          {ev.isFailed && <Badge variant="destructive">Hata</Badge>}
                        </span>
                      </button>
                    );
                  })}
                </div>
              </div>
            </div>

          </div>

          <div className="space-y-3">
              <div className="rounded-lg border border-slate-200 bg-white p-3 shadow-sm">
              <h2 className="text-sm font-semibold text-slate-900">Tarih Düzenleme</h2>
              {!selectedEvent ? (
                <p className="mt-2 text-sm text-slate-500">Bir etkinlik seçin, tarihi değiştirin ve zincirleme kaydırmayı ön izleyin.</p>
              ) : !canEditSelectedEvent ? (
                <div className="mt-3 space-y-3">
                  <div className="rounded-lg border border-amber-200 bg-amber-50 px-3 py-3 text-sm text-amber-900">
                    Tatil ve bloklayici ozel gunler tarih duzenleme ile degistirilemez.
                  </div>
                  <div className="grid grid-cols-2 gap-3 text-sm">
                    <div>
                      <p className="font-medium text-slate-500">Etkinlik</p>
                      <p className="mt-1 text-slate-900">{displayText(selectedEvent.title)}</p>
                    </div>
                    <div>
                      <p className="font-medium text-slate-500">Tarih</p>
                      <p className="mt-1 text-slate-900">{daySpan(selectedEvent)}</p>
                    </div>
                  </div>
                </div>
              ) : (
                <div className="mt-2 space-y-2">
                  <label className="block space-y-0.5 text-sm font-medium text-slate-700">
                    Başlık
                    <Input value={editForm.title} onChange={e => setEditForm(prev => ({ ...prev, title: e.target.value }))} />
                  </label>
                  <div className="grid grid-cols-2 gap-2">
                    <label className="block space-y-0.5 text-sm font-medium text-slate-700">
                      Başlangıç
                      <Input type="date" value={editForm.startDate} onChange={e => setEditForm(prev => ({ ...prev, startDate: e.target.value }))} />
                    </label>
                    <label className="block space-y-1 text-sm font-medium text-slate-700">
                      Bitiş
                      <Input type="date" value={editForm.endDate} onChange={e => setEditForm(prev => ({ ...prev, endDate: e.target.value }))} />
                    </label>
                  </div>
                  <label className="block space-y-0.5 text-sm font-medium text-slate-700">
                    Not
                    <Textarea rows={2} value={editForm.note} onChange={e => setEditForm(prev => ({ ...prev, note: e.target.value }))} />
                  </label>
                  <div className="flex flex-wrap gap-2">
                    <Button type="button" size="sm" variant="outline" onClick={handleWhatIf} disabled={whatIf.isPending}>
                      <GitBranch className="h-4 w-4" />
                      Zincirleme Önizle
                    </Button>
                    <Button type="button" size="sm" onClick={handleUpdateEvent} disabled={updateDraftEvent.isPending}>
                      <Save className="h-4 w-4" />
                      Taslağa Yaz
                    </Button>
                  </div>
                </div>
              )}
              </div>
              {renderDraftSidePanels(calendar)}
          </div>
        </div>
      </div>
    );
  }

  function renderDraftSidePanels(current: CalendarView) {
    return (
      <>
        {termAlignment.hasMismatch && (
          <div className="rounded-lg border border-red-200 bg-red-50 p-3 shadow-sm">
            <h2 className="text-sm font-semibold text-red-900">Dönem uyumsuzluğu</h2>
            <p className="mt-1 text-xs leading-5 text-red-800">
              {termAlignment.message}
            </p>
          </div>
        )}

        <div className="rounded-lg border border-slate-200 bg-white p-3 shadow-sm">
          <h2 className="text-sm font-semibold text-slate-900">Zincirleme Kaydırma Farkı</h2>
          {whatIfDiffs.length === 0 ? (
            <p className="mt-1 text-xs text-slate-500">Hesaplanmış fark yok.</p>
          ) : (
            <div className="mt-3 space-y-2">
              {whatIfDiffs.map(({ next, prev }) => (
                <div key={next.draftEventId} className="rounded-lg border border-slate-200 px-3 py-2 text-xs">
                  <div className="font-semibold text-slate-800">{displayText(next.title)}</div>
                  <div className="mt-1 text-slate-500">
                    {fmt(prev?.startDate)} -&gt; {fmt(next.startDate)}
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>

        <div className="rounded-lg border border-slate-200 bg-white p-3 shadow-sm">
          <div className="flex items-center justify-between gap-2">
            <div>
              <h2 className="text-sm font-semibold text-slate-900">Onayla ve kaydet</h2>
              <p className="mt-0.5 max-w-md text-xs leading-5 text-slate-600">
                Taslak onaylandığında eski takvim oluşturucu kayıtları temizlenir, bu etkinlikler yazılır.
                {forceOverwrite ? ' Yayınlanmış dönemlerin üzerine yazma açık.' : ' Yayınlanmış dönem varsa işlem reddedilir.'}
              </p>
              {termAlignment.hasMismatch && (
                <p className="mt-1 max-w-md text-xs font-medium text-red-700">
                  Uyarı: {termAlignment.message}
                </p>
              )}
            </div>
            <Switch checked={forceOverwrite} onCheckedChange={setForceOverwrite} />
          </div>
          {termAlignment.expectedSemesters.length > 0 && (
            <div className="mt-3 border-t border-slate-200 pt-3">
              <p className="text-xs font-medium text-slate-700">Uygulanacak yarıyıllar</p>
              <p className="mt-0.5 text-[11px] text-slate-500">
                İşaretlemediğin yarıyıla dokunulmaz; eşleşen dönem yoksa yeni dönem açılır.
              </p>
              <div className="mt-1.5 flex flex-wrap gap-2">
                {termAlignment.expectedSemesters.map(s => {
                  const exists = termAlignment.existingSemesters.includes(s);
                  const checked = applySemesters.includes(s);
                  return (
                    <label
                      key={s}
                      className="flex items-center gap-2 rounded-md border border-slate-200 px-2.5 py-1.5 text-xs cursor-pointer hover:bg-slate-50"
                    >
                      <input
                        type="checkbox"
                        checked={checked}
                        onChange={e =>
                          setApplySemesters(prev =>
                            e.target.checked ? [...prev, s] : prev.filter(x => x !== s),
                          )
                        }
                        className="h-3.5 w-3.5 accent-slate-900"
                      />
                      <span className="font-medium text-slate-800">{semesterLabel(s)}</span>
                      <span className={exists ? 'text-emerald-600' : 'text-amber-600'}>
                        {exists ? '(mevcut)' : '(yeni dönem açılacak)'}
                      </span>
                    </label>
                  );
                })}
              </div>
            </div>
          )}
          <div className="mt-2 flex flex-wrap gap-2">
            <Button type="button" onClick={handleApply} disabled={!activeDraftId || applyDraft.isPending || adminTerms.isLoading}>
              {applyDraft.isPending ? <RefreshCw className="h-4 w-4 animate-spin" /> : <CheckCircle2 className="h-4 w-4" />}
              {applyDraft.isPending ? 'Kaydediliyor' : 'Onayla'}
            </Button>
            <Button type="button" variant="outline" onClick={handleRollback} disabled={!activeDraftId || rollbackDraft.isPending}>
              <RotateCcw className="h-4 w-4" />
              Geri al
            </Button>
          </div>
        </div>

        <DraftMiniCalendar
          current={current}
          whatIfData={whatIf.data}
          onEventDrop={handleDraftEventDrop}
          getDropFeedback={getDraftDropFeedback}
          onInvalidEventDrop={handleInvalidDraftEventDrop}
          dragDisabled={updateDraftEvent.isPending}
        />
        <TermSummaryCards current={current} />
      </>
    );
  }

  return (
    <div className="space-y-3">
      <Tabs value={activeTab} onValueChange={setActiveTab} className="space-y-3">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <TabsList className="bg-white">
            <TabsTrigger value="generator">Kısıtlar & Üret</TabsTrigger>
            <TabsTrigger value="draft">Taslak & Zincirleme</TabsTrigger>
            <TabsTrigger value="holidays">Tatil Tanımları</TabsTrigger>
          </TabsList>
          <div className="flex flex-wrap items-center gap-2">
            <Button type="button" variant="outline" size="sm" onClick={() => setShowDraftHistory(true)}>
              Taslak geçmişi
            </Button>
          </div>
        </div>

        <ErrorDialog message={errorNotice} onClose={() => setErrorNotice(null)} />

        <NoticeToast notice={notice} tone={noticeTone} />

        <TabsContent value="generator">{renderGeneratorTab()}</TabsContent>
        <TabsContent value="draft">{renderDraftTab()}</TabsContent>
        <TabsContent value="holidays">
          <HolidaysTab isLoading={holidays.isLoading} holidays={visibleHolidayDefinitions} />
        </TabsContent>
      </Tabs>

      <DraftHistoryDialog
        open={showDraftHistory}
        drafts={drafts.data}
        isLoading={drafts.isLoading}
        isFetching={drafts.isFetching}
        deletePending={deleteDraft.isPending}
        onClose={() => setShowDraftHistory(false)}
        onRefetch={() => drafts.refetch()}
        onOpenDraft={openDraftFromHistory}
        onDeleteDraft={deleteDraftFromHistory}
        onDeleteAll={deleteAllDraftsFromHistory}
      />

      <ConfirmDialog confirm={pendingConfirm} onClose={closeConfirm} />
    </div>
  );
}

