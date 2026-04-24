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
  isPinned: boolean;
  suggestedSemester: number;
}

export interface FacultyPlanningSectionPage {
  items: FacultyPlanningSection[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface FacultyPlanningTimeSlot {
  day: number;
  slotOrder: number;
  startTime: string;
  endTime: string;
  displayName: string;
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
  invalidatedAssignments: InvalidatedAssignment[];
  seed: number;
  totalPenalty: number;
  stats: AutoScheduleStats;
}

export interface AutoScheduleStats {
  constructionPlaced: number;
  constructionFailed: number;
  saIterations: number;
  saInitialPenalty: number;
  saFinalPenalty: number;
  elapsedSeconds: number;
}

export interface AutoSchedulePreviewItem {
  sectionId: number;
  sectionNumber: number;
  courseCode: string;
  courseName: string;
  departmentCode: string;
  classroomId: number | null;
  classroomName: string;
  scheduleSlots: ScheduleSlot[];
  scheduleSummary: string;
  instructorId: number | null;
  instructorName: string | null;
  fitScore: number;
}

export interface InvalidatedAssignment {
  sectionId: number;
  sectionNumber: number;
  courseCode: string;
  courseName: string;
  reason: string;
}

export interface AutoScheduleFailure {
  sectionId: number;
  sectionNumber: number;
  capacity: number;
  courseCode: string;
  courseName: string;
  reason: string;
  suggestions: FailureSuggestion[];
}

export interface FailureSuggestion {
  constraintCode: string;
  message: string;
  suggestionType: string;
  priority: number;
}

export interface ApplyScheduleRequest {
  assignments: ApplyScheduleItem[];
}

export interface ApplyScheduleItem {
  sectionId: number;
  classroomId: number | null;
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

export interface ScheduleRunSummary {
  id: number;
  seed: number;
  totalProcessed: number;
  successCount: number;
  failedCount: number;
  totalPenalty: number;
  elapsedSeconds: number;
  createdAt: string;
}

export interface ScheduleRunDetail {
  id: number;
  seed: number;
  createdAt: string;
  result: AutoSchedulePreview;
}
