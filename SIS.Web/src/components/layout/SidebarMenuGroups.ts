import {
  LayoutGrid,
  BookOpen,
  User,
  GraduationCap,
  Users,
  Building2,
  Shield,
  Settings,
  Calendar,
  School,
  Megaphone,
  ClipboardCheck,
} from 'lucide-react';
import { ROUTES } from '../../lib/constants';

export interface MenuGroup {
  id: string;
  label: string;
  icon: React.ComponentType<{ className?: string }>;
  children: { to: string; label: string; openInNewTab?: boolean }[];
}

// ─── STUDENT MENU ───────────────────────────────────────────────
export const studentMenuGroups: MenuGroup[] = [
  {
    id: 'genel',
    label: 'Genel İşlemler',
    icon: LayoutGrid,
    children: [
      { to: ROUTES.DASHBOARD, label: 'Ana Sayfa' },
      { to: '/akademik-takvim', label: 'Akademik Takvim' },
      { to: ROUTES.ANNOUNCEMENTS, label: 'Duyurular' },
      { to: ROUTES.ALINAN_DERSLER, label: 'Alınan Dersler' },
    ],
  },
  {
    id: 'ders-donem',
    label: 'Ders ve Dönem İşlemleri',
    icon: BookOpen,
    children: [
      { to: ROUTES.DERS_KAYIT, label: 'Ders Kayıt' },
      { to: ROUTES.PLAN, label: 'Kayıt Planım' },
      { to: ROUTES.NOT_LISTESI, label: 'Not Listesi' },
      { to: ROUTES.DEVAMSIZLIK_DURUMU, label: 'Devamsızlık Durumu' },
      { to: '/donem-ortalamalari', label: 'Dönem Ortalamaları' },
      { to: '/mufredat-durum', label: 'Müfredat Durum' },
      { to: ROUTES.TRANSCRIPT, label: 'Transkript', openInNewTab: true },
      { to: ROUTES.TRANSCRIPT_SCENARIO, label: 'Transkript Senaryosu' },
    ],
  },
  {
    id: 'degerlendirme',
    label: 'Değerlendirme',
    icon: ClipboardCheck,
    children: [
      { to: ROUTES.COURSE_EVALUATION, label: 'Ders Değerlendirme' },
    ],
  },
  {
    id: 'kullanici',
    label: 'Kullanıcı İşlemleri',
    icon: User,
    children: [
      { to: '/sifre-degistir', label: 'Şifre Değiştir' },
    ],
  },
];

// ─── INSTRUCTOR MENU ────────────────────────────────────────────
export const instructorMenuGroups: MenuGroup[] = [
  {
    id: 'sik-kullanilanlar',
    label: 'Sık Kullanılanlar',
    icon: LayoutGrid,
    children: [
      { to: ROUTES.DASHBOARD, label: 'Ana Sayfa' },
      { to: ROUTES.INSTRUCTOR_SECTIONS, label: 'Derslerim' },
      { to: ROUTES.INSTRUCTOR_SCHEDULING, label: 'Ders Programım' },
      { to: ROUTES.ANNOUNCEMENTS, label: 'Duyurular' },
    ],
  },
  {
    id: 'ders-islemleri',
    label: 'Ders İşlemleri',
    icon: BookOpen,
    children: [
      { to: ROUTES.INSTRUCTOR_SECTIONS, label: 'Derslerim' },
      { to: ROUTES.INSTRUCTOR_SCHEDULING, label: 'Ders Programım' },
      { to: '/not-girisi', label: 'Not Girişi' },
    ],
  },
  {
    id: 'degerlendirme',
    label: 'Değerlendirme',
    icon: ClipboardCheck,
    children: [
      { to: ROUTES.EVALUATION_RESULTS, label: 'Değerlendirme Sonuçları' },
    ],
  },
];

// ─── ACADEMIC ADVISOR MENU ──────────────────────────────────────
export const advisorMenuGroups: MenuGroup[] = [
  {
    id: 'sik-kullanilanlar',
    label: 'Sık Kullanılanlar',
    icon: LayoutGrid,
    children: [
      { to: ROUTES.DASHBOARD, label: 'Ana Sayfa' },
      { to: ROUTES.INSTRUCTOR_SECTIONS, label: 'Derslerim' },
    ],
  },
  {
    id: 'danismanlik',
    label: 'Danışmanlık İşlemleri',
    icon: Users,
    children: [
      { to: ROUTES.DASHBOARD, label: 'Onay Bekleyenler' },
    ],
  },
  {
    id: 'ders-islemleri',
    label: 'Ders İşlemleri',
    icon: BookOpen,
    children: [
      { to: ROUTES.INSTRUCTOR_SECTIONS, label: 'Derslerim' },
      { to: ROUTES.INSTRUCTOR_SCHEDULING, label: 'Ders Programım' },
      { to: '/not-girisi', label: 'Not Girişi' },
    ],
  },
];

