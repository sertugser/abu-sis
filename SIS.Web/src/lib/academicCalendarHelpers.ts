// AcademicCalendarPage'in saf (React'sız) yardımcıları: tarih biçimleme, sürükle-bırak
// kuralları, taslak klonlama, PDF bölümü üretimi. Sayfadan ayrıldı — test edilebilir ve
// useCalendarDraftEditor hook'uyla paylaşılabilir olsun diye.
import { resolveCalendarEventType } from '@/lib/calendarEventTypes';
import { displayTurkishText } from '@/lib/turkishText';
import type { AcademicCalendarPdfSection } from '@/lib/academicCalendarPdf';
import type { VisualizerEvent } from '@/components/CalendarVisualizer';
import type {
  AcademicCalendarTermSummaryDto,
  StudentAcademicCalendarDto,
  TermCalendarEventDto,
} from '@/hooks/useAcademicCalendar';

export const NON_DRAGGABLE_EVENT_TYPES = new Set(['NationalHoliday', 'ReligiousHoliday', 'ImportantDay']);
export const EXAM_EVENT_TYPES = new Set(['MidtermExam', 'FinalExam', 'MakeupExam', 'ExcuseExam', 'PrepSchoolExam', 'SummerSchoolExam']);
export const MONDAY_START_EVENT_TYPES = new Set([
  'ClassesStart',
  'CourseRegistration',
  'AddDrop',
  'MidtermExam',
  'FinalExam',
  'MakeupExam',
  'PrepSchoolExam',
  'SummerSchoolExam',
]);
export const EDIT_CALENDAR_PERMISSION_KEYS = [
  'menu.akademik-takvim-olusturma.edit',
  'menu.akademik-takvim-olusturma.generate',
] as const;

export type DraftCalendarMap = Record<number, StudentAcademicCalendarDto>;

export function fmtDate(iso: string | null | undefined) {
  if (!iso) return '-';
  return new Date(iso).toLocaleDateString('tr-TR', { day: '2-digit', month: 'short', year: 'numeric' });
}

export function isoToDateStr(iso: string | null | undefined): string | null {
  return iso ? iso.substring(0, 10) : null;
}

// Backend, set edilmemiş faz tarihlerini DateTime.MinValue (0001-01-01) olarak döndürür.
// Bunları "tanımsız" say (null gibi). Aksi halde takvim bu sahte tarihten yıl 1'e
// kadar on binlerce ay üretmeye çalışıp tarayıcı sekmesini kilitler.
export function isUnsetIsoDate(iso: string | null | undefined): boolean {
  if (!iso) return true;
  const year = Number(iso.slice(0, 4));
  return !Number.isFinite(year) || year < 1900;
}

export function academicYearLabel(year: number | null | undefined) {
  return year ? `${year}-${year + 1}` : '-';
}

export function academicYearForTerm(term: Pick<AcademicCalendarTermSummaryDto, 'termStart' | 'year'>) {
  const start = isoToDateStr(term.termStart);
  if (!start) return term.year;

  const [startYear, startMonth] = start.split('-').map(Number);
  if (!startYear || !startMonth) return term.year;

  return startMonth >= 9 ? startYear : startYear - 1;
}

