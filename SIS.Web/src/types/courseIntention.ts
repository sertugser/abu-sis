export type CourseIntentionSurveyStatus = 'Scheduled' | 'Open' | 'Closed';
export type CourseIntentionLevel = 'Certain' | 'Maybe';

export interface CourseIntentionSurveyDto {
  id: number;
  termId: number;
  termName: string;
  title: string;
  status: CourseIntentionSurveyStatus;
  openAtUtc: string;
  closeAtUtc: string;
  isManualOverride: boolean;
  lastStatusChangedAtUtc?: string | null;
}

export interface CourseIntentionOptionDto {
  courseId: number;
  courseCode: string;
  courseName: string;
  ects: number;
  suggestedSemester: number;
  isCompulsory: boolean;
  takeStatus: string;
  previousGrade?: string | null;
  isBlockedByAdvisor: boolean;
  isLocked: boolean;
  lockedIntention?: CourseIntentionLevel | null;
  lockReason?: string | null;
}

export interface CourseIntentionResponseDto {
  courseId: number;
  courseCode: string;
  courseName: string;
  intention: CourseIntentionLevel;
}

export interface StudentSurveyPreferenceDto {
  preferredDays: string;
  preferredTimeRanges: string;
}

export interface CourseIntentionSurveyDetailDto {
  survey: CourseIntentionSurveyDto;
  availableCourses: CourseIntentionOptionDto[];
  responses: CourseIntentionResponseDto[];
  preference: StudentSurveyPreferenceDto;
}

export interface SaveCourseIntentionItemDto {
  courseId: number;
  intention: CourseIntentionLevel;
}

export interface SaveCourseIntentionsRequest {
  surveyId: number;
  intentions: SaveCourseIntentionItemDto[];
  preference: StudentSurveyPreferenceDto;
}

export interface CourseIntentionSummaryDto {
  surveyId: number;
  termId: number;
  termName: string;
  totalRespondingStudents: number;
  courses: CourseIntentionSummaryItemDto[];
}

export interface CourseIntentionSummaryItemDto {
  courseId: number;
  courseCode: string;
  courseName: string;
  certainCount: number;
  maybeCount: number;
  preferredDays: string[];
  preferredTimeRanges: string[];
}

export interface UpsertCourseIntentionSurveyRequest {
  termId: number;
  title?: string | null;
  openAtUtc: string;
  closeAtUtc: string;
}

export interface SetCourseIntentionSurveyStateRequest {
  status: CourseIntentionSurveyStatus;
  isManualOverride?: boolean;
}
