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
  {
    key: 'Schedule.SlotDurationMinutes',
    label: 'Ders Süresi (dakika)',
    type: 'int',
    helper: 'Bir ders kaç dakika sürer. Örnek: 50 yazarsan her slot 50 dakika olur.',
  },
  {
    key: 'Schedule.BreakBetweenSlotsMinutes',
    label: 'Dersler Arası Mola (dakika)',
    type: 'int',
    helper: 'Peş peşe iki ders arasında kaç dakika mola olsun. Örnek: 5 yazarsan ders biter, 5 dakika boşluk, sonraki ders başlar.',
  },
  {
    key: 'Schedule.MorningStartTime',
    label: 'Sabah İlk Ders Saati',
    type: 'time',
    helper: 'Günün ilk dersi kaçta başlasın. Örnek: 09:00 yazarsan her gün ilk slot 09:00\'da başlar.',
  },
  {
    key: 'Schedule.LunchBreakStart',
    label: 'Öğle Arası Başlangıç',
    type: 'time',
    helper: 'Öğle arası kaçta başlar. Sabah son ders bu saatten önce biter, öğle arasında slot üretilmez.',
  },
  {
    key: 'Schedule.LunchBreakEnd',
    label: 'Öğle Arası Bitiş',
    type: 'time',
    helper: 'Öğle arası kaçta biter. Öğleden sonra ilk ders bu saatten sonra başlar.',
  },
  {
    key: 'Schedule.AfternoonEndTime',
    label: 'Gündüz Son Ders Bitişi',
    type: 'time',
    helper: 'Gündüz dersleri en geç kaçta bitsin. Bu saatten sonra (boşluk varsa) akşam dersleri başlar.',
  },
  {
    key: 'Schedule.EveningStartTime',
    label: 'Akşam Dersleri Başlangıcı',
    type: 'time',
    helper: 'Akşam dersleri kaçta başlasın. Bu saatten itibaren aşağıda belirttiğin kadar akşam dersi peş peşe açılır.',
  },
  {
    key: 'Schedule.EveningSlotCount',
    label: 'Akşam Ders Sayısı',
    type: 'int',
    helper: 'Akşam başlangıç saatinden itibaren peş peşe kaç ders açılsın. Örnek: 2 yazarsan 18:30 ve 19:25 olmak üzere 2 ders, 0 yazarsan akşam ders olmaz.',
  },
  {
    key: 'Schedule.WorkDays',
    label: 'Çalışma Günleri',
    type: 'workdays',
    helper: 'Otomatik üretimde hangi günlerde ders saatleri olsun. Seçilmeyen günlerde slot üretilmez (sonradan elle eklenebilir).',
  },
  {
    key: 'Schedule.FaceToFaceCutoffTime',
    label: 'Yüz-Yüze Son Saati',
    type: 'time',
    helper: 'Bu saatten sonra açılan dersler otomatik olarak "Yalnızca Online" işaretlenir; yüz-yüze ders konulamaz. Örnek: 18:00 yazarsan 18:00 ve sonrası online olur.',
  },
  {
    key: 'Schedule.WeekendOnlineOnly',
    label: 'Hafta Sonu Yalnızca Online',
    type: 'bool',
    helper: 'Açık olursa Cumartesi/Pazar günleri açılan dersler otomatik olarak online işaretlenir; yüz-yüze yapılamaz.',
  },
];

export const TIMESLOT_GRID_SETTING_KEYS = new Set<string>(
  TIMESLOT_GRID_SETTINGS.map(s => s.key),
);

// ── Validation ────────────────────────────────────────────────────────────
//
// Her şablon ayarı kaydedilmeden önce çalıştırılır. Per-field tipi kontrolü +
// cross-field sıralama: Sabah < ÖğleBaşı < ÖğleSonu < GündüzSonu ≤ AkşamBaşı.
// Diğer ayarın değeri `allConfigs` map'inden okunur (kayıtlı son hali).

const VALID_DAYS = new Set(['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday']);
const TIME_RE = /^([01]\d|2[0-3]):([0-5]\d)(:[0-5]\d)?$/;

function parseTimeMinutes(t: string): number {
  const m = TIME_RE.exec(t);
  if (!m) return NaN;
  return parseInt(m[1], 10) * 60 + parseInt(m[2], 10);
}

