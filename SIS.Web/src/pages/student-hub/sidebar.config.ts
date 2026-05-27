export interface CardNavItem {
  segment: string; // route segment relative to /ogrenci-rehberi/:studentId; '' = index (overview)
  label: string;
  enabled: boolean; // false = visible but disabled ("yakında")
}
export interface CardNavGroup { title: string; items: CardNavItem[]; }

export const CARD_NAV: CardNavGroup[] = [
  { title: 'Genel Bilgiler', items: [
    { segment: '', label: 'Kart Bilgileri', enabled: true },
    { segment: 'akademik', label: 'Akademik Bilgiler', enabled: true },
    { segment: 'iletisim', label: 'İletişim Bilgileri', enabled: true },
    { segment: 'genel', label: 'Genel Bilgiler', enabled: true },
    { segment: 'askerlik', label: 'Askerlik Bilgileri', enabled: true },
    { segment: 'belgeler', label: 'Belgeler', enabled: true },
    { segment: 'verilen-belgeler', label: 'Verilen Belgeler', enabled: true },
  ]},
  { title: 'Kayıt Bilgileri', items: [
    { segment: 'burs', label: 'Burs Bilgileri', enabled: true },
    { segment: 'yerlestirme', label: 'Yerleştirme Bilgileri', enabled: true },
    { segment: 'entegrasyon', label: 'Entegrasyon (NVİ/YÖKSİS)', enabled: true },
  ]},
  { title: 'Ders İşlemleri', items: [
    { segment: 'transkript', label: 'Transkript', enabled: true },
    { segment: 'notlar', label: 'Notlar', enabled: true },
    { segment: 'donem-ortalamalari', label: 'Dönem Ortalamaları', enabled: true },
    { segment: 'ders-kaydi', label: 'Ders Kaydı', enabled: true },
    { segment: 'mufredat-durumu', label: 'Müfredat Durumu', enabled: true },
    { segment: 'devamsizlik', label: 'Devamsızlık', enabled: true },
  ]},
  { title: 'İşlemler', items: [
    { segment: 'danisman', label: 'Danışman Atama', enabled: true },
    { segment: 'no-degistir', label: 'Öğrenci No Değiştirme', enabled: true },
    { segment: 'sinif-atlatma', label: 'Sınıf Atlatma', enabled: true },
    { segment: 'sifre', label: 'Şifre İşlemleri', enabled: true },
  ]},
];
