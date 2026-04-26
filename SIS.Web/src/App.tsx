import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom';
import { QueryClientProvider } from '@tanstack/react-query';
import { queryClient } from './lib/queryClient';
import { AuthProvider } from './context/AuthContext';
import { ActiveRoleProvider } from './context/ActiveRoleContext';
import { PermissionProvider } from './context/PermissionContext';
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
import AcademicCalendarAdminPage from './pages/AcademicCalendarAdminPage';
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
import StudentDirectoryPage from './pages/StudentDirectoryPage';
import InstructorDirectoryPage from './pages/InstructorDirectoryPage';
import TermManagementPage from './pages/TermManagementPage';
import FacultyDefinitionsPage from './pages/FacultyDefinitionsPage';
import DepartmentDefinitionsPage from './pages/DepartmentDefinitionsPage';
import ProgramDefinitionsPage from './pages/ProgramDefinitionsPage';
import CourseCatalogAdminPage from './pages/CourseCatalogAdminPage';
import SystemConfigPage from './pages/SystemConfigPage';
import AuditLogPage from './pages/AuditLogPage';
import MufredatSinifDonemTanimlariPage from './pages/MufredatSinifDonemTanimlariPage';
// ── Öğrenci İşlemleri (placeholder) ──
import StudentNumberChangePage from './pages/StudentNumberChangePage';
import StudentPromotionPage from './pages/StudentPromotionPage';
import BulkAdvisorAssignmentPage from './pages/BulkAdvisorAssignmentPage';
import BulkInternshipAdvisorAssignmentPage from './pages/BulkInternshipAdvisorAssignmentPage';
import BulkCurriculumAssignmentPage from './pages/BulkCurriculumAssignmentPage';
import BulkCourseRegistrationPage from './pages/BulkCourseRegistrationPage';
import CourseRegistrationEditPage from './pages/CourseRegistrationEditPage';
import SectionTransferPage from './pages/SectionTransferPage';
import CourseTransferPage from './pages/CourseTransferPage';
import LetterGradeEditPage from './pages/LetterGradeEditPage';
// ── Yönetim → Kullanıcı İşlemleri (placeholder) ──
import UserPermissionGroupsPage from './pages/UserPermissionGroupsPage';
import UserReportGroupsPage from './pages/UserReportGroupsPage';
import UserGroupRestrictionsPage from './pages/UserGroupRestrictionsPage';
import UserReportDesignGroupsPage from './pages/UserReportDesignGroupsPage';
import UserDelegationPage from './pages/UserDelegationPage';
import PasswordChangePage from './pages/PasswordChangePage';
// ── Yönetim → Genel Ayarlar (placeholder) ──
import UniversityInfoPage from './pages/UniversityInfoPage';
import InstitutionalIntegrationPage from './pages/InstitutionalIntegrationPage';
import DocumentRestrictionsPage from './pages/DocumentRestrictionsPage';
import WebModuleSettingsPage from './pages/WebModuleSettingsPage';
import EbysSignatureAuthoritiesPage from './pages/EbysSignatureAuthoritiesPage';
import FacultySignatureUploadPage from './pages/FacultySignatureUploadPage';
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
import OsymRegistrationCalendarPage from './pages/OsymRegistrationCalendarPage';
import TimeSlotManagementPage from './pages/TimeSlotManagementPage';
import ProgramQuotaPage from './pages/ProgramQuotaPage';
import SectioningPage from './pages/SectioningPage';
import CourseQuotaPage from './pages/CourseQuotaPage';
import AdvisorCourseConstraintsPage from './pages/AdvisorCourseConstraintsPage';
import RoleDashboard from './components/RoleDashboard';
import NotFoundPage from './pages/NotFoundPage';
import ForbiddenPage from './pages/ForbiddenPage';
import RolePermissionMatrix from './components/admin/RolePermissionMatrix';
import { MANAGEMENT_ROLES, DEAN_ROLES } from './lib/roles';

