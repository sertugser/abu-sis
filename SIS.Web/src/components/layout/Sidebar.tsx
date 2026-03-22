import { useState } from 'react';
import { NavLink, useLocation } from 'react-router-dom';
import { useAuth } from '../../context/AuthContext';
import { ROUTES } from '../../lib/constants';
import {
  ADVISOR_ROLES,
  getPrimaryRole,
  hasAnyRole,
  INSTRUCTOR_ROLES,
  MANAGEMENT_ROLES,
  STUDENT_ROLES,
} from '../../lib/roles';
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
      { to: ROUTES.KULLANIM_KILAVUZU, label: 'Kullanım Kılavuzu' },
      { to: '/ozluk-bilgileri', label: 'Özlük Bilgileri' },
      { to: '/genel-bilgiler', label: 'Genel Bilgiler' },
      { to: ROUTES.AKADEMIK_TAKVIM, label: 'Akademik Takvim' },
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
      { to: ROUTES.DONEM_ORTALAMALARI, label: 'Dönem Ortalamaları' },
      { to: '/not-listesi', label: 'Not Listesi' },
      { to: '/transkript', label: 'Transkript' },
      { to: '/transkript-senaryosu', label: 'Transkript Senaryosu' },
      { to: '/diger-belgeler', label: 'Diğer Belgeler' },
      { to: ROUTES.MUFREDAT_DURUM, label: 'Müfredat Durum' },
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

