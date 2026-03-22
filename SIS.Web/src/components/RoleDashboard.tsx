import { useAuth } from '../context/AuthContext';
import AdvisorDashboardPage from '../pages/AdvisorDashboardPage';
import DashboardPage from '../pages/DashboardPage';
import InstructorDashboardPage from '../pages/InstructorDashboardPage';
import ManagementDashboardPage from '../pages/ManagementDashboardPage';
import { getPrimaryRole } from '../lib/roles';

export default function RoleDashboard() {
  const { user } = useAuth();
  const roles = user?.roles ?? [];
  const primaryRole = getPrimaryRole(roles);

  if (primaryRole === 'SystemAdministrator' || primaryRole === 'AdministrativeStaff' || primaryRole === 'DepartmentManager') {
    return <ManagementDashboardPage />;
  }

  if (primaryRole === 'Instructor') {
    return <InstructorDashboardPage />;
  }

  if (primaryRole === 'AcademicAdvisor') {
    return <AdvisorDashboardPage />;
  }

  return <DashboardPage />;
}
