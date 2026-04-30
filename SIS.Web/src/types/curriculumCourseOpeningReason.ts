export interface CurriculumCourseOpeningReasonDto {
  id: number;
  code: string;
  name: string;
  description: string | null;
  sortOrder: number;
  isActive: boolean;
  // Behavior flags (Phase 4 will use; Phase 1 just exposes them for UI hints/badges)
  allowsAdvisorGrading: boolean;
  bypassScheduleConflict: boolean;
  bypassMandatoryAttendance: boolean;
  isExcludedFromGpa: boolean;
  isCreditFree: boolean;
  requiresApproval: boolean;
  allowsCustomGradingScale: boolean;
}
