import apiClient from './axios';

export type ReportParameterType =
  | 'Text' | 'Int' | 'Decimal' | 'Bool' | 'Date' | 'Enum'
  | 'Term' | 'Faculty' | 'Department' | 'Program';

export interface ReportParameterOption { value: string; label: string }
export interface ReportParameter {
  key: string;
  label: string;
  type: ReportParameterType;
  required: boolean;
  options: ReportParameterOption[] | null;
}
export interface ReportParameterSchema { parameters: ReportParameter[] }

export type ReportColumnAlign = 'Left' | 'Right' | 'Center';
export interface ReportColumn { key: string; label: string; align: ReportColumnAlign }

export interface ReportRunRequest {
  parameters: Record<string, string | null>;
  page: number;
  pageSize: number;
  sortKey?: string | null;
  sortDescending?: boolean;
}
export interface ReportRunResult {
  columns: ReportColumn[];
  rows: (string | number | boolean | null)[][];
  totalCount: number;
  page: number;
  pageSize: number;
}

export const reportRunApi = {
  getSchema: async (code: string): Promise<ReportParameterSchema> => {
    const { data } = await apiClient.get<ReportParameterSchema>(`/me/reports/${code}/schema`);
    return data;
  },
  run: async (code: string, req: ReportRunRequest): Promise<ReportRunResult> => {
    const { data } = await apiClient.post<ReportRunResult>(`/me/reports/${code}/run`, req);
    return data;
  },
  export: async (code: string, format: 'xlsx' | 'pdf', req: ReportRunRequest): Promise<Blob> => {
    const { data } = await apiClient.post(`/me/reports/${code}/export?format=${format}`, req, {
      responseType: 'blob',
    });
    return data as Blob;
  },
};
