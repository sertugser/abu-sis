import { BrowserRouter, Routes, Route } from 'react-router-dom';
import { QueryClientProvider } from '@tanstack/react-query';
import { queryClient } from './lib/queryClient';
import { AuthProvider } from './context/AuthContext';
import { TooltipProvider } from '@/components/ui/tooltip';
import ProtectedRoute from './components/ProtectedRoute';
import AppLayout from './components/layout/AppLayout';
import LoginPage from './pages/LoginPage';
import CourseCatalogPage from './pages/CourseCatalogPage';
import RegistrationPlanPage from './pages/RegistrationPlanPage';
import MyCoursesPage from './pages/MyCoursesPage';
import CurriculumStatusPage from './pages/CurriculumStatusPage';
import InstructorSectionsPage from './pages/InstructorSectionsPage';
import GradeEntryPage from './pages/GradeEntryPage';
import RoleDashboard from './components/RoleDashboard';
import NotFoundPage from './pages/NotFoundPage';

export default function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <AuthProvider>
        <TooltipProvider delayDuration={300}>
          <BrowserRouter>
            <Routes>
              <Route path="/login" element={<LoginPage />} />
              <Route element={<ProtectedRoute />}>
                <Route element={<AppLayout />}>
                  {/* Role-based dashboard */}
                  <Route index element={<RoleDashboard />} />
                  {/* Student routes */}
                  <Route path="ders-kayit" element={<CourseCatalogPage />} />
                  <Route path="plan" element={<RegistrationPlanPage />} />
                  <Route path="alinan-dersler" element={<MyCoursesPage />} />
                  <Route path="mufredat-durum" element={<CurriculumStatusPage />} />
                  {/* Instructor routes */}
                  <Route path="derslerim" element={<InstructorSectionsPage />} />
                  <Route path="not-girisi/:sectionId" element={<GradeEntryPage />} />
                  <Route path="*" element={<NotFoundPage />} />
                </Route>
              </Route>
            </Routes>
          </BrowserRouter>
        </TooltipProvider>
      </AuthProvider>
    </QueryClientProvider>
  );
}
