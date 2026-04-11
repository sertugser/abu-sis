export type ElectiveSubtype = 'UniversityElective' | 'NonDepartmentElective' | 'TechnicalElective';

export interface ElectiveGroupListDto {
  id: number;
  name: string;
  code: string;
  electiveSubtype: ElectiveSubtype;
  facultyId: number | null;
  facultyName: string | null;
  description: string | null;
  isActive: boolean;
  courseCount: number;
  createdAt: string;
}

export interface ElectiveGroupDetailDto extends ElectiveGroupListDto {
  courses: ElectiveGroupCourseDto[];
}

export interface ElectiveGroupCourseDto {
  courseId: number;
  courseCode: string;
  courseName: string;
  ects: number;
}

export interface CreateElectiveGroupRequest {
  name: string;
  code: string;
  electiveSubtype: ElectiveSubtype;
  facultyId?: number;
  description?: string;
}

export interface UpdateElectiveGroupRequest {
  name?: string;
  code?: string;
  facultyId?: number;
  description?: string;
  isActive?: boolean;
}

export interface AddCoursesToGroupRequest {
  courseIds: number[];
}

export interface CourseAccessRuleDto {
  id: number;
  courseId: number;
  allowedFacultyId: number | null;
  allowedFacultyName: string | null;
  allowedDepartmentId: number | null;
  allowedDepartmentName: string | null;
  allowedProgramId: number | null;
  allowedProgramName: string | null;
  minClassYear: number | null;
  maxClassYear: number | null;
  minCgpa: number | null;
  requiresAdvisorApproval: boolean;
  isOpenToOwnDepartment: boolean;
}

export interface CreateAccessRuleRequest {
  allowedFacultyId?: number;
  allowedDepartmentId?: number;
  allowedProgramId?: number;
  minClassYear?: number;
  maxClassYear?: number;
  minCgpa?: number;
  requiresAdvisorApproval?: boolean;
  isOpenToOwnDepartment?: boolean;
}

export const ELECTIVE_SUBTYPE_LABELS: Record<ElectiveSubtype, string> = {
  UniversityElective: 'Üniversite Seçmeli (OC)',
  NonDepartmentElective: 'Bölüm Dışı Seçmeli (NDE)',
  TechnicalElective: 'Teknik Seçmeli (TE)',
};

export const ELECTIVE_SUBTYPE_SHORT: Record<ElectiveSubtype, string> = {
  UniversityElective: 'OC',
  NonDepartmentElective: 'NDE',
  TechnicalElective: 'TE',
};
