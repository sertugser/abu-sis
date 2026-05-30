import { useQuery } from '@tanstack/react-query';
import {
  getDepartmentSchedule,
  getDepartmentScheduleContext,
  type DepartmentScheduleQuery,
} from '@/api/departmentSchedule.api';

export function useDepartmentScheduleContext() {
  return useQuery({
    queryKey: ['department-schedule-context'],
    queryFn: getDepartmentScheduleContext,
    staleTime: 10 * 60_000,
  });
}

export function useDepartmentSchedule(query: DepartmentScheduleQuery | null) {
  return useQuery({
    queryKey: ['department-schedule', query],
    queryFn: () => getDepartmentSchedule(query as DepartmentScheduleQuery),
    enabled: query !== null,
    staleTime: 5 * 60_000,
  });
}
