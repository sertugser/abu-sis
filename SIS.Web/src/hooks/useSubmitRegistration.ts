import { useMutation, useQueryClient } from '@tanstack/react-query';
import { submitRegistration } from '../api/registration.api';
import { QUERY_KEYS } from '../lib/constants';

export function useSubmitRegistration() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (courseSectionIds: number[]) => submitRegistration(courseSectionIds),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.studentRegistrationCatalog });
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.studentCatalog });
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.myRegistrations });
      queryClient.invalidateQueries({ queryKey: ['courseHistory'] });
    },
  });
}
