import { useCallback, useEffect, useMemo, useState } from 'react';
import type { ReactNode } from 'react';
import { AlertTriangle, CalendarDays, CheckCircle2, FileDown, LogOut, RefreshCw, Repeat2, Save, Undo2, UserPlus, X } from 'lucide-react';
import { CalendarVisualizer, type DropFeedback, type DropFeedbackRequest, type VisualizerEvent } from '@/components/CalendarVisualizer';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { downloadAcademicCalendarPdf, type AcademicCalendarPdfSection } from '@/lib/academicCalendarPdf';
import { resolveCalendarEventType } from '@/lib/calendarEventTypes';
import { cn } from '@/lib/utils';
import { displayTurkishText } from '@/lib/turkishText';
import { usePermissions } from '@/hooks/usePermissions';
import {
  useAcademicCalendarTerms,
  useAcademicYearCalendars,
  usePreviewAcademicCalendarEventMove,
  useSaveAcademicCalendarChanges,
  type AcademicCalendarTermSummaryDto,
  type StudentAcademicCalendarDto,
  type TermCalendarEventDto,
} from '@/hooks/useAcademicCalendar';

const NON_DRAGGABLE_EVENT_TYPES = new Set(['NationalHoliday', 'ReligiousHoliday', 'ImportantDay']);
const EXAM_EVENT_TYPES = new Set(['MidtermExam', 'FinalExam', 'MakeupExam', 'ExcuseExam', 'PrepSchoolExam', 'SummerSchoolExam']);
const MONDAY_START_EVENT_TYPES = new Set([
  'ClassesStart',
  'CourseRegistration',
  'AddDrop',
  'MidtermExam',
  'FinalExam',
  'MakeupExam',
  'PrepSchoolExam',
  'SummerSchoolExam',
]);
const EDIT_CALENDAR_PERMISSION_KEYS = [
  'menu.akademik-takvim-olusturma.edit',
  'menu.akademik-takvim-olusturma.generate',
] as const;

function fmtDate(iso: string | null | undefined) {
  if (!iso) return '-';
  return new Date(iso).toLocaleDateString('tr-TR', { day: '2-digit', month: 'short', year: 'numeric' });
}

function isoToDateStr(iso: string | null | undefined): string | null {
  return iso ? iso.substring(0, 10) : null;
}

function academicYearLabel(year: number | null | undefined) {
  return year ? `${year}-${year + 1}` : '-';
}

function academicYearForTerm(term: Pick<AcademicCalendarTermSummaryDto, 'termStart' | 'year'>) {
  const start = isoToDateStr(term.termStart);
  if (!start) return term.year;

  const [startYear, startMonth] = start.split('-').map(Number);
  if (!startYear || !startMonth) return term.year;

  return startMonth >= 9 ? startYear : startYear - 1;
}

function formatEventDateRange(start: string | null | undefined, end: string | null | undefined) {
  if (!start)
    return '-';

  const startDate = new Date(start);
  const endDate = end ? new Date(end) : startDate;
  const sameDay = startDate.toDateString() === endDate.toDateString();
  if (sameDay)
    return startDate.toLocaleDateString('tr-TR', { day: 'numeric', month: 'long', year: 'numeric' });

  const sameYear = startDate.getFullYear() === endDate.getFullYear();
  const sameMonth = sameYear && startDate.getMonth() === endDate.getMonth();
  if (sameMonth) {
    return `${startDate.toLocaleDateString('tr-TR', { day: 'numeric' })} - ${endDate.toLocaleDateString('tr-TR', { day: 'numeric', month: 'long', year: 'numeric' })}`;
  }

  if (sameYear) {
    return `${startDate.toLocaleDateString('tr-TR', { day: 'numeric', month: 'long' })} - ${endDate.toLocaleDateString('tr-TR', { day: 'numeric', month: 'long', year: 'numeric' })}`;
  }

  return `${startDate.toLocaleDateString('tr-TR', { day: 'numeric', month: 'long', year: 'numeric' })} - ${endDate.toLocaleDateString('tr-TR', { day: 'numeric', month: 'long', year: 'numeric' })}`;
}

function rangeText(ranges: Array<[string | null | undefined, string | null | undefined]>) {
  const starts = ranges.map(([start]) => start).filter(Boolean).sort() as string[];
  const ends = ranges.map(([, end]) => end).filter(Boolean).sort() as string[];
  if (starts.length === 0 || ends.length === 0) return '-';
  return `${fmtDate(starts[0])} - ${fmtDate(ends[ends.length - 1])}`;
}

