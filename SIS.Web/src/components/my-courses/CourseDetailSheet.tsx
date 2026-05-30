import { Dialog, DialogTitle } from '@/components/ui/dialog';
import ResizableDialogContent from '@/components/ui/resizable-dialog';
import { Badge } from '@/components/ui/badge';
import { Skeleton } from '@/components/ui/skeleton';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { AlertCircle, BarChart3, ListChecks } from 'lucide-react';
import { cn } from '@/lib/utils';
import { getGradeBadgeClass } from '@/lib/gradeUtils';
import { useStudentGradeStatistics } from '@/hooks/useStudentGradeStatistics';

interface CourseDetailSheetProps {
  registrationId: number | null;
  fallbackTitle?: string;
  onOpenChange: (open: boolean) => void;
}

/**
 * "Dersin Ayrıntıları" dialog'u — ayarlanabilir boyutlu (resize + fullscreen toggle).
 * Sınav not bileşenleri, ağırlıklı ortalama, harf notu detayı.
 */
export function CourseDetailSheet({ registrationId, fallbackTitle, onOpenChange }: CourseDetailSheetProps) {
  const { data, isLoading, error } = useStudentGradeStatistics(registrationId, false);

  return (
    <Dialog open={registrationId !== null} onOpenChange={onOpenChange}>
      <ResizableDialogContent
        defaultWidth={780}
        defaultHeight={680}
        minWidth={560}
        minHeight={480}
        className="flex flex-col"
      >
        <DialogTitle className="sr-only">Dersin Ayrıntıları</DialogTitle>

        {/* Header */}
        <div className="shrink-0 border-b border-border pb-3">
          <div className="flex items-center gap-2">
            <BarChart3 className="h-5 w-5 text-primary" />
            <h2 className="font-heading text-base font-bold text-foreground">Dersin Ayrıntıları</h2>
          </div>
          <p className="mt-1 text-xs text-muted-foreground">
            Sınav not bileşenleri, ağırlıklı ortalama ve harf notu detayı.
          </p>
        </div>

        {/* Body */}
        <div className="flex-1 overflow-y-auto pt-4 pr-1">
          <div className="space-y-5">
            {isLoading && (
              <div className="space-y-3">
                <Skeleton className="h-10 rounded-md" />
                <Skeleton className="h-48 rounded-md" />
              </div>
            )}

            {error && (
              <Alert variant="destructive">
                <AlertCircle className="h-4 w-4" />
                <AlertDescription>Ders ayrıntıları yüklenirken hata oluştu.</AlertDescription>
              </Alert>
            )}

            {data && (
              <>
                <section className="rounded-lg border border-border bg-card p-4">
                  <p className="font-mono text-sm font-bold text-primary">
                    {data.courseCode} · Şube {data.sectionNumber}
                  </p>
                  <h3 className="mt-1 text-base font-semibold text-foreground">
                    {data.courseName || fallbackTitle}
                  </h3>
                  <p className="mt-1 text-sm text-muted-foreground">{data.facultyProgram}</p>
                  <p className="text-sm text-muted-foreground">{data.instructorName}</p>
                  <p className="text-sm text-muted-foreground">{data.termName}</p>
                </section>

                <section className="grid grid-cols-3 gap-3">
                  <StatBox
                    label="Ağırlıklı Ortalama"
                    value={data.studentWeightedAverage !== null ? data.studentWeightedAverage.toFixed(2) : '—'}
                  />
                  <StatBox
                    label="Harf Notu"
                    value={data.studentLetterGrade === 'W' ? 'Çekildi' : (data.studentLetterGrade ?? '—')}
                    badgeClass={
                      // 'W' bir not değil, çekilme durumudur — rozetsiz düz metin göster.
                      data.studentLetterGrade && data.studentLetterGrade !== 'W'
                        ? getGradeBadgeClass(data.studentLetterGrade)
                        : undefined
                    }
                  />
                  <StatBox
                    label="Sınıf Ortalaması"
                    value={data.summary.classAverage !== null ? data.summary.classAverage.toFixed(2) : '—'}
                  />
                </section>

                <section>
                  <h4 className="mb-2 flex items-center gap-2 text-sm font-semibold text-foreground">
                    <ListChecks className="h-4 w-4" />
                    Sınav Bileşenleri
                  </h4>
                  {data.components.length === 0 ? (
                    <p className="text-sm text-muted-foreground">Bu ders için sınav müfredatı tanımlı değil.</p>
                  ) : (
                    <div className="overflow-hidden rounded-md border border-border">
                      <Table>
                        <TableHeader>
                          <TableRow className="bg-muted/50 hover:bg-muted/50">
                            <TableHead>Bileşen</TableHead>
                            <TableHead className="w-20 text-center">Ağırlık</TableHead>
                            <TableHead className="w-24 text-center">Notum</TableHead>
                            <TableHead className="w-28 text-center">Sınıf Ort.</TableHead>
                          </TableRow>
                        </TableHeader>
                        <TableBody>
                          {data.components.map(c => (
                            <TableRow key={c.componentId}>
                              <TableCell className="font-medium">{c.name}</TableCell>
                              <TableCell className="text-center">
                                <Badge variant="secondary">%{c.weight}</Badge>
                              </TableCell>
                              <TableCell className="text-center font-mono text-sm">
                                {c.studentScore !== null ? c.studentScore.toFixed(2) : '—'}
                              </TableCell>
                              <TableCell className="text-center font-mono text-sm text-muted-foreground">
                                {c.averageScore !== null ? c.averageScore.toFixed(2) : '—'}
                              </TableCell>
                            </TableRow>
                          ))}
                        </TableBody>
                      </Table>
                    </div>
                  )}
                </section>
              </>
            )}
          </div>
        </div>
      </ResizableDialogContent>
    </Dialog>
  );
}

function StatBox({
  label,
  value,
  badgeClass,
}: {
  label: string;
  value: string;
  badgeClass?: string;
}) {
  return (
    <div className="rounded-md border border-border bg-card px-3 py-2 text-center">
      <p className="text-[10px] font-semibold uppercase tracking-wide text-muted-foreground">{label}</p>
      {badgeClass ? (
        <span className={cn('mt-1 inline-flex min-w-10 justify-center rounded-md px-2 py-0.5 text-sm font-bold', badgeClass)}>
          {value}
        </span>
      ) : (
        <p className="mt-1 font-mono text-base font-bold text-foreground">{value}</p>
      )}
    </div>
  );
}
