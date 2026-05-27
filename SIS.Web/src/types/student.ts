export interface StudentInfoDto {
  id: number;
  studentNumber: string;
  firstName: string;
  lastName: string;
  cgpa: number;
  sgpa: number;
  completedSemesters: number;
  status: string;
  isScholarshipStudent: boolean;
  hasFinancialBlock: boolean;
  programName: string;
  departmentName: string;
  facultyName: string;
  withdrawalCount: number;
  programId: number;
  classYear: number;
  ectsLimit: number;
  advisorName: string | null;
  advisorTitle: string | null;
}

export interface StudentTermGpaDto {
  termId: number;
  termName: string;
  year: number;
  semester: string;
  courseCount: number;
  totalCredits: number;
  totalEcts: number;
  // GPA'ya giren ders yoksa (yalnızca S/U/W vb.) null — gerçek 0 ile "veri yok" ayrımı.
  sgpa: number | null;
  cgpa: number | null;
}

export interface TermGpaHistoryDto {
  terms: StudentTermGpaDto[];
  totalAttemptedCredits: number;
  totalAttemptedEcts: number;
}

export interface StudentGradeStatisticsDto {
  registrationId: number;
  courseSectionId: number;
  termName: string;
  facultyProgram: string;
  instructorName: string;
  courseCode: string;
  courseName: string;
  sectionNumber: number;
  studentLetterGrade: string | null;
  studentWeightedAverage: number | null;
  hasGradingCriteria: boolean;
  hasAnyComponentScore: boolean;
  hasAllComponentScores: boolean;
  isResultFinalized: boolean;
  isCommonEvaluation: boolean;
  result: StudentResultInfoDto;
  gradeRanges: StudentGradeRangeDto[];
  components: StudentComponentStatisticsDto[];
  summary: StudentSectionStatisticsSummaryDto;
}

export interface StudentResultInfoDto {
  status: string;
  statusDate: string | null;
  evaluationMethod: string;
}

export interface StudentGradeRangeDto {
  grade: string;
  minScore: number;
  maxScore: number;
  studentCount: number;
}

export interface StudentComponentStatisticsDto {
  componentId: number;
  name: string;
  weight: number;
  totalStudentCount: number;
  enteredStudentCount: number;
  missingStudentCount: number;
  averageScore: number | null;
  studentScore: number | null;
}

export interface StudentSectionStatisticsSummaryDto {
  totalStudentCount: number;
  gradedStudentCount: number;
  classAverage: number | null;
}
