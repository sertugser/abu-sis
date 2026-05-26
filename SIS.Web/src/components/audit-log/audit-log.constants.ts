// KAYNAK: backend AuditInterceptor.AuditedEntityTypes + manuel log üreten action'ların
// EntityType'ları (Authentication=login, System=hata/yetki, AuditLog=veri erişimi).
// Yeni bir denetlenen varlık eklerken buraya da bir etiket ekle — ENTITY_TYPE_OPTIONS bundan türetilir.
export const ENTITY_TYPE_LABELS: Record<string, string> = {
  // Akademik yapı
  Faculty: 'Fakülte',
  Department: 'Bölüm',
  AcademicProgram: 'Program',
  AcademicTerm: 'Akademik Dönem',
  Curriculum: 'Müfredat',
  Course: 'Ders',
  CourseSection: 'Ders Seksiyonu',
  ClassroomTermAssignment: 'Derslik Dönem Ataması',
  FacultyScheduleApproval: 'Fakülte Program Onayı',
  PromotionRule: 'Yükselme Kuralı',
  CourseAccessPolicy: 'Ders Erişim Politikası',
  // Öğrenci
  Student: 'Öğrenci',
  StudentProgram: 'Öğrenci Programı',
  StudentPersonalInfo: 'Öğrenci Kişisel Bilgisi',
  StudentDocument: 'Öğrenci Belgesi',
  IssuedDocument: 'Üretilen Belge',
  StudentScholarshipDetail: 'Öğrenci Bursu',
  StudentPlacement: 'Öğrenci Yerleştirme',
  CourseRegistration: 'Ders Kaydı',
  RegistrationPlan: 'Kayıt Planı',
  // Not / değerlendirme
  StudentGrade: 'Öğrenci Notu',
  GradeComponent: 'Not Bileşeni',
  GradingCriteria: 'Değerlendirme Kriteri',
  // Kullanıcı / sistem
  ApplicationUser: 'Kullanıcı',
  SystemConfig: 'Sistem Ayarı',
  WebModuleSetting: 'Web Modül Ayarı',
  Authentication: 'Kimlik Doğrulama',
  System: 'Sistem',
  AuditLog: 'Denetim Kaydı',
};

export const ACTION_CONFIG: Record<string, { label: string; color: string; bg: string; icon: string }> = {
  Created:            { label: 'Oluşturuldu',      color: 'text-emerald-700', bg: 'bg-emerald-50 border-emerald-200', icon: '＋' },
  Modified:           { label: 'Güncellendi',       color: 'text-blue-700',    bg: 'bg-blue-50 border-blue-200',      icon: '✎' },
  Deleted:            { label: 'Silindi',           color: 'text-red-700',     bg: 'bg-red-50 border-red-200',        icon: '✕' },
  LoginFailed:        { label: 'Başarısız Giriş',   color: 'text-amber-700',   bg: 'bg-amber-50 border-amber-200',    icon: '⚠' },
  LoginSuccess:       { label: 'Başarılı Giriş',    color: 'text-emerald-700', bg: 'bg-emerald-50 border-emerald-200', icon: '✓' },
  ScheduleAssigned:   { label: 'Program Atandı',     color: 'text-cyan-700',  bg: 'bg-cyan-50 border-cyan-200',   icon: '→' },
  ScheduleUnassigned: { label: 'Program Kaldırıldı', color: 'text-slate-700', bg: 'bg-slate-50 border-slate-200', icon: '←' },
  DataAccess:         { label: 'Veri Erişimi',      color: 'text-violet-700',  bg: 'bg-violet-50 border-violet-200',  icon: '◉' },
  SystemError:        { label: 'Sistem Hatası',     color: 'text-red-700',     bg: 'bg-red-50 border-red-200',        icon: '✕' },
  Unauthorized:       { label: 'Yetkisiz Erişim',   color: 'text-orange-700',  bg: 'bg-orange-50 border-orange-200',  icon: '⛔' },
  Forbidden:          { label: 'Yasaklı Erişim',    color: 'text-orange-700',  bg: 'bg-orange-50 border-orange-200',  icon: '⛔' },
};

// ENTITY_TYPE_LABELS'tan türetilir → iki harita asla drift etmez.
export const ENTITY_TYPE_OPTIONS = [
  { value: 'all', label: 'Tüm Varlıklar' },
  ...Object.entries(ENTITY_TYPE_LABELS).map(([value, label]) => ({ value, label })),
];

export const DEFAULT_PAGE_SIZE = 50;
export const PAGE_SIZE_OPTIONS = [25, 50, 100] as const;
