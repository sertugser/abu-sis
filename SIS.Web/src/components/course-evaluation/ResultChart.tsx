import { cn } from '@/lib/utils';

interface ResultChartProps {
  distribution: Record<string, number>;
  totalResponses: number;
}

export default function ResultChart({ distribution, totalResponses }: ResultChartProps) {
  if (totalResponses <= 0) return null;

  return (
    <div className="space-y-1.5">
      {[5, 4, 3, 2, 1].map((rating) => {
        const count = distribution[String(rating)] ?? 0;
        const percent = totalResponses > 0 ? (count / totalResponses) * 100 : 0;
        return (
          <div key={rating} className="flex items-center gap-2 text-xs">
            <span className="w-6 text-muted-foreground font-medium">{rating}★</span>
            <div className="flex-1 h-2 bg-muted rounded-full overflow-hidden">
              <div
                className={cn(
                  'h-full transition-all',
                  rating >= 4 ? 'bg-emerald-500' : rating === 3 ? 'bg-amber-500' : 'bg-red-500'
                )}
                style={{ width: `${percent}%` }}
              />
            </div>
            <span className="w-10 text-right text-muted-foreground tabular-nums">{count}</span>
          </div>
        );
      })}
    </div>
  );
}
