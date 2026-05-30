import { useEffect, useState } from 'react';
import { Printer } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Skeleton } from '@/components/ui/skeleton';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { DepartmentScheduleWeekGrid } from '@/components/scheduling/DepartmentScheduleWeekGrid';
import {
  useDepartmentSchedule,
  useDepartmentScheduleContext,
} from '@/hooks/useDepartmentSchedule';
import { downloadDepartmentSchedulePdf } from '@/api/departmentSchedule.api';
import { useToast } from '@/components/ui/global-toast';
import { toApiError } from '@/lib/apiError';

const CLASS_YEAR_LABELS: Record<number, string> = {
  0: 'Hazırlık',
  1: '1.Sınıf',
  2: '2.Sınıf',
  3: '3.Sınıf',
  4: '4.Sınıf',
  5: '5.Sınıf',
  6: '6.Sınıf',
};

export default function DepartmentScheduleViewPage() {
  const { data: context, isLoading: contextLoading, isError: contextError } = useDepartmentScheduleContext();
  const { showError } = useToast();

  const [selectedTermId, setSelectedTermId] = useState<number | undefined>(undefined);
  const [selectedClassYear, setSelectedClassYear] = useState<number | undefined>(undefined);

  // Default değerleri context geldiğinde yükle (aktif dönem + öğrencinin sınıfı).
  useEffect(() => {
    if (!context) return;
    if (selectedTermId === undefined) {
      const defaultTerm = context.terms.find((t) => t.isDefault) ?? context.terms[0];
      if (defaultTerm) setSelectedTermId(defaultTerm.termId);
    }
    if (selectedClassYear === undefined) {
      setSelectedClassYear(context.defaultClassYear);
    }
  }, [context, selectedTermId, selectedClassYear]);

  const query =
    context && selectedTermId !== undefined && selectedClassYear !== undefined
      ? {
          termId: selectedTermId,
          programId: context.programId,
          classYear: selectedClassYear,
        }
      : null;

  const { data, isLoading, isError } = useDepartmentSchedule(query);

  const handlePrint = async () => {
    if (!query) return;
    try {
      const blob = await downloadDepartmentSchedulePdf(query);
      const url = URL.createObjectURL(blob);
      window.open(url, '_blank', 'noopener,noreferrer');
      setTimeout(() => URL.revokeObjectURL(url), 60_000);
    } catch (err) {
      showError('PDF oluşturulamadı', toApiError(err, 'Lütfen tekrar deneyin.').message);
    }
  };

  if (contextLoading) {
    return <Skeleton className="h-[600px] rounded-xl" />;
  }

  if (contextError || !context) {
    return (
      <Alert variant="destructive">
        <AlertTitle>Sayfa yüklenemedi</AlertTitle>
        <AlertDescription>
          Bölüm bilgisi alınamadı. Sayfayı yenileyip tekrar deneyin.
        </AlertDescription>
      </Alert>
    );
  }

  return (
    <Card className="flex flex-1 flex-col overflow-hidden">
      <CardContent className="flex flex-1 flex-col p-0">
        {/* Filtre + Yazdır */}
        <div className="flex flex-wrap items-center gap-3 border-b border-border px-4 py-3">
          <div className="flex items-center gap-2">
            <label className="text-sm text-muted-foreground">Dönem</label>
            <Select
              value={selectedTermId?.toString()}
              onValueChange={(v) => setSelectedTermId(Number(v))}
            >
              <SelectTrigger className="h-8 w-56 text-sm">
                <SelectValue placeholder="Dönem seçin" />
              </SelectTrigger>
              <SelectContent>
                {context.terms.map((t) => (
                  <SelectItem key={t.termId} value={t.termId.toString()}>
                    {t.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="flex items-center gap-2">
            <label className="text-sm text-muted-foreground">Program</label>
            <Select value={context.programId.toString()} disabled>
              <SelectTrigger className="h-8 w-64 text-sm">
                <SelectValue>{context.programName}</SelectValue>
              </SelectTrigger>
              <SelectContent>
                <SelectItem value={context.programId.toString()}>{context.programName}</SelectItem>
              </SelectContent>
            </Select>
          </div>

          <div className="flex items-center gap-2">
            <label className="text-sm text-muted-foreground">Sınıf</label>
            <Select
              value={selectedClassYear?.toString()}
              onValueChange={(v) => setSelectedClassYear(Number(v))}
            >
              <SelectTrigger className="h-8 w-32 text-sm">
                <SelectValue placeholder="Sınıf seçin" />
              </SelectTrigger>
              <SelectContent>
                {context.availableClassYears.map((y) => (
                  <SelectItem key={y} value={y.toString()}>
                    {CLASS_YEAR_LABELS[y] ?? `${y}.Sınıf`}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="ml-auto">
            <Button onClick={handlePrint} disabled={!query} variant="outline" size="sm">
              <Printer className="mr-2 h-4 w-4" /> Yazdır
            </Button>
          </div>
        </div>

        {/* İçerik */}
        <div className="flex-1 overflow-auto p-3">
          {isLoading ? (
            <Skeleton className="h-[400px] rounded-md" />
          ) : isError || !data ? (
            <Alert variant="destructive">
              <AlertTitle>Program yüklenemedi</AlertTitle>
              <AlertDescription>Filtreyi değiştirip tekrar deneyin.</AlertDescription>
            </Alert>
          ) : (
            <DepartmentScheduleWeekGrid data={data} />
          )}
        </div>
      </CardContent>
    </Card>
  );
}
