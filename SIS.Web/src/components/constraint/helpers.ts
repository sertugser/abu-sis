import type { ConstraintExceptionDto } from '../../types/constraint';

export function getConstraintErrorMessage(error: unknown, fallback: string): string {
  if (
    typeof error === 'object' && error !== null && 'response' in error
    && typeof (error as { response?: { data?: { error?: unknown } } }).response?.data?.error === 'string'
  ) {
    return (error as { response: { data: { error: string } } }).response.data.error;
  }
  if (error instanceof Error && error.message) return error.message;
  return fallback;
}

export function exceptionEntityInfo(ex: ConstraintExceptionDto): string {
  if (ex.instructorName) return `Ogretim Uyesi: ${ex.instructorName} (#${ex.instructorId})`;
  if (ex.studentName) return `Ogrenci: ${ex.studentName} (#${ex.studentId})`;
  if (ex.departmentName) return `Bolum: ${ex.departmentName} (#${ex.departmentId})`;
  if (ex.courseSectionInfo) return `Sube: ${ex.courseSectionInfo} (#${ex.courseSectionId})`;
  if (ex.termName) return `Donem: ${ex.termName} (#${ex.termId})`;
  return 'Bilinmeyen';
}

export const SCOPE_LABELS: Record<string, string> = {
  Schedule: 'Ders Programi',
  Registration: 'Kayit',
  Section: 'Sube',
  Instructor: 'Ogretim Uyesi',
  Classroom: 'Derslik',
};
