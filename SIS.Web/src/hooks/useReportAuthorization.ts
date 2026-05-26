import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  reportAuthorizationApi,
  type CreateReportCategoryDto,
  type CreateReportDefinitionDto,
  type CreateReportUserGroupDto,
  type UpdateReportUserGroupDto,
  type SetReportAccessGrantDto,
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

// Groups
export function useReportUserGroups() {
  return useQuery({
    queryKey: [...baseKey, 'groups'],
    queryFn: () => reportAuthorizationApi.listGroups(),
    staleTime: 30_000,
  });
}
export function useCreateReportUserGroup() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (dto: CreateReportUserGroupDto) => reportAuthorizationApi.createGroup(dto),
    onSuccess: () => invalidate(qc),
  });
}
export function useUpdateReportUserGroup() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, dto }: { id: number; dto: UpdateReportUserGroupDto }) => reportAuthorizationApi.updateGroup(id, dto),
    onSuccess: () => invalidate(qc),
  });
}
export function useDeleteReportUserGroup() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: number) => reportAuthorizationApi.deleteGroup(id),
    onSuccess: () => invalidate(qc),
  });
}
export function useReportGroupMembers(groupId: number | null) {
  return useQuery({
    queryKey: [...baseKey, 'group-members', groupId],
    queryFn: () => reportAuthorizationApi.listGroupMembers(groupId!),
    enabled: groupId !== null,
  });
}
export function useAddGroupMember() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ groupId, userId }: { groupId: number; userId: number }) => reportAuthorizationApi.addGroupMember(groupId, userId),
    onSuccess: () => invalidate(qc),
  });
}
export function useRemoveGroupMember() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ groupId, userId }: { groupId: number; userId: number }) => reportAuthorizationApi.removeGroupMember(groupId, userId),
    onSuccess: () => invalidate(qc),
  });
}

// Grants
export function useReportGrants() {
  return useQuery({
    queryKey: [...baseKey, 'grants'],
    queryFn: () => reportAuthorizationApi.listGrants(),
    staleTime: 30_000,
  });
}
export function useSetReportGrant() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (dto: SetReportAccessGrantDto) => reportAuthorizationApi.setGrant(dto),
    onSuccess: () => invalidate(qc),
  });
}
export function useDeleteReportGrant() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: number) => reportAuthorizationApi.deleteGrant(id),
    onSuccess: () => invalidate(qc),
  });
}
