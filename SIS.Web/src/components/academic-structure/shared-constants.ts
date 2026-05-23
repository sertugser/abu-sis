// ─── Helpers ─────────────────────────────────────────────────────

/**
 * API hata cevabından kullanıcıya gösterilecek mesajı çıkarır.
 * Backend `{ message }` / `{ error }` / `{ title }` döndürebilir.
 * Faculty/Department/Program sayfaları ortak kullanır.
 */
export function extractErrorMessage(e: unknown, fallback: string): string {
  const resp = (e as { response?: { data?: { error?: string; message?: string; title?: string } } })?.response?.data;
  return resp?.error ?? resp?.message ?? resp?.title ?? (e instanceof Error ? e.message : fallback);
}

/**
 * Audit tarihlerini gösterim için biçimler. Eski kayıtlarda timestamp set
 * edilmemiş olabilir (0001-01-01) — bu durumda "—" döner.
 */
export function formatDateTime(value: string | null | undefined): string {
  if (!value) return '—';
  const d = new Date(value);
  if (Number.isNaN(d.getTime()) || d.getFullYear() < 2000) return '—';
  return d.toLocaleString('tr-TR', { dateStyle: 'medium', timeStyle: 'short' });
}

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
  externalSystemCode: string;
  degreeLevel: string; educationType: string;
  maxStudyDurationSemesters: number; totalRequiredEcts: number;
}
export function emptyProgramForm(departmentId = 0): ProgramFormState {
  return {
    name: '', code: '', externalSystemCode: '', departmentId,
    degreeLevel: 'Bachelor', educationType: 'Semester',
    maxStudyDurationSemesters: 8, totalRequiredEcts: 240,
  };
}
