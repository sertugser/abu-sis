import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Skeleton } from '@/components/ui/skeleton';
import { useOpenedDepartmentCourses } from '@/hooks/useOpenedDepartmentCourses';

export default function OpenedDepartmentCoursesPage() {
  const { data, isLoading, isError, error } = useOpenedDepartmentCourses();

  return (
    <Card className="overflow-hidden border-border/70 shadow-sm">
      <CardHeader>
        <CardTitle>Açılan Bölüm Dersleri</CardTitle>
        {data && (
          <CardDescription>
            {data.departmentName ?? 'Bölüm bilgisi yok'} — {data.termLabel ?? 'Aktif dönem yok'}
          </CardDescription>
        )}
      </CardHeader>
      <CardContent>
        {isLoading && (
          <div className="space-y-2">
            <Skeleton className="h-10 w-full" />
            <Skeleton className="h-10 w-full" />
            <Skeleton className="h-10 w-full" />
            <Skeleton className="h-10 w-full" />
          </div>
        )}

        {isError && (
          <p className="text-sm text-destructive">
            Dersler yüklenemedi: {error instanceof Error ? error.message : 'Bilinmeyen hata'}
          </p>
        )}

        {!isLoading && !isError && data && data.items.length === 0 && (
          <div className="rounded-md border border-dashed p-8 text-center text-sm text-muted-foreground">
            Aktif dönemde bölümünüze ait açılmış ders bulunmuyor.
          </div>
        )}

        {!isLoading && !isError && data && data.items.length > 0 && (
          <div className="overflow-x-auto">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead className="w-14 text-center">Şb.</TableHead>
                  <TableHead className="w-28">Ders Kodu</TableHead>
                  <TableHead>Ders Adı</TableHead>
                  <TableHead className="w-16 text-center">Sınıf</TableHead>
                  <TableHead className="w-12 text-center">U</TableHead>
                  <TableHead className="w-12 text-center">L</TableHead>
                  <TableHead className="w-12 text-center">T</TableHead>
                  <TableHead className="w-16 text-center">KRD</TableHead>
                  <TableHead className="w-16 text-center">AKTS</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {data.items.map((item) => (
                  <TableRow key={`${item.sectionId}`}>
                    <TableCell className="text-center font-medium">{item.sectionNumber}</TableCell>
                    <TableCell className="font-mono text-xs">{item.courseCode}</TableCell>
                    <TableCell>{item.courseName}</TableCell>
                    <TableCell className="text-center">
                      {item.classYear ?? <span className="text-muted-foreground">—</span>}
                    </TableCell>
                    <TableCell className="text-center tabular-nums">{item.applicationHours}</TableCell>
                    <TableCell className="text-center tabular-nums">{item.labHours}</TableCell>
                    <TableCell className="text-center tabular-nums">{item.theoryHours}</TableCell>
                    <TableCell className="text-center tabular-nums">{item.nationalCredit}</TableCell>
                    <TableCell className="text-center tabular-nums">{item.ects}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
            <p className="mt-3 text-xs text-muted-foreground">
              Toplam {data.items.length} şube • U: Uygulama saati, L: Laboratuvar, T: Teori, KRD: Yerel kredi
            </p>
          </div>
        )}
      </CardContent>
    </Card>
  );
}
