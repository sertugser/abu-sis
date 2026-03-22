import { Navigate, Outlet } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';

interface RoleProtectedRouteProps {
  allowedRoles: readonly string[];
}

export default function RoleProtectedRoute({ allowedRoles }: RoleProtectedRouteProps) {
  const { user } = useAuth();
  const roles = user?.roles ?? [];
  const hasAccess = allowedRoles.some(role => roles.includes(role));

  if (!hasAccess) {
    return <Navigate to="/" replace />;
  }

  return <Outlet />;
}
