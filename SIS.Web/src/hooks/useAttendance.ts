import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  closeQrAttendance,
  createAttendanceSession,
  getAttendanceMatrix,
  getAttendanceSession,
  getQrAttendanceSession,
  openQrAttendance,
  saveAttendanceRecords,
  submitQrAttendance,
  type AttendanceSession,
  type CreateAttendanceSessionPayload,
  type OpenQrPayload,
  type SaveAttendancePayload,
} from '../api/attendance.api';

export const attendanceKeys = {
  session: (id?: number | null) => ['attendance', 'session', id] as const,
  matrix: (sectionId?: number | null) => ['attendance', 'matrix', sectionId] as const,
  qr: (token?: string) => ['attendance', 'qr', token] as const,
};

export function useAttendanceSession(sessionId: number | null) {
  return useQuery({
    queryKey: attendanceKeys.session(sessionId),
    queryFn: () => getAttendanceSession(sessionId!),
    enabled: sessionId !== null,
    refetchInterval: query => {
      const data = query.state.data as AttendanceSession | undefined;
      return data?.isQrCurrentlyActive ? 5000 : false;
    },
  });
}

export function useAttendanceMatrix(sectionId: number | null) {
  return useQuery({
    queryKey: attendanceKeys.matrix(sectionId),
    queryFn: () => getAttendanceMatrix(sectionId!),
    enabled: sectionId !== null,
  });
}

export function useCreateAttendanceSession() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (payload: CreateAttendanceSessionPayload) => createAttendanceSession(payload),
    onSuccess: data => {
      queryClient.setQueryData(attendanceKeys.session(data.id), data);
      queryClient.invalidateQueries({ queryKey: attendanceKeys.matrix(data.courseSectionId) });
    },
  });
}

export function useSaveAttendanceRecords(sessionId: number | null) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (payload: SaveAttendancePayload) => saveAttendanceRecords(sessionId!, payload),
    onSuccess: data => {
      queryClient.setQueryData(attendanceKeys.session(data.id), data);
      queryClient.invalidateQueries({ queryKey: attendanceKeys.matrix(data.courseSectionId) });
    },
  });
}

export function useOpenQrAttendance(sessionId: number | null) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (payload: OpenQrPayload) => openQrAttendance(sessionId!, payload),
    onSuccess: data => {
      queryClient.setQueryData(attendanceKeys.session(data.id), data);
      queryClient.invalidateQueries({ queryKey: attendanceKeys.matrix(data.courseSectionId) });
    },
  });
}

export function useCloseQrAttendance(sessionId: number | null) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => closeQrAttendance(sessionId!),
    onSuccess: data => {
      queryClient.setQueryData(attendanceKeys.session(data.id), data);
      queryClient.invalidateQueries({ queryKey: attendanceKeys.matrix(data.courseSectionId) });
    },
  });
}

export function useQrAttendanceSession(token?: string) {
  return useQuery({
    queryKey: attendanceKeys.qr(token),
    queryFn: () => getQrAttendanceSession(token!),
    enabled: Boolean(token),
    refetchInterval: query => (query.state.data?.isActive ? 10000 : false),
  });
}

export function useSubmitQrAttendance(token?: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => submitQrAttendance(token!),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: attendanceKeys.qr(token) });
    },
  });
}
