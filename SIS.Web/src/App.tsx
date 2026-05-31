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
import MyCoursesPage from './pages/MyCoursesPage';
import AdvisorInfoPage from './pages/AdvisorInfoPage';
import OpenedDepartmentCoursesPage from './pages/OpenedDepartmentCoursesPage';
import DepartmentScheduleViewPage from './pages/DepartmentScheduleViewPage';
import GradeListPage from './pages/GradeListPage';
import AttendanceStatusPage from './pages/AttendanceStatusPage';
import CurriculumStatusPage from './pages/CurriculumStatusPage';
import TermGpaPage from './pages/TermGpaPage';
import TranscriptPage from './pages/TranscriptPage';
import TranscriptScenarioPage from './pages/TranscriptScenarioPage';
import AcademicCalendarPage from './pages/AcademicCalendarPage';
import AcademicCalendarGeneratorPage from './pages/AcademicCalendarGeneratorPage';
import InstructorSectionsPage from './pages/InstructorSectionsPage';
import SinavTanimlamaPage from './pages/SinavTanimlamaPage';
import AttendanceEntryPage from './pages/AttendanceEntryPage';
import QrAttendancePage from './pages/QrAttendancePage';
import DersProgramimRouter from './pages/DersProgramimRouter';
import GradeEntryPage from './pages/GradeEntryPage';
import NotGirisiListPage from './pages/NotGirisiListPage';
import FacultyPlanningPage from './pages/FacultyPlanningPage';
import SchedulingEditorPage from './pages/SchedulingEditorPage';
import ClassroomManagementPage from './pages/ClassroomManagementPage';
import ClassroomAssignmentPage from './pages/ClassroomAssignmentPage';

