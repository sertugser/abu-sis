import type { ScheduleValidationResult, SchedulingConflict } from './scheduling';

export interface AccessibleDepartment {
  departmentId: number;
  departmentName: string;
  departmentCode: string;
}

export interface AcademicOperationsContext {
  roleLabel: string;
  canViewAllDepartments: boolean;
  defaultDepartmentId: number | null;
  accessibleDepartments: AccessibleDepartment[];
  notes: string[];
}

export interface DemandBreakdown {
  retakeFF: number;
  retakeNA: number;
  retakeU: number;
  regularOnSchedule: number;
  behindSchedule: number;
  upperCourse: number;
  ectsLimited: number;
  totalRetake: number;
  totalEligible: number;
}

export interface HistoricalEnrollment {
  termId: number;
  termName: string;
  year: number;
  actualEnrollment: number;
  sectionCount: number;
}

export interface ConfidenceRange {
  lowerBound: number;
  upperBound: number;
  margin: number;
  method: string;
}

export interface SectionDistribution {
  recommendedSections: number;
  effectiveCapacityPerSection: number;
  totalCapacity: number;
  utilizationRate: number;
  surplusCapacity: number;
  studentsPerSection: number[];
  warnings: string[];
}

export interface CourseDemandForecast {
  courseId: number;
  courseCode: string;
  courseName: string;
  predictedEligibleCount: number;
  predictedRetakeCount: number;
  predictedOverloadOrUpperClassCount: number;
  totalPredictedDemand: number;
  recommendedSectionCount: number;
  capacityBasis: string;
  notes: string[];
  demandBreakdown: DemandBreakdown;
  historicalEnrollments: HistoricalEnrollment[];
  confidence: ConfidenceRange;
  sectionDistribution: SectionDistribution;
}

export interface DepartmentCapacitySummary {
  totalCurrentSections: number;
  totalRecommendedSections: number;
  sectionDelta: number;
  coursesNeedingMoreSections: number;
  coursesWithExcessCapacity: number;
}

export interface DepartmentForecastSummary {
  departmentId: number;
  departmentName: string;
  totalCoursesAnalyzed: number;
  totalPredictedDemand: number;
  totalRecommendedSections: number;
  courseSummaries: CourseDemandForecast[];
  notes: string[];
  capacitySummary: DepartmentCapacitySummary;
}

export interface AcademicPlanningSummary {
  departmentId: number;
  departmentName: string;
  termId: number;
  termName: string;
  forecastSummary: DepartmentForecastSummary | null;
  scheduleValidation: ScheduleValidationResult;
  notes: string[];
}

export interface DepartmentSectionStudent {
  studentId: number;
  studentNumber: string;
  studentName: string;
  email: string;
  status: string;
  grade: string | null;
}

export interface DepartmentSectionClassList {
  sectionId: number;
  courseId: number;
  courseCode: string;
  courseName: string;
  instructorName: string;
  classroomName: string;
  scheduleSummary: string;
  scheduleJson: string;
  capacity: number;
  enrolledCount: number;
  activeStudentCount: number;
  isActive: boolean;
  students: DepartmentSectionStudent[];
}

export interface DepartmentClassList {
  departmentId: number;
  departmentName: string;
  termId: number;
  termName: string;
  totalSections: number;
  totalStudents: number;
  sections: DepartmentSectionClassList[];
  notes: string[];
}

export interface CurriculumSemesterOverview {
  semesterNumber: number;
  label: string;
  courseCount: number;
  credits: number;
  ects: number;
  compulsoryCourseCount: number;
  electiveCourseCount: number;
}

export interface ProgramCurriculumOverview {
  programId: number;
  programName: string;
  programCode: string;
  curriculumId: number;
  curriculumVersion: string;
  isActiveCurriculum: boolean;
  totalCourses: number;
  totalCredits: number;
  totalEcts: number;
  compulsoryCourseCount: number;
  electiveCourseCount: number;
  semesterSummaries: CurriculumSemesterOverview[];
}

export interface DepartmentCurriculumOverview {
  departmentId: number;
  departmentName: string;
  programCount: number;
  activeCurriculumCount: number;
  totalCourses: number;
  totalEcts: number;
  programs: ProgramCurriculumOverview[];
  notes: string[];
}

export interface LevelDistributionDto {
  level: number;
  label: string;
  count: number;
  percentage: number;
}

export interface BulkAcademicLevelDto {
  totalStudents: number;
  discrepancyCount: number;
  officialDistribution: LevelDistributionDto[];
  academicDistribution: LevelDistributionDto[];
}

export type PlanningConflict = SchedulingConflict;

// Section Approval types
export type SectionApprovalStatus = 'Pending' | 'Approved' | 'Rejected';

export interface CourseSectionApproval {
  id: number;
  courseId: number;
  courseCode: string;
  courseName: string;
  recommendedSectionCount: number;
  approvedSectionCount: number | null;
  predictedDemand: number;
  status: SectionApprovalStatus;
  note: string | null;
  decidedByUserName: string | null;
  createdAt: string;
  decidedAt: string | null;
}

export interface ApproveCourseSectionsRequest {
  courseId: number;
  sectionCount: number;
  note?: string;
}

export interface RejectCourseSectionsRequest {
  courseId: number;
  note?: string;
}

export interface BulkApprovalRequest {
  approvals: ApproveCourseSectionsRequest[];
}

export interface BulkApprovalResult {
  approvedCount: number;
  sectionsCreated: number;
  errors: string[];
}
