import apiClient from './axios';
import type {
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

export async function createAnnouncement(
  payload: CreateAnnouncementPayload
): Promise<AnnouncementDetail> {
  const { data } = await apiClient.post<AnnouncementDetail>('/internal-announcements', payload);
  return data;
}

export async function updateAnnouncement(
  id: number,
  payload: UpdateAnnouncementPayload
): Promise<AnnouncementDetail> {
  const { data } = await apiClient.put<AnnouncementDetail>(
    `/internal-announcements/${id}`,
    payload
  );
  return data;
}

export async function deleteAnnouncement(id: number): Promise<void> {
  await apiClient.delete(`/internal-announcements/${id}`);
}
