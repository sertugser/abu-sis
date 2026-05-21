import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { userScopeApi } from '../api/userScope.api';
import type { SetUserScopeDto } from '../api/userScope.api';

export function useUserScope(userId: number | null) {
  return useQuery({
    queryKey: ['admin', 'user-scope', userId],
    queryFn: () => userScopeApi.get(userId!),
    enabled: userId !== null,
  });
}

export function useSetUserScope(userId: number | null) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (dto: SetUserScopeDto) => userScopeApi.set(userId!, dto),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['admin', 'user-scope', userId] });
    },
  });
}

export function useClearUserScope(userId: number | null) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: () => userScopeApi.clear(userId!),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['admin', 'user-scope', userId] });
    },
  });
}
