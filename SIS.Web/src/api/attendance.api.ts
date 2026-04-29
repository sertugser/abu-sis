import apiClient from './axios';

export type AttendanceStatus = 'Present' | 'Absent' | 'Late' | 'Excused';
export type AttendanceEntrySource = 'Manual' | 'Qr';

export interface AttendanceStudent {
  studentId: number;
  registrationId: number;
  studentNumber: string;
  firstName: string;
  lastName: string;
  email: string;
  status: AttendanceStatus | null;
  source: AttendanceEntrySource | null;
  submittedAt: string | null;
}

export interface AttendanceSession {
  id: number;
  courseSectionId: number;
  courseCode: string;
  courseName: string;
  sectionNumber: number;
  weekNumber: number;
  sessionDate: string;
  topic: string;
  isQrOpen: boolean;
  isQrCurrentlyActive: boolean;
  qrExpiresAt: string | null;
  token: string;
  totalStudents: number;
  presentCount: number;
  absentCount: number;
  lateCount: number;
  excusedCount: number;
  students: AttendanceStudent[];
}

export interface CreateAttendanceSessionPayload {
  courseSectionId: number;
  weekNumber: number;
  sessionDate: string;
  topic?: string;
}

export interface SaveAttendancePayload {
  records: Array<{
    studentId: number;
    status: AttendanceStatus;
  }>;
}

export interface OpenQrPayload {
  durationMinutes: number;
}

export interface QrAttendanceSession {
  isActive: boolean;
  message: string;
  courseCode: string;
  courseName: string;
  sectionNumber: number;
  weekNumber: number;
  sessionDate: string;
  expiresAt: string | null;
}

export interface AttendanceStudentWeekStatus {
  status: AttendanceStatus;
  source: AttendanceEntrySource;
  submittedAt: string | null;
}

export interface AttendanceMatrixStudent {
  studentId: number;
  registrationId: number;
  studentNumber: string;
  firstName: string;
  lastName: string;
  email: string;
  weeks: Record<number, AttendanceStudentWeekStatus>;
}

export interface AttendanceMatrixWeek {
  weekNumber: number;
  sessionId: number | null;
  sessionDate: string | null;
  topic: string;
  presentCount: number;
  absentCount: number;
  lateCount: number;
  excusedCount: number;
}

export interface AttendanceMatrix {
  courseSectionId: number;
  courseCode: string;
  courseName: string;
  sectionNumber: number;
  totalWeeks: number;
  currentWeekNumber: number | null;
  weeks: AttendanceMatrixWeek[];
  students: AttendanceMatrixStudent[];
}

export interface SubmitQrPayload {
  studentNumber: string;
  firstName: string;
  lastName: string;
}

export interface SubmitQrResponse {
  success: boolean;
  message: string;
  studentName: string;
}

export async function createAttendanceSession(payload: CreateAttendanceSessionPayload): Promise<AttendanceSession> {
  const { data } = await apiClient.post('/attendance/sessions', payload);
  return data;
}

export async function getAttendanceSession(id: number): Promise<AttendanceSession> {
  const { data } = await apiClient.get(`/attendance/sessions/${id}`);
  return data;
}

export async function getAttendanceMatrix(sectionId: number): Promise<AttendanceMatrix> {
  const { data } = await apiClient.get(`/attendance/sections/${sectionId}/matrix`);
  return data;
}

export async function saveAttendanceRecords(id: number, payload: SaveAttendancePayload): Promise<AttendanceSession> {
  const { data } = await apiClient.put(`/attendance/sessions/${id}/records`, payload);
  return data;
}

export async function openQrAttendance(id: number, payload: OpenQrPayload): Promise<AttendanceSession> {
  const { data } = await apiClient.post(`/attendance/sessions/${id}/qr/open`, payload);
  return data;
}

export async function closeQrAttendance(id: number): Promise<AttendanceSession> {
  const { data } = await apiClient.post(`/attendance/sessions/${id}/qr/close`);
  return data;
}

export async function getQrAttendanceSession(token: string): Promise<QrAttendanceSession> {
  const { data } = await apiClient.get(`/attendance/qr/${token}`);
  return data;
}

export async function submitQrAttendance(token: string, payload: SubmitQrPayload): Promise<SubmitQrResponse> {
  const { data } = await apiClient.post(`/attendance/qr/${token}/submit`, payload);
  return data;
}
