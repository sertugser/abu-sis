import apiClient from './axios';
import type { StudentCourseDto } from '../types/course';

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
  programName: string;
  completedSemesters: number;
  cgpa: number;
  /** Aktif dönem plan durumu: Submitted | Approved | Rejected | None */
  planStatus: string;
  isAtRisk: boolean;
}

export interface StudentRegistrationItem {
  registrationId: number;
  courseSectionId: number;
  courseCode: string;
  courseName: string;
  ects: number;
  schedule: string;
  instructorName: string;
  status: string;
  isCompulsory: boolean;
  isRepeated: boolean;
  takeType: string;
}

export const getAdvisorStudents = () =>
  apiClient.get<AdvisorStudent[]>('/advisor/students').then(r => r.data);

export const getAdvisorStudentRegistrations = (studentId: number) =>
  apiClient.get<StudentRegistrationItem[]>(`/advisor/students/${studentId}/registrations`).then(r => r.data);

export const getAdvisorStudentCatalog = (studentId: number) =>
  apiClient.get<StudentCourseDto[]>(`/course/student-catalog/${studentId}`).then(r => r.data);

export const advisorAddCourse = (studentId: number, courseSectionId: number) =>
  apiClient.post(`/advisor/students/${studentId}/add`, { courseSectionId });

export const advisorDropCourse = (studentId: number, registrationId: number) =>
  apiClient.delete(`/advisor/students/${studentId}/registrations/${registrationId}`);
