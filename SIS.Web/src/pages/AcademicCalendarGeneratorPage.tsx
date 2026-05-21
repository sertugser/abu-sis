import { useEffect, useMemo, useState } from 'react';
import { AlertTriangle, CheckCircle2, ChevronDown, ChevronLeft, ChevronRight, GitBranch, RefreshCw, RotateCcw, Save, Trash2, Wand2 } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { CalendarVisualizer, type DropFeedback, type DropFeedbackRequest, type VisualizerEvent } from '@/components/CalendarVisualizer';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
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
  type CalendarEventBadgeVariant,
  type CalendarEventCategory,
} from '@/lib/calendarEventTypes';
import { countWeekdayOnlyEventDays, formatWeekdayOnlyDateRanges, getWeekdayOnlyDateRanges } from '@/lib/calendarWeekdaySegments';
import { displayTurkishText } from '@/lib/turkishText';
import { useAdminTerms } from '../hooks/useAdminTerms';
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
type NoticeTone = 'info' | 'success' | 'error';
type WarningRow = {
  warning: CalendarWarningDto;
  message: string;
  detail: string | null;
  key: string;
  badgeLabel: string;
  badgeVariant: 'destructive' | 'warning' | 'secondary' | 'success' | 'info';
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

const NOTICE_AUTO_DISMISS_MS = 2000;

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

type DateSelectionRange = {
  min: string;
  max: string;
  label: string;
};

type ActiveGeneratorSetting = {
  key: string;
  badgeLabel: string;
  badgeVariant: 'destructive' | 'secondary' | 'info';
  title: string;
  summary: string;
  detailLines: string[];
  removeKind: 'constraint' | 'duration';
  removeIndex?: number;
  removeKey?: string;
};

type SemesterWeekAnalysis = {
  semester: SemesterType;
  classesStart: string;
  midtermStart: string | null;
  midtermEnd: string | null;
  finalStart: string | null;
  finalEnd: string | null;
  weeksBeforeMidterm: number | null;
  midtermWeek: number | null;
  weeksAfterMidterm: number | null;
  finalWeek: number | null;
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
  'MakeupExamApplication',
  'MakeupExam',
  'ExcuseExam',
  'MidtermGradePublication',
  'FinalGradePublication',
  'MakeupGradePublication',
  'SummerSchoolRegistration',
  'SummerSchoolExam',
  'SummerSchoolGradePublication',
]);
const MONDAY_START_EVENT_TYPES = new Set([
  'ClassesStart',
  'CourseRegistration',
  'AddDrop',
  'MidtermExam',
  'FinalExam',
  'MakeupExam',
  'ExcuseExam',
  'PrepSchoolExam',
  'SummerSchoolExam',
]);
const FLEXIBLE_HOLIDAY_OVERLAP_MIN_DAYS = 15;

function defaultAcademicYear() {
  const now = new Date();
  const start = now.getMonth() >= 6 ? now.getFullYear() : now.getFullYear() - 1;
  return `${start}-${start + 1}`;
}

function academicYearStart(academicYear: string) {
  const parsed = Number(academicYear.split('-')[0]);
  return Number.isFinite(parsed) ? parsed : new Date().getFullYear();
}

function formatAcademicYear(startYear: number) {
  return `${startYear}-${startYear + 1}`;
}

function shiftAcademicYear(academicYear: string, offset: number) {
  return formatAcademicYear(academicYearStart(academicYear) + offset);
}

function dateValue(year: number, month: number, day: number) {
  return `${year}-${String(month).padStart(2, '0')}-${String(day).padStart(2, '0')}`;
}

function formatUtcDateValue(date: Date) {
  return dateValue(date.getUTCFullYear(), date.getUTCMonth() + 1, date.getUTCDate());
}

function nearestMondayDateValue(year: number, month: number, day: number) {
  const base = new Date(Date.UTC(year, month - 1, day));
  const weekday = base.getUTCDay();
  const previousOffset = weekday === 0 ? -6 : 1 - weekday;
  const nextOffset = weekday === 0 ? 1 : 8 - weekday;
  const chosenOffset = Math.abs(previousOffset) <= Math.abs(nextOffset) ? previousOffset : nextOffset;
  const adjusted = new Date(base);
  adjusted.setUTCDate(base.getUTCDate() + chosenOffset);
  return dateValue(adjusted.getUTCFullYear(), adjusted.getUTCMonth() + 1, adjusted.getUTCDate());
}

