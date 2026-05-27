import apiClient from './axios';

export interface PermissionBundleDto {
  id: number;
  name: string;
  description?: string | null;
  category?: string | null;
  isSystemDefault: boolean;
  permissionCount: number;
  createdAt: string;
  updatedAt: string;
}

export interface PermissionBundleDetailDto {
  id: number;
  name: string;
  description?: string | null;
  category?: string | null;
  isSystemDefault: boolean;
  createdAt: string;
  updatedAt: string;
  permissionIds: number[];
}

export interface CreatePermissionBundlePayload {
  name: string;
  description?: string | null;
  category?: string | null;
  permissionIds: number[];
}

export type UpdatePermissionBundlePayload = CreatePermissionBundlePayload;

export interface AppliedBundleDto {
  bundleId: number;
  /** true → paketin güncel içeriği, role uygulanmış snapshot'tan farklı (Yeniden Uygula gerekir). */
  isStale: boolean;
}

export async function listBundles(): Promise<PermissionBundleDto[]> {
  const res = await apiClient.get<PermissionBundleDto[]>('/admin/permission-bundles');
  return res.data;
}

export async function getBundle(id: number): Promise<PermissionBundleDetailDto> {
  const res = await apiClient.get<PermissionBundleDetailDto>(`/admin/permission-bundles/${id}`);
  return res.data;
}

export async function createBundle(payload: CreatePermissionBundlePayload): Promise<PermissionBundleDetailDto> {
  const res = await apiClient.post<PermissionBundleDetailDto>('/admin/permission-bundles', payload);
  return res.data;
}

export async function updateBundle(id: number, payload: UpdatePermissionBundlePayload): Promise<PermissionBundleDetailDto> {
  const res = await apiClient.put<PermissionBundleDetailDto>(`/admin/permission-bundles/${id}`, payload);
  return res.data;
}

export async function deleteBundle(id: number): Promise<void> {
  await apiClient.delete(`/admin/permission-bundles/${id}`);
}

export async function applyBundleToRole(bundleId: number, roleId: number): Promise<void> {
  await apiClient.post(`/admin/permission-bundles/${bundleId}/apply/${roleId}`);
}

export async function removeBundleFromRole(bundleId: number, roleId: number): Promise<void> {
  await apiClient.delete(`/admin/permission-bundles/${bundleId}/apply/${roleId}`);
}

export async function getAppliedBundles(roleId: number): Promise<AppliedBundleDto[]> {
  const res = await apiClient.get<AppliedBundleDto[]>(`/admin/roles/${roleId}/applied-bundles`);
  return res.data;
}
