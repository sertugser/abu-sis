// ── Response Types ──

export interface OpenedCourseSectionDto {
  sectionId: number;
  sectionNumber: number;
  courseId: number;
  courseCode: string;
  courseName: string;
  theoryHours: number;
  labHours: number;
  credits: number;
  ects: number;
  suggestedSemester: number;
  capacity: number;
  internalQuota: number | null;
  externalQuota: number | null;
  enrolledCount: number;
  registeredCount: number;
  draftOrPendingCount: number;
  instructorId: number | null;
  instructorName: string | null;
  isActive: boolean;
  departmentId: number | null;
  departmentName: string | null;
}

export interface OpenedCourseListDto {
  departmentId: number;
  departmentName: string;
  termId: number;
  termName: string;
  totalSections: number;
  sections: OpenedCourseSectionDto[];
}

export interface CrossDepartmentDemandDto {
  departmentId: number;
  departmentName: string;
  programName: string;
  estimatedStudentCount: number;
}

export interface CurriculumCourseForOpeningDto {
  courseId: number;
  courseCode: string;
  courseName: string;
  theoryHours: number;
  labHours: number;
  ects: number;
  suggestedSemester: number;
  isCompulsory: boolean;
  alreadyOpened: boolean;
  existingSectionCount: number;
  predictedDemand?: number;
  retakeCount?: number;
  recommendedSectionCount?: number;
  recommendedCapacity?: number;
  crossDepartmentDemand?: CrossDepartmentDemandDto[];
  previousTermEnrollment?: number;
  canOpen?: boolean;
}

export interface InstructorOptionDto {
  instructorId: number;
  fullName: string;
}

// ── Request Types ──

export interface OpenCourseRequest {
  courseId: number;
  sectionCount: number;
  capacityPerSection: number;
  internalQuota?: number;
  externalQuota?: number;
  instructorId?: number;
  accessibleDepartmentIds?: number[];
}

export interface BulkOpenCoursesRequest {
  courses: OpenCourseRequest[];
}

export interface BulkOpenCoursesResultDto {
  totalCoursesOpened: number;
  totalSectionsCreated: number;
  errors: string[];
}

export interface UpdateOpenedSectionRequest {
  sectionId: number;
  instructorId?: number;
  capacity?: number;
  internalQuota?: number | null;
  externalQuota?: number | null;
}

// ── Bulk Assign ──

export interface BulkAssignInstructorRequest {
  assignments: { sectionId: number; instructorId: number }[];
}

export interface BulkAssignInstructorResultDto {
  successCount: number;
  failedCount: number;
  errors: string[];
}

// ── Term Transfer ──

export interface PreviousTermCourseDto {
  courseId: number;
  courseCode: string;
  courseName: string;
  ects: number;
  suggestedSemester: number;
  isCompulsory: boolean;
  sectionCount: number;
  totalCapacity: number;
  instructorName: string | null;
  instructorId: number | null;
  alreadyOpened: boolean;
}

export interface TransferFromTermRequest {
  departmentId: number;
  sourceTermId: number;
  targetTermId: number;
  courseIds: number[];
}

export interface TransferCourseResultItem {
  courseId: number;
  courseCode: string;
  courseName: string;
  success: boolean;
  error?: string;
  sectionsCreated: number;
}

export interface TransferFromTermResultDto {
  successCount: number;
  failedCount: number;
  totalSectionsCreated: number;
  results: TransferCourseResultItem[];
}

// ── Instructor Workload ──

export interface AssignedSectionDto {
  sectionId: number;
  courseCode: string;
  courseName: string;
  sectionNumber: number;
  weeklyHours: number;
  capacity: number;
  enrolledCount: number;
}

export interface InstructorWorkloadDto {
  instructorId: number;
  fullName: string;
  title: string;
  maxWeeklyHours: number;
  assignedWeeklyHours: number;
  remainingCapacity: number;
  assignedSections: AssignedSectionDto[];
}

// ── Instructor Workload ──

export interface AssignedSectionDto {
  sectionId: number;
  courseCode: string;
  courseName: string;
  sectionNumber: number;
  weeklyHours: number;
  capacity: number;
  enrolledCount: number;
}

export interface InstructorWorkloadDto {
  instructorId: number;
  fullName: string;
  title: string;
  maxWeeklyHours: number;
  assignedWeeklyHours: number;
  remainingCapacity: number;
  assignedSections: AssignedSectionDto[];
}
