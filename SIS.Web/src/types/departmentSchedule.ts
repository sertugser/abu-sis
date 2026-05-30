export type DepartmentScheduleBlockType = 'Theory' | 'Lab';

export interface DepartmentScheduleBlock {
  day: number;
  startTime: string;
  endTime: string;
  blockType: DepartmentScheduleBlockType;
  classroomId: number | null;
  classroomName: string | null;
  classroomCapacity: number | null;
}

export interface DepartmentScheduleSection {
  sectionId: number;
  sectionNumber: string;
  courseCode: string;
  courseName: string;
  instructorDisplay: string;
  blocks: DepartmentScheduleBlock[];
}

export interface DepartmentSchedule {
  termId: number;
  termName: string;
  programId: number;
  programName: string;
  departmentName: string;
  classYear: number;
  sections: DepartmentScheduleSection[];
}

export interface DepartmentScheduleTerm {
  termId: number;
  label: string;
  isDefault: boolean;
}

export interface DepartmentScheduleContext {
  programId: number;
  programName: string;
  departmentName: string;
  defaultClassYear: number;
  availableClassYears: number[];
  terms: DepartmentScheduleTerm[];
}
