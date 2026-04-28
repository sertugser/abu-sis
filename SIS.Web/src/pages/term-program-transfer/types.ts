// Sayfa-içi local tipler. Domain tipleri ../../types/courseOpening.ts'te.

export interface CourseOverride {
  sectionCount: number;
  capacityPerSection: number;
  instructorId: number | null;
  internalQuota: number | null;
  externalQuota: number | null;
  accessibleDepartmentIds: number[] | null;
  includeCrossLists: boolean;
}

export type FilterType = 'all' | 'compulsory' | 'elective';

export const CLASS_LEVELS: ReadonlyArray<{ value: number; label: string }> = [
  { value: 1, label: '1. Sınıf' },
  { value: 2, label: '2. Sınıf' },
  { value: 3, label: '3. Sınıf' },
  { value: 4, label: '4. Sınıf' },
];

/** SuggestedSemester (1..8) → Sınıf (1..4). Semester 0 = bilinmiyor → 0 döner. */
export function semesterToClassLevel(semester: number): number {
  if (semester <= 0) return 0;
  return Math.ceil(semester / 2);
}
