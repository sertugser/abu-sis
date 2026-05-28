import { useMutation } from '@tanstack/react-query';
import { validateRegistration } from '../api/registrationCatalog.api';

/**
 * "Kontrol Et" — backend `POST /registrations/validate` (read-only).
 * Mutate hatasında bile state değiştirmez; her zaman ValidationResultDto döner.
 */
export function useValidateRegistration() {
  return useMutation({
    mutationFn: (courseSectionIds: number[]) => validateRegistration(courseSectionIds),
  });
}
