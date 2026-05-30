import { Trash2, Loader2 } from 'lucide-react';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { Button } from '@/components/ui/button';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { cn } from '@/lib/utils';
import RegistrationStatusBadge from '@/components/registration/RegistrationStatusBadge';
import type { SelectedCourse } from '@/types/registrationCatalog';
import { REG_COL } from './registrationColumns';

interface Props {
  selectedCourses: SelectedCourse[];
  registrationActionsOpen: boolean;
  /** Caller provides: how to drop. Returns promise that resolves on success. */
  onRemove: (registrationId: number) => Promise<void>;
  /** Currently in-flight registrationId (for showing spinner on that row). */
  removingId: number | null;
  /** Drop error (if any). */
  removeError?: string | null;
}

export function SelectedCoursesGrid({
  selectedCourses,
  registrationActionsOpen,
  onRemove,
  removingId,
  removeError,
}: Props) {
  function canDrop(status: string): boolean {
    return (status === 'Draft' || status === 'Rejected') && registrationActionsOpen;
  }

  return (
    <div>
      {/* Section title strip */}
      <div className="flex items-center justify-between border-b bg-muted/40 px-4 py-2.5">
        <h3 className="flex items-center gap-2 text-sm font-semibold">
          <span className="h-4 w-1 rounded-full bg-green-500" />
          Seçilen Dersler
        </h3>
      </div>

      {removeError && (
        <div className="px-3 py-1">
          <Alert variant="destructive">
            <AlertDescription>{removeError}</AlertDescription>
          </Alert>
        </div>
      )}

      {selectedCourses.length === 0 ? (
        <div className="px-3 py-4 text-xs text-muted-foreground">
          Henüz ders seçilmedi.
        </div>
      ) : (
        <div className="overflow-x-auto">
          <Table className="min-w-[900px] table-fixed">
            <TableHeader>
              <TableRow className="border-b bg-muted/60 hover:bg-muted/60">
                <TableHead className="h-11 w-12 px-2 text-center text-sm font-semibold">Çıkar</TableHead>
                <TableHead className="h-11 w-12 px-2 text-center text-sm font-semibold">Şube</TableHead>
                <TableHead className="h-11 w-20 px-2 text-sm font-semibold">Ders Kodu</TableHead>
                <TableHead className={`h-11 ${REG_COL.name} px-3 text-sm font-semibold`}>Ders Adı</TableHead>
                <TableHead className="h-11 w-16 px-2 text-center text-sm font-semibold">Z/S</TableHead>
                <TableHead className="h-11 w-16 px-2 text-center text-sm font-semibold">T+U</TableHead>
                <TableHead className="h-11 w-16 px-2 text-center text-sm font-semibold">KRD</TableHead>
                <TableHead className="h-11 w-16 px-2 text-center text-sm font-semibold">AKTS</TableHead>
                <TableHead className="hidden md:table-cell h-11 w-16 px-2 text-center text-sm font-semibold">
                  Sınıf
                </TableHead>
                <TableHead className="hidden lg:table-cell h-11 px-3 text-center text-sm font-semibold">Alış</TableHead>
                <TableHead className="hidden lg:table-cell h-11 px-3 text-center text-sm font-semibold">AcNeden</TableHead>
                <TableHead className="h-11 w-32 px-2 text-center text-sm font-semibold">Durum</TableHead>
                <TableHead className="hidden xl:table-cell h-11 w-20 px-2 text-center text-sm font-semibold">Grup Kod</TableHead>
              </TableRow>
            </TableHeader>

            <TableBody>
              {selectedCourses.map((row, index) => {
                const isPending = removingId === row.registrationId;

                return (
                  <TableRow
                    key={row.registrationId}
                    className={cn(
                      'group border-b border-l-[3px] border-l-transparent transition-all duration-150',
                      'hover:bg-primary/5 hover:shadow-[inset_2px_0_0_0_hsl(var(--primary))]',
                      index % 2 === 1 && 'bg-muted/20',
                      row.takeType === 'Alttan' && 'border-l-destructive/50',
                    )}
                  >
                    {/* Çıkar */}
                    <TableCell className="w-12 py-2.5 px-3 text-center">
                      <Button
                        type="button"
                        size="icon"
                        variant="ghost"
                        className="h-7 w-7 text-destructive hover:bg-destructive/10 hover:text-destructive"
                        disabled={!canDrop(row.status) || removingId !== null}
                        title={canDrop(row.status) ? 'Dersi çıkar' : 'Bu ders çıkarılamaz'}
                        onClick={() => onRemove(row.registrationId)}
                      >
                        {isPending ? (
                          <Loader2 className="h-4 w-4 animate-spin" />
                        ) : (
                          <Trash2 className="h-4 w-4" />
                        )}
                      </Button>
                    </TableCell>

                    {/* Şube */}
                    <TableCell className="w-12 py-2.5 px-3 text-center text-sm tabular-nums text-foreground">
                      {row.sectionNumber > 0 ? row.sectionNumber : '—'}
                    </TableCell>

                    {/* Kod */}
                    <TableCell className="w-20 py-2.5 px-3 font-mono text-sm font-semibold text-primary whitespace-nowrap">
                      {row.courseCode}
                    </TableCell>

                    {/* Ad */}
                    <TableCell className={`${REG_COL.name} py-2.5 px-3 text-sm text-foreground`}>
                      {row.courseName}
                    </TableCell>

                    {/* Z/S — Zorunlu=primary, Seçmeli=amber */}
                    <TableCell className="w-16 py-2.5 px-2 text-center">
                      <span
                        className={cn(
                          'inline-flex h-6 w-6 items-center justify-center rounded-md text-xs font-bold',
                          row.isCompulsory
                            ? 'bg-primary/10 text-primary'
                            : 'bg-amber-100 text-amber-700',
                        )}
                        title={row.isCompulsory ? 'Zorunlu' : 'Seçmeli'}
                      >
                        {row.isCompulsory ? 'Z' : 'S'}
                      </span>
                    </TableCell>

                    {/* T+U */}
                    <TableCell className="w-16 py-2.5 px-2 text-center text-sm font-mono tabular-nums text-muted-foreground">
                      {row.theoryHours}+{row.labHours}
                    </TableCell>

                    {/* KRD */}
                    <TableCell className="w-16 py-2.5 px-2 text-center text-sm tabular-nums text-muted-foreground">
                      {row.nationalCredit}
                    </TableCell>

                    {/* AKTS — primary vurgulu */}
                    <TableCell className="w-16 py-2.5 px-2 text-center">
                      <span className="font-semibold tabular-nums text-primary">{row.ects}</span>
                    </TableCell>

                    {/* Sınıf */}
                    <TableCell className="hidden md:table-cell w-16 py-2.5 px-2 text-center text-sm tabular-nums text-muted-foreground">
                      {row.suggestedSemester > 0 ? row.suggestedSemester : <span className="text-muted-foreground/30">—</span>}
                    </TableCell>

                    {/* Alış — Alttan=kırmızı (olumsuz), İlkDefa=nötr */}
                    <TableCell className="hidden lg:table-cell py-2.5 px-3 text-center text-sm">
                      <span className={row.takeType === 'Alttan' ? 'font-medium text-destructive' : 'text-muted-foreground'}>
                        {row.takeType}
                      </span>
                    </TableCell>

                    {/* AcNeden */}
                    <TableCell className="hidden lg:table-cell py-2.5 px-3 text-center text-sm text-foreground">
                      {row.openingReasonName ?? <span className="text-muted-foreground/30">—</span>}
                    </TableCell>

                    {/* Durum */}
                    <TableCell className="w-32 py-2.5 px-3 text-center">
                      <RegistrationStatusBadge status={row.status} />
                    </TableCell>

                    {/* Grup Kod */}
                    <TableCell className="hidden xl:table-cell w-20 py-2.5 px-3 text-center text-sm font-mono text-orange-700">
                      {row.electiveGroupCode ?? <span className="font-sans text-muted-foreground/30">—</span>}
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
