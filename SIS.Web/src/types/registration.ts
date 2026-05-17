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
  warnings: ValidationError[];
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
  courseSectionId: number;
  courseId: number;
  sectionNumber: number;
  courseCode: string;
  courseName: string;
  ects: number;
  /** Ulusal/yerel kredi (NationalCredit). Snapshot varsa o değer. */
  nationalCredit: number;
  theoryHours: number;
  labHours: number;
  applicationHours: number | null;
  /** Dersin müfredattaki sınıfı (1-4). Aktif curriculum'da yoksa null. */
  classYear: number | null;
  /** "Zorunlu" / "Seçmeli" / "Alttan FF" / "Devamlı Alttan NA" gibi. */
  acquisitionType: string;
  /** Dersi sahiplenen birim adı (örn. "Ortak Dersler", "Bilgisayar Müh."). */
  ownerDepartmentName: string | null;
  /** "Türkçe" / "İngilizce" / "Fransızca" / "Almanca" / "Arapça" */
  languageDisplay: string;
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
  advisorNote?: string;
}

export interface StudentAttendanceStatusDto {
  termId: number;
  termName: string;
  year: number;
  semester: string;
  availableTerms: StudentAttendanceTermDto[];
  policy: StudentAttendancePolicyDto;
  courses: StudentAttendanceCourseDto[];
}

export interface StudentAttendanceTermDto {
  termId: number;
  termName: string;
  year: number;
  semester: string;
  isActive: boolean;
}

export interface StudentAttendancePolicyDto {
  termWeekCount: number;
  theoryAbsenceLimitPercent: number;
  labAbsenceLimitPercent: number;
}

export interface StudentAttendanceCourseDto {
  registrationId: number;
  courseSectionId: number;
  sectionNumber: number;
  courseCode: string;
  courseName: string;
  ects: number;
  nationalCredit: number;
  suggestedSemester: number;
  programName: string;
  theoryHours: number;
  labHours: number;
  mandatoryAttendance: boolean;
  attendanceEntered: boolean;
  theoryAbsenceHours: number | null;
  labAbsenceHours: number | null;
  totalAbsenceHours: number | null;
  totalTheoryHours: number;
  totalLabHours: number;
  theoryAbsenceLimitHours: number;
  labAbsenceLimitHours: number;
  attendanceStatusLabel: string;
}
