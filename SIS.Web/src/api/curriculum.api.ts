import apiClient from './axios';
import type {
  CurriculumListDto,
  CurriculumDetailDto,
  CurriculumCourseItemDto,
  CreateCurriculumRequest,
  UpdateCurriculumRequest,
  AddCourseRequest,
  UpdateCourseRequest,
  CopyCurriculumRequest,
  CurriculumElectivePoolDto,
  CreatePoolRequest,
  UpdatePoolRequest,
} from '../types/curriculumManagement';
import type {
  CurriculumTermRuleDto,
  CreateTermRuleRequest,
  UpdateTermRuleRequest,
} from '../types/curriculumTermRule';

const BASE = '/curriculum';

export const curriculumApi = {
  list: (filters?: { programId?: number; departmentId?: number; facultyId?: number }) => {
    const params: Record<string, number> = {};
    if (filters?.programId) params.programId = filters.programId;
    else if (filters?.departmentId) params.departmentId = filters.departmentId;
    else if (filters?.facultyId) params.facultyId = filters.facultyId;
    return apiClient.get<CurriculumListDto[]>(BASE, {
      params: Object.keys(params).length ? params : undefined,
    }).then(r => r.data);
  },

  get: (id: number) =>
    apiClient.get<CurriculumDetailDto>(`${BASE}/${id}`).then(r => r.data),

  create: (req: CreateCurriculumRequest) =>
    apiClient.post<CurriculumListDto>(BASE, req).then(r => r.data),

  update: (id: number, req: UpdateCurriculumRequest) =>
    apiClient.put(`${BASE}/${id}`, req).then(r => r.data),

  delete: (id: number) =>
    apiClient.delete(`${BASE}/${id}`),

  listCourses: (id: number) =>
    apiClient.get<CurriculumCourseItemDto[]>(`${BASE}/${id}/courses`).then(r => r.data),

  addCourse: (id: number, req: AddCourseRequest) =>
    apiClient.post<CurriculumCourseItemDto>(`${BASE}/${id}/courses`, req).then(r => r.data),

  updateCourse: (id: number, courseId: number, req: UpdateCourseRequest) =>
    apiClient.put(`${BASE}/${id}/courses/${courseId}`, req).then(r => r.data),

  removeCourse: (id: number, courseId: number) =>
    apiClient.delete(`${BASE}/${id}/courses/${courseId}`),

  activate: (id: number) =>
    apiClient.post(`${BASE}/${id}/activate`).then(r => r.data),

  publish: (id: number) =>
    apiClient.post(`${BASE}/${id}/publish`).then(r => r.data),

  archive: (id: number) =>
    apiClient.post(`${BASE}/${id}/archive`).then(r => r.data),

  copy: (id: number, req: CopyCurriculumRequest) =>
    apiClient.post<CurriculumDetailDto>(`${BASE}/${id}/copy`, req).then(r => r.data),

  listPools: (id: number) =>
    apiClient.get<CurriculumElectivePoolDto[]>(`${BASE}/${id}/elective-pools`).then(r => r.data),

  createPool: (id: number, req: CreatePoolRequest) =>
    apiClient.post<CurriculumElectivePoolDto>(`${BASE}/${id}/elective-pools`, req).then(r => r.data),

  updatePool: (id: number, poolId: number, req: UpdatePoolRequest) =>
    apiClient.put(`${BASE}/${id}/elective-pools/${poolId}`, req).then(r => r.data),

  deletePool: (id: number, poolId: number) =>
    apiClient.delete(`${BASE}/${id}/elective-pools/${poolId}`),

  // ── Müfredat Dönem Kuralları ──
  listTermRules: (curriculumId: number) =>
    apiClient.get<CurriculumTermRuleDto[]>(`${BASE}/${curriculumId}/term-rules`).then(r => r.data),

  createTermRule: (curriculumId: number, req: CreateTermRuleRequest) =>
    apiClient.post<CurriculumTermRuleDto>(`${BASE}/${curriculumId}/term-rules`, req).then(r => r.data),

  updateTermRule: (curriculumId: number, ruleId: number, req: UpdateTermRuleRequest) =>
    apiClient.put(`${BASE}/${curriculumId}/term-rules/${ruleId}`, req).then(r => r.data),

  deleteTermRule: (curriculumId: number, ruleId: number) =>
    apiClient.delete(`${BASE}/${curriculumId}/term-rules/${ruleId}`),
};
