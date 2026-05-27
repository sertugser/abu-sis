import apiClient from './axios';
import type { CurriculumStatusValue } from '../types/curriculumManagement';

const BASE = '/curriculum';

/** Müfredat seçici için özet (intibak sayfası sol panel). */
export interface IntibakCurriculumDto {
  id: number;
  programId: number;
  programName: string;
  programCode: string;
  facultyName: string;
  departmentName: string;
  version: string;
  name: string | null;
  isActive: boolean;
  status: CurriculumStatusValue;
  courseCount: number;
  mappedCount: number;
}

/** Seçili müfredatın dersi + mevcut intibak (önceki ders) eşleştirmesi. */
export interface IntibakCourseDto {
  courseId: number;
  courseCode: string;
  courseName: string;
  suggestedSemester: number;
  isCompulsory: boolean;
  nationalCredit: number;
  ects: number;
  previousCourseId: number | null;
  previousCourseCode: string | null;
  previousCourseName: string | null;
}

/** Ders detay panelindeki öğrenci istatistikleri. */
export interface CourseIntibakStatisticsDto {
  courseId: number;
  totalStudents: number;
  passedStudents: number;
  failedStudents: number;
  activeStudents: number;
  activePassedStudents: number;
  activeFailedStudents: number;
}

export interface IntibakBulkItem {
  courseId: number;
  previousCourseId: number | null;
}

export interface SaveIntibakResult {
  updated: number;
  message: string;
}

export const intibakApi = {
  listCurricula: () =>
    apiClient.get<IntibakCurriculumDto[]>(`${BASE}/intibak/curricula`).then(r => r.data),

  getCourses: (curriculumId: number) =>
    apiClient.get<IntibakCourseDto[]>(`${BASE}/${curriculumId}/intibak`).then(r => r.data),

  getCourseStatistics: (courseId: number) =>
    apiClient.get<CourseIntibakStatisticsDto>(`${BASE}/course/${courseId}/intibak-statistics`).then(r => r.data),

  save: (curriculumId: number, items: IntibakBulkItem[]) =>
    apiClient.put<SaveIntibakResult>(`${BASE}/${curriculumId}/intibak`, { items }).then(r => r.data),
};
