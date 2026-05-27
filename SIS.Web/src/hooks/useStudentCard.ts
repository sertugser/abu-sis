import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import apiClient from '../api/axios';
import type { StudentRegistrationDto, CourseHistoryTermDto, StudentAttendanceStatusDto } from '../types/registration';
import type { TermGpaHistoryDto } from '../types/student';
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
 * Fetch the student's profile photo as a Blob and expose it as a per-instance
 * object URL.
 *
 * The `<img src>` must NOT point at the raw API URL because that request would
 * not carry the bearer token; we fetch via apiClient (which injects the token)
 * and wrap the blob in an object URL instead.
 *
 * The shared TanStack Query cache holds the *Blob*, not the URL string. Each
 * hook instance derives its OWN object URL from that blob and owns that URL's
 * lifecycle. This prevents the cross-subscriber revocation race: when two
 * components share the photo key (e.g. the always-mounted header + the panel on
 * the `genel` route), one unmounting can no longer revoke a URL the other is
 * still rendering — each instance only ever revokes the URL it created.
 */
export function useStudentPhoto(studentId: number | null, hasPhoto: boolean) {
  const query = useQuery({
    queryKey: ['admin', 'student-card', 'photo', studentId],
    queryFn: async () => {
      try {
        const { data } = await apiClient.get<Blob>(`${BASE}/${studentId}/photo`, {
          responseType: 'blob',
        });
        return data;
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
  });

  const blob = query.data ?? null;
  const [objectUrl, setObjectUrl] = useState<string | null>(null);

  // Derive a per-instance object URL from the cached blob. When the blob
  // changes, revoke the previous url and create a fresh one; on unmount revoke
  // the current one. Only this instance's url is ever revoked here.
  // setState-in-effect is intentional: URL.createObjectURL is a platform-API
  // side effect whose result must be stored to render and revoked on cleanup.
  /* eslint-disable react-hooks/set-state-in-effect */
  useEffect(() => {
    if (!blob) {
      setObjectUrl(null);
      return;
    }
    const url = URL.createObjectURL(blob);
    setObjectUrl(url);
    return () => {
      URL.revokeObjectURL(url);
    };
  }, [blob]);
  /* eslint-enable react-hooks/set-state-in-effect */

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

export interface StudentDocumentStatus {
  documentTypeId: number;
  code: string;
  name: string;
  isRequired: boolean;
  sortOrder: number;
  uploaded: boolean;
  documentId: number | null;
  originalFileName: string | null;
  uploadedAt: string | null;
}

export function useStudentDocuments(studentId: number | null) {
  return useQuery({
    queryKey: ['admin', 'student-card', 'documents', studentId],
    queryFn: async () =>
      (await apiClient.get<StudentDocumentStatus[]>(`${BASE}/${studentId}/documents`)).data,
    enabled: studentId != null,
  });
}

function invalidateDocumentQueries(qc: ReturnType<typeof useQueryClient>, studentId: number) {
  qc.invalidateQueries({ queryKey: ['admin', 'student-card', 'documents', studentId] });
  qc.invalidateQueries({ queryKey: ['admin', 'students', 'detail', studentId] });
}

export function useUploadStudentDocument() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({
      studentId,
      documentTypeId,
      file,
    }: {
      studentId: number;
      documentTypeId: number;
      file: File;
    }) => {
      const formData = new FormData();
      formData.append('file', file);
      try {
        // Pass Content-Type undefined so axios sets the multipart boundary itself
        // (the client default is application/json, which would break the upload).
        await apiClient.post(`${BASE}/${studentId}/documents/${documentTypeId}`, formData, {
          headers: { 'Content-Type': undefined },
        });
      } catch (err) {
        throw asBackendError(err, 'Belge yüklenemedi.');
      }
    },
    onSuccess: (_d, { studentId }) => invalidateDocumentQueries(qc, studentId),
  });
}

export function useDeleteStudentDocument() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ studentId, documentId }: { studentId: number; documentId: number }) => {
      try {
        await apiClient.delete(`${BASE}/${studentId}/documents/${documentId}`);
      } catch (err) {
        throw asBackendError(err, 'Belge silinemedi.');
      }
    },
    onSuccess: (_d, { studentId }) => invalidateDocumentQueries(qc, studentId),
  });
}

/**
 * Download a student document. The GET endpoint is auth-guarded, so the file
 * cannot be linked via a raw `<a href>` (no bearer token). Fetch the bytes as a
 * blob through apiClient, wrap them in an object URL, trigger a download via a
 * temporary anchor carrying the original file name, then revoke the URL.
 */
