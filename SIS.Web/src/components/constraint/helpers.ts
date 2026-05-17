import type { ConstraintExceptionDto } from '../../types/constraint';

// ─── Category Labels ───────────────────────────────────────────
export const CATEGORY_LABELS: Record<string, string> = {
  Hard: 'Zorunlu',
  Soft: 'Tercihli',
};

// ─── Category Toggle Capability ─────────────────────────────────
// Plan 10 — her kuralın Hard/Soft toggle yapabilirliği:
//   - 'toggleable': Hem Hard hem Soft encoder var, admin değiştirebilir
//   - 'hard-only': Mantıken/fiziksel olarak Hard zorunlu, toggle anlamsız
//   - 'soft-only': Doğası soft (penalty-bazlı), Hard yapılamaz
export type CategoryCapability = 'toggleable' | 'hard-only' | 'soft-only';

export const CATEGORY_CAPABILITY: Record<string, CategoryCapability> = {
  // ── Zorunlu HARD (fizik/mantık kuralı) ────────────────
  'H1':   'hard-only',   // Aynı oda + saat = fizik
  'H2':   'hard-only',   // Aynı hoca + saat = fizik
  'H3':   'hard-only',   // Section ≤ classroom kapasitesi
  'H6':   'hard-only',   // Same-section component overlap = mantıksal
  'H11':  'hard-only',   // Eşkoşul = öğrenci ikisini de almalı
  'H17':  'hard-only',   // Slot mode kuralı (AllowedMode)

  // ── Toggleable (Hard ↔ Soft her ikisi mantıklı) ──────
  'H4':   'toggleable',  // Derslik tipi tercihi
  'H5':   'toggleable',  // Müfredat çakışması esnekliği
  'H8':   'toggleable',  // Aynı ders şube tercihi
  'H10':  'toggleable',  // Fakülte derslik esnekliği
  'H13':  'toggleable',  // Öğle arası tercihi
  'H15':  'toggleable',  // Ardışık limit tercihi
  'H16':  'toggleable',  // Theory-Lab sıra tercihi
  'H-WL': 'toggleable',  // Haftalık yük limit tercihi
  'H-DL': 'toggleable',  // Günlük yük limit tercihi
  'H-DO': 'toggleable',  // Boş gün tercihi
  // Plan 10 Phase 4: DIST per-row PreferenceLevel ile zaten Hard/Soft seçimi yapıyor;
  // üst-seviyede Category toggle anlamsız. IsEnabled toggle ile tüm DIST kapatılabilir.
  'DIST': 'hard-only',

  // ── Doğası SOFT (penalty-bazlı, Hard yapılamaz) ──────
  // Plan 10 Phase 2: S2 kaldırıldı, H-DL Category toggle ile yönetiliyor.
  'S1':   'soft-only',
  'S3':   'soft-only',
  'S4':   'soft-only',
  'S7':   'soft-only',
  'S8':   'soft-only',
  'S12':  'soft-only',
  'S20':  'soft-only',
};

export function getCategoryCapability(code: string): CategoryCapability {
  return CATEGORY_CAPABILITY[code] ?? 'hard-only';
}

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
    codes: ['H2', 'H-WL', 'H-DL', 'H-DO', 'H15', 'S8'],
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
    codes: ['H13', 'H17', 'S3', 'S7', 'S12', 'DIST'],
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
  // H17: form alanı yok — slot davranışı Ders Saatleri Yönetimi sayfasındaki AllowedMode'dan yönetiliyor.
  'H17': [],
  'H-DL': [
    { key: 'MaxDailyHours', label: 'Günlük Maks. Ders Saati', type: 'number', suffix: 'saat', min: 1, max: 16 },
  ],
  'H-WL': [],
  'H-DO': [],

  // ── Soft Constraints ──────────────────────────────────────────
  // Not: 'Weight' alanı kaldırıldı — slider üstte kart başlığında ayarlanıyor.
  // Parameter JSON key'leri backend SchedulingConstraintConfig property adlarıyla
  // birebir eşleşiyor (MergeParameterOverride reflect ile bind ediyor).
  'S1': [
    { key: 'S1MinOccupancy', label: 'Min. Doluluk Oranı', type: 'number', suffix: '%', min: 0, max: 100 },
    { key: 'S1MaxOccupancy', label: 'Maks. Doluluk Oranı', type: 'number', suffix: '%', min: 0, max: 100 },
  ],
  // Plan 10 Phase 2 — S2 kaldırıldı, H-DL Category toggle ile yönetiliyor.
  'S3': [
    { key: 'S3MaxSectionsPerSlot', label: 'Slot Başına Maks. Şube', type: 'number', min: 1, max: 50 }, // gitleaks:allow
  ],
  'S4': [
    { key: 'S4MinCoPairThreshold', label: 'Min. Birlikte Kayıt Eşiği', type: 'number', suffix: 'öğrenci', min: 1 }, // gitleaks:allow
  ],
  'S7': [
    { key: 'S7MaxGapSlots', label: 'Maks. Boşluk Slot Sayısı', type: 'number', min: 0, max: 10 }, // gitleaks:allow
  ],
  'S8': [],
  'S12': [
    { key: 'S12MaxImbalancePercent', label: 'Maks. Dengesizlik Oranı', type: 'number', suffix: '%', min: 0, max: 100 }, // gitleaks:allow
  ],
  'DIST': [],
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
