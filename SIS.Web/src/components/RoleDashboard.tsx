import { useAuth } from '../context/AuthContext';
import AdvisorDashboardPage from '../pages/AdvisorDashboardPage';
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
    return <AdvisorDashboardPage />;
  }

  if (primaryRole === 'Instructor') {
    return <InstructorDashboardPage />;
  }

  return <DashboardPage />;
}
