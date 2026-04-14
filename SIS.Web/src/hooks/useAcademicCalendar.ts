import { useQuery } from '@tanstack/react-query';
import { isAxiosError } from 'axios';
import apiClient from '../api/axios';

export interface PublicCalendarEventDto {
  id: number;
  eventType: string;
  eventTypeLabel: string;
  title: string;
  startDate: string;
  endDate: string | null;
  note: string | null;
}

export interface ActiveTermCalendarDto {
  termId: number;
  termName: string;
  shortName: string;
  englishName: string;
  events: PublicCalendarEventDto[];
}

export function useAcademicCalendar() {
  return useQuery({
    queryKey: ['academic-calendar', 'active'],
    queryFn: async () => {
      try {
        const { data } = await apiClient.get<ActiveTermCalendarDto>('/academic-calendar/active');
        return data;
      } catch (error) {
        if (isAxiosError(error) && error.response?.status === 404) {
          return null;
        }
        throw error;
      }
    },
    staleTime: 0,
    refetchOnMount: 'always',
  });
}
