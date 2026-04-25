import apiClient from './axios';

export interface MenuItemDto {
  id: number;
  label: string;
  route: string;
  slug: string;
  sortOrder: number;
  openInNewTab: boolean;
}

export interface MenuModuleDto {
  id: number;
  /** Stabil string identifier (örn. "sik-kullanilanlar", "yonetim"). Label değişse bile sabit. */
  slug: string;
  label: string;
  icon?: string | null;
  sortOrder: number;
  items: MenuItemDto[];
  children: MenuModuleDto[];
}

/**
 * Kullanıcının dinamik menüsünü döner.
 * Backend: GET /api/me/menu (opsiyonel ?role=Dean)
 *
 * SidebarModules.ts hardcoded array'lerinin yerini alır. SystemAdmin için tüm
 * modüller döner; diğer roller için permission'ı olan item/module'lar.
 *
 * Patch 3 (panel switcher): activeRole verilirse multi-rol kullanıcı SADECE o role'in
 * modüllerini görür (tüm rollerin union'u yerine). SystemAdmin shortcut bypass edilir.
 */
export async function getMyMenu(activeRole?: string | null): Promise<MenuModuleDto[]> {
  const params = activeRole ? { role: activeRole } : undefined;
  const response = await apiClient.get<MenuModuleDto[]>('/me/menu', { params });
  return response.data;
}
