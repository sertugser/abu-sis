import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  reportAuthorizationApi,
  type CreateReportCategoryDto,
  type CreateReportDefinitionDto,
  type CreateReportOverrideDto,
  type SetCategoryRolesDto,
  type UpdateReportCategoryDto,
  type UpdateReportDefinitionDto,
} from '@/api/reportAuthorization.api';

const baseKey = ['admin', 'report-authorization'] as const;

function invalidate(qc: ReturnType<typeof useQueryClient>) {
  qc.invalidateQueries({ queryKey: [...baseKey] });
  qc.invalidateQueries({ queryKey: ['my-reports'] });
}

// Categories
export function useReportCategories() {
  return useQuery({
    queryKey: [...baseKey, 'categories'],
    queryFn: () => reportAuthorizationApi.listCategories(),
    staleTime: 30_000,
  });
}
export function useCreateReportCategory() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (dto: CreateReportCategoryDto) => reportAuthorizationApi.createCategory(dto),
    onSuccess: () => invalidate(qc),
  });
}
export function useUpdateReportCategory() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, dto }: { id: number; dto: UpdateReportCategoryDto }) =>
      reportAuthorizationApi.updateCategory(id, dto),
    onSuccess: () => invalidate(qc),
  });
}
export function useDeleteReportCategory() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: number) => reportAuthorizationApi.deleteCategory(id),
    onSuccess: () => invalidate(qc),
  });
}

// Reports
export function useReports(categoryId?: number) {
  return useQuery({
    queryKey: [...baseKey, 'reports', categoryId],
    queryFn: () => reportAuthorizationApi.listReports(categoryId),
    staleTime: 30_000,
  });
}
export function useCreateReport() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (dto: CreateReportDefinitionDto) => reportAuthorizationApi.createReport(dto),
    onSuccess: () => invalidate(qc),
  });
}
export function useUpdateReport() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, dto }: { id: number; dto: UpdateReportDefinitionDto }) =>
      reportAuthorizationApi.updateReport(id, dto),
    onSuccess: () => invalidate(qc),
  });
}
export function useDeleteReport() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: number) => reportAuthorizationApi.deleteReport(id),
    onSuccess: () => invalidate(qc),
  });
}

// Matrix
export function useReportPermissionMatrix() {
  return useQuery({
    queryKey: [...baseKey, 'matrix'],
    queryFn: () => reportAuthorizationApi.getMatrix(),
    staleTime: 30_000,
  });
}
export function useSetCategoryRoles() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (dto: SetCategoryRolesDto) => reportAuthorizationApi.setCategoryRoles(dto),
    onSuccess: () => invalidate(qc),
  });
}

// Overrides
export function useReportOverrides(reportId?: number) {
  return useQuery({
    queryKey: [...baseKey, 'overrides', reportId],
    queryFn: () => reportAuthorizationApi.listOverrides(reportId),
    staleTime: 30_000,
  });
}
export function useUpsertReportOverride() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (dto: CreateReportOverrideDto) => reportAuthorizationApi.upsertOverride(dto),
    onSuccess: () => invalidate(qc),
  });
}
export function useDeleteReportOverride() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: number) => reportAuthorizationApi.deleteOverride(id),
    onSuccess: () => invalidate(qc),
  });
}
