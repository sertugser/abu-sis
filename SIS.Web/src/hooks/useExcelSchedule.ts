import { useMutation, useQueryClient } from '@tanstack/react-query';
import { exportScheduleExcel, exportSchedulePdf, importScheduleApply, importSchedulePreview } from '../api/scheduleExcel.api';
import type { ImportPreviewDto } from '../types/scheduleExcel';

export function useExportExcel() {
  return useMutation({
    mutationFn: ({ facultyId, termId }: { facultyId: number; termId: number }) =>
      exportScheduleExcel(facultyId, termId),
    onSuccess: (blob) => {
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = 'schedule-export.xlsx';
      a.click();
      URL.revokeObjectURL(url);
    },
  });
}

export function useExportPdf() {
  return useMutation({
    mutationFn: ({ termId, facultyId, departmentId }: { termId: number; facultyId: number; departmentId?: number }) =>
      exportSchedulePdf(termId, facultyId, departmentId),
    onSuccess: (blob) => {
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = 'ders-programi.pdf';
      a.click();
      URL.revokeObjectURL(url);
    },
  });
}

export function useImportPreview() {
  return useMutation({
    mutationFn: ({
      facultyId,
      termId,
      file,
    }: {
      facultyId: number;
      termId: number;
      file: File;
    }) => importSchedulePreview(facultyId, termId, file),
  });
}

export function useImportApply() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({
      facultyId,
      termId,
      rows,
    }: {
      facultyId: number;
      termId: number;
      rows: ImportPreviewDto['rows'];
    }) => importScheduleApply(facultyId, termId, rows),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['facultyPlanning'] });
      qc.invalidateQueries({ queryKey: ['facultyPlanningConflicts'] });
    },
  });
}
