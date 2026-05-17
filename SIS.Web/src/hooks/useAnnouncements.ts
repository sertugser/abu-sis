import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  acknowledgeAnnouncement,
  createAnnouncement,
  deleteAnnouncement,
  deleteAnnouncementAttachment,
  permanentDeleteAnnouncement,
  reactivateAnnouncement,
  getActiveAnnouncements,
  getAllAnnouncements,
  getAnnouncementById,
  getAnnouncementCreateContext,
  getAnnouncementStats,
  getPendingPopupAnnouncements,
  getUnreadAnnouncementCount,
  markAnnouncementAsRead,
  markAllAnnouncementsAsRead,
  updateAnnouncement,
  uploadAnnouncementAttachment,
} from '../api/announcements.api';
import { QUERY_KEYS } from '../lib/constants';
import type { CreateAnnouncementPayload, UpdateAnnouncementPayload } from '../types/announcements';

export function useActiveAnnouncements(max?: number, enabled = true) {
  return useQuery({
    queryKey: QUERY_KEYS.activeAnnouncements(max),
    queryFn: () => getActiveAnnouncements(max),
    staleTime: 60 * 1000,
    enabled,
  });
}

export function useAnnouncementDetail(id: number | null, enabled = true) {
  return useQuery({
    queryKey: QUERY_KEYS.announcementDetail(id ?? 0),
    queryFn: () => getAnnouncementById(id!),
    enabled: enabled && id != null && id > 0,
    staleTime: 60 * 1000,
  });
}

export function useAllAnnouncements(enabled = true) {
  return useQuery({
    queryKey: QUERY_KEYS.allAnnouncements,
    queryFn: getAllAnnouncements,
    staleTime: 30 * 1000,
    enabled,
  });
}

export function useAnnouncementCreateContext(enabled = true) {
  return useQuery({
    queryKey: ['announcements', 'create-context'],
    queryFn: getAnnouncementCreateContext,
    staleTime: 5 * 60 * 1000,
    enabled,
  });
}

function invalidateAnnouncementLists(qc: ReturnType<typeof useQueryClient>) {
  qc.invalidateQueries({ queryKey: ['announcements'] });
}

export function useCreateAnnouncement() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (payload: CreateAnnouncementPayload) => createAnnouncement(payload),
    onSuccess: () => invalidateAnnouncementLists(qc),
  });
}

export function useUpdateAnnouncement() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, payload }: { id: number; payload: UpdateAnnouncementPayload }) =>
      updateAnnouncement(id, payload),
    onSuccess: () => invalidateAnnouncementLists(qc),
  });
}

export function useDeleteAnnouncement() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: number) => deleteAnnouncement(id),
    onSuccess: () => invalidateAnnouncementLists(qc),
  });
}

export function usePermanentDeleteAnnouncement() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: number) => permanentDeleteAnnouncement(id),
    onSuccess: () => invalidateAnnouncementLists(qc),
  });
}

export function useReactivateAnnouncement() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: number) => reactivateAnnouncement(id),
    onSuccess: () => invalidateAnnouncementLists(qc),
  });
}

// ── Read tracking ────────────────────────────────────────────────────────────

export function useUnreadAnnouncementCount(enabled = true, refetchIntervalMs?: number) {
  return useQuery({
    queryKey: ['announcements', 'unread-count'],
    queryFn: () => getUnreadAnnouncementCount().then((r) => r.unreadCount),
    enabled,
    staleTime: 30 * 1000,
    refetchInterval: refetchIntervalMs,
  });
}

export function useMarkAnnouncementAsRead() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: number) => markAnnouncementAsRead(id),
    onSuccess: () => {
      // Tüm duyuru listelerini ve okunmamış sayacını taze tut
      qc.invalidateQueries({ queryKey: ['announcements'] });
    },
  });
}

export function useMarkAllAnnouncementsAsRead() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: () => markAllAnnouncementsAsRead(),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['announcements'] });
    },
  });
}

// ── Attachments ──────────────────────────────────────────────────────────────

export function useUploadAnnouncementAttachment() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ announcementId, file, onProgress }: {
      announcementId: number;
      file: File;
      onProgress?: (percent: number) => void;
    }) => uploadAnnouncementAttachment(announcementId, file, onProgress),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['announcements'] });
    },
  });
}

// ── İstatistik ───────────────────────────────────────────────────────────────

export function useAnnouncementStats(id: number | null, enabled = true) {
  return useQuery({
    queryKey: ['announcements', 'stats', id ?? 0],
    queryFn: () => getAnnouncementStats(id!),
    enabled: enabled && id != null && id > 0,
    staleTime: 30 * 1000,
  });
}

// ── Pop-up duyurular ─────────────────────────────────────────────────────────

export function usePendingPopupAnnouncements(enabled = true) {
  return useQuery({
    queryKey: ['announcements', 'popup-pending'],
    queryFn: getPendingPopupAnnouncements,
    enabled,
    staleTime: 60 * 1000,
  });
}

export function useAcknowledgeAnnouncement() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: number) => acknowledgeAnnouncement(id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['announcements'] });
    },
  });
}

export function useDeleteAnnouncementAttachment() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ announcementId, attachmentId }: {
      announcementId: number;
      attachmentId: number;
    }) => deleteAnnouncementAttachment(announcementId, attachmentId),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['announcements'] });
    },
  });
}
