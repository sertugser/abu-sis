/**
 * Pure statistics helpers for grade entry.
 * No external dependencies — all math is vanilla JS.
 */

export interface ScoreStats {
  count: number;
  mean: number;
  stdDev: number;
  min: number;
  max: number;
  median: number;
}

/**
 * Computes descriptive statistics for an array of numeric scores.
 * Returns null when the array is empty.
 * Uses population standard deviation (÷ n).
 */
export function computeScoreStats(scores: number[]): ScoreStats | null {
  if (scores.length === 0) return null;

  const count = scores.length;
  const min = Math.min(...scores);
  const max = Math.max(...scores);

  const sum = scores.reduce((acc, s) => acc + s, 0);
  const mean = sum / count;

  const variance = scores.reduce((acc, s) => acc + (s - mean) ** 2, 0) / count;
  const stdDev = Math.sqrt(variance);

  const sorted = [...scores].sort((a, b) => a - b);
  const mid = Math.floor(count / 2);
  const median = count % 2 === 0 ? (sorted[mid - 1] + sorted[mid]) / 2 : sorted[mid];

  return {
    count,
    mean: Math.round(mean * 10) / 10,
    stdDev: Math.round(stdDev * 10) / 10,
    min,
    max,
    median: Math.round(median * 10) / 10,
  };
}

/**
 * Counts how many scores map to each letter grade using the provided resolver.
 */
export function letterDistribution(
  scores: number[],
  resolve: (score: number) => string,
): Record<string, number> {
  const dist: Record<string, number> = {};
  for (const score of scores) {
    const letter = resolve(score);
    dist[letter] = (dist[letter] ?? 0) + 1;
  }
  return dist;
}

const DEFAULT_FAIL_CODES = ['FF', 'NA'];

/**
 * Splits scores into pass / fail buckets.
 * @param failCodes - letter grades considered failing (default: FF, NA)
 */
export function passFailCounts(
  scores: number[],
  resolve: (score: number) => string,
  failCodes: string[] = DEFAULT_FAIL_CODES,
): { pass: number; fail: number } {
  const failSet = new Set(failCodes);
  let pass = 0;
  let fail = 0;
  for (const score of scores) {
    const letter = resolve(score);
    if (failSet.has(letter)) {
      fail += 1;
    } else {
      pass += 1;
    }
  }
  return { pass, fail };
}
