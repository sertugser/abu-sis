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
  index: number;
}

function pickButtonTitle(row: CatalogCourseRow, registrationActionsOpen: boolean): string {
  if (!row.hasOpenSections) return 'Bu dönem açılmadı';
  if (!registrationActionsOpen) return 'Kayıt kapalı';
  return 'Şube seç';
}

export function CurriculumCourseRow({ row, onPickSection, registrationActionsOpen, index }: Props) {
  const isAlttan = row.takeStatus === 'Alttan';
  const pickDisabled = !row.hasOpenSections || !registrationActionsOpen;

  return (
    <TableRow
      className={cn(
        'border-b border-slate-200 transition-colors hover:bg-[#dce9ff]',
        index % 2 === 1 && 'bg-slate-50/60',
        isAlttan && 'border-l-2 border-l-destructive/40',
      )}
    >
      {/* + / section picker */}
      <TableCell className="w-10 py-2 px-2 text-center">
        <Button
          size="icon"
          variant="ghost"
          className={cn(
            'h-6 w-6 rounded-full disabled:opacity-100',
            pickDisabled
              ? 'bg-slate-200 hover:bg-slate-200'
              : 'bg-emerald-500 hover:bg-emerald-600',
          )}
          disabled={pickDisabled}
          title={pickButtonTitle(row, registrationActionsOpen)}
          onClick={() => onPickSection(row)}
        >
          <Plus className="h-3.5 w-3.5 text-white" />
        </Button>
      </TableCell>

      {/* Ders Kodu */}
      <TableCell className="w-28 py-2 px-2 font-mono text-[15px] font-semibold text-primary whitespace-nowrap">
        {row.code}
      </TableCell>

      {/* Ders Adı */}
      <TableCell className="py-2 px-2 text-[15px] text-slate-700">
        <span>
          {row.name}
          {!row.prerequisitesMet && (
            <span title="Önkoşul karşılanmamış" className="inline-flex align-text-bottom">
              <AlertTriangle className="inline h-3 w-3 text-amber-500 ml-1" />
            </span>
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
      <TableCell className="w-12 py-2 px-2 text-center text-[15px] font-bold text-slate-700">
        <span className={row.isCompulsory ? 'text-primary' : 'text-muted-foreground'}>
          {row.isCompulsory ? 'Z' : 'S'}
        </span>
      </TableCell>

      {/* KRD */}
      <TableCell className="w-12 py-2 px-2 text-center tabular-nums text-[15px] text-slate-700">
        {row.nationalCredit}
      </TableCell>

      {/* AKTS */}
      <TableCell className="w-14 py-2 px-2 text-center text-[15px] text-slate-700">
        {row.ects}
      </TableCell>

      {/* Sınıf (yarıyıl) */}
      <TableCell className="hidden md:table-cell w-14 py-2 px-2 text-center tabular-nums text-[15px] text-slate-700">
        {row.suggestedSemester > 0 ? row.suggestedSemester : '—'}
      </TableCell>

      {/* Önceki Tekrar Durumu */}
      <TableCell className="hidden lg:table-cell py-2 px-2 text-[15px] text-slate-700">
        <span className={isAlttan ? 'text-destructive' : 'text-muted-foreground'}>
          {row.previousAttemptInfo ?? '—'}
        </span>
      </TableCell>

      {/* Aktif Kayıt Durumu */}
      <TableCell className="w-32 py-2 px-2 text-center text-[15px] text-slate-700">
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
