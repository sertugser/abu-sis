import apiClient from './axios';
import type { CourseHistoryTermDto } from '../types/registration';
import type { CurriculumStatusDto } from '../types/curriculum';
import type { StudentTranscriptDto } from '../types/transcript';
import type { TermGpaHistoryDto } from '../types/student';
import type { StudentRegistrationCatalog } from '../types/registrationCatalog';
import type { StudentTimetable, StudentTimetableTerm } from '../types/studentTimetable';
import type { DepartmentSchedule, DepartmentScheduleContext } from '../types/departmentSchedule';
import type { ScenarioReport, TranscriptScenarioRequest } from '../types/transcriptScenario';
import { toApiError } from '../lib/apiError';

export interface PendingApproval {
  studentId: number;
  studentNumber: string;
  studentName: string;
  cgpa: number;
  sgpa: number;
  completedSemesters: number;
  isScholarshipStudent: boolean;
  registrations: PendingRegistrationItem[];
  totalEcts: number;
  ectsLimit: number;
  submittedAt: string;
  planStatus: string;
  failedCourses: FailedCourse[];
  curriculumTotalCourses: number;
  curriculumCompletedCourses: number;
  curriculumTotalEcts: number;
  curriculumCompletedEcts: number;
}

export interface PendingApprovalListResponse {
  calculationMethod: string;
  calculationMethodLabel: string;
  items: PendingApproval[];
}

export interface PendingRegistrationItem {
  registrationId: number;
  courseSectionId: number;
  courseCode: string;
  courseName: string;
  ects: number;
  schedule: string;
  instructorName: string;
  isRepeated: boolean;
  status: string;
  hasScheduleConflict: boolean;
}

export interface FailedCourse {
  courseCode: string;
  courseName: string;
  grade: string;
  isSelectedInPlan: boolean;
}

export const getPendingApprovals = () =>
  apiClient.get<PendingApprovalListResponse>('/advisor/pending-approvals').then(r => r.data);

export const approveStudent = (studentId: number, note?: string) =>
  apiClient.post('/advisor/approve', { studentId, note });

export const rejectStudent = (studentId: number, note?: string) =>
  apiClient.post('/advisor/reject', { studentId, note });

// ── Dashboard stats ───────────────────────────────────────────────────────────

export interface AdvisorDashboardStats {
  adviseeCount: number;
  pendingApprovalCount: number;
  atRiskCount: number;
  approvedThisTerm: number;
  rejectedThisTerm: number;
  retentionThreshold: number;
}

export const getAdvisorDashboardStats = () =>
  apiClient.get<AdvisorDashboardStats>('/advisor/dashboard-stats').then(r => r.data);

// ── Advisor students + on-behalf editing ──────────────────────────────────────

export interface AdvisorStudent {
  studentId: number;
  studentNumber: string;
  studentName: string;
  /** Ad (filtre/kolon ayrımı için). */
  firstName: string;
  /** Soyad. */
  lastName: string;
  programName: string;
  /** Fakülte adı (program → bölüm → fakülte). */
  facultyName: string;
  /** Bağlı olunan müfredat sürümü/versiyonu. */
  curriculumVersion: string;
  /** Programa kayıt tarihi. */
  enrollmentDate: string;
  completedSemesters: number;
  /** Sınıf = (tamamlanan yarıyıl / 2) + 1. */
  classYear: number;
  /** "Normal Süresi İçinde" / "Normal Süre Aşıldı". */
  studyDurationStatus: string;
  cgpa: number;
  /** Aktif dönem plan durumu: Submitted | Approved | Rejected | None */
  planStatus: string;
  /** Öğrencinin planı kesinleştirme (submit) anı; yoksa null. */
  planSubmittedAt: string | null;
  /** Danışman onay/red anı (Approved/Rejected planda); yoksa null. */
  planDecidedAt: string | null;
  isAtRisk: boolean;
}

