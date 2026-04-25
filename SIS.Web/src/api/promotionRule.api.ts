import apiClient from './axios';

export interface PromotionRuleDto {
  id: number;
  programId: number;
  programName?: string;
  // γ hibrit: null = program-bazlı default, set = müfredat-spesifik override
  curriculumId: number | null;
  curriculumVersion: string | null;
  fromYear: number;
  toYear: number;
  minCgpa: number | null;
  minEcts: number | null;
  minCourseCount: number | null;
  maxFailedCourses: number | null;
  minCompulsoryCoursePassed: number | null;
}

export interface CreatePromotionRuleDto {
  programId: number;
  curriculumId?: number | null;
  fromYear: number;
  toYear: number;
  minCgpa?: number | null;
  minEcts?: number | null;
  minCourseCount?: number | null;
  maxFailedCourses?: number | null;
  minCompulsoryCoursePassed?: number | null;
}

export interface UpdatePromotionRuleDto {
  curriculumId?: number | null;
  fromYear: number;
  toYear: number;
  minCgpa?: number | null;
  minEcts?: number | null;
  minCourseCount?: number | null;
  maxFailedCourses?: number | null;
  minCompulsoryCoursePassed?: number | null;
}

export interface PromotionRuleFilter {
  programId?: number;
  curriculumId?: number;
}

export async function getPromotionRules(filter?: PromotionRuleFilter): Promise<PromotionRuleDto[]> {
  const params: Record<string, number> = {};
  if (filter?.programId !== undefined) params.programId = filter.programId;
  if (filter?.curriculumId !== undefined) params.curriculumId = filter.curriculumId;
  const { data } = await apiClient.get<PromotionRuleDto[]>('/admin/promotion-rules', {
    params: Object.keys(params).length > 0 ? params : undefined,
  });
  return data;
}

export async function createPromotionRule(dto: CreatePromotionRuleDto): Promise<PromotionRuleDto> {
  const { data } = await apiClient.post<PromotionRuleDto>('/admin/promotion-rules', dto);
  return data;
}

export async function updatePromotionRule(id: number, dto: UpdatePromotionRuleDto): Promise<PromotionRuleDto> {
  const { data } = await apiClient.put<PromotionRuleDto>(`/admin/promotion-rules/${id}`, dto);
  return data;
}

export async function deletePromotionRule(id: number): Promise<void> {
  await apiClient.delete(`/admin/promotion-rules/${id}`);
}
