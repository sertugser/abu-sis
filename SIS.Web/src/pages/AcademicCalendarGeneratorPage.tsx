import { useMemo, useState } from 'react';
import { CheckCircle2, GitBranch, RefreshCw, RotateCcw, Save, Trash2, Wand2 } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Switch } from '@/components/ui/switch';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Textarea } from '@/components/ui/textarea';
import { cn } from '@/lib/utils';
import { CALENDAR_EVENT_LABELS, calendarEventBadgeVariant, calendarEventCategory } from '@/lib/calendarEventTypes';
import { displayTurkishText } from '@/lib/turkishText';
import {
  type CalendarConstraintInputDto,
  type CalendarDraftDto,
  type CalendarDraftSummaryDto,
  type CalendarWarningDto,
  type GenerateCalendarResultDto,
  type GeneratedCalendarEventDto,
  type HolidayDefinitionDto,
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

type CalendarView = GenerateCalendarResultDto | CalendarDraftDto;
type ConfirmTone = 'default' | 'danger' | 'info';
type WarningRow = {
  warning: CalendarWarningDto;
  message: string;
  detail: string | null;
  key: string;
};

type ShiftWarningRow = WarningRow & {
  titles: string[];
  semester: string;
  reason: string;
  shiftText: string;
  newDate: string;
};

type ConfirmOptions = {
  title: string;
  message: string;
  confirmLabel: string;
  cancelLabel?: string;
  showCancel?: boolean;
  tone?: ConfirmTone;
};

type PendingConfirm = ConfirmOptions & {
  resolve: (value: boolean) => void;
};

type CalendarProcessOption = {
  key: string;
  templateItemKey: string;
  eventType: string;
  eventTypeLabel: string;
  title: string;
  defaultDurationDays: number;
  semester: SemesterType;
  chainCode: string;
};

const SEMESTERS: { value: SemesterType; label: string }[] = [
  { value: 'Fall', label: 'Güz' },
  { value: 'Spring', label: 'Bahar' },
  { value: 'Summer', label: 'Yaz' },
];

function displayText(value?: string | null) {
  return displayTurkishText(value);
}

const CRITICAL_EVENT_TYPES = new Set([
  'ClassesStart',
  'ClassesEnd',
  'CourseRegistration',
  'AddDrop',
  'CourseWithdrawal',
  'RegistrationFreezeDeadline',
  'MidtermExam',
  'FinalExam',
  'MakeupExam',
  'ExcuseExam',
  'MidtermGradePublication',
  'FinalGradePublication',
  'MakeupGradePublication',
  'SummerSchoolRegistration',
  'SummerSchoolExam',
  'SummerSchoolGradePublication',
]);

function defaultAcademicYear() {
  const now = new Date();
  const start = now.getMonth() >= 6 ? now.getFullYear() : now.getFullYear() - 1;
  return `${start}-${start + 1}`;
}

function academicYearStart(academicYear: string) {
  const parsed = Number(academicYear.split('-')[0]);
  return Number.isFinite(parsed) ? parsed : new Date().getFullYear();
}

function dateInput(value?: string | null) {
  return value ? value.substring(0, 10) : '';
}

function utcDate(value: string | null) {
  return value ? `${value}T00:00:00.000Z` : null;
}

function fmt(value?: string | null) {
  if (!value) return '-';
  return new Date(value).toLocaleDateString('tr-TR', { day: '2-digit', month: 'short', year: 'numeric' });
}

function fmtLong(value?: string | null) {
  if (!value) return '-';
  return new Date(value).toLocaleDateString('tr-TR', { day: 'numeric', month: 'long', year: 'numeric' });
}

function fixedDayMonth(day?: number | null, month?: number | null) {
  if (!day || !month) return '-';
  const date = new Date(2026, month - 1, day);
  return date.toLocaleDateString('tr-TR', { day: 'numeric', month: 'long' });
}

function holidayDateText(holiday: HolidayDefinitionDto) {
  if (holiday.fixedDay && holiday.fixedMonth)
    return fixedDayMonth(holiday.fixedDay, holiday.fixedMonth);

  if (holiday.overrides.length > 0) {
    return holiday.overrides
      .map(override => dateInput(override.startDate) === dateInput(override.endDate)
        ? fmtLong(override.startDate)
        : `${fmtLong(override.startDate)} - ${fmtLong(override.endDate)}`)
      .join(', ');
  }

  return `Hicri ${holiday.hijriDay}.${holiday.hijriMonth}`;
}

function daySpan(ev: GeneratedCalendarEventDto) {
  const start = dateInput(ev.startDate);
  const end = dateInput(ev.endDate);
  return end && end !== start ? `${fmt(ev.startDate)} - ${fmt(ev.endDate)}` : fmt(ev.startDate);
}

function getDraftId(calendar: CalendarView | null, selectedDraftId: number | null) {
  if (!calendar) return selectedDraftId;
  return 'id' in calendar ? calendar.id : calendar.draftId;
}

function getErrorMessage(error: unknown) {
  const fallback = 'Islem tamamlanamadi.';
  if (!error || typeof error !== 'object') return fallback;
  const maybe = error as { response?: { data?: { message?: string; error?: string } }; message?: string };
  return maybe.response?.data?.message ?? maybe.response?.data?.error ?? maybe.message ?? fallback;
}

function qualityClass(score: number) {
  if (score >= 90) return 'bg-emerald-50 text-emerald-700 border-emerald-200';
  if (score >= 75) return 'bg-yellow-50 text-yellow-700 border-yellow-200';
  if (score >= 50) return 'bg-orange-50 text-orange-700 border-orange-200';
  return 'bg-red-50 text-red-700 border-red-200';
}

function isCriticalEvent(ev: GeneratedCalendarEventDto | null) {
  return !!ev && CRITICAL_EVENT_TYPES.has(ev.eventType);
}

const CATEGORY_LABELS: Record<string, string> = {
  exam: 'Sınav',
  registration: 'Kayıt',
  holiday: 'Tatil',
  deadline: 'Kritik Gün',
  result: 'İlan',
  application: 'Başvuru',
  academic: 'Akademik',
};

function eventCategoryLabel(eventType: string, isHoliday: boolean) {
  if (isHoliday) return 'Tatil';
  const category = calendarEventCategory(eventType);
  return CATEGORY_LABELS[category] ?? 'Akademik';
}

function semesterLabel(semester?: SemesterType | string | null) {
  return SEMESTERS.find(s => s.value === semester)?.label ?? semester ?? 'Genel';
}

function isLegacyShiftWarning(warning: CalendarWarningDto) {
  return isShiftWarning(warning)
    && !displayText(warning.message).toLocaleLowerCase('tr-TR').includes('yeni tarih:');
}

function isShiftWarning(warning: CalendarWarningDto) {
  return warning.code === 'HOLIDAY_SHIFT' || warning.code === 'PROHIBITION_SHIFT';
}

function parseShiftDays(message: string) {
  const match = displayText(message).match(/(\d+)\s*g[üu]n/i);
  if (!match) return null;
  const value = Number(match[1]);
  return Number.isFinite(value) && value > 0 ? value : null;
}

function parseDateOnly(value?: string | null) {
  const input = dateInput(value);
  if (!input) return null;
  const [year, month, day] = input.split('-').map(Number);
  if (!year || !month || !day) return null;
  return new Date(Date.UTC(year, month - 1, day));
}

function addDays(date: Date, days: number) {
  const next = new Date(date);
  next.setUTCDate(next.getUTCDate() + days);
  return next;
}

function rangesOverlap(aStart: Date, aEnd: Date, bStart: Date, bEnd: Date) {
  return aStart.getTime() <= bEnd.getTime() && bStart.getTime() <= aEnd.getTime();
}

function isWeekend(date: Date) {
  const day = date.getUTCDay();
  return day === 0 || day === 6;
}

function fmtDateOnly(date: Date) {
  return date.toLocaleDateString('tr-TR', { day: '2-digit', month: 'short', year: 'numeric', timeZone: 'UTC' });
}

function holidayRangeText(start: Date, end: Date) {
  const startText = fmtDateOnly(start);
  const endText = fmtDateOnly(end);
  return startText === endText ? startText : `${startText} - ${endText}`;
}

function reconstructLegacyHolidayReasons(current: CalendarView, event: GeneratedCalendarEventDto, shiftDays: number) {
  const finalStart = parseDateOnly(event.startDate);
  const finalEnd = parseDateOnly(event.endDate ?? event.startDate);
  if (!finalStart || !finalEnd) return [];

  const originalStart = addDays(finalStart, -shiftDays);
  const originalEnd = addDays(finalEnd, -shiftDays);
  const reasons: string[] = [];

  for (let offset = 0; offset < shiftDays; offset++) {
    const start = addDays(originalStart, offset);
    const end = addDays(originalEnd, offset);

    current.holidays
      .filter(holiday => holiday.blocksAcademicEvents)
      .forEach(holiday => {
        const holidayStart = parseDateOnly(holiday.startDate);
        const holidayEnd = parseDateOnly(holiday.endDate);
        if (holidayStart && holidayEnd && rangesOverlap(start, end, holidayStart, holidayEnd)) {
          reasons.push(`${displayText(holiday.name)} (${holidayRangeText(holidayStart, holidayEnd)})`);
        }
      });

    if (isWeekend(start))
      reasons.push(`hafta sonu (${fmtDateOnly(start)})`);
  }

  return [...new Set(reasons)];
}

function extractShiftReason(message: string, title: string) {
  const text = displayText(message).replace(displayText(title), '').trim();
  const [reason] = text.split(/\s+nedeniyle\s+/i);
  return reason?.trim() || 'takvim çakışması';
}

function joinedTitles(titles: string[]) {
  if (titles.length <= 1) return titles[0] ?? 'Etkinlik';
  if (titles.length === 2) return `${titles[0]} ve ${titles[1]}`;
  return `${titles.slice(0, -1).join(', ')} ve ${titles[titles.length - 1]}`;
}

function readableShiftReason(reason: string) {
  const cleanReason = displayText(reason).trim();
  const weekendDates = [...cleanReason.matchAll(/hafta sonu \(([^)]+)\)/gi)]
    .map(match => match[1])
    .filter(Boolean);
  const otherReason = cleanReason
    .replace(/hafta sonu \([^)]+\)(,\s*)?/gi, '')
    .replace(/,\s*$/, '')
    .trim();
  const parts: string[] = [];

  if (weekendDates.length > 0) {
    parts.push(
      `${joinedTitles(weekendDates)} ${weekendDates.length === 1 ? 'tarihinin' : 'tarihlerinin'} hafta sonuna denk gelmesi`
    );
  }

  if (otherReason) {
    parts.push(
      otherReason === 'yasak aralık'
        ? 'tanımlı yasak aralıkla çakışması'
        : `${otherReason} ile çakışması`
    );
  }

  return parts.length > 0 ? joinedTitles(parts) : cleanReason;
}

