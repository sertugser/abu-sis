// TimeSlot grid (yeni dönem için saat dilimi şablonu) ayar anahtarları.
// Bu key'ler /ders-saatleri-yonetimi sayfasından yönetiliyor; SystemConfig
// sayfasından gizleniyor (UI tek yerden edit politikası).

export interface GridSettingMeta {
  key: string;
  label: string;
  type: 'int' | 'string' | 'bool' | 'time' | 'workdays';
  helper?: string;
}

export const TIMESLOT_GRID_SETTINGS: GridSettingMeta[] = [
  { key: 'Schedule.SlotDurationMinutes', label: 'Slot Süresi (dakika)', type: 'int', helper: 'Bir ders kaç dakika sürer.' },
  { key: 'Schedule.BreakBetweenSlotsMinutes', label: 'Slotlar Arası Mola (dakika)', type: 'int', helper: 'Ardışık iki slot arasındaki boşluk.' },
  { key: 'Schedule.MorningStartTime', label: 'Sabah İlk Ders Saati', type: 'time', helper: 'Günün ilk slotu kaçta başlasın.' },
  { key: 'Schedule.AfternoonEndTime', label: 'Gündüz Son Ders Bitişi', type: 'time', helper: 'Gündüz son ders en geç kaçta biter.' },
  { key: 'Schedule.EveningStartTime', label: 'Akşam Dersleri Başlangıcı', type: 'time', helper: 'Akşam dersleri kaçta başlar.' },
  { key: 'Schedule.EveningSlotCount', label: 'Akşam Slot Sayısı', type: 'int', helper: 'Akşam kaç slot olacak.' },
  { key: 'Schedule.WorkDays', label: 'Çalışma Günleri', type: 'workdays', helper: 'Hangi günler ders var (virgülle ayrılmış).' },
  { key: 'Schedule.FaceToFaceCutoffTime', label: 'Yüz-Yüze Kesim Saati', type: 'time', helper: 'Bu saatten sonra yeni TimeSlot\'lar OnlineOnly damgalanır.' },
  { key: 'Schedule.WeekendOnlineOnly', label: 'Hafta Sonu Online-Only', type: 'bool', helper: 'Cumartesi/Pazar otomatik OnlineOnly olsun.' },
];

export const TIMESLOT_GRID_SETTING_KEYS = new Set<string>(
  TIMESLOT_GRID_SETTINGS.map(s => s.key),
);
