import apiClient from './axios';

export interface AccessibleReportDto {
  id: number;
  code: string;
  name: string;
  categoryId: number;
  categoryName: string;
}

export const myReportsApi = {
  list: async (): Promise<AccessibleReportDto[]> => {
    const { data } = await apiClient.get<AccessibleReportDto[]>('/me/reports');
    return data;
  },
};
