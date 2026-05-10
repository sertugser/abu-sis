// ── Hedef kitle enum (backend AnnouncementAudience ile eşleşmeli) ─────────────

export type AnnouncementAudience =
  | 'All'
  | 'Students'
  | 'Instructors'
  | 'Management'
  | 'Department'
  | 'DepartmentStudents'
  | 'DepartmentInstructors'
  | 'Faculty'
  | 'FacultyStudents'
  | 'FacultyInstructors'
  | 'FacultyManagement'
  | 'CourseSection'
  | 'MyAdvisees'
  | 'AcademicAdvisors'
  | 'DepartmentManagers'
  | 'Deans'
  | 'AdministrativeStaffRole'
  | 'AllForSysAdmin'
  | 'AllForAdmin'
  | 'AllForDean'
  | 'AllForDeptMgr';

export const ANNOUNCEMENT_AUDIENCE_LABELS: Record<AnnouncementAudience, string> = {
  All:                    'Herkes',
  Students:               'Öğrenciler',
  Instructors:            'Öğretim Üyeleri',
  Management:             'Yönetim',
  Department:             'Bölümdeki Herkes',
  DepartmentStudents:     'Bölüm Öğrencileri',
  DepartmentInstructors:  'Bölüm Öğretim Elemanları',
  Faculty:                'Fakültedeki Herkes',
  FacultyStudents:        'Fakülte Öğrencileri',
  FacultyInstructors:     'Fakülte Öğretim Elemanları',
  FacultyManagement:      'Fakülte Bölüm Başkanları',
  CourseSection:          'Ders Şubesi Öğrencileri',
  MyAdvisees:             'Danışman Öğrencilerim',
  AcademicAdvisors:       'Akademik Danışmanlar',
  DepartmentManagers:     'Bölüm Başkanları',
  Deans:                  'Dekanlar',
  AdministrativeStaffRole: 'İdari Personel',
  AllForSysAdmin:         'Herkes',
  AllForAdmin:            'Herkes',
  AllForDean:             'Herkes',
  AllForDeptMgr:          'Herkes',
};

// ── Announcement DTOs ─────────────────────────────────────────────────────────

export interface AnnouncementList {
  id: number;
  title: string;
  contentPreview: string;
  targetAudience: AnnouncementAudience;
  targetDepartmentId: number | null;
  targetDepartmentName: string | null;
  targetFacultyId: number | null;
  targetFacultyName: string | null;
  targetSectionId: number | null;
  targetSectionInfo: string | null;
  isPinned: boolean;
  startsAt: string;
  expiresAt: string | null;
  createdByName: string;
  createdAt: string;
  isActive: boolean;
}

export interface AnnouncementDetail {
  id: number;
  title: string;
  content: string;
  targetAudience: AnnouncementAudience;
  targetDepartmentId: number | null;
  targetDepartmentName: string | null;
  targetFacultyId: number | null;
  targetFacultyName: string | null;
  targetSectionId: number | null;
  targetSectionInfo: string | null;
  isPinned: boolean;
  startsAt: string;
  expiresAt: string | null;
  createdByName: string;
  createdAt: string;
  updatedAt: string | null;
  isActive: boolean;
}

export interface CreateAnnouncementPayload {
  title: string;
  content: string;
  targetAudience: AnnouncementAudience;
  targetDepartmentId: number | null;
  targetFacultyId: number | null;
  targetSectionId: number | null;
  isPinned: boolean;
  startsAt: string | null;
  expiresAt: string | null;
}

export interface UpdateAnnouncementPayload {
  title: string;
  content: string;
  targetAudience: AnnouncementAudience;
  targetDepartmentId: number | null;
  targetFacultyId: number | null;
  targetSectionId: number | null;
  isPinned: boolean;
  startsAt: string;
  expiresAt: string | null;
}

// ── Create context (form seçenekleri) ─────────────────────────────────────────

export interface AnnouncementSectionOption {
  id: number;
  courseName: string;
  courseCode: string;
  sectionNumber: number;
  termName: string;
}

export interface AnnouncementCreateContext {
  senderRole: 'Instructor' | 'Dean' | 'DepartmentManager' | 'Admin';
  allowedAudiences: AnnouncementAudience[];
  departmentId: number | null;
  departmentName: string | null;
  facultyId: number | null;
  facultyName: string | null;
  sections: AnnouncementSectionOption[];
}

// ── Audience badge helper ─────────────────────────────────────────────────────

export function getAudienceBadgeLabel(item: Pick<AnnouncementList, 'targetAudience' | 'targetDepartmentName' | 'targetFacultyName' | 'targetSectionInfo'>): string {
  switch (item.targetAudience) {
    case 'Department':
      return item.targetDepartmentName ? `Bölüm: ${item.targetDepartmentName}` : 'Bölüm';
    case 'DepartmentStudents':
      return item.targetDepartmentName ? `${item.targetDepartmentName} — Öğrenciler` : 'Bölüm Öğrencileri';
    case 'DepartmentInstructors':
      return item.targetDepartmentName ? `${item.targetDepartmentName} — Öğretim Elemanları` : 'Bölüm Öğretim Elemanları';
    case 'Faculty':
      return item.targetFacultyName ? `Fakülte: ${item.targetFacultyName}` : 'Fakülte';
    case 'FacultyStudents':
      return item.targetFacultyName ? `${item.targetFacultyName} — Öğrenciler` : 'Fakülte Öğrencileri';
    case 'FacultyInstructors':
      return item.targetFacultyName ? `${item.targetFacultyName} — Öğretim Elemanları` : 'Fakülte Öğretim Elemanları';
    case 'FacultyManagement':
      return item.targetFacultyName ? `${item.targetFacultyName} — Bölüm Başkanları` : 'Fakülte Yönetimi';
    case 'CourseSection':
      return item.targetSectionInfo ?? 'Ders Şubesi';
    default:
      return ANNOUNCEMENT_AUDIENCE_LABELS[item.targetAudience];
  }
}
