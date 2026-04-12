export interface DemandBreakdownDto {
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

export interface HistoricalEnrollmentDto {
  termId: number;
  termName: string;
  year: number;
  actualEnrollment: number;
  sectionCount: number;
}

export interface ConfidenceRangeDto {
  lowerBound: number;
  upperBound: number;
  margin: number;
  method: string;
}

export interface SectionDistributionDto {
  recommendedSections: number;
  effectiveCapacityPerSection: number;
  totalCapacity: number;
  utilizationRate: number;
  surplusCapacity: number;
  studentsPerSection: number[];
  warnings: string[];
}

export interface CrossDepartmentDemandDto {
  departmentId: number;
  departmentName: string;
  programName: string;
  estimatedStudentCount: number;
}

export interface CourseDemandForecastDto {
  courseId: number;
  courseCode: string;
  courseName: string;
  predictedEligibleCount: number;
  predictedRetakeCount: number;
  predictedOverloadOrUpperClassCount: number;
  totalPredictedDemand: number;
  recommendedSectionCount: number;
  capacityBasis: string;
  comparableTermEnrollment: number | null;
  notes: string[];
  demandBreakdown: DemandBreakdownDto;
  historicalEnrollments: HistoricalEnrollmentDto[];
  confidence: ConfidenceRangeDto;
  sectionDistribution: SectionDistributionDto;
  crossDepartmentDemand: CrossDepartmentDemandDto[];
  showRate: number;
}

export interface DepartmentCapacitySummaryDto {
  totalCurrentSections: number;
  totalRecommendedSections: number;
  sectionDelta: number;
  coursesNeedingMoreSections: number;
  coursesWithExcessCapacity: number;
}

export interface DepartmentForecastSummaryDto {
  departmentId: number;
  departmentName: string;
  totalCoursesAnalyzed: number;
  totalPredictedDemand: number;
  totalRecommendedSections: number;
  courseSummaries: CourseDemandForecastDto[];
  notes: string[];
  capacitySummary: DepartmentCapacitySummaryDto;
}
