// ── Response Types ──

export interface OpenedCourseSectionDto {
  sectionId: number;
  sectionNumber: number;
  courseId: number;
  courseCode: string;
  courseName: string;
  theoryHours: number;
  labHours: number;
  credits: number;
  ects: number;
  suggestedSemester: number;
  capacity: number;
  enrolledCount: number;
  registeredCount: number;
  instructorId: number | null;
  instructorName: string | null;
  isActive: boolean;
}

export interface OpenedCourseListDto {
  departmentId: number;
  departmentName: string;
  termId: number;
  termName: string;
  totalSections: number;
  sections: OpenedCourseSectionDto[];
}

export interface CurriculumCourseForOpeningDto {
  courseId: number;
  courseCode: string;
  courseName: string;
  theoryHours: number;
  labHours: number;
  ects: number;
  suggestedSemester: number;
  isCompulsory: boolean;
  alreadyOpened: boolean;
  existingSectionCount: number;
}

export interface InstructorOptionDto {
  instructorId: number;
  fullName: string;
}

// ── Request Types ──

export interface OpenCourseRequest {
  courseId: number;
  sectionCount: number;
  capacityPerSection: number;
  instructorId?: number;
}

export interface BulkOpenCoursesRequest {
  courses: OpenCourseRequest[];
}

export interface BulkOpenCoursesResultDto {
  totalCoursesOpened: number;
  totalSectionsCreated: number;
  errors: string[];
}

export interface UpdateOpenedSectionRequest {
  sectionId: number;
  instructorId?: number;
  capacity?: number;
}