function shiftWarningMessage(row: ShiftWarningRow) {
  const subject = row.titles.length === 1 ? row.titles[0] : `${row.titles.length} etkinlik`;
  return `${row.semester} döneminde ${subject}, ${readableShiftReason(row.reason)} nedeniyle ${row.newDate} tarihine kaydırıldı.`;
}

function buildShiftWarningRow(current: CalendarView, warning: CalendarWarningDto, event: GeneratedCalendarEventDto): ShiftWarningRow | null {
  const shiftDays = parseShiftDays(warning.message);
  if (isLegacyShiftWarning(warning) && !shiftDays)
    return null;

  const title = displayText(event.title);
  const reason = warning.code === 'HOLIDAY_SHIFT' && shiftDays
    ? reconstructLegacyHolidayReasons(current, event, shiftDays).join(', ') || 'takvim çakışması'
    : extractShiftReason(warning.message, title);
  const shiftText = shiftDays ? `${shiftDays} gün kaydırıldı` : 'kaydırıldı';
  const semester = semesterLabel(event.semester);
  const newDate = daySpan(event);
  const key = `${warning.severity}::${warning.code}::${event.semester}::${reason}::${shiftText}::${newDate}`;
  const row: ShiftWarningRow = {
    warning,
    message: '',
    detail: `Etkilenen etkinlikler: ${title}`,
    key,
    titles: [title],
    semester,
    reason,
    shiftText,
    newDate,
  };
  row.message = shiftWarningMessage(row);
  return row;
}

