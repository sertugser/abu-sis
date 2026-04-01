import apiClient from './axios';

export interface PromotionRuleDto {
  id: number;
  programId: number;
  programName?: string;
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
  fromYear: number;
  toYear: number;
  minCgpa?: number | null;
  minEcts?: number | null;
  minCourseCount?: number | null;
  maxFailedCourses?: number | null;
  minCompulsoryCoursePassed?: number | null;
}

export interface UpdatePromotionRuleDto {
  fromYear: number;
  toYear: number;
  minCgpa?: number | null;
  minEcts?: number | null;
  minCourseCount?: number | null;
  maxFailedCourses?: number | null;
  minCompulsoryCoursePassed?: number | null;
}

export async function getPromotionRules(programId?: number): Promise<PromotionRuleDto[]> {
  const params = programId ? { programId } : undefined;
  const { data } = await apiClient.get<PromotionRuleDto[]>('/admin/promotion-rules', { params });
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
