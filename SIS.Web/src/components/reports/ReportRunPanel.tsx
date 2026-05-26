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

const PAGE_SIZE = 50;

// Export hatası blob döndüğünden gövdedeki mesajı çıkarmaya çalışır.
async function extractExportError(err: unknown): Promise<string | undefined> {
  const data = (err as { response?: { data?: unknown } })?.response?.data;
  if (data instanceof Blob) {
    try {
      const text = await data.text();
      const parsed = JSON.parse(text) as { message?: string };
      return parsed.message;
    } catch {
      return undefined;
    }
  }
  return undefined;
}

export function ReportRunPanel({ report, onClose }: ReportRunPanelProps) {
  const { showError } = useToast();
  const code = report?.code ?? null;
  const schemaQuery = useReportSchema(code);
  const runMut = useRunReport(code ?? '');
  const exportMut = useExportReport(code ?? '');
  const [values, setValues] = useState<Record<string, string>>({});

  const activeParams = () => Object.fromEntries(Object.entries(values).filter(([, v]) => v !== ''));

  const run = async (targetPage: number) => {
    try {
      await runMut.mutateAsync({ parameters: activeParams(), page: targetPage, pageSize: PAGE_SIZE });
    } catch {
      showError('Rapor çalıştırılamadı', 'Yetki veya bağlantı sorunu olabilir.');
    }
  };

  // Filtre değişince eski sonucu temizle (yeni filtreyle eski tablo gösterilmesin).
  const handleFilterChange = (k: string, v: string) => {
    setValues((prev) => ({ ...prev, [k]: v }));
    runMut.reset();
  };

  const handleExport = async (format: 'xlsx' | 'pdf') => {
    try {
      await exportMut.mutateAsync({ format, req: { parameters: activeParams(), page: 1, pageSize: PAGE_SIZE } });
    } catch (err) {
      showError('Dışa aktarım başarısız', await extractExportError(err));
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
                onChange={handleFilterChange}
              />
              <Button onClick={() => run(1)} disabled={runMut.isPending}>
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
              onPageChange={(p) => run(p)}
              busy={runMut.isPending}
            />
          )}
        </div>
      </SheetContent>
    </Sheet>
  );
}
