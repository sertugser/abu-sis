export const ENTITY_TYPE_LABELS: Record<string, string> = {
  AcademicTerm: 'Akademik Dönem',
  CourseSection: 'Ders Seksiyonu',
  CourseRegistration: 'Ders Kaydı',
  Student: 'Öğrenci',
  ApplicationUser: 'Kullanıcı',
  SystemConfig: 'Sistem Ayarı',
  WebModuleSetting: 'Web Modül Ayarı',
  FacultyScheduleApproval: 'Fakülte Program Onayı',
  PromotionRule: 'Yükselme Kuralı',
  RegistrationPlan: 'Kayıt Planı',
  CourseAccessPolicy: 'Ders Erişim Politikası',
  ClassroomTermAssignment: 'Derslik Dönem Ataması',
  Course: 'Ders',
  Curriculum: 'Müfredat',
  StudentGrade: 'Öğrenci Notu',
  GradeComponent: 'Not Bileşeni',
  GradingCriteria: 'Değerlendirme Kriteri',
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

export const ENTITY_TYPE_OPTIONS = [
  { value: 'all', label: 'Tüm Varlıklar' },
  { value: 'StudentGrade', label: 'Öğrenci Notu' },
  { value: 'CourseRegistration', label: 'Ders Kaydı' },
  { value: 'CourseSection', label: 'Ders Seksiyonu' },
  { value: 'Student', label: 'Öğrenci' },
  { value: 'ApplicationUser', label: 'Kullanıcı' },
  { value: 'Course', label: 'Ders' },
  { value: 'Curriculum', label: 'Müfredat' },
  { value: 'AcademicTerm', label: 'Akademik Dönem' },
  { value: 'SystemConfig', label: 'Sistem Ayarı' },
  { value: 'WebModuleSetting', label: 'Web Modül Ayarı' },
  { value: 'ClassroomTermAssignment', label: 'Derslik Dönem Ataması' },
  { value: 'Authentication', label: 'Kimlik Doğrulama' },
  { value: 'System', label: 'Sistem' },
];

export const DEFAULT_PAGE_SIZE = 50;
export const PAGE_SIZE_OPTIONS = [25, 50, 100] as const;
