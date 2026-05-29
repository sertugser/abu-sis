import { useAuth } from '../context/AuthContext';
import InstructorSchedulingPage from './InstructorSchedulingPage';
import StudentTimetablePage from './StudentTimetablePage';

export default function DersProgramimRouter() {
  const { user } = useAuth();
  const isStudent = user?.roles?.includes('Student') ?? false;
  return isStudent ? <StudentTimetablePage /> : <InstructorSchedulingPage />;
}
