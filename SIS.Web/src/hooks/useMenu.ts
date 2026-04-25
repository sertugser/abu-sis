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
 */
export function useMenu() {
  const { user, isAuthenticated } = useAuth();
  return useQuery({
    queryKey: ['menu', user?.userId],
    queryFn: async (): Promise<Module[]> => {
      const data = await getMyMenu();
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
    // Sidebar.tsx'in 'sik-kullanilanlar' / 'yonetim' gibi string id'lere bağlı favorites + accordion
    // mantığı için label'dan deterministik slug üretilir (Türkçe karakter normalize).
    // Backend integer DTO id'si yeterli değil çünkü frontend slug-based key'lerle çalışıyor.
    id: slugify(dto.label),
    label: dto.label,
    icon: resolveIcon(dto.icon),
    children,
  };
}

function toItem(dto: MenuItemDto): ModuleItem {
  return {
    to: dto.route,
    label: dto.label,
    openInNewTab: dto.openInNewTab,
  };
}

/**
 * Türkçe label'ı sidebar slug'ına çevirir.
 * Menu inventory'deki id'lerle (Sprint 0.2) uyumlu:
 *   "Sık Kullanılanlar" → "sik-kullanilanlar"
 *   "Yönetim"           → "yonetim"
 *   "Akademik İşlemler" → "akademik-islemler"
 *   "Hesabım"           → "hesabim"
 */
function slugify(label: string): string {
  return label
    .toLocaleLowerCase('tr-TR')
    .replace(/ı/g, 'i')
    .replace(/ğ/g, 'g')
    .replace(/ü/g, 'u')
    .replace(/ş/g, 's')
    .replace(/ö/g, 'o')
    .replace(/ç/g, 'c')
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '');
}
