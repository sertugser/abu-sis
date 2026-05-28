import { useState } from 'react';
import { Trash2, Loader2 } from 'lucide-react';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Alert, AlertDescription } from '@/components/ui/alert';
import RegistrationStatusBadge from '@/components/registration/RegistrationStatusBadge';
import { useDropCourse } from '@/hooks/useDropCourse';
import type { SelectedCourse } from '@/types/registrationCatalog';

interface Props {
  selectedCourses: SelectedCourse[];
  registrationActionsOpen: boolean;
}

export function SelectedCoursesGrid({ selectedCourses, registrationActionsOpen }: Props) {
  const [pendingId, setPendingId] = useState<number | null>(null);
  const dropCourse = useDropCourse();

  function canDrop(status: string): boolean {
    return (status === 'Draft' || status === 'Rejected') && registrationActionsOpen;
  }

  function handleDrop(registrationId: number): void {
    setPendingId(registrationId);
    dropCourse.mutate(registrationId, {
      onSettled: () => setPendingId(null),
    });
  }

  return (
    <Card className="border-border/70">
      <CardHeader className="pb-2">
        <div className="flex items-center justify-between">
          <CardTitle className="text-base">Seçilen Dersler</CardTitle>
          <span className="text-xs text-muted-foreground">
            Toplam: {selectedCourses.length} ders
          </span>
        </div>
      </CardHeader>

      <CardContent className="p-0">
        {dropCourse.isError && (
          <div className="px-4 pb-3">
            <Alert variant="destructive">
              <AlertDescription>
                {dropCourse.error instanceof Error
                  ? dropCourse.error.message
                  : 'Ders silinirken bir hata oluştu.'}
              </AlertDescription>
            </Alert>
          </div>
        )}

        {selectedCourses.length === 0 ? (
          <div className="px-4 py-6 text-sm text-muted-foreground">
            Henüz ders seçilmedi.
          </div>
        ) : (
          <div className="overflow-x-auto">
            <Table>
              <TableHeader>
                <TableRow className="text-xs">
                  <TableHead className="w-12 py-2 px-2 text-center">Çıkar</TableHead>
                  <TableHead className="w-12 py-2 px-2 text-center">Şube</TableHead>
                  <TableHead className="w-24 py-2 px-2">Kod</TableHead>
                  <TableHead className="py-2 px-2">Ad</TableHead>
                  <TableHead className="w-10 py-2 px-2 text-center">Z/S</TableHead>
                  <TableHead className="w-12 py-2 px-2 text-center">T+U</TableHead>
                  <TableHead className="w-10 py-2 px-2 text-center">KRD</TableHead>
                  <TableHead className="w-12 py-2 px-2 text-center">AKTS</TableHead>
                  <TableHead className="hidden md:table-cell w-12 py-2 px-2 text-center">
                    Sınıf
                  </TableHead>
                  <TableHead className="hidden lg:table-cell py-2 px-2">Alış</TableHead>
                  <TableHead className="hidden lg:table-cell py-2 px-2">AcNeden</TableHead>
                  <TableHead className="w-32 py-2 px-2 text-center">Durum</TableHead>
                  <TableHead className="hidden xl:table-cell w-20 py-2 px-2">Grup Kod</TableHead>
                </TableRow>
              </TableHeader>

              <TableBody>
                {selectedCourses.map((row) => {
                  const isPending = pendingId === row.registrationId && dropCourse.isPending;

                  return (
                    <TableRow key={row.registrationId} className="hover:bg-muted/30">
                      {/* Çıkar */}
                      <TableCell className="w-12 py-2 px-2 text-center">
                        <Button
                          size="icon"
                          variant="ghost"
                          className="h-7 w-7"
                          disabled={!canDrop(row.status) || dropCourse.isPending}
                          title={canDrop(row.status) ? 'Dersi çıkar' : 'Bu ders çıkarılamaz'}
                          onClick={() => handleDrop(row.registrationId)}
                        >
                          {isPending ? (
                            <Loader2 className="h-3.5 w-3.5 animate-spin" />
                          ) : (
                            <Trash2 className="h-3.5 w-3.5" />
                          )}
                        </Button>
                      </TableCell>

                      {/* Şube */}
                      <TableCell className="w-12 py-2 px-2 text-center text-xs tabular-nums">
                        —
                      </TableCell>

                      {/* Kod */}
                      <TableCell className="w-24 py-2 px-2 font-mono text-xs font-semibold text-primary whitespace-nowrap">
                        {row.courseCode}
                      </TableCell>

                      {/* Ad */}
                      <TableCell className="py-2 px-2 text-sm">
                        {row.courseName}
                      </TableCell>

                      {/* Z/S */}
                      <TableCell className="w-10 py-2 px-2 text-center text-xs font-bold">
                        <span className={row.isCompulsory ? 'text-primary' : 'text-muted-foreground'}>
                          {row.isCompulsory ? 'Z' : 'S'}
                        </span>
                      </TableCell>

                      {/* T+U */}
                      <TableCell className="w-12 py-2 px-2 text-center text-xs font-mono tabular-nums">
                        {row.theoryHours}+{row.labHours}
                      </TableCell>

                      {/* KRD — not on SelectedCourse */}
                      <TableCell className="w-10 py-2 px-2 text-center text-xs tabular-nums">
                        —
                      </TableCell>

                      {/* AKTS */}
                      <TableCell className="w-12 py-2 px-2 text-center">
                        <Badge variant="secondary" className="font-mono text-[11px] px-1.5 py-0">
                          {row.ects}
                        </Badge>
                      </TableCell>

                      {/* Sınıf */}
                      <TableCell className="hidden md:table-cell w-12 py-2 px-2 text-center text-xs tabular-nums text-muted-foreground">
                        {row.suggestedSemester > 0 ? row.suggestedSemester : '—'}
                      </TableCell>

                      {/* Alış */}
                      <TableCell className="hidden lg:table-cell py-2 px-2 text-xs">
                        <span className={row.takeType === 'Alttan' ? 'text-destructive' : undefined}>
                          {row.takeType}
                        </span>
                      </TableCell>

                      {/* AcNeden */}
                      <TableCell className="hidden lg:table-cell py-2 px-2 text-xs">
                        {row.openingReasonName ?? '—'}
                      </TableCell>

                      {/* Durum */}
                      <TableCell className="w-32 py-2 px-2 text-center">
                        <RegistrationStatusBadge status={row.status} />
                      </TableCell>

                      {/* Grup Kod */}
                      <TableCell className="hidden xl:table-cell w-20 py-2 px-2 text-xs font-mono text-orange-700">
                        {row.electiveGroupCode ?? '—'}
                      </TableCell>
                    </TableRow>
                  );
                })}
              </TableBody>
            </Table>
          </div>
        )}
      </CardContent>
    </Card>
  );
}