// ─── DEPARTMENT MANAGER MENU ────────────────────────────────────
export const departmentManagerMenuGroups: MenuGroup[] = [
  {
    id: 'sik-kullanilanlar',
    label: 'Sık Kullanılanlar',
    icon: LayoutGrid,
    children: [
      { to: ROUTES.DASHBOARD, label: 'Ana Sayfa' },
      { to: ROUTES.COURSE_OPENING, label: 'Ders Açma İşlemleri' },
    ],
  },
  {
    id: 'ders-islemleri',
    label: 'Ders İşlemleri',
    icon: BookOpen,
    children: [
      // İş akışı sırasına göre: katalog → müfredat → seçmeli grup → aç → onayla → hoca ata → kopyala → programla → kontrol → rapor → kapat
      { to: ROUTES.COURSE_CATALOG_ADMIN, label: 'Ders Kataloğu' },
      { to: ROUTES.CURRICULUM_MANAGEMENT, label: 'Müfredat İşlemleri' },
      { to: ROUTES.ELECTIVE_GROUPS, label: 'Seçmeli Ders Grupları' },
      { to: ROUTES.COURSE_OPENING, label: 'Ders Açma İşlemleri' },
      { to: ROUTES.INSTRUCTOR_ASSIGNMENT, label: 'Öğretim Elemanı Atama' },
      { to: ROUTES.TERM_PROGRAM_TRANSFER, label: 'Dönem Programı Aktarım' },
      { to: ROUTES.CONSTRAINT_MANAGEMENT, label: 'Program Kısıtlamaları' },
      { to: ROUTES.CLASS_LISTS, label: 'Sınıf Listeleri' },
      { to: ROUTES.INSTRUCTOR_COURSES_REPORT, label: 'Ders Veren Öğretim Üyeleri' },
      { to: ROUTES.TERM_CLOSING, label: 'Dönem Kapatma İşlemleri' },
    ],
  },
  {
    id: 'ogrenci-islemleri',
    label: 'Öğrenci İşlemleri',
    icon: Users,
    children: [
      { to: ROUTES.STUDENT_DIRECTORY, label: 'Öğrenci Rehberi' },
    ],
  },
  {
    id: 'derslerim',
    label: 'Derslerim',
    icon: GraduationCap,
    children: [
      { to: ROUTES.INSTRUCTOR_SECTIONS, label: 'Derslerim' },
      { to: ROUTES.INSTRUCTOR_SCHEDULING, label: 'Ders Programım' },
      { to: '/not-girisi', label: 'Not Girişi' },
      { to: ROUTES.EVALUATION_RESULTS, label: 'Değerlendirme Sonuçları' },
    ],
  },
  {
    id: 'duyuru-yonetimi',
    label: 'Duyuru Yönetimi',
    icon: Megaphone,
    children: [
      { to: ROUTES.ANNOUNCEMENT_MANAGEMENT, label: 'Duyuru Yönetimi' },
      { to: ROUTES.ANNOUNCEMENTS, label: 'Tüm Duyurular' },
    ],
  },
];

// ─── ADMINISTRATIVE STAFF MENU ──────────────────────────────────
export const administrativeStaffMenuGroups: MenuGroup[] = [
  {
    id: 'sik-kullanilanlar',
    label: 'Sık Kullanılanlar',
    icon: LayoutGrid,
    children: [
      { to: ROUTES.DASHBOARD, label: 'Ana Sayfa' },
    ],
  },
  {
    id: 'ders-islemleri',
    label: 'Ders İşlemleri',
    icon: BookOpen,
    children: [
      { to: ROUTES.COURSE_CATALOG_ADMIN, label: 'Ders Kataloğu' },
      { to: ROUTES.CURRICULUM_MANAGEMENT, label: 'Müfredat İşlemleri' },
      { to: ROUTES.ELECTIVE_GROUPS, label: 'Seçmeli Ders Grupları' },
      { to: ROUTES.COURSE_OPENING, label: 'Ders Açma İşlemleri' },
      { to: ROUTES.INSTRUCTOR_ASSIGNMENT, label: 'Öğretim Elemanı Atama' },
      { to: ROUTES.TERM_PROGRAM_TRANSFER, label: 'Dönem Programı Aktarım' },
      { to: ROUTES.SCHEDULE_CREATION, label: 'Ders Programı Oluşturma' },
      { to: ROUTES.CLASSROOM_DASHBOARD, label: 'Derslik Doluluk Tablosu' },
      { to: ROUTES.CLASS_LISTS, label: 'Sınıf Listeleri' },
      { to: ROUTES.INSTRUCTOR_COURSES_REPORT, label: 'Ders Veren Öğretim Üyeleri' },
    ],
  },
  {
    id: 'ogrenci-islemleri',
    label: 'Öğrenci İşlemleri',
    icon: Users,
    children: [
      { to: ROUTES.STUDENT_DIRECTORY, label: 'Öğrenci Rehberi' },
    ],
  },
  {
    id: 'duyuru-yonetimi',
    label: 'Duyuru Yönetimi',
    icon: Megaphone,
    children: [
      { to: ROUTES.ANNOUNCEMENT_MANAGEMENT, label: 'Duyuru Yönetimi' },
      { to: ROUTES.ANNOUNCEMENTS, label: 'Tüm Duyurular' },
    ],
  },
];

