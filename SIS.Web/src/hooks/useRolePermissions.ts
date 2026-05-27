import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  getAllPermissions,
  getAssignableRoles,
  getRolePermissions,
  updateRolePermissions,
} from '../api/permissionAdmin.api';

const STALE_TIME = 5 * 60 * 1000;

export function useAllPermissions() {
  return useQuery({
    queryKey: ['admin', 'permissions', 'all'],
    queryFn: getAllPermissions,
    staleTime: STALE_TIME,
  });
}

export function useAssignableRoles() {
  return useQuery({
    queryKey: ['admin', 'permissions', 'roles'],
    queryFn: getAssignableRoles,
    staleTime: STALE_TIME,
  });
}

export function useRolePermissions(roleId: number | null) {
  return useQuery({
    queryKey: ['admin', 'permissions', 'role', roleId],
    queryFn: () => getRolePermissions(roleId as number),
    enabled: roleId !== null,
    staleTime: 60 * 1000, // role değişince sık refresh
  });
}

// getRolePermissions artık { manualPermissionIds, bundlePermissionIds } döner.

export function useUpdateRolePermissions() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ roleId, permissionIds }: { roleId: number; permissionIds: number[] }) =>
      updateRolePermissions(roleId, permissionIds),
    onSuccess: (_data, { roleId }) => {
      // Bu rolün permission listesi cache'i invalid
      qc.invalidateQueries({ queryKey: ['admin', 'permissions', 'role', roleId] });
      // Ayrıca tüm kullanıcıların menü/permission cache'i (backend de invalidate ediyor;
      // frontend'de cross-user impact olduğu için tüm permission/menu query'lerini invalid)
      qc.invalidateQueries({ queryKey: ['permissions'] });
      qc.invalidateQueries({ queryKey: ['menu'] });
    },
  });
}
