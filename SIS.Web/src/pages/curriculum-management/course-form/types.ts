// State carried across all form sections.
// Each parent dialog instantiates these and passes slices to sections.

export type CourseOpenTypeState = 'Standart' | 'Closed' | 'PendingApproval' | null;

export interface CourseBasicState {
  code: string;
  name: string;
  englishName: string;
  alternativeName: string;
  showInInfoPackage: boolean;
}

export interface CourseScheduleState {
  ects: number;
  nationalCredit: number;
  theoryHours: number;
  labHours: number;
  applicationHours: number | null;
  // Plan 10 — Derslik tipi tercihleri (opsiyonel; null = scheduler otomatik seçsin)
  theoryClassroomType: import('@/hooks/useAdminCourseCatalog').ClassroomTypeValue | null;
  labClassroomType: import('@/hooks/useAdminCourseCatalog').ClassroomTypeValue | null;
}

export interface CourseClassificationState {
  language: 'Turkish' | 'English' | 'French' | 'German' | 'Arabic';
  teachingMethod: 'FaceToFace' | 'Online' | 'Hybrid';
  examType: string | null; // 'auto' | 'autoSU' | 'manualSU' | 'manualScore' | null (null = default 'auto')
  isNonGpa: boolean;
  status: 'Active' | 'Passive';
  level: 'Undergraduate' | 'Graduate' | 'Doctorate';
  openType: CourseOpenTypeState;
}

// CurriculumCourse-specific (only used in CurriculumCourseEditDialog)
// Faz B B6: groupCode form alanı kaldırıldı.
// Faz D D2: backend AddCourseRequest.groupCode + entity GroupCode tamamen söküldü.
export interface CurriculumSpecificState {
  suggestedSemester: number;
  isCompulsory: boolean;
  includeInGpa: boolean;
  showOnTranscript: boolean;
  categoryId: number | null;
  openingReasonId: number | null;
  curriculumDescription: string;
}

export interface CourseDescriptionState {
  description: string;
}

// Phase 2 — CurriculumCourse-specific flag bundle
export interface CourseFlagsState {
  isMainGroupCourse: boolean;
  isFormationCourse: boolean;
  excludedFromCreditTotal: boolean;
  excludedFromTranscriptTotal: boolean;
  autoAddOnNewRegistration: boolean;
  showAtTakenCurriculum: boolean;
  showInstructorAtRegistration: boolean;
  hasCustomExamCurriculum: boolean;
}

// Phase 2 — CurriculumCourse-specific decision metadata (Senato kararı vb.)
export interface CourseDecisionState {
  decisionDate: string;     // YYYY-MM-DD format from <input type="date">; empty string = null
  decisionNo: string;
  decisionNote: string;
}

// Phase 3 — İntibak (Son Aktif Ders Kodu) — curriculum-specific predecessor pointer
export interface CourseIntibakState {
  previousCourseId: number | null;
  previousCourseCode: string | null;   // display-only, populated from backend
  previousCourseName: string | null;   // display-only, populated from backend
}

export type FieldOwnership = 'shared' | 'curriculum-specific';
