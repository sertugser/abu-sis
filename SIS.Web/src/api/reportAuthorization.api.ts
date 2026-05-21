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

export interface CategoryRoleMatrixRowDto {
  categoryId: number;
  categoryCode: string;
  categoryName: string;
  allowedRoleIds: number[];
}
export interface SetCategoryRolesDto { categoryId: number; roleIds: number[] }

export interface ReportPermissionOverrideDto {
  id: number;
  reportId: number;
  reportCode: string;
  reportName: string;
  roleId: number;
  roleName: string;
  isAllowed: boolean;
  reason: string | null;
  createdAt: string;
}
export interface CreateReportOverrideDto {
  reportId: number;
  roleId: number;
  isAllowed: boolean;
  reason?: string | null;
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

  // Matrix
  getMatrix: async (): Promise<CategoryRoleMatrixRowDto[]> => {
    const { data } = await apiClient.get<CategoryRoleMatrixRowDto[]>('/admin/report-permissions/matrix');
    return data;
  },
  setCategoryRoles: async (dto: SetCategoryRolesDto): Promise<void> => {
    await apiClient.put('/admin/report-permissions/matrix', dto);
  },

  // Overrides
  listOverrides: async (reportId?: number): Promise<ReportPermissionOverrideDto[]> => {
    const { data } = await apiClient.get<ReportPermissionOverrideDto[]>(
      '/admin/report-permissions/overrides',
      { params: { reportId } },
    );
    return data;
  },
  upsertOverride: async (dto: CreateReportOverrideDto): Promise<ReportPermissionOverrideDto> => {
    const { data } = await apiClient.post<ReportPermissionOverrideDto>(
      '/admin/report-permissions/overrides',
      dto,
    );
    return data;
  },
  deleteOverride: async (id: number): Promise<void> => {
    await apiClient.delete(`/admin/report-permissions/overrides/${id}`);
  },
};
