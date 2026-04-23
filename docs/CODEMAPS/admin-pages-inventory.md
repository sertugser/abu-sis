# Admin / Kullanıcı Sayfa Envanteri

**Üretim tarihi:** 2026-04-23 01:31
**Üreten:** `SIS.Web/scripts/generate-codemap.mjs`

> ⚠️ **OTOMATIK ÜRETILIR — elle düzenleme.** Script yeniden çalışınca üzerine yazılır.
> Kod değiştikten sonra: `pnpm codemap:generate` veya `node scripts/generate-codemap.mjs`

## Özet

- **Toplam sayfa:** 41
- **Sidebar grubu:** 11

## İçindekiler

- [Ders İşlemleri](#ders-i̇slemleri) (15 sayfa)
- [Anketler](#anketler) (3 sayfa)
- [Kaynak Yönetimi](#kaynak-yonetimi) (2 sayfa)
- [Kullanıcı Yönetimi](#kullanici-yonetimi) (2 sayfa)
- [Akademik İşlemler](#akademik-i̇slemler) (4 sayfa)
- [Danışmanlık İşlemleri](#danismanlik-i̇slemleri) (1 sayfa)
- [Ders ve Dönem İşlemleri](#ders-ve-donem-i̇slemleri) (7 sayfa)
- [Duyuru Yönetimi](#duyuru-yonetimi) (1 sayfa)
- [Genel İşlemler](#genel-i̇slemler) (3 sayfa)
- [Öğrenci İşlemleri](#ogrenci-i̇slemleri) (1 sayfa)
- [Sistem](#sistem) (2 sayfa)

---

## Ders İşlemleri

### Ders Açma İşlemleri

| | |
|---|---|
| **Route** | `/ders-acma-islemleri` |
| **Dosya** | `SIS.Web/src/pages/CourseOpeningPage.tsx` |
| **Roller** | departmentManager, dean, systemAdmin |
| **Modal sayısı** | 0 (Dialog: 0, Sheet: 0, AlertDialog: 0) |

**Veri kancaları (16):**
- `useAcademicOperationsContext()`
- `useActiveTerm()`
- `useAdminFaculties()`
- `useAdminDepartmentsByStructure()`
- `useBulkOpenCourses()`
- `useCloseSection()`
- `useCloseAllSections()`
- `useReactivateSection()`
- `useCurriculumCoursesForOpening()`
- `useDepartmentInstructors()`
- `useExternalCourses()`
- `useInstructorWorkload()`
- `useOpenCourse()`
- `useOpenedCourses()`
- `useUpdateOpenedSection()`
- `useDepartmentForecast()`

**API endpoint'leri (12):**
- `DELETE ${BASE}/departments/${id}`
- `DELETE ${BASE}/faculties/${id}`
- `DELETE ${BASE}/programs/${id}`
- `GET ${BASE}/departments`
- `GET ${BASE}/faculties`
- `GET ${BASE}/programs`
- `POST ${BASE}/departments`
- `POST ${BASE}/faculties`
- `POST ${BASE}/programs`
- `PUT ${BASE}/departments/${id}`
- `PUT ${BASE}/faculties/${id}`
- `PUT ${BASE}/programs/${id}`

---

### Ders Kataloğu

| | |
|---|---|
| **Route** | `/ders-katalogu` |
| **Dosya** | `SIS.Web/src/pages/CourseCatalogAdminPage.tsx` |
| **Roller** | departmentManager, dean, systemAdmin |
| **Modal sayısı** | 2 (Dialog: 2, Sheet: 0, AlertDialog: 0) |

**Sekmeler (4):**
- `temel`
- `onkosullar`
- `mufredat`
- `istatistik`

**Veri kancaları (14):**
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
- `useAdminFaculties()`
- `useAdminDepartmentsByStructure()`

**API endpoint'leri (22):**
- `DELETE ${BASE}/${courseId}/cross-lists/${linkedCourseId}`
- `DELETE ${BASE}/${courseId}/equivalencies/${equivalentCourseId}`
- `DELETE ${BASE}/${courseId}/prerequisites/${requiredCourseId}`
- `DELETE ${BASE}/${id}`
- `DELETE ${BASE}/departments/${id}`
- `DELETE ${BASE}/faculties/${id}`
- `DELETE ${BASE}/programs/${id}`
- `GET ${BASE}/${courseId}/cross-lists`
- `GET ${BASE}/${id}`
- `GET ${BASE}/departments`
- `GET ${BASE}/faculties`
- `GET ${BASE}/programs`
- `POST ${BASE}/${courseId}/cross-lists/${linkedCourseId}`
- `POST ${BASE}/${courseId}/equivalencies`
- `POST ${BASE}/${courseId}/prerequisites`
- `POST ${BASE}/departments`
- `POST ${BASE}/faculties`
- `POST ${BASE}/programs`
- `PUT ${BASE}/${id}`
- `PUT ${BASE}/departments/${id}`
- `PUT ${BASE}/faculties/${id}`
- `PUT ${BASE}/programs/${id}`

---

### Ders Programı Oluşturma

| | |
|---|---|
| **Route** | `/ders-programi-olusturma` |
| **Dosya** | `SIS.Web/src/pages/FacultyPlanningPage.tsx` |
| **Roller** | dean, systemAdmin |
| **Modal sayısı** | 0 (Dialog: 0, Sheet: 0, AlertDialog: 0) |

**Veri kancaları (5):**
- `useAcademicOperationsContext()`
- `useActiveTerm()`
- `useAdminFaculties()`
- `useExportExcel()`
- `useExportPdf()`

**API endpoint'leri (12):**
- `DELETE ${BASE}/departments/${id}`
- `DELETE ${BASE}/faculties/${id}`
- `DELETE ${BASE}/programs/${id}`
- `GET ${BASE}/departments`
- `GET ${BASE}/faculties`
- `GET ${BASE}/programs`
- `POST ${BASE}/departments`
- `POST ${BASE}/faculties`
- `POST ${BASE}/programs`
- `PUT ${BASE}/departments/${id}`
- `PUT ${BASE}/faculties/${id}`
- `PUT ${BASE}/programs/${id}`

---

### Ders Programım

| | |
|---|---|
| **Route** | `/ders-programim` |
| **Dosya** | `SIS.Web/src/pages/InstructorSchedulingPage.tsx` |
| **Roller** | advisor, instructor, departmentManager, dean |
| **Modal sayısı** | 0 (Dialog: 0, Sheet: 0, AlertDialog: 0) |

**Veri kancaları (1):**
- `useInstructorSections()`

**API endpoint'leri (4):**
- `GET /scheduling/instructor/${instructorId}/timetable`
- `POST /instructor/section/${sectionId}/schedule/apply`
- `POST /instructor/section/${sectionId}/schedule/suggest`
- `POST /instructor/section/${sectionId}/schedule/validate`

---

### Ders Veren Öğretim Üyeleri

| | |
|---|---|
| **Route** | `/ders-veren-ogretim-uyeleri` |
| **Dosya** | `SIS.Web/src/pages/InstructorCoursesReportPage.tsx` |
| **Roller** | departmentManager, dean, systemAdmin |
| **Modal sayısı** | 0 (Dialog: 0, Sheet: 0, AlertDialog: 0) |

**Veri kancaları (5):**
- `useAdminFaculties()`
- `useAdminDepartmentsByStructure()`
- `useDepartmentInstructors()`
- `useInstructorWorkload()`
- `useActiveTerm()`

**API endpoint'leri (12):**
- `DELETE ${BASE}/departments/${id}`
- `DELETE ${BASE}/faculties/${id}`
- `DELETE ${BASE}/programs/${id}`
- `GET ${BASE}/departments`
- `GET ${BASE}/faculties`
- `GET ${BASE}/programs`
- `POST ${BASE}/departments`
- `POST ${BASE}/faculties`
- `POST ${BASE}/programs`
- `PUT ${BASE}/departments/${id}`
- `PUT ${BASE}/faculties/${id}`
- `PUT ${BASE}/programs/${id}`

---

### Derslerim

| | |
|---|---|
| **Route** | `/derslerim` |
| **Dosya** | `SIS.Web/src/pages/InstructorSectionsPage.tsx` |
| **Roller** | advisor, instructor, departmentManager, dean |
| **Modal sayısı** | 0 (Dialog: 0, Sheet: 0, AlertDialog: 0) |

**Veri kancaları (2):**
- `useInstructorSections()`
- `useSectionStudents()`

---

### Derslik Doluluk Tablosu

| | |
|---|---|
| **Route** | `/derslik-doluluk-tablosu` |
| **Dosya** | `SIS.Web/src/pages/ClassroomManagementPage.tsx` |
| **Roller** | dean, systemAdmin |
| **Modal sayısı** | 0 (Dialog: 0, Sheet: 0, AlertDialog: 0) |

---

### Dönem Kapatma İşlemleri

| | |
|---|---|
| **Route** | `/donem-kapatma` |
| **Dosya** | `SIS.Web/src/pages/term-closing/TermClosingPage.tsx` |
| **Roller** | departmentManager, dean, systemAdmin |
| **Modal sayısı** | 0 (Dialog: 0, Sheet: 0, AlertDialog: 0) |

---

### Dönem Programı Aktarım

| | |
|---|---|
| **Route** | `/donem-programi-aktarim` |
| **Dosya** | `SIS.Web/src/pages/TermProgramTransferPage.tsx` |
| **Roller** | departmentManager, dean, systemAdmin |
| **Modal sayısı** | 1 (Dialog: 0, Sheet: 0, AlertDialog: 1) |

**Veri kancaları (6):**
- `useAcademicOperationsContext()`
- `useActiveTerm()`
- `useAdminTerms()`
- `usePreviousTermCourses()`
- `useTransferFromTerm()`
- `useDepartmentInstructors()`

**API endpoint'leri (4):**
- `POST ${API}/${termId}/cancel`
- `POST ${API}/${termId}/override`
- `POST ${API}/${termId}/uncancel`
- `PUT ${API}/${id}`

---

### Müfredat İşlemleri

| | |
|---|---|
| **Route** | `/mufredat-islemleri` |
| **Dosya** | `SIS.Web/src/pages/CurriculumManagementPage.tsx` |
| **Roller** | departmentManager, dean, systemAdmin |
| **Modal sayısı** | 1 (Dialog: 1, Sheet: 0, AlertDialog: 0) |

**Veri kancaları (12):**
- `useAdminFaculties()`
- `useAdminDepartmentsByStructure()`
- `useAdminProgramsByStructure()`
- `useCurriculumList()`
- `useCurriculumDetail()`
- `useCreateCurriculum()`
- `useDeleteCurriculum()`
- `useActivateCurriculum()`
- `useAddCurriculumCourse()`
- `useUpdateCurriculumCourse()`
- `useRemoveCurriculumCourse()`
- `useCopyCurriculum()`

**API endpoint'leri (12):**
- `DELETE ${BASE}/departments/${id}`
- `DELETE ${BASE}/faculties/${id}`
- `DELETE ${BASE}/programs/${id}`
- `GET ${BASE}/departments`
- `GET ${BASE}/faculties`
- `GET ${BASE}/programs`
- `POST ${BASE}/departments`
- `POST ${BASE}/faculties`
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
| **Roller** | departmentManager, dean, systemAdmin |
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

**API endpoint'leri (12):**
- `DELETE ${BASE}/departments/${id}`
- `DELETE ${BASE}/faculties/${id}`
- `DELETE ${BASE}/programs/${id}`
- `GET ${BASE}/departments`
- `GET ${BASE}/faculties`
- `GET ${BASE}/programs`
- `POST ${BASE}/departments`
- `POST ${BASE}/faculties`
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
| **Roller** | departmentManager, dean, systemAdmin |
| **Modal sayısı** | 2 (Dialog: 0, Sheet: 0, AlertDialog: 2) |

**Veri kancaları (10):**
- `useConstraints()`
- `useToggleConstraint()`
- `useUpdateConstraintCategory()`
- `useScoreBreakdown()`
- `useUpdateConstraintWeight()`
- `useAdminTerms()`
- `useTrendData()`
- `useHeatmap()`
- `useBottlenecks()`
- `useTakeSnapshot()`

**API endpoint'leri (4):**
- `POST ${API}/${termId}/cancel`
- `POST ${API}/${termId}/override`
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

**API endpoint'leri (12):**
- `DELETE ${BASE}/departments/${id}`
- `DELETE ${BASE}/faculties/${id}`
- `DELETE ${BASE}/programs/${id}`
- `GET ${BASE}/departments`
- `GET ${BASE}/faculties`
- `GET ${BASE}/programs`
- `POST ${BASE}/departments`
- `POST ${BASE}/faculties`
- `POST ${BASE}/programs`
- `PUT ${BASE}/departments/${id}`
- `PUT ${BASE}/faculties/${id}`
- `PUT ${BASE}/programs/${id}`

---

### Seçmeli Ders Grupları

| | |
|---|---|
| **Route** | `/secmeli-ders-gruplari` |
| **Dosya** | `SIS.Web/src/pages/ElectiveGroupManagementPage.tsx` |
| **Roller** | departmentManager, dean, systemAdmin |
| **Modal sayısı** | 1 (Dialog: 1, Sheet: 0, AlertDialog: 0) |

**Veri kancaları (3):**
- `useElectiveGroupList()`
- `useCreateElectiveGroup()`
- `useAdminFaculties()`

**API endpoint'leri (12):**
- `DELETE ${BASE}/departments/${id}`
- `DELETE ${BASE}/faculties/${id}`
- `DELETE ${BASE}/programs/${id}`
- `GET ${BASE}/departments`
- `GET ${BASE}/faculties`
- `GET ${BASE}/programs`
- `POST ${BASE}/departments`
- `POST ${BASE}/faculties`
- `POST ${BASE}/programs`
- `PUT ${BASE}/departments/${id}`
- `PUT ${BASE}/faculties/${id}`
- `PUT ${BASE}/programs/${id}`

---

### Sınıf Listeleri

| | |
|---|---|
| **Route** | `/sinif-listeleri` |
| **Dosya** | `SIS.Web/src/pages/ClassListsPage.tsx` |
| **Roller** | departmentManager, dean, systemAdmin |
| **Modal sayısı** | 0 (Dialog: 0, Sheet: 0, AlertDialog: 0) |

**Veri kancaları (5):**
- `useAcademicOperationsContext()`
- `useDepartmentClassLists()`
- `useActiveTerm()`
- `useAdminFaculties()`
- `useAdminDepartmentsByStructure()`

**API endpoint'leri (18):**
- `DELETE ${BASE}/departments/${id}`
- `DELETE ${BASE}/faculties/${id}`
- `DELETE ${BASE}/programs/${id}`
- `GET ${BASE}/departments`
- `GET ${BASE}/faculties`
- `GET ${BASE}/programs`
- `GET /academicLevel/department/${departmentId}`
- `GET /academicoperations/class-lists`
- `GET /academicoperations/context`
- `GET /academicoperations/curriculum-overview`
- `GET /academicoperations/planning-summary`
- `GET /academicprogression/grade-lock-readiness/${departmentId}`
- `POST ${BASE}/departments`
- `POST ${BASE}/faculties`
- `POST ${BASE}/programs`
- `PUT ${BASE}/departments/${id}`
- `PUT ${BASE}/faculties/${id}`
- `PUT ${BASE}/programs/${id}`

---


## Anketler

### Değerlendirme Dönemi Yönetimi

| | |
|---|---|
| **Route** | `/degerlendirme-donemi-yonetimi` |
| **Dosya** | `SIS.Web/src/pages/EvaluationPeriodAdminPage.tsx` |
| **Roller** | systemAdmin |
| **Modal sayısı** | 3 (Dialog: 1, Sheet: 0, AlertDialog: 2) |

**Veri kancaları (8):**
- `useActiveEvaluationPeriod()`
- `useAllEvaluationQuestions()`
- `useCloseEvaluationPeriod()`
- `useCreateEvaluationQuestion()`
- `useDeleteEvaluationQuestion()`
- `useOpenEvaluationPeriod()`
- `useUpdateEvaluationQuestion()`
- `useAdminTerms()`

**API endpoint'leri (4):**
- `POST ${API}/${termId}/cancel`
- `POST ${API}/${termId}/override`
- `POST ${API}/${termId}/uncancel`
- `PUT ${API}/${id}`

---

### Değerlendirme Sonuçları

| | |
|---|---|
| **Route** | `/degerlendirme-sonuclari` |
| **Dosya** | `SIS.Web/src/pages/EvaluationResultsPage.tsx` |
| **Roller** | instructor, departmentManager, dean, systemAdmin |
| **Modal sayısı** | 1 (Dialog: 1, Sheet: 0, AlertDialog: 0) |

**Sekmeler (2):**
- `management`
- `instructor`

**Veri kancaları (8):**
- `useInstructorSections()`
- `useInstructorTerms()`
- `useActiveEvaluationPeriod()`
- `useDepartmentEvaluationSummary()`
- `useSectionEvaluationResults()`
- `useAdminFaculties()`
- `useAdminDepartmentsByStructure()`
- `useAdminTerms()`

**API endpoint'leri (16):**
- `DELETE ${BASE}/departments/${id}`
- `DELETE ${BASE}/faculties/${id}`
- `DELETE ${BASE}/programs/${id}`
- `GET ${BASE}/departments`
- `GET ${BASE}/faculties`
- `GET ${BASE}/programs`
- `POST ${API}/${termId}/cancel`
- `POST ${API}/${termId}/override`
- `POST ${API}/${termId}/uncancel`
- `POST ${BASE}/departments`
- `POST ${BASE}/faculties`
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
| **Roller** | student |
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

**Veri kancaları (8):**
- `useAdminTerms()`
- `useAdminFaculties()`
- `useClassrooms()`
- `useClassroomAssignments()`
- `useCreateClassroomAssignment()`
- `useDeleteClassroomAssignment()`
- `useBulkAssignClassrooms()`
- `useSuggestClassroomAssignments()`

**API endpoint'leri (16):**
- `DELETE ${BASE}/departments/${id}`
- `DELETE ${BASE}/faculties/${id}`
- `DELETE ${BASE}/programs/${id}`
- `GET ${BASE}/departments`
- `GET ${BASE}/faculties`
- `GET ${BASE}/programs`
- `POST ${API}/${termId}/cancel`
- `POST ${API}/${termId}/override`
- `POST ${API}/${termId}/uncancel`
- `POST ${BASE}/departments`
- `POST ${BASE}/faculties`
- `POST ${BASE}/programs`
- `PUT ${API}/${id}`
- `PUT ${BASE}/departments/${id}`
- `PUT ${BASE}/faculties/${id}`
- `PUT ${BASE}/programs/${id}`

---


## Kullanıcı Yönetimi

### Kullanıcı Listesi

| | |
|---|---|
| **Route** | `/kullanici-yonetimi` |
| **Dosya** | `SIS.Web/src/pages/UserManagementPage.tsx` |
| **Roller** | systemAdmin |
| **Modal sayısı** | 2 (Dialog: 2, Sheet: 0, AlertDialog: 0) |

**Veri kancaları (4):**
- `useAdminUsers()`
- `useUpdateUserRoles()`
- `useResetPassword()`
- `useSetUserStatus()`

**API endpoint'leri (4):**
- `GET ${API}/${id}`
- `POST ${API}/${id}/reset-password`
- `PUT ${API}/${id}/roles`
- `PUT ${API}/${id}/status`

---

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


## Akademik İşlemler

### Akademik Dönem Tanımları

| | |
|---|---|
| **Route** | `/donem-yonetimi` |
| **Dosya** | `SIS.Web/src/pages/TermManagementPage.tsx` |
| **Roller** | systemAdmin |
| **Modal sayısı** | 3 (Dialog: 2, Sheet: 0, AlertDialog: 1) |

**Veri kancaları (6):**
- `useAdminTerms()`
- `useCreateTerm()`
- `useUpdateTerm()`
- `useCancelTerm()`
- `useUncancelTerm()`
- `useUpdateTermPhaseOverride()`

**API endpoint'leri (4):**
- `POST ${API}/${termId}/cancel`
- `POST ${API}/${termId}/override`
- `POST ${API}/${termId}/uncancel`
- `PUT ${API}/${id}`

---

### Fakülte / Bölüm / Program

| | |
|---|---|
| **Route** | `/akademik-yapi` |
| **Dosya** | `SIS.Web/src/pages/AcademicStructurePage.tsx` |
| **Roller** | systemAdmin |
| **Modal sayısı** | 7 (Dialog: 7, Sheet: 0, AlertDialog: 0) |

**Veri kancaları (12):**
- `useAdminFaculties()`
- `useAdminDepartmentsByStructure()`
- `useAdminProgramsByStructure()`
- `useCreateFaculty()`
- `useUpdateFaculty()`
- `useDeleteFaculty()`
- `useCreateDepartment()`
- `useUpdateDepartment()`
- `useDeleteDepartment()`
- `useCreateProgram()`
- `useUpdateProgram()`
- `useDeleteProgram()`

**API endpoint'leri (12):**
- `DELETE ${BASE}/departments/${id}`
- `DELETE ${BASE}/faculties/${id}`
- `DELETE ${BASE}/programs/${id}`
- `GET ${BASE}/departments`
- `GET ${BASE}/faculties`
- `GET ${BASE}/programs`
- `POST ${BASE}/departments`
- `POST ${BASE}/faculties`
- `POST ${BASE}/programs`
- `PUT ${BASE}/departments/${id}`
- `PUT ${BASE}/faculties/${id}`
- `PUT ${BASE}/programs/${id}`

---

### Sınıf Atlatma Kuralları

| | |
|---|---|
| **Route** | `/sinif-atlatma-kurallari` |
| **Dosya** | `SIS.Web/src/pages/PromotionRulePage.tsx` |
| **Roller** | systemAdmin |
| **Modal sayısı** | 2 (Dialog: 2, Sheet: 0, AlertDialog: 0) |

**Veri kancaları (5):**
- `usePromotionRules()`
- `useCreatePromotionRule()`
- `useUpdatePromotionRule()`
- `useDeletePromotionRule()`
- `useAdminPrograms()`

**API endpoint'leri (3):**
- `GET /admin/advisors`
- `GET /admin/departments`
- `GET /admin/programs`

---

### Takvim Oluşturucu

| | |
|---|---|
| **Route** | `/akademik-takvim-olusturma` |
| **Dosya** | `SIS.Web/src/pages/AcademicCalendarGeneratorPage.tsx` |
| **Roller** | systemAdmin |
| **Modal sayısı** | 0 (Dialog: 0, Sheet: 0, AlertDialog: 0) |

**Sekmeler (3):**
- `generator`
- `draft`
- `holidays`

**Veri kancaları (10):**
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

**API endpoint'leri (11):**
- `DELETE ${API}/drafts/${draftId}`
- `GET ${API}/drafts`
- `GET ${API}/drafts/${draftId}`
- `GET ${API}/holidays/definitions`
- `GET ${API}/templates`
- `POST ${API}/drafts/${draftId}/apply`
- `POST ${API}/drafts/${draftId}/rollback`
- `POST ${API}/drafts/${draftId}/what-if`
- `POST ${API}/generate`
- `PUT ${API}/drafts/${draftId}/events/${encodeURIComponent(eventId)}`
- `PUT ${API}/templates`

---


## Danışmanlık İşlemleri

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
| **Modal sayısı** | 0 (Dialog: 0, Sheet: 0, AlertDialog: 0) |

**Veri kancaları (7):**
- `useStudentCatalog()`
- `useMyRegistrations()`
- `useAddToPlan()`
- `useDropCourse()`
- `useSubmitRegistration()`
- `useStudentInfo()`
- `useActiveTerm()`

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

### Kayıt Planım

| | |
|---|---|
| **Route** | `/plan` |
| **Dosya** | `SIS.Web/src/pages/RegistrationPlanPage.tsx` |
| **Roller** | student |
| **Modal sayısı** | 0 (Dialog: 0, Sheet: 0, AlertDialog: 0) |

**Veri kancaları (4):**
- `useMyRegistrations()`
- `useDropCourse()`
- `useSubmitRegistration()`
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

**Veri kancaları (2):**
- `useStudentInfo()`
- `useStudentTranscript()`

---


## Duyuru Yönetimi

### Duyuru Yönetimi

| | |
|---|---|
| **Route** | `/duyuru-yonetimi` |
| **Dosya** | `SIS.Web/src/pages/AnnouncementManagementPage.tsx` |
| **Roller** | departmentManager, dean, systemAdmin |
| **Modal sayısı** | 1 (Dialog: 0, Sheet: 0, AlertDialog: 1) |

**Veri kancaları (3):**
- `useAllAnnouncements()`
- `useAnnouncementDetail()`
- `useDeleteAnnouncement()`

---


## Genel İşlemler

### Alınan Dersler

| | |
|---|---|
| **Route** | `/alinan-dersler` |
| **Dosya** | `SIS.Web/src/pages/MyCoursesPage.tsx` |
| **Roller** | student |
| **Modal sayısı** | 0 (Dialog: 0, Sheet: 0, AlertDialog: 0) |

**Veri kancaları (1):**
- `useCourseHistory()`

---

### Ana Sayfa

| | |
|---|---|
| **Route** | `/` |
| **Dosya** | `(bulunamadı)` |
| **Roller** | student, advisor, instructor, departmentManager, dean, systemAdmin |
---

### Duyurular

| | |
|---|---|
| **Route** | `/duyurular` |
| **Dosya** | `SIS.Web/src/pages/AnnouncementsPage.tsx` |
| **Roller** | student, departmentManager, dean, systemAdmin |
| **Modal sayısı** | 1 (Dialog: 1, Sheet: 0, AlertDialog: 0) |

**Veri kancaları (2):**
- `useActiveAnnouncements()`
- `useAnnouncementDetail()`

---


## Öğrenci İşlemleri

### Öğrenci Rehberi

| | |
|---|---|
| **Route** | `/ogrenci-rehberi` |
| **Dosya** | `SIS.Web/src/pages/StudentDirectoryPage.tsx` |
| **Roller** | departmentManager, dean, systemAdmin |
| **Modal sayısı** | 2 (Dialog: 2, Sheet: 0, AlertDialog: 0) |

**Veri kancaları (5):**
- `useAdminStudents()`
- `useCreateStudent()`
- `useAdminDepartments()`
- `useAdminPrograms()`
- `useAdminAdvisors()`

**API endpoint'leri (5):**
- `GET ${API}/${id}`
- `GET /admin/advisors`
- `GET /admin/departments`
- `GET /admin/programs`
- `PUT ${API}/${studentId}/advisor`

---


## Sistem

### Denetim Kayıtları

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

### Sistem Ayarları

| | |
|---|---|
| **Route** | `/sistem-ayarlari` |
| **Dosya** | `SIS.Web/src/pages/SystemConfigPage.tsx` |
| **Roller** | systemAdmin |
| **Modal sayısı** | 1 (Dialog: 0, Sheet: 0, AlertDialog: 1) |

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

