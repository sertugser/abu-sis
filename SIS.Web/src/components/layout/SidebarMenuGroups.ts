import {
  LayoutGrid,
  BookOpen,
  FileText,
  Languages,
  ClipboardCheck,
  User,
  GraduationCap,
  Users,
  Building2,
  Shield,
  Settings,
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
      { to: '/kullanim-kilavuzu', label: 'Kullanım Kılavuzu' },
      { to: '/ozluk-bilgileri', label: 'Özlük Bilgileri' },
      { to: '/genel-bilgiler', label: 'Genel Bilgiler' },
      { to: '/akademik-takvim', label: 'Akademik Takvim' },
      { to: '/danisman-bilgileri', label: 'Danışman Bilgileri' },
      { to: ROUTES.ALINAN_DERSLER, label: 'Alınan Dersler' },
      { to: '/acilan-bolum-dersleri', label: 'Açılan Bölüm Dersleri' },
      { to: '/sinav-takvimi', label: 'Sınav Takvimi' },
      { to: '/ders-programi', label: 'Ders Programı' },
      { to: '/bolum-ders-programi', label: 'Bölüm Ders Programı' },
      { to: '/harc-bilgileri', label: 'Harç Bilgileri' },
      { to: '/staj-bilgileri', label: 'Staj Bilgileri' },
      { to: '/genel-duyurular', label: 'Genel Duyurular' },
      { to: '/mezuniyet-onay-bilgileri', label: 'Mezuniyet Onay Bilgileri' },
    ],
  },
  {
    id: 'ders-donem',
    label: 'Ders ve Dönem İşlemleri',
    icon: BookOpen,
    children: [
      { to: ROUTES.DERS_KAYIT, label: 'Ders Kayıt' },
      { to: '/ders-ekle-birak', label: 'Ders Ekle/Bırak' },
      { to: '/devamsizlik-durumu', label: 'Devamsızlık Durumu' },
      { to: '/diger-belgeler', label: 'Diğer Belgeler' },
      { to: '/donem-ortalamalari', label: 'Dönem Ortalamaları' },
      { to: '/mufredat-bilgi-paketi', label: 'Müfredat Bilgi Paketi' },
      { to: '/mufredat-durum', label: 'Müfredat Durum' },
      { to: '/not-listesi', label: 'Not Listesi' },
      { to: '/staj-basvurusu', label: 'Staj Başvurusu' },
      { to: '/transkript', label: 'Transkript' },
      { to: '/transkript-senaryosu', label: 'Transkript Senaryosu' },
    ],
  },
  {
    id: 'form',
    label: 'Form İşlemleri',
    icon: FileText,
    children: [
      { to: '/anketler', label: 'Anketler' },
      { to: '/degerlendirme-formlari', label: 'Değerlendirme Formları' },
      { to: '/hazirlik-deger-formlari', label: 'Hazırlık Değer. Formları' },
      { to: '/ogrenci-bilgi-formu', label: 'Öğrenci Bilgi Formu' },
    ],
  },
  {
    id: 'hazirlik',
    label: 'Hazırlık İşlemleri',
    icon: Languages,
    children: [
      { to: '/haz-sinav-takvimi', label: 'Haz. Sınav Takvimi' },
      { to: '/haz-not-listesi', label: 'Haz. Not Listesi' },
      { to: '/haz-devamsizlik-durumu', label: 'Haz. Devamsızlık Durumu' },
      { to: '/hazirlik-ders-programi', label: 'Hazırlık Ders Programı' },
    ],
  },
  {
    id: 'basvuru',
    label: 'Başvuru İşlemleri',
    icon: ClipboardCheck,
    children: [
      { to: '/kayit-dondurma-basvuru', label: 'Kayıt Dondurma Başvuru' },
      { to: '/mazeret-sinavi-basvuru', label: 'Mazeret Sınavı Başvuru' },
      { to: '/ek-sinav-basvuru-v2', label: 'Ek Sınav Başvuru V2' },
      { to: '/ek-sinav-basvuru-islemleri', label: 'Ek Sınav Başvuru İşlemleri' },
      { to: '/tek-ders-basvuru-islemleri', label: 'Tek Ders Başvuru İşlemleri' },
    ],
  },
  {
    id: 'kullanici',
    label: 'Kullanıcı İşlemleri',
    icon: User,
    children: [
      { to: '/yapilacaklar-listesi', label: 'Yapılacaklar Listesi' },
      { to: '/gelen-mesajlar', label: 'Gelen Mesajlar' },
      { to: '/gonderilen-mesajlar', label: 'Gönderilen Mesajlar' },
      { to: '/belge-talebi', label: 'Belge Talebi' },
      { to: '/sifre-degistir', label: 'Şifre Değiştir' },
      { to: '/fotograf-guncelle', label: 'Fotoğraf Güncelle' },
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
      { to: '/not-girisi', label: 'Not Girişi' },
      { to: '/derse-gore-kayit-yapanlar', label: 'Derse Göre Kayıt Yapan Öğrenciler' },
      { to: '/sinav-tanimla', label: 'Sınav Tanımla' },
      { to: '/sinav-tarihleri-duzenle', label: 'Sınav Tarihleri Düzenle' },
    ],
  },
  {
    id: 'bilgi-paketi',
    label: 'Bilgi Paketi İşlemleri',
    icon: FileText,
    children: [
      { to: '/bilgi-paketi-ders-bilgileri', label: 'Ders Bilgileri' },
      { to: '/bilgi-paketi-program-bilgileri', label: 'Program Bilgileri' },
      { to: '/bilgi-paketi-ogretim-elemanlari', label: 'Öğretim Elemanları' },
    ],
  },
];

