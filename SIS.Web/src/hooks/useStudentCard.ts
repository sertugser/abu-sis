import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import apiClient from '../api/axios';
import type { StudentRegistrationDto, CourseHistoryTermDto, StudentAttendanceStatusDto } from '../types/registration';
import type { StudentTermGpaDto } from '../types/student';
import type { StudentTranscriptDto } from '../types/transcript';
import type { CurriculumStatusDto } from '../types/curriculum';

const BASE = '/admin/students';

/**
 * Rethrow an axios error as a plain Error carrying the backend `{ message }`.
 * Lets callers surface the server reason via `err instanceof Error ? err.message`.
 */
function asBackendError(error: unknown, fallbackMessage: string): Error {
  const message =
    typeof error === 'object' &&
    error !== null &&
    'response' in error &&
    typeof (error as { response?: { data?: { message?: unknown } } }).response?.data?.message ===
      'string'
      ? (error as { response: { data: { message: string } } }).response.data.message
      : fallbackMessage;
  return new Error(message);
}

export function useStudentCardRegistrations(studentId: number | null) {
  return useQuery({
    queryKey: ['admin', 'student-card', 'registrations', studentId],
    queryFn: async () => {
      const { data } = await apiClient.get<StudentRegistrationDto[]>(`${BASE}/${studentId}/registrations`);
      return data;
    },
    enabled: studentId != null,
  });
}

export function useStudentCardCourseHistory(studentId: number | null) {
  return useQuery({
    queryKey: ['admin', 'student-card', 'course-history', studentId],
    queryFn: async () => {
      const { data } = await apiClient.get<CourseHistoryTermDto[]>(`${BASE}/${studentId}/course-history`);
      return data;
    },
    enabled: studentId != null,
  });
}

export function useStudentCardTermGpa(studentId: number | null) {
  return useQuery({
    queryKey: ['admin', 'student-card', 'term-gpa', studentId],
    queryFn: async () => {
      const { data } = await apiClient.get<StudentTermGpaDto[]>(`${BASE}/${studentId}/term-gpa`);
      return data;
    },
    enabled: studentId != null,
  });
}

export function useStudentCardAttendance(studentId: number | null, termId?: number | null) {
  return useQuery({
    queryKey: ['admin', 'student-card', 'attendance', studentId, termId ?? null],
    queryFn: async () => {
      const { data } = await apiClient.get<StudentAttendanceStatusDto>(`${BASE}/${studentId}/attendance`, {
        params: termId != null ? { termId } : undefined,
      });
      return data;
    },
    enabled: studentId != null,
  });
}

export function useStudentCardTranscript(studentId: number | null) {
  return useQuery({
    queryKey: ['admin', 'student-card', 'transcript', studentId],
    queryFn: async () => (await apiClient.get<StudentTranscriptDto>(`${BASE}/${studentId}/transcript`)).data,
    enabled: studentId != null,
  });
}

export function useStudentCardCurriculumStatus(studentId: number | null) {
  return useQuery({
    queryKey: ['admin', 'student-card', 'curriculum-status', studentId],
    queryFn: async () => (await apiClient.get<CurriculumStatusDto>(`${BASE}/${studentId}/curriculum-status`)).data,
    enabled: studentId != null,
  });
}

export interface PromotionCriterion {
  name: string;
  passed: boolean;
  detail: string | null;
}

export interface StudentPromotionStatus {
  currentLevel: number;
  eligibleTargetLevel: number | null;
  canPromote: boolean;
  reason: string | null;
  normalDurationYears: number;
  criteria: PromotionCriterion[];
}

export function useStudentPromotionStatus(studentId: number | null) {
  return useQuery({
    queryKey: ['admin', 'student-card', 'promotion-status', studentId],
    queryFn: async () =>
      (await apiClient.get<StudentPromotionStatus>(`${BASE}/${studentId}/promotion-status`)).data,
    enabled: studentId != null,
  });
}

function invalidatePromotionQueries(qc: ReturnType<typeof useQueryClient>, studentId: number) {
  qc.invalidateQueries({ queryKey: ['admin', 'student-card', 'promotion-status', studentId] });
  qc.invalidateQueries({ queryKey: ['admin', 'students', 'detail', studentId] });
  qc.invalidateQueries({ queryKey: ['admin', 'students', 'list'] });
}

export function usePromoteStudent() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ studentId, targetLevel }: { studentId: number; targetLevel?: number }) => {
      try {
        const body = targetLevel != null ? { targetLevel } : {};
        await apiClient.post(`${BASE}/${studentId}/promote`, body);
      } catch (err) {
        throw asBackendError(err, 'Sınıf atlatma işlemi başarısız oldu.');
      }
    },
    onSuccess: (_d, { studentId }) => invalidatePromotionQueries(qc, studentId),
  });
}

export function useRollbackPromotion() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ studentId }: { studentId: number }) => {
      try {
        await apiClient.post(`${BASE}/${studentId}/promotion-rollback`);
      } catch (err) {
        throw asBackendError(err, 'Sınıf düşürme işlemi başarısız oldu.');
      }
    },
    onSuccess: (_d, { studentId }) => invalidatePromotionQueries(qc, studentId),
  });
}
