import { FileSpreadsheet, FileText, Loader2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import type { ReportRunResult } from '@/api/reportRun.api';

interface ReportResultTableProps {
  result: ReportRunResult;
  onExport: (format: 'xlsx' | 'pdf') => void;
  exporting: boolean;
}

export function ReportResultTable({ result, onExport, exporting }: ReportResultTableProps) {
  return (
    <div className="space-y-3">
      <div className="flex items-center justify-between">
        <p className="text-sm text-muted-foreground">Toplam <strong>{result.totalCount}</strong> kayıt</p>
        <div className="flex gap-2">
          <Button variant="outline" size="sm" onClick={() => onExport('xlsx')} disabled={exporting}>
            {exporting ? <Loader2 className="h-4 w-4 animate-spin" /> : <FileSpreadsheet className="h-4 w-4" />}
            Excel
          </Button>
          <Button variant="outline" size="sm" onClick={() => onExport('pdf')} disabled={exporting}>
            {exporting ? <Loader2 className="h-4 w-4 animate-spin" /> : <FileText className="h-4 w-4" />}
            PDF
          </Button>
        </div>
      </div>
      <div className="overflow-auto rounded-md border">
        <Table>
          <TableHeader>
            <TableRow>
              {result.columns.map((c) => (
                <TableHead key={c.key} className={c.align === 'Right' ? 'text-right' : undefined}>{c.label}</TableHead>
              ))}
            </TableRow>
          </TableHeader>
          <TableBody>
            {result.rows.length === 0 ? (
              <TableRow>
                <TableCell colSpan={result.columns.length} className="py-8 text-center text-muted-foreground">
                  Kayıt bulunamadı.
                </TableCell>
              </TableRow>
            ) : (
              result.rows.map((row, ri) => (
                <TableRow key={ri}>
                  {row.map((cell, ci) => (
                    <TableCell key={ci} className={result.columns[ci]?.align === 'Right' ? 'text-right tabular-nums' : undefined}>
                      {cell === null ? '—' : String(cell)}
                    </TableCell>
                  ))}
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>
      </div>
    </div>
  );
}
