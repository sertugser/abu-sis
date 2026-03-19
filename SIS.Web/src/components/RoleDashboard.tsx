import { useAuth } from '../context/AuthContext';
import DashboardPage from '../pages/DashboardPage';
import InstructorDashboardPage from '../pages/InstructorDashboardPage';

export default function RoleDashboard() {
  const { user } = useAuth();
  const roles = user?.roles ?? [];

  if (roles.includes('Instructor') || roles.includes('AcademicAdvisor')) {
    return <InstructorDashboardPage />;
  }

  return <DashboardPage />;
}
