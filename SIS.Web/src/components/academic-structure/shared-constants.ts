// ─── Constants ───────────────────────────────────────────────────

export const DEGREE_LEVELS = [
  { value: 'Associate', label: 'Ön Lisans' },
  { value: 'Bachelor', label: 'Lisans' },
  { value: 'Master', label: 'Yüksek Lisans' },
  { value: 'Doctorate', label: 'Doktora' },
];

export const EDUCATION_TYPES = [
  { value: 'Semester', label: 'Dönemlik' },
  { value: 'Annual', label: 'Yıllık' },
];

export const SELECT_CLS = 'w-full rounded-md border border-border bg-background px-3 py-2 text-sm text-foreground focus:outline-none focus:ring-2 focus:ring-ring';

// ─── Form state types ────────────────────────────────────────────

export interface FacultyFormState { name: string; code: string; }
export function emptyFacultyForm(): FacultyFormState { return { name: '', code: '' }; }

export interface DepartmentFormState { name: string; code: string; facultyId: number; }
export function emptyDepartmentForm(facultyId = 0): DepartmentFormState { return { name: '', code: '', facultyId }; }

export interface ProgramFormState {
  name: string; code: string; departmentId: number;
  degreeLevel: string; educationType: string;
  maxStudyDurationSemesters: number; totalRequiredEcts: number;
}
export function emptyProgramForm(departmentId = 0): ProgramFormState {
  return {
    name: '', code: '', departmentId,
    degreeLevel: 'Bachelor', educationType: 'Semester',
    maxStudyDurationSemesters: 8, totalRequiredEcts: 240,
  };
}
