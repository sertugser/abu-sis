import { Navigate, Outlet } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';
import { usePermissions } from '../context/PermissionContext';
import { Skeleton } from '@/components/ui/skeleton';

interface RoleProtectedRouteProps {
  /** Mevcut role-based check (Sprint 5 controller migration sonrası kalkacak). */
  allowedRoles?: readonly string[];
  /** Yeni: tek permission key kontrolü. SystemAdmin shortcut otomatik. */
  permission?: string;
  /** Yeni: en az birine sahip olunca yeterli (OR mantığı). */
  anyPermission?: readonly string[];
}

/**
 * Sayfa erişim kontrolü. Transition mode (v2 Karar 5):
 *   1) `permission` prop varsa ve user'da varsa → izin
 *   2) `anyPermission` set'inden birinde user varsa → izin
 *   3) `allowedRoles` mevcut → role kontrolü (geriye uyumluluk)
 *   4) Hiçbiri eşleşmezse → /forbidden redirect
 *
 * SystemAdmin: PermissionContext shortcut'ı sayesinde her zaman izin alır.
 *
 * Sprint 5'te tüm route'ların `allowedRoles` prop'u kalkıp `permission` ile değiştirilecek.
 */
export default function RoleProtectedRoute({
  allowedRoles,
  permission,
  anyPermission,
}: RoleProtectedRouteProps) {
  const { user } = useAuth();
  const { hasPermission, hasAnyPermission, isLoading } = usePermissions();

  // Permission verilerini bekle (ilk login flash'ını önlemek için)
  if (isLoading) {
    return (
      <div className="flex items-center justify-center min-h-[40vh]">
        <Skeleton className="h-8 w-48" />
      </div>
    );
  }

  // 1) Permission check (öncelikli)
  if (permission && hasPermission(permission)) {
    return <Outlet />;
  }
  if (anyPermission && hasAnyPermission(anyPermission)) {
    return <Outlet />;
  }

  // 2) Role check (transition için)
  if (allowedRoles && allowedRoles.length > 0) {
    const roles = user?.roles ?? [];
    if (allowedRoles.some(role => roles.includes(role))) {
      return <Outlet />;
    }
  }

  // 3) Hiçbiri yok ama permission/role props'u verilmemişse default izin
  // (legacy davranış — props'suz <RoleProtectedRoute /> kullanan eski kod için)
  if (!permission && !anyPermission && (!allowedRoles || allowedRoles.length === 0)) {
    return <Outlet />;
  }

  return <Navigate to="/forbidden" replace />;
}