export async function downloadStudentDocument(
  studentId: number,
  documentId: number,
  fileName?: string | null,
): Promise<void> {
  const { data } = await apiClient.get<Blob>(`${BASE}/${studentId}/documents/${documentId}`, {
    responseType: 'blob',
  });
  const objectUrl = URL.createObjectURL(data);
  try {
    const anchor = document.createElement('a');
    anchor.href = objectUrl;
    anchor.download = fileName ?? `belge-${documentId}`;
    document.body.appendChild(anchor);
    anchor.click();
    anchor.remove();
  } finally {
    URL.revokeObjectURL(objectUrl);
  }
}

export interface IssuedDocument {
  id: number;
  kind: string;
  kindLabel: string;
  issuedAt: string;
  documentNumber: string | null;
  issuedByName: string | null;
}

/** History of documents issued for a student (newest-first). */
export function useIssuedDocuments(studentId: number | null) {
  return useQuery({
    queryKey: ['admin', 'student-card', 'issued-documents', studentId],
    queryFn: async () =>
      (await apiClient.get<IssuedDocument[]>(`${BASE}/${studentId}/issued-documents`)).data,
    enabled: studentId != null,
  });
}

/**
 * Fetch an auth-guarded PDF as a blob (the bearer token cannot ride a raw
 * `<a href>`), wrap it in an object URL, trigger a download via a temporary
 * anchor carrying the given file name, then revoke the URL.
 */
async function downloadGeneratedPdf(url: string, fileName: string): Promise<void> {
  const { data } = await apiClient.get<Blob>(url, { responseType: 'blob' });
  const objectUrl = URL.createObjectURL(data);
  try {
    const anchor = document.createElement('a');
    anchor.href = objectUrl;
    anchor.download = fileName;
    document.body.appendChild(anchor);
    anchor.click();
    anchor.remove();
  } finally {
    URL.revokeObjectURL(objectUrl);
  }
}

/**
 * Generate + download the student info form PDF, then refresh the issuance
 * history (the server logs an issuance as a side effect of generating).
 */
export function useGenerateStudentInfoForm() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ studentId }: { studentId: number }) => {
      try {
        await downloadGeneratedPdf(
          `${BASE}/${studentId}/documents/student-info-form`,
          'ogrenci-bilgi-formu.pdf',
        );
      } catch (err) {
        throw asBackendError(err, 'Öğrenci bilgi formu üretilemedi.');
      }
    },
    onSuccess: (_d, { studentId }) =>
      qc.invalidateQueries({ queryKey: ['admin', 'student-card', 'issued-documents', studentId] }),
  });
}

/**
 * Generate + download the enrollment certificate PDF, then refresh the
 * issuance history.
 */
export function useGenerateEnrollmentCertificate() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ studentId }: { studentId: number }) => {
      try {
        await downloadGeneratedPdf(
          `${BASE}/${studentId}/documents/enrollment-certificate`,
          'ogrenci-belgesi.pdf',
        );
      } catch (err) {
        throw asBackendError(err, 'Öğrenci belgesi üretilemedi.');
      }
    },
    onSuccess: (_d, { studentId }) =>
      qc.invalidateQueries({ queryKey: ['admin', 'student-card', 'issued-documents', studentId] }),
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
      const { data } = await apiClient.get<TermGpaHistoryDto>(`${BASE}/${studentId}/term-gpa`);
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

export interface ScholarshipDetail {
  id: number;
  type: string;
  typeLabel: string;
  ratePercent: number | null;
  source: string | null;
  startDate: string | null;
  endDate: string | null;
  isActive: boolean;
  note: string | null;
}

export interface UpsertScholarship {
  type: string;
  ratePercent: number | null;
  source: string | null;
  startDate: string | null;
  endDate: string | null;
  isActive: boolean;
  note: string | null;
}

const SCHOLARSHIPS_KEY = (studentId: number) =>
  ['admin', 'student-card', 'scholarships', studentId] as const;

export function useScholarships(studentId: number | null) {
  return useQuery({
    queryKey: SCHOLARSHIPS_KEY(studentId ?? 0),
    queryFn: async () =>
      (await apiClient.get<ScholarshipDetail[]>(`${BASE}/${studentId}/scholarships`)).data,
    enabled: studentId != null,
  });
}

export function useCreateScholarship() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ studentId, dto }: { studentId: number; dto: UpsertScholarship }) => {
      try {
        await apiClient.post(`${BASE}/${studentId}/scholarships`, dto);
      } catch (err) {
        throw asBackendError(err, 'Burs kaydı eklenemedi.');
      }
    },
    onSuccess: (_d, { studentId }) =>
      qc.invalidateQueries({ queryKey: SCHOLARSHIPS_KEY(studentId) }),
  });
}

export function useUpdateScholarship() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({
      studentId,
      id,
      dto,
    }: {
      studentId: number;
      id: number;
      dto: UpsertScholarship;
    }) => {
      try {
        await apiClient.put(`${BASE}/${studentId}/scholarships/${id}`, dto);
      } catch (err) {
        throw asBackendError(err, 'Burs kaydı güncellenemedi.');
      }
    },
    onSuccess: (_d, { studentId }) =>
      qc.invalidateQueries({ queryKey: SCHOLARSHIPS_KEY(studentId) }),
  });
}

