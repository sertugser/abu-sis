import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  getConstraintProfiles,
  getConstraintProfileDetail,
  createProfileFromCurrent,
  applyProfile,
  deleteProfile,
} from '../api/constraintProfile.api';
import { QUERY_KEYS } from '../lib/constants';

const PROFILE_KEY = [...QUERY_KEYS.constraints, 'profiles'] as const;

export function useConstraintProfiles() {
  return useQuery({
    queryKey: PROFILE_KEY,
    queryFn: getConstraintProfiles,
    staleTime: 60 * 1000,
  });
}

export function useConstraintProfileDetail(id: number | null) {
  return useQuery({
    queryKey: [...PROFILE_KEY, id],
    queryFn: () => getConstraintProfileDetail(id!),
    enabled: id != null,
    staleTime: 30 * 1000,
  });
}

export function useCreateProfileFromCurrent() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ name, description }: { name: string; description?: string }) =>
      createProfileFromCurrent(name, description),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: PROFILE_KEY });
    },
  });
}

export function useApplyProfile() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: number) => applyProfile(id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: QUERY_KEYS.constraints });
      qc.invalidateQueries({ queryKey: PROFILE_KEY });
    },
  });
}

export function useDeleteProfile() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: number) => deleteProfile(id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: PROFILE_KEY });
    },
  });
}
