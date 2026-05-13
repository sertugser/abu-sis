import apiClient from './axios';
import type {
  AnnouncementCreateContext,
  AnnouncementDetail,
  AnnouncementList,
  AnnouncementAttachmentSummary,
  CreateAnnouncementPayload,
  UpdateAnnouncementPayload,
} from '../types/announcements';
import type { AnnouncementStats } from '../types/announcementStats';

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

// ── Pop-up duyurular ─────────────────────────────────────────────────────────

export async function getPendingPopupAnnouncements(): Promise<AnnouncementDetail[]> {
  const { data } = await apiClient.get<AnnouncementDetail[]>(
    '/internal-announcements/popup-pending',
  );
  return data;
}

export async function acknowledgeAnnouncement(id: number): Promise<{ acknowledged: boolean }> {
  const { data } = await apiClient.post<{ acknowledged: boolean }>(
    `/internal-announcements/${id}/acknowledge`,
  );
  return data;
}

export async function getAnnouncementStats(id: number): Promise<AnnouncementStats> {
  const { data } = await apiClient.get<AnnouncementStats>(`/internal-announcements/${id}/stats`);
  return data;
}

// ── Attachments ──────────────────────────────────────────────────────────────

export async function uploadAnnouncementAttachment(
  announcementId: number,
  file: File,
  onProgress?: (percent: number) => void,
): Promise<AnnouncementAttachmentSummary> {
  const formData = new FormData();
  formData.append('file', file, file.name);

  const { data } = await apiClient.post<AnnouncementAttachmentSummary>(
    `/internal-announcements/${announcementId}/attachments`,
    formData,
    {
      headers: { 'Content-Type': 'multipart/form-data' },
      onUploadProgress: (e) => {
        if (onProgress && e.total) {
          onProgress(Math.round((e.loaded * 100) / e.total));
        }
      },
    },
  );
  return data;
}

export async function deleteAnnouncementAttachment(
  announcementId: number,
  attachmentId: number,
): Promise<void> {
  await apiClient.delete(
    `/internal-announcements/${announcementId}/attachments/${attachmentId}`,
  );
}

/**
 * Eki indirir: Bearer auth ile blob fetch + anchor click ile tarayıcıyı tetikler.
 * Content-Disposition'daki orijinal dosya adı kullanılır; yoksa fallback ad döner.
 */
export async function downloadAnnouncementAttachment(
  announcementId: number,
  attachmentId: number,
  fallbackFileName: string,
): Promise<void> {
  const response = await apiClient.get<Blob>(
    `/internal-announcements/${announcementId}/attachments/${attachmentId}/download`,
    { responseType: 'blob' },
  );

  // Content-Disposition header'ından dosya adını çek (varsa)
  const disposition = response.headers['content-disposition'] as string | undefined;
  const fileName = parseContentDispositionFileName(disposition) ?? fallbackFileName;

  const objectUrl = URL.createObjectURL(response.data);
  try {
    const anchor = document.createElement('a');
    anchor.href = objectUrl;
    anchor.download = fileName;
    document.body.appendChild(anchor);
    anchor.click();
    document.body.removeChild(anchor);
  } finally {
    // ObjectURL'i hemen revoke etmek bazı tarayıcılarda indirme başlamadan iptal eder.
    // Kısa bir gecikme ile temizle.
    setTimeout(() => URL.revokeObjectURL(objectUrl), 1000);
  }
}

function parseContentDispositionFileName(header: string | undefined): string | null {
  if (!header) return null;
  // RFC 5987 (filename*=UTF-8'') öncelikli
  const utf8Match = /filename\*=UTF-8''([^;]+)/i.exec(header);
  if (utf8Match) {
    try {
      return decodeURIComponent(utf8Match[1].trim());
    } catch {
      // ignore decode error, fall back to ASCII variant
    }
  }
  const asciiMatch = /filename="?([^";]+)"?/i.exec(header);
  return asciiMatch ? asciiMatch[1].trim() : null;
}