// ─── ACADEMIC ADVISOR MENU (Instructor + advising) ──────────────
export const advisorMenuGroups: MenuGroup[] = [
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
    id: 'danismanlik',
    label: 'Danışmanlık İşlemleri',
    icon: Users,
    children: [
      { to: '/ogrenci-rehber', label: 'Öğrenci Rehber' },
      { to: '/toplu-danisman-atama', label: 'Toplu Danışman Atama' },
      { to: '/ogrenci-basari-hesaplamalari', label: 'Öğrenci Başarı Hesaplamaları' },
    ],
  },
  {
    id: 'ders-islemleri',
    label: 'Ders İşlemleri',
    icon: BookOpen,
    children: [
      { to: '/not-girisi', label: 'Not Girişi' },
      { to: '/derse-gore-kayit-yapanlar', label: 'Derse Göre Kayıt Yapan Öğrenciler' },
      { to: '/sinav-tanimla', label: 'Sınav Tanımla' },
      { to: '/sinav-tarihleri-duzenle', label: 'Sınav Tarihleri Düzenle' },
    ],
  },
  {
    id: 'bilgi-paketi',
    label: 'Bilgi Paketi İşlemleri',
    icon: FileText,
    children: [
      { to: '/bilgi-paketi-ders-bilgileri', label: 'Ders Bilgileri' },
      { to: '/bilgi-paketi-program-bilgileri', label: 'Program Bilgileri' },
      { to: '/bilgi-paketi-ogretim-elemanlari', label: 'Öğretim Elemanları' },
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
      { to: ROUTES.INSTRUCTOR_SECTIONS, label: 'Derslerim' },
    ],
  },
  {
    id: 'bolum-yonetimi',
    label: 'Bölüm Yönetimi',
    icon: GraduationCap,
    children: [
      { to: '/ders-acma-islemleri', label: 'Ders Açma İşlemleri' },
      { to: '/donem-kapatma', label: 'Dönem Kapatma İşlemleri' },
      { to: ROUTES.CLASS_LISTS, label: 'Sınıf Listeleri' },
      { to: '/ders-kayit-istatistikleri', label: 'Ders Kayıt İstatistikleri' },
      { to: '/basari-istatistigi', label: 'Başarı İstatistiği' },
      { to: '/sinav-kontrol-islemleri', label: 'Sınav Kontrol İşlemleri' },
    ],
  },
  {
    id: 'ogrenci-islemleri',
    label: 'Öğrenci İşlemleri',
    icon: Users,
    children: [
      { to: '/ogrenci-rehber', label: 'Öğrenci Rehber' },
      { to: '/toplu-danisman-atama', label: 'Toplu Danışman Atama' },
      { to: '/toplu-mufredat-atama', label: 'Toplu Müfredat Atama' },
      { to: '/sinif-atlatma', label: 'Sınıf Atlatma İşlemleri' },
      { to: '/ogrenci-basari-hesaplamalari', label: 'Öğrenci Başarı Hesaplamaları' },
      { to: '/ogrenci-sifre-islemleri', label: 'Öğrenci Şifre İşlemleri' },
    ],
  },
  {
    id: 'ders-islemleri',
    label: 'Ders İşlemleri',
    icon: BookOpen,
    children: [
      { to: '/not-girisi', label: 'Not Girişi' },
      { to: ROUTES.INSTRUCTOR_SCHEDULING, label: 'Ders Programım' },
      { to: ROUTES.CLASSROOM_DASHBOARD, label: 'Derslik Doluluk Tablosu' },
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
      { to: '/ogrenci-rehber', label: 'Öğrenci Rehber' },
      { to: ROUTES.CLASS_LISTS, label: 'Sınıf Listeleri' },
      { to: '/ogrenci-no-degistirme', label: 'Öğrenci No Değiştirme' },
      { to: '/toplu-ders-kaydi', label: 'Toplu Ders Kaydı' },
      { to: '/ders-kayit-duzenle', label: 'Ders Kayıt Düzenle' },
      { to: '/subeler-arasi-ogrenci-transfer', label: 'Şubeler Arası Öğrenci Transfer' },
      { to: '/harf-notu-duzenle', label: 'Harf Notu Düzenle' },
      { to: '/belge-talepleri', label: 'Belge Talepleri' },
    ],
  },
  {
    id: 'ders-islemleri',
    label: 'Ders İşlemleri',
    icon: BookOpen,
    children: [
      { to: '/ders-acma-islemleri', label: 'Ders Açma İşlemleri' },
      { to: ROUTES.CLASSROOM_DASHBOARD, label: 'Derslik Doluluk Tablosu' },
      { to: ROUTES.SCHEDULE_CREATION, label: 'Ders Programı Oluşturma' },
    ],
  },
];

