// Backend: SIS.Domain.Enums.CurriculumStatus
// 0 = Draft (Düzenlenebilir), 1 = Published (Yayında, kilitli), 2 = Archived (Geçmiş)
export type CurriculumStatusValue = 0 | 1 | 2;

export interface CurriculumListDto {
  id: number;
  programId: number;
  programName: string;
  programCode: string;
  facultyName: string;
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
  courseCategory: string | null;
  groupCode: string | null;
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
  courseCategory?: string;
  groupCode?: string;
}

export interface UpdateCourseRequest {
  suggestedSemester?: number;
  isCompulsory?: boolean;
  includeInGpa?: boolean;
  showOnTranscript?: boolean;
  courseCategory?: string;
  groupCode?: string;
}

export interface CopyCurriculumRequest {
  newVersion: string;
}

export interface CurriculumElectivePoolDto {
  id: number;
  electiveGroupId: number;
  groupName: string;
  groupCode: string;
  electiveSubtype: string;
  suggestedSemesterStart: number;
  suggestedSemesterEnd: number;
  requiredCourseCount: number;
  requiredEcts: number;
  poolCourseCount: number;
}

export interface CreatePoolRequest {
  electiveGroupId: number;
  suggestedSemesterStart: number;
  suggestedSemesterEnd: number;
  requiredCourseCount: number;
  requiredEcts: number;
}

export interface UpdatePoolRequest {
  suggestedSemesterStart?: number;
  suggestedSemesterEnd?: number;
  requiredCourseCount?: number;
  requiredEcts?: number;
}
