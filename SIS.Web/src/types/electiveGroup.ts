// Faz E: ElectiveSubtype string union + ELECTIVE_SUBTYPE_LABELS/SHORT helper'ları kaldırıldı.
// Display: useElectiveSubtypeList + lib/electiveSubtypeUtils kullanılır (DB-driven name + Code-based color).

export interface ElectiveGroupListDto {
  id: number;
  name: string;
  code: string;
  electiveSubtypeId: number;
  facultyId: number | null;
  facultyName: string | null;
  departmentId: number | null;
  departmentName: string | null;
  description: string | null;
  isActive: boolean;
  courseCount: number;
  createdAt: string;
}

export interface ElectiveGroupDetailDto extends ElectiveGroupListDto {
  courses: ElectiveGroupCourseDto[];
  usages: ElectiveGroupUsageDto[];
}

export interface ElectiveGroupCourseDto {
  courseId: number;
  courseCode: string;
  courseName: string;
  ects: number;
}

/** Bu seçmeli grubun bağlı olduğu müfredat slotları —
 * "Bu havuz nerede kullanılıyor?" sorusuna cevap. */
export interface ElectiveGroupUsageDto {
  poolId: number;
  curriculumId: number;
  curriculumName: string;
  curriculumVersion: string;
  programId: number;
  programName: string;
  slotName: string;
  suggestedSemesterStart: number;
  suggestedSemesterEnd: number;
  requiredCourseCount: number;
  requiredEcts: number;
  ectsPerCourse: number | null;
}

export interface CreateElectiveGroupRequest {
  name: string;
  code: string;
  electiveSubtypeId: number;
  facultyId?: number;
  departmentId?: number;
  description?: string;
}

export interface UpdateElectiveGroupRequest {
  name?: string;
  code?: string;
  facultyId?: number;
  departmentId?: number;
  clearFaculty?: boolean;
  clearDepartment?: boolean;
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
