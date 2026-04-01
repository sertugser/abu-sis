import type { ScheduleSlot, SchedulingConflict } from './scheduling';

export interface FacultyPlanningOverview {
  facultyId: number;
  facultyName: string;
  termId: number;
  termName: string;
  totalSections: number;
  scheduledSections: number;
  unscheduledSections: number;
  conflictCount: number;
  conflictCountByType: Record<string, number>;
  departmentProgress: DepartmentProgress[];
  pendingApprovalWarnings: PendingApprovalWarning[];
  recentAssignments: RecentAssignment[];
}

export interface DepartmentProgress {
  departmentId: number;
  departmentName: string;
  departmentCode: string;
  totalSections: number;
  scheduledSections: number;
  pendingApprovals: number;
}

export interface PendingApprovalWarning {
  departmentId: number;
  departmentName: string;
  pendingCount: number;
}

export interface RecentAssignment {
  sectionId: number;
  courseCode: string;
  courseName: string;
  classroomName: string | null;
  scheduleSummary: string | null;
  assignedByUserName: string;
  action: string;
  assignedAt: string;
}

export type SectionStatus = 'Unscheduled' | 'Scheduled' | 'Conflicted' | 'Partial';

export interface FacultyPlanningSection {
  sectionId: number;
  courseId: number;
  courseCode: string;
  courseName: string;
  departmentId: number;
  departmentCode: string;
  capacity: number;
  classroomId: number | null;
  classroomName: string | null;
  classroomType: string | null;
  instructorId: number | null;
  instructorName: string | null;
  scheduleSummary: string;
  status: SectionStatus;
  theoryHours: number;
  labHours: number;
  sectionIndex: number;
}

export interface FacultyPlanningSectionPage {
  items: FacultyPlanningSection[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface AssignSectionRequest {
  sectionId: number;
  classroomId?: number;
  scheduleSlots?: ScheduleSlot[];
  instructorId?: number;
}

export interface AssignmentResult {
  success: boolean;
  warnings: string[];
  conflicts: SchedulingConflict[];
}

export interface AutoScheduleRequest {
  facultyId: number;
  termId: number;
  departmentId?: number;
  sectionIds?: number[];
}

export interface AutoSchedulePreview {
  totalProcessed: number;
  successCount: number;
  failedCount: number;
  assignments: AutoSchedulePreviewItem[];
  failures: AutoScheduleFailure[];
}

export interface AutoSchedulePreviewItem {
  sectionId: number;
  courseCode: string;
  courseName: string;
  departmentCode: string;
  classroomId: number;
  classroomName: string;
  scheduleSlots: ScheduleSlot[];
  scheduleSummary: string;
  instructorId: number | null;
  instructorName: string | null;
  fitScore: number;
}

export interface AutoScheduleFailure {
  sectionId: number;
  courseCode: string;
  courseName: string;
  reason: string;
}

export interface ApplyScheduleRequest {
  assignments: ApplyScheduleItem[];
}

export interface ApplyScheduleItem {
  sectionId: number;
  classroomId: number;
  scheduleSlots: ScheduleSlot[];
  instructorId?: number;
}

export interface ApplyScheduleResult {
  successCount: number;
  failedCount: number;
  errors: string[];
}

export interface AvailableClassroom {
  classroomId: number;
  name: string;
  capacity: number;
  type: string;
  fitScore: number;
}
