/**
 * Akademik sınıf yılı yardımcıları.
 *
 * Lisans programlarında 1-4 sınıf varsayımı yapar (default `maxClassYear = 4`).
 * Önlisans (2), 5/6 yıllık programlar (Tıp/Mimarlık/Hukuk) ve lisansüstü için
 * çağıran tarafın `maxClassYear` parametresini override etmesi gerekir.
 *
 * Backend `StudentInfoDto.ClassYear` zaten hesaplayıp döndürüyor — UI tarafında
 * tekrar hesaplama yapma; sadece dropdown/filter listeleri için bu helper'ı kullan.
 */

export const DEFAULT_MAX_CLASS_YEAR = 4;

/**
 * Dropdown ve filtre listeleri için 1..N sınıf yılı dizisi üretir.
 *
 * @example
 * classYearOptions()       // [1, 2, 3, 4]
 * classYearOptions(2)      // [1, 2]   önlisans
 * classYearOptions(6)      // [1, 2, 3, 4, 5, 6]   tıp
 */
export function classYearOptions(maxClassYear: number = DEFAULT_MAX_CLASS_YEAR): number[] {
  const safeMax = Math.max(1, Math.floor(maxClassYear));
  return Array.from({ length: safeMax }, (_, i) => i + 1);
}

/**
 * Tamamlanan yarıyıl sayısından sınıf yılı hesaplar (1-based).
 * Backend `StudentInfoDto.ClassYear` zaten bunu hesapladığı için UI'da tekrar
 * çağırma — bu sadece backend'in dönmediği durumlar (öğretmen/admin görünümü) için.
 */
export function classYearFromCompletedSemesters(
  completedSemesters: number,
  maxClassYear: number = DEFAULT_MAX_CLASS_YEAR,
): number {
  const safeMax = Math.max(1, Math.floor(maxClassYear));
  const safeCompleted = Math.max(0, Math.floor(completedSemesters));
  return Math.min(Math.floor(safeCompleted / 2) + 1, safeMax);
}
