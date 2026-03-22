import { useState } from 'react';
import { NavLink } from 'react-router-dom';
import { useAuth } from '../../context/AuthContext';
import { ROUTES } from '../../lib/constants';
import {
  LayoutGrid,
  BookOpen,
  FileText,
  Languages,
  ClipboardCheck,
  User,
  ChevronRight,
  LogOut,
  GraduationCap,
  Users,
} from 'lucide-react';
import { ScrollArea } from '@/components/ui/scroll-area';
import { Separator } from '@/components/ui/separator';
import { cn } from '@/lib/utils';
import logo from '/logo.png';

interface MenuGroup {
  id: string;
  label: string;
  icon: React.ComponentType<{ className?: string }>;
  children: { to: string; label: string }[];
}

const studentMenuGroups: MenuGroup[] = [
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
      { to: '/donem-ortalamalari', label: 'Dönem Ortalamaları' },
      { to: '/not-listesi', label: 'Not Listesi' },
      { to: '/transkript', label: 'Transkript' },
      { to: '/transkript-senaryosu', label: 'Transkript Senaryosu' },
      { to: '/diger-belgeler', label: 'Diğer Belgeler' },
      { to: '/mufredat-durum', label: 'Müfredat Durum' },
      { to: '/mufredat-bilgi-paketi', label: 'Müfredat Bilgi Paketi' },
      { to: '/staj-basvurusu', label: 'Staj Başvurusu' },
      { to: '/devamsizlik-durumu', label: 'Devamsızlık Durumu' },
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

const instructorMenuGroups: MenuGroup[] = [
  {
    id: 'sik-kullanilanlar',
    label: 'Sık Kullanılanlar',
    icon: LayoutGrid,
    children: [
      { to: ROUTES.DASHBOARD, label: 'Ana Sayfa' },
      { to: ROUTES.INSTRUCTOR_SECTIONS, label: 'Derslerim' },
      { to: '/not-girisi', label: 'Not Girişi' },
    ],
  },
  {
    id: 'ogrenci-islemleri',
    label: 'Öğrenci İşlemleri',
    icon: Users,
    children: [
      { to: '/ogrenci-rehber', label: 'Öğrenci Rehber' },
      { to: '/ogrenci-no-degistirme', label: 'Öğrenci No Değiştirme' },
      { to: '/sinif-atlatma', label: 'Sınıf Atlatma İşlemleri' },
      { to: '/toplu-danisman-atama', label: 'Toplu Danışman Atama' },
      { to: '/toplu-staj-danisman-atama', label: 'Toplu Staj Danışman Atama' },
      { to: '/toplu-mufredat-atama', label: 'Toplu Müfredat Atama' },
      { to: '/toplu-ders-kaydi', label: 'Toplu Ders Kaydı' },
      { to: '/ders-kayit-duzenle', label: 'Ders Kayıt Düzenle' },
      { to: '/subeler-arasi-ogrenci-transfer', label: 'Şubeler Arası Öğrenci Transfer' },
      { to: '/dersler-arasi-ogrenci-transfer', label: 'Dersler Arası Öğrenci Transfer' },
      { to: '/harf-notu-duzenle', label: 'Harf Notu Düzenle' },
      { to: '/askerlik-islemleri', label: 'Askerlik İşlemleri' },
      { to: '/on-tanimli-danisman-atama', label: 'Ön Tanımlı Danışman Atama' },
      { to: '/belge-talepleri', label: 'Belge Talepleri' },
      { to: '/ders-kayit-nakil', label: 'Ders Kayıt Nakil İşlemleri' },
      { to: '/sartli-basarili', label: 'Şartlı Başarılı İşlemleri' },
      { to: '/ders-kaydi-mazeret', label: 'Ders Kaydı Mazeret İşlemleri' },
      { to: '/ogrenci-kulup-kayit', label: 'Öğrenci Kulüp Kayıt' },
      { to: '/ogrenci-hatirlatmalar', label: 'Öğrenci Hatırlatmalar' },
      { to: '/toplu-ogrenim-durumu-atama', label: 'Toplu Öğrenim Durumu Atama' },
      { to: '/ogrenci-basari-hesaplamalari', label: 'Öğrenci Başarı Hesaplamaları' },
      { to: '/kimlik-suresi-dolanlar', label: 'Kimlik Süresi Dolanlar' },
      { to: '/toplu-yoksis-not-baremi', label: 'Toplu YÖKSİS Not Baremi Atama' },
    ],
  },
  {
    id: 'ders-islemleri',
    label: 'Ders İşlemleri',
    icon: BookOpen,
    children: [
      { to: '/ders-acma-islemleri', label: 'Ders Açma İşlemleri' },
      { to: '/mufredat-islemleri', label: 'Müfredat İşlemleri' },
      { to: '/mufredat-sinif-donem-tanimlari', label: 'Müfredat Sınıf Dönem Tanımları' },
      { to: '/ders-ac-mufredat', label: 'Ders Aç (Müfredat)' },
      { to: '/ders-ac-hareket', label: 'Ders Aç (Hareket)' },
      { to: '/subelendirme-islemleri', label: 'Şubelendirme İşlemleri' },
      { to: '/ders-kontenjan-islemleri', label: 'Ders Kontenjan İşlemleri' },
      { to: '/ders-programi-tanimlari', label: 'Ders Programı Tanımları' },
      { to: '/ders-toplu-ogretim-elemani-atama', label: 'Ders Toplu Öğr. Elemanı Atama' },
      { to: '/ders-prog-toplu-aktarim', label: 'Ders Prog. Toplu Aktarım' },
      { to: '/mufredat-ders-intibak', label: 'Müfredat Ders İntibak İşlemleri' },
      { to: '/mufredat-ders-kod-duzenleme', label: 'Müfredat Ders Kod Düzenleme' },
      { to: '/derslik-tanimlari', label: 'Derslik Tanımları' },
      { to: '/ders-veren-ogretim-elemanlari', label: 'Ders Veren Öğretim Elemanları' },
      { to: '/sinav-mufredati', label: 'Sınav Müfredatı (Snv Yönergeleri)' },
      { to: '/sinav-tanimla', label: 'Sınav Tanımla' },
      { to: '/sinav-tarihleri-duzenle', label: 'Sınav Tarihleri Düzenle' },
      { to: '/sinav-not-giris-tarihleri', label: 'Sınav Not Giriş Tarihleri Düzenle' },
      { to: '/tek-ders-sinav-ogrenci-listesi', label: 'Tek Ders Sınav Öğrenci Listesi' },
      { to: '/ek-sinav-ogrenci-listesi', label: 'Ek Sınav Öğrenci Listesi' },
      { to: '/ek-sinav-donem-sonu', label: 'Ek Sınav Dönem Sonu İşlemleri' },
      { to: '/derse-gore-kayit-yapanlar', label: 'Derse Göre Kayıt Yapan Öğrenciler' },
      { to: '/esdeger-dersler', label: 'Eşdeğer Dersler' },
      { to: '/ders-kayit-kredi-duzenle', label: 'Ders Kayıt Kredi Düzenle' },
    ],
  },
  {
    id: 'bolum-islemleri',
    label: 'Bölüm İşlemleri',
    icon: GraduationCap,
    children: [
      { to: '/bolum-ogrenci-islemleri', label: 'Öğrenci İşlemleri' },
      { to: '/sinif-listeleri', label: 'Sınıf Listeleri' },
      { to: '/bolum-ders-islemleri', label: 'Bölüm Ders İşlemleri' },
      { to: '/bolum-ders-ac-mufredat', label: 'Ders Aç (Müfredat)' },
      { to: '/bolum-ders-ac-hareket', label: 'Ders Aç (Hareket)' },
      { to: '/bolum-subelendirme', label: 'Şubelendirme İşlemleri' },
      { to: '/derslik-tanimla', label: 'Derslik Tanımla' },
      { to: '/bolum-ders-veren-ogretim-elemanlari', label: 'Ders Veren Öğretim Elemanları' },
      { to: '/ogretim-elemani-ders-yukleri', label: 'Öğretim Elemanı Ders Yükleri' },
      { to: '/bolum-toplu-danisman-atama', label: 'Toplu Danışman Atama' },
      { to: '/bolum-toplu-mufredat-atama', label: 'Toplu Müfredat Atama' },
      { to: '/sinav-kontrol-islemleri', label: 'Sınav Kontrol İşlemleri' },
      { to: '/ogrenci-sifre-islemleri', label: 'Öğrenci Şifre İşlemleri' },
      { to: '/toplu-karne-islemleri', label: 'Toplu Karne İşlemleri' },
      { to: '/ders-kayit-istatistikleri', label: 'Ders Kayıt İstatistikleri' },
      { to: '/basari-istatistigi', label: 'Başarı İstatistiği' },
      { to: '/akademik-cv-islemleri', label: 'Akademik CV İşlemleri' },
      { to: '/ders-basari-istatistigi', label: 'Ders Başarı İstatistiği' },
      { to: '/ogretim-elemani-sinav-notlari', label: 'Öğretim Elemanı Sınav Notları' },
      { to: '/staj-not-girisi', label: 'Staj Not Girişi' },
      { to: '/sifre-degistir', label: 'Şifre Değiştir' },
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

function getMenuGroupsForRole(roles: string[]): MenuGroup[] {
  if (roles.includes('Instructor') || roles.includes('AcademicAdvisor')) {
    return instructorMenuGroups;
  }
  return studentMenuGroups;
}

function getRoleLabel(roles: string[]): string {
  if (roles.includes('Instructor')) return 'Öğretim Üyesi Paneli';
  if (roles.includes('AcademicAdvisor')) return 'Danışman Paneli';
  if (roles.includes('DepartmentManager')) return 'Bölüm Başkanı Paneli';
  if (roles.includes('SystemAdministrator')) return 'Yönetici Paneli';
  return 'Öğrenci Bilgi Sistemi';
}

export default function Sidebar() {
  const { user, logout } = useAuth();
  const [openGroup, setOpenGroup] = useState<string | null>(null);

  const roles = user?.roles ?? [];
  const menuGroups = getMenuGroupsForRole(roles);

  const toggleGroup = (id: string) => {
    setOpenGroup(prev => prev === id ? null : id);
  };

  const initials = user?.name
    ? user.name.split(' ').map(n => n[0]).join('').toUpperCase().slice(0, 2)
    : '??';

return (
    <aside className="fixed inset-y-0 left-0 z-40 hidden lg:flex w-[260px] flex-col bg-sidebar border-r border-border">
      {/* Header / Branding */}
      <div className="p-5 pb-2">
        <div className="flex items-center gap-3">
          <img src={logo} alt="Logo" className="h-10 w-auto object-contain" />
          <div className="flex flex-col items-start justify-center">
            <h1 className="font-heading text-[13px] font-bold text-[#003366] leading-none uppercase tracking-tight">
              Ankara Bilim
            </h1>
            <h1 className="font-heading text-[11px] font-bold text-[#003366]/90 leading-none uppercase tracking-tighter mt-1">
              Üniversitesi
            </h1>
          </div>
        </div>

        {/* Sistem Adı ve Dinamik Rol - Tek Blokta Hizalı */}
        <div className="mt-4 flex flex-col gap-0.5">
          <p className="text-[11px] font-bold text-[#003366] uppercase tracking-[0.1em]">
            Öğrenci Bilgi Sistemi
          </p>
        </div>
        
        <Separator className="mt-4" />
      </div>

      {/* Navigation - Kaydırılabilir Alan */}
      <ScrollArea className="flex-1 px-3">
        <nav className="space-y-0.5 py-2">
          {menuGroups.map(group => {
            const isOpen = openGroup === group.id;
            const Icon = group.icon;

            return (
              <div key={group.id} className="mb-1">
                <button
                  onClick={() => toggleGroup(group.id)}
                  className={cn(
                    'group flex w-full items-center gap-3 rounded-md px-3 py-2 text-sm font-medium transition-all duration-200',
                    isOpen
                      ? 'bg-primary/10 text-primary shadow-sm'
                      : 'text-muted-foreground hover:bg-muted hover:text-foreground'
                  )}
                >
                  <Icon className="h-[18px] w-[18px] shrink-0" />
                  <span className="flex-1 text-left truncate">{group.label}</span>
                  <ChevronRight
                    className={cn(
                      'h-4 w-4 shrink-0 opacity-50 transition-transform duration-200',
                      isOpen && 'rotate-90 opacity-100'
                    )}
                  />
                </button>

                {isOpen && (
                  <div className="mt-1 ml-[22px] border-l-2 border-primary/20 pl-3 space-y-1 pb-1">
                    {group.children.map(child => (
                      <NavLink
                        key={child.to}
                        to={child.to}
                        end={child.to === '/'}
                        className={({ isActive }) =>
                          cn(
                            'block rounded-md px-3 py-1.5 text-[13px] transition-all duration-150',
                            isActive
                              ? 'text-primary font-semibold bg-primary/5'
                              : 'text-muted-foreground hover:text-foreground hover:bg-muted'
                          )
                        }
                      >
                        {child.label}
                      </NavLink>
                    ))}
                  </div>
                )}
              </div>
            );
          })}
        </nav>
      </ScrollArea>

      {/* User Section - Alt Kısım */}
      <div className="mt-auto border-t p-4 bg-muted/30">
        <div className="flex items-center gap-3">
          <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-[#003366] text-white text-xs font-bold shadow-sm">
            {initials}
          </div>
          <div className="flex-1 min-w-0">
            <p className="text-sm font-semibold text-foreground truncate">{user?.name}</p>
            <p className="text-[11px] text-muted-foreground truncate">{user?.email}</p>
          </div>
          <button
            onClick={logout}
            className="flex h-8 w-8 items-center justify-center rounded-md text-muted-foreground hover:bg-destructive/10 hover:text-destructive transition-colors"
            title="Güvenli Çıkış"
          >
            <LogOut className="h-4 w-4" />
          </button>
        </div>
      </div>
    </aside>
  );
}
