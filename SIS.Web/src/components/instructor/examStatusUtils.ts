// ── constants ─────────────────────────────────────────────────
const MS_PER_HOUR = 1000 * 60 * 60;
const MS_PER_DAY = MS_PER_HOUR * 24;

// ── announce status ───────────────────────────────────────────
export type AnnounceStatus =
  | { kind: 'announced' }
  | { kind: 'countdown'; days: number; hours: number }
  | { kind: 'overdue' };

/**
 * Pure announce-status helper.
 * deadline = examDate − announceDeadlineDays. Compared against `now`.
 */
export function computeAnnounceStatus(
  examDateIso: string,
  isAnnounced: boolean,
  announceDeadlineDays: number,
  now: number,
): AnnounceStatus {
  if (isAnnounced) return { kind: 'announced' };

  const deadline = new Date(examDateIso).getTime() - announceDeadlineDays * MS_PER_DAY;
  const remaining = deadline - now;
  if (remaining <= 0) return { kind: 'overdue' };

  const days = Math.floor(remaining / MS_PER_DAY);
  const hours = Math.floor((remaining % MS_PER_DAY) / MS_PER_HOUR);
  return { kind: 'countdown', days, hours };
}
