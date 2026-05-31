export interface SectionExamClassroom {
  classroomId: number;
  classroomName: string;
}

export interface SectionExam {
  id: number;
  gradeComponentId: number;
  componentName: string;
  componentType: 'Other' | 'Midterm' | 'Final';
  weight: number;
  examDate: string;            // ISO
  durationMinutes: number | null;
  isAnnouncedToStudents: boolean;
  announcedAt: string | null;
  description: string | null;
  classrooms: SectionExamClassroom[];
}

export interface SaveSectionExamRequest {
  id: number | null;
  gradeComponentId: number;
  examDate: string;            // ISO (UTC)
  durationMinutes: number | null;
  isAnnouncedToStudents: boolean;
  description: string | null;
  classroomIds: number[];
}

export interface ExamConfigComponent {
  id: number;
  name: string;
  shortName: string;
  type: 'Other' | 'Midterm' | 'Final';
  weight: number;
  order: number;
}

export interface ExamConfigClassroom {
  id: number;
  name: string;
  buildingName: string;
  capacity: number;
}

export interface ExamConfig {
  courseCode: string;
  courseName: string;
  hasRegulation: boolean;
  midtermCount: number;
  finalCount: number;
  minMidtermWeight: number | null;
  maxMidtermWeight: number | null;
  minFinalWeight: number | null;
  maxFinalWeight: number | null;
  components: ExamConfigComponent[];
  classrooms: ExamConfigClassroom[];
  announceDeadlineDays: number;
}
