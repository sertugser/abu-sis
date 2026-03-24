export interface ScheduleSlot {
  day: number;
  startTime: string;
  endTime: string;
}

export interface InstructorAvailability {
  id: number;
  instructorId: number;
  dayOfWeek: number;
  startTime: string;
  endTime: string;
  isAvailable: boolean;
  isPreferred: boolean;
}

export interface UpsertInstructorAvailabilityRequest {
  dayOfWeek: number;
  startTime: string;
  endTime: string;
  isAvailable: boolean;
  isPreferred: boolean;
}

export interface ApplyScheduleRequest {
  sectionId: number;
  classroomId: number | null;
  scheduleSlots: ScheduleSlot[];
}

export interface SchedulingConflict {
  conflictType: string;
  sectionId: number;
  relatedSectionId: number | null;
  courseCode: string;
  courseName: string;
  instructorId: number | null;
  instructorName: string;
  classroomId: number | null;
  classroomName: string;
  day: number | null;
  startTime: string | null;
  endTime: string | null;
  message: string;
}

export interface ScheduleValidationResult {
  isValid: boolean;
  conflicts: SchedulingConflict[];
  notes: string[];
}

export interface BatchAutoScheduleRequest {
  termId?: number | null;
  departmentId?: number | null;
}

export interface BatchAutoScheduleResult {
  totalSections: number;
  scheduledCount: number;
  failedCount: number;
  skippedCount: number;
  results: BatchAutoScheduleItem[];
  notes: string[];
}

export interface BatchAutoScheduleItem {
  sectionId: number;
  courseCode: string;
  courseName: string;
  success: boolean;
  classroomName: string | null;
  slots: ScheduleSlot[] | null;
  failureReason: string | null;
}

export interface ScheduleSuggestion {
  isFeasible: boolean;
  sectionId: number;
  instructorId: number | null;
  instructorName: string;
  suggestedClassroomId: number | null;
  suggestedClassroomName: string;
  suggestedSlots: ScheduleSlot[];
  estimatedWeeklyHoursImpact: number;
  notes: string[];
  instructorAvailabilities: InstructorAvailability[];
}
