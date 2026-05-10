import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom';
import { QueryClientProvider } from '@tanstack/react-query';
import { queryClient } from './lib/queryClient';
import { AuthProvider } from './context/AuthContext';
import { ToastProvider } from './components/ui/global-toast';
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
import AcademicCalendarGeneratorPage from './pages/AcademicCalendarGeneratorPage';
import { OsymRegistrationCalendarAccessGate, ProgramQuotaAccessGate } from './components/AcademicOperationsRouteGates';
import InstructorSectionsPage from './pages/InstructorSectionsPage';
import AttendanceEntryPage from './pages/AttendanceEntryPage';
import QrAttendancePage from './pages/QrAttendancePage';
import InstructorSchedulingPage from './pages/InstructorSchedulingPage';
import GradeEntryPage from './pages/GradeEntryPage';
import ClassListsPage from './pages/ClassListsPage';
import FacultyPlanningPage from './pages/FacultyPlanningPage';
import ClassroomManagementPage from './pages/ClassroomManagementPage';
import ClassroomAssignmentPage from './pages/ClassroomAssignmentPage';

import CourseOpeningPage from './pages/CourseOpeningPage';
import ExamCurriculumPage from './pages/ExamCurriculumPage';
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
// â”€â”€ Ã–ÄŸrenci Ä°ÅŸlemleri (placeholder) â”€â”€
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
// â”€â”€ YÃ¶netim â†’ KullanÄ±cÄ± Ä°ÅŸlemleri (placeholder) â”€â”€
import UserPermissionGroupsPage from './pages/UserPermissionGroupsPage';
import UserReportGroupsPage from './pages/UserReportGroupsPage';
import UserGroupRestrictionsPage from './pages/UserGroupRestrictionsPage';
import UserReportDesignGroupsPage from './pages/UserReportDesignGroupsPage';
import UserDelegationPage from './pages/UserDelegationPage';
import PasswordChangePage from './pages/PasswordChangePage';
// â”€â”€ YÃ¶netim â†’ Genel Ayarlar (placeholder) â”€â”€
import UniversityInfoPage from './pages/UniversityInfoPage';
import InstitutionalIntegrationPage from './pages/InstitutionalIntegrationPage';
import DocumentRestrictionsPage from './pages/DocumentRestrictionsPage';
import WebModuleSettingsPage from './pages/WebModuleSettingsPage';
import EbysSignatureAuthoritiesPage from './pages/EbysSignatureAuthoritiesPage';
import FacultySignatureUploadPage from './pages/FacultySignatureUploadPage';
import ScheduleApprovalPage from './pages/ScheduleApprovalPage';
import ClassroomManagementAdminPage from './pages/ClassroomManagementAdminPage';
import ConstraintManagementPage from './pages/ConstraintManagementPage';
import ScheduleScoreAnalyticsPage from './pages/ScheduleScoreAnalyticsPage';
import CurriculumManagementPage from './pages/CurriculumManagementPage';
import ElectiveConfigurationPage from './pages/ElectiveConfigurationPage';
import InstructorCoursesReportPage from './pages/InstructorCoursesReportPage';
import TermProgramTransferPage from './pages/term-program-transfer';
import CourseEvaluationPage from './pages/CourseEvaluationPage';
import EvaluationResultsPage from './pages/EvaluationResultsPage';
import EvaluationPeriodAdminPage from './pages/EvaluationPeriodAdminPage';
import AnnouncementsPage from './pages/AnnouncementsPage';
import AnnouncementManagementPage from './pages/AnnouncementManagementPage';
import TimeSlotManagementPage from './pages/TimeSlotManagementPage';
import SectioningPage from './pages/SectioningPage';
import CourseQuotaPage from './pages/CourseQuotaPage';
import AdvisorCourseConstraintsPage from './pages/AdvisorCourseConstraintsPage';
import RoleDashboard from './components/RoleDashboard';
import NotFoundPage from './pages/NotFoundPage';
import ForbiddenPage from './pages/ForbiddenPage';
import RolePermissionMatrix from './components/admin/RolePermissionMatrix';

