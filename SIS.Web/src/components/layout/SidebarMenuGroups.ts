import {
  LayoutGrid,
  BookOpen,
  User,
  GraduationCap,
  Users,
  Shield,
  Settings,
  Calendar,
  School,
  Megaphone,
  ClipboardCheck,
} from 'lucide-react';
import { ROUTES } from '../../lib/constants';

export interface MenuItem {
  to: string;
  label: string;
  openInNewTab?: boolean;
}

/**
 * Alt-grup (2. seviye nested). Tek bir grup içinde alt başlıklara ayırma için kullanılır.
 * Sadece `subGroups` opsiyonu kullanan gruplarda anlamlıdır (örn. Yönetim grubu).
 * 3+ seviye nested gerekirse type genişletilmeli — şu an yok.
 */
export interface MenuSubGroup {
  id: string;
  label: string;
  items: MenuItem[];
}

/**
 * Sidebar grubu. İki şekilde içerik tanımlanır:
 * - `children`: düz, tek seviye linkler (mevcut çoğu grup)
 * - `subGroups`: 2. seviye gruplama (Yönetim → Kullanıcı İşlemleri / Genel Ayarlar)
 *
 * Bir grup ya `children` ya da `subGroups` kullanır — ikisi birden tanımlanmamalı.
 */
export interface MenuGroup {
  id: string;
  label: string;
  icon: React.ComponentType<{ className?: string }>;
  children?: MenuItem[];
  subGroups?: MenuSubGroup[];
}

