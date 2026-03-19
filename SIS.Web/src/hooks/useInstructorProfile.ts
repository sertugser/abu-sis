import { useQuery } from '@tanstack/react-query';
import { getInstructorProfile } from '../api/instructor.api';

export function useInstructorProfile() {
  return useQuery({
    queryKey: ['instructorProfile'],
    queryFn: getInstructorProfile,
    staleTime: 5 * 60 * 1000,
  });
}
