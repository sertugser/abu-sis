import { useQuery } from '@tanstack/react-query';
import { useAuth } from '../context/AuthContext';
import { getMyMenu, type MenuItemDto, type MenuModuleDto } from '../api/menu.api';
import { resolveIcon } from '../components/layout/SidebarModules';
import type { Module, ModuleItem, ModuleNode } from '../components/layout/SidebarModules';

/**
 * Backend'den gelen menüyü cache'li çeker, frontend `Module[]` formatına çevirir.
 * Replaces hardcoded `studentModules`/`instructorModules`/... arrays from SidebarModules.ts.
 *
 * v2 Karar 2: Pure DB — menü artık DB-driven; bu hook tek source of truth.
 * staleTime 5dk: backend permission değişikliği max 5dk içinde refresh'te yansır.
 *
 * Patch 3 (panel switcher): activeRole queryKey'in parçası — multi-rol kullanıcı
 * RoleSwitcher'dan rol değiştirince yeni query tetiklenir, sidebar o role'in
 * modülleriyle yeniden render olur. activeRole null/undefined ise tüm rollerin union'u.
 */
export function useMenu(activeRole?: string | null) {
  const { user, isAuthenticated } = useAuth();
  return useQuery({
    queryKey: ['menu', user?.userId, activeRole ?? null],
    queryFn: async (): Promise<Module[]> => {
      const data = await getMyMenu(activeRole);
      return data.map(toModule);
    },
    enabled: isAuthenticated && !!user,
    staleTime: 5 * 60 * 1000,
    gcTime: 10 * 60 * 1000,
  });
}

function toModule(dto: MenuModuleDto): Module {
  const children: ModuleNode[] = [
    // Nested module'lar önce
    ...dto.children.map(toModule),
    // Sonra leaf item'lar
    ...dto.items.map(toItem),
  ];
  return {
    // Backend MenuModule.Slug — stabil identifier (label değişse bile sabit).
    // Sidebar.tsx'in 'sik-kullanilanlar' / 'yonetim' kontrolleri buna bağlı.
    id: dto.slug,
    label: dto.label,
    icon: resolveIcon(dto.icon),
    children,
  };
}

function toItem(dto: MenuItemDto): ModuleItem {
  return {
    to: dto.route,
    label: dto.label,
    slug: dto.slug,
    openInNewTab: dto.openInNewTab,
  };
}
