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
  label: string;
  icon?: string | null;
  sortOrder: number;
  items: MenuItemDto[];
  children: MenuModuleDto[];
}

/**
 * Kullanıcının dinamik menüsünü döner.
 * Backend: GET /api/me/menu
 *
 * SidebarModules.ts hardcoded array'lerinin yerini alır. SystemAdmin için tüm
 * modüller döner; diğer roller için permission'ı olan item/module'lar.
 */
export async function getMyMenu(): Promise<MenuModuleDto[]> {
  const response = await apiClient.get<MenuModuleDto[]>('/me/menu');
  return response.data;
}
