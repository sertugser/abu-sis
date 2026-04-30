/**
 * Lisans (240 AKTS) → 4 yıl → atlatma 1→2, 2→3, 3→4
 * Tıp (360 AKTS) → 6 yıl → atlatma 1→2, ..., 5→6
 * Önlisans (120 AKTS) → 2 yıl → atlatma 1→2
 */
export function getFromYearOptionsFromEcts(totalRequiredEcts: number | undefined | null): number[] {
  const DEFAULT_MAX_YEAR = 4;
  const maxYear = totalRequiredEcts && totalRequiredEcts > 0
    ? Math.ceil(totalRequiredEcts / 60)
    : DEFAULT_MAX_YEAR;
  return Array.from({ length: Math.max(1, maxYear - 1) }, (_, i) => i + 1);
}
