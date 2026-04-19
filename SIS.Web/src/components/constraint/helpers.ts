import type { ConstraintExceptionDto } from '../../types/constraint';

// ─── Category Labels ───────────────────────────────────────────
export const CATEGORY_LABELS: Record<string, string> = {
  Hard: 'Zorunlu',
  Soft: 'Tercihli',
};

export const SCOPE_LABELS: Record<string, string> = {
  Schedule: 'Ders Programı',
  Scheduling: 'Ders Programı',
  Registration: 'Kayıt',
  Section: 'Şube',
  Instructor: 'Öğretim Üyesi',
  Classroom: 'Derslik',
};

// ─── Constraint Groups ─────────────────────────────────────────
export interface ConstraintGroup {
  key: string;
  label: string;
  icon: 'Building2' | 'GraduationCap' | 'BookOpen' | 'Clock';
  codes: string[];
}

export const CONSTRAINT_GROUPS: ConstraintGroup[] = [
  {
    key: 'classroom',
    label: 'Derslik Kısıtlamaları',
    icon: 'Building2',
    codes: ['H1', 'H3', 'H4', 'H10', 'S1'],
  },
  {
    key: 'instructor',
    label: 'Öğretim Üyesi Kısıtlamaları',
    icon: 'GraduationCap',
    codes: ['H2', 'H-WL', 'H-DL', 'H-DO', 'H15', 'S2', 'S8'],
  },
  {
    key: 'curriculum',
    label: 'Müfredat & Ders Kısıtlamaları',
    icon: 'BookOpen',
    codes: ['H5', 'H6', 'H8', 'H11', 'H12', 'H16', 'S4'],
  },
  {
    key: 'time',
    label: 'Zaman & Dağılım Kısıtlamaları',
    icon: 'Clock',
    codes: ['H13', 'S3', 'S5', 'S7', 'S9', 'S12', 'DIST'],
  },
];

// ─── Parameter Form Schemas ────────────────────────────────────
export interface ParameterFieldOption {
  value: string;
  label: string;
}

export interface ParameterField {
  key: string;
  label: string;
  type: 'number' | 'time' | 'boolean' | 'select';
  suffix?: string;
  min?: number;
  max?: number;
  options?: ParameterFieldOption[];
}

