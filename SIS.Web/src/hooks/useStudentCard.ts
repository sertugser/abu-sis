import { useQuery } from '@tanstack/react-query';
import apiClient from '../api/axios';
import type { StudentRegistrationDto, CourseHistoryTermDto, StudentAttendanceStatusDto } from '../types/registration';
import type { StudentTermGpaDto } from '../types/student';

const BASE = '/admin/students';

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