export default function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <AuthProvider>
        <PermissionProvider>
          <ActiveRoleProvider>
            <TooltipProvider delayDuration={300}>
            <BrowserRouter>
            <Routes>
              <Route path="/login" element={<LoginPage />} />
              <Route element={<ProtectedRoute />}>
                <Route element={<RoleProtectedRoute permission="menu.transkript.view" />}>
                  <Route path="report/transkript" element={<TranscriptPage />} />
                </Route>

                <Route element={<AppLayout />}>
                  <Route index element={<RoleDashboard />} />

                  {/* RBAC: yetki yetersiz olduğunda RoleProtectedRoute buraya yönlendirir */}
                  <Route path="forbidden" element={<ForbiddenPage />} />

                  {/* Herkesin erişebildiği sayfalar */}
                  <Route path="akademik-takvim" element={<AcademicCalendarPage />} />
                  <Route path="duyurular" element={<AnnouncementsPage />} />

                  <Route element={<RoleProtectedRoute permission="menu.akademik-takvim-yonetimi.view" />}>
                    <Route path="akademik-takvim-yonetimi" element={<AcademicCalendarAdminPage />} />
                  </Route>

                  <Route element={<RoleProtectedRoute permission="menu.ders-kayit.view" />}>
                    <Route path="ders-kayit" element={<CourseCatalogPage />} />
                  </Route>
                  <Route element={<RoleProtectedRoute permission="menu.akilli-planlama.view" />}>
                    <Route path="akilli-planlama" element={<SmartPlanningPage />} />
                  </Route>
                  <Route element={<RoleProtectedRoute permission="menu.plan.view" />}>
                    <Route path="plan" element={<RegistrationPlanPage />} />
                  </Route>
                  <Route element={<RoleProtectedRoute permission="menu.alinan-dersler.view" />}>
                    <Route path="alinan-dersler" element={<MyCoursesPage />} />
                  </Route>
                  <Route element={<RoleProtectedRoute permission="menu.not-listesi.view" />}>
                    <Route path="not-listesi" element={<GradeListPage />} />
                  </Route>
                  <Route element={<RoleProtectedRoute permission="menu.devamsizlik-durumu.view" />}>
                    <Route path="devamsizlik-durumu" element={<AttendanceStatusPage />} />
                  </Route>
                  <Route element={<RoleProtectedRoute permission="menu.donem-ortalamalari.view" />}>
                    <Route path="donem-ortalamalari" element={<TermGpaPage />} />
                  </Route>
                  <Route element={<RoleProtectedRoute permission="menu.mufredat-durum.view" />}>
                    <Route path="mufredat-durum" element={<CurriculumStatusPage />} />
                  </Route>
                  <Route element={<RoleProtectedRoute permission="menu.transkript-senaryosu.view" />}>
                    <Route path="transkript-senaryosu" element={<TranscriptScenarioPage />} />
                  </Route>
                  <Route element={<RoleProtectedRoute permission="menu.ders-degerlendirme.view" />}>
                    <Route path="ders-degerlendirme" element={<CourseEvaluationPage />} />
                  </Route>

                  <Route element={<RoleProtectedRoute permission="menu.danisman-ders-kisitlari.view" />}>
                    <Route path="danisman-ders-kisitlari" element={<AdvisorCourseConstraintsPage />} />
                  </Route>

                  <Route element={<RoleProtectedRoute permission="menu.derslerim.view" />}>
                    <Route path="derslerim" element={<InstructorSectionsPage />} />
                  </Route>
                  <Route element={<RoleProtectedRoute permission="menu.derslerim.view" />}>
                    <Route path="not-girisi" element={<Navigate to="/derslerim" replace />} />
                  </Route>
                  <Route element={<RoleProtectedRoute permission="menu.not-girisi.view" />}>
                    <Route path="not-girisi/:sectionId" element={<GradeEntryPage />} />
                  </Route>
                  <Route element={<RoleProtectedRoute permission="menu.ders-programim.view" />}>
                    <Route path="ders-programim" element={<InstructorSchedulingPage />} />
                  </Route>
                  <Route element={<RoleProtectedRoute permission="menu.akademik-takvim.view" />}>
                    <Route path="ogretim-elemani-akademik-takvim" element={<AcademicCalendarPage />} />
                  </Route>

                  <Route element={<RoleProtectedRoute permission="menu.degerlendirme-sonuclari.view" />}>
                    <Route path="degerlendirme-sonuclari" element={<EvaluationResultsPage />} />
                  </Route>

                  <Route element={<RoleProtectedRoute allowedRoles={MANAGEMENT_ROLES} />}>
                    <Route element={<RoleProtectedRoute permission="menu.sinif-listeleri.view" />}>
                      <Route path="sinif-listeleri" element={<ClassListsPage />} />
                    </Route>
                    <Route element={<RoleProtectedRoute permission="menu.ders-programi-olusturma.view" />}>
                      <Route path="ders-programi-olusturma" element={<FacultyPlanningPage />} />
                    </Route>
                    <Route element={<RoleProtectedRoute permission="menu.derslik-doluluk.view" />}>
                      <Route path="derslik-doluluk-tablosu" element={<ClassroomManagementPage />} />
                    </Route>
                    <Route element={<RoleProtectedRoute permission="menu.derslik-yonetimi.view" />}>
                      <Route path="derslik-fakulte-atamasi" element={<ClassroomAssignmentPage />} />
                    </Route>
                    <Route element={<RoleProtectedRoute permission="menu.donem-kapatma.view" />}>
                      <Route path="donem-kapatma" element={<TermClosingPage />} />
                    </Route>
                    <Route element={<RoleProtectedRoute permission="menu.ders-acma-islemleri.view" />}>
                      <Route path="ders-acma-islemleri" element={<CourseOpeningPage />} />
                    </Route>
                    <Route element={<RoleProtectedRoute permission="menu.ogretim-elemani-atama.view" />}>
                      <Route path="ogretim-elemani-atama" element={<InstructorAssignmentPage />} />
                    </Route>
                    <Route element={<RoleProtectedRoute permission="menu.kullanici-tanimlama.view" />}>
                      <Route path="kullanici-yonetimi" element={<UserManagementPage />} />
                    </Route>
                    <Route element={<RoleProtectedRoute permission="menu.ogrenci-rehberi.view" />}>
                      <Route path="ogrenci-rehberi" element={<StudentDirectoryPage />} />
                    </Route>
                    <Route element={<RoleProtectedRoute permission="menu.personel-listesi.view" />}>
                      <Route path="personel-listesi" element={<InstructorDirectoryPage />} />
                    </Route>
                    <Route element={<RoleProtectedRoute permission="menu.akademik-donem.view" />}>
                      <Route path="donem-yonetimi" element={<TermManagementPage />} />
                    </Route>
                    <Route element={<RoleProtectedRoute permission="menu.akademik-takvim-olusturma.view" />}>
                      <Route path="akademik-takvim-olusturma" element={<AcademicCalendarGeneratorPage />} />
                    </Route>
                    <Route element={<RoleProtectedRoute permission="menu.fakulte-tanimlari.view" />}>
                      <Route path="akademik-yapi" element={<Navigate to="/fakulte-tanimlari" replace />} />
                    </Route>
                    <Route element={<RoleProtectedRoute permission="menu.fakulte-tanimlari.view" />}>
                      <Route path="fakulte-tanimlari" element={<FacultyDefinitionsPage />} />
                    </Route>
                    <Route element={<RoleProtectedRoute permission="menu.bolum-tanimlari.view" />}>
                      <Route path="bolum-tanimlari" element={<DepartmentDefinitionsPage />} />
                    </Route>
                    <Route element={<RoleProtectedRoute permission="menu.program-tanimlari.view" />}>
                      <Route path="program-tanimlari" element={<ProgramDefinitionsPage />} />
                    </Route>
                    <Route element={<RoleProtectedRoute permission="menu.ders-katalogu.view" />}>
                      <Route path="ders-katalogu" element={<CourseCatalogAdminPage />} />
                    </Route>
                    <Route element={<RoleProtectedRoute permission="menu.mufredat-sinif-donem.view" />}>
                      <Route path="mufredat-sinif-donem-tanimlari" element={<MufredatSinifDonemTanimlariPage />} />
                    </Route>
                    {/* Eski URL → yeni URL'e redirect (γ hibrit + C rename) */}
                    <Route element={<RoleProtectedRoute permission="menu.mufredat-sinif-donem.view" />}>
                      <Route path="sinif-atlatma-kurallari" element={<Navigate to="/mufredat-sinif-donem-tanimlari" replace />} />
                    </Route>
                    {/* ── Öğrenci İşlemleri (placeholder, SystemAdmin) ── */}
                    <Route path="ogrenci-no-degistirme" element={<StudentNumberChangePage />} />
                    <Route path="sinif-atlatma-islemleri" element={<StudentPromotionPage />} />
                    <Route path="toplu-danisman-atama" element={<BulkAdvisorAssignmentPage />} />
                    <Route path="toplu-staj-danisman-atama" element={<BulkInternshipAdvisorAssignmentPage />} />
                    <Route path="toplu-mufredat-atama" element={<BulkCurriculumAssignmentPage />} />
                    <Route path="toplu-ders-kaydi" element={<BulkCourseRegistrationPage />} />
                    <Route path="ders-kayit-duzenleme" element={<CourseRegistrationEditPage />} />
                    <Route path="subeler-arasi-ogrenci-transfer" element={<SectionTransferPage />} />
                    <Route path="dersler-arasi-ogrenci-transfer" element={<CourseTransferPage />} />
                    <Route path="harf-notu-duzenleme" element={<LetterGradeEditPage />} />
                    {/* ── Yönetim → Kullanıcı İşlemleri (placeholder, SystemAdmin) ── */}
                    <Route path="kullanici-yetki-gruplari" element={<UserPermissionGroupsPage />} />
                    <Route path="kullanici-rapor-gruplari" element={<UserReportGroupsPage />} />
                    <Route path="kullanici-grup-sinirlandirmalar" element={<UserGroupRestrictionsPage />} />
                    <Route path="kullanici-rapor-tasarim-gruplari" element={<UserReportDesignGroupsPage />} />
                    <Route path="kullanici-vekalet-islemleri" element={<UserDelegationPage />} />
                    <Route path="sifre-degistir" element={<PasswordChangePage />} />
                    {/* ── Yönetim → Genel Ayarlar (placeholder, SystemAdmin) ── */}
                    <Route path="universite-bilgileri" element={<UniversityInfoPage />} />
                    <Route path="kurumsal-entegrasyon-tanimlari" element={<InstitutionalIntegrationPage />} />
                    <Route path="belge-sinirlandirmalar" element={<DocumentRestrictionsPage />} />
                    <Route path="web-modul-ayarlari" element={<WebModuleSettingsPage />} />
                    <Route path="ebys-imza-yetkilileri" element={<EbysSignatureAuthoritiesPage />} />
                    <Route path="fakulte-imza-yukleme-islemleri" element={<FacultySignatureUploadPage />} />
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
                    <Route path="osym-kayit-takvimi" element={<OsymRegistrationCalendarPage />} />
                    <Route path="ders-saatleri-yonetimi" element={<TimeSlotManagementPage />} />
                    <Route path="subelendirme-islemleri" element={<SectioningPage />} />
                    <Route path="ders-kontenjan-islemleri" element={<CourseQuotaPage />} />
                  </Route>

                  <Route element={<RoleProtectedRoute allowedRoles={DEAN_ROLES} />}>
                    <Route path="program-kontenjan" element={<ProgramQuotaPage />} />
                  </Route>

                  <Route element={<RoleProtectedRoute allowedRoles={[...DEAN_ROLES, ...MANAGEMENT_ROLES]} />}>
                    <Route path="program-onayi" element={<ScheduleApprovalPage />} />
                  </Route>

                  {/* Dev-only test route — Karar 9: production sidebar/route YOK,
                      Phase 2'de Kullanıcı Yetki Grupları placeholder'a import edilecek */}
                  {import.meta.env.DEV && (
                    <Route element={<RoleProtectedRoute allowedRoles={['SystemAdministrator']} />}>
                      <Route path="__dev/permission-matrix" element={
                        <div className="p-6">
                          <h1 className="text-xl font-semibold mb-4">RolePermissionMatrix (Dev Test)</h1>
                          <RolePermissionMatrix />
                        </div>
                      } />
                    </Route>
                  )}

                  <Route path="*" element={<NotFoundPage />} />
                </Route>
              </Route>
            </Routes>
            </BrowserRouter>
            </TooltipProvider>
          </ActiveRoleProvider>
        </PermissionProvider>
      </AuthProvider>
    </QueryClientProvider>
  );
}
