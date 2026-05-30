export type TakeStatus = 'IlkDefa' | 'Alttan' | 'OncedenAlindi';
export type SemesterParity = 'Guz' | 'Bahar';

export interface RegistrationStudentHeader {
  id: number;
  studentNumber: string;
  fullName: string;
  programName: string;
  curriculumName: string;
  classYear: number;
  completedSemesters: number;
  cgpa: number;
  ectsLimit: number;
  financialStatus: string;
  studyDurationStatus: string;
}

export interface RegistrationTermInfo {
  id: number;
  name: string;
  year: number;
  termType: string;
  isRegistrationOpen: boolean;
  isAddDropOpen: boolean;
  hasAcademicCalendar: boolean;
  registrationMessage: string | null;
}

export interface ApplicablePoolSlotRef {
  poolId: number;
  slotName: string;
  groupCode: string;
  groupName: string;
  ectsPerCourse: number | null;
}

export interface CatalogCourseRow {
  courseId: number;
  code: string;
  name: string;
  isCompulsory: boolean;
  theoryHours: number;
  labHours: number;
  nationalCredit: number;
  ects: number;
  suggestedSemester: number;
  classYear: number;
  takeStatus: string;
  prerequisitesMet: boolean;
  missingPrerequisites: string[];
  previousAttemptInfo: string | null;
  activeRegistrationStatus: string | null;
  hasOpenSections: boolean;
  applicablePoolSlots: ApplicablePoolSlotRef[];
}

export interface CatalogSemester {
  parity: string;
  semesterNumber: number;
  courses: CatalogCourseRow[];
}

export interface CatalogClassYear {
  classYear: number;
  semesters: CatalogSemester[];
}

export interface CatalogPoolSlot {
  poolId: number;
  groupCode: string;
  groupName: string;
  slotName: string;
  suggestedSemesterStart: number;
  suggestedSemesterEnd: number;
  requiredCourseCount: number;
  completedCourseCount: number;
  requiredEcts: number;
  earnedEcts: number;
  ectsPerCourse: number | null;
  isFulfilled: boolean;
}

export interface SelectedCourse {
  registrationId: number;
  courseSectionId: number;
  courseId: number;
  courseCode: string;
  courseName: string;
  sectionNumber: number;
  isCompulsory: boolean;
  theoryHours: number;
  labHours: number;
  ects: number;
  nationalCredit: number;
  suggestedSemester: number;
  takeType: string;
  status: string;
  openingReasonName: string | null;
  electiveGroupCode: string | null;
}

export interface StudentRegistrationCatalog {
  student: RegistrationStudentHeader;
  term: RegistrationTermInfo;
  classYears: CatalogClassYear[];
  retakeCourses: CatalogCourseRow[];
  electivePoolSlots: CatalogPoolSlot[];
  selectedCourses: SelectedCourse[];
}

export interface SectionPickerRow {
  sectionId: number;
  sectionNumber: number;
  code: string;
  name: string;
  schedule: string;
  isCompulsory: boolean;
  theoryHours: number;
  labHours: number;
  nationalCredit: number;
  ects: number;
  instructorName: string;
  classYear: number;
  openingReasonName: string | null;
  takeTypeLabel: string;
  capacity: number;
  enrolledCount: number;
  availableSeats: number;
  quotaVisible: boolean;
  groupCode: string | null;
  isEnrollmentSuspended: boolean;
  isActive: boolean;
  isEligible: boolean;
  ineligibleReason: string | null;
  prerequisiteNote: string | null;
}

export interface SectionPickerResult {
  items: SectionPickerRow[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface SectionPickerQuery {
  instructorId?: number;
  facultyId?: number;
  programId?: number;
  search?: string;
  page?: number;
  pageSize?: number;
}
