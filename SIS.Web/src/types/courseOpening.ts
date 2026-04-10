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
  enrolledCount: number;
  registeredCount: number;
  draftOrPendingCount: number;
  instructorId: number | null;
  instructorName: string | null;
  isActive: boolean;
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

export type SectionApprovalStatus = 'Pending' | 'Approved' | 'Rejected';
export type ApprovalWorkflowStatus = SectionApprovalStatus | 'PartiallyOpened' | 'Completed';

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
  approvalStatus?: SectionApprovalStatus | null;
  approvalWorkflowStatus?: ApprovalWorkflowStatus;
  approvedSectionCount?: number | null;
  openedSectionCount?: number;
  remainingApprovedSectionCount?: number;
  canOpen?: boolean;
  openingBlockedReason?: string | null;
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
