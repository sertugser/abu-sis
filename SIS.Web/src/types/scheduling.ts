export interface ScheduleSlot {
  day: number;
  startTime: string;
  endTime: string;
  classroomId?: number;
  classroomName?: string;
  /** Teori/Lab — düzenleyici ayrı blok sürüklediğinde backend'in doğru etiketlemesi için. */
  blockType?: 'Theory' | 'Lab';
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
  slotOrder: number;
  startTime: string;
  endTime: string;
  label: string;
  occupancy: number;
  usedClassrooms: number;
  details: string;
  occupations: ClassroomOccupation[];
}

export interface ClassroomScheduleSlot {
  day: number;
  startHour: number;
  endHour: number;
  startTime: string;
  endTime: string;
  courseName: string;
  courseCode: string;
  instructorName: string;
}

export interface TimeSlotSummary {
  slotOrder: number;
  startTime: string;
  endTime: string;
  label: string;
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
  weeklyOccupancyRate: number;
  schedules: ClassroomScheduleSlot[];
}

export interface ClassroomDashboard {
  totalClassrooms: number;
  occupancyRate: number;
  conflictsCount: number;
  emptySlots: number;
  slotsPerDay: number;
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
  slots: TimeSlotSummary[];
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
  startTime: string;
  endTime: string;
  minCapacity?: number;
  classroomType?: string;
  facultyId?: number;
  buildingName?: string;
}
