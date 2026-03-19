export interface RegistrationPlanItemDto {
  courseSectionId: number;
}

export interface RegistrationSubmitDto {
  courseSectionIds: number[];
}

export interface ValidationError {
  rule: string;
  message: string;
  courseSectionId?: number;
}

export interface ValidationResultDto {
  isValid: boolean;
  errors: ValidationError[];
}

export interface RegistrationItemDto {
  id: number;
  courseSectionId: number;
  courseCode: string;
  courseName: string;
  ects: number;
  status: string;
}

export interface RegistrationResponseDto {
  success: boolean;
  registrations: RegistrationItemDto[];
  validationResult?: ValidationResultDto;
}

export interface CourseHistoryTermDto {
  termId: number;
  termName: string;
  year: number;
  semester: string;
  courses: CourseHistoryItemDto[];
}

export interface CourseHistoryItemDto {
  registrationId: number;
  courseCode: string;
  courseName: string;
  ects: number;
  instructorName: string;
  grade: string | null;
  status: string;
  isRepeated: boolean;
}

export interface StudentRegistrationDto {
  registrationId: number;
  courseSectionId: number;
  courseCode: string;
  courseName: string;
  ects: number;
  theoryHours: number;
  labHours: number;
  instructorName: string;
  schedule: string;
  status: string;
  isCompulsory: boolean;
  suggestedSemester: number;
  isRepeated: boolean;
  takeType: string;
}
