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
  quotaVisible: boolean;
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
  /**
   * Faz C C1: Bu ders öğrencinin müfredatındaki hangi grup slot(lar)ına aday?
   * Bir ders Karar #5 gereği birden fazla grupta yer alabilir.
   * Boş liste = ders herhangi bir slot'a aday değil.
   */
  applicablePoolSlots: ApplicablePoolSlotDto[];
  sections: SectionDto[];
}

export interface ApplicablePoolSlotDto {
  poolId: number;
  slotName: string;
  groupCode: string;
  groupName: string;
  ectsPerCourse: number | null;
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

export interface CourseDetailRefDto {
  courseId: number;
  code: string;
  name: string;
}

export interface CourseDetailDto {
  id: number;
  code: string;
  name: string;
  englishName: string | null;
  description: string | null;
  ects: number;
  nationalCredit: number;
  theoryHours: number;
  labHours: number;
  applicationHours: number | null;
  language: string;
  level: string;
  teachingMethod: string;
  status: string;
  isNonGpa: boolean;
  departmentName: string | null;
  facultyName: string | null;
  examType: string | null;
  prerequisites: CourseDetailRefDto[];
  equivalencies: CourseDetailRefDto[];
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
  hasAcademicCalendar: boolean;
  studentRegistrationEnabled: boolean;
  studentRegistrationOpenAt: string | null;
  studentRegistrationMessage: string | null;
  isFreezeApplicationOpen: boolean;
  studentFreezeApplicationMessage: string | null;
}
