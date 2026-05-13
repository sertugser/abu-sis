import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Skeleton } from '@/components/ui/skeleton';
import { useOpenedDepartmentCourses } from '@/hooks/useOpenedDepartmentCourses';

export default function OpenedDepartmentCoursesPage() {
  const { data, isLoading, isError, error } = useOpenedDepartmentCourses();

  return (
    <div className="overflow-hidden rounded-md border border-border/70 bg-card shadow-sm">
      {/* Üst başlık şeridi */}
      <div className="border-b border-border/60 bg-muted/40 px-5 py-3 text-sm font-medium text-foreground">
        Aktif Dönemde Açılan Bölüm Dersleri
      </div>

      {isLoading && (
        <div className="space-y-2 p-5">
          <Skeleton className="h-10 w-full" />
          <Skeleton className="h-10 w-full" />
          <Skeleton className="h-10 w-full" />
          <Skeleton className="h-10 w-full" />
        </div>
      )}

      {isError && (
        <p className="px-5 py-4 text-sm text-destructive">
          Dersler yüklenemedi: {error instanceof Error ? error.message : 'Bilinmeyen hata'}
        </p>
      )}

      {!isLoading && !isError && data && data.items.length === 0 && (
        <p className="px-5 py-10 text-center text-sm text-muted-foreground">
          Aktif dönemde bölümünüze ait açılmış ders bulunmuyor.
        </p>
      )}

      {!isLoading && !isError && data && data.items.length > 0 && (
        <div className="overflow-x-auto">
          <Table>
            <TableHeader>
              <TableRow className="bg-muted/30 hover:bg-muted/30">
                <TableHead className="w-16 text-center font-semibold text-foreground">Şb.</TableHead>
                <TableHead className="w-32 font-semibold text-foreground">Ders Kodu</TableHead>
                <TableHead className="font-semibold text-foreground">Ders Adı</TableHead>
                <TableHead className="w-20 text-center font-semibold text-foreground">Sınıf</TableHead>
                <TableHead className="w-16 text-center font-semibold text-foreground">U</TableHead>
                <TableHead className="w-16 text-center font-semibold text-foreground">L</TableHead>
                <TableHead className="w-16 text-center font-semibold text-foreground">T</TableHead>
                <TableHead className="w-20 text-center font-semibold text-foreground">KRD</TableHead>
                <TableHead className="w-20 text-center font-semibold text-foreground">AKTS</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {data.items.map((item) => (
                <TableRow key={item.sectionId} className="transition-colors hover:bg-muted/30">
                  <TableCell className="py-3 text-center font-medium tabular-nums">{item.sectionNumber}</TableCell>
                  <TableCell className="py-3 font-mono text-xs whitespace-nowrap">{item.courseCode}</TableCell>
                  <TableCell className="py-3">{item.courseName}</TableCell>
                  <TableCell className="py-3 text-center tabular-nums">
                    {item.classYear ?? <span className="text-muted-foreground">—</span>}
                  </TableCell>
                  <TableCell className="py-3 text-center tabular-nums">{item.applicationHours}</TableCell>
                  <TableCell className="py-3 text-center tabular-nums">{item.labHours}</TableCell>
                  <TableCell className="py-3 text-center tabular-nums">{item.theoryHours}</TableCell>
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
