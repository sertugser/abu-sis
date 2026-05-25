# Admin / Kullanıcı Sayfa Envanteri

**Üretim tarihi:** 2026-05-23 00:45
**Üreten:** `SIS.Web/scripts/generate-codemap.mjs`

> ⚠️ **OTOMATIK ÜRETILIR — elle düzenleme.** Script yeniden çalışınca üzerine yazılır.
> Kod değiştikten sonra: `pnpm codemap:generate` veya `node scripts/generate-codemap.mjs`

## Özet

- **Toplam sayfa:** 76
- **Sidebar grubu:** 16

## İçindekiler

- [Genel İşlemler](#genel-i̇slemler) (5 sayfa)
- [Kullanıcı İşlemleri](#kullanici-i̇slemleri) (7 sayfa)
- [Genel Ayarlar](#genel-ayarlar) (8 sayfa)
- [Sık Kullanılanlar](#sik-kullanilanlar) (1 sayfa)
- [Akademik İşlemler](#akademik-i̇slemler) (5 sayfa)
- [Ders ve Dönem İşlemleri](#ders-ve-donem-i̇slemleri) (9 sayfa)
- [Ders İşlemleri](#ders-i̇slemleri) (16 sayfa)
- [Bölüm İşlemleri](#bolum-i̇slemleri) (2 sayfa)
- [Öğrenci İşlemleri](#ogrenci-i̇slemleri) (8 sayfa)
- [Anketler](#anketler) (3 sayfa)
- [Kaynak Yönetimi](#kaynak-yonetimi) (2 sayfa)
- [Duyuru Yönetimi](#duyuru-yonetimi) (2 sayfa)
- [Hesabım](#hesabim) (1 sayfa)
- [Personel İşlemleri](#personel-i̇slemleri) (1 sayfa)
- [Öğretim İşlemleri](#ogretim-i̇slemleri) (5 sayfa)
- [Raporlar](#raporlar) (1 sayfa)

---

## Genel İşlemler

### Açılan Bölüm Dersleri

| | |
|---|---|
| **Route** | `/acilan-bolum-dersleri` |
| **Dosya** | `SIS.Web/src/pages/OpenedDepartmentCoursesPage.tsx` |
| **Roller** | student |
| **Modal sayısı** | 0 (Dialog: 0, Sheet: 0, AlertDialog: 0) |

---

### Akademik Takvim

| | |
|---|---|
| **Route** | `/akademik-takvim` |
| **Dosya** | `SIS.Web/src/pages/AcademicCalendarPage.tsx` |
| **Roller** | student, administrativeStaff, advisor, dean, departmentManager, instructor, systemAdmin |
| **Modal sayısı** | 2 (Dialog: 2, Sheet: 0, AlertDialog: 0) |

**Sekmeler (2):**
- `calendar`
- `list`

---

### Alınan Dersler

| | |
|---|---|
| **Route** | `/alinan-dersler` |
| **Dosya** | `SIS.Web/src/pages/MyCoursesPage.tsx` |
| **Roller** | student |
| **Modal sayısı** | 0 (Dialog: 0, Sheet: 0, AlertDialog: 0) |

**Veri kancaları (1):**
- `useCourseHistory()`

**API endpoint'leri (8):**
- `DELETE /registrations/${registrationId}/drop`
- `GET /registrations/attendance-status`
- `GET /registrations/history`
- `GET /registrations/history/pdf`
- `GET /registrations/my`
- `POST /registrations/${registrationId}/withdraw`
- `POST /registrations/plan`
- `POST /registrations/submit`

---

### Danışman Bilgileri

| | |
|---|---|
| **Route** | `/danisman-bilgileri` |
| **Dosya** | `SIS.Web/src/pages/AdvisorInfoPage.tsx` |
| **Roller** | student |
| **Modal sayısı** | 0 (Dialog: 0, Sheet: 0, AlertDialog: 0) |

---

### Duyurular

| | |
|---|---|
| **Route** | `/duyurular` |
| **Dosya** | `SIS.Web/src/pages/AnnouncementsPage.tsx` |
| **Roller** | student, administrativeStaff, dean, departmentManager, systemAdmin, advisor, instructor |
| **Modal sayısı** | 1 (Dialog: 1, Sheet: 0, AlertDialog: 0) |

**Veri kancaları (5):**
- `useActiveAnnouncements()`
- `useAnnouncementDetail()`
- `useMarkAllAnnouncementsAsRead()`
- `useMarkAnnouncementAsRead()`
- `useAnnouncementCategories()`

---


## Kullanıcı İşlemleri

### Kullanıcı Grupları Sınırlandırmalar

| | |
|---|---|
| **Route** | `/kullanici-grup-sinirlandirmalar` |
| **Dosya** | `SIS.Web/src/pages/UserGroupRestrictionsPage.tsx` |
| **Roller** | systemAdmin |
| **Modal sayısı** | 0 (Dialog: 0, Sheet: 0, AlertDialog: 0) |

---

### Kullanıcı Rapor Grupları

| | |
|---|---|
| **Route** | `/kullanici-rapor-gruplari` |
| **Dosya** | `SIS.Web/src/pages/UserReportGroupsPage.tsx` |
| **Roller** | systemAdmin |
| **Modal sayısı** | 5 (Dialog: 3, Sheet: 0, AlertDialog: 2) |

**Sekmeler (3):**
- `categories`
- `reports`
- `permissions`

**Veri kancaları (13):**
- `useCreateReport()`
- `useCreateReportCategory()`
- `useDeleteReport()`
- `useDeleteReportCategory()`
- `useDeleteReportOverride()`
- `useReportCategories()`
- `useReportOverrides()`
- `useReportPermissionMatrix()`
- `useReports()`
- `useSetCategoryRoles()`
- `useUpdateReport()`
- `useUpdateReportCategory()`
- `useUpsertReportOverride()`

---

### Kullanıcı Rapor Tasarım Grupları

| | |
|---|---|
| **Route** | `/kullanici-rapor-tasarim-gruplari` |
| **Dosya** | `SIS.Web/src/pages/UserReportDesignGroupsPage.tsx` |
| **Roller** | systemAdmin |
| **Modal sayısı** | 0 (Dialog: 0, Sheet: 0, AlertDialog: 0) |

---

### Kullanıcı Tanımlama

| | |
|---|---|
| **Route** | `/kullanici-yonetimi` |
| **Dosya** | `SIS.Web/src/pages/UserManagementPage.tsx` |
| **Roller** | systemAdmin |
| **Modal sayısı** | 2 (Dialog: 2, Sheet: 0, AlertDialog: 0) |

**Veri kancaları (8):**
- `useAdminUsers()`
- `useUpdateUserRoles()`
- `useResetPassword()`
- `useSetUserStatus()`
- `useAdminFaculties()`
- `useClearUserScope()`
- `useSetUserScope()`
- `useUserScope()`

**API endpoint'leri (21):**
- `DELETE ${BASE}/departments/${id}`
- `DELETE ${BASE}/faculties/${id}`
- `DELETE ${BASE}/programs/${id}`
- `GET ${API}/${id}`
- `GET ${BASE}/departments`
- `GET ${BASE}/departments/${departmentId}/deletion-summary`
- `GET ${BASE}/faculties`
- `GET ${BASE}/faculties/${facultyId}/deletion-summary`
- `GET ${BASE}/programs`
- `GET ${BASE}/programs/${programId}/deletion-summary`
- `POST ${API}/${id}/reset-password`
- `POST ${BASE}/departments`
- `POST ${BASE}/departments/bulk`
- `POST ${BASE}/faculties`
- `POST ${BASE}/faculties/bulk`
- `POST ${BASE}/programs`
- `PUT ${API}/${id}/roles`
- `PUT ${API}/${id}/status`
- `PUT ${BASE}/departments/${id}`
- `PUT ${BASE}/faculties/${id}`
- `PUT ${BASE}/programs/${id}`

---

### Kullanıcı Vekalet İşlemleri

| | |
|---|---|
| **Route** | `/kullanici-vekalet-islemleri` |
| **Dosya** | `SIS.Web/src/pages/UserDelegationPage.tsx` |
| **Roller** | systemAdmin |
| **Modal sayısı** | 2 (Dialog: 1, Sheet: 0, AlertDialog: 1) |

**Veri kancaları (3):**
- `useCreateDelegation()`
- `useDelegations()`
- `useRevokeDelegation()`

---

### Kullanıcı Yetki Grupları

| | |
|---|---|
| **Route** | `/kullanici-yetki-gruplari` |
| **Dosya** | `SIS.Web/src/pages/UserPermissionGroupsPage.tsx` |
| **Roller** | systemAdmin |
| **Modal sayısı** | 1 (Dialog: 0, Sheet: 0, AlertDialog: 1) |

**Veri kancaları (2):**
- `usePermissionBundles()`
- `useDeleteBundle()`

---

### LOG

| | |
|---|---|
| **Route** | `/denetim-kayitlari` |
| **Dosya** | `SIS.Web/src/pages/AuditLogPage.tsx` |
| **Roller** | systemAdmin |
| **Modal sayısı** | 1 (Dialog: 0, Sheet: 1, AlertDialog: 0) |

**Veri kancaları (2):**
- `useAuditLogs()`
- `useAuditLogFilters()`

**API endpoint'leri (5):**
- `GET /admin/audit-logs`
- `GET /admin/audit-logs/${id}`
- `GET /admin/audit-logs/anomalies`
- `GET /admin/audit-logs/export/csv`
- `GET /admin/audit-logs/stats`

---


## Genel Ayarlar

### Belge Sınırlandırmalar

| | |
|---|---|
| **Route** | `/belge-sinirlandirmalar` |
| **Dosya** | `SIS.Web/src/pages/DocumentRestrictionsPage.tsx` |
| **Roller** | systemAdmin |
| **Modal sayısı** | 0 (Dialog: 0, Sheet: 0, AlertDialog: 0) |

---

### EBYS İmza Yetkilileri

| | |
|---|---|
| **Route** | `/ebys-imza-yetkilileri` |
| **Dosya** | `SIS.Web/src/pages/EbysSignatureAuthoritiesPage.tsx` |
| **Roller** | systemAdmin |
| **Modal sayısı** | 0 (Dialog: 0, Sheet: 0, AlertDialog: 0) |

---

### Fakülte İmza Yükleme İşlemleri

| | |
|---|---|
| **Route** | `/fakulte-imza-yukleme-islemleri` |
| **Dosya** | `SIS.Web/src/pages/FacultySignatureUploadPage.tsx` |
| **Roller** | systemAdmin |
| **Modal sayısı** | 0 (Dialog: 0, Sheet: 0, AlertDialog: 0) |

---

### Kurumsal Entegrasyon Tanımları

| | |
|---|---|
| **Route** | `/kurumsal-entegrasyon-tanimlari` |
| **Dosya** | `SIS.Web/src/pages/InstitutionalIntegrationPage.tsx` |
| **Roller** | systemAdmin |
| **Modal sayısı** | 0 (Dialog: 0, Sheet: 0, AlertDialog: 0) |

---

### Menü Yönetimi

| | |
|---|---|
| **Route** | `/menu-yonetimi` |
| **Dosya** | `SIS.Web/src/pages/MenuManagementPage.tsx` |
| **Roller** | systemAdmin |
| **Modal sayısı** | 1 (Dialog: 0, Sheet: 0, AlertDialog: 1) |

**Veri kancaları (10):**
- `useAvailableMenuPermissions()`
- `useCreateItem()`
- `useCreateModule()`
- `useDeleteItem()`
- `useDeleteModule()`
- `useMenuTree()`
- `useReorderItems()`
- `useReorderModules()`
- `useUpdateItem()`
- `useUpdateModule()`

---

### Sistem Konfigürasyonu

| | |
|---|---|
| **Route** | `/sistem-ayarlari` |
| **Dosya** | `SIS.Web/src/pages/SystemConfigPage.tsx` |
| **Roller** | systemAdmin |
| **Modal sayısı** | 1 (Dialog: 1, Sheet: 0, AlertDialog: 0) |

**Veri kancaları (5):**
- `useConfigLimits()`
- `useImportSystemConfigs()`
- `useSystemConfigs()`
- `useUpdateSystemConfig()`
- `useDebounce()`

**API endpoint'leri (5):**
- `GET /admin/system-config`
- `GET /admin/system-config/export`
- `GET /admin/system-config/limits`
- `POST /admin/system-config/import`
- `PUT /admin/system-config/${id}`

---

### Üniversite Bilgileri

| | |
|---|---|
| **Route** | `/universite-bilgileri` |
| **Dosya** | `SIS.Web/src/pages/UniversityInfoPage.tsx` |
| **Roller** | systemAdmin |
| **Modal sayısı** | 0 (Dialog: 0, Sheet: 0, AlertDialog: 0) |

---

### Web Modül Ayarları

| | |
|---|---|
| **Route** | `/web-modul-ayarlari` |
| **Dosya** | `SIS.Web/src/pages/WebModuleSettingsPage.tsx` |
| **Roller** | systemAdmin |
| **Modal sayısı** | 0 (Dialog: 0, Sheet: 0, AlertDialog: 0) |

**Veri kancaları (2):**
- `useUpdateWebModuleControl()`
- `useWebModuleControlSummaries()`

---


## Sık Kullanılanlar

### Ana Sayfa

| | |
|---|---|
| **Route** | `/` |
| **Dosya** | `(bulunamadı)` |
| **Roller** | administrativeStaff, advisor, dean, departmentManager, instructor, student, systemAdmin |
---


## Akademik İşlemler

### Akademik Dönem Tanımları

| | |
|---|---|
| **Route** | `/donem-yonetimi` |
| **Dosya** | `SIS.Web/src/pages/TermManagementPage.tsx` |
| **Roller** | systemAdmin |
| **Modal sayısı** | 3 (Dialog: 3, Sheet: 0, AlertDialog: 0) |

**Veri kancaları (7):**
- `useAdminTerms()`
- `useCreateTerm()`
- `useUpdateTerm()`
- `useCancelTerm()`
- `useUncancelTerm()`
- `useActivateTerm()`
- `useDeactivateTerm()`

**API endpoint'leri (5):**
- `POST ${API}/${termId}/activate`
- `POST ${API}/${termId}/cancel`
- `POST ${API}/${termId}/deactivate`
- `POST ${API}/${termId}/uncancel`
- `PUT ${API}/${id}`

---

### Bölüm Tanımları

| | |
|---|---|
| **Route** | `/bolum-tanimlari` |
| **Dosya** | `SIS.Web/src/pages/DepartmentDefinitionsPage.tsx` |
| **Roller** | systemAdmin |
| **Modal sayısı** | 3 (Dialog: 3, Sheet: 0, AlertDialog: 0) |

**Veri kancaları (8):**
- `useAdminDepartmentsByStructure()`
- `useAdminFaculties()`
- `useAdminProgramsByStructure()`
- `useCreateDepartment()`
- `useDeleteDepartment()`
- `useUpdateDepartment()`
- `useBulkCreateDepartments()`
- `useListController()`

**API endpoint'leri (17):**
- `DELETE ${BASE}/departments/${id}`
- `DELETE ${BASE}/faculties/${id}`
- `DELETE ${BASE}/programs/${id}`
- `GET ${BASE}/departments`
- `GET ${BASE}/departments/${departmentId}/deletion-summary`
- `GET ${BASE}/faculties`
- `GET ${BASE}/faculties/${facultyId}/deletion-summary`
- `GET ${BASE}/programs`
- `GET ${BASE}/programs/${programId}/deletion-summary`
- `POST ${BASE}/departments`
- `POST ${BASE}/departments/bulk`
- `POST ${BASE}/faculties`
- `POST ${BASE}/faculties/bulk`
- `POST ${BASE}/programs`
- `PUT ${BASE}/departments/${id}`
- `PUT ${BASE}/faculties/${id}`
- `PUT ${BASE}/programs/${id}`

---

### Fakülte Tanımları

| | |
|---|---|
| **Route** | `/fakulte-tanimlari` |
| **Dosya** | `SIS.Web/src/pages/FacultyDefinitionsPage.tsx` |
| **Roller** | systemAdmin |
| **Modal sayısı** | 3 (Dialog: 3, Sheet: 0, AlertDialog: 0) |

**Veri kancaları (7):**
- `useAdminFaculties()`
- `useAdminDepartmentsByStructure()`
- `useCreateFaculty()`
- `useUpdateFaculty()`
- `useDeleteFaculty()`
- `useBulkCreateFaculties()`
- `useListController()`

**API endpoint'leri (17):**
- `DELETE ${BASE}/departments/${id}`
- `DELETE ${BASE}/faculties/${id}`
- `DELETE ${BASE}/programs/${id}`
- `GET ${BASE}/departments`
- `GET ${BASE}/departments/${departmentId}/deletion-summary`
- `GET ${BASE}/faculties`
- `GET ${BASE}/faculties/${facultyId}/deletion-summary`
- `GET ${BASE}/programs`
- `GET ${BASE}/programs/${programId}/deletion-summary`
- `POST ${BASE}/departments`
- `POST ${BASE}/departments/bulk`
- `POST ${BASE}/faculties`
- `POST ${BASE}/faculties/bulk`
- `POST ${BASE}/programs`
- `PUT ${BASE}/departments/${id}`
- `PUT ${BASE}/faculties/${id}`
- `PUT ${BASE}/programs/${id}`

---

### Program Tanımları

| | |
|---|---|
| **Route** | `/program-tanimlari` |
| **Dosya** | `SIS.Web/src/pages/ProgramDefinitionsPage.tsx` |
| **Roller** | systemAdmin |
| **Modal sayısı** | 3 (Dialog: 3, Sheet: 0, AlertDialog: 0) |

**Veri kancaları (7):**
- `useAdminDepartmentsByStructure()`
- `useAdminFaculties()`
- `useAdminProgramsByStructure()`
- `useCreateProgram()`
- `useDeleteProgram()`
- `useUpdateProgram()`
- `useListController()`

**API endpoint'leri (17):**
- `DELETE ${BASE}/departments/${id}`
- `DELETE ${BASE}/faculties/${id}`
- `DELETE ${BASE}/programs/${id}`
- `GET ${BASE}/departments`
- `GET ${BASE}/departments/${departmentId}/deletion-summary`
- `GET ${BASE}/faculties`
- `GET ${BASE}/faculties/${facultyId}/deletion-summary`
- `GET ${BASE}/programs`
- `GET ${BASE}/programs/${programId}/deletion-summary`
- `POST ${BASE}/departments`
- `POST ${BASE}/departments/bulk`
- `POST ${BASE}/faculties`
- `POST ${BASE}/faculties/bulk`
- `POST ${BASE}/programs`
- `PUT ${BASE}/departments/${id}`
- `PUT ${BASE}/faculties/${id}`
- `PUT ${BASE}/programs/${id}`

---

### Takvim Oluşturucu

| | |
|---|---|
| **Route** | `/akademik-takvim-olusturma` |
| **Dosya** | `SIS.Web/src/pages/AcademicCalendarGeneratorPage.tsx` |
| **Roller** | administrativeStaff, systemAdmin |
| **Modal sayısı** | 1 (Dialog: 1, Sheet: 0, AlertDialog: 0) |

**Sekmeler (3):**
- `generator`
- `draft`
- `holidays`

**Veri kancaları (11):**
- `useAdminTerms()`
- `useApplyDraft()`
- `useCalendarDraft()`
- `useCalendarDrafts()`
- `useCalendarTemplates()`
- `useDeleteCalendarDraft()`
- `useGenerateCalendar()`
- `useHolidayDefinitions()`
- `useRollbackDraft()`
- `useUpdateDraftEvent()`
- `useWhatIfCalendar()`

**API endpoint'leri (16):**
- `DELETE ${API}/drafts/${draftId}`
- `GET ${API}/drafts`
- `GET ${API}/drafts/${draftId}`
- `GET ${API}/holidays/definitions`
- `GET ${API}/templates`
- `POST ${API}/${termId}/activate`
- `POST ${API}/${termId}/cancel`
- `POST ${API}/${termId}/deactivate`
- `POST ${API}/${termId}/uncancel`
- `POST ${API}/drafts/${draftId}/apply`
- `POST ${API}/drafts/${draftId}/rollback`
- `POST ${API}/drafts/${draftId}/what-if`
- `POST ${API}/generate`
- `PUT ${API}/${id}`
- `PUT ${API}/drafts/${draftId}/events/${encodeURIComponent(eventId)}`
- `PUT ${API}/templates`

---


## Ders ve Dönem İşlemleri

### Akıllı Ders Önerileri

| | |
|---|---|
| **Route** | `/akilli-planlama` |
| **Dosya** | `SIS.Web/src/pages/SmartPlanningPage.tsx` |
| **Roller** | student |
| **Modal sayısı** | 0 (Dialog: 0, Sheet: 0, AlertDialog: 0) |

**Veri kancaları (1):**
- `useRecommendations()`

---

### Ders Kayıt

| | |
|---|---|
| **Route** | `/ders-kayit` |
| **Dosya** | `SIS.Web/src/pages/CourseCatalogPage.tsx` |
| **Roller** | student |
| **Modal sayısı** | 1 (Dialog: 1, Sheet: 0, AlertDialog: 0) |

**Veri kancaları (8):**
- `useStudentCatalog()`
- `useMyRegistrations()`
- `useAddToPlan()`
- `useDropCourse()`
- `useSubmitRegistration()`
- `useStudentInfo()`
- `useStudentPoolStatus()`
- `useActiveTerm()`

**API endpoint'leri (1):**
- `GET /curriculum/student/${studentId}/pool-status`

---

### Devamsızlık Durumu

| | |
|---|---|
| **Route** | `/devamsizlik-durumu` |
| **Dosya** | `SIS.Web/src/pages/AttendanceStatusPage.tsx` |
| **Roller** | student |
| **Modal sayısı** | 1 (Dialog: 1, Sheet: 0, AlertDialog: 0) |

**Veri kancaları (1):**
- `useStudentAttendanceStatus()`

---

### Dönem Ortalamaları

| | |
|---|---|
| **Route** | `/donem-ortalamalari` |
| **Dosya** | `SIS.Web/src/pages/TermGpaPage.tsx` |
| **Roller** | student |
| **Modal sayısı** | 0 (Dialog: 0, Sheet: 0, AlertDialog: 0) |

**Veri kancaları (3):**
- `useStudentInfo()`
- `useStudentTranscript()`
- `useTermGpaHistory()`

---

### Kayıt Planım

| | |
|---|---|
| **Route** | `/plan` |
| **Dosya** | `SIS.Web/src/pages/RegistrationPlanPage.tsx` |
| **Roller** | student |
| **Modal sayısı** | 1 (Dialog: 0, Sheet: 0, AlertDialog: 1) |

**Veri kancaları (5):**
- `useMyRegistrations()`
- `useDropCourse()`
- `useSubmitRegistration()`
- `useStudentInfo()`
- `useStudentPoolStatus()`

**API endpoint'leri (1):**
- `GET /curriculum/student/${studentId}/pool-status`

---

### Müfredat Durum

| | |
|---|---|
| **Route** | `/mufredat-durum` |
| **Dosya** | `SIS.Web/src/pages/CurriculumStatusPage.tsx` |
| **Roller** | student |
| **Modal sayısı** | 0 (Dialog: 0, Sheet: 0, AlertDialog: 0) |

**Veri kancaları (2):**
- `useCurriculumStatus()`
- `useStudentInfo()`

---

### Not Listesi

| | |
|---|---|
| **Route** | `/not-listesi` |
| **Dosya** | `SIS.Web/src/pages/GradeListPage.tsx` |
| **Roller** | student |
| **Modal sayısı** | 1 (Dialog: 1, Sheet: 0, AlertDialog: 0) |

**Veri kancaları (2):**
- `useCourseHistory()`
- `useStudentGradeStatistics()`

**API endpoint'leri (5):**
- `GET /student/curriculum-status`
- `GET /student/me`
- `GET /student/registrations/${registrationId}/grade-statistics`
- `GET /student/term-gpa-history`
- `GET /student/transcript`

---

### Transkript

| | |
|---|---|
| **Route** | `/report/transkript` |
| **Dosya** | `SIS.Web/src/pages/TranscriptPage.tsx` |
| **Roller** | student |
| **Modal sayısı** | 0 (Dialog: 0, Sheet: 0, AlertDialog: 0) |

**Veri kancaları (1):**
- `useStudentTranscript()`

---

### Transkript Senaryosu

| | |
|---|---|
| **Route** | `/transkript-senaryosu` |
| **Dosya** | `SIS.Web/src/pages/TranscriptScenarioPage.tsx` |
| **Roller** | student |
| **Modal sayısı** | 1 (Dialog: 1, Sheet: 0, AlertDialog: 0) |

**Veri kancaları (3):**
- `useCurriculumStatus()`
- `useStudentInfo()`
- `useStudentTranscript()`

---


## Ders İşlemleri

### Ders Aç (Hareket)

| | |
|---|---|
| **Route** | `/donem-programi-aktarim` |
| **Dosya** | `SIS.Web/src/pages/term-program-transfer.tsx` |
| **Roller** | administrativeStaff, dean, departmentManager, systemAdmin |
---

### Ders Aç (Müfredat)

| | |
|---|---|
| **Route** | `/ders-acma-islemleri` |
| **Dosya** | `SIS.Web/src/pages/CourseOpeningPage.tsx` |
| **Roller** | administrativeStaff, dean, departmentManager, systemAdmin |
| **Modal sayısı** | 0 (Dialog: 0, Sheet: 0, AlertDialog: 0) |

**Veri kancaları (20):**
- `useAcademicOperationsContext()`
- `useActiveTerm()`
- `useAdminTerms()`
- `useAdminFaculties()`
- `useAdminDepartmentsByStructure()`
- `useAdminProgramsByStructure()`
- `useBulkOpenCourses()`
- `useCloseSection()`
- `useCloseAllSections()`
- `useDeleteAllSectionsHard()`
- `useReactivateSection()`
- `useCurriculumCoursesForOpening()`
- `usePoolSlotsForOpening()`
- `useDepartmentInstructors()`
- `useExternalCourses()`
- `useInstructorWorkload()`
- `useOpenCourse()`
- `useOpenedCourses()`
- `useUpdateOpenedSection()`
- `useDepartmentForecast()`

**API endpoint'leri (22):**
- `DELETE ${BASE}/departments/${id}`
- `DELETE ${BASE}/faculties/${id}`
- `DELETE ${BASE}/programs/${id}`
- `GET ${BASE}/departments`
- `GET ${BASE}/departments/${departmentId}/deletion-summary`
- `GET ${BASE}/faculties`
- `GET ${BASE}/faculties/${facultyId}/deletion-summary`
- `GET ${BASE}/programs`
- `GET ${BASE}/programs/${programId}/deletion-summary`
- `POST ${API}/${termId}/activate`
- `POST ${API}/${termId}/cancel`
- `POST ${API}/${termId}/deactivate`
- `POST ${API}/${termId}/uncancel`
- `POST ${BASE}/departments`
- `POST ${BASE}/departments/bulk`
- `POST ${BASE}/faculties`
- `POST ${BASE}/faculties/bulk`
- `POST ${BASE}/programs`
- `PUT ${API}/${id}`
- `PUT ${BASE}/departments/${id}`
- `PUT ${BASE}/faculties/${id}`
- `PUT ${BASE}/programs/${id}`

---

### Ders Kataloğu

| | |
|---|---|
| **Route** | `/ders-katalogu` |
| **Dosya** | `SIS.Web/src/pages/CourseCatalogAdminPage.tsx` |
| **Roller** | administrativeStaff, dean, departmentManager, systemAdmin |
| **Modal sayısı** | 4 (Dialog: 4, Sheet: 0, AlertDialog: 0) |

**Sekmeler (7):**
- `genel`
- `akademik`
- `onkosullar`
- `mufredat`
- `program`
- `hocalar`
- `istatistik`

**Veri kancaları (16):**
- `useAdminCourses()`
- `useAdminCourseDetail()`
- `useCreateCourse()`
- `useUpdateCourse()`
- `useDeleteCourse()`
- `useAddPrerequisite()`
- `useRemovePrerequisite()`
- `useAddEquivalency()`
- `useRemoveEquivalency()`
- `useCrossLists()`
- `useAddCrossList()`
- `useRemoveCrossList()`
- `useCourseSections()`
- `useBulkDeleteCourses()`
- `useAdminFaculties()`
- `useAdminDepartmentsByStructure()`

**API endpoint'leri (30):**
- `DELETE ${BASE}/${courseId}/cross-lists/${linkedCourseId}`
- `DELETE ${BASE}/${courseId}/equivalencies/${equivalentCourseId}`
- `DELETE ${BASE}/${courseId}/prerequisites/${requiredCourseId}`
- `DELETE ${BASE}/${id}`
- `DELETE ${BASE}/departments/${id}`
- `DELETE ${BASE}/faculties/${id}`
- `DELETE ${BASE}/programs/${id}`
- `GET ${BASE}/${courseId}/cross-lists`
- `GET ${BASE}/${courseId}/curriculum-usage`
- `GET ${BASE}/${courseId}/sections`
- `GET ${BASE}/${id}`
- `GET ${BASE}/departments`
- `GET ${BASE}/departments/${departmentId}/deletion-summary`
- `GET ${BASE}/faculties`
- `GET ${BASE}/faculties/${facultyId}/deletion-summary`
- `GET ${BASE}/programs`
- `GET ${BASE}/programs/${programId}/deletion-summary`
- `POST ${BASE}/${courseId}/cross-lists/${linkedCourseId}`
- `POST ${BASE}/${courseId}/equivalencies`
- `POST ${BASE}/${courseId}/prerequisites`
- `POST ${BASE}/departments`
- `POST ${BASE}/departments/bulk`
- `POST ${BASE}/faculties`
- `POST ${BASE}/faculties/bulk`
- `POST ${BASE}/programs`
- `PUT ${BASE}/${courseId}/prereq-rules`
- `PUT ${BASE}/${id}`
- `PUT ${BASE}/departments/${id}`
- `PUT ${BASE}/faculties/${id}`
- `PUT ${BASE}/programs/${id}`

---

### Ders Kontenjan İşlemleri

| | |
|---|---|
| **Route** | `/ders-kontenjan-islemleri` |
| **Dosya** | `SIS.Web/src/pages/CourseQuotaPage.tsx` |
| **Roller** | systemAdmin |
| **Modal sayısı** | 0 (Dialog: 0, Sheet: 0, AlertDialog: 0) |

---

### Ders Programı Oluşturma

| | |
|---|---|
| **Route** | `/ders-programi-olusturma` |
| **Dosya** | `SIS.Web/src/pages/FacultyPlanningPage.tsx` |
| **Roller** | administrativeStaff, dean, departmentManager, systemAdmin |
| **Modal sayısı** | 0 (Dialog: 0, Sheet: 0, AlertDialog: 0) |

**Veri kancaları (5):**
- `useAcademicOperationsContext()`
- `useActiveTerm()`
- `useAdminFaculties()`
- `useExportExcel()`
- `useExportPdf()`

**API endpoint'leri (17):**
- `DELETE ${BASE}/departments/${id}`
- `DELETE ${BASE}/faculties/${id}`
- `DELETE ${BASE}/programs/${id}`
- `GET ${BASE}/departments`
- `GET ${BASE}/departments/${departmentId}/deletion-summary`
- `GET ${BASE}/faculties`
- `GET ${BASE}/faculties/${facultyId}/deletion-summary`
- `GET ${BASE}/programs`
- `GET ${BASE}/programs/${programId}/deletion-summary`
- `POST ${BASE}/departments`
- `POST ${BASE}/departments/bulk`
- `POST ${BASE}/faculties`
- `POST ${BASE}/faculties/bulk`
- `POST ${BASE}/programs`
- `PUT ${BASE}/departments/${id}`
- `PUT ${BASE}/faculties/${id}`
- `PUT ${BASE}/programs/${id}`

---

### Ders Saatleri Yönetimi

| | |
|---|---|
| **Route** | `/ders-saatleri-yonetimi` |
| **Dosya** | `SIS.Web/src/pages/TimeSlotManagementPage.tsx` |
| **Roller** | systemAdmin |
| **Modal sayısı** | 4 (Dialog: 4, Sheet: 0, AlertDialog: 0) |

**Veri kancaları (7):**
- `useAdminTerms()`
- `useTimeSlots()`
- `useCreateTimeSlot()`
- `useUpdateTimeSlot()`
- `useDeleteTimeSlot()`
- `useRegenerateTimeSlots()`
- `useScheduleSettings()`

**API endpoint'leri (8):**
- `DELETE ${BASE}/${id}`
- `POST ${API}/${termId}/activate`
- `POST ${API}/${termId}/cancel`
- `POST ${API}/${termId}/deactivate`
- `POST ${API}/${termId}/uncancel`
- `POST ${BASE}/regenerate`
- `PUT ${API}/${id}`
- `PUT ${BASE}/${id}`

---

### Ders Veren Öğretim Üyeleri

| | |
|---|---|
| **Route** | `/ders-veren-ogretim-uyeleri` |
| **Dosya** | `SIS.Web/src/pages/InstructorCoursesReportPage.tsx` |
| **Roller** | administrativeStaff, dean, departmentManager, instructor, systemAdmin |
| **Modal sayısı** | 0 (Dialog: 0, Sheet: 0, AlertDialog: 0) |

**Veri kancaları (4):**
- `useAcademicOperationsContext()`
- `useDepartmentInstructors()`
- `useInstructorWorkload()`
- `useActiveTerm()`

**API endpoint'leri (10):**
- `GET /academicLevel/department/${departmentId}`
- `GET /academicoperations/class-lists`
- `GET /academicoperations/context`
- `GET /academicoperations/curriculum-options`
- `GET /academicoperations/curriculum-overview`
- `GET /academicoperations/planning-summary`
- `GET /academicoperations/students`
- `GET /academicprogression/grade-lock-readiness/${departmentId}`
- `POST /academicoperations/bulk-advisor-assignment`
- `POST /academicoperations/bulk-curriculum-assignment`

---

### Derslik Doluluk Tablosu

| | |
|---|---|
| **Route** | `/derslik-doluluk-tablosu` |
| **Dosya** | `SIS.Web/src/pages/ClassroomManagementPage.tsx` |
| **Roller** | administrativeStaff, dean, departmentManager, systemAdmin |
| **Modal sayısı** | 0 (Dialog: 0, Sheet: 0, AlertDialog: 0) |

---

### Müfredat İşlemleri

| | |
|---|---|
| **Route** | `/mufredat-islemleri` |
| **Dosya** | `SIS.Web/src/pages/CurriculumManagementPage.tsx` |
| **Roller** | administrativeStaff, dean, departmentManager, systemAdmin |
| **Modal sayısı** | 0 (Dialog: 0, Sheet: 0, AlertDialog: 0) |

**Veri kancaları (10):**
- `useCurriculumList()`
- `useCurriculumDetail()`
- `useCreateCurriculum()`
- `useUpdateCurriculum()`
- `useDeleteCurriculum()`
- `useActivateCurriculum()`
- `useAddCurriculumCourse()`
- `useRemoveCurriculumCourse()`
- `useCopyCurriculum()`
- `useCurriculumPools()`

---

### Müfredat Sınıf Dönem Tanımları

| | |
|---|---|
| **Route** | `/mufredat-sinif-donem-tanimlari` |
| **Dosya** | `SIS.Web/src/pages/MufredatSinifDonemTanimlariPage.tsx` |
| **Roller** | administrativeStaff, systemAdmin |
| **Modal sayısı** | 0 (Dialog: 0, Sheet: 0, AlertDialog: 0) |

**Veri kancaları (5):**
- `useAdminFaculties()`
- `useAdminDepartmentsByStructure()`
- `useAdminProgramsByStructure()`
- `useCurriculumList()`
- `useCurriculumTermRules()`

**API endpoint'leri (17):**
- `DELETE ${BASE}/departments/${id}`
- `DELETE ${BASE}/faculties/${id}`
- `DELETE ${BASE}/programs/${id}`
- `GET ${BASE}/departments`
- `GET ${BASE}/departments/${departmentId}/deletion-summary`
- `GET ${BASE}/faculties`
- `GET ${BASE}/faculties/${facultyId}/deletion-summary`
- `GET ${BASE}/programs`
- `GET ${BASE}/programs/${programId}/deletion-summary`
- `POST ${BASE}/departments`
- `POST ${BASE}/departments/bulk`
- `POST ${BASE}/faculties`
- `POST ${BASE}/faculties/bulk`
- `POST ${BASE}/programs`
- `PUT ${BASE}/departments/${id}`
- `PUT ${BASE}/faculties/${id}`
- `PUT ${BASE}/programs/${id}`

---

### Öğretim Elemanı Atama

| | |
|---|---|
| **Route** | `/ogretim-elemani-atama` |
| **Dosya** | `SIS.Web/src/pages/InstructorAssignmentPage.tsx` |
| **Roller** | administrativeStaff, dean, departmentManager, systemAdmin |
| **Modal sayısı** | 0 (Dialog: 0, Sheet: 0, AlertDialog: 0) |

**Veri kancaları (8):**
- `useAcademicOperationsContext()`
- `useActiveTerm()`
- `useAdminFaculties()`
- `useAdminDepartmentsByStructure()`
- `useBulkAssignInstructor()`
- `useDepartmentInstructors()`
- `useOpenedCourses()`
- `useUpdateOpenedSection()`

**API endpoint'leri (17):**
- `DELETE ${BASE}/departments/${id}`
- `DELETE ${BASE}/faculties/${id}`
- `DELETE ${BASE}/programs/${id}`
- `GET ${BASE}/departments`
- `GET ${BASE}/departments/${departmentId}/deletion-summary`
- `GET ${BASE}/faculties`
- `GET ${BASE}/faculties/${facultyId}/deletion-summary`
- `GET ${BASE}/programs`
- `GET ${BASE}/programs/${programId}/deletion-summary`
- `POST ${BASE}/departments`
- `POST ${BASE}/departments/bulk`
- `POST ${BASE}/faculties`
- `POST ${BASE}/faculties/bulk`
- `POST ${BASE}/programs`
- `PUT ${BASE}/departments/${id}`
- `PUT ${BASE}/faculties/${id}`
- `PUT ${BASE}/programs/${id}`

---

### Program Kısıtlamaları

| | |
|---|---|
| **Route** | `/ders-programi-kisitlamalari` |
| **Dosya** | `SIS.Web/src/pages/ConstraintManagementPage.tsx` |
| **Roller** | administrativeStaff, dean, departmentManager, systemAdmin |
| **Modal sayısı** | 1 (Dialog: 1, Sheet: 0, AlertDialog: 0) |

**Sekmeler (2):**
- `constraints`
- `pairing`

**Veri kancaları (7):**
- `usePermissions()`
- `useConstraints()`
- `useToggleConstraint()`
- `useUpdateConstraintCategory()`
- `useUpdateConstraintWeight()`
- `useAdminTerms()`
- `useUpdateConstraintParameters()`

**API endpoint'leri (5):**
- `POST ${API}/${termId}/activate`
- `POST ${API}/${termId}/cancel`
- `POST ${API}/${termId}/deactivate`
- `POST ${API}/${termId}/uncancel`
- `PUT ${API}/${id}`

---

### Program Onayı

| | |
|---|---|
| **Route** | `/program-onayi` |
| **Dosya** | `SIS.Web/src/pages/ScheduleApprovalPage.tsx` |
| **Roller** | dean, systemAdmin |
| **Modal sayısı** | 1 (Dialog: 1, Sheet: 0, AlertDialog: 0) |

**Veri kancaları (8):**
- `useActiveTerm()`
- `useAdminFaculties()`
- `useScheduleApprovalStatus()`
- `useApproveSchedule()`
- `useRevokeScheduleApproval()`
- `useFacultyPlanningOverview()`
- `useFacultyPlanningSections()`
- `useFacultyPlanningTimeSlots()`

**API endpoint'leri (17):**
- `DELETE ${BASE}/departments/${id}`
- `DELETE ${BASE}/faculties/${id}`
- `DELETE ${BASE}/programs/${id}`
- `GET ${BASE}/departments`
- `GET ${BASE}/departments/${departmentId}/deletion-summary`
- `GET ${BASE}/faculties`
- `GET ${BASE}/faculties/${facultyId}/deletion-summary`
- `GET ${BASE}/programs`
- `GET ${BASE}/programs/${programId}/deletion-summary`
- `POST ${BASE}/departments`
- `POST ${BASE}/departments/bulk`
- `POST ${BASE}/faculties`
- `POST ${BASE}/faculties/bulk`
- `POST ${BASE}/programs`
- `PUT ${BASE}/departments/${id}`
- `PUT ${BASE}/faculties/${id}`
- `PUT ${BASE}/programs/${id}`

---

### Seçmeli Ders Yapılandırması

| | |
|---|---|
| **Route** | `/secmeli-yapilandirma` |
| **Dosya** | `(bulunamadı)` |
| **Roller** | administrativeStaff, dean, departmentManager, systemAdmin |
---

### Sınav Müfredatı

| | |
|---|---|
| **Route** | `/sinav-mufredati` |
| **Dosya** | `SIS.Web/src/pages/ExamRegulationManagementPage.tsx` |
| **Roller** | administrativeStaff, dean, departmentManager, systemAdmin |
| **Modal sayısı** | 1 (Dialog: 1, Sheet: 0, AlertDialog: 0) |

**Açıklama:** Program bazında sınav yönergesi (Sınav Müfredatı) yönetimi. Her kayıt bir program + dönem tipi + yıl bileşimine ait değerlendirme kurallarını tanımlar (ara sınav/YYS sayısı, ağırlık oranları, geçme notu, ham başarı baraj değerleri).

**Veri kancaları (4):**
- `useAdminPrograms()`
- `useExamRegulations(programId?)`
- `useDeleteExamRegulation()`
- `useCloneExamRegulation()`

**Controller:** `ExamRegulationController` → `api/admin/exam-regulations`

**API endpoint'leri (6):**
- `GET api/admin/exam-regulations` — liste (programId query filtresi opsiyonel)
- `GET api/admin/exam-regulations/{id}` — tek kayıt
- `POST api/admin/exam-regulations` — oluştur
- `PUT api/admin/exam-regulations/{id}` — güncelle
- `DELETE api/admin/exam-regulations/{id}` — sil
- `POST api/admin/exam-regulations/{id}/clone` — yeni yıl/programa çoğalt

**İzinler:**
- `menu.sinav-mufredati.view`
- `menu.sinav-mufredati.create`
- `menu.sinav-mufredati.edit`
- `menu.sinav-mufredati.delete`

---

### Şubelendirme İşlemleri

| | |
|---|---|
| **Route** | `/subelendirme-islemleri` |
| **Dosya** | `SIS.Web/src/pages/SectioningPage.tsx` |
| **Roller** | administrativeStaff, dean, departmentManager, instructor, systemAdmin |
| **Modal sayısı** | 0 (Dialog: 0, Sheet: 0, AlertDialog: 0) |

---


## Bölüm İşlemleri

### Toplu Danışman Atama

| | |
|---|---|
| **Route** | `/toplu-danisman-atama` |
| **Dosya** | `SIS.Web/src/pages/BulkAdvisorAssignmentPage.tsx` |
| **Roller** | administrativeStaff, dean, departmentManager, instructor, systemAdmin |
| **Modal sayısı** | 0 (Dialog: 0, Sheet: 0, AlertDialog: 0) |

**Veri kancaları (5):**
- `useAcademicOperationsContext()`
- `useBulkAdvisorAssignment()`
- `useDepartmentStudents()`
- `useDepartmentInstructors()`
- `usePermissions()`

**API endpoint'leri (10):**
- `GET /academicLevel/department/${departmentId}`
- `GET /academicoperations/class-lists`
- `GET /academicoperations/context`
- `GET /academicoperations/curriculum-options`
- `GET /academicoperations/curriculum-overview`
- `GET /academicoperations/planning-summary`
- `GET /academicoperations/students`
- `GET /academicprogression/grade-lock-readiness/${departmentId}`
- `POST /academicoperations/bulk-advisor-assignment`
- `POST /academicoperations/bulk-curriculum-assignment`

---

### Toplu Müfredat Atama

| | |
|---|---|
| **Route** | `/toplu-mufredat-atama` |
| **Dosya** | `SIS.Web/src/pages/BulkCurriculumAssignmentPage.tsx` |
| **Roller** | administrativeStaff, dean, departmentManager, instructor, systemAdmin |
| **Modal sayısı** | 0 (Dialog: 0, Sheet: 0, AlertDialog: 0) |

**Veri kancaları (5):**
- `useAcademicOperationsContext()`
- `useBulkCurriculumAssignment()`
- `useDepartmentCurriculumOptions()`
- `useDepartmentStudents()`
- `usePermissions()`

**API endpoint'leri (10):**
- `GET /academicLevel/department/${departmentId}`
- `GET /academicoperations/class-lists`
- `GET /academicoperations/context`
- `GET /academicoperations/curriculum-options`
- `GET /academicoperations/curriculum-overview`
- `GET /academicoperations/planning-summary`
- `GET /academicoperations/students`
- `GET /academicprogression/grade-lock-readiness/${departmentId}`
- `POST /academicoperations/bulk-advisor-assignment`
- `POST /academicoperations/bulk-curriculum-assignment`

---


## Öğrenci İşlemleri

### Ders Kayıt Düzenleme

| | |
|---|---|
| **Route** | `/ders-kayit-duzenleme` |
| **Dosya** | `SIS.Web/src/pages/CourseRegistrationEditPage.tsx` |
| **Roller** | advisor, systemAdmin |
| **Modal sayısı** | 0 (Dialog: 0, Sheet: 0, AlertDialog: 0) |

---

### Dersler Arası Öğrenci Transfer

| | |
|---|---|
| **Route** | `/dersler-arasi-ogrenci-transfer` |
| **Dosya** | `SIS.Web/src/pages/CourseTransferPage.tsx` |
| **Roller** | systemAdmin |
| **Modal sayısı** | 0 (Dialog: 0, Sheet: 0, AlertDialog: 0) |

---

### Harf Notu Düzenleme

| | |
|---|---|
| **Route** | `/harf-notu-duzenleme` |
| **Dosya** | `SIS.Web/src/pages/LetterGradeEditPage.tsx` |
| **Roller** | systemAdmin |
| **Modal sayısı** | 0 (Dialog: 0, Sheet: 0, AlertDialog: 0) |

---

### Öğrenci No Değiştirme

| | |
|---|---|
| **Route** | `/ogrenci-no-degistirme` |
| **Dosya** | `SIS.Web/src/pages/StudentNumberChangePage.tsx` |
| **Roller** | systemAdmin |
| **Modal sayısı** | 0 (Dialog: 0, Sheet: 0, AlertDialog: 0) |

---

### Öğrenci Rehberi

| | |
|---|---|
| **Route** | `/ogrenci-rehberi` |
| **Dosya** | `SIS.Web/src/pages/StudentDirectoryPage.tsx` |
| **Roller** | administrativeStaff, advisor, dean, departmentManager, student, systemAdmin |
| **Modal sayısı** | 2 (Dialog: 2, Sheet: 0, AlertDialog: 0) |

**Veri kancaları (5):**
- `useAdminStudents()`
- `useCreateStudent()`
- `useAdminDepartments()`
- `useAdminPrograms()`
- `useAdminAdvisors()`

**API endpoint'leri (8):**
- `GET ${API}/${id}`
- `GET /admin/advisors`
- `GET /admin/departments`
- `GET /admin/programs`
- `POST ${API}/bulk-advisor-assignment/apply`
- `POST ${API}/bulk-advisor-assignment/preview`
- `PUT ${API}/${studentId}/advisor`
- `PUT ${API}/bulk-advisor-assignment/advisor-settings`

---

### Sınıf Atlatma İşlemleri

| | |
|---|---|
| **Route** | `/sinif-atlatma-islemleri` |
| **Dosya** | `SIS.Web/src/pages/StudentPromotionPage.tsx` |
| **Roller** | dean, departmentManager, systemAdmin |
| **Modal sayısı** | 0 (Dialog: 0, Sheet: 0, AlertDialog: 0) |

---

### Toplu Ders Kaydı

| | |
|---|---|
| **Route** | `/toplu-ders-kaydi` |
| **Dosya** | `SIS.Web/src/pages/BulkCourseRegistrationPage.tsx` |
| **Roller** | systemAdmin |
| **Modal sayısı** | 0 (Dialog: 0, Sheet: 0, AlertDialog: 0) |

---

### Toplu Staj Danışman Atama

| | |
|---|---|
| **Route** | `/toplu-staj-danisman-atama` |
| **Dosya** | `SIS.Web/src/pages/BulkInternshipAdvisorAssignmentPage.tsx` |
| **Roller** | systemAdmin |
| **Modal sayısı** | 0 (Dialog: 0, Sheet: 0, AlertDialog: 0) |

---


## Anketler

### Değerlendirme Dönemi Yönetimi

| | |
|---|---|
| **Route** | `/degerlendirme-donemi-yonetimi` |
| **Dosya** | `SIS.Web/src/pages/EvaluationPeriodAdminPage.tsx` |
| **Roller** | systemAdmin |
| **Modal sayısı** | 4 (Dialog: 1, Sheet: 0, AlertDialog: 3) |

**Veri kancaları (9):**
- `useActiveEvaluationPeriod()`
- `useAllEvaluationQuestions()`
- `useCloseEvaluationPeriod()`
- `useCreateEvaluationQuestion()`
- `useDeleteEvaluationQuestion()`
- `useOpenEvaluationPeriod()`
- `useRestoreEvaluationQuestion()`
- `useUpdateEvaluationQuestion()`
- `useAdminTerms()`

**API endpoint'leri (5):**
- `POST ${API}/${termId}/activate`
- `POST ${API}/${termId}/cancel`
- `POST ${API}/${termId}/deactivate`
- `POST ${API}/${termId}/uncancel`
- `PUT ${API}/${id}`

---

### Değerlendirme Sonuçları

| | |
|---|---|
| **Route** | `/degerlendirme-sonuclari` |
| **Dosya** | `SIS.Web/src/pages/EvaluationResultsPage.tsx` |
| **Roller** | administrativeStaff, advisor, dean, departmentManager, instructor, systemAdmin |
| **Modal sayısı** | 1 (Dialog: 1, Sheet: 0, AlertDialog: 0) |

**Sekmeler (2):**
- `management`
- `instructor`

**Veri kancaları (9):**
- `useInstructorSections()`
- `useInstructorTerms()`
- `useActiveEvaluationPeriod()`
- `useDepartmentEvaluationSummary()`
- `useExportDepartmentEvaluationResults()`
- `useSectionEvaluationResults()`
- `useAdminFaculties()`
- `useAdminDepartmentsByStructure()`
- `useAdminTerms()`

**API endpoint'leri (22):**
- `DELETE ${BASE}/departments/${id}`
- `DELETE ${BASE}/faculties/${id}`
- `DELETE ${BASE}/programs/${id}`
- `GET ${BASE}/departments`
- `GET ${BASE}/departments/${departmentId}/deletion-summary`
- `GET ${BASE}/faculties`
- `GET ${BASE}/faculties/${facultyId}/deletion-summary`
- `GET ${BASE}/programs`
- `GET ${BASE}/programs/${programId}/deletion-summary`
- `POST ${API}/${termId}/activate`
- `POST ${API}/${termId}/cancel`
- `POST ${API}/${termId}/deactivate`
- `POST ${API}/${termId}/uncancel`
- `POST ${BASE}/departments`
- `POST ${BASE}/departments/bulk`
- `POST ${BASE}/faculties`
- `POST ${BASE}/faculties/bulk`
- `POST ${BASE}/programs`
- `PUT ${API}/${id}`
- `PUT ${BASE}/departments/${id}`
- `PUT ${BASE}/faculties/${id}`
- `PUT ${BASE}/programs/${id}`

---

### Ders Değerlendirme

| | |
|---|---|
| **Route** | `/ders-degerlendirme` |
| **Dosya** | `SIS.Web/src/pages/CourseEvaluationPage.tsx` |
| **Roller** | student, systemAdmin |
| **Modal sayısı** | 0 (Dialog: 0, Sheet: 0, AlertDialog: 0) |

**Veri kancaları (2):**
- `useActiveEvaluationPeriod()`
- `useMyEvaluableCourses()`

---


## Kaynak Yönetimi

### Derslik Yönetimi

| | |
|---|---|
| **Route** | `/derslik-yonetimi` |
| **Dosya** | `SIS.Web/src/pages/ClassroomManagementAdminPage.tsx` |
| **Roller** | systemAdmin |
| **Modal sayısı** | 2 (Dialog: 2, Sheet: 0, AlertDialog: 0) |

**Veri kancaları (4):**
- `useClassrooms()`
- `useCreateClassroom()`
- `useUpdateClassroom()`
- `useDeleteClassroom()`

---

### Derslik-Fakülte Ataması

| | |
|---|---|
| **Route** | `/derslik-fakulte-atamasi` |
| **Dosya** | `SIS.Web/src/pages/ClassroomAssignmentPage.tsx` |
| **Roller** | systemAdmin |
| **Modal sayısı** | 2 (Dialog: 2, Sheet: 0, AlertDialog: 0) |

**Veri kancaları (7):**
- `useAdminTerms()`
- `useAdminFaculties()`
- `useClassrooms()`
- `useClassroomAssignments()`
- `useDeleteClassroomAssignment()`
- `useBulkAssignClassrooms()`
- `useSuggestClassroomAssignments()`

**API endpoint'leri (22):**
- `DELETE ${BASE}/departments/${id}`
- `DELETE ${BASE}/faculties/${id}`
- `DELETE ${BASE}/programs/${id}`
- `GET ${BASE}/departments`
- `GET ${BASE}/departments/${departmentId}/deletion-summary`
- `GET ${BASE}/faculties`
- `GET ${BASE}/faculties/${facultyId}/deletion-summary`
- `GET ${BASE}/programs`
- `GET ${BASE}/programs/${programId}/deletion-summary`
- `POST ${API}/${termId}/activate`
- `POST ${API}/${termId}/cancel`
- `POST ${API}/${termId}/deactivate`
- `POST ${API}/${termId}/uncancel`
- `POST ${BASE}/departments`
- `POST ${BASE}/departments/bulk`
- `POST ${BASE}/faculties`
- `POST ${BASE}/faculties/bulk`
- `POST ${BASE}/programs`
- `PUT ${API}/${id}`
- `PUT ${BASE}/departments/${id}`
- `PUT ${BASE}/faculties/${id}`
- `PUT ${BASE}/programs/${id}`

---


## Duyuru Yönetimi

### Duyuru Kategorileri

| | |
|---|---|
| **Route** | `/duyuru-kategorileri` |
| **Dosya** | `SIS.Web/src/pages/AnnouncementCategoryPage.tsx` |
| **Roller** | dean, departmentManager, systemAdmin |
| **Modal sayısı** | 2 (Dialog: 1, Sheet: 0, AlertDialog: 1) |

**Veri kancaları (4):**
- `useAnnouncementCategories()`
- `useCreateAnnouncementCategory()`
- `useDeleteAnnouncementCategory()`
- `useUpdateAnnouncementCategory()`

---

### Duyuru Yönetimi

| | |
|---|---|
| **Route** | `/duyuru-yonetimi` |
| **Dosya** | `SIS.Web/src/pages/AnnouncementManagementPage.tsx` |
| **Roller** | administrativeStaff, advisor, dean, departmentManager, instructor, systemAdmin |
| **Modal sayısı** | 2 (Dialog: 0, Sheet: 0, AlertDialog: 2) |

**Veri kancaları (5):**
- `useAllAnnouncements()`
- `useAnnouncementDetail()`
- `useDeleteAnnouncement()`
- `usePermanentDeleteAnnouncement()`
- `useReactivateAnnouncement()`

---


## Hesabım

### Şifre Değiştir

| | |
|---|---|
| **Route** | `/sifre-degistir` |
| **Dosya** | `SIS.Web/src/pages/PasswordChangePage.tsx` |
| **Roller** | student |
| **Modal sayısı** | 0 (Dialog: 0, Sheet: 0, AlertDialog: 0) |

---


## Personel İşlemleri

### Personel Listesi

| | |
|---|---|
| **Route** | `/personel-listesi` |
| **Dosya** | `SIS.Web/src/pages/InstructorDirectoryPage.tsx` |
| **Roller** | systemAdmin |
| **Modal sayısı** | 2 (Dialog: 2, Sheet: 0, AlertDialog: 0) |

**Veri kancaları (3):**
- `useAdminInstructors()`
- `useCreateInstructor()`
- `useAdminDepartments()`

**API endpoint'leri (3):**
- `GET /admin/advisors`
- `GET /admin/departments`
- `GET /admin/programs`

---


## Öğretim İşlemleri

### Ders Kısıtları

| | |
|---|---|
| **Route** | `/danisman-ders-kisitlari` |
| **Dosya** | `SIS.Web/src/pages/AdvisorCourseConstraintsPage.tsx` |
| **Roller** | advisor, instructor |
| **Modal sayısı** | 0 (Dialog: 0, Sheet: 0, AlertDialog: 0) |

**Veri kancaları (7):**
- `useAdvisorConstraints()`
- `useAdvisorStudents()`
- `useDeleteAdvisorConstraint()`
- `useUpdateAdvisorConstraint()`
- `useUpsertAdvisorConstraint()`
- `useStudentEligibility()`
- `useActiveTerm()`

---

### Ders Programım

| | |
|---|---|
| **Route** | `/ders-programim` |
| **Dosya** | `SIS.Web/src/pages/InstructorSchedulingPage.tsx` |
| **Roller** | advisor, dean, departmentManager, instructor |
| **Modal sayısı** | 0 (Dialog: 0, Sheet: 0, AlertDialog: 0) |

**Veri kancaları (1):**
- `useInstructorSections()`

**API endpoint'leri (4):**
- `GET /scheduling/instructor/${instructorId}/timetable`
- `POST /instructor/section/${sectionId}/schedule/apply`
- `POST /instructor/section/${sectionId}/schedule/suggest`
- `POST /instructor/section/${sectionId}/schedule/validate`

---

### Derslerim

| | |
|---|---|
| **Route** | `/derslerim` |
| **Dosya** | `SIS.Web/src/pages/InstructorSectionsPage.tsx` |
| **Roller** | advisor, dean, departmentManager, instructor |
| **Modal sayısı** | 0 (Dialog: 0, Sheet: 0, AlertDialog: 0) |

**Veri kancaları (2):**
- `useInstructorSections()`
- `useSectionStudents()`

---

### Not Girişi

| | |
|---|---|
| **Route** | `/not-girisi` |
| **Dosya** | `(bulunamadı)` |
| **Roller** | dean, departmentManager, instructor |
---

### Yoklama Yönetimi

| | |
|---|---|
| **Route** | `/yoklama-girisi` |
| **Dosya** | `SIS.Web/src/pages/AttendanceEntryPage.tsx` |
| **Roller** | advisor, dean, departmentManager, instructor |
| **Modal sayısı** | 0 (Dialog: 0, Sheet: 0, AlertDialog: 0) |

**Sekmeler (4):**
- `entry`
- `manual`
- `qr`
- `history`

**Veri kancaları (7):**
- `useInstructorSections()`
- `useAttendanceMatrix()`
- `useAttendanceSession()`
- `useCloseQrAttendance()`
- `useCreateAttendanceSession()`
- `useOpenQrAttendance()`
- `useSaveAttendanceRecords()`

---


## Raporlar

### Raporlarım

| | |
|---|---|
| **Route** | `/raporlarim` |
| **Dosya** | `SIS.Web/src/pages/MyReportsPage.tsx` |
| **Roller** | administrativeStaff, advisor, dean, departmentManager, instructor, student, systemAdmin |
| **Modal sayısı** | 0 (Dialog: 0, Sheet: 0, AlertDialog: 0) |

---