// ─── DEAN MENU ──────────────────────────────────────────────────
export const deanMenuGroups: MenuGroup[] = [
  {
    id: 'sik-kullanilanlar',
    label: 'Sık Kullanılanlar',
    icon: LayoutGrid,
    children: [
      { to: ROUTES.DASHBOARD, label: 'Ana Sayfa' },
      { to: ROUTES.SCHEDULE_CREATION, label: 'Ders Programı Oluşturma' },
      { to: ROUTES.SCHEDULE_APPROVAL, label: 'Program Onayı' },
    ],
  },
  {
    id: 'ders-islemleri',
    label: 'Ders İşlemleri',
    icon: BookOpen,
    children: [
      { to: ROUTES.COURSE_CATALOG_ADMIN, label: 'Ders Kataloğu' },
      { to: ROUTES.CURRICULUM_MANAGEMENT, label: 'Müfredat İşlemleri' },
      { to: ROUTES.ELECTIVE_GROUPS, label: 'Seçmeli Ders Grupları' },
      { to: ROUTES.COURSE_OPENING, label: 'Ders Açma İşlemleri' },
      { to: ROUTES.INSTRUCTOR_ASSIGNMENT, label: 'Öğretim Elemanı Atama' },
      { to: ROUTES.TERM_PROGRAM_TRANSFER, label: 'Dönem Programı Aktarım' },
      { to: ROUTES.SCHEDULE_CREATION, label: 'Ders Programı Oluşturma' },
      { to: ROUTES.CLASSROOM_DASHBOARD, label: 'Derslik Doluluk Tablosu' },
      { to: ROUTES.CONSTRAINT_MANAGEMENT, label: 'Program Kısıtlamaları' },
      { to: ROUTES.SCHEDULE_APPROVAL, label: 'Program Onayı' },
      { to: ROUTES.CLASS_LISTS, label: 'Sınıf Listeleri' },
      { to: ROUTES.INSTRUCTOR_COURSES_REPORT, label: 'Ders Veren Öğretim Üyeleri' },
      { to: ROUTES.TERM_CLOSING, label: 'Dönem Kapatma İşlemleri' },
    ],
  },
  {
    id: 'ogrenci-islemleri',
    label: 'Öğrenci İşlemleri',
    icon: Users,
    children: [
      { to: ROUTES.STUDENT_DIRECTORY, label: 'Öğrenci Rehberi' },
    ],
  },
  {
    id: 'derslerim',
    label: 'Derslerim',
    icon: GraduationCap,
    children: [
      { to: ROUTES.INSTRUCTOR_SECTIONS, label: 'Derslerim' },
      { to: ROUTES.INSTRUCTOR_SCHEDULING, label: 'Ders Programım' },
      { to: '/not-girisi', label: 'Not Girişi' },
      { to: ROUTES.EVALUATION_RESULTS, label: 'Değerlendirme Sonuçları' },
    ],
  },
  {
    id: 'duyuru-yonetimi',
    label: 'Duyuru Yönetimi',
    icon: Megaphone,
    children: [
      { to: ROUTES.ANNOUNCEMENT_MANAGEMENT, label: 'Duyuru Yönetimi' },
      { to: ROUTES.ANNOUNCEMENTS, label: 'Tüm Duyurular' },
    ],
  },
];

