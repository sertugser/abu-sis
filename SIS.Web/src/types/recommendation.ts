export interface StudentRecommendationsDto {
  studentId: number;
  pacePreference: number;
  generatedAtUtc: string;
  recommendations: CourseRecommendationDto[];
}

export interface CourseRecommendationDto {
  courseId: number;
  courseCode: string;
  courseName: string;
  ects: number;
  isCompulsory: boolean;
  suggestedSemester: number;
  takeType: string;
  category: 'Zorunlu Al' | 'Onerilen' | 'Alabilirsin';
  priorityScore: number;
  difficulty: CourseDifficultyDto;
  criticalPath: CriticalPathAnalysisDto;
  previousGrade?: string | null;
  advisorConstraintType?: 'Block' | 'Warning' | null;
  advisorConstraintNote?: string | null;
  reasons: string[];
}

export interface CourseDifficultyDto {
  courseId: number;
  score: number;
  failureRateComponent: number;
  gradeDistributionComponent: number;
  retakeDifficultyComponent: number;
  ectsComponent: number;
  trendComponent: number;
}

export interface CriticalPathAnalysisDto {
  courseId: number;
  estimatedDelaySemesters: number;
  dependentCourses: string[];
}
