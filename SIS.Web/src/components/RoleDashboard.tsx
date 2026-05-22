import { Navigate } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';
import DashboardPage from '../pages/DashboardPage';
import InstructorDashboardPage from '../pages/InstructorDashboardPage';
import ManagementDashboardPage from '../pages/ManagementDashboardPage';
import SystemAdminDashboardPage from '../pages/SystemAdminDashboardPage';
import { getPrimaryRole } from '../lib/roles';

export default function RoleDashboard() {
  const { user } = useAuth();
  const roles = user?.roles ?? [];
  const primaryRole = getPrimaryRole(roles);

  if (primaryRole === 'SystemAdministrator') {
    return <SystemAdminDashboardPage />;
  }

  if (primaryRole === 'Dean' || primaryRole === 'AdministrativeStaff' || primaryRole === 'DepartmentManager') {
    return <ManagementDashboardPage />;
  }

  if (primaryRole === 'AcademicAdvisor') {
    return <Navigate to="/danisman-paneli" replace />;
  }

  if (primaryRole === 'Instructor') {
    return <InstructorDashboardPage />;
  }

  return <DashboardPage />;
}
