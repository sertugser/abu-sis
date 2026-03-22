import { CalendarDays, Clock3, ExternalLink } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import {
  ACADEMIC_CALENDAR_SOURCE_URL,
  SPRING_2025_2026_CALENDAR,
  type AcademicCalendarEntry,
} from '../lib/academicCalendar';

const TODAY = new Date('2026-03-22T12:00:00+03:00');

export default function AcademicCalendarPage() {
  const currentEvents = SPRING_2025_2026_CALENDAR.filter(event => getEventStatus(event) === 'active');
  const upcomingEvents = SPRING_2025_2026_CALENDAR.filter(event => getEventStatus(event) === 'upcoming');
  const nextEvent = upcomingEvents[0] ?? null;

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <h1 className="font-heading text-2xl font-extrabold tracking-tight text-foreground">
            Akademik Takvim
          </h1>
          <p className="mt-1 max-w-3xl text-sm text-muted-foreground">
            2025-2026 Bahar dönemi akademik takvimi, Ankara Bilim Üniversitesi resmi öğrenci sayfasındaki
            duyuruya göre düzenlenmiştir.
          </p>
        </div>

        <a
          href={ACADEMIC_CALENDAR_SOURCE_URL}
          target="_blank"
          rel="noreferrer"
          className="inline-flex items-center gap-2 rounded-lg border border-border bg-card px-4 py-2 text-sm font-medium text-primary transition-colors hover:bg-muted"
        >
          Resmi kaynağı aç
          <ExternalLink className="h-4 w-4" />
        </a>
      </div>

      <div className="grid gap-4 md:grid-cols-3">
        <SummaryCard
          title="Aktif dönem"
          value="2025-2026 Bahar"
          description={`${SPRING_2025_2026_CALENDAR.length} resmi takvim kalemi`}
        />
        <SummaryCard
          title="Bugün devam eden"
          value={currentEvents.length === 0 ? 'Yok' : String(currentEvents.length)}
          description={currentEvents[0]?.title ?? 'Bugün için aktif takvim olayı görünmüyor'}
          accent="success"
        />
        <SummaryCard
          title="Sıradaki takvim"
          value={nextEvent?.startLabel ?? 'Yok'}
          description={nextEvent?.title ?? 'Yaklaşan yeni takvim kalemi yok'}
          accent="info"
        />
      </div>

      <Card>
        <CardHeader className="pb-3">
          <div className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
            <div>
              <CardTitle>2025-2026 Bahar Dönemi</CardTitle>
              <CardDescription>
                Başlangıç ve bitiş tarihleri resmi takvimde ilan edilen sıra ile listelenmiştir.
              </CardDescription>
            </div>
            <div className="flex flex-wrap items-center gap-2">
              <Badge variant="success">Devam eden</Badge>
              <Badge variant="info">Yaklaşan</Badge>
              <Badge variant="secondary">Tamamlandı</Badge>
              <Badge variant="outline">Tarih bekleniyor</Badge>
            </div>
          </div>
        </CardHeader>
        <CardContent>
          <div className="overflow-hidden rounded-xl border">
            <Table>
              <TableHeader>
                <TableRow className="bg-muted/60 hover:bg-muted/60">
                  <TableHead className="w-[48%]">Takvim Adı</TableHead>
                  <TableHead>Başlangıç Tarihi</TableHead>
                  <TableHead>Bitiş Tarihi</TableHead>
                  <TableHead className="w-[140px]">Durum</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {SPRING_2025_2026_CALENDAR.map(event => {
                  const status = getEventStatus(event);

                  return (
                    <TableRow key={event.id} className="align-top">
                      <TableCell>
                        <div className="space-y-1">
                          <p className="font-medium text-foreground">{event.title}</p>
                          {event.note && (
                            <p className="text-xs text-muted-foreground">{event.note}</p>
                          )}
                        </div>
                      </TableCell>
                      <TableCell>{event.startLabel}</TableCell>
                      <TableCell>{event.endLabel}</TableCell>
                      <TableCell>
                        <StatusBadge status={status} />
                      </TableCell>
                    </TableRow>
                  );
                })}
              </TableBody>
            </Table>
          </div>
        </CardContent>
      </Card>

      <div className="grid gap-4 xl:grid-cols-2">
        <Card>
          <CardHeader className="pb-3">
            <CardTitle className="flex items-center gap-2 text-base">
              <Clock3 className="h-4 w-4 text-success" />
              Şu an öne çıkanlar
            </CardTitle>
            <CardDescription>Bugün itibarıyla aktif olan veya en yakın tarihte başlayacak kalemler.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-3">
            {[...currentEvents, ...upcomingEvents.slice(0, 3)].map(event => (
              <div key={event.id} className="rounded-xl border bg-card px-4 py-3">
                <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
                  <div>
                    <p className="font-medium text-foreground">{event.title}</p>
                    <p className="text-xs text-muted-foreground">
                      {event.startLabel} - {event.endLabel}
                    </p>
                  </div>
                  <StatusBadge status={getEventStatus(event)} />
                </div>
              </div>
            ))}
            {currentEvents.length === 0 && upcomingEvents.length === 0 && (
              <p className="text-sm text-muted-foreground">Takvim üzerinde yeni bir aksiyon görünmüyor.</p>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="pb-3">
            <CardTitle className="flex items-center gap-2 text-base">
              <CalendarDays className="h-4 w-4 text-primary" />
              Kullanım Notu
            </CardTitle>
            <CardDescription>Öğrenci ekranında sık takip edilen başlıklar için kısa yönlendirme.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-3 text-sm text-muted-foreground">
            <p>
              Ders kayıt ve danışman onayı tarihleri kaçırılmaması gereken ilk kritik aralıklar olarak öne çıkar.
            </p>
            <p>
              Ara sınav, dönem sonu ve bütünleme tarihleri resmi takvimde ilan edilen günlere göre takip edilmelidir.
            </p>
            <p>
              Resmi tatil ve bayram günleri de aynı tabloda yer aldığı için ders veya sınav planlaması yaparken doğrudan
              bu ekran referans alınabilir.
            </p>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}

function SummaryCard({
  title,
  value,
  description,
  accent = 'default',
}: {
  title: string;
  value: string;
  description: string;
  accent?: 'default' | 'success' | 'info';
}) {
  const accentClassName =
    accent === 'success'
      ? 'border-l-success'
      : accent === 'info'
        ? 'border-l-info'
        : 'border-l-primary';

  return (
    <Card className={`border-l-4 ${accentClassName}`}>
      <CardContent className="p-5">
        <p className="text-xs font-medium uppercase tracking-wider text-muted-foreground">{title}</p>
        <p className="mt-2 text-2xl font-heading font-bold text-foreground">{value}</p>
        <p className="mt-1 text-sm text-muted-foreground">{description}</p>
      </CardContent>
    </Card>
  );
}

function StatusBadge({ status }: { status: ReturnType<typeof getEventStatus> }) {
  if (status === 'active') {
    return <Badge variant="success">Devam ediyor</Badge>;
  }

  if (status === 'upcoming') {
    return <Badge variant="info">Yaklaşıyor</Badge>;
  }

  if (status === 'pending') {
    return <Badge variant="outline">Tarih bekleniyor</Badge>;
  }

  return <Badge variant="secondary">Tamamlandı</Badge>;
}

function getEventStatus(event: AcademicCalendarEntry) {
  if (!event.startDate || !event.endDate) {
    return 'pending' as const;
  }

  const start = new Date(`${event.startDate}T00:00:00+03:00`);
  const end = new Date(`${event.endDate}T23:59:59+03:00`);

  if (TODAY >= start && TODAY <= end) {
    return 'active' as const;
  }

  if (TODAY < start) {
    return 'upcoming' as const;
  }

  return 'completed' as const;
}
