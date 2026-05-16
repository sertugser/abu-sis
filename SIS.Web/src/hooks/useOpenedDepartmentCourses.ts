import { useQuery } from '@tanstack/react-query';
import { getOpenedDepartmentCourses } from '@/api/openedDepartmentCourses.api';

export const useOpenedDepartmentCourses = () =>
  useQuery({
    queryKey: ['opened-department-courses', 'me'],
    queryFn: getOpenedDepartmentCourses,
    staleTime: 5 * 60_000,
  });
