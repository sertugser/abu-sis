import { useState } from 'react';
import { CalendarDays, User as UserIcon } from 'lucide-react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { useAdvisorInfo } from '@/hooks/useAdvisorInfo';
import { AdvisorScheduleSheet } from '@/components/advisor-info/AdvisorScheduleSheet';

interface InfoRowProps {
  label: string;
  value: string | null | undefined;
}

function InfoRow({ label, value }: InfoRowProps) {
  return (
    <div className="grid grid-cols-[140px_1fr] gap-3 text-sm">
      <span className="text-muted-foreground">{label}</span>
      <span className="font-medium">{value && value.trim().length > 0 ? value : '—'}</span>
    </div>
  );
}

export default function AdvisorInfoPage() {
  const { data, isLoading, isError, error } = useAdvisorInfo();
  const [scheduleOpen, setScheduleOpen] = useState(false);

  return (
    <>
      <Card className="overflow-hidden border-border/70 shadow-sm">
        <CardHeader>
          <CardTitle>Danışman Bilgileri</CardTitle>
        </CardHeader>
        <CardContent>
          {isLoading && (
            <div className="flex gap-6">
              <Skeleton className="h-24 w-24 rounded-full" />
              <div className="flex-1 space-y-2">
                <Skeleton className="h-4 w-1/2" />
                <Skeleton className="h-4 w-1/3" />
                <Skeleton className="h-4 w-2/3" />
                <Skeleton className="h-4 w-1/2" />
              </div>
            </div>
          )}

          {isError && (
            <p className="text-sm text-destructive">
              Danışman bilgileri yüklenemedi: {error instanceof Error ? error.message : 'Bilinmeyen hata'}
            </p>
          )}

          {!isLoading && !isError && data && !data.hasAdvisor && (
            <div className="rounded-md border border-dashed p-8 text-center text-sm text-muted-foreground">
              Henüz danışman atanmamış. Bölüm sekreterliğinizle iletişime geçin.
            </div>
          )}

          {!isLoading && !isError && data && data.hasAdvisor && (
            <div className="space-y-6">
              {/* Header: Avatar + isim + sağda Ders Programı butonu */}
              <div className="flex flex-col gap-4 border-b border-border/60 pb-6 sm:flex-row sm:items-center">
                <div className="flex h-28 w-28 shrink-0 items-center justify-center rounded-full bg-muted">
                  <UserIcon className="h-14 w-14 text-muted-foreground" aria-hidden />
                </div>
                <div className="flex-1 min-w-0">
                  <h2 className="text-2xl font-semibold text-foreground">
                    {data.fullName}
                  </h2>
                  {data.departmentName && (
                    <p className="text-sm text-muted-foreground">
                      {data.departmentName}
                      {data.facultyName ? ` — ${data.facultyName}` : ''}
                    </p>
                  )}
                </div>
                <Button
                  variant="outline"
                  onClick={() => setScheduleOpen(true)}
                  className="gap-2 sm:self-center"
                >
                  <CalendarDays className="h-4 w-4" />
                  Ders Programı
                </Button>
              </div>

              {/* Info grid: 2-kolonlu, E-Posta tam satır */}
              <div className="grid grid-cols-1 gap-x-10 gap-y-4 sm:grid-cols-2">
                <InfoRow label="Fakülte" value={data.facultyName} />
                <InfoRow label="Bölüm" value={data.departmentName} />
                <InfoRow label="Program" value={data.programName} />
                <InfoRow label="Telefon" value={data.phone} />
                <div className="sm:col-span-2">
                  <InfoRow label="E-Posta" value={data.email} />
                </div>
              </div>
            </div>
          )}
        </CardContent>
      </Card>

      {data?.hasAdvisor && data.fullName && (
        <AdvisorScheduleSheet
          open={scheduleOpen}
          onOpenChange={setScheduleOpen}
          advisorName={data.fullName}
        />
      )}
    </>
  );
}
