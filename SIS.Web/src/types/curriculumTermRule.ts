// Backend: SIS.Domain.Enums.CurriculumClassYear
export type CurriculumClassYear = 0 | 1 | 2 | 3 | 4; // 0 = Hazırlık, 1-4 = Sınıf

// Backend: SIS.Domain.Enums.CurriculumTermHalf
export type CurriculumTermHalf = 0 | 1 | 2; // 0 = Güz, 1 = Bahar, 2 = Yaz

// Backend: SIS.Domain.Enums.CurriculumTermRuleType
export type CurriculumTermRuleType =
  | 1  // MinRequiredCredit
  | 2  // MinRequiredCourseCount
  | 3  // MinRequiredCourseHours
  | 4  // MaxAllowedCredit
  | 5  // MaxAllowedCourseHours
  | 6  // MaxAllowedEcts
  | 7  // MinGpaForUpperCourse
  | 8  // MaxUpperAllowedCredit
  | 9  // MaxUpperAllowedCourseCount
  | 10 // MaxUpperAllowedCourseHours
  | 11 // MaxUpperAllowedEcts
  | 12 // MinGpaForGradeImprovement
  | 13 // MaxGradeImprovementCredit
  | 14; // MinRequiredEcts

export interface CurriculumTermRuleDto {
  id: number;
  curriculumId: number;
  classYear: CurriculumClassYear;
  termHalf: CurriculumTermHalf;
  ruleType: CurriculumTermRuleType;
  value: number;
  createdAt: string;
  updatedAt: string | null;
}

export interface CreateTermRuleRequest {
  classYear: CurriculumClassYear;
  termHalf: CurriculumTermHalf;
  ruleType: CurriculumTermRuleType;
  value: number;
}

export interface UpdateTermRuleRequest {
  value: number;
}

// Etiketler (UI için)
export const CLASS_YEAR_LABELS: Record<CurriculumClassYear, string> = {
  0: 'Hazırlık',
  1: '1. Sınıf',
  2: '2. Sınıf',
  3: '3. Sınıf',
  4: '4. Sınıf',
};

export const TERM_HALF_LABELS: Record<CurriculumTermHalf, string> = {
  0: 'Güz',
  1: 'Bahar',
  2: 'Yaz',
};

export const RULE_TYPE_LABELS: Record<CurriculumTermRuleType, string> = {
  1: 'Alması Gereken En Az Kredi',
  2: 'Alması Gereken En Az Ders Sayısı',
  3: 'Alması Gereken En Az Ders Saati',
  4: 'Alabileceği En Fazla Kredi',
  5: 'Alabileceği En Fazla Ders Saati',
  6: 'Alabileceği En Fazla AKTS',
  7: 'Üstten Ders Alabilmesi İçin Gerekli En Az Ortalama',
  8: 'Üstten Alabileceği En Fazla Kredi',
  9: 'Üstten Alabileceği En Fazla Ders Sayısı',
  10: 'Üstten Alabileceği En Fazla Ders Saati',
  11: 'Üstten Alabileceği En Fazla AKTS',
  12: 'Not Yük. Amacıyla Ders Alabilmesi İçin En Az Ort.',
  13: 'Not Yükseltme için Alabileceği En Fazla Kredi',
  14: 'Alması Gereken En Az AKTS',
};

// Ortalama (GPA) içeren kural tipleri — değer 0-4.00 aralığında ondalıklı, diğerleri tam sayı
export const GPA_RULE_TYPES: readonly CurriculumTermRuleType[] = [7, 12] as const;

export const ALL_RULE_TYPES: readonly CurriculumTermRuleType[] = [
  1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14,
] as const;

export const ALL_CLASS_YEARS: readonly CurriculumClassYear[] = [0, 1, 2, 3, 4] as const;
export const ALL_TERM_HALVES: readonly CurriculumTermHalf[] = [0, 1, 2] as const;
