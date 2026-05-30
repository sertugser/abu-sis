import { useQuery } from '@tanstack/react-query';
import { getAdvisorRegistrationCatalog } from '../api/advisor.api';

/**
 * Danışmanın "öğrenci adına" katalog görünümü — student-side useStudentRegistration'ın
 * advisor karşılığı. Backend'de aynı veri (Faz 2 B6), sahiplik kontrolü sunucu tarafında.
 */
export function useAdvisorRegistrationCatalog(studentId: number | null) {
  return useQuery({
    queryKey: ['advisor', 'registrationCatalog', studentId],
    queryFn: () => getAdvisorRegistrationCatalog(studentId!),
    enabled: studentId !== null,
    staleTime: 2 * 60 * 1000,
  });
}
