import { useQuery } from '@tanstack/react-query';
import { getRegistrationCatalog } from '../api/registrationCatalog.api';
import { QUERY_KEYS } from '../lib/constants';

/**
 * Faz 3 tek-kaynak katalog hook'u: müfredat-ağacı katalog + öğrenci başlığı + term + retake + havuz + seçilen.
 * Cache key `studentRegistrationCatalog` — ekle/çıkar/submit sonrası bu key invalidate edilmelidir (#5/#20).
 */
export function useStudentRegistration() {
  return useQuery({
    queryKey: QUERY_KEYS.studentRegistrationCatalog,
    queryFn: getRegistrationCatalog,
    staleTime: 2 * 60 * 1000,
  });
}
