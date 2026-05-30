// scheduleUtils.ts sabit 09:00–18:00 SAAT-BAŞI ızgarayı kullanır (editör için).
// Bu modül VERİ-ODAKLI kardeşidir: hem aralık hem de yatay çizgiler derslerin
// gerçek başlangıç/bitiş saatlerinden üretilir; böylece her blok kendi iki çizgisi
// arasına tam oturur (saat-başı çizgiyi "kesip kayma" sorunu olmaz).

const PX_PER_MINUTE = 1.5; // dikey yoğunluk; 60 dk = 90px
const DEFAULT_RANGE: MinuteRange = { startMin: 9 * 60, endMin: 17 * 60 };

export interface MinuteRange {
  /** Izgara taban dakikası (gün başından, ör. 09:00 → 540). */
  startMin: number;
  /** Izgara tavan dakikası (ör. 17:45 → 1065). */
  endMin: number;
}

export interface TimeBlock {
  startTime: string; // "HH:mm" veya "HH:mm:ss"
  endTime: string;
}

export interface Positioned<T> {
  item: T;
  /** 0-tabanlı kolon indeksi (çakışma varsa). */
  lane: number;
  /** Bu kümedeki toplam paralel kolon sayısı. */
  laneCount: number;
}

/** "HH:mm"/"HH:mm:ss" → gün başından dakika. */
export function timeToMinutes(time: string): number {
  const [h, m] = time.split(':').map(Number);
  return h * 60 + m;
}

/** Dakika → "HH:mm" etiketi. */
export function minutesToLabel(minutes: number): string {
  const h = Math.floor(minutes / 60);
  const m = minutes % 60;
  return `${String(h).padStart(2, '0')}:${String(m).padStart(2, '0')}`;
}

/**
 * Aralığı blokların TAM en erken başlangıcı / en geç bitişinden üretir (saat sınırına
 * yuvarlamaz — ilk satır tam ilk dersin saatinde başlar). Hiç blok yoksa varsayılan 9–17.
 */
export function computeMinuteRange(blocks: TimeBlock[]): MinuteRange {
  if (blocks.length === 0) return DEFAULT_RANGE;
  let min = Number.POSITIVE_INFINITY;
  let max = Number.NEGATIVE_INFINITY;
  for (const b of blocks) {
    min = Math.min(min, timeToMinutes(b.startTime));
    max = Math.max(max, timeToMinutes(b.endTime));
  }
  return { startMin: min, endMin: max };
}

/** Dakika → ızgara tepesinden px offset (range.startMin referanslı). */
export function minutesToOffset(minutes: number, range: MinuteRange): number {
  return (minutes - range.startMin) * PX_PER_MINUTE;
}

/** time (HH:mm) → ızgara tepesinden px offset. */
export function timeToOffset(time: string, range: MinuteRange): number {
  return minutesToOffset(timeToMinutes(time), range);
}

/** Süreye orantılı yükseklik (px). */
export function blockHeight(startTime: string, endTime: string): number {
  const duration = timeToMinutes(endTime) - timeToMinutes(startTime);
  return Math.max(duration, 1) * PX_PER_MINUTE;
}

/** Toplam ızgara yüksekliği (px). */
export function rangeHeight(range: MinuteRange): number {
  return (range.endMin - range.startMin) * PX_PER_MINUTE;
}

/**
 * Tüm blokların başlangıç + bitiş saatlerinden tekilleştirilmiş, sıralı sınır
 * listesi (dakika). Yatay ızgara çizgileri ve sol eksen etiketleri bundan üretilir.
 */
export function collectBoundaries(blocks: TimeBlock[]): number[] {
  const set = new Set<number>();
  for (const b of blocks) {
    set.add(timeToMinutes(b.startTime));
    set.add(timeToMinutes(b.endTime));
  }
  return [...set].sort((a, b) => a - b);
}

/**
 * Aynı gün içinde zaman olarak örtüşen blokları yan yana kolonlara (lane) yerleştirir.
 * Öğrencinin kendi dersleri normalde çakışmaz; bu yalnızca veri hatası durumunda
 * blokların üst üste binmesini engeller. Greedy lane atama, küme bazında.
 */
export function computeLanes<T extends TimeBlock>(blocks: T[]): Positioned<T>[] {
  const items = blocks
    .map((b) => ({
      item: b,
      start: timeToMinutes(b.startTime),
      end: timeToMinutes(b.endTime),
      lane: 0,
      laneCount: 1,
    }))
    .sort((a, b) => a.start - b.start || a.end - b.end);

  let i = 0;
  while (i < items.length) {
    // Karşılıklı örtüşen bir küme topla.
    let clusterEnd = items[i].end;
    let j = i + 1;
    while (j < items.length && items[j].start < clusterEnd) {
      clusterEnd = Math.max(clusterEnd, items[j].end);
      j++;
    }
    const cluster = items.slice(i, j);
    // Greedy lane atama.
    const laneEnds: number[] = [];
    for (const it of cluster) {
      let placed = false;
      for (let lane = 0; lane < laneEnds.length; lane++) {
        if (laneEnds[lane] <= it.start) {
          it.lane = lane;
          laneEnds[lane] = it.end;
          placed = true;
          break;
        }
      }
      if (!placed) {
        it.lane = laneEnds.length;
        laneEnds.push(it.end);
      }
    }
    for (const it of cluster) it.laneCount = laneEnds.length;
    i = j;
  }

  return items.map(({ item, lane, laneCount }) => ({ item, lane, laneCount }));
}
