import type { AcademicOperationsContext } from '@/types/academicOperations';

export function resolveInitialDepartmentId(context: AcademicOperationsContext | null): number | null {
  if (!context) return null;
  return context.defaultDepartmentId ?? context.accessibleDepartments[0]?.departmentId ?? null;
}
