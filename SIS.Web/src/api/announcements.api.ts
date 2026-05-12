import apiClient from './axios';
import type {
  AnnouncementCreateContext,
  AnnouncementDetail,
  AnnouncementList,
  CreateAnnouncementPayload,
  UpdateAnnouncementPayload,
} from '../types/announcements';

export async function getActiveAnnouncements(max?: number): Promise<AnnouncementList[]> {
  const { data } = await apiClient.get<AnnouncementList[]>('/internal-announcements/active', {
    params: max ? { max } : undefined,
  });
  return data;
}

export async function getAnnouncementById(id: number): Promise<AnnouncementDetail> {
  const { data } = await apiClient.get<AnnouncementDetail>(`/internal-announcements/${id}`);
  return data;
}

export async function getAllAnnouncements(): Promise<AnnouncementList[]> {
  const { data } = await apiClient.get<AnnouncementList[]>('/internal-announcements/all');
  return data;
}

export async function getAnnouncementCreateContext(): Promise<AnnouncementCreateContext> {
  const { data } = await apiClient.get<AnnouncementCreateContext>('/internal-announcements/create-context');
  return data;
}

export async function createAnnouncement(
  payload: CreateAnnouncementPayload,
): Promise<AnnouncementDetail> {
  const { data } = await apiClient.post<AnnouncementDetail>('/internal-announcements', payload);
  return data;
}

export async function updateAnnouncement(
  id: number,
  payload: UpdateAnnouncementPayload,
): Promise<AnnouncementDetail> {
  const { data } = await apiClient.put<AnnouncementDetail>(
    `/internal-announcements/${id}`,
    payload,
  );
  return data;
}

export async function deleteAnnouncement(id: number): Promise<void> {
  await apiClient.delete(`/internal-announcements/${id}`);
}

export async function permanentDeleteAnnouncement(id: number): Promise<void> {
  await apiClient.delete(`/internal-announcements/${id}/permanent`);
}

export async function reactivateAnnouncement(id: number): Promise<void> {
  await apiClient.post(`/internal-announcements/${id}/reactivate`);
}

// ── Read tracking ────────────────────────────────────────────────────────────

export async function markAnnouncementAsRead(id: number): Promise<{ marked: boolean }> {
  const { data } = await apiClient.post<{ marked: boolean }>(
    `/internal-announcements/${id}/read`,
  );
  return data;
}

export async function markAllAnnouncementsAsRead(): Promise<{ markedCount: number }> {
  const { data } = await apiClient.post<{ markedCount: number }>(
    '/internal-announcements/read-all',
  );
  return data;
}

export async function getUnreadAnnouncementCount(): Promise<{ unreadCount: number }> {
  const { data } = await apiClient.get<{ unreadCount: number }>(
    '/internal-announcements/unread-count',
  );
  return data;
}
