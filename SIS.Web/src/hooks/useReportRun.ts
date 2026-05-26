import { useMutation, useQuery } from '@tanstack/react-query';
import { reportRunApi, type ReportRunRequest } from '@/api/reportRun.api';

export function useReportSchema(code: string | null) {
  return useQuery({
    queryKey: ['report-schema', code],
    queryFn: () => reportRunApi.getSchema(code!),
    enabled: !!code,
    staleTime: 5 * 60_000,
  });
}

export function useRunReport(code: string) {
  return useMutation({
    mutationFn: (req: ReportRunRequest) => reportRunApi.run(code, req),
  });
}

export function useExportReport(code: string) {
  return useMutation({
    mutationFn: ({ format, req }: { format: 'xlsx' | 'pdf'; req: ReportRunRequest }) =>
      reportRunApi.export(code, format, req),
    onSuccess: (blob, { format }) => {
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `${code}.${format}`;
      a.click();
      URL.revokeObjectURL(url);
    },
  });
}
