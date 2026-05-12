import apiClient from './axios';
import type {
  AnnouncementCategory,
  CreateAnnouncementCategoryPayload,
  UpdateAnnouncementCategoryPayload,
} from '../types/announcementCategories';

const BASE = '/admin/announcement-categories';

export async function getAnnouncementCategories(): Promise<AnnouncementCategory[]> {
  const { data } = await apiClient.get<AnnouncementCategory[]>(BASE);
  return data;
}

export async function createAnnouncementCategory(
  payload: CreateAnnouncementCategoryPayload,
): Promise<AnnouncementCategory> {
  const { data } = await apiClient.post<AnnouncementCategory>(BASE, payload);
  return data;
}

export async function updateAnnouncementCategory(
  id: number,
  payload: UpdateAnnouncementCategoryPayload,
): Promise<AnnouncementCategory> {
  const { data } = await apiClient.put<AnnouncementCategory>(`${BASE}/${id}`, payload);
  return data;
}

export async function deleteAnnouncementCategory(id: number): Promise<void> {
  await apiClient.delete(`${BASE}/${id}`);
}
