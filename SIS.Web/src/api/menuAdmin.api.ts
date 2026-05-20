import apiClient from './axios';
import type { PermissionDto } from './permissionAdmin.api';

export interface MenuItemAdminDto {
  id: number;
  moduleId: number;
  label: string;
  route: string;
  slug: string;
  sortOrder: number;
  openInNewTab: boolean;
  isActive: boolean;
  permissionKeys: string[];
}

export interface MenuModuleAdminDto {
  id: number;
  parentId: number | null;
  slug: string;
  label: string;
  icon?: string | null;
  sortOrder: number;
  isActive: boolean;
  allowedRolesCsv?: string | null;
  items: MenuItemAdminDto[];
  children: MenuModuleAdminDto[];
}

export interface CreateMenuModulePayload {
  parentId: number | null;
  slug: string;
  label: string;
  icon?: string | null;
  allowedRolesCsv?: string | null;
}

export interface UpdateMenuModulePayload {
  label: string;
  icon?: string | null;
  allowedRolesCsv?: string | null;
  isActive: boolean;
}

export interface CreateMenuItemPayload {
  moduleId: number;
  label: string;
  route: string;
  slug: string;
  openInNewTab: boolean;
  permissionKeys: string[];
}

export interface UpdateMenuItemPayload {
  label: string;
  route: string;
  openInNewTab: boolean;
  isActive: boolean;
  permissionKeys: string[];
}

export interface ReorderEntry {
  id: number;
  sortOrder: number;
}

export async function getMenuTree(): Promise<MenuModuleAdminDto[]> {
  const res = await apiClient.get<MenuModuleAdminDto[]>('/admin/menu/tree');
  return res.data;
}

export async function getAvailableMenuPermissions(): Promise<PermissionDto[]> {
  const res = await apiClient.get<PermissionDto[]>('/admin/menu/available-permissions');
  return res.data;
}

export async function createMenuModule(payload: CreateMenuModulePayload): Promise<MenuModuleAdminDto> {
  const res = await apiClient.post<MenuModuleAdminDto>('/admin/menu/modules', payload);
  return res.data;
}

export async function updateMenuModule(
  moduleId: number,
  payload: UpdateMenuModulePayload,
): Promise<MenuModuleAdminDto> {
  const res = await apiClient.put<MenuModuleAdminDto>(`/admin/menu/modules/${moduleId}`, payload);
  return res.data;
}

export async function deleteMenuModule(moduleId: number): Promise<void> {
  await apiClient.delete(`/admin/menu/modules/${moduleId}`);
}

export async function reorderMenuModules(parentId: number | null, entries: ReorderEntry[]): Promise<void> {
  await apiClient.post('/admin/menu/modules/reorder', { parentId, entries });
}

export async function createMenuItem(payload: CreateMenuItemPayload): Promise<MenuItemAdminDto> {
  const res = await apiClient.post<MenuItemAdminDto>('/admin/menu/items', payload);
  return res.data;
}

export async function updateMenuItem(
  itemId: number,
  payload: UpdateMenuItemPayload,
): Promise<MenuItemAdminDto> {
  const res = await apiClient.put<MenuItemAdminDto>(`/admin/menu/items/${itemId}`, payload);
  return res.data;
}

export async function deleteMenuItem(itemId: number): Promise<void> {
  await apiClient.delete(`/admin/menu/items/${itemId}`);
}

export async function reorderMenuItems(moduleId: number, entries: ReorderEntry[]): Promise<void> {
  await apiClient.post('/admin/menu/items/reorder', { moduleId, entries });
}
