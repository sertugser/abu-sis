import { Navigate, Outlet } from 'react-router-dom';
import { usePermissions } from '../context/PermissionContext';
import { Skeleton } from '@/components/ui/skeleton';

interface RoleProtectedRouteProps {
  /** Tek permission key kontrolü. SystemAdmin shortcut otomatik. */
  permission?: string;
  /** En az birine sahip olunca yeterli (OR mantığı). SystemAdmin → true. */
  anyPermission?: readonly string[];
}

/**
 * Sayfa erişim kontrolü — DB-driven permission check.
 *   1) `permission` prop varsa ve user'da varsa → izin
 *   2) `anyPermission` set'inden birinde user varsa → izin
 *   3) Hiç prop verilmemişse → default izin (sadece authenticated yeterli)
 *   4) Verilen prop eşleşmezse → /forbidden redirect
 *
 * SystemAdmin: PermissionContext shortcut'ı sayesinde her zaman izin alır.
 *
 * Migration tarihi: 2026-04-26 — Sprint 5 frontend permission migration tamamlandı.
 * Eski `allowedRoles` prop'u (geçiş süreci içindi) bu commit'te kaldırıldı.
 */
export default function RoleProtectedRoute({
  permission,
  anyPermission,
}: RoleProtectedRouteProps) {
  const { hasPermission, hasAnyPermission, isLoading } = usePermissions();

  // Permission verilerini bekle (ilk login flash'ını önlemek için)
  if (isLoading) {
    return (
      <div className="flex items-center justify-center min-h-[40vh]">
        <Skeleton className="h-8 w-48" />
      </div>
    );
  }

  // 1) Permission check
  if (permission && hasPermission(permission)) {
    return <Outlet />;
  }
  if (anyPermission && hasAnyPermission(anyPermission)) {
    return <Outlet />;
  }

  // 2) Hiç prop verilmemiş → authenticated yeterli (legacy default)
  if (!permission && !anyPermission) {
    return <Outlet />;
  }

  return <Navigate to="/forbidden" replace />;
}