export default function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <AuthProvider>
        <PermissionProvider>
          <ActiveRoleProvider>
            <TooltipProvider delayDuration={300}>
            <ToastProvider>
            <BrowserRouter>
            <Routes>
              <Route path="/login" element={<LoginPage />} />
              <Route path="/yoklama/qr/:token" element={<QrAttendancePage />} />
              <Route element={<ProtectedRoute />}>
                <Route element={<RoleProtectedRoute permission="menu.transkript.view" />}>
                  <Route path="report/transkript" element={<TranscriptPage />} />
                </Route>

                <Route element={<AppLayout />}>
                  <Route index element={<RoleDashboard />} />

                  {/* RBAC: yetki yetersiz olduÄŸunda RoleProtectedRoute buraya yÃ¶nlendirir */}
                  <Route path="forbidden" element={<ForbiddenPage />} />

                  {/* Herkesin eriÅŸebildiÄŸi sayfalar */}
                  <Route path="akademik-takvim" element={<AcademicCalendarPage />} />
                  <Route path="duyurular" element={<AnnouncementsPage />} />

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
                  <Route element={<RoleProtectedRoute permission="menu.yoklama-girisi.view" />}>
                    <Route path="yoklama-girisi" element={<AttendanceEntryPage />} />
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
                  <Route element={<RoleProtectedRoute permission="menu.ders-acma-islemleri.view" />}>
                    <Route path="ders-acma-islemleri" element={<CourseOpeningPage />} />
              <Route path="sinav-mufredati" element={<ExamCurriculumPage />} />
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
                  {/* Eski URL â†’ yeni URL'e redirect (Î³ hibrit + C rename) */}
                  <Route element={<RoleProtectedRoute permission="menu.mufredat-sinif-donem.view" />}>
                    <Route path="sinif-atlatma-kurallari" element={<Navigate to="/mufredat-sinif-donem-tanimlari" replace />} />
                  </Route>
                  {/* â”€â”€ Ã–ÄŸrenci Ä°ÅŸlemleri (placeholder, SystemAdmin) â”€â”€ */}
                  <Route element={<RoleProtectedRoute permission="menu.ogrenci-no-degistirme.view" />}>
                    <Route path="ogrenci-no-degistirme" element={<StudentNumberChangePage />} />
                  </Route>
                  <Route element={<RoleProtectedRoute permission="menu.sinif-atlatma-islemleri.view" />}>
                    <Route path="sinif-atlatma-islemleri" element={<StudentPromotionPage />} />
                  </Route>
                  <Route element={<RoleProtectedRoute permission="menu.toplu-danisman-atama.view" />}>
                    <Route path="toplu-danisman-atama" element={<BulkAdvisorAssignmentPage />} />
                  </Route>
                  <Route element={<RoleProtectedRoute permission="menu.toplu-staj-danisman-atama.view" />}>
                    <Route path="toplu-staj-danisman-atama" element={<BulkInternshipAdvisorAssignmentPage />} />
                  </Route>
                  <Route element={<RoleProtectedRoute permission="menu.toplu-mufredat-atama.view" />}>
                    <Route path="toplu-mufredat-atama" element={<BulkCurriculumAssignmentPage />} />
                  </Route>
                  <Route element={<RoleProtectedRoute permission="menu.toplu-ders-kaydi.view" />}>
                    <Route path="toplu-ders-kaydi" element={<BulkCourseRegistrationPage />} />
                  </Route>
                  <Route element={<RoleProtectedRoute permission="menu.ders-kayit-duzenleme.view" />}>
                    <Route path="ders-kayit-duzenleme" element={<CourseRegistrationEditPage />} />
                  </Route>
                  <Route element={<RoleProtectedRoute permission="menu.subeler-arasi-ogrenci-transfer.view" />}>
                    <Route path="subeler-arasi-ogrenci-transfer" element={<SectionTransferPage />} />
                  </Route>
                  <Route element={<RoleProtectedRoute permission="menu.dersler-arasi-ogrenci-transfer.view" />}>
                    <Route path="dersler-arasi-ogrenci-transfer" element={<CourseTransferPage />} />
                  </Route>
                  <Route element={<RoleProtectedRoute permission="menu.harf-notu-duzenleme.view" />}>
                    <Route path="harf-notu-duzenleme" element={<LetterGradeEditPage />} />
                  </Route>
                  {/* â”€â”€ YÃ¶netim â†’ KullanÄ±cÄ± Ä°ÅŸlemleri (placeholder, SystemAdmin) â”€â”€ */}
                  <Route element={<RoleProtectedRoute permission="menu.kullanici-yetki-gruplari.view" />}>
                    <Route path="kullanici-yetki-gruplari" element={<UserPermissionGroupsPage />} />
                  </Route>
                  <Route element={<RoleProtectedRoute permission="menu.kullanici-rapor-gruplari.view" />}>
                    <Route path="kullanici-rapor-gruplari" element={<UserReportGroupsPage />} />
                  </Route>
                  <Route element={<RoleProtectedRoute permission="menu.kullanici-grup-sinirlandirmalar.view" />}>
                    <Route path="kullanici-grup-sinirlandirmalar" element={<UserGroupRestrictionsPage />} />
                  </Route>
                  <Route element={<RoleProtectedRoute permission="menu.kullanici-rapor-tasarim-gruplari.view" />}>
                    <Route path="kullanici-rapor-tasarim-gruplari" element={<UserReportDesignGroupsPage />} />
                  </Route>
                  <Route element={<RoleProtectedRoute permission="menu.kullanici-vekalet-islemleri.view" />}>
                    <Route path="kullanici-vekalet-islemleri" element={<UserDelegationPage />} />
                  </Route>
                  <Route element={<RoleProtectedRoute permission="menu.sifre-degistir.view" />}>
                    <Route path="sifre-degistir" element={<PasswordChangePage />} />
                  </Route>
                  {/* â”€â”€ YÃ¶netim â†’ Genel Ayarlar (placeholder, SystemAdmin) â”€â”€ */}
                  <Route element={<RoleProtectedRoute permission="menu.universite-bilgileri.view" />}>
                    <Route path="universite-bilgileri" element={<UniversityInfoPage />} />
                  </Route>
                  <Route element={<RoleProtectedRoute permission="menu.kurumsal-entegrasyon-tanimlari.view" />}>
                    <Route path="kurumsal-entegrasyon-tanimlari" element={<InstitutionalIntegrationPage />} />
                  </Route>
                  <Route element={<RoleProtectedRoute permission="menu.belge-sinirlandirmalar.view" />}>
                    <Route path="belge-sinirlandirmalar" element={<DocumentRestrictionsPage />} />
                  </Route>
                  <Route element={<RoleProtectedRoute permission="menu.web-modul-ayarlari.view" />}>
                    <Route path="web-modul-ayarlari" element={<WebModuleSettingsPage />} />
                  </Route>
                  <Route element={<RoleProtectedRoute permission="menu.ebys-imza-yetkilileri.view" />}>
                    <Route path="ebys-imza-yetkilileri" element={<EbysSignatureAuthoritiesPage />} />
                  </Route>
                  <Route element={<RoleProtectedRoute permission="menu.fakulte-imza-yukleme.view" />}>
                    <Route path="fakulte-imza-yukleme-islemleri" element={<FacultySignatureUploadPage />} />
                  </Route>
                  <Route element={<RoleProtectedRoute permission="menu.derslik-yonetimi.view" />}>
                    <Route path="derslik-yonetimi" element={<ClassroomManagementAdminPage />} />
                  </Route>
                  <Route element={<RoleProtectedRoute permission="menu.ders-programi-kisitlamalari.view" />}>
                    <Route path="ders-programi-kisitlamalari" element={<ConstraintManagementPage />} />
                    <Route path="ders-programi-skor-analizi" element={<ScheduleScoreAnalyticsPage />} />
                  </Route>
                  <Route element={<RoleProtectedRoute permission="menu.mufredat-islemleri.view" />}>
                    <Route path="mufredat-islemleri" element={<CurriculumManagementPage />} />
                  </Route>
                  <Route element={<RoleProtectedRoute permission="menu.secmeli-yapilandirma.view" />}>
                    <Route path="secmeli-yapilandirma" element={<Navigate to="/secmeli-yapilandirma/gruplar" replace />} />
                    <Route path="secmeli-yapilandirma/:tab" element={<ElectiveConfigurationPage />} />
                  </Route>
                  {/* Backward-compat redirects (eski deep-link/bookmark) */}
                  <Route path="secmeli-ders-gruplari" element={<Navigate to="/secmeli-yapilandirma/gruplar" replace />} />
                  <Route path="secmeli-tipleri" element={<Navigate to="/secmeli-yapilandirma/tipler" replace />} />
                  <Route element={<RoleProtectedRoute permission="menu.ders-veren-ogretim-uyeleri.view" />}>
                    <Route path="ders-veren-ogretim-uyeleri" element={<InstructorCoursesReportPage />} />
                  </Route>
                  <Route element={<RoleProtectedRoute permission="menu.donem-programi-aktarim.view" />}>
                    <Route path="donem-programi-aktarim" element={<TermProgramTransferPage />} />
                  </Route>
                  <Route element={<RoleProtectedRoute permission="menu.duyuru-yonetimi.view" />}>
                    <Route path="duyuru-yonetimi" element={<AnnouncementManagementPage />} />
                  </Route>

                  <Route element={<RoleProtectedRoute permission="menu.system-config.view" />}>
                    <Route path="sistem-ayarlari" element={<SystemConfigPage />} />
                  </Route>
                  <Route element={<RoleProtectedRoute permission="menu.audit-log.view" />}>
                    <Route path="denetim-kayitlari" element={<AuditLogPage />} />
                  </Route>
                  <Route element={<RoleProtectedRoute permission="menu.degerlendirme-donemi-yonetimi.view" />}>
                    <Route path="degerlendirme-donemi-yonetimi" element={<EvaluationPeriodAdminPage />} />
                  </Route>
                  <Route element={<RoleProtectedRoute permission="menu.osym-kayit-takvimi.view" />}>
                    <Route path="osym-kayit-takvimi" element={<OsymRegistrationCalendarAccessGate />} />
                  </Route>
                  <Route element={<RoleProtectedRoute permission="menu.ders-saatleri-yonetimi.view" />}>
                    <Route path="ders-saatleri-yonetimi" element={<TimeSlotManagementPage />} />
                  </Route>
                  <Route element={<RoleProtectedRoute permission="menu.subelendirme-islemleri.view" />}>
                    <Route path="subelendirme-islemleri" element={<SectioningPage />} />
                  </Route>
                  <Route element={<RoleProtectedRoute permission="menu.ders-kontenjan-islemleri.view" />}>
                    <Route path="ders-kontenjan-islemleri" element={<CourseQuotaPage />} />
                  </Route>

                  <Route element={<RoleProtectedRoute permission="menu.program-kontenjan.view" />}>
                    <Route path="program-kontenjan" element={<ProgramQuotaAccessGate />} />
                  </Route>

                  <Route element={<RoleProtectedRoute permission="menu.program-onayi.view" />}>
                    <Route path="program-onayi" element={<ScheduleApprovalPage />} />
                  </Route>

                  {/* Dev-only test route â€” Karar 9: production sidebar/route YOK,
                      Phase 2'de KullanÄ±cÄ± Yetki GruplarÄ± placeholder'a import edilecek */}
                  {import.meta.env.DEV && (
                    <Route element={<RoleProtectedRoute permission="system.permission.view" />}>
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
            </ToastProvider>
            </TooltipProvider>
          </ActiveRoleProvider>
        </PermissionProvider>
      </AuthProvider>
    </QueryClientProvider>
  );
}

