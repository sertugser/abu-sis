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

/** Bir rolün şu an sahip olduğu permission ID listesi. */
export async function getRolePermissions(roleId: number): Promise<number[]> {
  const res = await apiClient.get<number[]>(`/admin/permissions/roles/${roleId}`);
  return res.data;
}

/** Bir rolün permission'larını TOPLU değiştirir (replace-all). */
export async function updateRolePermissions(
  roleId: number,
  permissionIds: number[],
): Promise<void> {
  await apiClient.put(`/admin/permissions/roles/${roleId}`, { permissionIds });
}
