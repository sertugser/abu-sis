import { AlertCircle, CheckCircle2 } from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import type { BulkAcademicOperationResult } from '@/types/academicOperations';

interface BulkOperationResultPanelProps {
  result: BulkAcademicOperationResult | null;
  title: string;
}

export default function BulkOperationResultPanel({ result, title }: BulkOperationResultPanelProps) {
  if (!result) return null;

  const hasFailures = result.failedCount > 0 || result.errors.length > 0;

  return (
    <Card>
      <CardHeader className="pb-3">
        <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <CardTitle className="text-base">{title}</CardTitle>
          <div className="flex flex-wrap gap-2">
            <Badge variant="outline">İstenen: {result.requestedCount}</Badge>
            <Badge variant="success">Başarılı: {result.successCount}</Badge>
            <Badge variant={hasFailures ? 'warning' : 'secondary'}>Atlanan: {result.failedCount}</Badge>
          </div>
        </div>
      </CardHeader>
      <CardContent className="space-y-3">
        <Alert variant={hasFailures ? 'warning' : 'success'}>
          {hasFailures ? <AlertCircle className="h-4 w-4" /> : <CheckCircle2 className="h-4 w-4" />}
          <AlertTitle>{hasFailures ? 'İşlem kısmen tamamlandı' : 'İşlem tamamlandı'}</AlertTitle>
          <AlertDescription>
            {hasFailures
              ? 'Bazı öğrenciler iş kuralları veya seçim kapsamı nedeniyle atlandı.'
              : 'Seçili öğrencilerin tamamı güncellendi.'}
          </AlertDescription>
        </Alert>

        {result.errors.length > 0 && (
          <div className="rounded-md border border-destructive/30 bg-destructive/5 p-3 text-sm text-destructive">
            {result.errors.map(error => (
              <div key={error}>{error}</div>
            ))}
          </div>
        )}

        {result.results.length > 0 && (
          <div className="max-h-[280px] overflow-auto rounded-md border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Öğrenci</TableHead>
                  <TableHead className="w-[120px]">Durum</TableHead>
                  <TableHead>Mesaj</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {result.results.map(item => (
                  <TableRow key={`${item.studentId}-${item.message}`}>
                    <TableCell>
                      <div className="font-medium">{item.studentName || item.studentNumber || `#${item.studentId}`}</div>
                      {item.studentNumber && <div className="text-xs text-muted-foreground">{item.studentNumber}</div>}
                    </TableCell>
                    <TableCell>
                      <Badge variant={item.success ? 'success' : 'warning'}>
                        {item.success ? 'Başarılı' : 'Atlandı'}
                      </Badge>
                    </TableCell>
                    <TableCell className="text-sm text-muted-foreground">{item.message}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>
        )}
      </CardContent>
    </Card>
  );
}
