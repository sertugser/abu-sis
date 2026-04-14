export interface ScheduleSlot {
  day: number;
  startTime: string;
  endTime: string;
  classroomId?: number;
  classroomName?: string;
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
}

export interface ClassroomOccupation {
  classroomName: string;
  courseCode: string;
  courseName: string;
  enrolledCount: number;
  capacity: number;
}

export interface ClassroomHeatmapCell {
  day: number;
  hour: number;
  occupancy: number;
  usedClassrooms: number;
  details: string;
  occupations: ClassroomOccupation[];
}

export interface ClassroomScheduleSlot {
  day: number;
  startHour: number;
  endHour: number;
  courseName: string;
  courseCode: string;
  instructorName: string;
}

export interface ClassroomInfo {
  id: number;
  name: string;
  capacity: number;
  type: string;
  buildingName: string;
  floor: number;
  facultyId: number;
  facultyName: string;
  schedules: ClassroomScheduleSlot[];
}

export interface ClassroomDashboard {
  totalClassrooms: number;
  occupancyRate: number;
  conflictsCount: number;
  emptySlots: number;
  heatmap: ClassroomHeatmapCell[];
  classrooms: ClassroomInfo[];
}

export interface ClassroomFacultyOption {
  id: number;
  name: string;
}

export interface ClassroomFilterOptions {
  faculties: ClassroomFacultyOption[];
  buildings: string[];
  classroomTypes: string[];
}

export interface AvailableClassroomResult {
  id: number;
  name: string;
  capacity: number;
  type: string;
  buildingName: string;
  floor: number;
  weeklyOccupancyRate: number;
  schedules: ClassroomScheduleSlot[];
}

export interface AvailableClassroomSearchParams {
  termId: number;
  days: number[];
  startHour: number;
  endHour: number;
  minCapacity?: number;
  classroomType?: string;
  facultyId?: number;
  buildingName?: string;
}