export function useDeleteScholarship() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ studentId, id }: { studentId: number; id: number }) => {
      try {
        await apiClient.delete(`${BASE}/${studentId}/scholarships/${id}`);
      } catch (err) {
        throw asBackendError(err, 'Burs kaydı silinemedi.');
      }
    },
    onSuccess: (_d, { studentId }) =>
      qc.invalidateQueries({ queryKey: SCHOLARSHIPS_KEY(studentId) }),
  });
}

export interface StudentPlacement {
  id: number | null;
  entranceType: string | null;
  entranceTypeLabel: string | null;
  scoreType: string | null;
  score: number | null;
  rankOrder: number | null;
  quotaType: string | null;
  placementYear: number | null;
  preferenceOrder: number | null;
  placedProgramName: string | null;
  note: string | null;
}

export type UpdatePlacement = Omit<StudentPlacement, 'id' | 'entranceTypeLabel'>;

const PLACEMENT_KEY = (studentId: number) =>
  ['admin', 'student-card', 'placement', studentId] as const;

export function usePlacement(studentId: number | null) {
  return useQuery({
    queryKey: PLACEMENT_KEY(studentId ?? 0),
    queryFn: async () =>
      (await apiClient.get<StudentPlacement>(`${BASE}/${studentId}/placement`)).data,
    enabled: studentId != null,
  });
}

export function useUpdatePlacement() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ studentId, dto }: { studentId: number; dto: UpdatePlacement }) => {
      try {
        await apiClient.put(`${BASE}/${studentId}/placement`, dto);
      } catch (err) {
        throw asBackendError(err, 'Yerleştirme bilgileri güncellenemedi.');
      }
    },
    onSuccess: (_d, { studentId }) =>
      qc.invalidateQueries({ queryKey: PLACEMENT_KEY(studentId) }),
  });
}

// ─── NVİ / YÖKSİS / ÖSYM integration (mock-capable) ────────────────────────────
// These calls are user-triggered actions (button clicks), so they are modelled as
// mutations rather than queries — they must NOT auto-run on mount.

/** NVİ identity verification result. `isMock` true when the mock provider answered. */
export interface IdentityVerificationResult {
  isMock: boolean;
  matched: boolean;
  fullName: string | null;
  birthDate: string | null;
  birthPlace: string | null;
  addressProvince: string | null;
  addressDistrict: string | null;
  mode: string;
  queriedAt: string;
  message: string | null;
}

/** YÖKSİS enrollment-notification status result. */
export interface YoksisStatusResult {
  isMock: boolean;
  status: string;
  yoksisRecordNo: string | null;
  queriedAt: string;
  mode: string;
  message: string | null;
}

/** ÖSYM placement preview result (server does NOT save it; the panel saves via placement PUT). */
export interface OsymPlacementResult {
  isMock: boolean;
  entranceType: string;
  scoreType: string | null;
  score: number | null;
  rankOrder: number | null;
  quotaType: string | null;
  placementYear: number | null;
  placedProgramName: string | null;
  queriedAt: string;
  mode: string;
  message: string | null;
}

export function useVerifyNvi() {
  return useMutation({
    mutationFn: async ({ studentId }: { studentId: number }) => {
      try {
        const { data } = await apiClient.post<IdentityVerificationResult>(
          `${BASE}/${studentId}/integration/nvi/verify`,
        );
        return data;
      } catch (err) {
        throw asBackendError(err, 'NVİ sorgusu yapılamadı.');
      }
    },
  });
}

export function useYoksisStatus() {
  return useMutation({
    mutationFn: async ({ studentId }: { studentId: number }) => {
      try {
        const { data } = await apiClient.get<YoksisStatusResult>(
          `${BASE}/${studentId}/integration/yoksis/status`,
        );
        return data;
      } catch (err) {
        throw asBackendError(err, 'YÖKSİS durumu alınamadı.');
      }
    },
  });
}

export function useSubmitYoksis() {
  return useMutation({
    mutationFn: async ({ studentId }: { studentId: number }) => {
      try {
        const { data } = await apiClient.post<YoksisStatusResult>(
          `${BASE}/${studentId}/integration/yoksis/submit`,
        );
        return data;
      } catch (err) {
        throw asBackendError(err, 'YÖKSİS bildirimi yapılamadı.');
      }
    },
  });
}

export function useFetchOsymPlacement() {
  return useMutation({
    mutationFn: async ({ studentId }: { studentId: number }) => {
      try {
        const { data } = await apiClient.post<OsymPlacementResult>(
          `${BASE}/${studentId}/integration/osym/fetch-placement`,
        );
        return data;
      } catch (err) {
        throw asBackendError(err, 'ÖSYM yerleştirme sorgusu yapılamadı.');
      }
    },
  });
}
