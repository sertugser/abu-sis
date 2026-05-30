export interface StudentTranscriptDto {
  studentId: number;
  studentNumber: string;
  fullName: string;
  facultyName: string;
  departmentName: string;
  programName: string;
  studentStatus: string;
  enrollmentDate: string;
  generatedAt: string;
  currentCgpa: number;
  totalAttemptedCredits: number;
  totalAttemptedEcts: number;
  totalEarnedCredits: number;
  totalEarnedEcts: number;
  totalGpaEcts: number;
  totalGpaPoints: number;
  terms: StudentTranscriptTermDto[];
  // Aktif dönemde kayıtlı olup henüz notu girilmemiş dersler. Resmî transkript bunları
  // göstermez; Transkript Senaryosu düzenlenebilir "projeksiyon" satırı olarak ekler.
  inProgressCourses: StudentTranscriptInProgressCourseDto[];
}

export interface StudentTranscriptInProgressCourseDto {
  registrationId: number;
  courseId: number;
  courseCode: string;
  courseName: string;
  credits: number;
  ects: number;
  equivalentCourseIds: number[];
  termId: number;
  termName: string;
  year: number;
  semester: string;
}

export interface StudentTranscriptTermDto {
  termId: number;
  termName: string;
  year: number;
  semester: string;
  attemptedCredits: number;
  attemptedEcts: number;
  earnedCredits: number;
  earnedEcts: number;
  gpaEcts: number;
  gpaPoints: number;
  cumulativeGpaEcts: number;
  cumulativeGpaPoints: number;
  sgpa: number;
  cgpa: number;
  courses: StudentTranscriptCourseDto[];
}

export interface StudentTranscriptCourseDto {
  registrationId: number;
  courseId: number;
  courseCode: string;
  courseName: string;
  credits: number;
  ects: number;
  equivalentCourseIds: number[];
  averageScore: number | null;
  grade: string;
  status: string;
  isRepeated: boolean;
  isPassed: boolean;
  countsTowardsGpa: boolean;
  gradePoint: number | null;
  weightedEcts: number | null;
}
