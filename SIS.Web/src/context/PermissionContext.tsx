import { createContext, useContext, useMemo, type ReactNode } from 'react';
import { useQuery } from '@tanstack/react-query';
import { useAuth } from './AuthContext';
import { getMyPermissions } from '../api/permissions.api';

const SYSTEM_ADMIN_ROLE = 'SystemAdministrator';

interface PermissionContextValue {
  /** User'ın tüm permission key'leri (Set, lookup O(1)). SystemAdmin için her zaman empty set — isSystemAdmin shortcut kullanılır. */
  permissions: Set<string>;
  /** Tek permission kontrolü. SystemAdmin → her zaman true. */
  hasPermission: (key: string) => boolean;
  /** Bir set'ten en az birine sahip mi (OR mantığı). SystemAdmin → true. */
  hasAnyPermission: (keys: readonly string[]) => boolean;
  /** İlk yükleme veya refetch sırasında true. */
  isLoading: boolean;
  /** SystemAdministrator rolüne sahip kullanıcı (Karar 4 — locked, kod-shortcut). */
  isSystemAdmin: boolean;
}

const PermissionContext = createContext<PermissionContextValue | null>(null);

export function PermissionProvider({ children }: { children: ReactNode }) {
  const { user, isAuthenticated } = useAuth();

  // v2 Karar 6: Permission'lar SADECE /api/me/permissions endpoint'inden gelir.
  // 5 dk staleTime → admin değişikliği max 5 dk içinde refresh'te görünür.
  const { data: permissionList, isLoading } = useQuery({
    queryKey: ['permissions', user?.userId],
    queryFn: getMyPermissions,
    enabled: isAuthenticated && !!user,
    staleTime: 5 * 60 * 1000,
    gcTime: 10 * 60 * 1000,
  });

  const permissions = useMemo(() => new Set<string>(permissionList ?? []), [permissionList]);
  const isSystemAdmin = user?.roles?.includes(SYSTEM_ADMIN_ROLE) ?? false;

  const value = useMemo<PermissionContextValue>(() => ({
    permissions,
    isLoading: isAuthenticated && isLoading,
    isSystemAdmin,
    hasPermission: (key: string) => isSystemAdmin || permissions.has(key),
    hasAnyPermission: (keys: readonly string[]) => isSystemAdmin || keys.some(k => permissions.has(k)),
  }), [permissions, isAuthenticated, isLoading, isSystemAdmin]);

  return <PermissionContext.Provider value={value}>{children}</PermissionContext.Provider>;
}

// eslint-disable-next-line react-refresh/only-export-components
export function usePermissions() {
  const ctx = useContext(PermissionContext);
  if (!ctx) {
    throw new Error('usePermissions must be used within PermissionProvider');
  }
  return ctx;
}