/** Bir grubun tüm leaf item'larını döndürür (children veya subGroups → items düzleştirilmiş). */
export function flattenGroupItems(group: MenuGroup): MenuItem[] {
  if (group.subGroups) {
    return group.subGroups.flatMap(sg => sg.items);
  }
  return group.children ?? [];
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
      { to: ROUTES.SMART_PLANNING, label: 'Akıllı Ders Önerileri' },
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
    id: 'anketler',
    label: 'Anketler',
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
      { to: ROUTES.ADVISOR_COURSE_CONSTRAINTS, label: 'Ders Kısıtları' },
    ],
  },
  {
    id: 'anketler',
    label: 'Anketler',
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
    ],
  },
  {
    id: 'danismanlik',
    label: 'Danışmanlık İşlemleri',
    icon: Users,
    children: [
      { to: ROUTES.DASHBOARD, label: 'Onay Bekleyenler' },
      { to: ROUTES.ADVISOR_COURSE_CONSTRAINTS, label: 'Ders Kısıtları' },
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
    ],
  },
  {
    id: 'ders-islemleri',
    label: 'Ders İşlemleri',
    icon: BookOpen,
    // Workflow sıralı: Katalog → Açma → Programlama → Dönem İçi + Sonu
    children: [
      // Katalog
      { to: ROUTES.COURSE_CATALOG_ADMIN, label: 'Ders Kataloğu' },
      { to: ROUTES.CURRICULUM_MANAGEMENT, label: 'Müfredat İşlemleri' },
      { to: ROUTES.ELECTIVE_GROUPS, label: 'Seçmeli Ders Grupları' },
      // Açma
      { to: ROUTES.COURSE_OPENING, label: 'Ders Açma İşlemleri' },
      // Programlama
      { to: ROUTES.CONSTRAINT_MANAGEMENT, label: 'Program Kısıtlamaları' },
      { to: ROUTES.INSTRUCTOR_ASSIGNMENT, label: 'Öğretim Elemanı Atama' },
      { to: ROUTES.TERM_PROGRAM_TRANSFER, label: 'Dönem Programı Aktarım' },
      // Dönem içi + kapanış
      { to: ROUTES.INSTRUCTOR_COURSES_REPORT, label: 'Ders Veren Öğretim Üyeleri' },
      { to: ROUTES.CLASS_LISTS, label: 'Sınıf Listeleri' },
      { to: ROUTES.TERM_CLOSING, label: 'Dönem Kapatma İşlemleri' },
    ],
  },
  {
    id: 'anketler',
    label: 'Anketler',
    icon: ClipboardCheck,
    children: [
      { to: ROUTES.EVALUATION_RESULTS, label: 'Değerlendirme Sonuçları' },
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
    // Workflow sıralı: Katalog → Açma → Programlama → Dönem İçi
    children: [
      // Katalog
      { to: ROUTES.COURSE_CATALOG_ADMIN, label: 'Ders Kataloğu' },
      { to: ROUTES.CURRICULUM_MANAGEMENT, label: 'Müfredat İşlemleri' },
      { to: ROUTES.ELECTIVE_GROUPS, label: 'Seçmeli Ders Grupları' },
      // Açma
      { to: ROUTES.COURSE_OPENING, label: 'Ders Açma İşlemleri' },
      // Programlama
      { to: ROUTES.SCHEDULE_CREATION, label: 'Ders Programı Oluşturma' },
      { to: ROUTES.INSTRUCTOR_ASSIGNMENT, label: 'Öğretim Elemanı Atama' },
      { to: ROUTES.TERM_PROGRAM_TRANSFER, label: 'Dönem Programı Aktarım' },
      { to: ROUTES.CLASSROOM_DASHBOARD, label: 'Derslik Doluluk Tablosu' },
      // Dönem içi
      { to: ROUTES.INSTRUCTOR_COURSES_REPORT, label: 'Ders Veren Öğretim Üyeleri' },
      { to: ROUTES.CLASS_LISTS, label: 'Sınıf Listeleri' },
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
    ],
  },
  {
    id: 'akademik-islemler',
    label: 'Akademik İşlemler',
    icon: Calendar,
    children: [
      { to: ROUTES.PROGRAM_QUOTA, label: 'Program Kontenjan Bilgileri' },
    ],
  },
  {
    id: 'ders-islemleri',
    label: 'Ders İşlemleri',
    icon: BookOpen,
    // Workflow sıralı: Katalog → Açma → Programlama → Dönem İçi + Sonu
    children: [
      // Katalog
      { to: ROUTES.COURSE_CATALOG_ADMIN, label: 'Ders Kataloğu' },
      { to: ROUTES.CURRICULUM_MANAGEMENT, label: 'Müfredat İşlemleri' },
      { to: ROUTES.ELECTIVE_GROUPS, label: 'Seçmeli Ders Grupları' },
      // Açma
      { to: ROUTES.COURSE_OPENING, label: 'Ders Açma İşlemleri' },
      // Programlama
      { to: ROUTES.CONSTRAINT_MANAGEMENT, label: 'Program Kısıtlamaları' },
      { to: ROUTES.SCHEDULE_CREATION, label: 'Ders Programı Oluşturma' },
      { to: ROUTES.INSTRUCTOR_ASSIGNMENT, label: 'Öğretim Elemanı Atama' },
      { to: ROUTES.TERM_PROGRAM_TRANSFER, label: 'Dönem Programı Aktarım' },
      { to: ROUTES.CLASSROOM_DASHBOARD, label: 'Derslik Doluluk Tablosu' },
      { to: ROUTES.INSTRUCTOR_COURSES_REPORT, label: 'Ders Veren Öğretim Üyeleri' },
      { to: ROUTES.SCHEDULE_APPROVAL, label: 'Program Onayı' },
      // Dönem içi + kapanış
      { to: ROUTES.CLASS_LISTS, label: 'Sınıf Listeleri' },
      { to: ROUTES.TERM_CLOSING, label: 'Dönem Kapatma İşlemleri' },
    ],
  },
  {
    id: 'anketler',
    label: 'Anketler',
    icon: ClipboardCheck,
    children: [
      { to: ROUTES.EVALUATION_RESULTS, label: 'Değerlendirme Sonuçları' },
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
// Gruplar workflow sıralı: Sık Kullanılanlar → Dönem → Ders İşlemleri → Anketler →
//                         Kaynaklar → Kullanıcılar → Akademik Yapı → Duyuru → Sistem
export const systemAdminMenuGroups: MenuGroup[] = [
  {
    id: 'sik-kullanilanlar',
    label: 'Sık Kullanılanlar',
    icon: LayoutGrid,
    children: [
      { to: ROUTES.DASHBOARD, label: 'Ana Sayfa' },
    ],
  },
  {
    id: 'akademik-islemler',
    label: 'Akademik İşlemler',
    icon: Calendar,
    children: [
      { to: ROUTES.TERM_MANAGEMENT, label: 'Akademik Dönem Tanımları' },
      { to: ROUTES.FACULTY_DEFINITIONS, label: 'Fakülte Tanımları' },
      { to: ROUTES.DEPARTMENT_DEFINITIONS, label: 'Bölüm Tanımları' },
      { to: ROUTES.PROGRAM_DEFINITIONS, label: 'Program Tanımları' },
      { to: ROUTES.ACADEMIC_CALENDAR_ADMIN, label: 'Akademik Takvim Yönetimi' },
      { to: ROUTES.AKADEMIK_TAKVIM, label: 'Akademik Takvim (Görünüm)' },
      { to: ROUTES.ACADEMIC_CALENDAR_GENERATOR, label: 'Takvim Oluşturucu' },
      { to: ROUTES.OSYM_REGISTRATION_CALENDAR, label: 'ÖSYM Kayıt Takvimi' },
      { to: ROUTES.PROGRAM_QUOTA, label: 'Program Kontenjan Bilgileri' },
    ],
  },
  {
    id: 'ogrenci-islemleri',
    label: 'Öğrenci İşlemleri',
    icon: Users,
    children: [
      { to: ROUTES.STUDENT_DIRECTORY, label: 'Öğrenci Rehberi' },
      { to: ROUTES.STUDENT_NUMBER_CHANGE, label: 'Öğrenci No Değiştirme' },
      { to: ROUTES.STUDENT_PROMOTION, label: 'Sınıf Atlatma İşlemleri' },
      { to: ROUTES.BULK_ADVISOR_ASSIGNMENT, label: 'Toplu Danışman Atama' },
      { to: ROUTES.BULK_INTERNSHIP_ADVISOR_ASSIGNMENT, label: 'Toplu Staj Danışman Atama' },
      { to: ROUTES.BULK_CURRICULUM_ASSIGNMENT, label: 'Toplu Müfredat Atama' },
      { to: ROUTES.BULK_COURSE_REGISTRATION, label: 'Toplu Ders Kaydı' },
      { to: ROUTES.COURSE_REGISTRATION_EDIT, label: 'Ders Kayıt Düzenleme' },
      { to: ROUTES.SECTION_TRANSFER, label: 'Şubeler Arası Öğrenci Transfer' },
      { to: ROUTES.COURSE_TRANSFER, label: 'Dersler Arası Öğrenci Transfer' },
      { to: ROUTES.LETTER_GRADE_EDIT, label: 'Harf Notu Düzenleme' },
    ],
  },
  {
    id: 'ders-islemleri',
    label: 'Ders İşlemleri',
    icon: BookOpen,
    // İçerik iş akışı sırasına göre: Katalog → Açma → Programlama → Dönem İçi + Sonu
    children: [
      // Katalog (altyapı, nadiren değişir)
      { to: ROUTES.COURSE_CATALOG_ADMIN, label: 'Ders Kataloğu' },
      { to: ROUTES.CURRICULUM_MANAGEMENT, label: 'Müfredat İşlemleri' },
      { to: ROUTES.PROMOTION_RULES, label: 'Müfredat Sınıf Dönem Tanımları' },
      { to: ROUTES.ELECTIVE_GROUPS, label: 'Seçmeli Ders Grupları' },
      // Ders açma (dönem başı)
      { to: ROUTES.COURSE_OPENING, label: 'Ders Açma İşlemleri' },
      { to: ROUTES.SECTIONING, label: 'Şubelendirme İşlemleri' },
      { to: ROUTES.COURSE_QUOTA, label: 'Ders Kontenjan İşlemleri' },
      // Programlama altyapısı + kısıt + oluşturma + onay
      { to: ROUTES.TIME_SLOT_MANAGEMENT, label: 'Ders Saatleri Yönetimi' },
      { to: ROUTES.CONSTRAINT_MANAGEMENT, label: 'Program Kısıtlamaları' },
      { to: ROUTES.SCHEDULE_CREATION, label: 'Ders Programı Oluşturma' },
      { to: ROUTES.INSTRUCTOR_ASSIGNMENT, label: 'Öğretim Elemanı Atama' },
      { to: ROUTES.TERM_PROGRAM_TRANSFER, label: 'Dönem Programı Aktarım' },
      { to: ROUTES.CLASSROOM_DASHBOARD, label: 'Derslik Doluluk Tablosu' },
      { to: ROUTES.INSTRUCTOR_COURSES_REPORT, label: 'Ders Veren Öğretim Üyeleri' },
      { to: ROUTES.SCHEDULE_APPROVAL, label: 'Program Onayı' },
      // Dönem içi + kapanış
      { to: ROUTES.CLASS_LISTS, label: 'Sınıf Listeleri' },
      { to: ROUTES.TERM_CLOSING, label: 'Dönem Kapatma İşlemleri' },
    ],
  },
  {
    id: 'anketler',
    label: 'Anketler',
    icon: ClipboardCheck,
    children: [
      { to: ROUTES.EVALUATION_PERIOD_ADMIN, label: 'Değerlendirme Dönemi Yönetimi' },
      { to: ROUTES.EVALUATION_RESULTS, label: 'Değerlendirme Sonuçları' },
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
    id: 'duyuru-yonetimi',
    label: 'Duyuru Yönetimi',
    icon: Megaphone,
    children: [
      { to: ROUTES.ANNOUNCEMENT_MANAGEMENT, label: 'Duyuru Yönetimi' },
      { to: ROUTES.ANNOUNCEMENTS, label: 'Tüm Duyurular' },
    ],
  },
  // ── Yönetim — Proliz tarzı 2-seviye nested grup (subGroups kullanır) ──
  // Eski "Kullanıcı Yönetimi" + "Sistem" grupları bu grup altında alt-gruplara taşındı.
  {
    id: 'yonetim',
    label: 'Yönetim',
    icon: Shield,
    subGroups: [
      {
        id: 'kullanici-islemleri',
        label: 'Kullanıcı İşlemleri',
        items: [
          { to: ROUTES.USER_MANAGEMENT, label: 'Kullanıcı Tanımlama' },
          { to: ROUTES.INSTRUCTOR_DIRECTORY, label: 'Personel Listesi' },
          { to: ROUTES.USER_PERMISSION_GROUPS, label: 'Kullanıcı Yetki Grupları' },
          { to: ROUTES.USER_REPORT_GROUPS, label: 'Kullanıcı Rapor Grupları' },
          { to: ROUTES.USER_GROUP_RESTRICTIONS, label: 'Kullanıcı Grupları Sınırlandırmalar' },
          { to: ROUTES.USER_REPORT_DESIGN_GROUPS, label: 'Kullanıcı Rapor Tasarım Grupları' },
          { to: ROUTES.USER_DELEGATION, label: 'Kullanıcı Vekalet İşlemleri' },
          { to: ROUTES.AUDIT_LOG, label: 'LOG' },
          { to: ROUTES.PASSWORD_CHANGE, label: 'Şifre Değiştir' },
        ],
      },
      {
        id: 'genel-ayarlar',
        label: 'Genel Ayarlar',
        items: [
          { to: ROUTES.UNIVERSITY_INFO, label: 'Üniversite Bilgileri' },
          { to: ROUTES.INSTITUTIONAL_INTEGRATION, label: 'Kurumsal Entegrasyon Tanımları' },
          { to: ROUTES.DOCUMENT_RESTRICTIONS, label: 'Belge Sınırlandırmalar' },
          { to: ROUTES.WEB_MODULE_SETTINGS, label: 'Web Modül Ayarları' },
          { to: ROUTES.SYSTEM_CONFIG, label: 'Sistem Konfigürasyonu' },
          { to: ROUTES.EBYS_SIGNATURE_AUTHORITIES, label: 'EBYS İmza Yetkilileri' },
          { to: ROUTES.FACULTY_SIGNATURE_UPLOAD, label: 'Fakülte İmza Yükleme İşlemleri' },
        ],
      },
    ],
  },
];