import CourseOpeningPage from './pages/CourseOpeningPage';
import ExamRegulationManagementPage from './pages/ExamRegulationManagementPage';
import InstructorAssignmentPage from './pages/InstructorAssignmentPage';
import UserManagementPage from './pages/UserManagementPage';
import StudentSearchPage from './pages/student-hub/StudentSearchPage';
import StudentCardPage from './pages/student-hub/StudentCardPage';
import OverviewPanel from './pages/student-hub/panels/OverviewPanel';
import AcademicInfoPanel from './pages/student-hub/panels/AcademicInfoPanel';
import AdvisorPanel from './pages/student-hub/panels/AdvisorPanel';
import PasswordPanel from './pages/student-hub/panels/PasswordPanel';
import PromotionPanel from './pages/student-hub/panels/PromotionPanel';
import StudentNumberPanel from './pages/student-hub/panels/StudentNumberPanel';
import RegistrationsPanel from './pages/student-hub/panels/RegistrationsPanel';
import GradesPanel from './pages/student-hub/panels/GradesPanel';
import TermGpaPanel from './pages/student-hub/panels/TermGpaPanel';
import AttendancePanel from './pages/student-hub/panels/AttendancePanel';
import TranscriptPanel from './pages/student-hub/panels/TranscriptPanel';
import CurriculumStatusPanel from './pages/student-hub/panels/CurriculumStatusPanel';
import ContactInfoPanel from './pages/student-hub/panels/ContactInfoPanel';
import GeneralInfoPanel from './pages/student-hub/panels/GeneralInfoPanel';
import MilitaryInfoPanel from './pages/student-hub/panels/MilitaryInfoPanel';
import DocumentsPanel from './pages/student-hub/panels/DocumentsPanel';
import IssuedDocumentsPanel from './pages/student-hub/panels/IssuedDocumentsPanel';
import ScholarshipPanel from './pages/student-hub/panels/ScholarshipPanel';
import PlacementPanel from './pages/student-hub/panels/PlacementPanel';
import IntegrationPanel from './pages/student-hub/panels/IntegrationPanel';
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
import BulkCurriculumAssignmentPage from './pages/BulkCurriculumAssignmentPage';
import CourseRegistrationApprovalPage from './pages/CourseRegistrationApprovalPage';
import LetterGradeEditPage from './pages/LetterGradeEditPage';
// ── Yönetim → Kullanıcı İşlemleri (placeholder) ──
import UserPermissionGroupsPage from './pages/UserPermissionGroupsPage';
import UserReportGroupsPage from './pages/UserReportGroupsPage';
import MyReportsPage from './pages/MyReportsPage';
import UserDelegationPage from './pages/UserDelegationPage';
import PasswordChangePage from './pages/PasswordChangePage';
// ── Yönetim → Genel Ayarlar (placeholder) ──
import DocumentRestrictionsPage from './pages/DocumentRestrictionsPage';
import WebModuleSettingsPage from './pages/WebModuleSettingsPage';
import MenuManagementPage from './pages/MenuManagementPage';
import ClassroomManagementAdminPage from './pages/ClassroomManagementAdminPage';
import ConstraintManagementPage from './pages/ConstraintManagementPage';
import CurriculumManagementPage from './pages/CurriculumManagementPage';
import IntibakManagementPage from './pages/IntibakManagementPage';
import ElectiveConfigurationPage from './pages/ElectiveConfigurationPage';
import InstructorCoursesReportPage from './pages/InstructorCoursesReportPage';
import TermProgramTransferPage from './pages/term-program-transfer';
import CourseEvaluationPage from './pages/CourseEvaluationPage';
import EvaluationResultsPage from './pages/EvaluationResultsPage';
import EvaluationPeriodAdminPage from './pages/EvaluationPeriodAdminPage';
import EvaluationRequiredGate from './components/EvaluationRequiredGate';
import AnnouncementsPage from './pages/AnnouncementsPage';
import NotificationsPage from './pages/NotificationsPage';
import AnnouncementManagementPage from './pages/AnnouncementManagementPage';
import AnnouncementCategoryPage from './pages/AnnouncementCategoryPage';
import TimeSlotManagementPage from './pages/TimeSlotManagementPage';
import SectioningPage from './pages/SectioningPage';
import CourseQuotaPage from './pages/CourseQuotaPage';
import AdvisorCourseConstraintsPage from './pages/AdvisorCourseConstraintsPage';
import RoleDashboard from './components/RoleDashboard';
import NotFoundPage from './pages/NotFoundPage';
import ForbiddenPage from './pages/ForbiddenPage';

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
                <Route element={<EvaluationRequiredGate />}>
                  <Route index element={<RoleDashboard />} />

                  {/* RBAC: yetki yetersiz olduğunda RoleProtectedRoute buraya yönlendirir */}
                  <Route path="forbidden" element={<ForbiddenPage />} />

                  <Route element={<RoleProtectedRoute permission="menu.ders-kayit-onay.view" />}>
                    {/* Onay/düzenleme işlevi Ders Kayıt Onay sayfasına taşındı; eski rotalar oraya yönlenir. */}
                    <Route path="danisman-paneli" element={<Navigate to="/ders-kayit-onay" replace />} />
                    <Route path="danismanpaneli" element={<Navigate to="/ders-kayit-onay" replace />} />
                  </Route>

                  {/* Herkesin erişebildiği sayfalar */}
                  <Route path="akademik-takvim" element={<AcademicCalendarPage />} />
                  <Route path="duyurular" element={<AnnouncementsPage />} />
                  <Route path="bildirimler" element={<NotificationsPage />} />

                  <Route element={<RoleProtectedRoute permission="menu.ders-kayit.view" />}>
                    <Route path="ders-kayit" element={<CourseCatalogPage />} />
                  </Route>
                  <Route element={<RoleProtectedRoute permission="menu.plan.view" />}>
                    <Route path="plan" element={<Navigate to="/ders-kayit" replace />} />
                  </Route>
                  <Route element={<RoleProtectedRoute permission="menu.danisman-bilgileri.view" />}>
                    <Route path="danisman-bilgileri" element={<AdvisorInfoPage />} />
                  </Route>
                  <Route element={<RoleProtectedRoute permission="menu.alinan-dersler.view" />}>
                    <Route path="alinan-dersler" element={<MyCoursesPage />} />
                  </Route>
                  <Route element={<RoleProtectedRoute permission="menu.acilan-bolum-dersleri.view" />}>
                    <Route path="acilan-bolum-dersleri" element={<OpenedDepartmentCoursesPage />} />
                  </Route>
                  <Route element={<RoleProtectedRoute permission="menu.bolum-ders-programi.view" />}>
                    <Route path="bolum-ders-programi" element={<DepartmentScheduleViewPage />} />
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
                  <Route element={<RoleProtectedRoute permission="menu.sinav-tanimlama.view" />}>
                    <Route path="sinav-tanimlama" element={<SinavTanimlamaPage />} />
                  </Route>
                  <Route element={<RoleProtectedRoute permission="menu.yoklama-girisi.view" />}>
                    <Route path="yoklama-girisi" element={<AttendanceEntryPage />} />
                  </Route>
                  <Route element={<RoleProtectedRoute permission="menu.not-girisi.view" />}>
                    <Route path="not-girisi" element={<NotGirisiListPage />} />
                  </Route>
                  <Route element={<RoleProtectedRoute permission="menu.not-girisi.view" />}>
                    <Route path="not-girisi/:sectionId" element={<GradeEntryPage />} />
                  </Route>
                  <Route element={<RoleProtectedRoute permission="menu.ders-programim.view" />}>
                    <Route path="ders-programim" element={<DersProgramimRouter />} />
                  </Route>
                  <Route element={<RoleProtectedRoute permission="menu.akademik-takvim.view" />}>
                    <Route path="ogretim-elemani-akademik-takvim" element={<AcademicCalendarPage />} />
                  </Route>

                  <Route element={<RoleProtectedRoute permission="menu.degerlendirme-sonuclari.view" />}>
                    <Route path="degerlendirme-sonuclari" element={<EvaluationResultsPage />} />
                  </Route>

                  <Route element={<RoleProtectedRoute permission="menu.ders-programi-olusturma.view" />}>
                    <Route path="ders-programi-olusturma" element={<FacultyPlanningPage />} />
                  </Route>
                  <Route element={<RoleProtectedRoute permission="menu.derslik-doluluk.view" />}>
                    <Route path="derslik-doluluk-tablosu" element={<ClassroomManagementPage />} />
                  </Route>
                  <Route element={<RoleProtectedRoute permission="menu.derslik-fakulte-atamasi.view" />}>
                    <Route path="derslik-fakulte-atamasi" element={<ClassroomAssignmentPage />} />
                  </Route>
                  <Route element={<RoleProtectedRoute permission="menu.ders-acma-islemleri.view" />}>
                    <Route path="ders-acma-islemleri" element={<CourseOpeningPage />} />
                  </Route>
                  <Route element={<RoleProtectedRoute permission="menu.sinav-mufredati.view" />}>
                    <Route path="sinav-mufredati" element={<ExamRegulationManagementPage />} />
                  </Route>
                  <Route element={<RoleProtectedRoute permission="menu.ogretim-elemani-atama.view" />}>
                    <Route path="ogretim-elemani-atama" element={<InstructorAssignmentPage />} />
                  </Route>
                  <Route element={<RoleProtectedRoute permission="menu.kullanici-tanimlama.view" />}>
                    <Route path="kullanici-yonetimi" element={<UserManagementPage />} />
                  </Route>
                  <Route element={<RoleProtectedRoute permission="menu.ogrenci-rehberi.view" />}>
                    <Route path="ogrenci-rehberi" element={<StudentSearchPage />} />
                    <Route path="ogrenci-rehberi/:studentId" element={<StudentCardPage />}>
                      <Route index element={<OverviewPanel />} />
                      <Route path="akademik" element={<AcademicInfoPanel />} />
                      <Route path="iletisim" element={<ContactInfoPanel />} />
                      <Route path="genel" element={<GeneralInfoPanel />} />
                      <Route path="askerlik" element={<MilitaryInfoPanel />} />
                      <Route path="belgeler" element={<DocumentsPanel />} />
                      <Route path="verilen-belgeler" element={<IssuedDocumentsPanel />} />
                      <Route path="burs" element={<ScholarshipPanel />} />
                      <Route path="yerlestirme" element={<PlacementPanel />} />
                      <Route path="entegrasyon" element={<IntegrationPanel />} />
                      <Route path="ders-kaydi" element={<RegistrationsPanel />} />
                      <Route path="notlar" element={<GradesPanel />} />
                      <Route path="donem-ortalamalari" element={<TermGpaPanel />} />
                      <Route path="transkript" element={<TranscriptPanel />} />
                      <Route path="mufredat-durumu" element={<CurriculumStatusPanel />} />
                      <Route path="devamsizlik" element={<AttendancePanel />} />
                      <Route path="danisman" element={<AdvisorPanel />} />
                      <Route path="no-degistir" element={<StudentNumberPanel />} />
                      <Route path="sinif-atlatma" element={<PromotionPanel />} />
                      <Route path="sifre" element={<PasswordPanel />} />
                    </Route>
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
                  <Route element={<RoleProtectedRoute permission="menu.ogrenci-no-degistirme.view" />}>
                    <Route path="ogrenci-no-degistirme" element={<StudentNumberChangePage />} />
                  </Route>
                  <Route element={<RoleProtectedRoute permission="menu.sinif-atlatma-islemleri.view" />}>
                    <Route path="sinif-atlatma-islemleri" element={<StudentPromotionPage />} />
                  </Route>
                  <Route element={<RoleProtectedRoute permission="menu.toplu-danisman-atama.view" />}>
                    <Route path="toplu-danisman-atama" element={<BulkAdvisorAssignmentPage />} />
                  </Route>
                  <Route element={<RoleProtectedRoute permission="menu.toplu-mufredat-atama.view" />}>
                    <Route path="toplu-mufredat-atama" element={<BulkCurriculumAssignmentPage />} />
                  </Route>
                  <Route element={<RoleProtectedRoute permission="menu.ders-kayit-onay.view" />}>
                    <Route path="ders-kayit-onay" element={<CourseRegistrationApprovalPage />} />
                    <Route path="ders-kayit-duzenleme" element={<Navigate to="/ders-kayit-onay" replace />} />
                  </Route>
                  <Route element={<RoleProtectedRoute permission="menu.harf-notu-duzenleme.view" />}>
                    <Route path="harf-notu-duzenleme" element={<LetterGradeEditPage />} />
                  </Route>
                  {/* ── Yönetim → Kullanıcı İşlemleri (placeholder, SystemAdmin) ── */}
                  <Route element={<RoleProtectedRoute permission="system.permission-bundle.view" />}>
                    <Route path="kullanici-yetki-gruplari" element={<UserPermissionGroupsPage />} />
                  </Route>
                  <Route element={<RoleProtectedRoute permission="menu.kullanici-rapor-gruplari.view" />}>
                    <Route path="kullanici-rapor-gruplari" element={<UserReportGroupsPage />} />
                  </Route>
                  <Route element={<RoleProtectedRoute permission="menu.kullanici-vekalet-islemleri.view" />}>
                    <Route path="kullanici-vekalet-islemleri" element={<UserDelegationPage />} />
                  </Route>
                  <Route element={<RoleProtectedRoute permission="menu.sifre-degistir.view" />}>
                    <Route path="sifre-degistir" element={<PasswordChangePage />} />
                  </Route>
                  {/* ── Yönetim → Genel Ayarlar (placeholder, SystemAdmin) ── */}
                  <Route element={<RoleProtectedRoute permission="menu.belge-sinirlandirmalar.view" />}>
                    <Route path="belge-sinirlandirmalar" element={<DocumentRestrictionsPage />} />
                  </Route>
                  <Route element={<RoleProtectedRoute permission="menu.web-modul-ayarlari.view" />}>
                    <Route path="web-modul-ayarlari" element={<WebModuleSettingsPage />} />
                  </Route>
                  <Route element={<RoleProtectedRoute permission="menu.menu-yonetimi.view" />}>
                    <Route path="menu-yonetimi" element={<MenuManagementPage />} />
                  </Route>
                  <Route element={<RoleProtectedRoute permission="menu.raporlarim.view" />}>
                    <Route path="raporlarim" element={<MyReportsPage />} />
                  </Route>
                  <Route element={<RoleProtectedRoute permission="menu.derslik-yonetimi.view" />}>
                    <Route path="derslik-yonetimi" element={<ClassroomManagementAdminPage />} />
                  </Route>
                  <Route element={<RoleProtectedRoute permission="menu.ders-programi-kisitlamalari.view" />}>
                    <Route path="ders-programi-kisitlamalari" element={<ConstraintManagementPage />} />
                  </Route>
                  <Route element={<RoleProtectedRoute permission="menu.mufredat-islemleri.view" />}>
                    <Route path="mufredat-islemleri" element={<CurriculumManagementPage />} />
                  </Route>
                  <Route element={<RoleProtectedRoute permission="menu.mufredat-ders-intibak.view" />}>
                    <Route path="mufredat-ders-intibak" element={<IntibakManagementPage />} />
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
                  <Route element={<RoleProtectedRoute permission="menu.duyuru-kategorileri.view" />}>
                    <Route path="duyuru-kategorileri" element={<AnnouncementCategoryPage />} />
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
                  <Route element={<RoleProtectedRoute permission="menu.ders-saatleri-yonetimi.view" />}>
                    <Route path="ders-saatleri-yonetimi" element={<TimeSlotManagementPage />} />
                  </Route>
                  <Route element={<RoleProtectedRoute permission="menu.subelendirme-islemleri.view" />}>
                    <Route path="subelendirme-islemleri" element={<SectioningPage />} />
                  </Route>
                  <Route element={<RoleProtectedRoute permission="menu.ders-kontenjan-islemleri.view" />}>
                    <Route path="ders-kontenjan-islemleri" element={<CourseQuotaPage />} />
                  </Route>

                  {/* Program Onayı ayrı sayfa olmaktan çıktı → Ders Programı Oluşturma sayfasındaki
                      "Programı Onayla" butonuna taşındı. Eski link yönlendirilir. */}
                  <Route element={<RoleProtectedRoute permission="menu.program-onayi.view" />}>
                    <Route path="program-onayi" element={<Navigate to="/ders-programi-olusturma" replace />} />
                  </Route>

                  <Route path="*" element={<NotFoundPage />} />
                </Route>
                </Route>

                {/* AppLayout dışı: tam-ekran düzenleyici sayfası, sidebar/topbar görünmez */}
                <Route element={<RoleProtectedRoute permission="menu.ders-programi-olusturma.view" />}>
                  <Route path="ders-programi-olusturma/duzenle" element={<SchedulingEditorPage />} />
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