function latestText(values: Array<string | null | undefined>) {
  const dates = values.filter(Boolean).sort() as string[];
  return dates.length > 0 ? fmtDate(dates[dates.length - 1]) : '-';
}

function parseDateValue(value: string) {
  const [year, month, day] = value.split('-').map(Number);
  return new Date(Date.UTC(year, month - 1, day));
}

function shiftDateValue(value: string, days: number) {
  const shifted = parseDateValue(value);
  shifted.setUTCDate(shifted.getUTCDate() + days);
  return shifted.toISOString().substring(0, 10);
}

function dayDifference(target: string, source: string) {
  return Math.round((parseDateValue(target).getTime() - parseDateValue(source).getTime()) / (24 * 60 * 60 * 1000));
}

function isWeekendDate(value: string) {
  const day = parseDateValue(value).getUTCDay();
  return day === 0 || day === 6;
}

function isMondayDate(value: string) {
  return parseDateValue(value).getUTCDay() === 1;
}

function calendarEditWindow(calendar: StudentAcademicCalendarDto) {
  return {
    start: `${calendar.year}-06-01`,
    end: `${calendar.year + 1}-09-30`,
  };
}

function buildVisualizerEventId(termId: number, eventId: number) {
  return `${termId}:${eventId}`;
}

function parseVisualizerEventId(value: string | number) {
  const [termId, eventId] = String(value).split(':').map(Number);
  return Number.isFinite(termId) && Number.isFinite(eventId)
    ? { termId, eventId }
    : null;
}

function canDragCalendarEvent(event: TermCalendarEventDto) {
  return event.isPublic && !event.blocksAcademicEvents && !NON_DRAGGABLE_EVENT_TYPES.has(event.eventType);
}

function schedulingRuleEventType(event: TermCalendarEventDto) {
  return resolveCalendarEventType(event.eventType, event.title, event.templateItemKey);
}

type DraftCalendarMap = Record<number, StudentAcademicCalendarDto>;

function cloneCalendar(calendar: StudentAcademicCalendarDto): StudentAcademicCalendarDto {
  return {
    ...calendar,
    events: calendar.events.map(event => ({ ...event })),
  };
}

function cloneDraftCalendarMap(map: DraftCalendarMap): DraftCalendarMap {
  return Object.fromEntries(
    Object.entries(map).map(([termId, calendar]) => [Number(termId), cloneCalendar(calendar)]),
  ) as DraftCalendarMap;
}

function getErrorMessage(error: unknown) {
  const fallback = 'İşlem tamamlanamadı.';
  if (!error || typeof error !== 'object')
    return fallback;

  const maybe = error as { response?: { data?: { message?: string; error?: string } }; message?: string };
  const message = maybe.response?.data?.message ?? maybe.response?.data?.error ?? maybe.message ?? fallback;
  return displayTurkishText(message);
}

function eventToVisualizerEvent(termId: number, event: TermCalendarEventDto, allowDrag: boolean): VisualizerEvent | null {
  const startDate = isoToDateStr(event.startDate);
  const endDate = isoToDateStr(event.endDate) ?? startDate;
  if (!startDate || !endDate) return null;

  return {
    id: buildVisualizerEventId(termId, event.id),
    eventType: event.eventType,
    title: displayTurkishText(event.title),
    startDate,
    endDate,
    note: event.note ? displayTurkishText(event.note) : event.note,
    templateItemKey: event.templateItemKey,
    draggable: allowDrag && canDragCalendarEvent(event),
  };
}

function fallbackPhaseEvents(calendar: StudentAcademicCalendarDto): VisualizerEvent[] {
  const events: VisualizerEvent[] = [];
  const add = (id: string, eventType: string, title: string, start?: string | null, end?: string | null) => {
    const startDate = isoToDateStr(start);
    const endDate = isoToDateStr(end) ?? startDate;
    if (!startDate || !endDate) return;
    events.push({ id: `${calendar.termId}-${id}`, eventType, title, startDate, endDate });
  };

  add('registration', 'CourseRegistration', 'Ders kayıtları', calendar.registrationStart, calendar.registrationEnd);
  add('add-drop', 'AddDrop', 'Ders ekleme / bırakma', calendar.addDropStart, calendar.addDropEnd);
  add('withdrawal', 'CourseWithdrawal', 'Dersten çekilme son günü', calendar.withdrawalDeadline, calendar.withdrawalDeadline);
  add('grade-entry', 'FinalGradeEntry', 'Not girişi', calendar.gradeEntryStart, calendar.gradeEntryEnd);

  return events;
}

