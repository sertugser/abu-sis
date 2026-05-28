import { cn } from '@/lib/utils';
import { Progress } from '@/components/ui/progress';

interface Props {
  totalCredit: number;
  totalEcts: number;
  ectsLimit: number;
  totalHours: number;
  courseCount: number;
}

export function RegistrationSummaryBar({
  totalCredit,
  totalEcts,
  ectsLimit,
  totalHours,
  courseCount,
}: Props) {
  const isOver = totalEcts > ectsLimit;
  const progressValue = Math.min(100, (totalEcts / Math.max(1, ectsLimit)) * 100);

  return (
    <div className="bg-muted/40 border-t border-border/40 px-4 py-2 flex flex-wrap items-center gap-6 text-sm">
      <span>
        <span className="text-muted-foreground text-xs">Toplam Kredi: </span>
        <strong className="tabular-nums text-xs">{totalCredit}</strong>
      </span>

      <span>
        <span className="text-muted-foreground text-xs">AKTS: </span>
        <strong
          className={cn(
            'tabular-nums text-xs',
            isOver && 'text-destructive font-bold',
          )}
        >
          {totalEcts} / {ectsLimit}
        </strong>
      </span>

      <span>
        <span className="text-muted-foreground text-xs">Saat: </span>
        <strong className="tabular-nums text-xs">{totalHours}</strong>
      </span>

      <span>
        <span className="text-muted-foreground text-xs">Ders Sayısı: </span>
        <strong className="tabular-nums text-xs">{courseCount}</strong>
      </span>

      <Progress
        value={progressValue}
        className="h-1.5 w-24"
        indicatorClassName={cn(
          'bg-primary',
          isOver && 'bg-destructive',
        )}
      />
    </div>
  );
}
