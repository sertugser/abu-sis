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
} from 'lucide-react';
import { ROUTES } from '../../lib/constants';

export interface MenuGroup {
  id: string;
  label: string;
  icon: React.ComponentType<{ className?: string }>;
  children: { to: string; label: string }[];
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
      { to: '/donem-ortalamalari', label: 'Dönem Ortalamaları' },
      { to: '/mufredat-durum', label: 'Müfredat Durum' },
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
      { to: ROUTES.COURSE_APPROVAL, label: 'Ders Onay İşlemleri' },
      { to: ROUTES.COURSE_OPENING, label: 'Ders Açma İşlemleri' },
    ],
  },
  {
    id: 'bolum-yonetimi',
    label: 'Bölüm Yönetimi',
    icon: GraduationCap,
    children: [
      { to: ROUTES.COURSE_OPENING, label: 'Ders Açma İşlemleri' },
      { to: ROUTES.COURSE_APPROVAL, label: 'Ders Onay İşlemleri' },
      { to: ROUTES.CLASS_LISTS, label: 'Sınıf Listeleri' },
      { to: ROUTES.TERM_CLOSING, label: 'Dönem Kapatma İşlemleri' },
      { to: ROUTES.CONSTRAINT_MANAGEMENT, label: 'Program Kısıtlamaları' },
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
    id: 'ders-islemleri',
    label: 'Derslerim',
    icon: BookOpen,
    children: [
      { to: ROUTES.INSTRUCTOR_SECTIONS, label: 'Derslerim' },
      { to: ROUTES.INSTRUCTOR_SCHEDULING, label: 'Ders Programım' },
      { to: '/not-girisi', label: 'Not Girişi' },
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
      { to: ROUTES.COURSE_APPROVAL, label: 'Ders Onay İşlemleri' },
    ],
  },
  {
    id: 'ogrenci-islemleri',
    label: 'Öğrenci İşlemleri',
    icon: Users,
    children: [
      { to: ROUTES.STUDENT_DIRECTORY, label: 'Öğrenci Rehberi' },
      { to: ROUTES.CLASS_LISTS, label: 'Sınıf Listeleri' },
    ],
  },
  {
    id: 'ders-islemleri',
    label: 'Ders İşlemleri',
    icon: BookOpen,
    children: [
      { to: ROUTES.COURSE_OPENING, label: 'Ders Açma İşlemleri' },
      { to: ROUTES.COURSE_APPROVAL, label: 'Ders Onay İşlemleri' },
      { to: ROUTES.CLASSROOM_DASHBOARD, label: 'Derslik Doluluk Tablosu' },
      { to: ROUTES.SCHEDULE_CREATION, label: 'Ders Programı Oluşturma' },
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
    id: 'fakulte-yonetimi',
    label: 'Fakülte Yönetimi',
    icon: Building2,
    children: [
      { to: ROUTES.COURSE_OPENING, label: 'Ders Açma İşlemleri' },
      { to: ROUTES.COURSE_APPROVAL, label: 'Ders Onay İşlemleri' },
      { to: ROUTES.SCHEDULE_CREATION, label: 'Ders Programı Oluşturma' },
      { to: ROUTES.SCHEDULE_APPROVAL, label: 'Program Onayı' },
      { to: ROUTES.CLASS_LISTS, label: 'Sınıf Listeleri' },
      { to: ROUTES.CLASSROOM_DASHBOARD, label: 'Derslik Doluluk Tablosu' },
      { to: ROUTES.TERM_CLOSING, label: 'Dönem Kapatma İşlemleri' },
      { to: ROUTES.CONSTRAINT_MANAGEMENT, label: 'Program Kısıtlamaları' },
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
    id: 'ders-islemleri',
    label: 'Derslerim',
    icon: BookOpen,
    children: [
      { to: ROUTES.INSTRUCTOR_SECTIONS, label: 'Derslerim' },
      { to: ROUTES.INSTRUCTOR_SCHEDULING, label: 'Ders Programım' },
      { to: '/not-girisi', label: 'Not Girişi' },
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
      { to: '/akademik-takvim', label: 'Akademik Takvim' },
      { to: ROUTES.TERM_MANAGEMENT, label: 'Dönem İşlemleri' },
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
      { to: ROUTES.CONSTRAINT_MANAGEMENT, label: 'Program Kısıtlamaları' },
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
      { to: ROUTES.COURSE_CATALOG_ADMIN, label: 'Ders Kataloğu' },
      { to: ROUTES.PROMOTION_RULES, label: 'Sınıf Atlatma Kuralları' },
    ],
  },
  {
    id: 'ders-islemleri',
    label: 'Ders İşlemleri',
    icon: BookOpen,
    children: [
      { to: ROUTES.COURSE_OPENING, label: 'Ders Açma İşlemleri' },
      { to: ROUTES.COURSE_APPROVAL, label: 'Ders Onay İşlemleri' },
      { to: ROUTES.SCHEDULE_CREATION, label: 'Ders Programı Oluşturma' },
      { to: ROUTES.CLASSROOM_DASHBOARD, label: 'Derslik Doluluk Tablosu' },
      { to: ROUTES.CLASS_LISTS, label: 'Sınıf Listeleri' },
      { to: ROUTES.TERM_CLOSING, label: 'Dönem Kapatma İşlemleri' },
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
];
