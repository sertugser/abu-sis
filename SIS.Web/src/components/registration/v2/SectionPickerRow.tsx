import { Plus } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { TableCell, TableRow } from '@/components/ui/table';
import { prolizPrimaryButtonClass } from '@/lib/prolizButton';
import type { SectionPickerRow as SectionPickerRowType } from '@/types/registrationCatalog';

function shortLabelFromReason(reason: string | null | undefined): string {
  if (!reason) return 'Uygun değil';
  if (reason.includes('düzenlemede') || reason.includes('EnrollmentSuspended')) return 'Geçici kapalı';
  if (reason.includes('yayınlanmadı') || reason.includes('FacultyScheduleNotApproved')) return 'Yayında değil';
  if (reason.includes('Önkoşul') || reason.includes('Prerequisite')) return 'Önkoşul';
  return 'Uygun değil';
}

interface Props {
  row: SectionPickerRowType;
  onAdd: (sectionId: number) => void;
  isAdding: boolean;
  isInPlan: boolean;
  registrationActionsOpen: boolean;
}

export function SectionPickerRow({ row, onAdd, isAdding, isInPlan, registrationActionsOpen }: Props) {
  const isFull = row.availableSeats <= 0 && row.quotaVisible;

  function renderAction() {
    if (isInPlan) {
      return <Badge variant="success" className="text-[10px]">Planda</Badge>;
    }
    if (!row.isEligible) {
      return (
        <Badge
          variant="destructive"
          className="text-[10px]"
          title={row.ineligibleReason ?? undefined}
        >
          {shortLabelFromReason(row.ineligibleReason)}
        </Badge>
      );
    }
    if (isFull) {
      return <Badge variant="destructive" className="text-[10px]">Dolu</Badge>;
    }
    if (!registrationActionsOpen) {
      return <Badge variant="secondary" className="text-[10px]">Kayıt kapalı</Badge>;
    }
    return (
      <button
        type="button"
        className={prolizPrimaryButtonClass}
        onClick={() => onAdd(row.sectionId)}
        disabled={isAdding}
      >
        <Plus className="h-3 w-3" />
        Ekle
      </button>
    );
  }

  return (
    <TableRow className="transition-colors hover:bg-muted/30">
      {/* Şube */}
      <TableCell className="w-12 py-2 px-2 text-center text-xs tabular-nums">
        {row.sectionNumber}
      </TableCell>

      {/* Ders Kodu */}
      <TableCell className="w-24 py-2 px-2 font-mono text-xs font-semibold text-primary whitespace-nowrap">
        {row.code}
      </TableCell>

      {/* Ders Adı + schedule */}
      <TableCell className="py-2 px-2">
        <span className="text-xs">{row.name}</span>
        {row.schedule && (
          <span className="block text-[10px] text-muted-foreground">{row.schedule}</span>
        )}
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

      {/* KRD */}
      <TableCell className="w-10 py-2 px-2 text-center text-xs tabular-nums">
        {row.nationalCredit}
      </TableCell>

      {/* AKTS */}
      <TableCell className="w-12 py-2 px-2 text-center">
        <Badge variant="secondary" className="font-mono text-[11px] px-1.5 py-0">
          {row.ects}
        </Badge>
      </TableCell>

      {/* Öğretim Üyesi */}
      <TableCell className="py-2 px-2 text-xs">
        {row.instructorName || '—'}
      </TableCell>

      {/* Sınıf */}
      <TableCell className="hidden md:table-cell w-12 py-2 px-2 text-center text-xs tabular-nums">
        {row.classYear || '—'}
      </TableCell>

      {/* AcNeden */}
      <TableCell className="hidden lg:table-cell py-2 px-2 text-xs text-muted-foreground">
        {row.openingReasonName ?? '—'}
      </TableCell>

      {/* Alma Tipi */}
      <TableCell className="hidden lg:table-cell py-2 px-2 text-xs text-muted-foreground">
        {row.takeTypeLabel}
      </TableCell>

      {/* Kontenjan */}
      <TableCell className="w-20 py-2 px-2 text-center text-xs">
        {row.quotaVisible ? (
          <span
            className={`font-mono tabular-nums ${isFull ? 'text-destructive' : 'text-foreground'}`}
          >
            {row.enrolledCount}/{row.capacity}
          </span>
        ) : (
          <span className="text-muted-foreground text-[10px]">Gizli</span>
        )}
      </TableCell>

      {/* Grup Kod */}
      <TableCell className="hidden xl:table-cell w-20 py-2 px-2 text-xs font-mono text-orange-700">
        {row.groupCode ?? '—'}
      </TableCell>

      {/* Aksiyon */}
      <TableCell className="w-28 py-2 px-2 text-right">
        {renderAction()}
      </TableCell>
    </TableRow>
  );
}
