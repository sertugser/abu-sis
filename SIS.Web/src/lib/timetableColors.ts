export interface CourseColor {
  /** Zemin + kenarlık sınıfları. Yazı rengi DAHİL DEĞİL — okunabilirlik için
   *  bloklarda koyu nötr (text-slate-900) kullanılır; renk yalnızca zeminde. */
  base: string;
  /** Dolu renk swatch'ı (kenar çubuğu / rozet arka planı). */
  accent: string;
}

// Takvim ızgarası ders renkleri — tek kaynak (öğrenci + öğretmen ortak).
const COURSE_PALETTE: readonly CourseColor[] = [
  { base: 'bg-blue-100 border-blue-300', accent: 'bg-blue-500' },
  { base: 'bg-emerald-100 border-emerald-300', accent: 'bg-emerald-500' },
  { base: 'bg-violet-100 border-violet-300', accent: 'bg-violet-500' },
  { base: 'bg-amber-100 border-amber-300', accent: 'bg-amber-500' },
  { base: 'bg-rose-100 border-rose-300', accent: 'bg-rose-500' },
  { base: 'bg-cyan-100 border-cyan-300', accent: 'bg-cyan-500' },
  { base: 'bg-orange-100 border-orange-300', accent: 'bg-orange-500' },
  { base: 'bg-teal-100 border-teal-300', accent: 'bg-teal-500' },
  { base: 'bg-pink-100 border-pink-300', accent: 'bg-pink-500' },
  { base: 'bg-indigo-100 border-indigo-300', accent: 'bg-indigo-500' },
];

/**
 * Ders koduna göre deterministik renk döner. Aynı kod her zaman aynı rengi
 * verir (gün/şube fark etmez); böylece göz aynı dersi haftada hızlıca takip eder.
 */
export function colorForCourse(courseCode: string): CourseColor {
  let hash = 0;
  for (let i = 0; i < courseCode.length; i++) {
    hash = (hash * 31 + courseCode.charCodeAt(i)) | 0;
  }
  const index = Math.abs(hash) % COURSE_PALETTE.length;
  return COURSE_PALETTE[index];
}

/**
 * Lab bloklarının üstüne bindirilecek ince diyagonal çizgi deseni.
 * Tailwind v4 arbitrary value — statik string olduğu için purge'lenmez.
 */
export const LAB_STRIPE_CLASS =
  'bg-[repeating-linear-gradient(45deg,transparent,transparent_5px,rgba(0,0,0,0.07)_5px,rgba(0,0,0,0.07)_10px)]';
