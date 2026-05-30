import { Plus, AlertTriangle, CheckCircle2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { TableCell, TableRow } from '@/components/ui/table';
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/tooltip';
import { cn } from '@/lib/utils';
import type { CatalogCourseRow } from '@/types/registrationCatalog';
import { REG_COL } from './registrationColumns';

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

  const isRegistered = Boolean(row.activeRegistrationStatus);

  return (
    <TableRow
      className={cn(
        'group border-b border-l-[3px] border-l-transparent transition-all duration-150',
        'hover:bg-primary/5 hover:shadow-[inset_2px_0_0_0_hsl(var(--primary))]',
        index % 2 === 1 && 'bg-muted/20',
        isRegistered && 'border-l-green-500 bg-green-50/40',
        isAlttan && !isRegistered && 'border-l-destructive/50',
      )}
    >
      {/* + / section picker */}
      <TableCell className="w-12 py-2.5 px-3 text-center">
        <Button
          size="icon"
          variant="ghost"
          className={cn(
            'h-7 w-7 rounded-md transition-all duration-150 disabled:opacity-100',
            pickDisabled
              ? 'bg-muted text-muted-foreground/40'
              : 'bg-emerald-500 text-white shadow-sm hover:bg-emerald-600 hover:shadow-md active:scale-95',
          )}
          disabled={pickDisabled}
          title={pickButtonTitle(row, registrationActionsOpen)}
          onClick={() => onPickSection(row)}
        >
          <Plus className="h-4 w-4" strokeWidth={2.5} />
        </Button>
      </TableCell>

      {/* Ders Kodu */}
      <TableCell className="w-32 py-2.5 px-3 font-mono text-sm font-semibold text-primary whitespace-nowrap">
        {row.code}
      </TableCell>

      {/* Ders Adı */}
      <TableCell className={`${REG_COL.name} py-2.5 px-3 text-sm text-foreground`}>
        <span>
          {row.name}
          {!row.prerequisitesMet && (
            <TooltipProvider delayDuration={100}>
              <Tooltip>
                <TooltipTrigger asChild>
                  <span className="ml-1.5 inline-flex h-6 w-6 animate-pulse items-center justify-center rounded-full bg-amber-100 align-text-bottom ring-1 ring-amber-300 transition-transform hover:scale-110">
                    <AlertTriangle className="h-3.5 w-3.5 text-amber-600" />
                  </span>
                </TooltipTrigger>
                <TooltipContent side="top" className="max-w-xs">
                  <p className="font-semibold">Önkoşul karşılanmamış</p>
                  {row.missingPrerequisites.length > 0 ? (
                    <p className="text-xs">
                      Önce şu ders(ler)i başarıyla geçmelisiniz:{' '}
                      <span className="font-mono font-medium">
                        {row.missingPrerequisites.join(', ')}
                      </span>
                    </p>
                  ) : (
                    <p className="text-xs">
                      Bu dersin önkoşul gereksinimleri henüz karşılanmamış.
                    </p>
                  )}
                </TooltipContent>
              </Tooltip>
            </TooltipProvider>
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

      {/* Z/S — Zorunlu=primary, Seçmeli=amber (anlam taşıyan renk) */}
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
      <TableCell className="w-16 py-2.5 px-2 text-center tabular-nums text-sm text-muted-foreground">
        {row.nationalCredit}
      </TableCell>

      {/* AKTS — en önemli sayı, primary vurgulu */}
      <TableCell className="w-16 py-2.5 px-2 text-center">
        <span className="font-semibold tabular-nums text-primary">{row.ects}</span>
      </TableCell>

      {/* Sınıf (yarıyıl) */}
      <TableCell className="hidden md:table-cell w-16 py-2.5 px-2 text-center tabular-nums text-sm text-muted-foreground">
        {row.suggestedSemester > 0 ? row.suggestedSemester : <span className="text-muted-foreground/30">—</span>}
      </TableCell>

      {/* Önceki Tekrar Durumu */}
      <TableCell className="hidden lg:table-cell py-2.5 px-3 text-center text-sm">
        {row.previousAttemptInfo ? (
          <span className={isAlttan ? 'font-medium text-destructive' : 'text-foreground'}>
            {row.previousAttemptInfo}
          </span>
        ) : (
          <span className="text-muted-foreground/30">—</span>
        )}
      </TableCell>

      {/* Aktif Kayıt Durumu */}
      <TableCell className="w-32 py-2.5 px-3 text-center text-sm">
        {row.activeRegistrationStatus ? (
          <Badge variant="successSoft" className="gap-1 text-[10px] px-1.5 py-0 h-5">
            <CheckCircle2 className="h-3 w-3" />
            {row.activeRegistrationStatus}
          </Badge>
        ) : (
          <span className="text-muted-foreground/30">—</span>
        )}
      </TableCell>
    </TableRow>
  );
}