function printableEventsForCalendar(calendar: StudentAcademicCalendarDto, includeAllEvents: boolean) {
  const publicEvents = calendar.events
    .filter(event => includeAllEvents || event.isPublic)
    .slice()
    .sort((left, right) =>
      left.startDate.localeCompare(right.startDate)
      || (left.endDate ?? left.startDate).localeCompare(right.endDate ?? right.startDate)
      || displayTurkishText(left.title).localeCompare(displayTurkishText(right.title), 'tr-TR'))
    .map(event => ({
      dateText: formatEventDateRange(event.startDate, event.endDate),
      title: displayTurkishText(event.title),
    }));

  if (publicEvents.length > 0)
    return publicEvents;

  return [
    {
      dateText: formatEventDateRange(calendar.registrationStart, calendar.registrationEnd),
      title: 'Ders kayıtları',
    },
    {
      dateText: formatEventDateRange(calendar.addDropStart, calendar.addDropEnd),
      title: 'Ders ekleme / bırakma',
    },
    {
      dateText: formatEventDateRange(calendar.withdrawalDeadline, calendar.withdrawalDeadline),
      title: 'Dersten çekilme son günü',
    },
    {
      dateText: formatEventDateRange(calendar.gradeEntryStart, calendar.gradeEntryEnd),
      title: 'Not girişi',
    },
  ].filter(event => event.dateText !== '-');
}

function buildPdfSections(calendars: StudentAcademicCalendarDto[], includeAllEvents: boolean): AcademicCalendarPdfSection[] {
  return calendars.map(calendar => ({
    title: `${displayTurkishText(calendar.termTypeLabel).toLocaleUpperCase('tr-TR')} DÖNEMİ`,
    rows: printableEventsForCalendar(calendar, includeAllEvents),
  }));
}

function PhaseCard({
  icon,
  accent,
  label,
  value,
  topBorder = false,
}: {
  icon: ReactNode;
  accent: string;
  label: string;
  value: string;
  topBorder?: boolean;
}) {
  return (
    <div className={cn('relative px-4 py-3 min-h-[64px] flex flex-col justify-center', topBorder && 'border-t lg:border-t-0 border-slate-100')}>
      <span className={cn('absolute left-0 top-1/2 -translate-y-1/2 w-1 h-8 rounded-r-full', accent)} aria-hidden="true" />
      <div className="flex items-center gap-1.5 text-[10px] font-semibold uppercase tracking-wider text-slate-500">
        {icon}
        <span>{label}</span>
      </div>
      <div className="mt-1 text-sm font-medium text-slate-800 truncate">{value}</div>
    </div>
  );
}

