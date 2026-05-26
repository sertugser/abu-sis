import apiClient from './axios';

// ── DTOs ─────────────────────────────────────────────────────

export interface ReportCategoryDto {
  id: number;
  code: string;
  name: string;
  reportCount: number;
}
export interface CreateReportCategoryDto { code: string; name: string }
export interface UpdateReportCategoryDto { name: string }

export interface ReportDefinitionDto {
  id: number;
  code: string;
  name: string;
  description: string | null;
  categoryId: number;
  categoryCode: string;
  categoryName: string;
}
export interface CreateReportDefinitionDto {
  code: string;
  name: string;
  description?: string | null;
  categoryId: number;
}
export interface UpdateReportDefinitionDto {
  name: string;
  description?: string | null;
  categoryId: number;
}

// ── Kullanıcı grupları ──
export interface ReportUserGroupDto { id: number; name: string; description: string | null; memberCount: number }
export interface CreateReportUserGroupDto { name: string; description?: string | null }
export interface UpdateReportUserGroupDto { name: string; description?: string | null }
export interface ReportUserGroupMemberDto { userId: number; fullName: string; email: string; addedAt: string }

// ── Birleşik yetki (grant) ──
// scopeKind: 0=Kategori 1=Rapor | subjectKind: 0=Rol 1=Kişi 2=Grup | effect: 0=İzin 1=Yasak
export interface ReportAccessGrantDto {
  id: number;
  scopeKind: number; targetId: number; targetLabel: string;
  subjectKind: number; subjectId: number; subjectLabel: string;
  effect: number; reason: string | null; grantedAt: string;
}
export interface SetReportAccessGrantDto {
  scopeKind: number; targetId: number; subjectKind: number; subjectId: number; effect: number; reason?: string | null;
}

export interface AccessibleReportDto {
  id: number;
  code: string;
  name: string;
  categoryId: number;
  categoryName: string;
}

// ── API ──────────────────────────────────────────────────────

export const reportAuthorizationApi = {
  // Categories
  listCategories: async (): Promise<ReportCategoryDto[]> => {
    const { data } = await apiClient.get<ReportCategoryDto[]>('/admin/report-categories');
    return data;
  },
  createCategory: async (dto: CreateReportCategoryDto): Promise<ReportCategoryDto> => {
    const { data } = await apiClient.post<ReportCategoryDto>('/admin/report-categories', dto);
    return data;
  },
  updateCategory: async (id: number, dto: UpdateReportCategoryDto): Promise<ReportCategoryDto> => {
    const { data } = await apiClient.put<ReportCategoryDto>(`/admin/report-categories/${id}`, dto);
    return data;
  },
  deleteCategory: async (id: number): Promise<void> => {
    await apiClient.delete(`/admin/report-categories/${id}`);
  },

  // Reports
  listReports: async (categoryId?: number): Promise<ReportDefinitionDto[]> => {
    const { data } = await apiClient.get<ReportDefinitionDto[]>('/admin/reports', {
      params: { categoryId },
    });
    return data;
  },
  createReport: async (dto: CreateReportDefinitionDto): Promise<ReportDefinitionDto> => {
    const { data } = await apiClient.post<ReportDefinitionDto>('/admin/reports', dto);
    return data;
  },
  updateReport: async (id: number, dto: UpdateReportDefinitionDto): Promise<ReportDefinitionDto> => {
    const { data } = await apiClient.put<ReportDefinitionDto>(`/admin/reports/${id}`, dto);
    return data;
  },
  deleteReport: async (id: number): Promise<void> => {
    await apiClient.delete(`/admin/reports/${id}`);
  },

  // Groups
  listGroups: async (): Promise<ReportUserGroupDto[]> => (await apiClient.get('/admin/report-user-groups')).data,
  createGroup: async (dto: CreateReportUserGroupDto): Promise<ReportUserGroupDto> => (await apiClient.post('/admin/report-user-groups', dto)).data,
  updateGroup: async (id: number, dto: UpdateReportUserGroupDto): Promise<ReportUserGroupDto> => (await apiClient.put(`/admin/report-user-groups/${id}`, dto)).data,
  deleteGroup: async (id: number): Promise<void> => { await apiClient.delete(`/admin/report-user-groups/${id}`); },
  listGroupMembers: async (id: number): Promise<ReportUserGroupMemberDto[]> => (await apiClient.get(`/admin/report-user-groups/${id}/members`)).data,
  addGroupMember: async (id: number, userId: number): Promise<void> => { await apiClient.post(`/admin/report-user-groups/${id}/members`, { userId }); },
  removeGroupMember: async (id: number, userId: number): Promise<void> => { await apiClient.delete(`/admin/report-user-groups/${id}/members/${userId}`); },

  // Grants
  listGrants: async (): Promise<ReportAccessGrantDto[]> => (await apiClient.get('/admin/report-grants')).data,
  setGrant: async (dto: SetReportAccessGrantDto): Promise<ReportAccessGrantDto> => (await apiClient.post('/admin/report-grants', dto)).data,
  deleteGrant: async (id: number): Promise<void> => { await apiClient.delete(`/admin/report-grants/${id}`); },
};