// ─── DEAN MENU (Faculty-level scope) ────────────────────────────
export const deanMenuGroups: MenuGroup[] = [
  {
    id: 'sik-kullanilanlar',
    label: 'Sık Kullanılanlar',
    icon: LayoutGrid,
    children: [
      { to: ROUTES.DASHBOARD, label: 'Ana Sayfa' },
      { to: ROUTES.SCHEDULE_CREATION, label: 'Ders Programı Oluşturma' },
      { to: ROUTES.COURSE_APPROVAL, label: 'Ders Onay İşlemleri' },
    ],
  },
  {
    id: 'fakulte-yonetimi',
    label: 'Fakülte Yönetimi',
    icon: Building2,
    children: [
      { to: '/ders-acma-islemleri', label: 'Ders Açma İşlemleri' },
      { to: '/donem-kapatma', label: 'Dönem Kapatma İşlemleri' },
      { to: ROUTES.CLASS_LISTS, label: 'Sınıf Listeleri' },
      { to: ROUTES.CLASSROOM_DASHBOARD, label: 'Derslik Doluluk Tablosu' },
      { to: ROUTES.SCHEDULE_APPROVAL, label: 'Program Onayı' },
      { to: '/ders-kayit-istatistikleri', label: 'Ders Kayıt İstatistikleri' },
      { to: '/basari-istatistigi', label: 'Başarı İstatistiği' },
    ],
  },
  {
    id: 'ogrenci-islemleri',
    label: 'Öğrenci İşlemleri',
    icon: Users,
    children: [
      { to: '/ogrenci-rehber', label: 'Öğrenci Rehber' },
      { to: '/sinif-atlatma', label: 'Sınıf Atlatma İşlemleri' },
      { to: '/ogrenci-basari-hesaplamalari', label: 'Öğrenci Başarı Hesaplamaları' },
    ],
  },
  {
    id: 'ders-islemleri',
    label: 'Ders İşlemleri',
    icon: BookOpen,
    children: [
      { to: '/not-girisi', label: 'Not Girişi' },
      { to: ROUTES.INSTRUCTOR_SECTIONS, label: 'Derslerim' },
      { to: ROUTES.INSTRUCTOR_SCHEDULING, label: 'Ders Programım' },
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
      { to: ROUTES.COURSE_APPROVAL, label: 'Ders Onay İşlemleri' },
      { to: ROUTES.SCHEDULE_CREATION, label: 'Ders Programı Oluşturma' },
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
    id: 'sistem-yonetimi',
    label: 'Sistem Yönetimi',
    icon: Shield,
    children: [
      { to: ROUTES.TERM_MANAGEMENT, label: 'Dönem Yönetimi' },
      { to: ROUTES.COURSE_OPENING, label: 'Ders Açma İşlemleri' },
      { to: ROUTES.TERM_CLOSING, label: 'Dönem Kapatma İşlemleri' },
      { to: ROUTES.CLASSROOM_DASHBOARD, label: 'Derslik Doluluk Tablosu' },
      { to: ROUTES.CLASSROOM_ASSIGNMENT, label: 'Derslik-Fakülte Ataması' },
      { to: ROUTES.CLASS_LISTS, label: 'Sınıf Listeleri' },
      { to: ROUTES.SYSTEM_CONFIG, label: 'Sistem Ayarları' },
      { to: ROUTES.AUDIT_LOG, label: 'Denetim Kayıtları' },
      { to: ROUTES.CLASSROOM_ADMIN, label: 'Derslik Yönetimi' },
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
];
