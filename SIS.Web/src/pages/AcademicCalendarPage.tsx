import { useMemo, useState } from 'react';
import type { ReactNode } from 'react';
import { AlertTriangle, CalendarDays, LogOut, Repeat2, UserPlus } from 'lucide-react';
import { CalendarVisualizer, type VisualizerEvent } from '@/components/CalendarVisualizer';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { cn } from '@/lib/utils';
import { displayTurkishText } from '@/lib/turkishText';
import {
  useAcademicCalendarTerms,
  useAcademicYearCalendars,
  type StudentAcademicCalendarDto,
  type TermCalendarEventDto,
} from '@/hooks/useAcademicCalendar';

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

function eventToVisualizerEvent(termId: number, event: TermCalendarEventDto): VisualizerEvent | null {
  const startDate = isoToDateStr(event.startDate);
  const endDate = isoToDateStr(event.endDate) ?? startDate;
  if (!startDate || !endDate) return null;

  return {
    id: `${termId}-${event.id}`,
    eventType: event.eventType,
    title: displayTurkishText(event.title),
    startDate,
    endDate,
    note: event.note ? displayTurkishText(event.note) : event.note,
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

function toVisualizerEvents(calendar: StudentAcademicCalendarDto): VisualizerEvent[] {
  const publicEvents = calendar.events
    .map(event => eventToVisualizerEvent(calendar.termId, event))
    .filter((event): event is VisualizerEvent => event !== null);

  return publicEvents.length > 0 ? publicEvents : fallbackPhaseEvents(calendar);
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
  const [explicitYear, setExplicitYear] = useState<number | null>(null);

  const academicYears = useMemo(() => {
    const years = new Set((terms.data ?? []).map(term => term.year));
    return Array.from(years).sort((left, right) => right - left);
  }, [terms.data]);

  const activeTerm = terms.data?.find(term => term.isActive && !term.isCancelled);
  const selectedYear = explicitYear ?? activeTerm?.year ?? academicYears[0] ?? null;

  const selectedYearTerms = useMemo(
    () => (terms.data ?? [])
      .filter(term => term.year === selectedYear && !term.isCancelled)
      .sort((left, right) => left.termStart.localeCompare(right.termStart)),
    [selectedYear, terms.data],
  );

  const calendarQueries = useAcademicYearCalendars(selectedYearTerms.map(term => term.termId));
  const calendars = calendarQueries
    .map(query => query.data)
    .filter((calendar): calendar is StudentAcademicCalendarDto => Boolean(calendar));
  const isLoading = terms.isLoading || calendarQueries.some(query => query.isLoading);
  const missingCalendarTerms = selectedYearTerms.filter(term => !term.hasCalendar);

  const visualizerEvents = useMemo(
    () => calendars.flatMap(calendar => toVisualizerEvents(calendar)),
    [calendars],
  );

  const yearRangeValue = selectedYear
    ? `${fmtDate(`${selectedYear}-09-01`)} - ${fmtDate(`${selectedYear + 1}-08-31`)}`
    : '-';

  return (
    <div className="w-full min-w-0 max-w-full space-y-3 overflow-x-hidden">
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
        <CalendarVisualizer
          events={visualizerEvents}
          inlineYearView
          filterableLegend
          academicYearStart={selectedYear ?? undefined}
        />
      )}
    </div>
  );
}
