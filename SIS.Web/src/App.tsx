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
import SmartPlanningPage from './pages/SmartPlanningPage';
import RegistrationPlanPage from './pages/RegistrationPlanPage';
import MyCoursesPage from './pages/MyCoursesPage';
import GradeListPage from './pages/GradeListPage';
import AttendanceStatusPage from './pages/AttendanceStatusPage';
import CurriculumStatusPage from './pages/CurriculumStatusPage';
import TermGpaPage from './pages/TermGpaPage';
import TranscriptPage from './pages/TranscriptPage';
import TranscriptScenarioPage from './pages/TranscriptScenarioPage';
import AcademicCalendarPage from './pages/AcademicCalendarPage';
import AcademicCalendarGeneratorPage from './pages/AcademicCalendarGeneratorPage';
import InstructorSectionsPage from './pages/InstructorSectionsPage';
import InstructorSchedulingPage from './pages/InstructorSchedulingPage';
import GradeEntryPage from './pages/GradeEntryPage';
import ClassListsPage from './pages/ClassListsPage';
import FacultyPlanningPage from './pages/FacultyPlanningPage';
import ClassroomManagementPage from './pages/ClassroomManagementPage';
import ClassroomAssignmentPage from './pages/ClassroomAssignmentPage';
import TermClosingPage from './pages/term-closing/TermClosingPage';

import CourseOpeningPage from './pages/CourseOpeningPage';
import InstructorAssignmentPage from './pages/InstructorAssignmentPage';
import UserManagementPage from './pages/UserManagementPage';
import CourseIntentionManagementPage from './pages/CourseIntentionManagementPage';
import StudentDirectoryPage from './pages/StudentDirectoryPage';
import InstructorDirectoryPage from './pages/InstructorDirectoryPage';
import TermManagementPage from './pages/TermManagementPage';
import AcademicStructurePage from './pages/AcademicStructurePage';
import CourseCatalogAdminPage from './pages/CourseCatalogAdminPage';
import SystemConfigPage from './pages/SystemConfigPage';
import AuditLogPage from './pages/AuditLogPage';
import PromotionRulePage from './pages/PromotionRulePage';
import ScheduleApprovalPage from './pages/ScheduleApprovalPage';
import ClassroomManagementAdminPage from './pages/ClassroomManagementAdminPage';
import ConstraintManagementPage from './pages/ConstraintManagementPage';
import CurriculumManagementPage from './pages/CurriculumManagementPage';
import ElectiveGroupManagementPage from './pages/ElectiveGroupManagementPage';
import InstructorCoursesReportPage from './pages/InstructorCoursesReportPage';
import TermProgramTransferPage from './pages/TermProgramTransferPage';
import CourseEvaluationPage from './pages/CourseEvaluationPage';
import EvaluationResultsPage from './pages/EvaluationResultsPage';
import EvaluationPeriodAdminPage from './pages/EvaluationPeriodAdminPage';
import AnnouncementsPage from './pages/AnnouncementsPage';
import AnnouncementManagementPage from './pages/AnnouncementManagementPage';
import AdvisorCourseConstraintsPage from './pages/AdvisorCourseConstraintsPage';
import RoleDashboard from './components/RoleDashboard';
import NotFoundPage from './pages/NotFoundPage';
import { INSTRUCTOR_ROLES, MANAGEMENT_ROLES, STUDENT_ROLES, ADVISOR_ROLES, DEAN_ROLES } from './lib/roles';

