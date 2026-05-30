import { useMutation, useQueryClient } from '@tanstack/react-query';
import { dropCourse } from '../api/registration.api';
import { QUERY_KEYS } from '../lib/constants';

export function useDropCourse() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (registrationId: number) => dropCourse(registrationId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.studentRegistrationCatalog });
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.myRegistrations });
      queryClient.invalidateQueries({ queryKey: ['sectionPicker'] }); // kontenjan tazelensin
      queryClient.invalidateQueries({ queryKey: ['courseHistory'] });
    },
  });
}
