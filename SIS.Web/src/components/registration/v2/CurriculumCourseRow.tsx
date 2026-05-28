import { Plus, AlertTriangle } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { TableCell, TableRow } from '@/components/ui/table';
import { cn } from '@/lib/utils';
import type { CatalogCourseRow } from '@/types/registrationCatalog';

interface Props {
  row: CatalogCourseRow;
  onPickSection: (course: CatalogCourseRow) => void;
  registrationActionsOpen: boolean;
}

function pickButtonTitle(row: CatalogCourseRow, registrationActionsOpen: boolean): string {
  if (!row.hasOpenSections) return 'Bu dönem açılmadı';
  if (!registrationActionsOpen) return 'Kayıt kapalı';
  return 'Şube seç';
}

export function CurriculumCourseRow({ row, onPickSection, registrationActionsOpen }: Props) {
  const isAlttan = row.takeStatus === 'Alttan';
  const pickDisabled = !row.hasOpenSections || !registrationActionsOpen;

  return (
    <TableRow
      className={cn(
        'transition-colors hover:bg-muted/30',
        isAlttan && 'bg-destructive/5 border-l-2 border-destructive/40',
      )}
    >
      {/* + / section picker */}
      <TableCell className="w-10 py-2 px-2 text-center">
        <Button
          size="icon"
          variant="ghost"
          className="h-7 w-7"
          disabled={pickDisabled}
          title={pickButtonTitle(row, registrationActionsOpen)}
          onClick={() => onPickSection(row)}
        >
          <Plus className="h-3.5 w-3.5" />
        </Button>
      </TableCell>

      {/* Ders Kodu */}
      <TableCell className="w-28 py-2 px-2 font-mono text-xs font-semibold text-primary whitespace-nowrap">
        {row.code}
      </TableCell>

      {/* Ders Adı */}
      <TableCell className="py-2 px-2">
        <span className="text-sm">
          {row.name}
          {!row.prerequisitesMet && (
            <AlertTriangle
              className="inline h-3 w-3 text-amber-500 ml-1 align-text-bottom"
              title="Önkoşul karşılanmamış"
            />
          )}
        </span>
        {row.applicablePoolSlots.length > 0 && (
          <Badge
            variant="outline"
            className="ml-1.5 bg-orange-50 text-orange-700 border-orange-200 font-normal text-[10px] px-1.5 py-0"
            title={row.applicablePoolSlots[0].slotName}
          >
            Grup
          </Badge>
        )}
      </TableCell>

      {/* Z/S */}
      <TableCell className="w-12 py-2 px-2 text-center text-xs font-bold">
        <span className={row.isCompulsory ? 'text-primary' : 'text-muted-foreground'}>
          {row.isCompulsory ? 'Z' : 'S'}
        </span>
      </TableCell>

      {/* KRD */}
      <TableCell className="w-12 py-2 px-2 text-center tabular-nums text-xs">
        {row.nationalCredit}
      </TableCell>

      {/* AKTS */}
      <TableCell className="w-14 py-2 px-2 text-center">
        <Badge variant="secondary" className="font-mono text-[11px] px-1.5 py-0">
          {row.ects}
        </Badge>
      </TableCell>

      {/* Sınıf (yarıyıl) */}
      <TableCell className="hidden md:table-cell w-14 py-2 px-2 text-center tabular-nums text-xs text-muted-foreground">
        {row.suggestedSemester > 0 ? row.suggestedSemester : '—'}
      </TableCell>

      {/* Önceki Tekrar Durumu */}
      <TableCell className="hidden lg:table-cell py-2 px-2 text-xs">
        <span className={isAlttan ? 'text-destructive' : 'text-muted-foreground'}>
          {row.previousAttemptInfo ?? '—'}
        </span>
      </TableCell>

      {/* Aktif Kayıt Durumu */}
      <TableCell className="w-32 py-2 px-2 text-center text-xs">
        {row.activeRegistrationStatus ? (
          <Badge variant="success" className="text-[11px] px-1.5 py-0">
            {row.activeRegistrationStatus}
          </Badge>
        ) : (
          <span className="text-muted-foreground">—</span>
        )}
      </TableCell>
    </TableRow>
  );
}
