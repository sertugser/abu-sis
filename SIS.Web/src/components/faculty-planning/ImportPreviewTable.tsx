import { useState } from 'react';
import {
  Table, TableBody, TableHead, TableHeader, TableRow, TableCell,
} from '@/components/ui/table';
import type { ImportRowDto } from '../../types/scheduleExcel';

type FilterMode = 'all' | 'error' | 'warning' | 'valid';

const STATUS_ICON: Record<ImportRowDto['status'], string> = {
  Valid: '✅',
  Warning: '⚠',
  Error: '❌',
};

const ROW_BG: Record<ImportRowDto['status'], string> = {
  Valid: '',
  Warning: 'bg-amber-50',
  Error: 'bg-red-50',
};

const FILTER_OPTIONS: { value: FilterMode; label: string }[] = [
  { value: 'all', label: 'Tümünü Göster' },
  { value: 'error', label: 'Sadece Hatalar' },
  { value: 'warning', label: 'Sadece Uyarılar' },
  { value: 'valid', label: 'Sadece Geçerliler' },
];

const STATUS_BY_FILTER: Record<FilterMode, ImportRowDto['status'] | null> = {
  all: null,
  error: 'Error',
  warning: 'Warning',
  valid: 'Valid',
};

interface ImportPreviewTableProps {
  rows: ImportRowDto[];
}

export function ImportPreviewTable({ rows }: ImportPreviewTableProps) {
  const [filterMode, setFilterMode] = useState<FilterMode>('all');

  const filtered = filterMode === 'all'
    ? rows
    : rows.filter(r => r.status === STATUS_BY_FILTER[filterMode]);

  return (
    <div className="space-y-3">
      <div className="flex items-center gap-3">
        <select
          className="h-8 rounded-md border border-slate-200 bg-white px-3 text-sm text-slate-700"
          value={filterMode}
          onChange={e => setFilterMode(e.target.value as FilterMode)}
        >
          {FILTER_OPTIONS.map(o => (
            <option key={o.value} value={o.value}>{o.label}</option>
          ))}
        </select>
        <span className="text-sm text-slate-500">{filtered.length} satır</span>
      </div>

      <div className="max-h-80 overflow-y-auto border border-slate-200 rounded-lg bg-white">
        <Table>
          <TableHeader>
            <TableRow className="border-b-2 border-slate-100">
              <TableHead className="text-xs font-semibold text-slate-500 py-2 pl-3 w-10"></TableHead>
              <TableHead className="text-xs font-semibold text-slate-500 py-2">DERS</TableHead>
              <TableHead className="text-xs font-semibold text-slate-500 py-2">ŞUBE</TableHead>
              <TableHead className="text-xs font-semibold text-slate-500 py-2">GÜN</TableHead>
              <TableHead className="text-xs font-semibold text-slate-500 py-2">SAAT</TableHead>
              <TableHead className="text-xs font-semibold text-slate-500 py-2">DERSLİK</TableHead>
              <TableHead className="text-xs font-semibold text-slate-500 py-2">MESAJ</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {filtered.length === 0 ? (
              <TableRow>
                <TableCell colSpan={7} className="py-10 text-center text-slate-400 text-sm">
                  Eşleşen satır bulunamadı.
                </TableCell>
              </TableRow>
            ) : (
              filtered.map(row => (
                <TableRow
                  key={row.rowNumber}
                  className={`border-b border-slate-100 ${ROW_BG[row.status]}`}
                >
                  <TableCell className="py-2 pl-3 text-base leading-none">
                    {STATUS_ICON[row.status]}
                  </TableCell>
                  <TableCell className="py-2 text-sm font-medium">{row.courseCode}</TableCell>
                  <TableCell className="py-2 text-sm">{row.sectionCode}</TableCell>
                  <TableCell className="py-2 text-sm">{row.day}</TableCell>
                  <TableCell className="py-2 text-sm tabular-nums">
                    {row.startTime} – {row.endTime}
                  </TableCell>
                  <TableCell className="py-2 text-sm">{row.classroomName}</TableCell>
                  <TableCell className="py-2 text-sm text-slate-600">{row.message}</TableCell>
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>
      </div>
    </div>
  );
}
