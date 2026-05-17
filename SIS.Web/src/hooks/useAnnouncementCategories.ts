import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  createAnnouncementCategory,
  deleteAnnouncementCategory,
  getAnnouncementCategories,
  updateAnnouncementCategory,
} from '../api/announcementCategories.api';
import type {
  CreateAnnouncementCategoryPayload,
  UpdateAnnouncementCategoryPayload,
} from '../types/announcementCategories';

const KEY = ['announcement-categories'] as const;

export function useAnnouncementCategories(enabled = true) {
  return useQuery({
    queryKey: KEY,
    queryFn: getAnnouncementCategories,
    staleTime: 5 * 60 * 1000,
    enabled,
  });
}

export function useCreateAnnouncementCategory() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (payload: CreateAnnouncementCategoryPayload) => createAnnouncementCategory(payload),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: KEY });
      qc.invalidateQueries({ queryKey: ['announcements'] });
    },
  });
}

export function useUpdateAnnouncementCategory() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, payload }: { id: number; payload: UpdateAnnouncementCategoryPayload }) =>
      updateAnnouncementCategory(id, payload),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: KEY });
      qc.invalidateQueries({ queryKey: ['announcements'] });
    },
  });
}

export function useDeleteAnnouncementCategory() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: number) => deleteAnnouncementCategory(id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: KEY });
      qc.invalidateQueries({ queryKey: ['announcements'] });
    },
  });
}
