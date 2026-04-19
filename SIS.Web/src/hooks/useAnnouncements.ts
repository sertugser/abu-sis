import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  createAnnouncement,
  deleteAnnouncement,
  getActiveAnnouncements,
  getAllAnnouncements,
  getAnnouncementById,
  updateAnnouncement,
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
