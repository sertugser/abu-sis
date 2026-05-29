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
  isEnrollmentSuspended?: boolean;
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
  studentRegistrationMessage: string | null;
}