export function formatEventDateRange(start: string | null | undefined, end: string | null | undefined) {
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

export function rangeText(ranges: Array<[string | null | undefined, string | null | undefined]>) {
  const starts = ranges.map(([start]) => start).filter(Boolean).sort() as string[];
  const ends = ranges.map(([, end]) => end).filter(Boolean).sort() as string[];
  if (starts.length === 0 || ends.length === 0) return '-';
  return `${fmtDate(starts[0])} - ${fmtDate(ends[ends.length - 1])}`;
}

export function latestText(values: Array<string | null | undefined>) {
  const dates = values.filter(Boolean).sort() as string[];
  return dates.length > 0 ? fmtDate(dates[dates.length - 1]) : '-';
}

export function parseDateValue(value: string) {
  const [year, month, day] = value.split('-').map(Number);
  return new Date(Date.UTC(year, month - 1, day));
}

export function shiftDateValue(value: string, days: number) {
  const shifted = parseDateValue(value);
  shifted.setUTCDate(shifted.getUTCDate() + days);
  return shifted.toISOString().substring(0, 10);
}

export function dayDifference(target: string, source: string) {
  return Math.round((parseDateValue(target).getTime() - parseDateValue(source).getTime()) / (24 * 60 * 60 * 1000));
}

export function isWeekendDate(value: string) {
  const day = parseDateValue(value).getUTCDay();
  return day === 0 || day === 6;
}

export function isMondayDate(value: string) {
  return parseDateValue(value).getUTCDay() === 1;
}

export function calendarEditWindow(calendar: StudentAcademicCalendarDto) {
  return {
    start: `${calendar.year}-06-01`,
    end: `${calendar.year + 1}-09-30`,
  };
}

export function buildVisualizerEventId(termId: number, eventId: number) {
  return `${termId}:${eventId}`;
}

export function parseVisualizerEventId(value: string | number) {
  const [termId, eventId] = String(value).split(':').map(Number);
  return Number.isFinite(termId) && Number.isFinite(eventId)
    ? { termId, eventId }
    : null;
}

export function canDragCalendarEvent(event: TermCalendarEventDto) {
  return event.isPublic && !NON_DRAGGABLE_EVENT_TYPES.has(event.eventType);
}

export function schedulingRuleEventType(event: TermCalendarEventDto) {
  return resolveCalendarEventType(event.eventType, event.title, event.templateItemKey);
}

export function cloneCalendar(calendar: StudentAcademicCalendarDto): StudentAcademicCalendarDto {
  return {
    ...calendar,
    events: calendar.events.map(event => ({ ...event })),
  };
}

export function cloneDraftCalendarMap(map: DraftCalendarMap): DraftCalendarMap {
  return Object.fromEntries(
    Object.entries(map).map(([termId, calendar]) => [Number(termId), cloneCalendar(calendar)]),
  ) as DraftCalendarMap;
}

export function getErrorMessage(error: unknown) {
  const fallback = 'İşlem tamamlanamadı.';
  if (!error || typeof error !== 'object')
    return fallback;

  const maybe = error as { response?: { status?: number; data?: { message?: string; error?: string } }; message?: string };
  if (maybe.response?.status === 401)
    return 'Oturum süreniz doldu. Lütfen tekrar giriş yapın.';
  if (maybe.response?.status === 403)
    return 'Bu işlem için yetkiniz bulunmuyor.';

  const message = maybe.response?.data?.message ?? maybe.response?.data?.error ?? maybe.message ?? fallback;
  return displayTurkishText(message);
}

export function eventToVisualizerEvent(termId: number, event: TermCalendarEventDto, allowDrag: boolean): VisualizerEvent | null {
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

export function fallbackPhaseEvents(calendar: StudentAcademicCalendarDto): VisualizerEvent[] {
  const events: VisualizerEvent[] = [];
  const add = (id: string, eventType: string, title: string, start?: string | null, end?: string | null) => {
    if (isUnsetIsoDate(start)) return;
    const startDate = isoToDateStr(start);
    const endDate = (isUnsetIsoDate(end) ? null : isoToDateStr(end)) ?? startDate;
    if (!startDate || !endDate) return;
    events.push({ id: `${calendar.termId}-${id}`, eventType, title, startDate, endDate });
  };

  add('registration', 'CourseRegistration', 'Ders kayıtları', calendar.registrationStart, calendar.registrationEnd);
  add('add-drop', 'AddDrop', 'Ders ekleme / bırakma', calendar.addDropStart, calendar.addDropEnd);
  add('withdrawal', 'CourseWithdrawal', 'Dersten çekilme son günü', calendar.withdrawalDeadline, calendar.withdrawalDeadline);
  add('grade-entry', 'FinalGradeEntry', 'Not girişi', calendar.gradeEntryStart, calendar.gradeEntryEnd);

  return events;
}

// Liste sekmesi için gösterilebilir event'leri kronolojik sırada döner.
// Takvim oluşturucu yetkisi olan kullanıcılar API'den includeAllEvents=true ile
// tüm event'leri aldığı için burada tekrar sadece public'e daraltmıyoruz.
// PDF helper'ı (printableEventsForCalendar) string formatlanmış tek sütun döndürdüğü için
// tabloya ayrı bir kaynak gerekti: tablo Başlangıç ve Bitiş'i ayrı kolonlarda gösteriyor.
export function visibleEventsForCalendar(calendar: StudentAcademicCalendarDto, includeAllEvents: boolean): TermCalendarEventDto[] {
  return calendar.events
    .filter(event => includeAllEvents || event.isPublic)
    .slice()
    .sort((left, right) =>
      left.startDate.localeCompare(right.startDate)
      || (left.endDate ?? left.startDate).localeCompare(right.endDate ?? right.startDate)
      || displayTurkishText(left.title).localeCompare(displayTurkishText(right.title), 'tr-TR'));
}

export function printableEventsForCalendar(calendar: StudentAcademicCalendarDto, includeAllEvents: boolean) {
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

export function buildPdfSections(calendars: StudentAcademicCalendarDto[], includeAllEvents: boolean): AcademicCalendarPdfSection[] {
  return calendars.map(calendar => ({
    title: `${displayTurkishText(calendar.termTypeLabel).toLocaleUpperCase('tr-TR')} DÖNEMİ`,
    rows: printableEventsForCalendar(calendar, includeAllEvents),
  }));
}
