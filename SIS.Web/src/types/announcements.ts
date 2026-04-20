export type AnnouncementAudience = 'All' | 'Students' | 'Instructors' | 'Department' | 'Management';

export const ANNOUNCEMENT_AUDIENCE_LABELS: Record<AnnouncementAudience, string> = {
  All: 'Herkes',
  Students: 'Öğrenciler',
  Instructors: 'Öğretim Elemanları',
  Department: 'Bölüm',
  Management: 'Yönetim',
};

export interface AnnouncementList {
  id: number;
  title: string;
  contentPreview: string;
  targetAudience: AnnouncementAudience;
  targetDepartmentId: number | null;
  targetDepartmentName: string | null;
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
  isPinned: boolean;
  startsAt: string | null;
  expiresAt: string | null;
}

export interface UpdateAnnouncementPayload {
  title: string;
  content: string;
  targetAudience: AnnouncementAudience;
  targetDepartmentId: number | null;
  isPinned: boolean;
  startsAt: string;
  expiresAt: string | null;
}
