import apiClient from './axios';

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
  apiClient.get<PendingApproval[]>('/advisor/pending-approvals').then(r => r.data);

export const approveStudent = (studentId: number, note?: string) =>
  apiClient.post('/advisor/approve', { studentId, note });

export const rejectStudent = (studentId: number, note?: string) =>
  apiClient.post('/advisor/reject', { studentId, note });
