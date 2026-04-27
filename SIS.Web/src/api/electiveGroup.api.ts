import apiClient from './axios';
import type {
  ElectiveGroupListDto,
  ElectiveGroupDetailDto,
  ElectiveGroupCourseDto,
  CreateElectiveGroupRequest,
  UpdateElectiveGroupRequest,
  AddCoursesToGroupRequest,
  ElectiveSubtype,
  CourseAccessRuleDto,
  CreateAccessRuleRequest,
} from '../types/electiveGroup';

const BASE = '/elective-groups';

export const electiveGroupApi = {
  /**
   * Faz B B3c-3: subtypeId (FK) paralel filter param. Backend FK öncelikli;
   * subtype enum geri-uyum için tutuluyor.
   */
  list: (
    subtypeId?: number,
    facultyId?: number,
    departmentId?: number,
    isActive?: boolean,
    subtype?: ElectiveSubtype,
  ) =>
    apiClient.get<ElectiveGroupListDto[]>(BASE, {
      params: { subtypeId, facultyId, departmentId, isActive, subtype },
    }).then(r => r.data),

  get: (id: number) =>
    apiClient.get<ElectiveGroupDetailDto>(`${BASE}/${id}`).then(r => r.data),

  create: (req: CreateElectiveGroupRequest) =>
    apiClient.post<ElectiveGroupListDto>(BASE, req).then(r => r.data),

  update: (id: number, req: UpdateElectiveGroupRequest) =>
    apiClient.put(`${BASE}/${id}`, req).then(r => r.data),

  delete: (id: number) =>
    apiClient.delete(`${BASE}/${id}`),

  listCourses: (id: number) =>
    apiClient.get<ElectiveGroupCourseDto[]>(`${BASE}/${id}/courses`).then(r => r.data),

  addCourses: (id: number, req: AddCoursesToGroupRequest) =>
    apiClient.post(`${BASE}/${id}/courses`, req).then(r => r.data),

  removeCourse: (id: number, courseId: number) =>
    apiClient.delete(`${BASE}/${id}/courses/${courseId}`),
};

export const courseAccessRuleApi = {
  list: (courseId: number) =>
    apiClient.get<CourseAccessRuleDto[]>(`/courses/${courseId}/access-rules`).then(r => r.data),

  create: (courseId: number, req: CreateAccessRuleRequest) =>
    apiClient.post<CourseAccessRuleDto>(`/courses/${courseId}/access-rules`, req).then(r => r.data),

  update: (courseId: number, ruleId: number, req: Partial<CreateAccessRuleRequest>) =>
    apiClient.put(`/courses/${courseId}/access-rules/${ruleId}`, req).then(r => r.data),

  delete: (courseId: number, ruleId: number) =>
    apiClient.delete(`/courses/${courseId}/access-rules/${ruleId}`),
};
