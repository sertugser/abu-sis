export type ScheduleBlockType = 'Theory' | 'Lab';

export interface ScheduleBlock {
  day: number;
  startTime: string;
  endTime: string;
  blockType: ScheduleBlockType;
  classroomId: number | null;
  classroomName: string | null;
  classroomType: string | null;
}

export interface StudentTimetableSection {
  sectionId: number;
  sectionNumber: string;
  courseCode: string;
  courseName: string;
  instructorDisplay: string;
  blocks: ScheduleBlock[];
}

export interface StudentTimetable {
  studentName: string;
  termId: number;
  termName: string;
  sections: StudentTimetableSection[];
  /** Sistem ayarından gelen öğle arası penceresi, "HH:mm". */
  lunchBreakStart: string;
  lunchBreakEnd: string;
  /** Term'in tanımlı ders saati pencereleri — ızgaranın satır yapısını besler. */
  slots: TimetableSlot[];
}

export interface TimetableSlot {
  start: string; // "HH:mm"
  end: string;
}

export interface StudentTimetableTerm {
  termId: number;
  label: string;
  isDefault: boolean;
}
