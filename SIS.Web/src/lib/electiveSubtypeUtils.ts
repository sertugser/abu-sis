import type { ElectiveSubtypeDto } from '@/types/electiveSubtype';

/**
 * DB-driven elective subtype display utilities (Faz E + custom subtype renk fallback).
 *
 * Renk eşlemesi:
 * - Bilinen 3 default subtype (UniversityElective / NonDepartmentElective /
 *   TechnicalElective) → sabit renk eşlemesi (geri uyumluluk).
 * - Admin tarafından eklenen custom subtype'lar (örn. "OpenCourse") →
 *   code string'i deterministik hash'lenerek `FALLBACK_PALETTE` paletinden
 *   tutarlı bir renk seçilir. Aynı code her zaman aynı rengi alır.
 *
 * Bu yaklaşım DB'ye `Color` field'i eklemeden, custom subtype tanımlanır
 * tanımlanmaz görsel olarak ayırt edilebilir hale getirir.
 */

/** Default subtype'lar için sabit renkler — Faz 6 öncesi seed'lerle uyumlu. */
const SUBTYPE_COLOR_BY_CODE: Record<string, string> = {
  UniversityElective: 'bg-blue-50 text-blue-700 border-blue-200',
  NonDepartmentElective: 'bg-purple-50 text-purple-700 border-purple-200',
  TechnicalElective: 'bg-amber-50 text-amber-700 border-amber-200',
};

/** Custom subtype'lar için palette — code hash'ine göre deterministik seçilir. */
const FALLBACK_PALETTE: readonly string[] = [
  'bg-emerald-50 text-emerald-700 border-emerald-200',
  'bg-rose-50 text-rose-700 border-rose-200',
  'bg-cyan-50 text-cyan-700 border-cyan-200',
  'bg-pink-50 text-pink-700 border-pink-200',
  'bg-orange-50 text-orange-700 border-orange-200',
  'bg-teal-50 text-teal-700 border-teal-200',
  'bg-indigo-50 text-indigo-700 border-indigo-200',
  'bg-lime-50 text-lime-700 border-lime-200',
];

/** Code/name eksik olursa nötr gri. */
const NEUTRAL_FALLBACK = 'bg-slate-50 text-slate-700 border-slate-200';

/** Basit deterministik string hash (djb2 türevi) — palette index seçimi için. */
function hashCode(s: string): number {
  let hash = 0;
  for (let i = 0; i < s.length; i++) {
    hash = ((hash << 5) - hash) + s.charCodeAt(i);
    hash |= 0;
  }
  return Math.abs(hash);
}

/**
 * Subtype code'una göre badge renk class'ları döndürür.
 * - Default 3 subtype → sabit renk
 * - Custom subtype → palette'ten deterministik (her zaman aynı)
 * - null/undefined code → nötr gri
 */
export function getSubtypeColorByCode(code: string | undefined | null): string {
  if (!code) return NEUTRAL_FALLBACK;
  const fixed = SUBTYPE_COLOR_BY_CODE[code];
  if (fixed) return fixed;
  return FALLBACK_PALETTE[hashCode(code) % FALLBACK_PALETTE.length];
}

export function findSubtypeById(
  subtypes: ElectiveSubtypeDto[] | undefined,
  id: number | null | undefined,
): ElectiveSubtypeDto | null {
  if (!subtypes || id === null || id === undefined) return null;
  return subtypes.find(s => s.id === id) ?? null;
}