export default function AcademicCalendarPage() {
  const terms = useAcademicCalendarTerms();
  const { hasAnyPermission } = usePermissions();
  const previewCalendarEventMove = usePreviewAcademicCalendarEventMove();
  const saveCalendarChanges = useSaveAcademicCalendarChanges();
  const [explicitYear, setExplicitYear] = useState<number | null>(null);
  const [isDownloadingPdf, setIsDownloadingPdf] = useState(false);
  const [notice, setNotice] = useState<string | null>(null);
  const [errorNotice, setErrorNotice] = useState<string | null>(null);
  const [draftCalendars, setDraftCalendars] = useState<DraftCalendarMap>({});
  const [draftHistory, setDraftHistory] = useState<DraftCalendarMap[]>([]);
  const canEditCalendar = hasAnyPermission(EDIT_CALENDAR_PERMISSION_KEYS);

  const academicYears = useMemo(() => {
    const years = new Set((terms.data ?? []).map(academicYearForTerm));
    return Array.from(years).sort((left, right) => right - left);
  }, [terms.data]);

  const activeTerm = terms.data?.find(term => term.isActive && !term.isCancelled);
  const selectedYear = explicitYear ?? (activeTerm ? academicYearForTerm(activeTerm) : null) ?? academicYears[0] ?? null;

  useEffect(() => {
    setDraftCalendars({});
    setDraftHistory([]);
  }, [selectedYear]);

  const selectedYearTerms = useMemo(
    () => (terms.data ?? [])
      .filter(term => academicYearForTerm(term) === selectedYear)
      .sort((left, right) => left.termStart.localeCompare(right.termStart)),
    [selectedYear, terms.data],
  );

  const calendarQueries = useAcademicYearCalendars(selectedYearTerms.map(term => term.termId), canEditCalendar);
  const serverCalendars = calendarQueries
    .map(query => query.data)
    .filter((calendar): calendar is StudentAcademicCalendarDto => Boolean(calendar));
  const calendars = useMemo(
    () => serverCalendars.map(calendar => draftCalendars[calendar.termId] ?? calendar),
    [draftCalendars, serverCalendars],
  );
  const isLoading = terms.isLoading || calendarQueries.some(query => query.isLoading);
  const missingCalendarTerms = selectedYearTerms.filter(term => !term.isCancelled && !term.hasCalendar);
  const hasDraftChanges = Object.keys(draftCalendars).length > 0;
  const isCalendarMutating = previewCalendarEventMove.isPending || saveCalendarChanges.isPending;

  const visualizerEvents = useMemo(
    () => calendars.flatMap(calendar => {
      const publicEvents = calendar.events
        .map(event => eventToVisualizerEvent(calendar.termId, event, canEditCalendar))
        .filter((event): event is VisualizerEvent => event !== null);

      return publicEvents.length > 0 ? publicEvents : fallbackPhaseEvents(calendar);
    }),
    [calendars, canEditCalendar],
  );
  const pdfSections = useMemo(
    () => buildPdfSections(calendars, canEditCalendar),
    [calendars, canEditCalendar],
  );

  const yearRangeValue = selectedYear
    ? `${fmtDate(`${selectedYear}-09-01`)} - ${fmtDate(`${selectedYear + 1}-08-31`)}`
    : '-';

  async function handleDownloadPdf() {
    if (pdfSections.length === 0)
      return;

    try {
      setIsDownloadingPdf(true);
      await new Promise(resolve => window.requestAnimationFrame(() => resolve(null)));

      downloadAcademicCalendarPdf({
        documentTitle: `${academicYearLabel(selectedYear)} Akademik Takvim`,
        fileName: `akademik-takvim-${academicYearLabel(selectedYear)}.pdf`,
        sections: pdfSections,
      });
    } finally {
      setIsDownloadingPdf(false);
    }
  }

  const showErrorNotice = useCallback((message: string) => {
    setNotice(null);
    setErrorNotice(message);
  }, []);

  const handleEventDrop = useCallback(({ eventId, targetDate, sourceStartDate, sourceEndDate, dragAnchorDate }: {
    eventId: VisualizerEvent['id'];
    targetDate: string;
    sourceStartDate: string;
    sourceEndDate: string;
    dragAnchorDate: string;
  }) => {
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

  return (
    <div className="w-full min-w-0 max-w-full space-y-3 overflow-x-hidden">
      <Dialog open={!!errorNotice} onOpenChange={(open) => !open && setErrorNotice(null)}>
        <DialogContent className="max-w-md border-red-200">
          <DialogHeader>
            <DialogTitle className="flex items-center gap-2 text-red-900">
              <AlertTriangle className="h-5 w-5 text-red-600" />
              Hata
            </DialogTitle>
            <DialogDescription className="text-sm leading-6 text-slate-700">
              {errorNotice}
            </DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button type="button" onClick={() => setErrorNotice(null)}>
              Tamam
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={!!notice} onOpenChange={(open) => !open && setNotice(null)}>
        <DialogContent className="max-w-md border-emerald-200">
          <DialogHeader>
            <DialogTitle className="flex items-center gap-2 text-emerald-900">
              <CheckCircle2 className="h-5 w-5 text-emerald-600" />
              İşlem tamamlandı
            </DialogTitle>
            <DialogDescription className="text-sm leading-6 text-slate-700">
              {notice}
            </DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button type="button" onClick={() => setNotice(null)}>
              Tamam
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <div className="flex flex-col lg:flex-row lg:items-stretch border border-slate-200 rounded-xl bg-white shadow-sm overflow-hidden">
        <div className="lg:w-72 lg:flex-shrink-0 bg-slate-50 lg:border-r border-slate-200">
          <label htmlFor="academic-year-select" className="sr-only">Akademik yıl</label>
          <Select value={selectedYear?.toString() ?? ''} onValueChange={(value) => setExplicitYear(Number(value))}>
            <SelectTrigger id="academic-year-select" aria-label="Akademik yıl seçimi" className="border-0 focus:ring-0 shadow-none h-full min-h-[64px] bg-transparent rounded-none px-4 font-semibold text-slate-800">
              <div className="flex flex-col items-start gap-0.5">
                <span className="text-[10px] font-bold uppercase tracking-[0.18em] text-slate-400">Akademik yıl seçimi</span>
                <SelectValue placeholder="Akademik yıl seçin..." />
              </div>
            </SelectTrigger>
            <SelectContent>
              {academicYears.map(year => (
                <SelectItem key={year} value={year.toString()}>
                  {academicYearLabel(year)}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>

        <div className="flex-1 grid grid-cols-2 lg:grid-cols-4 divide-x divide-slate-100 border-t lg:border-t-0 border-slate-200">
          <PhaseCard
            icon={<CalendarDays className="w-4 h-4 text-indigo-500" />}
            accent="bg-indigo-500"
            label="Akademik Yıl"
            value={yearRangeValue}
          />
          <PhaseCard
            icon={<UserPlus className="w-4 h-4 text-blue-500" />}
            accent="bg-blue-500"
            label="Kayıt"
            value={rangeText(calendars.map(calendar => [calendar.registrationStart, calendar.registrationEnd]))}
          />
          <PhaseCard
            icon={<Repeat2 className="w-4 h-4 text-emerald-500" />}
            accent="bg-emerald-500"
            label="Ekle / Bırak"
            value={rangeText(calendars.map(calendar => [calendar.addDropStart, calendar.addDropEnd]))}
            topBorder
          />
          <PhaseCard
            icon={<LogOut className="w-4 h-4 text-rose-500" />}
            accent="bg-rose-500"
            label="Ders Çekilme"
            value={latestText(calendars.map(calendar => calendar.withdrawalDeadline))}
            topBorder
          />
        </div>
      </div>

      {missingCalendarTerms.length > 0 && (
        <div className="flex items-center gap-2 p-4 bg-amber-50 border border-amber-200 rounded-lg text-amber-900">
          <AlertTriangle className="w-5 h-5 flex-shrink-0" />
          <span>
            Bu akademik yılda takvimi eksik dönemler var: {missingCalendarTerms.map(term => term.termName).join(', ')}.
          </span>
        </div>
      )}

      {isLoading ? (
        <div className="border border-gray-200 rounded-xl bg-white shadow-sm p-10 text-center text-gray-400">
          Takvim yükleniyor...
        </div>
      ) : (
        <div className="space-y-3">
          <CalendarVisualizer
            events={visualizerEvents}
            inlineYearView
            filterableLegend
            headerActions={(
              <div className="flex flex-wrap items-center justify-end gap-2">
                {hasDraftChanges && (
                  <>
                    <Button
                      type="button"
                      variant="outline"
                      onClick={handleUndoDraftChange}
                      disabled={isCalendarMutating || draftHistory.length === 0}
                    >
                      <Undo2 className="h-4 w-4" />
                      Geri al
                    </Button>
                    <Button
                      type="button"
                      variant="outline"
                      onClick={handleCancelDraftChanges}
                      disabled={isCalendarMutating}
                    >
                      <X className="h-4 w-4" />
                      Iptal
                    </Button>
                    <Button
                      type="button"
                      onClick={handleSaveDraftChanges}
                      disabled={isCalendarMutating}
                    >
                      {saveCalendarChanges.isPending ? <RefreshCw className="h-4 w-4 animate-spin" /> : <Save className="h-4 w-4" />}
                      Kaydet
                    </Button>
                  </>
                )}
                <Button
                  type="button"
                  variant="outline"
                  onClick={handleDownloadPdf}
                  disabled={isDownloadingPdf || isCalendarMutating || pdfSections.length === 0}
                >
                  {isDownloadingPdf ? <RefreshCw className="h-4 w-4 animate-spin" /> : <FileDown className="h-4 w-4" />}
                  {isDownloadingPdf ? 'PDF hazirlaniyor' : 'PDF indir'}
                </Button>
              </div>
            )}
            academicYearStart={selectedYear ?? undefined}
            onEventDrop={canEditCalendar ? handleEventDrop : undefined}
            getDropFeedback={canEditCalendar ? getDropFeedback : undefined}
            onInvalidEventDrop={handleInvalidEventDrop}
            dragDisabled={isCalendarMutating}
          />
        </div>
      )}
    </div>
  );
}
