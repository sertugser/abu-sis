import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';
import { useActiveEvaluationPeriod, useMyEvaluableCourses } from '../hooks/useCourseEvaluation';

export default function EvaluationRequiredGate() {
  const { user } = useAuth();
  const location = useLocation();
  const isStudent = user?.roles?.includes('Student') ?? false;

  const { data: periodData, isLoading: periodLoading } = useActiveEvaluationPeriod();
  const isOpen = periodData?.isOpen === true;

  const { data: courses } = useMyEvaluableCourses(isStudent && isOpen);

  if (!isStudent) return <Outlet />;
  if (periodLoading) return <Outlet />;
  if (!isOpen) return <Outlet />;
  if (location.pathname === '/ders-degerlendirme') return <Outlet />;
  if (location.pathname === '/forbidden') return <Outlet />;

  const hasIncomplete = courses?.some((c) => !c.isCompleted) ?? false;
  if (!hasIncomplete) return <Outlet />;

  return <Navigate to="/ders-degerlendirme" replace />;
}
