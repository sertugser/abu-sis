import { useQuery } from '@tanstack/react-query';
import { courseLookupsApi } from '../api/courseLookups.api';

export function useOpeningReasons() {
  return useQuery({
    queryKey: ['courseLookups', 'openingReasons'],
    queryFn: courseLookupsApi.getOpeningReasons,
    staleTime: 5 * 60 * 1000,
  });
}

export function useCourseCategories() {
  return useQuery({
    queryKey: ['courseLookups', 'categories'],
    queryFn: courseLookupsApi.getCategories,
    staleTime: 5 * 60 * 1000,
  });
}
