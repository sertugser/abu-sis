/**
 * Harf notu görüntü yardımcıları. MyCoursesPage ve GradeListPage gibi
 * birden fazla sayfa aynı görsel sözleşmeyi paylaşır.
 *
 * Tailwind theme token'ları üzerinden gider; hex renk hardcoded yazılmaz.
 */

export const GRADE_BADGE_CLASS: Record<string, string> = {
  AA: 'bg-success text-success-foreground',
  BA: 'bg-success text-success-foreground',
  BB: 'bg-success/80 text-success-foreground',
  CB: 'bg-accent/20 text-accent-foreground',
  CC: 'bg-accent/20 text-accent-foreground',
  DC: 'bg-warning/20 text-warning-foreground',
  DD: 'bg-warning/20 text-warning-foreground',
  FF: 'bg-destructive text-destructive-foreground',
  NA: 'bg-destructive text-destructive-foreground',
  W:  'bg-muted text-muted-foreground',
  I:  'bg-muted text-muted-foreground',
  S:  'bg-success text-success-foreground',
  U:  'bg-destructive text-destructive-foreground',
  NI: 'bg-muted text-muted-foreground',
};

/** Geçer/kalır durumunu sayfa tipinden bağımsız döner. */
export const PASSING_GRADES = new Set(['AA', 'BA', 'BB', 'CB', 'CC', 'DC', 'DD', 'S']);
export const FAILING_GRADES = new Set(['FF', 'NA', 'U']);

export function getGradeBadgeClass(grade: string | null | undefined): string {
  if (!grade) return 'bg-muted text-muted-foreground';
  return GRADE_BADGE_CLASS[grade] ?? 'bg-muted text-muted-foreground';
}

/** "Alış Şekli" rozeti rengi. Server hesaplar; UI semantik renk eşler. */
export function getAcquisitionBadgeClass(acquisitionType: string): string {
  if (acquisitionType.startsWith('Devamlı Alttan')) return 'bg-destructive/15 text-destructive border border-destructive/30';
  if (acquisitionType.startsWith('Alttan')) return 'bg-warning/15 text-warning-foreground border border-warning/30';
  if (acquisitionType === 'Zorunlu') return 'bg-primary/10 text-primary border border-primary/20';
  if (acquisitionType === 'Seçmeli') return 'bg-accent/15 text-accent-foreground border border-accent/30';
  return 'bg-muted text-muted-foreground border border-border';
}
