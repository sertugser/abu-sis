import { useQuery } from '@tanstack/react-query';
import { getDepartmentForecastSummary } from '../api/forecast.api';
import { QUERY_KEYS } from '../lib/constants';

export function useDepartmentForecast(departmentId: number | null) {
  return useQuery({
    queryKey: QUERY_KEYS.forecastDepartmentSummary(departmentId ?? 0),
    queryFn: () => getDepartmentForecastSummary(departmentId!),
    enabled: departmentId !== null && departmentId > 0,
    staleTime: 2 * 60 * 1000,
  });
}
