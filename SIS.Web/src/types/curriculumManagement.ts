// Backend: SIS.Domain.Enums.CurriculumStatus
// 0 = Draft (Düzenlenebilir), 1 = Published (Yayında, kilitli), 2 = Archived (Geçmiş)
export type CurriculumStatusValue = 0 | 1 | 2;

export interface CurriculumListDto {
  id: number;
  programId: number;
  programName: string;
  programCode: string;
  facultyName: string;
  departmentName: string;
  version: string;
  name: string | null;
  startDate: string | null;
  endDate: string | null;
  isActive: boolean;
  status: CurriculumStatusValue;
  courseCount: number;
  totalEcts: number;
  totalCredit: number;
  compulsoryCount: number;
  electiveCount: number;
  activeStudentCount: number;
  totalStudentCount: number;
  createdAt: string;
}

export interface CurriculumDetailDto {
  id: number;
  programId: number;
  programName: string;
  version: string;
  isActive: boolean;
  name: string | null;
  shortName: string | null;
  englishName: string | null;
  startDate: string | null;
  endDate: string | null;
  decisionDate: string | null;
  decisionNo: string | null;
  decisionNote: string | null;
  gradingSystem: string | null;
  internshipDuration: number | null;
  description: string | null;
  // Müfredat-özel dönem AKTS tavanı override'ları (null = global varsayılan)
  ectsLimitBelow200: number | null;
  ectsLimit200To249: number | null;
  ectsLimitAbove250: number | null;
  courses: CurriculumCourseItemDto[];
}

export interface CurriculumCourseItemDto {
  courseId: number;
  courseCode: string;
  courseName: string;
  ects: number;
  theoryHours: number;
  labHours: number;
  suggestedSemester: number;
  isCompulsory: boolean;
  includeInGpa: boolean;
  showOnTranscript: boolean;
  categoryId: number | null;
  categoryName: string | null;
  openingReasonId: number | null;
  openingReasonCode: string | null;
  openingReasonName: string | null;
  nationalCredit: number;
  // Phase 2 — flags
  isMainGroupCourse: boolean;
  isFormationCourse: boolean;
  excludedFromCreditTotal: boolean;
  excludedFromTranscriptTotal: boolean;
  autoAddOnNewRegistration: boolean;
  showAtTakenCurriculum: boolean;
  showInstructorAtRegistration: boolean;
  hasCustomExamCurriculum: boolean;
  // Phase 2 — decision metadata
  decisionDate: string | null;
  decisionNo: string | null;
  decisionNote: string | null;
  // Phase 2 — curriculum-specific description
  curriculumDescription: string | null;
  // Phase 3 — İntibak (önceki ders)
  previousCourseId: number | null;
  previousCourseCode: string | null;
  previousCourseName: string | null;
}

export interface CreateCurriculumRequest {
  programId: number;
  version: string;
  name?: string;
  shortName?: string;
  englishName?: string;
  startDate?: string;
  endDate?: string;
  decisionDate?: string;
  decisionNo?: string;
  decisionNote?: string;
  gradingSystem?: string;
  internshipDuration?: number;
  description?: string;
}

export interface UpdateCurriculumRequest {
  version?: string;
  isActive?: boolean;
  name?: string;
  shortName?: string;
  englishName?: string;
  startDate?: string;
  endDate?: string;
  decisionDate?: string;
  decisionNo?: string;
  decisionNote?: string;
  gradingSystem?: string;
  internshipDuration?: number;
  description?: string;
  // AKTS override'ları: uygulanması için ectsLimitsProvided=true gönderilmeli.
  // true iken üçü de null → override'lar temizlenir (global fallback).
  ectsLimitBelow200?: number | null;
  ectsLimit200To249?: number | null;
  ectsLimitAbove250?: number | null;
  ectsLimitsProvided?: boolean;
}

