import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import {
  getPendingApprovals,
  approveStudent,
  rejectStudent,
  getAdvisorDashboardStats,
  getAdvisorStudents,
  getAdvisorStudentRegistrations,
  getAdvisorStudentCatalog,
  advisorAddCourse,
  advisorDropCourse,
  getAdvisorStudentCourseHistory,
  getAdvisorStudentCurriculumStatus,
  getAdvisorStudentTranscript,
  getAdvisorStudentTermGpa,
} from '../api/advisor.api';

export function usePendingApprovals() {
  return useQuery({
    queryKey: ['advisor', 'pending-approvals'],
    queryFn: getPendingApprovals,
  });
}

export function useAdvisorDashboardStats() {
  return useQuery({
    queryKey: ['advisor', 'dashboard-stats'],
    queryFn: getAdvisorDashboardStats,
    staleTime: 60_000,
  });
}

export function useAdvisorStudents() {
  return useQuery({
    queryKey: ['advisor', 'students'],
    queryFn: getAdvisorStudents,
  });
}

export function useAdvisorStudentRegistrations(studentId: number | null) {
  return useQuery({
    queryKey: ['advisor', 'student-registrations', studentId],
    queryFn: () => getAdvisorStudentRegistrations(studentId!),
    enabled: studentId != null,
  });
}

export function useAdvisorStudentCatalog(studentId: number | null) {
  return useQuery({
    queryKey: ['advisor', 'student-catalog', studentId],
    queryFn: () => getAdvisorStudentCatalog(studentId!),
    enabled: studentId != null,
  });
}

// ── Danışan bilgi görünümleri (Ek/Diğer İşlemler) — yalnızca ilgili görünüm açıkken çek ──

export function useAdvisorStudentCourseHistory(studentId: number | null, enabled: boolean) {
  return useQuery({
    queryKey: ['advisor', 'student-course-history', studentId],
    queryFn: () => getAdvisorStudentCourseHistory(studentId!),
    enabled: studentId != null && enabled,
  });
}

export function useAdvisorStudentCurriculumStatus(studentId: number | null, enabled: boolean) {
  return useQuery({
    queryKey: ['advisor', 'student-curriculum-status', studentId],
    queryFn: () => getAdvisorStudentCurriculumStatus(studentId!),
    enabled: studentId != null && enabled,
  });
}

export function useAdvisorStudentTranscript(studentId: number | null, enabled: boolean) {
  return useQuery({
    queryKey: ['advisor', 'student-transcript', studentId],
    queryFn: () => getAdvisorStudentTranscript(studentId!),
    enabled: studentId != null && enabled,
  });
}

export function useAdvisorStudentTermGpa(studentId: number | null, enabled: boolean) {
  return useQuery({
    queryKey: ['advisor', 'student-term-gpa', studentId],
    queryFn: () => getAdvisorStudentTermGpa(studentId!),
    enabled: studentId != null && enabled,
  });
}

export function useAdvisorAddCourse() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ studentId, courseSectionId }: { studentId: number; courseSectionId: number }) =>
      advisorAddCourse(studentId, courseSectionId),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['advisor'] }),
  });
}

export function useAdvisorDropCourse() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ studentId, registrationId }: { studentId: number; registrationId: number }) =>
      advisorDropCourse(studentId, registrationId),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['advisor'] }),
  });
}

export function useApproveStudent() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ studentId, note }: { studentId: number; note?: string }) =>
      approveStudent(studentId, note),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['advisor'] }),
  });
}

export function useRejectStudent() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ studentId, note }: { studentId: number; note?: string }) =>
      rejectStudent(studentId, note),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['advisor'] }),
  });
}
