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
    <div className="border-t bg-muted/30 px-4 py-3">
      <div className="flex flex-wrap items-center gap-x-6 gap-y-2">
        <span className="text-sm">
          <span className="text-muted-foreground">Toplam Kredi: </span>
          <strong className="tabular-nums">{totalCredit}</strong>
        </span>

        <span className="text-sm">
          <span className="text-muted-foreground">AKTS: </span>
          <strong
            className={cn(
              'tabular-nums',
              isOver && 'text-destructive font-bold',
            )}
          >
            {totalEcts} / {ectsLimit}
          </strong>
        </span>

        <span className="text-sm">
          <span className="text-muted-foreground">Saat: </span>
          <strong className="tabular-nums">{totalHours}</strong>
        </span>

        <span className="text-sm">
          <span className="text-muted-foreground">Ders Sayısı: </span>
          <strong className="tabular-nums">{courseCount}</strong>
        </span>

        <Progress
          value={progressValue}
          className="h-1.5 w-32"
          indicatorClassName={cn(
            'bg-primary',
            isOver && 'bg-destructive',
          )}
        />
      </div>
    </div>
  );
}
