export const ROUTES = {
  LOGIN: '/login',
  DASHBOARD: '/',
  KULLANIM_KILAVUZU: '/kullanim-kilavuzu',
  AKADEMIK_TAKVIM: '/akademik-takvim',
  DERS_KAYIT: '/ders-kayit',
  PLAN: '/plan',
  ALINAN_DERSLER: '/alinan-dersler',
  DONEM_ORTALAMALARI: '/donem-ortalamalari',
  MUFREDAT_DURUM: '/mufredat-durum',
  // Instructor routes
  INSTRUCTOR_SECTIONS: '/derslerim',
  INSTRUCTOR_SCHEDULING: '/program-planlama',
  GRADE_ENTRY: '/not-girisi',
  MANAGEMENT_PLANNING: '/planlama-merkezi',
  MANAGEMENT_CLASS_LISTS: '/sinif-listeleri',
  MANAGEMENT_CURRICULUM: '/mufredat-gorunumu',
  MANAGEMENT_BATCH_SCHEDULING: '/toplu-programlama',
  // New pages from technical analysis
  CLASSROOM_MANAGEMENT: '/merkezi-derslik-yonetimi',
  SECTION_APPROVAL: '/sube-acma-onayi',
  TERM_CLOSING: '/donem-kapatma',
  PRE_REGISTRATION_SURVEY: '/on-kayit-anketi',
} as const;

export const QUERY_KEYS = {
  activeTerm: ['activeTerm'] as const,
  courses: (programId?: number) => ['courses', programId] as const,
  studentCatalog: ['studentCatalog'] as const,
  myRegistrations: ['myRegistrations'] as const,
  studentInfo: ['studentInfo'] as const,
  termGpaHistory: ['termGpaHistory'] as const,
  curriculumStatus: ['curriculumStatus'] as const,
  instructorAvailabilities: ['instructorAvailabilities'] as const,
  academicOperationsContext: ['academicOperationsContext'] as const,
  academicPlanningSummary: (departmentId?: number, termId?: number) => ['academicPlanningSummary', departmentId, termId] as const,
  departmentClassLists: (departmentId?: number, termId?: number) => ['departmentClassLists', departmentId, termId] as const,
  departmentCurriculumOverview: (departmentId?: number) => ['departmentCurriculumOverview', departmentId] as const,
  departmentAcademicLevels: (departmentId?: number) => ['departmentAcademicLevels', departmentId] as const,
  gradeComponents: (sectionId: number) => ['gradeComponents', sectionId] as const,
  grades: (sectionId: number) => ['grades', sectionId] as const,
  gradingCriteria: (sectionId: number) => ['gradingCriteria', sectionId] as const,
};

export const STATUS_LABELS: Record<string, string> = {
  Draft: 'Taslak',
  PendingAdvisorApproval: 'Danışman Onayı Bekleniyor',
  Active: 'Aktif',
  Completed: 'Tamamlandı',
  Dropped: 'Bırakıldı',
  Withdrawn: 'Çekildi',
};

export const STATUS_VARIANT: Record<string, 'default' | 'secondary' | 'destructive' | 'outline' | 'success' | 'warning'> = {
  Draft: 'secondary',
  PendingAdvisorApproval: 'warning',
  Active: 'success',
  Completed: 'default',
  Dropped: 'destructive',
  Withdrawn: 'outline',
};

/** Routes that have actual page implementations in App.tsx */
export const IMPLEMENTED_ROUTES = new Set([
  '/',
  '/kullanim-kilavuzu',
  '/akademik-takvim',
  '/ders-kayit',
  '/plan',
  '/alinan-dersler',
  '/donem-ortalamalari',
  '/mufredat-durum',
  '/on-kayit-anketi',
  '/derslerim',
  '/not-girisi',
  '/program-planlama',
  '/planlama-merkezi',
  '/sinif-listeleri',
  '/mufredat-gorunumu',
  '/toplu-programlama',
  '/merkezi-derslik-yonetimi',
  '/sube-acma-onayi',
  '/donem-kapatma',
]);

export const STUDENT_STATUS_LABELS: Record<string, string> = {
  Active: 'Aktif',
  Passive: 'Pasif',
  Frozen: 'Dondurulmuş',
  Suspended: 'Uzaklaştırılmış',
  Dismissed: 'İlişik Kesilmiş',
  Graduated: 'Mezun',
};