export default function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <AuthProvider>
        <TooltipProvider delayDuration={300}>
          <BrowserRouter>
            <Routes>
              <Route path="/login" element={<LoginPage />} />
              <Route element={<ProtectedRoute />}>
                <Route element={<RoleProtectedRoute allowedRoles={STUDENT_ROLES} />}>
                  <Route path="report/transkript" element={<TranscriptPage />} />
                </Route>

                <Route element={<AppLayout />}>
                  <Route index element={<RoleDashboard />} />

                  {/* Herkesin erişebildiği sayfalar */}
                  <Route path="akademik-takvim" element={<AcademicCalendarPage />} />
                  <Route path="duyurular" element={<AnnouncementsPage />} />

                  <Route element={<RoleProtectedRoute allowedRoles={STUDENT_ROLES} />}>
                    <Route path="ders-kayit" element={<CourseCatalogPage />} />
                    <Route path="akilli-planlama" element={<SmartPlanningPage />} />
                    <Route path="plan" element={<RegistrationPlanPage />} />
                    <Route path="alinan-dersler" element={<MyCoursesPage />} />
                    <Route path="not-listesi" element={<GradeListPage />} />
                    <Route path="devamsizlik-durumu" element={<AttendanceStatusPage />} />
                    <Route path="donem-ortalamalari" element={<TermGpaPage />} />
                    <Route path="mufredat-durum" element={<CurriculumStatusPage />} />
                    <Route path="transkript-senaryosu" element={<TranscriptScenarioPage />} />
                    <Route path="ders-degerlendirme" element={<CourseEvaluationPage />} />
                  </Route>

                  <Route element={<RoleProtectedRoute allowedRoles={[...ADVISOR_ROLES, ...INSTRUCTOR_ROLES]} />}>
                    <Route path="danisman-ders-kisitlari" element={<AdvisorCourseConstraintsPage />} />
                  </Route>

                  <Route element={<RoleProtectedRoute allowedRoles={[...INSTRUCTOR_ROLES, ...ADVISOR_ROLES, 'Dean', 'DepartmentManager']} />}>
                    <Route path="derslerim" element={<InstructorSectionsPage />} />
                    <Route path="not-girisi" element={<Navigate to="/derslerim" replace />} />
                    <Route path="not-girisi/:sectionId" element={<GradeEntryPage />} />
                    <Route path="ders-programim" element={<InstructorSchedulingPage />} />
                  </Route>

                  <Route element={<RoleProtectedRoute allowedRoles={[...INSTRUCTOR_ROLES, ...ADVISOR_ROLES, 'Dean', 'DepartmentManager', 'SystemAdministrator']} />}>
                    <Route path="degerlendirme-sonuclari" element={<EvaluationResultsPage />} />
                  </Route>

                  <Route element={<RoleProtectedRoute allowedRoles={MANAGEMENT_ROLES} />}>
<Route path="sinif-listeleri" element={<ClassListsPage />} />
                    <Route path="ders-programi-olusturma" element={<FacultyPlanningPage />} />
                    <Route path="derslik-doluluk-tablosu" element={<ClassroomManagementPage />} />
                    <Route path="derslik-fakulte-atamasi" element={<ClassroomAssignmentPage />} />
                    <Route path="donem-kapatma" element={<TermClosingPage />} />
                    <Route path="ders-acma-islemleri" element={<CourseOpeningPage />} />
                    <Route path="niyet-anketi-yonetimi" element={<CourseIntentionManagementPage />} />
                    <Route path="ogretim-elemani-atama" element={<InstructorAssignmentPage />} />
                    <Route path="kullanici-yonetimi" element={<UserManagementPage />} />
                    <Route path="ogrenci-rehberi" element={<StudentDirectoryPage />} />
                    <Route path="personel-listesi" element={<InstructorDirectoryPage />} />
                    <Route path="donem-yonetimi" element={<TermManagementPage />} />
                    <Route path="akademik-takvim-olusturma" element={<AcademicCalendarGeneratorPage />} />
                    <Route path="akademik-yapi" element={<AcademicStructurePage />} />
                    <Route path="ders-katalogu" element={<CourseCatalogAdminPage />} />
                    <Route path="sinif-atlatma-kurallari" element={<PromotionRulePage />} />
                    <Route path="derslik-yonetimi" element={<ClassroomManagementAdminPage />} />
                    <Route path="ders-programi-kisitlamalari" element={<ConstraintManagementPage />} />
                    <Route path="mufredat-islemleri" element={<CurriculumManagementPage />} />
                    <Route path="secmeli-ders-gruplari" element={<ElectiveGroupManagementPage />} />
                    <Route path="ders-veren-ogretim-uyeleri" element={<InstructorCoursesReportPage />} />
                    <Route path="donem-programi-aktarim" element={<TermProgramTransferPage />} />
                    <Route path="duyuru-yonetimi" element={<AnnouncementManagementPage />} />
                  </Route>

                  <Route element={<RoleProtectedRoute allowedRoles={['SystemAdministrator']} />}>
                    <Route path="sistem-ayarlari" element={<SystemConfigPage />} />
                    <Route path="denetim-kayitlari" element={<AuditLogPage />} />
                    <Route path="degerlendirme-donemi-yonetimi" element={<EvaluationPeriodAdminPage />} />
                  </Route>

                  <Route element={<RoleProtectedRoute allowedRoles={[...DEAN_ROLES, ...MANAGEMENT_ROLES]} />}>
                    <Route path="program-onayi" element={<ScheduleApprovalPage />} />
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