export interface StudentRegistrationItem {
  registrationId: number;
  courseSectionId: number;
  /** Ders kimliği — "Diğer Bilgiler" detayı için. */
  courseId: number;
  /** Şube no (Proliz "Şb"). */
  sectionNumber: number;
  courseCode: string;
  courseName: string;
  ects: number;
  /** Ulusal kredi (Proliz "Krd"). */
  nationalCredit: number;
  theoryHours: number;
  labHours: number;
  applicationHours: number | null;
  schedule: string;
  instructorName: string;
  status: string;
  isCompulsory: boolean;
  /** Müfredattaki önerilen yarıyıl → sınıf hesabı için. */
  suggestedSemester: number;
  isRepeated: boolean;
  takeType: string;
  /** Açma Nedeni adı; yoksa null. */
  openingReasonName: string | null;
  /** Seçmeli havuz slot adı (Proliz "Grup Kod"); yoksa null. */
  electivePoolSlotName: string | null;
  /** Seçmeli grup kodu; yoksa null. */
  electiveGroupCode: string | null;
}

export const getAdvisorStudents = () =>
  apiClient.get<AdvisorStudent[]>('/advisor/students').then(r => r.data);

export const getAdvisorStudentRegistrations = (studentId: number) =>
  apiClient.get<StudentRegistrationItem[]>(`/advisor/students/${studentId}/registrations`).then(r => r.data);

export const advisorAddCourse = (studentId: number, courseSectionId: number) =>
  apiClient.post(`/advisor/students/${studentId}/add`, { courseSectionId });

export const advisorDropCourse = (studentId: number, registrationId: number) =>
  apiClient.delete(`/advisor/students/${studentId}/registrations/${registrationId}`);

// ── Danışan bilgi görünümleri (Ek/Diğer İşlemler — Proliz paritesi) ────────────

export const getAdvisorStudentCourseHistory = (studentId: number) =>
  apiClient.get<CourseHistoryTermDto[]>(`/advisor/students/${studentId}/course-history`).then(r => r.data);

export const getAdvisorStudentCurriculumStatus = (studentId: number) =>
  apiClient.get<CurriculumStatusDto>(`/advisor/students/${studentId}/curriculum-status`).then(r => r.data);

export const getAdvisorStudentTranscript = (studentId: number) =>
  apiClient.get<StudentTranscriptDto>(`/advisor/students/${studentId}/transcript`).then(r => r.data);

export const getAdvisorStudentTermGpa = (studentId: number) =>
  apiClient.get<TermGpaHistoryDto>(`/advisor/students/${studentId}/term-gpa`).then(r => r.data);

// ── Danışman → öğrenci adına kayıt kataloğu (Faz 2 B6) ───────────────────────

export async function getAdvisorRegistrationCatalog(studentId: number): Promise<StudentRegistrationCatalog> {
  try {
    const r = await apiClient.get<StudentRegistrationCatalog>(`/course/registration-catalog/${studentId}`);
    return r.data;
  } catch (error) {
    throw toApiError(error, 'Öğrenci kataloğu yüklenemedi.');
  }
}

// ── Danışman → advisee Ek/Diğer İşlemler wrapper'ları (Plan 2) ────────────────

export const getAdvisorStudentTimetableTerms = (studentId: number) =>
  apiClient.get<StudentTimetableTerm[]>(`/advisor/students/${studentId}/timetable/terms`).then(r => r.data);

export const getAdvisorStudentTimetable = (studentId: number, termId?: number) =>
  apiClient.get<StudentTimetable>(`/advisor/students/${studentId}/timetable`, { params: termId ? { termId } : {} }).then(r => r.data);

export const getAdvisorStudentDeptScheduleContext = (studentId: number) =>
  apiClient.get<DepartmentScheduleContext>(`/advisor/students/${studentId}/department-schedule/context`).then(r => r.data);

export const getAdvisorStudentDeptSchedule = (
  studentId: number, termId: number, programId: number, classYear: number,
) => apiClient.get<DepartmentSchedule>(`/advisor/students/${studentId}/department-schedule`, {
  params: { termId, programId, classYear },
}).then(r => r.data);

export const calculateAdvisorTranscriptScenario = (studentId: number, request: TranscriptScenarioRequest) =>
  apiClient.post<ScenarioReport>(`/advisor/students/${studentId}/transcript-scenario`, request).then(r => r.data);

export const downloadAdvisorRegistrationPlanPdf = (studentId: number) =>
  apiClient.get<Blob>(`/advisor/students/${studentId}/registration-plan/pdf`, { responseType: 'blob' }).then(r => r.data);
