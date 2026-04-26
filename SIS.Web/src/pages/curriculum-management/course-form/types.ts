// State carried across all form sections.
// Each parent dialog instantiates these and passes slices to sections.

export interface CourseBasicState {
  code: string;
  name: string;
  englishName: string;
}

export interface CourseScheduleState {
  ects: number;
  nationalCredit: number | null;
  theoryHours: number;
  labHours: number;
}

export interface CourseClassificationState {
  language: 'Turkish' | 'English' | 'French' | 'German' | 'Arabic';
  teachingMethod: 'FaceToFace' | 'Online' | 'Hybrid';
  examType: string | null; // 'auto' | 'autoSU' | 'manualSU' | 'manualScore' | null (null = default 'auto')
  isNonGpa: boolean;
  status: 'Active' | 'Passive';
  level: 'Undergraduate' | 'Graduate' | 'Doctorate';
}

// CurriculumCourse-specific (only used in CurriculumCourseEditDialog)
export interface CurriculumSpecificState {
  suggestedSemester: number;
  isCompulsory: boolean;
  includeInGpa: boolean;
  showOnTranscript: boolean;
  categoryId: number | null;
  openingReasonId: number | null;
  groupCode: string;
}

export interface CourseDescriptionState {
  description: string;
}

export type FieldOwnership = 'shared' | 'curriculum-specific';
