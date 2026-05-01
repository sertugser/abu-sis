import apiClient from './axios';
import type { CurriculumCourseOpeningReasonDto } from '../types/curriculumCourseOpeningReason';
import type { CourseCategoryDto } from '../types/courseCategory';

const BASE = '/admin/course-lookups';

export const courseLookupsApi = {
  getOpeningReasons: () =>
    apiClient.get<CurriculumCourseOpeningReasonDto[]>(`${BASE}/opening-reasons`).then(r => r.data),

  getCategories: () =>
    apiClient.get<CourseCategoryDto[]>(`${BASE}/categories`).then(r => r.data),
};
