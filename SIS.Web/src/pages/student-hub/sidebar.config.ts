export interface CardNavItem {
  segment: string; // route segment relative to /ogrenci-rehberi/:studentId; '' = index (overview)
  label: string;
  enabled: boolean; // false = visible but disabled ("yakında")
}
export interface CardNavGroup { title: string; items: CardNavItem[]; }

export const CARD_NAV: CardNavGroup[] = [
  { title: 'Genel Bilgiler', items: [
    { segment: '', label: 'Kart Bilgileri', enabled: true },
    { segment: 'akademik', label: 'Akademik Bilgiler', enabled: false },
    { segment: 'iletisim', label: 'İletişim Bilgileri', enabled: false },
    { segment: 'genel', label: 'Genel Bilgiler', enabled: false },
  ]},
  { title: 'Ders İşlemleri', items: [
    { segment: 'transkript', label: 'Transkript', enabled: false },
    { segment: 'notlar', label: 'Notlar', enabled: false },
    { segment: 'ders-kaydi', label: 'Ders Kaydı', enabled: false },
    { segment: 'mufredat-durumu', label: 'Müfredat Durumu', enabled: false },
    { segment: 'devamsizlik', label: 'Devamsızlık', enabled: false },
  ]},
  { title: 'İşlemler', items: [
    { segment: 'danisman', label: 'Danışman Atama', enabled: false },
    { segment: 'no-degistir', label: 'Öğrenci No Değiştirme', enabled: false },
    { segment: 'sinif-atlatma', label: 'Sınıf Atlatma', enabled: false },
    { segment: 'sifre', label: 'Şifre İşlemleri', enabled: false },
  ]},
];