function buildOperationsMenuGroups(options: {
  hasInstructor: boolean;
  hasManagement: boolean;
}): MenuGroup[] {
  const { hasInstructor, hasManagement } = options;

  const quickLinks = [
    { to: ROUTES.DASHBOARD, label: 'Ana Sayfa' },
    ...(hasInstructor ? [{ to: ROUTES.INSTRUCTOR_SECTIONS, label: 'Derslerim' }] : []),
    ...(hasInstructor ? [{ to: ROUTES.INSTRUCTOR_SCHEDULING, label: 'Program Planlama' }] : []),
    ...(hasInstructor ? [{ to: ROUTES.GRADE_ENTRY, label: 'Not Girişi' }] : []),
    ...(hasManagement ? [{ to: ROUTES.MANAGEMENT_PLANNING, label: 'Planlama Merkezi' }] : []),
    ...(hasManagement ? [{ to: ROUTES.MANAGEMENT_CLASS_LISTS, label: 'Sınıf Listeleri' }] : []),
    ...(hasManagement ? [{ to: ROUTES.MANAGEMENT_CURRICULUM, label: 'Müfredat Görünümü' }] : []),
  ];

  return [
    {
      id: 'sik-kullanilanlar',
      label: 'Sık Kullanılanlar',
      icon: LayoutGrid,
      children: quickLinks,
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
        { to: hasManagement ? ROUTES.MANAGEMENT_CLASS_LISTS : '/sinif-listeleri-yonlendirme', label: 'Sınıf Listeleri' },
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
}

function getMenuGroupsForRole(roles: string[]): MenuGroup[] {
  const menuGroups: MenuGroup[] = [];
  const hasStudent = hasAnyRole(roles, STUDENT_ROLES);
  const hasInstructor = hasAnyRole(roles, INSTRUCTOR_ROLES);
  const hasManagement = hasAnyRole(roles, MANAGEMENT_ROLES);
  const hasAdvisor = hasAnyRole(roles, ADVISOR_ROLES);

  if (hasStudent) {
    menuGroups.push(...studentMenuGroups);
  }

  if (hasInstructor || hasManagement || hasAdvisor) {
    menuGroups.push(...buildOperationsMenuGroups({ hasInstructor, hasManagement }));
  }

  return menuGroups.length > 0 ? menuGroups : studentMenuGroups;
}

function getRoleLabel(roles: string[]): string {
  const primaryRole = getPrimaryRole(roles);

  if (primaryRole === 'Instructor') return 'Öğretim Üyesi Paneli';
  if (primaryRole === 'AcademicAdvisor') return 'Danışman Paneli';
  if (primaryRole === 'DepartmentManager') return 'Bölüm Başkanı Paneli';
  if (primaryRole === 'AdministrativeStaff') return 'Akademik İşler Paneli';
  if (primaryRole === 'SystemAdministrator') return 'Yönetici Paneli';
  return 'Öğrenci Bilgi Sistemi';
}

interface SidebarProps {
  isCollapsed: boolean;
}

export default function Sidebar({ isCollapsed }: SidebarProps) {
  const { user, logout } = useAuth();
  const [openGroup, setOpenGroup] = useState<string | null>(null);
  const location = useLocation();

  const roles = user?.roles ?? [];
  const menuGroups = getMenuGroupsForRole(roles);

  const toggleGroup = (id: string) => {
    setOpenGroup(prev => (prev === id ? null : id));
  };

  const initials = user?.name
    ? user.name.split(' ').map(name => name[0]).join('').toUpperCase().slice(0, 2)
    : '??';

  const isRouteInGroup = (group: MenuGroup) =>
    group.children.some(child =>
      child.to === '/'
        ? location.pathname === '/'
        : location.pathname === child.to || location.pathname.startsWith(`${child.to}/`)
    );

  const hasAnyActiveGroup = menuGroups.some(isRouteInGroup);

  return (
    <aside
      className={cn(
        'fixed inset-y-0 left-0 z-40 hidden flex-col border-r border-white/10 bg-[linear-gradient(180deg,#13233f_0%,#182b4b_58%,#132139_100%)] text-slate-100 shadow-[18px_0_50px_rgba(15,23,42,0.18)] transition-[width] duration-300 lg:flex',
        isCollapsed ? 'w-[88px]' : 'w-[280px]',
      )}
    >
      <div className="p-5 pb-4">
        <div
          className={cn(
            'rounded-2xl border border-white/10 bg-white/5 py-4 shadow-[inset_0_1px_0_rgba(255,255,255,0.06)]',
            isCollapsed ? 'px-0' : 'px-4',
          )}
        >
          <div className={cn('flex items-center gap-3', isCollapsed && 'justify-center')}>
            <div className="flex h-11 w-11 shrink-0 items-center justify-center rounded-2xl bg-white text-primary shadow-sm">
              <GraduationCap className="h-5 w-5" />
            </div>
            {!isCollapsed && (
              <div className="min-w-0">
                <h1 className="font-heading text-sm font-bold tracking-tight text-white">
                  Ankara Bilim Üniversitesi
                </h1>
                <p className="mt-0.5 text-xs text-slate-300">{getRoleLabel(roles)}</p>
              </div>
            )}
          </div>
        </div>
        <Separator className="mt-4 bg-white/10" />
      </div>

      <ScrollArea className={cn('flex-1', isCollapsed ? 'px-2' : 'px-3')}>
        <nav className="space-y-0.5 py-1">
          {menuGroups.map((group, index) => {
            const isGroupActive = isRouteInGroup(group);
            const isOpen = openGroup
              ? openGroup === group.id
              : hasAnyActiveGroup
                ? isGroupActive
                : index === 0;
            const Icon = group.icon;

            return (
              <div key={group.id}>
                <button
                  onClick={() => toggleGroup(group.id)}
                  title={group.label}
                  className={cn(
                    'group flex w-full items-center rounded-xl text-sm font-medium transition-colors duration-150',
                    isCollapsed ? 'justify-center px-2 py-3.5' : 'gap-3 px-3.5 py-3',
                    isOpen || isGroupActive
                      ? 'bg-white/10 text-white shadow-[inset_0_1px_0_rgba(255,255,255,0.05)]'
                      : 'text-slate-300 hover:bg-white/5 hover:text-white',
                  )}
                >
                  <Icon className="h-[18px] w-[18px] shrink-0" />
                  {!isCollapsed && (
                    <>
                      <span className="flex-1 text-left truncate">{group.label}</span>
                      <ChevronRight
                        className={cn(
                          'h-4 w-4 shrink-0 text-slate-400 transition-transform duration-200',
                          isOpen && 'rotate-90',
                        )}
                      />
                    </>
                  )}
                </button>

                {!isCollapsed && isOpen && (
                  <div className="mt-1 ml-[21px] border-l border-white/10 pl-3 space-y-1 pb-1">
                    {group.children.map(child => (
                      <NavLink
                        key={`${group.id}-${child.label}-${child.to}`}
                        to={child.to}
                        end={child.to === '/'}
                        className={({ isActive }) =>
                          cn(
                            'block rounded-lg px-3 py-2 text-[13px] transition-colors duration-150',
                            isActive
                              ? 'bg-white/12 font-semibold text-white'
                              : 'text-slate-300 hover:bg-white/5 hover:text-white',
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

      <div className="p-4">
        <Separator className="mb-4 bg-white/10" />
        <div
          className={cn(
            'rounded-2xl border border-white/10 bg-white/5 p-3 shadow-[inset_0_1px_0_rgba(255,255,255,0.05)]',
            isCollapsed ? 'flex flex-col items-center gap-3' : 'flex items-center gap-3',
          )}
        >
          <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-white text-xs font-bold text-primary shadow-sm">
            {initials}
          </div>
          {!isCollapsed && (
            <div className="min-w-0 flex-1">
              <p className="truncate text-sm font-medium text-white">{user?.name}</p>
              <p className="truncate text-xs text-slate-300">{user?.email}</p>
            </div>
          )}
          <button
            onClick={logout}
            className="flex h-9 w-9 items-center justify-center rounded-xl text-slate-300 transition-colors duration-150 hover:bg-white/10 hover:text-white"
            title="Çıkış Yap"
          >
            <LogOut className="h-4 w-4" />
          </button>
        </div>
      </div>
    </aside>
  );
}
