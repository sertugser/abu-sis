import { useQuery } from '@tanstack/react-query';
import { Dialog, DialogTitle } from '@/components/ui/dialog';
import ResizableDialogContent from '@/components/ui/resizable-dialog';
import { Badge } from '@/components/ui/badge';
import { Skeleton } from '@/components/ui/skeleton';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { AlertCircle, BookOpen, Layers, Languages, ScrollText } from 'lucide-react';
import { getCourseDetail } from '@/api/course.api';

interface CourseInfoSheetProps {
  courseId: number | null;
  onOpenChange: (open: boolean) => void;
}

/**
 * "Ders Bilgileri" dialog'u — ayarlanabilir boyutlu (resize + fullscreen toggle).
 * "Alınan Dersler" sayfasındaki dişli ikonundan açılır.
 */
export function CourseInfoSheet({ courseId, onOpenChange }: CourseInfoSheetProps) {
  const { data, isLoading, error } = useQuery({
    queryKey: ['courseDetail', courseId],
    queryFn: () => getCourseDetail(courseId!),
    enabled: courseId !== null,
    staleTime: 5 * 60 * 1000,
  });

  return (
    <Dialog open={courseId !== null} onOpenChange={onOpenChange}>
      <ResizableDialogContent
        defaultWidth={720}
        defaultHeight={680}
        minWidth={520}
        minHeight={460}
        className="flex flex-col"
      >
        <DialogTitle className="sr-only">Ders Bilgileri</DialogTitle>

        {/* Header */}
        <div className="shrink-0 border-b border-border pb-3">
          <div className="flex items-center gap-2">
            <BookOpen className="h-5 w-5 text-primary" />
            <h2 className="font-heading text-base font-bold text-foreground">Ders Bilgileri</h2>
          </div>
          <p className="mt-1 text-xs text-muted-foreground">
            Ders katalog detayı — kredi, saat, ön koşul ve eşdeğer ders bilgileri.
          </p>
        </div>

        {/* Body — kalan alanı kaplar, kendi içinde scroll */}
        <div className="flex-1 overflow-y-auto pt-4 pr-1">
          <div className="space-y-5">
            {isLoading && (
              <div className="space-y-3">
                <Skeleton className="h-10 rounded-md" />
                <Skeleton className="h-32 rounded-md" />
                <Skeleton className="h-20 rounded-md" />
              </div>
            )}

            {error && (
              <Alert variant="destructive">
                <AlertCircle className="h-4 w-4" />
                <AlertDescription>Ders detayları yüklenirken hata oluştu.</AlertDescription>
              </Alert>
            )}

            {data && (
              <>
                {/* Kimlik bloğu */}
                <section className="rounded-lg border border-border bg-card p-4">
                  <div className="flex items-baseline gap-2">
                    <span className="font-mono text-sm font-bold text-primary">{data.code}</span>
                    <Badge variant="outline" className="text-xs">{data.level}</Badge>
                    <Badge variant="outline" className="text-xs">{data.status}</Badge>
                  </div>
                  <h3 className="mt-1 text-base font-semibold text-foreground">{data.name}</h3>
                  {data.englishName && (
                    <p className="text-sm italic text-muted-foreground">{data.englishName}</p>
                  )}
                </section>

                {/* Kredi / saat / dil */}
                <section className="grid grid-cols-2 gap-3 md:grid-cols-3">
                  <InfoBox label="AKTS" value={data.ects.toString()} />
                  <InfoBox label="Yerel Kredi" value={data.nationalCredit.toString()} />
                  <InfoBox label="Teorik" value={`${data.theoryHours} sa`} />
                  <InfoBox label="Lab." value={`${data.labHours} sa`} />
                  <InfoBox label="Uygulama" value={`${data.applicationHours ?? 0} sa`} />
                  <InfoBox label="Dil" value={data.language} icon={<Languages className="h-3 w-3" />} />
                </section>

                {/* Bölüm / Fakülte / Öğretim yöntemi */}
                <section className="space-y-2 rounded-lg border border-border bg-accent/30 p-4">
                  <DetailLine label="Sahibi" value={data.departmentName ?? '—'} />
                  <DetailLine label="Fakülte" value={data.facultyName ?? '—'} />
                  <DetailLine label="Öğretim Yöntemi" value={data.teachingMethod} />
                  <DetailLine label="Sınav Tipi" value={data.examType ?? '—'} />
                  <DetailLine label="Not Sistemi" value={data.isNonGpa ? 'S/U (GPA hariç)' : 'Harf Notu (GPA dahil)'} />
                </section>

                {/* Açıklama */}
                {data.description && (
                  <section className="rounded-lg border border-border bg-card p-4">
                    <h4 className="mb-2 flex items-center gap-2 text-sm font-semibold text-foreground">
                      <ScrollText className="h-4 w-4" />
                      Açıklama
                    </h4>
                    <p className="whitespace-pre-wrap text-sm leading-relaxed text-muted-foreground">
                      {data.description}
                    </p>
                  </section>
                )}

                {/* Ön koşullar */}
                <section>
                  <h4 className="mb-2 flex items-center gap-2 text-sm font-semibold text-foreground">
                    <Layers className="h-4 w-4" />
                    Ön Koşul Dersler
                  </h4>
                  {data.prerequisites.length === 0 ? (
                    <p className="text-sm text-muted-foreground">Ön koşul bulunmamaktadır.</p>
                  ) : (
                    <ul className="space-y-1.5">
                      {data.prerequisites.map(p => (
                        <li
                          key={p.courseId}
                          className="flex items-baseline gap-2 rounded border border-border bg-card px-3 py-2 text-sm"
                        >
                          <span className="font-mono font-semibold text-primary">{p.code}</span>
                          <span className="text-foreground">{p.name}</span>
                        </li>
                      ))}
                    </ul>
                  )}
                </section>

                {/* Eşdeğer dersler */}
                <section>
                  <h4 className="mb-2 text-sm font-semibold text-foreground">Eşdeğer Dersler</h4>
                  {data.equivalencies.length === 0 ? (
                    <p className="text-sm text-muted-foreground">Eşdeğer ders bulunmamaktadır.</p>
                  ) : (
                    <ul className="space-y-1.5">
                      {data.equivalencies.map(e => (
                        <li
                          key={e.courseId}
                          className="flex items-baseline gap-2 rounded border border-border bg-card px-3 py-2 text-sm"
                        >
                          <span className="font-mono font-semibold text-primary">{e.code}</span>
                          <span className="text-foreground">{e.name}</span>
                        </li>
                      ))}
                    </ul>
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

function InfoBox({ label, value, icon }: { label: string; value: string; icon?: React.ReactNode }) {
  return (
    <div className="rounded-md border border-border bg-card px-3 py-2">
      <p className="flex items-center gap-1 text-[10px] font-semibold uppercase tracking-wide text-muted-foreground">
        {icon}{label}
      </p>
      <p className="mt-0.5 font-mono text-sm font-bold text-foreground">{value}</p>
    </div>
  );
}

function DetailLine({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex gap-3 text-sm">
      <span className="min-w-32 font-medium text-muted-foreground">{label}</span>
      <span className="text-foreground">{value}</span>
    </div>
  );
}
