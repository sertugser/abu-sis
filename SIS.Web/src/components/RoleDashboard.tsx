import { useAuth } from '../context/AuthContext';
import { useActiveRole } from '../context/ActiveRoleContext';
import DashboardPage from '../pages/DashboardPage';
import InstructorDashboardPage from '../pages/InstructorDashboardPage';
import AdvisorDashboardPage from '../pages/AdvisorDashboardPage';
import ManagementDashboardPage from '../pages/ManagementDashboardPage';
import SystemAdminDashboardPage from '../pages/SystemAdminDashboardPage';
import { getPrimaryRole } from '../lib/roles';

export default function RoleDashboard() {
  const { user } = useAuth();
  const { activeRole } = useActiveRole();

  // Ana sayfa, RoleSwitcher'da seçili rolü takip eder (sidebar/menü gibi).
  // activeRole ilk mount'ta kısa süre null olabilir → tüm rollerin öncelik
  // sırasına göre güvenli fallback (mevcut davranış korunur).
  const role = activeRole ?? getPrimaryRole(user?.roles ?? []);

  if (role === 'SystemAdministrator') {
    return <SystemAdminDashboardPage />;
  }

  if (role === 'Dean' || role === 'AdministrativeStaff' || role === 'DepartmentManager') {
    return <ManagementDashboardPage />;
  }

  if (role === 'AcademicAdvisor') {
    return <AdvisorDashboardPage />;
  }

  if (role === 'Instructor') {
    return <InstructorDashboardPage />;
  }

  return <DashboardPage />;
}
