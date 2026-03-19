import { useMutation, useQueryClient } from '@tanstack/react-query';
import { withdrawCourse } from '../api/registration.api';
import { QUERY_KEYS } from '../lib/constants';

export function useWithdrawCourse() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (registrationId: number) => withdrawCourse(registrationId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.myRegistrations });
    },
  });
}