function legacyShiftMessage(current: CalendarView, warning: CalendarWarningDto, event: GeneratedCalendarEventDto) {
  const shiftDays = parseShiftDays(warning.message);
  const reason = warning.code === 'HOLIDAY_SHIFT' && shiftDays
    ? reconstructLegacyHolidayReasons(current, event, shiftDays).join(', ') || 'takvim çakışması'
    : 'yasak aralık';
  const shiftText = shiftDays ? `${shiftDays} gün kaydırıldı` : 'kaydırıldı';
  return `${semesterLabel(event.semester)}: ${displayText(event.title)} ${reason} nedeniyle ${shiftText}. Yeni tarih: ${daySpan(event)}.`;
}

function warningRows(current: CalendarView) {
  const seen = new Set<string>();
  const shiftGroups = new Map<string, ShiftWarningRow>();
  return current.warnings.reduce<WarningRow[]>((rows, warning) => {
    const event = warning.draftEventId
      ? current.events.find(item => item.draftEventId === warning.draftEventId) ?? null
      : null;

    if (isShiftWarning(warning) && event) {
      const nextRow = buildShiftWarningRow(current, warning, event);
      if (!nextRow)
        return rows;

      const existing = shiftGroups.get(nextRow.key);
      if (existing) {
        for (const title of nextRow.titles) {
          if (!existing.titles.includes(title))
            existing.titles.push(title);
        }
        existing.message = shiftWarningMessage(existing);
        existing.detail = `Etkilenen etkinlikler: ${joinedTitles(existing.titles)}`;
        return rows;
      }

      shiftGroups.set(nextRow.key, nextRow);
      rows.push(nextRow);
      return rows;
    }

    const message = isLegacyShiftWarning(warning)
      ? event ? legacyShiftMessage(current, warning, event) : null
      : displayText(warning.message);

    if (!message)
      return rows;

    const detail = event ? `Etkinlik: ${semesterLabel(event.semester)} · ${daySpan(event)}` : null;
    const key = `${warning.severity}::${warning.code}::${message}`;
    if (seen.has(key)) return rows;
    seen.add(key);
    rows.push({ warning, message, detail, key });
    return rows;
  }, []);
}

function processOptionSemesterLabel(option: CalendarProcessOption) {
  return SEMESTERS.find(s => s.value === option.semester)?.label ?? option.semester;
}

function processOptionLabel(option: CalendarProcessOption) {
  return `${displayText(option.title)} ${processOptionSemesterLabel(option)}`;
}

function normalizeSearch(value: string) {
  return value.trim().toLocaleLowerCase('tr-TR');
}

function filterProcessOptions(options: CalendarProcessOption[], query: string) {
  const normalized = normalizeSearch(query);
  if (!normalized) return options;

  return options.filter(option => [
    displayText(option.title),
    displayText(option.eventTypeLabel),
    option.chainCode,
    processOptionSemesterLabel(option),
  ].some(value => normalizeSearch(value).includes(normalized)));
}

function draftStatusBadgeVariant(status: string) {
  if (status === 'Draft') return 'secondary';
  if (status === 'Applied') return 'success';
  if (status === 'RolledBack') return 'warning';
  return 'outline';
}

