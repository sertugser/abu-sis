import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  applyBundleToRole,
  createBundle,
  deleteBundle,
  getAppliedBundleIds,
  getBundle,
  listBundles,
  removeBundleFromRole,
  updateBundle,
  type CreatePermissionBundlePayload,
  type UpdatePermissionBundlePayload,
} from '../api/permissionBundle.api';

const STALE_TIME = 5 * 60 * 1000;

export function usePermissionBundles() {
  return useQuery({
    queryKey: ['admin', 'permission-bundles'],
    queryFn: listBundles,
    staleTime: STALE_TIME,
  });
}

export function usePermissionBundle(id: number | null) {
  return useQuery({
    queryKey: ['admin', 'permission-bundles', id],
    queryFn: () => getBundle(id as number),
    enabled: id !== null,
    staleTime: STALE_TIME,
  });
}

export function useAppliedBundleIds(roleId: number | null) {
  return useQuery({
    queryKey: ['admin', 'permission-bundles', 'role', roleId, 'applied'],
    queryFn: () => getAppliedBundleIds(roleId as number),
    enabled: roleId !== null,
    staleTime: 60 * 1000,
  });
}

export function useCreateBundle() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (payload: CreatePermissionBundlePayload) => createBundle(payload),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['admin', 'permission-bundles'] });
    },
  });
}

export function useUpdateBundle() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, payload }: { id: number; payload: UpdatePermissionBundlePayload }) =>
      updateBundle(id, payload),
    onSuccess: (_data, { id }) => {
      qc.invalidateQueries({ queryKey: ['admin', 'permission-bundles'] });
      qc.invalidateQueries({ queryKey: ['admin', 'permission-bundles', id] });
    },
  });
}

export function useDeleteBundle() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: number) => deleteBundle(id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['admin', 'permission-bundles'] });
      qc.invalidateQueries({ queryKey: ['admin', 'permission-bundles', 'role'] });
      qc.invalidateQueries({ queryKey: ['admin', 'permissions'] });
      qc.invalidateQueries({ queryKey: ['admin', 'permissions', 'role'] });
      qc.invalidateQueries({ queryKey: ['permissions'] });
      qc.invalidateQueries({ queryKey: ['menu'] });
    },
  });
}

export function useApplyBundleToRole() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ bundleId, roleId }: { bundleId: number; roleId: number }) =>
      applyBundleToRole(bundleId, roleId),
    onSuccess: (_data, { roleId }) => {
      qc.invalidateQueries({ queryKey: ['admin', 'permissions', 'role', roleId] });
      qc.invalidateQueries({ queryKey: ['admin', 'permission-bundles', 'role', roleId, 'applied'] });
      qc.invalidateQueries({ queryKey: ['permissions'] });
      qc.invalidateQueries({ queryKey: ['menu'] });
    },
  });
}

export function useRemoveBundleFromRole() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ bundleId, roleId }: { bundleId: number; roleId: number }) =>
      removeBundleFromRole(bundleId, roleId),
    onSuccess: (_data, { roleId }) => {
      qc.invalidateQueries({ queryKey: ['admin', 'permissions', 'role', roleId] });
      qc.invalidateQueries({ queryKey: ['admin', 'permission-bundles', 'role', roleId, 'applied'] });
      qc.invalidateQueries({ queryKey: ['permissions'] });
      qc.invalidateQueries({ queryKey: ['menu'] });
    },
  });
}
