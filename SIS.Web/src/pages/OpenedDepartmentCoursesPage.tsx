import { RefreshCw } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { useOpenedDepartmentCourses } from '@/hooks/useOpenedDepartmentCourses';
import type { OpenedDepartmentCoursesEmptyReason } from '@/api/openedDepartmentCourses.api';

function emptyMessage(reason: OpenedDepartmentCoursesEmptyReason): string {
  switch (reason) {
    case 'StudentNotFound':
      return 'Öğrenci kaydınız bulunamadı. Lütfen öğrenci işleri ile iletişime geçin.';
    case 'NoMainProgram':
      return 'Aktif bir ana programa kayıtlı görünmüyorsunuz. Lütfen öğrenci işleri ile iletişime geçin.';
    case 'NoActiveTerm':
      return 'Sistemde şu an aktif (kullanıma açık) bir akademik dönem yok.';
    case 'Empty':
    default:
      return 'Aktif dönemde müfredatınıza ait açılmış ders bulunmuyor.';
  }
}

function buildTitle(termLabel: string | null, departmentName: string | null): string {
  const parts = ['Aktif Dönemde Açılan Bölüm Dersleri'];
  if (departmentName) parts.push(departmentName);
  if (termLabel) parts.push(termLabel);
  return parts.join(' · ');
}

export default function OpenedDepartmentCoursesPage() {
  const { data, isLoading, isError, error, refetch, isFetching } = useOpenedDepartmentCourses();

  return (
    <div className="overflow-hidden rounded-md border border-border/70 bg-card shadow-sm">
      {/* Üst başlık şeridi */}
      <div className="flex items-center justify-between gap-3 border-b border-border/60 bg-muted/40 px-5 py-3">
        <span className="text-sm font-medium text-foreground">
          {data ? buildTitle(data.termLabel, data.departmentName) : 'Aktif Dönemde Açılan Bölüm Dersleri'}
        </span>
        <Button
          size="sm"
          variant="ghost"
          onClick={() => refetch()}
          disabled={isFetching}
          aria-label="Listeyi yenile"
        >
          <RefreshCw className={`h-4 w-4 ${isFetching ? 'animate-spin' : ''}`} />
        </Button>
      </div>

      {isLoading && (
        <div className="space-y-2 p-5">
          {Array.from({ length: 10 }).map((_, i) => (
            <Skeleton key={i} className="h-10 w-full" />
          ))}
        </div>
      )}

      {isError && (
        <p className="px-5 py-4 text-sm text-destructive" role="alert">
          Dersler yüklenemedi: {error instanceof Error ? error.message : 'Bilinmeyen hata'}
        </p>
      )}

      {!isLoading && !isError && data && data.items.length === 0 && (
        <p className="px-5 py-10 text-center text-sm text-muted-foreground">
          {emptyMessage(data.emptyReason)}
        </p>
      )}

      {!isLoading && !isError && data && data.items.length > 0 && (
        <div className="overflow-x-auto">
          <Table>
            <caption className="sr-only">{buildTitle(data.termLabel, data.departmentName)}</caption>
            <TableHeader>
              <TableRow className="bg-muted/30 hover:bg-muted/30">
                <TableHead scope="col" className="w-16 text-center font-semibold text-foreground">Şb.</TableHead>
                <TableHead scope="col" className="w-32 font-semibold text-foreground">Ders Kodu</TableHead>
                <TableHead scope="col" className="font-semibold text-foreground">Ders Adı</TableHead>
                <TableHead scope="col" className="w-20 text-center font-semibold text-foreground">Sınıf</TableHead>
                <TableHead scope="col" className="w-16 text-center font-semibold text-foreground" aria-label="Teori saati">T</TableHead>
                <TableHead scope="col" className="w-16 text-center font-semibold text-foreground" aria-label="Uygulama saati">U</TableHead>
                <TableHead scope="col" className="w-16 text-center font-semibold text-foreground" aria-label="Laboratuvar saati">L</TableHead>
                <TableHead scope="col" className="w-16 text-center font-semibold text-foreground" aria-label="Zorunlu/Seçmeli">Z</TableHead>
                <TableHead scope="col" className="w-20 text-center font-semibold text-foreground">KRD</TableHead>
                <TableHead scope="col" className="w-20 text-center font-semibold text-foreground">AKTS</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {data.items.map((item) => (
                <TableRow key={item.sectionId} className="transition-colors hover:bg-muted/30">
                  <TableCell className="py-3 text-center font-medium tabular-nums">{item.sectionNumber}</TableCell>
                  <TableCell className="py-3 font-mono text-xs whitespace-nowrap">{item.courseCode}</TableCell>
                  <TableCell className="py-3">{item.courseName}</TableCell>
                  <TableCell className="py-3 text-center tabular-nums">{item.classYear}</TableCell>
                  <TableCell className="py-3 text-center tabular-nums">{item.theoryHours}</TableCell>
                  <TableCell className="py-3 text-center tabular-nums">{item.applicationHours}</TableCell>
                  <TableCell className="py-3 text-center tabular-nums">{item.labHours}</TableCell>
                  <TableCell className="py-3 text-center tabular-nums">
                    <span aria-label={item.isCompulsory ? 'Zorunlu' : 'Seçmeli'}>
                      {item.isCompulsory ? 1 : 2}
                    </span>
                  </TableCell>
                  <TableCell className="py-3 text-center tabular-nums">{item.nationalCredit}</TableCell>
                  <TableCell className="py-3 text-center tabular-nums">{item.ects}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
      )}
    </div>
  );
}
