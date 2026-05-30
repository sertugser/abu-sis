import { useMutation, useQueryClient } from '@tanstack/react-query';
import { addToPlan } from '../api/registration.api';
import { QUERY_KEYS } from '../lib/constants';

export function useAddToPlan() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (courseSectionId: number) => addToPlan(courseSectionId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.studentRegistrationCatalog });
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.myRegistrations });
      queryClient.invalidateQueries({ queryKey: ['sectionPicker'] }); // kontenjan tazelensin
      queryClient.invalidateQueries({ queryKey: ['courseHistory'] });
    },
  });
}
