import type { ElectiveSubtypeDto } from '@/types/electiveSubtype';

/**
 * Faz E: DB-driven elective subtype display utilities.
 * Hardcoded ELECTIVE_SUBTYPE_LABELS/SHORT/COLORS yerine — code'a göre renk
 * fallback (bilinen 3 default için), custom subtype'lar için gri.
 */

const SUBTYPE_COLOR_BY_CODE: Record<string, string> = {
  UniversityElective: 'bg-blue-50 text-blue-700 border-blue-200',
  NonDepartmentElective: 'bg-purple-50 text-purple-700 border-purple-200',
  TechnicalElective: 'bg-amber-50 text-amber-700 border-amber-200',
};

const FALLBACK_COLOR = 'bg-slate-50 text-slate-700 border-slate-200';

export function getSubtypeColorByCode(code: string | undefined | null): string {
  if (!code) return FALLBACK_COLOR;
  return SUBTYPE_COLOR_BY_CODE[code] ?? FALLBACK_COLOR;
}

export function findSubtypeById(
  subtypes: ElectiveSubtypeDto[] | undefined,
  id: number | null | undefined,
): ElectiveSubtypeDto | null {
  if (!subtypes || id === null || id === undefined) return null;
  return subtypes.find(s => s.id === id) ?? null;
}
