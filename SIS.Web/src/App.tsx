import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom';
import { QueryClientProvider } from '@tanstack/react-query';
import { queryClient } from './lib/queryClient';
import { AuthProvider } from './context/AuthContext';
import { TooltipProvider } from '@/components/ui/tooltip';
import ProtectedRoute from './components/ProtectedRoute';
import RoleProtectedRoute from './components/RoleProtectedRoute';
import AppLayout from './components/layout/AppLayout';
import LoginPage from './pages/LoginPage';
import CourseCatalogPage from './pages/CourseCatalogPage';
import RegistrationPlanPage from './pages/RegistrationPlanPage';
import MyCoursesPage from './pages/MyCoursesPage';
import CurriculumStatusPage from './pages/CurriculumStatusPage';
import TermGpaPage from './pages/TermGpaPage';
import UsageGuidePage from './pages/UsageGuidePage';
import AcademicCalendarPage from './pages/AcademicCalendarPage';
import InstructorSectionsPage from './pages/InstructorSectionsPage';
import InstructorSchedulingPage from './pages/InstructorSchedulingPage';
import GradeEntryPage from './pages/GradeEntryPage';
import AcademicOperationsPage from './pages/AcademicOperationsPage';
import FacultyPlanningPage from './pages/FacultyPlanningPage';
import ClassroomManagementPage from './pages/ClassroomManagementPage';
import TermClosingWizard from './pages/TermClosingWizard';
import PreRegistrationSurveyPage from './pages/PreRegistrationSurveyPage';
import RoleDashboard from './components/RoleDashboard';
import NotFoundPage from './pages/NotFoundPage';
import { INSTRUCTOR_ROLES, MANAGEMENT_ROLES, STUDENT_ROLES } from './lib/roles';

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
                  <Route index element={<RoleDashboard />} />

                  <Route element={<RoleProtectedRoute allowedRoles={STUDENT_ROLES} />}>
                    <Route path="kullanim-kilavuzu" element={<UsageGuidePage />} />
                    <Route path="akademik-takvim" element={<AcademicCalendarPage />} />
                    <Route path="ders-kayit" element={<CourseCatalogPage />} />
                    <Route path="plan" element={<RegistrationPlanPage />} />
                    <Route path="alinan-dersler" element={<MyCoursesPage />} />
                    <Route path="donem-ortalamalari" element={<TermGpaPage />} />
                    <Route path="mufredat-durum" element={<CurriculumStatusPage />} />
                    <Route path="on-kayit-anketi" element={<PreRegistrationSurveyPage />} />
                  </Route>

                  <Route element={<RoleProtectedRoute allowedRoles={INSTRUCTOR_ROLES} />}>
                    <Route path="derslerim" element={<InstructorSectionsPage />} />
                    <Route path="not-girisi" element={<Navigate to="/derslerim" replace />} />
                    <Route path="not-girisi/:sectionId" element={<GradeEntryPage />} />
                    <Route path="program-planlama" element={<InstructorSchedulingPage />} />
                  </Route>

                  <Route element={<RoleProtectedRoute allowedRoles={MANAGEMENT_ROLES} />}>
                    <Route path="planlama-merkezi" element={<AcademicOperationsPage mode="planning" />} />
                    <Route path="sinif-listeleri" element={<AcademicOperationsPage mode="class-lists" />} />
                    <Route path="mufredat-gorunumu" element={<AcademicOperationsPage mode="curriculum" />} />
                    <Route path="fakulte-planlama" element={<FacultyPlanningPage />} />
                    <Route path="merkezi-derslik-yonetimi" element={<ClassroomManagementPage />} />
                    <Route path="donem-kapatma" element={<TermClosingWizard />} />
                  </Route>

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
