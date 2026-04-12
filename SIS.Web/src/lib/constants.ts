export const ROUTES = {
  LOGIN: '/login',
  DASHBOARD: '/',
  AKADEMIK_TAKVIM: '/akademik-takvim',
  DERS_KAYIT: '/ders-kayit',
  PLAN: '/plan',
  ALINAN_DERSLER: '/alinan-dersler',
  DONEM_ORTALAMALARI: '/donem-ortalamalari',
  MUFREDAT_DURUM: '/mufredat-durum',
  // Instructor routes
  INSTRUCTOR_SECTIONS: '/derslerim',
  INSTRUCTOR_SCHEDULING: '/ders-programim',
  GRADE_ENTRY: '/not-girisi',
  CLASS_LISTS: '/sinif-listeleri',
  CLASSROOM_DASHBOARD: '/derslik-doluluk-tablosu',
  TERM_CLOSING: '/donem-kapatma',
  SCHEDULE_CREATION: '/ders-programi-olusturma',
  CLASSROOM_ASSIGNMENT: '/derslik-fakulte-atamasi',
  COURSE_OPENING: '/ders-acma-islemleri',
  INSTRUCTOR_ASSIGNMENT: '/ogretim-elemani-atama',
  // Admin routes
  USER_MANAGEMENT: '/kullanici-yonetimi',
  STUDENT_DIRECTORY: '/ogrenci-rehberi',
  INSTRUCTOR_DIRECTORY: '/personel-listesi',
  TERM_MANAGEMENT: '/donem-yonetimi',
  ACADEMIC_STRUCTURE: '/akademik-yapi',
  COURSE_CATALOG_ADMIN: '/ders-katalogu',
  SYSTEM_CONFIG: '/sistem-ayarlari',
  AUDIT_LOG: '/denetim-kayitlari',
  PROMOTION_RULES: '/sinif-atlatma-kurallari',
  SCHEDULE_APPROVAL: '/program-onayi',
  CLASSROOM_ADMIN: '/derslik-yonetimi',
  CONSTRAINT_MANAGEMENT: '/ders-programi-kisitlamalari',
  CURRICULUM_MANAGEMENT: '/mufredat-islemleri',
  ELECTIVE_GROUPS: '/secmeli-ders-gruplari',
  INSTRUCTOR_COURSES_REPORT: '/ders-veren-ogretim-uyeleri',
  TERM_PROGRAM_TRANSFER: '/donem-programi-aktarim',
} as const;

export const QUERY_KEYS = {
  activeTerm: ['activeTerm'] as const,
  courses: (programId?: number) => ['courses', programId] as const,
  studentCatalog: ['studentCatalog'] as const,
  myRegistrations: ['myRegistrations'] as const,
  studentInfo: ['studentInfo'] as const,
  termGpaHistory: ['termGpaHistory'] as const,
  curriculumStatus: ['curriculumStatus'] as const,
  instructorTimetable: ['instructorTimetable'] as const,
  academicOperationsContext: ['academicOperationsContext'] as const,
  academicPlanningSummary: (departmentId?: number, termId?: number) => ['academicPlanningSummary', departmentId, termId] as const,
  departmentClassLists: (departmentId?: number, termId?: number) => ['departmentClassLists', departmentId, termId] as const,
  departmentCurriculumOverview: (departmentId?: number) => ['departmentCurriculumOverview', departmentId] as const,
  departmentAcademicLevels: (departmentId?: number) => ['departmentAcademicLevels', departmentId] as const,
  gradeComponents: (sectionId: number) => ['gradeComponents', sectionId] as const,
  grades: (sectionId: number) => ['grades', sectionId] as const,
  gradingCriteria: (sectionId: number) => ['gradingCriteria', sectionId] as const,
  facultyPlanningOverview: (facultyId: number, termId: number) => ['facultyPlanning', 'overview', facultyId, termId] as const,
  facultyPlanningSections: (facultyId: number, termId: number, departmentId?: number, status?: string, page?: number) =>
    ['facultyPlanning', 'sections', facultyId, termId, departmentId, status, page] as const,
  facultyPlanningConflicts: (facultyId: number, termId: number) => ['facultyPlanning', 'conflicts', facultyId, termId] as const,
  courseOpeningList: (departmentId: number, termId: number) => ['courseOpening', 'list', departmentId, termId] as const,
  curriculumCoursesForOpening: (departmentId: number, termId: number) => ['courseOpening', 'curriculum', departmentId, termId] as const,
  departmentInstructors: (departmentId: number) => ['courseOpening', 'instructors', departmentId] as const,
  forecastDepartmentSummary: (departmentId: number) => ['forecast', 'department', departmentId] as const,
  // Admin query keys
  adminUsers: (search?: string, role?: string, isActive?: boolean, page?: number) =>
    ['admin', 'users', search, role, isActive, page] as const,
  adminStudents: (search?: string, programId?: number, departmentId?: number, status?: string, page?: number) =>
    ['admin', 'students', search, programId, departmentId, status, page] as const,
  adminInstructors: (search?: string, departmentId?: number) =>
    ['admin', 'instructors', search, departmentId] as const,
  adminTerms: ['admin', 'terms'] as const,
  systemConfigs: ['admin', 'systemConfigs'] as const,
  auditLogs: (params?: Record<string, unknown>) => ['admin', 'auditLogs', params] as const,
  promotionRules: (programId?: number) => ['admin', 'promotionRules', programId] as const,
  constraints: ['constraints'] as const,
  constraintDetail: (id: number) => ['constraints', 'detail', id] as const,
  scheduleApprovalStatus: (facultyId: number, termId?: number) => ['scheduleApproval', facultyId, termId] as const,
  adminFaculties: ['admin', 'faculties'] as const,
  adminPrograms: (departmentId?: number) => ['admin', 'programs', departmentId] as const,
  electiveGroups: (subtype?: string, facultyId?: number) => ['electiveGroups', 'list', subtype, facultyId] as const,
  electiveGroupDetail: (id: number) => ['electiveGroups', 'detail', id] as const,
  curriculumPools: (curriculumId: number) => ['curriculum', 'pools', curriculumId] as const,
};

