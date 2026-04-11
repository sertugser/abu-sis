export interface CourseListDto {
  courseId: number;
  code: string;
  name: string;
  ects: number;
  theoryHours: number;
  labHours: number;
  isCompulsory: boolean;
  suggestedSemester: number;
  sections: SectionDto[];
}

export interface SectionDto {
  sectionId: number;
  instructorName: string;
  capacity: number;
  enrolledCount: number;
  availableSeats: number;
  classroomName: string;
  schedule: string;
  isActive: boolean;
}

export interface StudentCourseDto {
  courseId: number;
  code: string;
  name: string;
  ects: number;
  theoryHours: number;
  labHours: number;
  isCompulsory: boolean;
  suggestedSemester: number;
  /** IlkDefa = hic alinmamis, Alttan = FF/NA/U tekrar, OncedenAlindi = gecmis (yukseltme mumkun) */
  takeStatus: 'IlkDefa' | 'Alttan' | 'OncedenAlindi';
  prerequisitesMet: boolean;
  previousGrade: string | null;
  /** Zorunlu / Secmeli / MufredatDisi */
  curriculumType: 'Zorunlu' | 'Secmeli' | 'MufredatDisi';
  sections: SectionDto[];
}

export interface SectionDetailDto {
  section: SectionDto;
  course: {
    id: number;
    code: string;
    name: string;
    ects: number;
  };
}

export interface ActiveTermDto {
  id: number;
  name: string;
  year: number;
  semester: string;
  registrationStart: string;
  registrationEnd: string;
  addDropStart: string;
  addDropEnd: string;
  isRegistrationOpen: boolean;
  isAddDropOpen: boolean;
  status: string;
}