function dateInput(value?: string | null) {
  return value ? value.substring(0, 10) : '';
}

function utcDate(value: string | null) {
  return value ? `${value}T00:00:00.000Z` : null;
}

function parseUtcDisplayDate(value?: string | null) {
  const input = dateInput(value);
  if (!input) return null;
  const [year, month, day] = input.split('-').map(Number);
  if (!year || !month || !day) return null;
  return new Date(Date.UTC(year, month - 1, day));
}

function fmt(value?: string | null) {
  if (!value) return '-';
  const date = parseUtcDisplayDate(value);
  return date
    ? date.toLocaleDateString('tr-TR', { day: '2-digit', month: 'short', year: 'numeric', timeZone: 'UTC' })
    : '-';
}

function fmtLong(value?: string | null) {
  if (!value) return '-';
  const date = parseUtcDisplayDate(value);
  return date
    ? date.toLocaleDateString('tr-TR', { day: 'numeric', month: 'long', year: 'numeric', timeZone: 'UTC' })
    : '-';
}

function buildDateSelectionRange(minYear: number, minMonth: number, minDay: number, maxYear: number, maxMonth: number, maxDay: number): DateSelectionRange {
  const min = dateValue(minYear, minMonth, minDay);
  const max = dateValue(maxYear, maxMonth, maxDay);
  return {
    min,
    max,
    label: `${fmtLong(utcDate(min))} - ${fmtLong(utcDate(max))}`,
  };
}

function buildAcademicYearSelectionRange(academicYear: string, includeSummer: boolean) {
  const startYear = academicYearStart(academicYear);
  return includeSummer
    ? buildDateSelectionRange(startYear, 7, 1, startYear + 1, 9, 30)
    : buildDateSelectionRange(startYear, 7, 1, startYear + 1, 6, 30);
}

function buildSemesterSelectionRange(academicYear: string, semester: SemesterType) {
  const startYear = academicYearStart(academicYear);
  switch (semester) {
    case 'Fall':
      return buildDateSelectionRange(startYear, 7, 1, startYear + 1, 1, 31);
    case 'Spring':
      return buildDateSelectionRange(startYear + 1, 1, 1, startYear + 1, 6, 30);
    case 'Summer':
      return buildDateSelectionRange(startYear + 1, 6, 1, startYear + 1, 9, 30);
    default:
      return buildAcademicYearSelectionRange(academicYear, true);
  }
}

function isDateValueWithinRange(value: string, range: DateSelectionRange) {
  return !value || (value >= range.min && value <= range.max);
}

