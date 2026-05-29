import apiClient from './axios';
import type {
  StudentTimetable,
  StudentTimetableTerm,
} from '@/types/studentTimetable';

export async function getStudentTimetable(termId?: number): Promise<StudentTimetable> {
  const url = termId !== undefined
    ? `/scheduling/student/me/timetable?termId=${termId}`
    : '/scheduling/student/me/timetable';
  const { data } = await apiClient.get<StudentTimetable>(url);
  return data;
}

export async function getStudentTimetableTerms(): Promise<StudentTimetableTerm[]> {
  const { data } = await apiClient.get<StudentTimetableTerm[]>(
    '/scheduling/student/me/timetable/terms',
  );
  return data;
}

export async function downloadStudentTimetablePdf(termId: number): Promise<Blob> {
  const response = await apiClient.get<Blob>(
    `/scheduling/student/me/timetable/pdf?termId=${termId}`,
    { responseType: 'blob' },
  );
  return response.data;
}