// ─── SYSTEM ADMINISTRATOR MENU ──────────────────────────────────
export const systemAdminMenuGroups: MenuGroup[] = [
  {
    id: 'sik-kullanilanlar',
    label: 'Sık Kullanılanlar',
    icon: LayoutGrid,
    children: [
      { to: ROUTES.DASHBOARD, label: 'Ana Sayfa' },
      { to: ROUTES.TERM_MANAGEMENT, label: 'Dönem İşlemleri' },
      { to: '/akademik-takvim', label: 'Akademik Takvim' },
    ],
  },
  {
    id: 'ders-islemleri',
    label: 'Ders İşlemleri',
    icon: BookOpen,
    children: [
      { to: ROUTES.COURSE_CATALOG_ADMIN, label: 'Ders Kataloğu' },
      { to: ROUTES.CURRICULUM_MANAGEMENT, label: 'Müfredat İşlemleri' },
      { to: ROUTES.ELECTIVE_GROUPS, label: 'Seçmeli Ders Grupları' },
      { to: ROUTES.COURSE_OPENING, label: 'Ders Açma İşlemleri' },
      { to: ROUTES.INSTRUCTOR_ASSIGNMENT, label: 'Öğretim Elemanı Atama' },
      { to: ROUTES.TERM_PROGRAM_TRANSFER, label: 'Dönem Programı Aktarım' },
      { to: ROUTES.SCHEDULE_CREATION, label: 'Ders Programı Oluşturma' },
      { to: ROUTES.CLASSROOM_DASHBOARD, label: 'Derslik Doluluk Tablosu' },
      { to: ROUTES.CONSTRAINT_MANAGEMENT, label: 'Program Kısıtlamaları' },
      { to: ROUTES.SCHEDULE_APPROVAL, label: 'Program Onayı' },
      { to: ROUTES.CLASS_LISTS, label: 'Sınıf Listeleri' },
      { to: ROUTES.INSTRUCTOR_COURSES_REPORT, label: 'Ders Veren Öğretim Üyeleri' },
      { to: ROUTES.TERM_CLOSING, label: 'Dönem Kapatma İşlemleri' },
    ],
  },
  {
    id: 'donem-yonetimi',
    label: 'Dönem Yönetimi',
    icon: Calendar,
    children: [
      { to: ROUTES.TERM_MANAGEMENT, label: 'Dönem İşlemleri' },
      { to: '/akademik-takvim', label: 'Akademik Takvim' },
    ],
  },
  {
    id: 'kaynak-yonetimi',
    label: 'Kaynak Yönetimi',
    icon: School,
    children: [
      { to: ROUTES.CLASSROOM_ADMIN, label: 'Derslik Yönetimi' },
      { to: ROUTES.CLASSROOM_ASSIGNMENT, label: 'Derslik-Fakülte Ataması' },
    ],
  },
  {
    id: 'kullanici-yonetimi',
    label: 'Kullanıcı Yönetimi',
    icon: Settings,
    children: [
      { to: ROUTES.USER_MANAGEMENT, label: 'Kullanıcı Listesi' },
      { to: ROUTES.STUDENT_DIRECTORY, label: 'Öğrenci Rehberi' },
      { to: ROUTES.INSTRUCTOR_DIRECTORY, label: 'Personel Listesi' },
    ],
  },
  {
    id: 'akademik-yapi',
    label: 'Akademik Yapı',
    icon: Building2,
    children: [
      { to: ROUTES.ACADEMIC_STRUCTURE, label: 'Fakülte / Bölüm / Program' },
      { to: ROUTES.PROMOTION_RULES, label: 'Sınıf Atlatma Kuralları' },
    ],
  },
  {
    id: 'sistem',
    label: 'Sistem',
    icon: Shield,
    children: [
      { to: ROUTES.SYSTEM_CONFIG, label: 'Sistem Ayarları' },
      { to: ROUTES.AUDIT_LOG, label: 'Denetim Kayıtları' },
    ],
  },
  {
    id: 'degerlendirme',
    label: 'Değerlendirme',
    icon: ClipboardCheck,
    children: [
      { to: ROUTES.EVALUATION_PERIOD_ADMIN, label: 'Değerlendirme Dönemi Yönetimi' },
      { to: ROUTES.EVALUATION_RESULTS, label: 'Değerlendirme Sonuçları' },
    ],
  },
  {
    id: 'duyuru-yonetimi',
    label: 'Duyuru Yönetimi',
    icon: Megaphone,
    children: [
      { to: ROUTES.ANNOUNCEMENT_MANAGEMENT, label: 'Duyuru Yönetimi' },
      { to: ROUTES.ANNOUNCEMENTS, label: 'Tüm Duyurular' },
    ],
  },
];
