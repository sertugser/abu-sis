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
import { Badge } from '@/components/ui/badge';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { cn } from '@/lib/utils';
import { prolizDangerButtonClass } from '@/lib/prolizButton';
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
    <div>
      {/* Section title strip */}
      <div className="border-b border-slate-200 bg-slate-50 px-3 py-1.5 flex items-center justify-between">
        <h3 className="text-[12px] font-semibold text-slate-700">Seçilen Dersler</h3>
        <span className="text-[12px] text-slate-500">Toplam: {selectedCourses.length} ders</span>
      </div>

      {dropCourse.isError && (
        <div className="px-3 py-1">
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
        <div className="px-3 py-4 text-xs text-muted-foreground">
          Henüz ders seçilmedi.
        </div>
      ) : (
        <div className="overflow-x-auto">
          <Table>
            <TableHeader>
              {/* Dark navy column headers */}
              <TableRow className="border-b-0 bg-[#003366] hover:bg-[#003366]">
                <TableHead className="h-8 w-12 px-2 text-center text-[12px] font-semibold text-white">Çıkar</TableHead>
                <TableHead className="h-8 w-12 px-2 text-center text-[12px] font-semibold text-white">Şube</TableHead>
                <TableHead className="h-8 w-24 px-2 text-[12px] font-semibold text-white">Kod</TableHead>
                <TableHead className="h-8 px-2 text-[12px] font-semibold text-white">Ad</TableHead>
                <TableHead className="h-8 w-10 px-2 text-center text-[12px] font-semibold text-white">Z/S</TableHead>
                <TableHead className="h-8 w-12 px-2 text-center text-[12px] font-semibold text-white">T+U</TableHead>
                <TableHead className="h-8 w-10 px-2 text-center text-[12px] font-semibold text-white">KRD</TableHead>
                <TableHead className="h-8 w-12 px-2 text-center text-[12px] font-semibold text-white">AKTS</TableHead>
                <TableHead className="hidden md:table-cell h-8 w-12 px-2 text-center text-[12px] font-semibold text-white">
                  Sınıf
                </TableHead>
                <TableHead className="hidden lg:table-cell h-8 px-2 text-[12px] font-semibold text-white">Alış</TableHead>
                <TableHead className="hidden lg:table-cell h-8 px-2 text-[12px] font-semibold text-white">AcNeden</TableHead>
                <TableHead className="h-8 w-32 px-2 text-center text-[12px] font-semibold text-white">Durum</TableHead>
                <TableHead className="hidden xl:table-cell h-8 w-20 px-2 text-[12px] font-semibold text-white">Grup Kod</TableHead>
              </TableRow>
            </TableHeader>

            <TableBody>
              {selectedCourses.map((row, index) => {
                const isPending = pendingId === row.registrationId && dropCourse.isPending;

                return (
                  <TableRow
                    key={row.registrationId}
                    className={cn(
                      'border-b border-slate-200 transition-colors hover:bg-[#dce9ff]',
                      index % 2 === 1 && 'bg-slate-50/60',
                    )}
                  >
                    {/* Çıkar */}
                    <TableCell className="w-12 py-1 px-2 text-center">
                      <button
                        type="button"
                        className={prolizDangerButtonClass}
                        disabled={!canDrop(row.status) || dropCourse.isPending}
                        title={canDrop(row.status) ? 'Dersi çıkar' : 'Bu ders çıkarılamaz'}
                        onClick={() => handleDrop(row.registrationId)}
                      >
                        {isPending ? (
                          <Loader2 className="h-3 w-3 animate-spin" />
                        ) : (
                          <Trash2 className="h-3 w-3" />
                        )}
                      </button>
                    </TableCell>

                    {/* Şube */}
                    <TableCell className="w-12 py-1 px-2 text-center text-xs tabular-nums text-slate-700">
                      —
                    </TableCell>

                    {/* Kod */}
                    <TableCell className="w-24 py-1 px-2 font-mono text-xs font-semibold text-primary whitespace-nowrap">
                      {row.courseCode}
                    </TableCell>

                    {/* Ad */}
                    <TableCell className="py-1 px-2 text-xs text-slate-700">
                      {row.courseName}
                    </TableCell>

                    {/* Z/S */}
                    <TableCell className="w-10 py-1 px-2 text-center text-xs font-bold">
                      <span className={row.isCompulsory ? 'text-primary' : 'text-muted-foreground'}>
                        {row.isCompulsory ? 'Z' : 'S'}
                      </span>
                    </TableCell>

                    {/* T+U */}
                    <TableCell className="w-12 py-1 px-2 text-center text-xs font-mono tabular-nums text-slate-700">
                      {row.theoryHours}+{row.labHours}
                    </TableCell>

                    {/* KRD */}
                    <TableCell className="w-10 py-1 px-2 text-center text-xs tabular-nums text-slate-700">
                      —
                    </TableCell>

                    {/* AKTS */}
                    <TableCell className="w-12 py-1 px-2 text-center text-xs text-slate-700">
                      <Badge variant="secondary" className="font-mono text-[10px] px-1.5 py-0 h-5">
                        {row.ects}
                      </Badge>
                    </TableCell>

                    {/* Sınıf */}
                    <TableCell className="hidden md:table-cell w-12 py-1 px-2 text-center text-xs tabular-nums text-slate-700">
                      {row.suggestedSemester > 0 ? row.suggestedSemester : '—'}
                    </TableCell>

                    {/* Alış */}
                    <TableCell className="hidden lg:table-cell py-1 px-2 text-xs text-slate-700">
                      <span className={row.takeType === 'Alttan' ? 'text-destructive' : undefined}>
                        {row.takeType}
                      </span>
                    </TableCell>

                    {/* AcNeden */}
                    <TableCell className="hidden lg:table-cell py-1 px-2 text-xs text-slate-700">
                      {row.openingReasonName ?? '—'}
                    </TableCell>

                    {/* Durum */}
                    <TableCell className="w-32 py-1 px-2 text-center">
                      <RegistrationStatusBadge status={row.status} />
                    </TableCell>

                    {/* Grup Kod */}
                    <TableCell className="hidden xl:table-cell w-20 py-1 px-2 text-xs font-mono text-orange-700">
                      {row.electiveGroupCode ?? '—'}
                    </TableCell>
                  </TableRow>
                );
              })}
            </TableBody>
          </Table>
        </div>
      )}
    </div>
  );
}
