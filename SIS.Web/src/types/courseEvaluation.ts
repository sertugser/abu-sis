export type EvaluationPeriodStatus = 'Open' | 'Closed';

export type EvaluationQuestionType = 'Rating' | 'Text';

export type EvaluationQuestionCategory = 'Course' | 'Instructor';

export interface EvaluationPeriod {
  id: number;
  termId: number;
  termName: string;
  openedAt: string;
  closesAt: string;
  status: EvaluationPeriodStatus;
  isCurrentlyOpen: boolean;
}

export interface ActivePeriodResponse {
  isOpen: boolean;
  period?: EvaluationPeriod;
}

export interface EvaluationQuestion {
  id: number;
  text: string;
  questionType: EvaluationQuestionType;
  category: EvaluationQuestionCategory;
  sortOrder: number;
  isActive: boolean;
}

export interface CreateEvaluationQuestionPayload {
  text: string;
  questionType: EvaluationQuestionType;
  category: EvaluationQuestionCategory;
  sortOrder: number;
}

export interface UpdateEvaluationQuestionPayload {
  text: string;
  questionType: EvaluationQuestionType;
  category: EvaluationQuestionCategory;
  sortOrder: number;
  isActive: boolean;
}

export interface EvaluableCourse {
  courseSectionId: number;
  courseCode: string;
  courseName: string;
  instructorName: string;
  isCompleted: boolean;
  submittedAt: string | null;
}

export interface EvaluationAnswerPayload {
  questionId: number;
  ratingValue: number | null;
  textValue: string | null;
}

export interface SubmitEvaluationPayload {
  courseSectionId: number;
  answers: EvaluationAnswerPayload[];
}

export interface QuestionResult {
  questionId: number;
  questionText: string;
  category: EvaluationQuestionCategory;
  questionType: EvaluationQuestionType;
  averageRating: number | null;
  ratingDistribution: Record<string, number> | null;
  textResponses: string[] | null;
}

export interface SectionEvaluationResult {
  courseSectionId: number;
  courseCode: string;
  courseName: string;
  instructorName: string;
  totalResponses: number;
  enrolledCount: number;
  responseRate: number;
  isVisible: boolean;
  overallAverage: number | null;
  questionResults: QuestionResult[];
}

export interface SectionSummary {
  courseSectionId: number;
  courseCode: string;
  courseName: string;
  instructorName: string;
  responseCount: number;
  enrolledCount: number;
  responseRate: number;
  averageRating: number | null;
  isVisible: boolean;
}

export interface DepartmentEvaluationSummary {
  departmentId: number;
  departmentName: string;
  termId: number;
  termName: string;
  totalSections: number;
  evaluatedSections: number;
  overallResponseRate: number;
  averageRating: number | null;
  sectionSummaries: SectionSummary[];
}

export interface OpenPeriodPayload {
  termId: number;
  closesAt: string;
}
