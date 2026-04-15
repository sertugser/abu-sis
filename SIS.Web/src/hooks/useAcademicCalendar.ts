import { useQuery } from '@tanstack/react-query';
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
      const { data } = await apiClient.get<ActiveTermCalendarDto>('/academic-calendar/active');
      return data;
    },
    staleTime: 10 * 60 * 1000,
  });
}
