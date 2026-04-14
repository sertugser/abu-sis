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
  CourseRegistration: 'Ders Kayit',
  NewStudentRegistration: 'Yeni Ogrenci Ders Kayit',
  AdvisorApproval: 'Danisman Onayi',
  AddDrop: 'Ders Ekle / Birak',
  AddDropApproval: 'Ders Ekle / Birak Onayi',
  CourseWithdrawal: 'Dersten Cekilme',
  AdvisorWithdrawal: 'Danisman Dersten Cekme',
  MidtermExam: 'Ara Sinav',
  MidtermGradeEntry: 'Ara Sinav Not Girisi',
  MidtermGradePublication: 'Ara Sinav Not Ilani',
  FinalExam: 'Donem Sonu Sinavi',
  FinalGradeEntry: 'Final Not Girisi',
  FinalGradePublication: 'Final Not Ilani',
  MakeupExam: 'Butunleme Sinavi',
  MakeupGradeEntry: 'Butunleme Not Girisi',
  MakeupGradePublication: 'Butunleme Not Ilani',
  NationalHoliday: 'Resmi Tatil',
  ReligiousHoliday: 'Dini Bayram',
  ClassesStart: 'Derslerin Baslangici',
  ClassesEnd: 'Derslerin Bitisi',
  TuitionPayment: 'Ucret Odeme',
  GraduateApplication: 'Lisansustu Basvuru',
  HorizontalTransfer: 'Yatay Gecis',
  DoubleMajor: 'Cift Anadal / Yandal',
  Other: 'Diger',
  ExcuseExamApplication: 'Mazeret Basvurusu',
  ExcuseExam: 'Mazeret Sinavi',
  ExcuseExamResult: 'Mazeret Sonucu',
  Orientation: 'Oryantasyon',
  SemesterBreak: 'Donem Arasi',
  CourseExemptionApplication: 'Ders Muafiyet Basvurusu',
  CourseExemptionResult: 'Ders Muafiyet Sonucu',
  ScholarshipApplication: 'Burs Basvurusu',
  ScholarshipResult: 'Burs Sonucu',
  ScholarshipRefund: 'Burs Iadesi',
  PrepSchoolExam: 'Hazirlik Sinavi',
  RegistrationFreezeDeadline: 'Kayit Dondurma Son Gunu',
  SummerSchoolRegistration: 'Yaz Okulu Kaydi',
  SummerSchoolExam: 'Yaz Okulu Sinavi',
  SummerSchoolGradePublication: 'Yaz Okulu Not Ilani',
  SimurgApplication: 'Simurg Basvurusu',
  SimurgResult: 'Simurg Sonucu',
  InternationalStudentApplication: 'Uluslararasi Ogrenci Basvurusu',
  TransferEvaluation: 'Yatay Gecis Degerlendirme',
  TransferResult: 'Yatay Gecis Sonucu',
  TransferRegistration: 'Yatay Gecis Kaydi',
  GraduateResult: 'Lisansustu Basvuru Sonucu',
  GraduateRegistration: 'Lisansustu Kayit',
  ImportantDay: 'Onemli Gun',
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
