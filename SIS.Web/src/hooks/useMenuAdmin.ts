import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  type CreateMenuItemPayload,
  type CreateMenuModulePayload,
  type ReorderEntry,
  type UpdateMenuItemPayload,
  type UpdateMenuModulePayload,
  createMenuItem,
  createMenuModule,
  deleteMenuItem,
  deleteMenuModule,
  getAvailableMenuPermissions,
  getMenuTree,
  reorderMenuItems,
  reorderMenuModules,
  updateMenuItem,
  updateMenuModule,
} from '../api/menuAdmin.api';

const TREE_KEY = ['admin', 'menu', 'tree'] as const;
const PERMS_KEY = ['admin', 'menu', 'available-permissions'] as const;

function useInvalidateMenuCaches() {
  const qc = useQueryClient();
  return () => {
    qc.invalidateQueries({ queryKey: TREE_KEY });
    // Sidebar'daki menü cache'i (useMenu) — tüm rol varyantları için invalidate
    qc.invalidateQueries({ queryKey: ['menu'] });
  };
}

export function useMenuTree() {
  return useQuery({
    queryKey: TREE_KEY,
    queryFn: getMenuTree,
    staleTime: 30_000,
  });
}

export function useAvailableMenuPermissions() {
  return useQuery({
    queryKey: PERMS_KEY,
    queryFn: getAvailableMenuPermissions,
    staleTime: 5 * 60 * 1000,
  });
}

export function useCreateModule() {
  const invalidate = useInvalidateMenuCaches();
  return useMutation({
    mutationFn: (payload: CreateMenuModulePayload) => createMenuModule(payload),
    onSuccess: invalidate,
  });
}

export function useUpdateModule() {
  const invalidate = useInvalidateMenuCaches();
  return useMutation({
    mutationFn: (vars: { moduleId: number; payload: UpdateMenuModulePayload }) =>
      updateMenuModule(vars.moduleId, vars.payload),
    onSuccess: invalidate,
  });
}

export function useDeleteModule() {
  const invalidate = useInvalidateMenuCaches();
  return useMutation({
    mutationFn: (moduleId: number) => deleteMenuModule(moduleId),
    onSuccess: invalidate,
  });
}

export function useReorderModules() {
  const invalidate = useInvalidateMenuCaches();
  return useMutation({
    mutationFn: (vars: { parentId: number | null; entries: ReorderEntry[] }) =>
      reorderMenuModules(vars.parentId, vars.entries),
    onSuccess: invalidate,
  });
}

export function useCreateItem() {
  const invalidate = useInvalidateMenuCaches();
  return useMutation({
    mutationFn: (payload: CreateMenuItemPayload) => createMenuItem(payload),
    onSuccess: invalidate,
  });
}

export function useUpdateItem() {
  const invalidate = useInvalidateMenuCaches();
  return useMutation({
    mutationFn: (vars: { itemId: number; payload: UpdateMenuItemPayload }) =>
      updateMenuItem(vars.itemId, vars.payload),
    onSuccess: invalidate,
  });
}

export function useDeleteItem() {
  const invalidate = useInvalidateMenuCaches();
  return useMutation({
    mutationFn: (itemId: number) => deleteMenuItem(itemId),
    onSuccess: invalidate,
  });
}

export function useReorderItems() {
  const invalidate = useInvalidateMenuCaches();
  return useMutation({
    mutationFn: (vars: { moduleId: number; entries: ReorderEntry[] }) =>
      reorderMenuItems(vars.moduleId, vars.entries),
    onSuccess: invalidate,
  });
}
