import apiClient from './axios';

export interface PermissionDto {
  id: number;
  key: string;
  category: 'Menu' | 'Lookup' | 'System' | string;
  description?: string | null;
}

export interface RoleDto {
  id: number;
  name: string;
}

/**
 * Bir rolün yetki durumu: manuel atanmış (düzenlenebilir) ve paketten gelen (kilitli) ayrı.
 */
export interface RolePermissionState {
  manualPermissionIds: number[];
  bundlePermissionIds: number[];
}

/** Tüm DB-seed permission'ları (admin matrisi için). lookup.* DB'de yok, listede de yok. */
export async function getAllPermissions(): Promise<PermissionDto[]> {
  const res = await apiClient.get<PermissionDto[]>('/admin/permissions');
  return res.data;
}

/** Atanabilir rolleri döner (SystemAdministrator hariç — locked, Karar 4). */
export async function getAssignableRoles(): Promise<RoleDto[]> {
  const res = await apiClient.get<RoleDto[]>('/admin/permissions/roles');
  return res.data;
}

/** Bir rolün yetki durumu (manuel + paket ayrı). */
export async function getRolePermissions(roleId: number): Promise<RolePermissionState> {
  const res = await apiClient.get<RolePermissionState>(`/admin/permissions/roles/${roleId}`);
  return res.data;
}

/** Bir rolün MANUEL permission'larını TOPLU değiştirir (sadece manuel satırlar; paketler korunur). */
export async function updateRolePermissions(
  roleId: number,
  permissionIds: number[],
): Promise<void> {
  await apiClient.put(`/admin/permissions/roles/${roleId}`, { permissionIds });
}
