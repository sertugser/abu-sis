import { useState } from 'react';
import { Loader2, Play } from 'lucide-react';
import { Sheet, SheetContent, SheetDescription, SheetHeader, SheetTitle } from '@/components/ui/sheet';
import { Button } from '@/components/ui/button';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { useToast } from '@/components/ui/global-toast';
import { useReportSchema, useRunReport, useExportReport } from '@/hooks/useReportRun';
import type { AccessibleReportDto } from '@/api/myReports.api';
import { ReportFilterForm } from './ReportFilterForm';
import { ReportResultTable } from './ReportResultTable';

interface ReportRunPanelProps {
  report: AccessibleReportDto | null;
  onClose: () => void;
}

export function ReportRunPanel({ report, onClose }: ReportRunPanelProps) {
  const { showError } = useToast();
  const code = report?.code ?? null;
  const schemaQuery = useReportSchema(code);
  const runMut = useRunReport(code ?? '');
  const exportMut = useExportReport(code ?? '');
  const [values, setValues] = useState<Record<string, string>>({});

  const buildRequest = () => ({
    parameters: Object.fromEntries(Object.entries(values).filter(([, v]) => v !== '')),
    page: 1,
    pageSize: 50,
  });

  const handleRun = async () => {
    try {
      await runMut.mutateAsync(buildRequest());
    } catch {
      showError('Rapor çalıştırılamadı', 'Yetki veya bağlantı sorunu olabilir.');
    }
  };

  const handleExport = async (format: 'xlsx' | 'pdf') => {
    try {
      await exportMut.mutateAsync({ format, req: buildRequest() });
    } catch {
      showError('Dışa aktarım başarısız');
    }
  };

  return (
    <Sheet
      open={!!report}
      onOpenChange={(o) => {
        if (!o) { setValues({}); runMut.reset(); onClose(); }
      }}
    >
      <SheetContent side="right" className="w-full sm:max-w-3xl overflow-y-auto">
        <SheetHeader>
          <SheetTitle>{report?.name}</SheetTitle>
          <SheetDescription>{report?.categoryName}</SheetDescription>
        </SheetHeader>

        <div className="mt-6 space-y-6">
          {schemaQuery.isLoading ? (
            <div className="flex items-center gap-2 text-muted-foreground"><Loader2 className="h-4 w-4 animate-spin" />Filtreler yükleniyor...</div>
          ) : schemaQuery.isError ? (
            <Alert variant="destructive"><AlertDescription>Filtre şeması yüklenemedi.</AlertDescription></Alert>
          ) : schemaQuery.data ? (
            <>
              <ReportFilterForm
                parameters={schemaQuery.data.parameters}
                values={values}
                onChange={(k, v) => setValues((prev) => ({ ...prev, [k]: v }))}
              />
              <Button onClick={handleRun} disabled={runMut.isPending}>
                {runMut.isPending ? <Loader2 className="h-4 w-4 mr-2 animate-spin" /> : <Play className="h-4 w-4 mr-2" />}
                Çalıştır
              </Button>
            </>
          ) : null}

          {runMut.data && (
            <ReportResultTable
              result={runMut.data}
              onExport={handleExport}
              exporting={exportMut.isPending}
            />
          )}
        </div>
      </SheetContent>
    </Sheet>
  );
}
