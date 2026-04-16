export interface InstructorPreferenceDto {
  id: number;
  instructorId: number;
  instructorName: string;
  preferenceType: 'TimeSlotPreference' | 'ClassroomPreference' | 'DailyLoadPreference' | 'DayOffPreference';
  preferenceLevel: 'Preferred' | 'StronglyPreferred' | 'Discouraged' | 'StronglyDiscouraged' | 'Unavailable';
  dayOfWeek: number | null;
  timeSlotStart: string | null;
  timeSlotEnd: string | null;
  classroomId: number | null;
  classroomName: string | null;
  buildingName: string | null;
  maxHours: number | null;
  reason: string | null;
  termId: number | null;
  isApproved: boolean;
  createdAt: string;
}

export interface CreateInstructorPreferenceRequest {
  preferenceType: string;
  preferenceLevel: string;
  dayOfWeek?: number | null;
  timeSlotStart?: string | null;
  timeSlotEnd?: string | null;
  classroomId?: number | null;
  buildingName?: string | null;
  maxHours?: number | null;
  reason?: string | null;
  termId?: number | null;
}