function maxDateValue(left: string, right: string) {
  return left > right ? left : right;
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

function isAtaturkMemorialHoliday(holiday: HolidayDefinitionDto) {
  return holiday.type === 'ImportantDay'
    && holiday.fixedMonth === 11
    && holiday.fixedDay === 10
    && (
      holiday.englishName.toLocaleLowerCase('en-US').includes('ataturk memorial')
      || displayText(holiday.name).toLocaleLowerCase('tr-TR').includes('atatürk')
    );
}

function daySpan(ev: GeneratedCalendarEventDto) {
  return formatWeekdayOnlyDateRanges(ev, value => fmt(value));
}
function diffWeeks(start: string, end: string) {
  const startDate = parseUtcDisplayDate(start);
  const endDate = parseUtcDisplayDate(end);
  if (!startDate || !endDate) return null;
  return Math.floor((endDate.getTime() - startDate.getTime()) / (7 * 24 * 60 * 60 * 1000));
}

function visibleEventBounds(event?: GeneratedCalendarEventDto | null) {
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

function buildExamDayCounts(events: GeneratedCalendarEventDto[]) {
  return events.reduce<Record<SemesterType, number>>((counts, event) => {
    if (event.isHoliday || calendarEventCategory(event.eventType, event.title, event.templateItemKey) !== 'exam')
      return counts;

    counts[event.semester] = (counts[event.semester] ?? 0) + countWeekdayOnlyEventDays(event);
    return counts;
  }, { Fall: 0, Spring: 0, Summer: 0 });
}

function buildSemesterWeekAnalyses(events: GeneratedCalendarEventDto[]): SemesterWeekAnalysis[] {
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

function getDraftId(calendar: CalendarView | null, selectedDraftId: number | null) {
  if (!calendar) return selectedDraftId;
  return 'id' in calendar ? calendar.id : calendar.draftId;
}

function getErrorMessage(error: unknown) {
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

function isCriticalEvent(ev: GeneratedCalendarEventDto | null) {
  return !!ev && CRITICAL_EVENT_TYPES.has(ev.eventType);
}

function isEditableDraftEvent(ev: GeneratedCalendarEventDto | null) {
  return !!ev && !ev.isHoliday;
}

function isExamDraftEvent(ev: GeneratedCalendarEventDto) {
  return calendarEventCategory(ev.eventType, ev.title, ev.templateItemKey) === 'exam';
}

function mustStartOnMonday(eventType: string) {
  return MONDAY_START_EVENT_TYPES.has(eventType);
}

function validateDraftEventDateEdit(ev: GeneratedCalendarEventDto, startDate: string, endDate: string) {
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

function constraintMatchesDraftEvent(constraint: CalendarConstraintInputDto, ev: GeneratedCalendarEventDto) {
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

function validateDraftEventConstraints(
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

function validateDraftEventHolidayOverlap(
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

function shouldTrimTrailingWeekend(ev: GeneratedCalendarEventDto) {
  return !ev.isHoliday && (!CRITICAL_EVENT_TYPES.has(ev.eventType) || isExamDraftEvent(ev));
}

function trimTrailingWeekendEnd(ev: GeneratedCalendarEventDto, startDate: string, endDate: string) {
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

const CATEGORY_LABELS: Record<string, string> = {
  exam: 'Sınav',
  registration: 'Kayıt',
  holiday: 'Tatil',
  deadline: 'Son Gün',
  result: 'İlan',
  application: 'Başvuru',
  academic: 'Akademik',
};

const CATEGORY_BADGE_VARIANTS: Record<CalendarEventCategory, CalendarEventBadgeVariant> = {
  exam: 'destructive',
  registration: 'info',
  holiday: 'success',
  deadline: 'warning',
  result: 'secondary',
  application: 'accent',
  academic: 'outline',
};

const CATEGORY_FILTER_ORDER: CalendarEventCategory[] = [
  'exam',
  'registration',
  'deadline',
  'application',
  'result',
  'holiday',
  'academic',
];

function eventCategoryLabel(eventType: string, isHoliday: boolean, title?: string | null, templateItemKey?: string | null) {
  if (isHoliday) return 'Tatil';
  const category = calendarEventCategory(eventType, title, templateItemKey);
  return CATEGORY_LABELS[category] ?? 'Akademik';
}

function eventCategoryValue(event: Pick<GeneratedCalendarEventDto, 'eventType' | 'title' | 'templateItemKey'>) {
  return calendarEventCategory(event.eventType, event.title, event.templateItemKey);
}

function semesterLabel(semester?: SemesterType | string | null) {
  return SEMESTERS.find(s => s.value === semester)?.label ?? semester ?? 'Genel';
}

function compareSemesters(a: SemesterType, b: SemesterType) {
  return SEMESTERS.findIndex(option => option.value === a) - SEMESTERS.findIndex(option => option.value === b);
}

function normalizeSemesters(semesters: SemesterType[]) {
  return Array.from(new Set(semesters)).sort(compareSemesters);
}

function isSemesterType(value: string): value is SemesterType {
  return SEMESTERS.some(option => option.value === value);
}

function termTypeToSemester(termType: string): SemesterType | null {
  if (termType === 'Fall' || termType === 'FallResit') return 'Fall';
  if (termType === 'Spring' || termType === 'SpringResit' || termType === 'SpringOther') return 'Spring';
  if (termType === 'Summer' || termType === 'SummerResit' || termType === 'SummerOther') return 'Summer';
  return null;
}

function semesterListText(semesters: SemesterType[]) {
  return semesters.length > 0 ? semesters.map(semester => semesterLabel(semester)).join(', ') : 'yok';
}

function parseConstraintParameters(parameterJson: string) {
  try {
    const parsed = JSON.parse(parameterJson);
    return parsed && typeof parsed === 'object' ? parsed as Record<string, unknown> : null;
  } catch {
    return null;
  }
}

function readString(value: unknown) {
  return typeof value === 'string' ? value : null;
}

function isLegacyShiftWarning(warning: CalendarWarningDto) {
  return isShiftWarning(warning)
    && !displayText(warning.message).toLocaleLowerCase('tr-TR').includes('yeni tarih:');
}

function isShiftWarning(warning: CalendarWarningDto) {
  return warning.code === 'HOLIDAY_SHIFT' || warning.code === 'PROHIBITION_SHIFT';
}

function warningBadge(warning: CalendarWarningDto) {
  if (isShiftWarning(warning) || warning.code === 'DOMINO' || warning.code === 'SINGLE_EVENT_UPDATE')
    return { label: 'Değiştirildi', variant: 'success' as const };

  if (warning.code === 'WHAT_IF')
    return { label: 'Önizleme', variant: 'info' as const };

  if (warning.severity === 'Error')
    return { label: 'Hata', variant: 'destructive' as const };

  if (warning.severity === 'Warning')
    return { label: 'Uyarı', variant: 'warning' as const };

  return { label: 'Bilgi', variant: 'secondary' as const };
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

function dayDifference(target: string, source: string) {
  const targetDate = parseUtcDisplayDate(target);
  const sourceDate = parseUtcDisplayDate(source);
  if (!targetDate || !sourceDate) return null;

  return Math.round((targetDate.getTime() - sourceDate.getTime()) / (24 * 60 * 60 * 1000));
}

function shiftDateValue(value: string, days: number) {
  const date = parseUtcDisplayDate(value);
  if (!date) return null;

  return formatUtcDateValue(addDays(date, days));
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
  const subject = joinedTitles(row.titles);
  const cause = row.titles.length === 1
    ? readableShiftReason(row.reason)
    : `tarihlerinin ${readableShiftReason(row.reason)}`;
  return `${row.semester} döneminde ${subject}, ${cause} nedeniyle ${row.newDate} tarihine kaydırıldı.`;
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
  const badge = warningBadge(warning);
  const row: ShiftWarningRow = {
    warning,
    message: '',
    detail: null,
    key,
    badgeLabel: badge.label,
    badgeVariant: badge.variant,
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
  return `${semesterLabel(event.semester)} döneminde ${displayText(event.title)}, ${reason} nedeniyle ${daySpan(event)} tarihine kaydırıldı.`;
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
        existing.detail = null;
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
    const badge = warningBadge(warning);
    if (seen.has(key)) return rows;
    seen.add(key);
    rows.push({ warning, message, detail, key, badgeLabel: badge.label, badgeVariant: badge.variant });
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
        {renderWarningsPanel(calendar)}

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

        {renderDraftMiniCalendar(current)}
        {renderTermSummaryCards(current)}
      </>
    );
  }

  function renderDraftMiniCalendar(current: CalendarView) {
    const defaultEvents = current.events;
    
    // Map to VisualizerEvent format
    const eventsToRender: VisualizerEvent[] = defaultEvents.map(e => {
      // Find if this event is modified in the what-if state
      const whatIfEvent = whatIf.data?.events.find(we => we.draftEventId === e.draftEventId);
      
      let isChangedPreview = false;
      let targetEvent = e;

      if (whatIfEvent && (dateInput(whatIfEvent.startDate) !== dateInput(e.startDate) || dateInput(whatIfEvent.endDate) !== dateInput(e.endDate))) {
        isChangedPreview = true;
        targetEvent = whatIfEvent; // Render the new dates
      }

      return {
        id: targetEvent.draftEventId,
        eventType: targetEvent.eventType,
        title: displayText(targetEvent.title),
        startDate: dateInput(targetEvent.startDate),
        endDate: dateInput(targetEvent.endDate) || dateInput(targetEvent.startDate),
        note: null,
        isChangedPreview,
        templateItemKey: targetEvent.templateItemKey,
        draggable: isEditableDraftEvent(e),
      };
    });

    if (eventsToRender.length === 0) return null;

    // Use the calendar's academic year to correctly anchor the year view range
    const calendarYearStart = academicYearStart(current.academicYear);

    return (
      <div className="rounded-lg border border-slate-200 bg-white p-3 shadow-sm">
        <h2 className="text-sm font-semibold text-slate-900 mb-3">Takvim Görünümü</h2>
        <CalendarVisualizer
          events={eventsToRender}
          compact={true}
          multiMonth={true}
          academicYearStart={calendarYearStart}
          onEventDrop={handleDraftEventDrop}
          getDropFeedback={getDraftDropFeedback}
          onInvalidEventDrop={handleInvalidDraftEventDrop}
          dragDisabled={updateDraftEvent.isPending}
        />
      </div>
    );
  }

  function renderTermSummaryCards(current: CalendarView) {
    const examDayCounts = buildExamDayCounts(current.events);
    const analyses = buildSemesterWeekAnalyses(current.events);
    const analysesBySemester = new Map(analyses.map(analysis => [analysis.semester, analysis]));

    return (
      <div className="rounded-lg border border-slate-200 bg-white p-3 shadow-sm">
        <h2 className="text-sm font-semibold text-slate-900 mb-3">Dönem Özeti</h2>
        <div className="overflow-x-auto rounded-md border border-slate-200">
          <table className="w-full text-left text-sm text-slate-600">
            <thead className="bg-slate-50 text-xs uppercase text-slate-500 border-b border-slate-200">
              <tr>
                <th className="px-3 py-2 font-medium">Dönem</th>
                <th className="px-3 py-2 font-medium">Tarihler</th>
                <th className="px-3 py-2 font-medium">Ders / Tatil / Sınav (Gün)</th>
                <th className="px-3 py-2 font-medium">Vize Öncesi</th>
                <th className="px-3 py-2 font-medium">Vize Haftası</th>
                <th className="px-3 py-2 font-medium">Vize Sonrası</th>
                <th className="px-3 py-2 font-medium">Final Haftası</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-200 bg-white">
              {current.termSummaries.map(summary => {
                const analysis = analysesBySemester.get(summary.semester);
                const isSummer = summary.semester === 'Summer';
                const examDays = examDayCounts[summary.semester] ?? summary.examDays;
                return (
                  <tr key={summary.semester} className="hover:bg-slate-50 transition-colors">
                    <td className="whitespace-nowrap px-3 py-2 font-medium text-slate-900">
                      {SEMESTERS.find(s => s.value === summary.semester)?.label}
                    </td>
                    <td className="whitespace-nowrap px-3 py-2">
                      {fmt(summary.termStart)} - {fmt(summary.termEnd)}
                    </td>
                    <td className="whitespace-nowrap px-3 py-2">
                      <span className="font-medium text-slate-700">{summary.teachingDays}</span> /{' '}
                      <span className="text-slate-500">{summary.holidayDaysInTerm}</span> /{' '}
                      <span className="text-red-600">{examDays}</span>
                    </td>
                    <td className="whitespace-nowrap px-3 py-2">
                      {isSummer || !analysis ? '-' : (
                        <span title={analysis.midtermStart ? `Vize Başlangıcı: ${fmt(utcDate(analysis.midtermStart))}` : 'Tarih yok'} className={analysis.midtermStart ? 'cursor-help border-b border-dotted border-slate-400' : ''}>
                          {analysis.weeksBeforeMidterm ?? '-'} hafta
                        </span>
                      )}
                    </td>
                    <td className="whitespace-nowrap px-3 py-2">
                      {isSummer || !analysis ? '-' : (
                        <span title={analysis.midtermStart && analysis.midtermEnd ? `${fmt(utcDate(analysis.midtermStart))} - ${fmt(utcDate(analysis.midtermEnd))}` : 'Tarih yok'} className={analysis.midtermStart ? 'cursor-help border-b border-dotted border-slate-400' : ''}>
                          {analysis.midtermWeek ? `${analysis.midtermWeek}. hafta` : '-'}
                        </span>
                      )}
                    </td>
                    <td className="whitespace-nowrap px-3 py-2">
                      {isSummer || !analysis ? '-' : (
                        <span title={analysis.finalStart ? `Final Başlangıcı: ${fmt(utcDate(analysis.finalStart))}` : 'Tarih yok'} className={analysis.finalStart ? 'cursor-help border-b border-dotted border-slate-400' : ''}>
                          {analysis.weeksAfterMidterm ?? '-'} hafta
                        </span>
                      )}
                    </td>
                    <td className="whitespace-nowrap px-3 py-2">
                      {isSummer || !analysis ? '-' : (
                        <span title={analysis.finalStart && analysis.finalEnd ? `${fmt(utcDate(analysis.finalStart))} - ${fmt(utcDate(analysis.finalEnd))}` : 'Tarih yok'} className={analysis.finalStart ? 'cursor-help border-b border-dotted border-slate-400' : ''}>
                          {analysis.finalWeek ? `${analysis.finalWeek}. hafta` : '-'}
                        </span>
                      )}
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      </div>
    );
  }

  function renderWarningsPanel(current: CalendarView) {
    const rows = warningRows(current);
    const failedEvents = current.events.filter(event => event.isFailed);
    return (
      <div className="rounded-lg border border-slate-200 bg-white p-3 shadow-sm">
        <h2 className="text-sm font-semibold text-slate-900">Uyarılar</h2>
        <div className="mt-2 space-y-1.5">
          {failedEvents.length > 0 && (
            <div className="rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-800">
              <div className="font-semibold">Program seçtiğiniz kısıtlardan dolayı oluşturulamadı.</div>
              <div className="mt-1 text-xs">
                Yerleşemeyen etkinlikler: {failedEvents.map(event => `${semesterLabel(event.semester)} - ${displayText(event.title)}`).join(', ')}
              </div>
            </div>
          )}
          {rows.length === 0 && failedEvents.length === 0 ? (
            <p className="text-sm text-slate-500">Uyarı yok.</p>
          ) : rows.map(row => (
            <div key={row.key} className="rounded-lg border border-slate-200 px-3 py-2 text-sm">
              <Badge variant={row.badgeVariant}>
                {row.badgeLabel}
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

  function renderHolidaysTab() {
    return (
      <div className="rounded-lg border border-slate-200 bg-white p-3 shadow-sm">
        <h2 className="text-sm font-semibold text-slate-900">Tatil tanımları</h2>
        <p className="text-xs text-slate-600">Sabit resmi tatiller, dini bayramlar ve bloklayıcı önemli günler.</p>
        <div className="mt-2 grid gap-2 md:grid-cols-2 xl:grid-cols-3">
          {holidays.isLoading ? (
            <p className="text-sm text-slate-500">Yükleniyor...</p>
          ) : visibleHolidayDefinitions.length === 0 ? (
            <p className="text-sm text-slate-500">Gösterilecek tatil tanımı yok.</p>
          ) : visibleHolidayDefinitions.map(holiday => (
            <div key={holiday.id} className="rounded-lg border border-slate-200 px-3 py-3 text-sm">
              <div className="flex items-center justify-between gap-2">
                <span className="font-semibold text-slate-800">{displayText(holiday.name)}</span>
                {holiday.type === 'HijriCalculated' && (
                  <Badge variant="default">Hicri hesaplandı</Badge>
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
            <TabsTrigger value="draft">Taslak & Zincirleme</TabsTrigger>
            <TabsTrigger value="holidays">Tatil Tanımları</TabsTrigger>
          </TabsList>
          <div className="flex flex-wrap items-center gap-2">
            <Button type="button" variant="outline" size="sm" onClick={() => setShowDraftHistory(true)}>
              Taslak geçmişi
            </Button>
          </div>
        </div>

        <Dialog open={!!errorNotice} onOpenChange={(open) => !open && setErrorNotice(null)}>
          <DialogContent className="z-[150] max-w-md border-red-200">
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

        {notice && (
          <div
            role="status"
            aria-live="polite"
            className={cn(
              'fixed right-4 top-4 z-[140] max-h-[40vh] w-[min(calc(100vw-2rem),32rem)] overflow-auto rounded-lg border px-4 py-3 text-sm shadow-xl',
              noticeTone === 'success' && 'border-emerald-200 bg-emerald-50 text-emerald-900',
              noticeTone === 'error' && 'border-red-200 bg-red-50 text-red-900',
              noticeTone === 'info' && 'border-slate-200 bg-white text-slate-700',
            )}
          >
            {notice}
          </div>
        )}

        <TabsContent value="generator">{renderGeneratorTab()}</TabsContent>
        <TabsContent value="draft">{renderDraftTab()}</TabsContent>
        <TabsContent value="holidays">{renderHolidaysTab()}</TabsContent>
      </Tabs>

      {renderDraftHistoryDialog()}

      {pendingConfirm && (
        <div className="fixed inset-0 z-[160] flex items-center justify-center bg-slate-950/50 px-4 py-6">
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

