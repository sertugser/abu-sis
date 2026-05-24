import { useEffect } from 'react';
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

export interface StudentPersonalInfo {
  studentId: number;
  // Contact
  mobilePhone: string | null;
  phone: string | null;
  secondaryEmail: string | null;
  addressProvince: string | null;
  addressDistrict: string | null;
  addressLine: string | null;
  postalCode: string | null;
  emergencyContactName: string | null;
  emergencyContactPhone: string | null;
  emergencyContactRelation: string | null;
  // Demographic
  birthDate: string | null;
  birthPlace: string | null;
  gender: string | null;
  nationality: string | null;
  bloodType: string | null;
  maritalStatus: string | null;
  passportNo: string | null;
  // Military
  militaryStatus: string | null;
  militaryDeferralUntil: string | null;
  militaryOffice: string | null;
  // Photo
  photoExtension: string | null;
}

/** Write model (no studentId; that comes from the route). */
export type UpdateStudentPersonalInfo = Omit<StudentPersonalInfo, 'studentId'>;

export function useStudentPersonalInfo(studentId: number | null) {
  return useQuery({
    queryKey: ['admin', 'student-card', 'personal-info', studentId],
    queryFn: async () =>
      (await apiClient.get<StudentPersonalInfo>(`${BASE}/${studentId}/personal-info`)).data,
    enabled: studentId != null,
  });
}

export function useUpdateStudentPersonalInfo() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({
      studentId,
      dto,
    }: {
      studentId: number;
      dto: UpdateStudentPersonalInfo;
    }) => {
      try {
        await apiClient.put(`${BASE}/${studentId}/personal-info`, dto);
      } catch (err) {
        throw asBackendError(err, 'Kişisel bilgiler güncellenemedi.');
      }
    },
    onSuccess: (_d, { studentId }) => {
      qc.invalidateQueries({ queryKey: ['admin', 'student-card', 'personal-info', studentId] });
      qc.invalidateQueries({ queryKey: ['admin', 'students', 'detail', studentId] });
    },
  });
}

/**
 * Fetch the student's profile photo as a blob and expose it as an object URL.
 * The `<img src>` must NOT point at the raw API URL because that request would
 * not carry the bearer token; we fetch via apiClient (which injects the token)
 * and wrap the blob in an object URL instead. The previous object URL is revoked
 * whenever the query produces a new one and on unmount, to avoid leaks.
 */
export function useStudentPhoto(studentId: number | null, hasPhoto: boolean) {
  const query = useQuery({
    queryKey: ['admin', 'student-card', 'photo', studentId],
    queryFn: async () => {
      try {
        const { data } = await apiClient.get<Blob>(`${BASE}/${studentId}/photo`, {
          responseType: 'blob',
        });
        return URL.createObjectURL(data);
      } catch (err) {
        // No photo on the server (404) → treat as "no photo" rather than an error state.
        if (
          typeof err === 'object' &&
          err !== null &&
          'response' in err &&
          (err as { response?: { status?: number } }).response?.status === 404
        ) {
          return null;
        }
        throw err;
      }
    },
    enabled: studentId != null && hasPhoto,
    staleTime: Infinity,
    gcTime: 0,
  });

  const objectUrl = query.data ?? null;

  // Revoke the object URL when it changes (new fetch) or on unmount.
  useEffect(() => {
    if (!objectUrl) return;
    return () => {
      URL.revokeObjectURL(objectUrl);
    };
  }, [objectUrl]);

  return objectUrl;
}

function invalidatePhotoQueries(qc: ReturnType<typeof useQueryClient>, studentId: number) {
  qc.invalidateQueries({ queryKey: ['admin', 'student-card', 'photo', studentId] });
  qc.invalidateQueries({ queryKey: ['admin', 'student-card', 'personal-info', studentId] });
  qc.invalidateQueries({ queryKey: ['admin', 'students', 'detail', studentId] });
}

export function useUploadStudentPhoto() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ studentId, file }: { studentId: number; file: File }) => {
      const formData = new FormData();
      formData.append('file', file);
      try {
        // Pass Content-Type undefined so axios sets the multipart boundary itself
        // (the client default is application/json, which would break the upload).
        await apiClient.put(`${BASE}/${studentId}/photo`, formData, {
          headers: { 'Content-Type': undefined },
        });
      } catch (err) {
        throw asBackendError(err, 'Fotoğraf yüklenemedi.');
      }
    },
    onSuccess: (_d, { studentId }) => invalidatePhotoQueries(qc, studentId),
  });
}

export function useDeleteStudentPhoto() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ studentId }: { studentId: number }) => {
      try {
        await apiClient.delete(`${BASE}/${studentId}/photo`);
      } catch (err) {
        throw asBackendError(err, 'Fotoğraf kaldırılamadı.');
      }
    },
    onSuccess: (_d, { studentId }) => invalidatePhotoQueries(qc, studentId),
  });
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
