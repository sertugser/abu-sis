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

import type { AnnouncementCategorySummary } from './announcementCategories';

// ── İçerik formatı (backend ContentFormat ile eşleşmeli) ─────────────────────
export type AnnouncementContentFormat = 'plain' | 'html';

// ── Announcement DTOs ─────────────────────────────────────────────────────────

export interface AnnouncementAttachmentSummary {
  id: number;
  fileName: string;
  mimeType: string;
  sizeBytes: number;
  uploadedAt: string;
}

export interface AnnouncementList {
  id: number;
  title: string;
  contentPreview: string;
  contentFormat: AnnouncementContentFormat;
  targetAudience: AnnouncementAudience;
  targetDepartmentId: number | null;
  targetDepartmentName: string | null;
  targetFacultyId: number | null;
  targetFacultyName: string | null;
  targetSectionId: number | null;
  targetSectionInfo: string | null;
  targetStudentYear: number | null;
  category: AnnouncementCategorySummary | null;
  isPinned: boolean;
  isPopup: boolean;
  requiresAcknowledgement: boolean;
  startsAt: string;
  expiresAt: string | null;
  createdByName: string;
  createdAt: string;
  isActive: boolean;
  isReadByMe: boolean;
  attachmentCount: number;
}

export interface AnnouncementDetail {
  id: number;
  title: string;
  content: string;
  contentFormat: AnnouncementContentFormat;
  targetAudience: AnnouncementAudience;
  targetDepartmentId: number | null;
  targetDepartmentName: string | null;
  targetFacultyId: number | null;
  targetFacultyName: string | null;
  targetSectionId: number | null;
  targetSectionInfo: string | null;
  targetStudentYear: number | null;
  category: AnnouncementCategorySummary | null;
  isPinned: boolean;
  isPopup: boolean;
  requiresAcknowledgement: boolean;
  startsAt: string;
  expiresAt: string | null;
  createdByName: string;
  createdAt: string;
  updatedAt: string | null;
  isActive: boolean;
  isReadByMe: boolean;
  attachments: AnnouncementAttachmentSummary[];
}

export interface CreateAnnouncementPayload {
  title: string;
  content: string;
  contentFormat: AnnouncementContentFormat;
  targetAudience: AnnouncementAudience;
  targetDepartmentId: number | null;
  targetFacultyId: number | null;
  targetSectionId: number | null;
  targetStudentYear: number | null;
  categoryId: number | null;
  isPinned: boolean;
  isPopup: boolean;
  requiresAcknowledgement: boolean;
  startsAt: string | null;
  expiresAt: string | null;
}

export interface UpdateAnnouncementPayload {
  title: string;
  content: string;
  contentFormat: AnnouncementContentFormat;
  targetAudience: AnnouncementAudience;
  targetDepartmentId: number | null;
  targetFacultyId: number | null;
  targetSectionId: number | null;
  targetStudentYear: number | null;
  categoryId: number | null;
  isPinned: boolean;
  isPopup: boolean;
  requiresAcknowledgement: boolean;
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

export interface AnnouncementSettings {
  maxTitleLength: number;
  maxContentLength: number;
  studentTopCount: number;
  pinnedFirst: boolean;
  allowImagesInContent: boolean;
  allowTablesInContent: boolean;
  studentYearFilterMin: number;
  studentYearFilterMax: number;
  attachmentsEnabled: boolean;
  attachmentMaxSizeMB: number;
  attachmentMaxCount: number;
}

// Sınıf filtresi destekleyen audience'lar (backend ile eşleşmeli)
export const STUDENT_YEAR_FILTERABLE_AUDIENCES: AnnouncementAudience[] = [
  'Students',
  'DepartmentStudents',
  'FacultyStudents',
];

export function audienceSupportsStudentYearFilter(audience: AnnouncementAudience): boolean {
  return STUDENT_YEAR_FILTERABLE_AUDIENCES.includes(audience);
}

export interface AnnouncementCreateContext {
  senderRole: 'Instructor' | 'Dean' | 'DepartmentManager' | 'Admin';
  allowedAudiences: AnnouncementAudience[];
  departmentId: number | null;
  departmentName: string | null;
  facultyId: number | null;
  facultyName: string | null;
  sections: AnnouncementSectionOption[];
  settings: AnnouncementSettings;
}

// ── Audience badge helper ─────────────────────────────────────────────────────

type AudienceBadgeItem = Pick<
  AnnouncementList,
  'targetAudience' | 'targetDepartmentName' | 'targetFacultyName' | 'targetSectionInfo' | 'targetStudentYear'
>;

export function getAudienceBadgeLabel(item: AudienceBadgeItem): string {
  const base = formatAudienceBase(item);
  if (item.targetStudentYear && audienceSupportsStudentYearFilter(item.targetAudience)) {
    return `${base} · ${item.targetStudentYear}. Sınıf`;
  }
  return base;
}

function formatAudienceBase(item: AudienceBadgeItem): string {
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