function SearchableProcessSelect({
  options,
  selectedKey,
  onChange,
  disabled,
  placeholder = 'Etkinlik seçin',
}: {
  options: CalendarProcessOption[];
  selectedKey: string;
  onChange: (key: string) => void;
  disabled?: boolean;
  placeholder?: string;
}) {
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState('');
  const selected = options.find(option => option.key === selectedKey) ?? null;
  const filtered = useMemo(() => filterProcessOptions(options, query), [options, query]);

  function selectOption(key: string) {
    onChange(key);
    setOpen(false);
    setQuery('');
  }

  return (
    <div
      className="relative"
      onBlur={event => {
        if (!event.currentTarget.contains(event.relatedTarget as Node | null)) {
          setOpen(false);
        }
      }}
    >
      <button
        type="button"
        disabled={disabled}
        onClick={() => setOpen(prev => !prev)}
        className="flex h-10 w-full items-center justify-between gap-2 rounded-lg border border-slate-200 bg-white px-3 text-left text-sm text-slate-800 disabled:cursor-not-allowed disabled:opacity-60"
      >
        <span className="min-w-0 truncate">{selected ? processOptionLabel(selected) : placeholder}</span>
        <span className="shrink-0 text-slate-500">v</span>
      </button>

      {open && (
        <div className="absolute left-0 right-0 top-11 z-30 rounded-lg border border-slate-200 bg-white p-2 shadow-lg">
          <Input
            autoFocus
            value={query}
            onChange={event => setQuery(event.target.value)}
            placeholder="Ara..."
            className="h-9"
          />
          <div className="mt-2 max-h-64 overflow-auto">
            {filtered.length === 0 ? (
              <div className="px-3 py-2 text-sm text-slate-500">Sonuç bulunamadı.</div>
            ) : filtered.map(option => (
              <button
                key={option.key}
                type="button"
                onMouseDown={event => event.preventDefault()}
                onClick={() => selectOption(option.key)}
                className={cn(
                  'block w-full rounded-md px-3 py-2 text-left text-sm hover:bg-cyan-50',
                  selectedKey === option.key ? 'bg-cyan-50 font-semibold text-cyan-900' : 'text-slate-700',
                )}
              >
                <span className="block truncate">{processOptionLabel(option)}</span>
              </button>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}

export default function AcademicCalendarGeneratorPage() {
  const [activeTab, setActiveTab] = useState('generator');
  const [academicYear, setAcademicYear] = useState(defaultAcademicYear);
  const [includeSummer, setIncludeSummer] = useState(true);
  const [forceOverwrite, setForceOverwrite] = useState(false);
  const [durationOverrides, setDurationOverrides] = useState<Record<string, number>>({});
  const [durationEventKey, setDurationEventKey] = useState('');
  const [durationDays, setDurationDays] = useState('');
  const [pinEventKey, setPinEventKey] = useState('');
  const [constraints, setConstraints] = useState<CalendarConstraintInputDto[]>([]);
  const [pinDate, setPinDate] = useState('');
  const [pinEndDate, setPinEndDate] = useState('');
  const [selectedSemester, setSelectedSemester] = useState<SemesterType>('Fall');
  const [selectedDraftId, setSelectedDraftId] = useState<number | null>(null);
  const [generated, setGenerated] = useState<GenerateCalendarResultDto | null>(null);
  const [selectedEventId, setSelectedEventId] = useState<string | null>(null);
  const [editForm, setEditForm] = useState({ title: '', startDate: '', endDate: '', note: '' });
  const [notice, setNotice] = useState<string | null>(null);
  const [pendingConfirm, setPendingConfirm] = useState<PendingConfirm | null>(null);
  const [showDraftHistory, setShowDraftHistory] = useState(false);
  const defaultStartDates = useMemo(() => {
    const parsedStart = academicYearStart(academicYear);
    return {
      fallStartDate: `${parsedStart}-09-22`,
      springStartDate: `${parsedStart + 1}-02-23`,
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

  const calendar = (draft.data ?? generated) as CalendarView | null;
  const activeDraftId = getDraftId(calendar, selectedDraftId);
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

  const semesterEvents = useMemo(
    () => (calendar?.events ?? []).filter(e => e.semester === selectedSemester).sort((a, b) => a.startDate.localeCompare(b.startDate)),
    [calendar, selectedSemester],
  );

  const selectedEvent = useMemo(
    () => semesterEvents.find(e => e.draftEventId === selectedEventId) ?? null,
    [semesterEvents, selectedEventId],
  );

  const whatIfDiffs = useMemo(() => {
    if (!whatIf.data || !calendar) return [];
    const current = new Map(calendar.events.map(e => [e.draftEventId, e]));
    return whatIf.data.events
      .map(next => ({ next, prev: current.get(next.draftEventId) }))
      .filter(({ next, prev }) => prev && (dateInput(prev.startDate) !== dateInput(next.startDate) || dateInput(prev.endDate) !== dateInput(next.endDate)))
      .slice(0, 12);
  }, [calendar, whatIf.data]);

  function eventDatesChanged(ev: GeneratedCalendarEventDto | null) {
    if (!ev) return false;
    const oldStart = dateInput(ev.startDate);
    const oldEnd = dateInput(ev.endDate) || oldStart;
    return oldStart !== editForm.startDate || oldEnd !== (editForm.endDate || editForm.startDate);
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

  async function askDominoPreference(ev: GeneratedCalendarEventDto, actionLabel: string) {
    if (!eventDatesChanged(ev))
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
      title: 'Domino uygulansın mı?',
      message:
        `${actionLabel}: ${displayText(ev.title)} tarihi ${daySpan(ev)} -> ${fmt(utcDate(editForm.startDate))}${editForm.endDate && editForm.endDate !== editForm.startDate ? ` - ${fmt(utcDate(editForm.endDate))}` : ''} olacak.\n\n` +
        'Bağımlı sonraki olaylar da aynı fark kadar kaydırılsın mı?',
      confirmLabel: 'Evet, domino uygula',
      cancelLabel: 'Hayır, sadece bu etkinlik',
      tone: 'info',
    });
  }

  function selectEvent(ev: GeneratedCalendarEventDto) {
    setSelectedEventId(ev.draftEventId);
    setEditForm({ title: displayText(ev.title), startDate: dateInput(ev.startDate), endDate: dateInput(ev.endDate), note: ev.note ?? '' });
    whatIf.reset();
  }

  async function addPinConstraint() {
    if (!pinDate || !selectedPinOption) return;
    const label = processOptionLabel(selectedPinOption);
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
        endDate: utcDate(pinEndDate || pinDate),
        templateItemKey: selectedPinOption.templateItemKey,
        semester: selectedPinOption.semester,
      }),
      isHard: true,
      priority: 1,
      description: `${label} sabitlendi`,
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
        setGenerated(data);
        setSelectedDraftId(data.draftId);
        setSelectedSemester('Fall');
        setSelectedEventId(null);
        setActiveTab('draft');
        setNotice(`Taslak #${data.draftId} oluşturuldu. Bu aşamada akademik takvim sayfasına yazılmaz; aktarmak için "Onayla ve kaydet" kullanın.`);
      },
      onError: error => setNotice(getErrorMessage(error)),
    });
  }

  async function handleWhatIf() {
    if (!activeDraftId || !selectedEvent) return;
    const propagateChanges = await askDominoPreference(selectedEvent, 'Domino ön izleme');
    whatIf.mutate({
      draftId: activeDraftId,
      dto: {
        draftEventId: selectedEvent.draftEventId,
        startDate: utcDate(editForm.startDate) ?? selectedEvent.startDate,
        endDate: utcDate(editForm.endDate || editForm.startDate),
        propagateChanges,
      },
    }, {
      onSuccess: data => setNotice(`What-if hesaplandı. ${propagateChanges ? 'Domino dahil edildi.' : 'Sadece seçili etkinlik denendi.'} Kalite skoru: ${data.qualityScore}.`),
      onError: error => setNotice(getErrorMessage(error)),
    });
  }

  async function handleUpdateEvent() {
    if (!activeDraftId || !selectedEvent) return;
    const propagateChanges = await askDominoPreference(selectedEvent, 'Taslağa yaz');
    const shouldUpdate = await askConfirm({
      title: 'Taslağa yazılsın mı?',
      message:
        `${displayText(selectedEvent.title)} taslakta güncellenecek.\n\n` +
        `Yeni tarih: ${editForm.startDate}${editForm.endDate && editForm.endDate !== editForm.startDate ? ` - ${editForm.endDate}` : ''}\n` +
        `Domino: ${propagateChanges ? 'uygulanacak' : 'uygulanmayacak'}\n\n` +
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
        setGenerated(null);
        setSelectedDraftId(data.id);
        setSelectedEventId(null);
        setNotice(propagateChanges
          ? 'Taslak etkinliği güncellendi; bağımlı tarihler yeniden hesaplandı.'
          : 'Taslak etkinliği güncellendi; domino uygulanmadı, sadece seçili etkinlik değişti.');
      },
      onError: error => setNotice(getErrorMessage(error)),
    });
  }

  async function handleApply() {
    if (!activeDraftId) return;
    const shouldApply = await askConfirm({
      title: 'Akademik takvime kaydedilsin mi?',
      message:
        `Taslak #${activeDraftId} gerçek akademik takvime uygulanacak.\n\n` +
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
    applyDraft.mutate({ draftId: activeDraftId, forceOverwrite }, {
      onSuccess: result => {
        const terms = result.termIds.length > 0 ? result.termIds.join(', ') : 'yok';
        setNotice(`Applied. ${result.message} Güz, Bahar ve varsa Yaz dönemleri Dönem İşleri sayfasına aktarıldı. Güncellenen dönemler: ${terms}.`);
      },
      onError: error => setNotice(getErrorMessage(error)),
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
      <div className="grid gap-4 xl:grid-cols-[1.2fr_0.8fr]">
        <div className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm">
          <div className="grid gap-4 md:grid-cols-4">
            <label className="space-y-1 text-sm font-medium text-slate-700">
              Akademik yıl
              <Input value={academicYear} onChange={e => setAcademicYear(e.target.value)} placeholder="2025-2026" />
            </label>
            <label className="space-y-1 text-sm font-medium text-slate-700">
              Güz ders başlangıcı
              <Input type="date" value={fallStartDate} onChange={e => setFallStartDate(e.target.value)} />
            </label>
            <label className="space-y-1 text-sm font-medium text-slate-700">
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

          <div className="mt-5">
            <div className="rounded-lg border border-slate-200 p-3">
              <div className="mb-3 flex items-center gap-2 font-semibold text-slate-800">
                <GitBranch className="h-4 w-4" />
                Tarih Sabitleme
              </div>
              <p className="mt-1 text-xs text-slate-500">
                Belirli bir etkinliğin tarihini sabitlemek için seçin ve başlangıç / bitiş tarihini girin.
              </p>
              <div className="mt-3 grid gap-3 md:grid-cols-[minmax(0,1.5fr)_140px_140px]">
                <SearchableProcessSelect
                  options={durationOptions}
                  selectedKey={activePinEventKey}
                  onChange={handlePinEventChange}
                  disabled={templates.isLoading || durationOptions.length === 0}
                  placeholder={templates.isLoading ? 'Yükleniyor...' : 'Etkinlik seçin'}
                />
                <Input type="date" value={pinDate} onChange={e => setPinDate(e.target.value)} />
                <Input type="date" value={pinEndDate} onChange={e => setPinEndDate(e.target.value)} />
              </div>
              {selectedPinOption && (
                <p className="mt-2 text-sm font-medium text-slate-700">
                  Seçilen süreç: {processOptionLabel(selectedPinOption)}. Bu sabitleme yalnızca seçilen dönem için uygulanır.
                </p>
              )}
              <Button type="button" size="sm" className="mt-3" onClick={addPinConstraint} disabled={!activePinEventKey || !selectedPinOption}>
                Kısıt Ekle
              </Button>
            </div>
          </div>

          <div className="mt-4 rounded-lg border border-slate-200 p-3">
            <div className="font-semibold text-slate-800">Süre Ayarları</div>
            <p className="mt-1 text-xs text-slate-500">
              Bir etkinliğin varsayılan süresini değiştirmek için seçin ve yeni gün sayısını girin.
            </p>

            <div className="mt-3 grid gap-3 md:grid-cols-[minmax(0,1.5fr)_140px_auto]">
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
              <p className="mt-2 text-sm font-medium text-slate-700">
                Seçilen süreç: {processOptionLabel(selectedDurationOption)}. Varsayılan: {selectedDurationOption.defaultDurationDays} gün.
              </p>
            )}


            <div className="mt-3 space-y-2">
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

          <div className="mt-4 flex flex-wrap items-center gap-3">
            <Button onClick={handleGenerate} disabled={generate.isPending}>
              {generate.isPending ? <RefreshCw className="h-4 w-4 animate-spin" /> : <Wand2 className="h-4 w-4" />}
              Taslak Üret
            </Button>
            <Button type="button" variant="outline" onClick={() => {
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
            <span className="text-xs text-slate-500">Taslaklar 30 gün saklanır. Onaylama yalnızca takvim oluşturucu kaynaklı etkinlikleri günceller.</span>
          </div>
        </div>

        <div className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm">
          <h2 className="font-semibold text-slate-900">Aktif Kısıtlar</h2>
          <div className="mt-3 space-y-2">
            {constraints.length === 0 ? (
              <p className="text-sm text-slate-500">Henüz ek kısıt yok.</p>
            ) : constraints.map((constraint, index) => (
              <div key={`${constraint.constraintType}-${index}`} className="flex items-center justify-between gap-3 rounded-lg border border-slate-200 px-3 py-2">
                <div>
                  <Badge variant={constraint.isHard ? 'destructive' : 'secondary'}>{constraint.constraintType}</Badge>
                  <p className="mt-1 text-sm text-slate-700">{constraint.description}</p>
                </div>
                <Button type="button" size="sm" variant="ghost" onClick={() => setConstraints(prev => prev.filter((_, i) => i !== index))}>
                  Sil
                </Button>
              </div>
            ))}
          </div>

          <div className="mt-5 border-t border-cyan-200 pt-4 text-sm text-cyan-900">
            <div className="font-semibold">Yönetmelik Kontrolü</div>
            <div className="mt-2 space-y-1 text-xs">
              <div>Normal dönem: 14 hafta ders, sınavlarla 16 hafta.</div>
              <div>Yaz okulu: sınavlar hariç 7 hafta.</div>
              <div>Çekilme ve kayıt dondurma: yedinci hafta sonuna kadar.</div>
              <div>Sınav sonuçları: en geç 10 iş günü.</div>
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
      <div className="space-y-4">
        {renderWarningsPanel(calendar)}

        <div className="grid gap-4 lg:grid-cols-3">
          <div className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm lg:col-span-2">
            <div className="flex flex-wrap items-center justify-between gap-3">
              <div>
                <h2 className="font-semibold text-slate-900">Taslak #{activeDraftId}</h2>
                <p className="text-sm text-slate-500">{calendar.academicYear} için {calendar.events.length} etkinlik üretildi.</p>
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

            <div className="mt-4 overflow-hidden rounded-lg border border-slate-200">
              <div className="grid grid-cols-[minmax(160px,1.5fr)_130px_120px_100px] bg-slate-50 px-3 py-2 text-xs font-semibold uppercase text-slate-500">
                <span>Etkinlik</span>
                <span>Tarih</span>
                <span>Zincir</span>
                <span>Tür</span>
              </div>
              <div className="max-h-[560px] overflow-auto">
                {semesterEvents.map(ev => (
                  <button
                    key={ev.draftEventId}
                    type="button"
                    onClick={() => selectEvent(ev)}
                    className={cn(
                      'grid w-full grid-cols-[minmax(160px,1.5fr)_130px_120px_100px] items-center gap-2 border-t border-slate-100 px-3 py-2 text-left text-sm hover:bg-slate-50',
                      selectedEventId === ev.draftEventId && 'bg-cyan-50',
                    )}
                  >
                    <span className="min-w-0">
                      <span className="block truncate font-medium text-slate-800">{displayText(ev.title)}</span>
                      <span className="text-xs text-slate-500">{displayText(ev.eventTypeLabel)}</span>
                    </span>
                    <span className="text-xs text-slate-600">{daySpan(ev)}</span>
                    <span className="text-xs text-slate-500">{ev.chainCode}</span>
                    <span className="flex flex-wrap gap-1">
                      <Badge variant={calendarEventBadgeVariant(ev.eventType)}>
                        {eventCategoryLabel(ev.eventType, ev.isHoliday)}
                      </Badge>
                      {ev.isFailed && <Badge variant="destructive">Hata</Badge>}
                    </span>
                  </button>
                ))}
              </div>
            </div>
          </div>

          <div className="space-y-4">
            <div className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm">
              <h2 className="font-semibold text-slate-900">Tarih Düzenleme</h2>
              {!selectedEvent ? (
                <p className="mt-2 text-sm text-slate-500">Bir etkinlik seçin, tarihi değiştirin ve domino etkisini hesaplayın.</p>
              ) : (
                <div className="mt-3 space-y-3">
                  <label className="block space-y-1 text-sm font-medium text-slate-700">
                    Başlık
                    <Input value={editForm.title} onChange={e => setEditForm(prev => ({ ...prev, title: e.target.value }))} />
                  </label>
                  <div className="grid grid-cols-2 gap-3">
                    <label className="block space-y-1 text-sm font-medium text-slate-700">
                      Başlangıç
                      <Input type="date" value={editForm.startDate} onChange={e => setEditForm(prev => ({ ...prev, startDate: e.target.value }))} />
                    </label>
                    <label className="block space-y-1 text-sm font-medium text-slate-700">
                      Bitiş
                      <Input type="date" value={editForm.endDate} onChange={e => setEditForm(prev => ({ ...prev, endDate: e.target.value }))} />
                    </label>
                  </div>
                  <label className="block space-y-1 text-sm font-medium text-slate-700">
                    Not
                    <Textarea value={editForm.note} onChange={e => setEditForm(prev => ({ ...prev, note: e.target.value }))} />
                  </label>
                  <div className="flex flex-wrap gap-2">
                    <Button type="button" size="sm" variant="outline" onClick={handleWhatIf} disabled={whatIf.isPending}>
                      <GitBranch className="h-4 w-4" />
                      Domino Önizle
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
        <div className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm">
          <h2 className="font-semibold text-slate-900">Domino Farkı</h2>
          {whatIfDiffs.length === 0 ? (
            <p className="mt-2 text-sm text-slate-500">Hesaplanmış fark yok.</p>
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
              <div className={cn('rounded-lg border px-3 py-2 text-sm font-semibold', qualityClass(whatIf.data?.qualityScore ?? current.qualityScore))}>
                Tahmini kalite skoru: {whatIf.data?.qualityScore}
              </div>
            </div>
          )}
        </div>

        <div className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm">
          <div className="flex items-center justify-between gap-3">
            <div>
              <h2 className="font-semibold text-slate-900">Onayla ve kaydet</h2>
              <p className="mt-1 max-w-md text-sm leading-6 text-slate-600">
                Onayladığınızda ilgili Güz, Bahar ve Yaz dönemlerindeki eski takvim oluşturucu kayıtları temizlenir,
                bu taslaktaki etkinlikler gerçek akademik takvime yazılır. Dönem durumu Published veya daha ileriyse
                sistem normalde üzerine yazmaz; bu anahtar açıkken yayınlanmış takvimi bilinçli olarak yenilersiniz.
                Closed ve Archived dönemler yine korunur.
              </p>
            </div>
            <Switch checked={forceOverwrite} onCheckedChange={setForceOverwrite} />
          </div>
          <div className="mt-3 flex flex-wrap gap-2">
            <Button type="button" onClick={handleApply} disabled={!activeDraftId || applyDraft.isPending}>
              {applyDraft.isPending ? <RefreshCw className="h-4 w-4 animate-spin" /> : <CheckCircle2 className="h-4 w-4" />}
              {applyDraft.isPending ? 'Kaydediliyor' : 'Onayla'}
            </Button>
            <Button type="button" variant="outline" onClick={handleRollback} disabled={!activeDraftId || rollbackDraft.isPending}>
              <RotateCcw className="h-4 w-4" />
              Geri al
            </Button>
          </div>
        </div>

        {renderTermSummaryPanel(current)}
      </>
    );
  }

  function renderWarningsPanel(current: CalendarView) {
    const rows = warningRows(current);
    return (
      <div className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm">
        <h2 className="font-semibold text-slate-900">Uyarılar</h2>
        <div className="mt-3 space-y-2">
          {rows.length === 0 ? (
            <p className="text-sm text-slate-500">Uyarı yok.</p>
          ) : rows.map(row => (
            <div key={row.key} className="rounded-lg border border-slate-200 px-3 py-2 text-sm">
              <Badge variant={row.warning.severity === 'Error' ? 'destructive' : row.warning.severity === 'Warning' ? 'warning' : 'secondary'}>
                {row.warning.severity}
              </Badge>
              <span className="ml-2 text-slate-700">{row.message}</span>
              {row.detail && (
                <div className="mt-1 text-xs text-slate-500">{row.detail}</div>
              )}
            </div>
          ))}
        </div>
      </div>
    );
  }

  function renderTermSummaryPanel(current: CalendarView) {
    return (
      <div className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm">
        <h2 className="font-semibold text-slate-900">Dönem özeti</h2>
        <div className="mt-3 grid gap-2">
          {current.termSummaries.map(summary => (
            <div key={summary.semester} className="rounded-lg border border-slate-200 px-3 py-2 text-sm">
              <div className="font-semibold text-slate-800">{SEMESTERS.find(s => s.value === summary.semester)?.label}</div>
              <div className="mt-1 text-xs text-slate-500">{fmt(summary.termStart)} - {fmt(summary.termEnd)}</div>
              <div className="mt-2 text-xs text-slate-600">
                {summary.teachingDays} ders günü, {summary.holidayDaysInTerm} tatil günü, {summary.examDays} sınav günü
              </div>
            </div>
          ))}
        </div>
      </div>
    );
  }

  function renderHolidaysTab() {
    return (
      <div className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm">
        <h2 className="font-semibold text-slate-900">Tatil tanımları</h2>
        <p className="mt-1 text-sm text-slate-600">Sabit resmi tatiller, dini bayramlar ve bloklayıcı önemli günler.</p>
        <div className="mt-4 grid gap-3 md:grid-cols-2 xl:grid-cols-3">
          {holidays.isLoading ? (
            <p className="text-sm text-slate-500">Yükleniyor...</p>
          ) : holidays.data?.map(holiday => (
            <div key={holiday.id} className="rounded-lg border border-slate-200 px-3 py-3 text-sm">
              <div className="flex items-center justify-between gap-2">
                <span className="font-semibold text-slate-800">{displayText(holiday.name)}</span>
                {holiday.type === 'HijriCalculated' && (
                  <Badge variant="info">Hicri hesaplandı</Badge>
                )}
              </div>
              <div className="mt-2 font-medium text-slate-700">
                {holidayDateText(holiday)}
              </div>
              <div className="mt-1 text-xs text-slate-600">
                Süre: {holiday.durationDays} gün
              </div>
              {holiday.overrides.length > 0 && (
                <div className="mt-1 text-xs text-slate-500">
                  Kaynak tarih: {holiday.overrides.map(o => `${o.year}: ${fmtLong(o.startDate)}${dateInput(o.startDate) === dateInput(o.endDate) ? '' : ` - ${fmtLong(o.endDate)}`}`).join(', ')}
                </div>
              )}
            </div>
          ))}
        </div>
      </div>
    );
  }

  function renderDraftHistoryDialog() {
    if (!showDraftHistory) return null;

    return (
      <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/50 px-4 py-6">
        <div className="w-full max-w-4xl rounded-lg border border-slate-200 bg-white p-5 shadow-xl">
          <div className="flex flex-wrap items-start justify-between gap-3">
            <div>
              <h2 className="text-lg font-semibold text-slate-900">Taslak geçmişi</h2>
              <p className="mt-1 text-sm text-slate-600">
                Önceden üretilen taslakları açabilir veya artık gerek olmayanları silebilirsin.
              </p>
            </div>
            <div className="flex gap-2">
              <Button type="button" size="sm" variant="outline" onClick={() => drafts.refetch()} disabled={drafts.isFetching}>
                Yenile
              </Button>
              <Button
                type="button"
                size="sm"
                variant="destructive"
                onClick={deleteAllDraftsFromHistory}
                disabled={deleteDraft.isPending || drafts.isLoading || (drafts.data?.length ?? 0) === 0}
              >
                Tümünü Sil
              </Button>
              <Button type="button" size="sm" variant="outline" onClick={() => setShowDraftHistory(false)}>
                Kapat
              </Button>
            </div>
          </div>

          <div className="mt-4 max-h-[70vh] overflow-auto">
            {drafts.isLoading ? (
              <p className="rounded-lg border border-slate-200 px-3 py-4 text-sm text-slate-500">Taslaklar yükleniyor...</p>
            ) : (drafts.data?.length ?? 0) === 0 ? (
              <p className="rounded-lg border border-slate-200 px-3 py-4 text-sm text-slate-500">Henüz taslak yok.</p>
            ) : (
              <div className="space-y-2">
                {drafts.data?.map(summary => (
                  <div key={summary.id} className="grid gap-3 rounded-lg border border-slate-200 px-3 py-3 text-sm md:grid-cols-[1fr_auto] md:items-center">
                    <div>
                      <div className="flex flex-wrap items-center gap-2">
                        <span className="font-semibold text-slate-900">Taslak #{summary.id}</span>
                        <Badge variant={draftStatusBadgeVariant(summary.status)}>{summary.status}</Badge>
                        <span className={cn('rounded-md border px-2 py-0.5 text-xs font-semibold', qualityClass(summary.qualityScore))}>
                          Kalite {summary.qualityScore}
                        </span>
                      </div>
                      <div className="mt-2 text-slate-700">
                        {summary.academicYear} · {summary.eventCount} etkinlik · {summary.warningCount} uyarı
                      </div>
                      <div className="mt-1 text-xs text-slate-500">
                        Güncelleme: {fmt(summary.updatedAt)} · Saklama bitişi: {fmt(summary.expiresAt)}
                      </div>
                    </div>
                    <div className="flex flex-wrap gap-2 md:justify-end">
                      <Button type="button" size="sm" variant="outline" onClick={() => openDraftFromHistory(summary)}>
                        Aç
                      </Button>
                      <Button type="button" size="sm" variant="destructive" onClick={() => deleteDraftFromHistory(summary)} disabled={deleteDraft.isPending}>
                        Sil
                      </Button>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-3">
      <Tabs value={activeTab} onValueChange={setActiveTab} className="space-y-3">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <TabsList className="bg-white">
            <TabsTrigger value="generator">Kısıtlar & Üret</TabsTrigger>
            <TabsTrigger value="draft">Taslak & Domino</TabsTrigger>
            <TabsTrigger value="holidays">Tatil Tanımları</TabsTrigger>
          </TabsList>
          <div className="flex flex-wrap items-center gap-2">
            <Button type="button" variant="outline" size="sm" onClick={() => setShowDraftHistory(true)}>
              Taslak geçmişi
            </Button>
            <div className={cn('rounded-lg border px-3 py-2 text-sm font-semibold', qualityClass(calendar?.qualityScore ?? 0))}>
              Kalite: {calendar?.qualityScore ?? '-'}
            </div>
          </div>
        </div>

        {notice && (
          <div className="rounded-lg border border-slate-200 bg-white px-4 py-3 text-sm text-slate-700 shadow-sm">
            {notice}
          </div>
        )}

        <TabsContent value="generator">{renderGeneratorTab()}</TabsContent>
        <TabsContent value="draft">{renderDraftTab()}</TabsContent>
        <TabsContent value="holidays">{renderHolidaysTab()}</TabsContent>
      </Tabs>

      {renderDraftHistoryDialog()}

      {pendingConfirm && (
        <div className="fixed inset-0 z-[60] flex items-center justify-center bg-slate-950/50 px-4 py-6">
          <div
            className="w-full max-w-lg rounded-lg border border-slate-200 bg-white p-5 shadow-xl"
            role="dialog"
            aria-modal="true"
            aria-labelledby="calendar-confirm-title"
          >
            <h2 id="calendar-confirm-title" className="text-lg font-semibold text-slate-900">
              {pendingConfirm.title}
            </h2>
            <p className="mt-3 whitespace-pre-line text-sm leading-6 text-slate-600">
              {pendingConfirm.message}
            </p>
            <div className="mt-5 flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
              {pendingConfirm.showCancel !== false && (
                <Button type="button" variant="outline" onClick={() => closeConfirm(false)}>
                  {pendingConfirm.cancelLabel ?? 'Vazgeç'}
                </Button>
              )}
              <Button
                type="button"
                variant={pendingConfirm.tone === 'danger' ? 'destructive' : 'default'}
                className={pendingConfirm.tone === 'info' ? 'bg-cyan-700 hover:bg-cyan-800' : undefined}
                onClick={() => closeConfirm(true)}
              >
                {pendingConfirm.confirmLabel}
              </Button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
