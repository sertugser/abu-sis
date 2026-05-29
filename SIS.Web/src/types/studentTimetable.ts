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
}

export interface StudentTimetableTerm {
  termId: number;
  label: string;
  isDefault: boolean;
}