export const PARAMETER_SCHEMAS: Record<string, ParameterField[]> = {
  // ── Hard Constraints ──────────────────────────────────────────
  'H1': [],
  'H2': [],
  'H3': [],
  'H4': [
    { key: 'AmphiThreshold', label: 'Amfi Eşik Kontenjanı', type: 'number', min: 50, max: 500 },
  ],
  'H5': [
    { key: 'CurriculumConflictMode', label: 'Çakışma Modu', type: 'select', options: [
      { value: 'curriculum', label: 'Müfredat Bazlı' },
      { value: 'enrollment', label: 'Kayıt Bazlı' },
    ]},
    { key: 'EnrollmentConflictHardThreshold', label: 'Kayıt Çakışma Eşiği', type: 'number', min: 1, max: 100 },
  ],
  'H6': [],
  'H8': [
    { key: 'AllowSameCourseSectionOverlap', label: 'Aynı Ders Şube Çakışmasına İzin Ver', type: 'boolean' },
  ],
  'H10': [],
  'H11': [],
  'H12': [],
  'H13': [
    { key: 'LunchBreakStart', label: 'Öğle Arası Başlangıç', type: 'time' },
    { key: 'LunchBreakEnd', label: 'Öğle Arası Bitiş', type: 'time' },
  ],
  'H15': [
    { key: 'MaxConsecutiveHours', label: 'Maks. Ardışık Ders Saati', type: 'number', suffix: 'saat', min: 1, max: 12 },
  ],
  'H16': [
    { key: 'TheoryLabSplitThreshold', label: 'Teori-Lab Ayrım Eşiği (saat)', type: 'number', min: 2, max: 10 },
  ],
  'H-DL': [
    { key: 'MaxDailyHours', label: 'Günlük Maks. Ders Saati', type: 'number', suffix: 'saat', min: 1, max: 16 },
  ],
  'H-WL': [],
  'H-DO': [],

  // ── Soft Constraints ──────────────────────────────────────────
  'S1': [
    { key: 'MinOccupancy', label: 'Min. Doluluk Oranı', type: 'number', suffix: '%', min: 0, max: 100 },
    { key: 'MaxOccupancy', label: 'Maks. Doluluk Oranı', type: 'number', suffix: '%', min: 0, max: 100 },
    { key: 'Weight', label: 'Ağırlık Puanı', type: 'number', min: 1, max: 10 },
  ],
  'S2': [
    { key: 'MaxDailyHours', label: 'Günlük Maks. Ders Saati', type: 'number', suffix: 'saat', min: 1, max: 16 },
    { key: 'Weight', label: 'Ağırlık Puanı', type: 'number', min: 1, max: 10 },
  ],
  'S3': [
    { key: 'MaxSectionsPerSlot', label: 'Slot Başına Maks. Şube', type: 'number', min: 1, max: 50 },
    { key: 'Weight', label: 'Ağırlık Puanı', type: 'number', min: 1, max: 10 },
  ],
  'S4': [
    { key: 'MinCoPairThreshold', label: 'Min. Birlikte Kayıt Eşiği', type: 'number', suffix: 'öğrenci', min: 1 },
    { key: 'Weight', label: 'Ağırlık Puanı', type: 'number', min: 1, max: 10 },
  ],
  'S5': [
    { key: 'Weight', label: 'Ağırlık Puanı', type: 'number', min: 1, max: 10 },
  ],
  'S7': [
    { key: 'MaxGapSlots', label: 'Maks. Boşluk Slot Sayısı', type: 'number', min: 0, max: 10 },
    { key: 'Weight', label: 'Ağırlık Puanı', type: 'number', min: 1, max: 10 },
  ],
  'S8': [
    { key: 'Weight', label: 'Ağırlık Puanı', type: 'number', min: 1, max: 10 },
  ],
  'S9': [
    { key: 'Weight', label: 'Ağırlık Puanı', type: 'number', min: 1, max: 10 },
  ],
  'S12': [
    { key: 'MaxImbalancePercent', label: 'Maks. Dengesizlik Oranı', type: 'number', suffix: '%', min: 0, max: 100 },
    { key: 'Weight', label: 'Ağırlık Puanı', type: 'number', min: 1, max: 10 },
  ],
  'DIST': [
    { key: 'Weight', label: 'Ağırlık Puanı', type: 'number', min: 1, max: 10 },
  ],
};

// ─── Entity Type Labels ────────────────────────────────────────
export const ENTITY_TYPE_LABELS: Record<string, string> = {
  instructor: 'Öğretim Üyesi',
  student: 'Öğrenci',
  department: 'Bölüm',
  courseSection: 'Ders Şubesi',
  term: 'Dönem',
};

// ─── Utilities ─────────────────────────────────────────────────
export function getConstraintErrorMessage(error: unknown, fallback: string): string {
  if (
    typeof error === 'object' && error !== null && 'response' in error
    && typeof (error as { response?: { data?: { error?: unknown } } }).response?.data?.error === 'string'
  ) {
    return (error as { response: { data: { error: string } } }).response.data.error;
  }
  if (error instanceof Error && error.message) return error.message;
  return fallback;
}

export function exceptionEntityInfo(ex: ConstraintExceptionDto): string {
  if (ex.instructorName) return `Öğretim Üyesi: ${ex.instructorName} (#${ex.instructorId})`;
  if (ex.studentName) return `Öğrenci: ${ex.studentName} (#${ex.studentId})`;
  if (ex.departmentName) return `Bölüm: ${ex.departmentName} (#${ex.departmentId})`;
  if (ex.courseSectionInfo) return `Şube: ${ex.courseSectionInfo} (#${ex.courseSectionId})`;
  if (ex.termName) return `Dönem: ${ex.termName} (#${ex.termId})`;
  return 'Bilinmeyen';
}
