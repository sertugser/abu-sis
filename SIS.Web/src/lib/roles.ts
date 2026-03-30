export const MANAGEMENT_ROLES = ['SystemAdministrator', 'Dean', 'DepartmentManager', 'AdministrativeStaff'] as const;
export const DEAN_ROLES = ['Dean', 'SystemAdministrator'] as const;
export const STUDENT_ROLES = ['Student'] as const;
export const INSTRUCTOR_ROLES = ['Instructor'] as const;
export const ADVISOR_ROLES = ['AcademicAdvisor'] as const;

export type AppRole =
  | 'SystemAdministrator'
  | 'Dean'
  | 'DepartmentManager'
  | 'AdministrativeStaff'
  | 'AcademicAdvisor'
  | 'Instructor'
  | 'Student';

const ROLE_PRIORITY: AppRole[] = [
  'SystemAdministrator',
  'Dean',
  'DepartmentManager',
  'AdministrativeStaff',
  'AcademicAdvisor',
  'Instructor',
  'Student',
];

export function getPrimaryRole(roles: string[]): AppRole {
  return ROLE_PRIORITY.find(role => roles.includes(role)) ?? 'Student';
}

export function hasAnyRole(roles: string[], allowedRoles: readonly string[]) {
  return allowedRoles.some(role => roles.includes(role));
}

export function isManagementRole(roles: string[]) {
  return hasAnyRole(roles, MANAGEMENT_ROLES);
}