export const STATUS_LABELS: Record<string, string> = {
  Draft: 'Taslak',
  PendingAdvisorApproval: 'Danışman Onayı Bekleniyor',
  Active: 'Aktif',
  Completed: 'Tamamlandı',
  Dropped: 'Bırakıldı',
  Withdrawn: 'Çekildi',
  Rejected: 'Reddedildi',
};

export const STATUS_VARIANT: Record<string, 'default' | 'secondary' | 'destructive' | 'outline' | 'success' | 'warning'> = {
  Draft: 'secondary',
  PendingAdvisorApproval: 'warning',
  Active: 'success',
  Completed: 'default',
  Dropped: 'destructive',
  Withdrawn: 'outline',
  Rejected: 'destructive',
};

/** Routes that have actual page implementations in App.tsx */
export const IMPLEMENTED_ROUTES = new Set([
  '/',
  '/akademik-takvim',
  '/ders-kayit',
  '/plan',
  '/alinan-dersler',
  '/donem-ortalamalari',
  '/mufredat-durum',
  '/derslerim',
  '/not-girisi',
  '/ders-programim',
  '/sinif-listeleri',
  '/ders-programi-olusturma',
  '/derslik-doluluk-tablosu',
  '/derslik-fakulte-atamasi',
  '/donem-kapatma',
  '/ders-acma-islemleri',
  '/ogretim-elemani-atama',
  '/kullanici-yonetimi',
  '/ogrenci-rehberi',
  '/personel-listesi',
  '/donem-yonetimi',
  '/akademik-yapi',
  '/ders-katalogu',
  '/sistem-ayarlari',
  '/denetim-kayitlari',
  '/sinif-atlatma-kurallari',
  '/program-onayi',
  '/derslik-yonetimi',
  '/ders-programi-kisitlamalari',
  '/mufredat-islemleri',
  '/secmeli-ders-gruplari',
  '/ders-veren-ogretim-uyeleri',
  '/donem-programi-aktarim',
]);

export const STUDENT_STATUS_LABELS: Record<string, string> = {
  Active: 'Aktif',
  Passive: 'Pasif',
  Frozen: 'Dondurulmuş',
  Suspended: 'Uzaklaştırılmış',
  Dismissed: 'İlişik Kesilmiş',
  Graduated: 'Mezun',
};

export const COURSE_LANGUAGE_LABELS: Record<string, string> = {
  Turkish: 'Türkçe',
  English: 'İngilizce',
  French: 'Fransızca',
  German: 'Almanca',
  Arabic: 'Arapça',
};

export const TEACHING_METHOD_LABELS: Record<string, string> = {
  FaceToFace: 'Yüz Yüze',
  Online: 'Uzaktan',
  Hybrid: 'Hibrit',
};

export const COURSE_STATUS_LABELS: Record<string, string> = {
  Active: 'Aktif',
  Passive: 'Pasif',
};

export const COURSE_LEVEL_LABELS: Record<string, string> = {
  Undergraduate: 'Lisans',
  Masters: 'Yüksek Lisans',
  Doctorate: 'Doktora',
};
