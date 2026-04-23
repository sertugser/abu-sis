export type CalendarEventBadgeVariant =
  | 'destructive'
  | 'default'
  | 'success'
  | 'warning'
  | 'secondary'
  | 'outline'
  | 'info'
  | 'accent';

export type CalendarEventCategory =
  | 'exam'
  | 'registration'
  | 'holiday'
  | 'deadline'
  | 'result'
  | 'application'
  | 'academic';

export const CALENDAR_EVENT_LABELS: Record<string, string> = {
  CourseRegistration: 'Ders Kayıt',
  NewStudentRegistration: 'Yeni Öğrenci Ders Kayıt',
  AdvisorApproval: 'Danışman Onayı',
  AddDrop: 'Ders Ekle / Bırak',
  AddDropApproval: 'Ders Ekle / Bırak Onayı',
  CourseWithdrawal: 'Dersten Çekilme',
  AdvisorWithdrawal: 'Danışman Dersten Çekme',
  MidtermExam: 'Ara Sınav',
  MidtermGradeEntry: 'Ara Sınav Not Girişi',
  MidtermGradePublication: 'Ara Sınav Not İlanı',
  FinalExam: 'Dönem Sonu Sınavı',
  FinalGradeEntry: 'Final Not Girişi',
  FinalGradePublication: 'Final Not İlanı',
  MakeupExam: 'Bütünleme Sınavı',
  MakeupGradeEntry: 'Bütünleme Not Girişi',
  MakeupGradePublication: 'Bütünleme Not İlanı',
  NationalHoliday: 'Resmi Tatil',
  ReligiousHoliday: 'Dini Bayram',
  ClassesStart: 'Derslerin Başlangıcı',
  ClassesEnd: 'Derslerin Bitişi',
  TuitionPayment: 'Ücret Ödeme',
  GraduateApplication: 'Lisansüstü Başvuru',
  HorizontalTransfer: 'Yatay Geçiş',
  DoubleMajor: 'Çift Anadal / Yandal',
  Other: 'Diğer',
  ExcuseExamApplication: 'Mazeret Başvurusu',
  ExcuseExam: 'Mazeret Sınavı',
  ExcuseExamResult: 'Mazeret Sonucu',
  Orientation: 'Oryantasyon',
  SemesterBreak: 'Dönem Arası',
  CourseExemptionApplication: 'Ders Muafiyet Başvurusu',
  CourseExemptionResult: 'Ders Muafiyet Sonucu',
  ScholarshipApplication: 'Burs Başvurusu',
  ScholarshipResult: 'Burs Sonucu',
  ScholarshipRefund: 'Burs İadesi',
  PrepSchoolExam: 'Hazırlık Sınavı',
  RegistrationFreezeDeadline: 'Kayıt Dondurma Son Günü',
  SummerSchoolRegistration: 'Yaz Okulu Kaydı',
  SummerSchoolExam: 'Yaz Okulu Sınavı',
  SummerSchoolGradePublication: 'Yaz Okulu Not İlanı',
  SimurgApplication: 'Simurg Başvurusu',
  SimurgResult: 'Simurg Sonucu',
  InternationalStudentApplication: 'Uluslararası Öğrenci Başvurusu',
  TransferEvaluation: 'Yatay Geçiş Değerlendirme',
  TransferResult: 'Yatay Geçiş Sonucu',
  TransferRegistration: 'Yatay Geçiş Kaydı',
  GraduateResult: 'Lisansüstü Başvuru Sonucu',
  GraduateRegistration: 'Lisansüstü Kayıt',
  ImportantDay: 'Önemli Gün',
};

export const EVENT_TYPE_OPTIONS = Object.entries(CALENDAR_EVENT_LABELS).map(([value, label]) => ({
  value,
  label,
}));

export const EVENT_TYPE_DEFAULTS = CALENDAR_EVENT_LABELS;

export function calendarEventCategory(eventType: string): CalendarEventCategory {
  if (['MidtermExam', 'FinalExam', 'MakeupExam', 'ExcuseExam', 'PrepSchoolExam', 'SummerSchoolExam'].includes(eventType)) {
    return 'exam';
  }

  if ([
    'CourseRegistration',
    'NewStudentRegistration',
    'AddDrop',
    'AdvisorApproval',
    'AddDropApproval',
    'TransferRegistration',
    'GraduateRegistration',
    'SummerSchoolRegistration',
  ].includes(eventType)) {
    return 'registration';
  }

  if (['NationalHoliday', 'ReligiousHoliday', 'ImportantDay'].includes(eventType)) {
    return 'holiday';
  }

  if ([
    'CourseWithdrawal',
    'AdvisorWithdrawal',
    'RegistrationFreezeDeadline',
    'ExcuseExamApplication',
  ].includes(eventType)) {
    return 'deadline';
  }

  if ([
    'MidtermGradeEntry',
    'MidtermGradePublication',
    'FinalGradeEntry',
    'FinalGradePublication',
    'MakeupGradeEntry',
    'MakeupGradePublication',
    'ExcuseExamResult',
    'CourseExemptionResult',
    'ScholarshipResult',
    'SimurgResult',
    'TransferResult',
    'GraduateResult',
    'SummerSchoolGradePublication',
  ].includes(eventType)) {
    return 'result';
  }

  if ([
    'GraduateApplication',
    'HorizontalTransfer',
    'DoubleMajor',
    'CourseExemptionApplication',
    'ScholarshipApplication',
    'SimurgApplication',
    'InternationalStudentApplication',
    'TransferEvaluation',
  ].includes(eventType)) {
    return 'application';
  }

  return 'academic';
}

export function calendarEventBadgeVariant(eventType: string): CalendarEventBadgeVariant {
  switch (calendarEventCategory(eventType)) {
    case 'exam':
      return 'destructive';
    case 'registration':
      return 'info';
    case 'holiday':
      return 'success';
    case 'deadline':
      return 'warning';
    case 'result':
      return 'secondary';
    case 'application':
      return 'accent';
    default:
      return 'outline';
  }
}

export const CAT_STYLE: Record<CalendarEventCategory, { bg: string; border: string; text: string; label: string }> = {
  exam:         { bg: 'bg-red-100',    border: 'border-l-red-500',    text: 'text-red-800',    label: 'SINAV' },
  registration: { bg: 'bg-blue-100',   border: 'border-l-blue-500',   text: 'text-blue-800',   label: 'KAYIT' },
  holiday:      { bg: 'bg-green-100',  border: 'border-l-green-500',  text: 'text-green-800',  label: 'TATİL' },
  deadline:     { bg: 'bg-amber-100',  border: 'border-l-amber-500',  text: 'text-amber-800',  label: 'SON GÜN' },
  result:       { bg: 'bg-gray-100',   border: 'border-l-gray-400',   text: 'text-gray-700',   label: 'SONUÇ' },
  application:  { bg: 'bg-teal-100',   border: 'border-l-teal-500',   text: 'text-teal-800',   label: 'BAŞVURU' },
  academic:     { bg: 'bg-indigo-100', border: 'border-l-indigo-500', text: 'text-indigo-800', label: 'AKADEMİK' },
};
