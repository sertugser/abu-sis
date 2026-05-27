import { displayTurkishText } from '@/lib/turkishText';
import type { CalendarEventBadgeVariant, CalendarEventCategory } from '@/lib/calendarEventTypes';
import type {
  CalendarDraftDto,
  CalendarWarningDto,
  GenerateCalendarResultDto,
  SemesterType,
} from '@/hooks/useCalendarGenerator';

export type CalendarView = GenerateCalendarResultDto | CalendarDraftDto;
export type ConfirmTone = 'default' | 'danger' | 'info';
export type NoticeTone = 'info' | 'success' | 'error';

export type WarningRow = {
  warning: CalendarWarningDto;
  message: string;
  detail: string | null;
  key: string;
  badgeLabel: string;
  badgeVariant: 'destructive' | 'warning' | 'secondary' | 'success' | 'info';
};

export type ShiftWarningRow = WarningRow & {
  titles: string[];
  semester: string;
  reason: string;
  shiftText: string;
  newDate: string;
};

export type ConfirmOptions = {
  title: string;
  message: string;
  confirmLabel: string;
  cancelLabel?: string;
  showCancel?: boolean;
  tone?: ConfirmTone;
};

export type PendingConfirm = ConfirmOptions & {
  resolve: (value: boolean) => void;
};

export type CalendarProcessOption = {
  key: string;
  templateItemKey: string;
  eventType: string;
  eventTypeLabel: string;
  title: string;
  defaultDurationDays: number;
  semester: SemesterType;
  chainCode: string;
};

export type ActiveGeneratorSetting = {
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

export type SemesterWeekAnalysis = {
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

export const NOTICE_AUTO_DISMISS_MS = 2000;

export const CRITICAL_EVENT_TYPES = new Set([
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

export const MONDAY_START_EVENT_TYPES = new Set([
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

export const FLEXIBLE_HOLIDAY_OVERLAP_MIN_DAYS = 15;

export const CATEGORY_LABELS: Record<string, string> = {
  exam: 'Sınav',
  registration: 'Kayıt',
  holiday: 'Tatil',
  deadline: 'Son Gün',
  result: 'İlan',
  application: 'Başvuru',
  academic: 'Akademik',
};

export const CATEGORY_BADGE_VARIANTS: Record<CalendarEventCategory, CalendarEventBadgeVariant> = {
  exam: 'destructive',
  registration: 'info',
  holiday: 'success',
  deadline: 'warning',
  result: 'secondary',
  application: 'accent',
  academic: 'outline',
};

export const CATEGORY_FILTER_ORDER: CalendarEventCategory[] = [
  'exam',
  'registration',
  'deadline',
  'application',
  'result',
  'holiday',
  'academic',
];

export function displayText(value?: string | null) {
  return displayTurkishText(value);
}
