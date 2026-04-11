export interface CurriculumListDto {
  id: number;
  programId: number;
  programName: string;
  programCode: string;
  version: string;
  name: string | null;
  startDate: string | null;
  endDate: string | null;
  isActive: boolean;
  courseCount: number;
  totalEcts: number;
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