export interface AddCourseRequest {
  courseId: number;
  suggestedSemester: number;
  isCompulsory: boolean;
  includeInGpa?: boolean;
  showOnTranscript?: boolean;
  categoryId?: number | null;
  openingReasonId?: number | null;
  // Phase 2 — flags
  isMainGroupCourse?: boolean;
  isFormationCourse?: boolean;
  excludedFromCreditTotal?: boolean;
  excludedFromTranscriptTotal?: boolean;
  autoAddOnNewRegistration?: boolean;
  showAtTakenCurriculum?: boolean;
  showInstructorAtRegistration?: boolean;
  hasCustomExamCurriculum?: boolean;
  // Phase 2 — decision metadata
  decisionDate?: string | null;
  decisionNo?: string | null;
  decisionNote?: string | null;
  // Phase 2 — curriculum-specific description
  curriculumDescription?: string | null;
  // Phase 3 — İntibak
  previousCourseId?: number | null;
}

export interface UpdateCourseRequest {
  suggestedSemester?: number;
  isCompulsory?: boolean;
  includeInGpa?: boolean;
  showOnTranscript?: boolean;
  categoryId?: number | null;
  openingReasonId?: number | null;
  clearCategory?: boolean;
  clearOpeningReason?: boolean;
  // Phase 2 — flags (null/undefined = no change)
  isMainGroupCourse?: boolean;
  isFormationCourse?: boolean;
  excludedFromCreditTotal?: boolean;
  excludedFromTranscriptTotal?: boolean;
  autoAddOnNewRegistration?: boolean;
  showAtTakenCurriculum?: boolean;
  showInstructorAtRegistration?: boolean;
  hasCustomExamCurriculum?: boolean;
  // Phase 2 — decision metadata
  decisionDate?: string | null;
  decisionNo?: string | null;
  decisionNote?: string | null;
  clearDecisionDate?: boolean;
  // Phase 2 — curriculum-specific description
  curriculumDescription?: string | null;
  clearCurriculumDescription?: boolean;
  // Phase 3 — İntibak (null/undefined = no change; clearPreviousCourse=true → null)
  previousCourseId?: number | null;
  clearPreviousCourse?: boolean;
}

export interface CopyCurriculumRequest {
  newVersion: string;
}

export interface CurriculumElectivePoolDto {
  id: number;
  electiveGroupId: number;
  groupName: string;
  groupCode: string;
  /** Faz E: ElectiveSubtypes.Id (deprecated electiveSubtype enum string kalktı) */
  electiveSubtypeId: number;
  /** Faz B B1: aynı grubun farklı slot'ları (örn. TE-1, TE-2) */
  slotName: string;
  suggestedSemesterStart: number;
  suggestedSemesterEnd: number;
  requiredCourseCount: number;
  requiredEcts: number;
  /** Faz B B1: havuz seviyesinde AKTS override (null → ders kataloğu AKTS) */
  ectsPerCourse: number | null;
  poolCourseCount: number;
}

export interface CreatePoolRequest {
  electiveGroupId: number;
  /** Faz B B2: zorunlu, müfredat içinde (group, slotName) unique */
  slotName: string;
  suggestedSemesterStart: number;
  suggestedSemesterEnd: number;
  requiredCourseCount: number;
  requiredEcts: number;
  /** Faz B B2: opsiyonel; null → ders kataloğu AKTS'i kullanılır */
  ectsPerCourse?: number;
}

export interface UpdatePoolRequest {
  slotName?: string;
  suggestedSemesterStart?: number;
  suggestedSemesterEnd?: number;
  requiredCourseCount?: number;
  requiredEcts?: number;
  ectsPerCourse?: number;
  /** Faz B B2: explicit clear flag (partial update + explicit null) */
  clearEctsPerCourse?: boolean;
}

/**
 * Faz B B5: Müfredat tablosunda zorunlular + grup slot'ları karışık satırlar.
 * Discriminated union — render tarafı `kind` üzerinden ayrıştırır.
 */
export type CurriculumRow =
  | { kind: 'course'; course: CurriculumCourseItemDto }
  | { kind: 'pool'; pool: CurriculumElectivePoolDto };