export function validateGridSetting(
  key: string,
  newValue: string,
  allConfigs: ReadonlyMap<string, string>,
): string | null {
  const setting = TIMESLOT_GRID_SETTINGS.find(s => s.key === key);
  if (!setting) return null;

  const value = newValue.trim();
  if (value === '') return 'Boş olamaz.';

  if (setting.type === 'int') {
    const n = parseInt(value, 10);
    if (!Number.isFinite(n) || String(n) !== value) return 'Geçerli bir tam sayı girin.';
    if (key === 'Schedule.SlotDurationMinutes' && n < 1) return 'Slot süresi en az 1 dakika olmalı.';
    if (key === 'Schedule.BreakBetweenSlotsMinutes' && n < 0) return 'Mola negatif olamaz.';
    if (key === 'Schedule.EveningSlotCount' && n < 0) return 'Akşam slot sayısı negatif olamaz.';
    return null;
  }

  if (setting.type === 'time') {
    if (!TIME_RE.test(value)) return 'Saat formatı HH:MM olmalı.';
    const min = parseTimeMinutes(value);
    const otherMin = (k: string): number | null => {
      const v = allConfigs.get(k);
      if (!v) return null;
      const m = parseTimeMinutes(v);
      return Number.isFinite(m) ? m : null;
    };

    if (key === 'Schedule.MorningStartTime') {
      const lunchStart = otherMin('Schedule.LunchBreakStart');
      if (lunchStart !== null && min >= lunchStart) return `Öğle arası başlangıcından (${allConfigs.get('Schedule.LunchBreakStart')}) önce olmalı.`;
    }
    if (key === 'Schedule.LunchBreakStart') {
      const morning = otherMin('Schedule.MorningStartTime');
      const lunchEnd = otherMin('Schedule.LunchBreakEnd');
      if (morning !== null && min <= morning) return `Sabah başlangıcından (${allConfigs.get('Schedule.MorningStartTime')}) sonra olmalı.`;
      if (lunchEnd !== null && min >= lunchEnd) return `Öğle arası bitişinden (${allConfigs.get('Schedule.LunchBreakEnd')}) önce olmalı.`;
    }
    if (key === 'Schedule.LunchBreakEnd') {
      const lunchStart = otherMin('Schedule.LunchBreakStart');
      const afternoonEnd = otherMin('Schedule.AfternoonEndTime');
      if (lunchStart !== null && min <= lunchStart) return `Öğle arası başlangıcından (${allConfigs.get('Schedule.LunchBreakStart')}) sonra olmalı.`;
      if (afternoonEnd !== null && min >= afternoonEnd) return `Gündüz son ders bitişinden (${allConfigs.get('Schedule.AfternoonEndTime')}) önce olmalı.`;
    }
    if (key === 'Schedule.AfternoonEndTime') {
      const lunchEnd = otherMin('Schedule.LunchBreakEnd');
      const eveningStart = otherMin('Schedule.EveningStartTime');
      if (lunchEnd !== null && min <= lunchEnd) return `Öğle arası bitişinden (${allConfigs.get('Schedule.LunchBreakEnd')}) sonra olmalı.`;
      if (eveningStart !== null && min > eveningStart) return `Akşam başlangıcından (${allConfigs.get('Schedule.EveningStartTime')}) önce veya ona eşit olmalı.`;
    }
    if (key === 'Schedule.EveningStartTime') {
      const afternoonEnd = otherMin('Schedule.AfternoonEndTime');
      if (afternoonEnd !== null && min < afternoonEnd) return `Gündüz son ders bitişinden (${allConfigs.get('Schedule.AfternoonEndTime')}) sonra veya ona eşit olmalı.`;
    }
    // FaceToFaceCutoffTime: serbest — herhangi bir saat anlamlı olabilir.
    return null;
  }

  if (setting.type === 'workdays') {
    const days = value.split(',').map(d => d.trim()).filter(d => d.length > 0);
    if (days.length === 0) return 'En az 1 gün seçmelisiniz.';
    for (const d of days) {
      if (!VALID_DAYS.has(d)) return `Geçersiz gün: "${d}". Beklenen: Monday, Tuesday, ...`;
    }
    if (new Set(days).size !== days.length) return 'Tekrarlanan gün var.';
    return null;
  }

  // bool ve string: serbest
  return null;
}
