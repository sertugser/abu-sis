import type { SemesterType } from '@/hooks/useCalendarGenerator';

export const SEMESTERS: { value: SemesterType; label: string }[] = [
  { value: 'Fall', label: 'Güz' },
  { value: 'Spring', label: 'Bahar' },
  { value: 'Summer', label: 'Yaz' },
];

export function semesterLabel(semester?: SemesterType | string | null) {
  return SEMESTERS.find(s => s.value === semester)?.label ?? semester ?? 'Genel';
}

export function compareSemesters(a: SemesterType, b: SemesterType) {
  return SEMESTERS.findIndex(option => option.value === a) - SEMESTERS.findIndex(option => option.value === b);
}

export function normalizeSemesters(semesters: SemesterType[]) {
  return Array.from(new Set(semesters)).sort(compareSemesters);
}

export function isSemesterType(value: string): value is SemesterType {
  return SEMESTERS.some(option => option.value === value);
}

export function termTypeToSemester(termType: string): SemesterType | null {
  if (termType === 'Fall' || termType === 'FallResit') return 'Fall';
  if (termType === 'Spring' || termType === 'SpringResit' || termType === 'SpringOther') return 'Spring';
  if (termType === 'Summer' || termType === 'SummerResit' || termType === 'SummerOther') return 'Summer';
  return null;
}

export function semesterListText(semesters: SemesterType[]) {
  return semesters.length > 0 ? semesters.map(semester => semesterLabel(semester)).join(', ') : 'yok';
}
