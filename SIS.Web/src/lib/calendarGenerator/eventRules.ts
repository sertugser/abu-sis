import { displayTurkishText } from '@/lib/turkishText';
import { calendarEventCategory } from '@/lib/calendarEventTypes';
import {
  countWeekdayOnlyEventDays,
  formatWeekdayOnlyDateRanges,
  getWeekdayOnlyDateRanges,
} from '@/lib/calendarWeekdaySegments';
import {
  addDays,
  dateInput,
  diffWeeks,
  fixedDayMonth,
  fmt,
  fmtLong,
  formatUtcDateValue,
  isWeekend,
  parseUtcDisplayDate,
  rangesOverlap,
} from '@/lib/calendarGenerator/dates';
import {
  CATEGORY_LABELS,
  CRITICAL_EVENT_TYPES,
  FLEXIBLE_HOLIDAY_OVERLAP_MIN_DAYS,
  MONDAY_START_EVENT_TYPES,
  displayText,
  type CalendarView,
  type SemesterWeekAnalysis,
} from '@/lib/calendarGenerator/types';
import type {
  CalendarConstraintInputDto,
  GeneratedCalendarEventDto,
  HolidayDefinitionDto,
  SemesterType,
} from '@/hooks/useCalendarGenerator';

export function holidayDateText(holiday: HolidayDefinitionDto) {
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

export function isAtaturkMemorialHoliday(holiday: HolidayDefinitionDto) {
  return holiday.type === 'ImportantDay'
    && holiday.fixedMonth === 11
    && holiday.fixedDay === 10
    && (
      holiday.englishName.toLocaleLowerCase('en-US').includes('ataturk memorial')
      || displayText(holiday.name).toLocaleLowerCase('tr-TR').includes('atatürk')
    );
}

export function daySpan(ev: GeneratedCalendarEventDto) {
  return formatWeekdayOnlyDateRanges(ev, value => fmt(value));
}

export function visibleEventBounds(event?: GeneratedCalendarEventDto | null) {
  if (!event)
    return { start: null, end: null };

  const ranges = getWeekdayOnlyDateRanges(event);
  const first = ranges[0];
  const last = ranges[ranges.length - 1];
  const rawStart = dateInput(event.startDate) || null;
  const rawEnd = dateInput(event.endDate) || rawStart;

  return {
    start: first?.startDate ?? rawStart,
    end: last?.endDate ?? first?.startDate ?? rawEnd,
  };
}

export function buildExamDayCounts(events: GeneratedCalendarEventDto[]) {
  return events.reduce<Record<SemesterType, number>>((counts, event) => {
    if (event.isHoliday || calendarEventCategory(event.eventType, event.title, event.templateItemKey) !== 'exam')
      return counts;

    counts[event.semester] = (counts[event.semester] ?? 0) + countWeekdayOnlyEventDays(event);
    return counts;
  }, { Fall: 0, Spring: 0, Summer: 0 });
}

export function buildSemesterWeekAnalyses(events: GeneratedCalendarEventDto[]): SemesterWeekAnalysis[] {
  return (['Fall', 'Spring', 'Summer'] as SemesterType[])
    .map(semester => {
      const semesterEvents = events.filter(event => event.semester === semester);
      const classesStart = semesterEvents.find(event => event.templateItemKey === 'classes_start');
      const midterm = semesterEvents.find(event => event.templateItemKey === 'midterm_exam');
      const finalExam = semesterEvents.find(event => event.templateItemKey === 'final_exam');
      if (!classesStart)
        return null;

      const classesStartDate = dateInput(classesStart.startDate);
      const midtermBounds = visibleEventBounds(midterm);
      const finalBounds = visibleEventBounds(finalExam);
      const midtermStart = midtermBounds.start;
      const midtermEnd = midtermBounds.end;
      const finalStart = finalBounds.start;
      const finalEnd = finalBounds.end;
      const weeksBeforeMidterm = midtermStart ? diffWeeks(classesStartDate, midtermStart) : null;
      const midtermWeek = weeksBeforeMidterm !== null ? weeksBeforeMidterm + 1 : null;
      const weeksAfterMidterm = midtermEnd && finalStart ? diffWeeks(midtermEnd, finalStart) : null;
      const finalWeek = finalStart ? (diffWeeks(classesStartDate, finalStart) ?? -1) + 1 : null;

      return {
        semester,
        classesStart: classesStartDate,
        midtermStart,
        midtermEnd,
        finalStart,
        finalEnd,
        weeksBeforeMidterm,
        midtermWeek,
        weeksAfterMidterm,
        finalWeek,
      };
    })
    .filter((analysis): analysis is SemesterWeekAnalysis => !!analysis);
}

export function getDraftId(calendar: CalendarView | null, selectedDraftId: number | null) {
  if (!calendar) return selectedDraftId;
  return 'id' in calendar ? calendar.id : calendar.draftId;
}

export function getErrorMessage(error: unknown) {
  const fallback = 'İşlem tamamlanamadı.';
  if (!error || typeof error !== 'object') return fallback;
  const maybe = error as { response?: { status?: number; data?: { message?: string; error?: string } }; message?: string };
  if (maybe.response?.status === 401)
    return 'Oturum süreniz doldu. Lütfen tekrar giriş yapın.';
  if (maybe.response?.status === 403)
    return 'Bu işlem için yetkiniz bulunmuyor.';

  const message = maybe.response?.data?.message ?? maybe.response?.data?.error ?? maybe.message ?? fallback;
  return displayTurkishText(message);
}

export function isCriticalEvent(ev: GeneratedCalendarEventDto | null) {
  return !!ev && CRITICAL_EVENT_TYPES.has(ev.eventType);
}

export function isEditableDraftEvent(ev: GeneratedCalendarEventDto | null) {
  return !!ev && !ev.isHoliday;
}

export function isExamDraftEvent(ev: GeneratedCalendarEventDto) {
  return calendarEventCategory(ev.eventType, ev.title, ev.templateItemKey) === 'exam';
}

export function mustStartOnMonday(eventType: string) {
  return MONDAY_START_EVENT_TYPES.has(eventType);
}

export function validateDraftEventDateEdit(ev: GeneratedCalendarEventDto, startDate: string, endDate: string) {
  if (!startDate)
    return 'Baslangic tarihi secilmelidir.';

  if (endDate < startDate)
    return 'Bitis tarihi baslangic tarihinden once olamaz.';

  const parsedStart = parseUtcDisplayDate(startDate);
  const parsedEnd = parseUtcDisplayDate(endDate || startDate);
  if (mustStartOnMonday(ev.eventType) && parsedStart?.getUTCDay() !== 1)
    return 'Bu etkinlik Pazartesi günü başlamalıdır.';

  if (isExamDraftEvent(ev)) {
    if (parsedStart && isWeekend(parsedStart))
      return 'Sinav baslangic tarihi hafta sonuna denk gelemez.';
    if (parsedEnd && isWeekend(parsedEnd))
      return 'Sinav bitis tarihi hafta sonuna denk gelemez.';
  }

  return null;
}

export function constraintMatchesDraftEvent(constraint: CalendarConstraintInputDto, ev: GeneratedCalendarEventDto) {
  if (constraint.targetEventType && constraint.targetEventType !== ev.eventType)
    return false;

  if (constraint.targetChainCode && constraint.targetChainCode !== ev.chainCode)
    return false;

  const parameters = parseConstraintParameters(constraint.parameterJson);
  const templateItemKey = readString(parameters?.templateItemKey);
  if (templateItemKey && templateItemKey.toLocaleLowerCase('tr-TR') !== (ev.templateItemKey ?? '').toLocaleLowerCase('tr-TR'))
    return false;

  const semester = readString(parameters?.semester);
  if (semester && semester !== ev.semester)
    return false;

  return true;
}

export function validateDraftEventConstraints(
  ev: GeneratedCalendarEventDto,
  startDate: string,
  endDate: string,
  constraints: CalendarConstraintInputDto[],
) {
  const pinConstraint = constraints
    .filter(constraint => constraint.isHard && constraint.constraintType === 'Pin' && constraintMatchesDraftEvent(constraint, ev))
    .sort((left, right) => left.priority - right.priority)[0];

  if (!pinConstraint)
    return null;

  const parameters = parseConstraintParameters(pinConstraint.parameterJson);
  const pinnedStartDate = readString(parameters?.startDate);
  const pinnedEndDate = readString(parameters?.endDate) ?? pinnedStartDate;
  if (!pinnedStartDate || !pinnedEndDate)
    return null;

  if (startDate === pinnedStartDate && endDate === pinnedEndDate)
    return null;

  const pinnedRange = pinnedStartDate === pinnedEndDate ? pinnedStartDate : `${pinnedStartDate} - ${pinnedEndDate}`;
  return `${displayText(ev.title)} ${pinnedRange} tarihine sabitlenmis; Pin kisiti nedeniyle tasinamaz.`;
}

export function validateDraftEventHolidayOverlap(
  ev: GeneratedCalendarEventDto,
  startDate: string,
  endDate: string,
  holidays: CalendarView['holidays'],
) {
  const start = parseUtcDisplayDate(startDate);
  const end = parseUtcDisplayDate(endDate);
  if (!start || !end)
    return null;

  const overlaps = holidays.filter(holiday => {
    if (!holiday.blocksAcademicEvents)
      return false;

    const holidayStart = parseUtcDisplayDate(holiday.startDate);
    const holidayEnd = parseUtcDisplayDate(holiday.endDate);
    return !!holidayStart && !!holidayEnd && rangesOverlap(start, end, holidayStart, holidayEnd);
  });
  if (overlaps.length === 0)
    return null;

  const durationDays = Math.floor((end.getTime() - start.getTime()) / (24 * 60 * 60 * 1000)) + 1;
  const allowsHolidayOverlap = !CRITICAL_EVENT_TYPES.has(ev.eventType)
    && durationDays >= FLEXIBLE_HOLIDAY_OVERLAP_MIN_DAYS
    && overlaps.every(holiday => holiday.type !== 'HijriCalculated');
  if (allowsHolidayOverlap)
    return null;

  const hasReligiousHoliday = overlaps.some(holiday => holiday.type === 'HijriCalculated');
  return hasReligiousHoliday
    ? `${displayText(ev.title)} dini bayram üzerine taşınamaz.`
    : `${displayText(ev.title)} resmi tatil veya bloklayıcı önemli gün üzerine taşınamaz.`;
}

export function shouldTrimTrailingWeekend(ev: GeneratedCalendarEventDto) {
  return !ev.isHoliday && (!CRITICAL_EVENT_TYPES.has(ev.eventType) || isExamDraftEvent(ev));
}

export function trimTrailingWeekendEnd(ev: GeneratedCalendarEventDto, startDate: string, endDate: string) {
  if (!shouldTrimTrailingWeekend(ev))
    return { nextStartDate: startDate, nextEndDate: endDate };

  const start = parseUtcDisplayDate(startDate);
  const end = parseUtcDisplayDate(endDate);
  if (!start || !end || end.getTime() <= start.getTime())
    return { nextStartDate: startDate, nextEndDate: endDate };

  let trimmedEnd = end;
  while (trimmedEnd.getTime() > start.getTime() && (trimmedEnd.getUTCDay() === 0 || trimmedEnd.getUTCDay() === 6))
    trimmedEnd = addDays(trimmedEnd, -1);

  return {
    nextStartDate: startDate,
    nextEndDate: formatUtcDateValue(trimmedEnd),
  };
}

export function eventCategoryLabel(eventType: string, isHoliday: boolean, title?: string | null, templateItemKey?: string | null) {
  if (isHoliday) return 'Tatil';
  const category = calendarEventCategory(eventType, title, templateItemKey);
  return CATEGORY_LABELS[category] ?? 'Akademik';
}

export function eventCategoryValue(event: Pick<GeneratedCalendarEventDto, 'eventType' | 'title' | 'templateItemKey'>) {
  return calendarEventCategory(event.eventType, event.title, event.templateItemKey);
}

export function parseConstraintParameters(parameterJson: string) {
  try {
    const parsed = JSON.parse(parameterJson);
    return parsed && typeof parsed === 'object' ? parsed as Record<string, unknown> : null;
  } catch {
    return null;
  }
}

export function readString(value: unknown) {
  return typeof value === 'string' ? value : null;
}
