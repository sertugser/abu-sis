import { Progress } from '@/components/ui/progress';
import { cn } from '@/lib/utils';

interface CapacityBarProps {
  enrolled: number;
  capacity: number;
}

export default function CapacityBar({ enrolled, capacity }: CapacityBarProps) {
  const percentage = capacity > 0 ? (enrolled / capacity) * 100 : 0;

  const indicatorColor =
    percentage >= 100 ? 'bg-destructive' :
    percentage >= 75  ? 'bg-warning' :
                        'bg-success';

  return (
    <div className="flex items-center gap-2.5">
      <Progress
        value={Math.min(percentage, 100)}
        className="h-2 w-24 rounded-full"
        indicatorClassName={cn(indicatorColor, )}
      />
      <span className="text-xs font-mono text-muted-foreground tabular-nums">
        {enrolled}/{capacity}
      </span>
    </div>
  );
}
