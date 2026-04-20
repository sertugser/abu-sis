import { Star } from 'lucide-react';
import { cn } from '@/lib/utils';

interface StarRatingProps {
  value: number | null;
  onChange?: (value: number) => void;
  readOnly?: boolean;
  size?: 'sm' | 'md' | 'lg';
}

const SIZE_MAP = {
  sm: 'h-4 w-4',
  md: 'h-6 w-6',
  lg: 'h-8 w-8',
};

export default function StarRating({ value, onChange, readOnly, size = 'md' }: StarRatingProps) {
  const disabled = readOnly || !onChange;

  return (
    <div className="flex items-center gap-1" role="radiogroup">
      {[1, 2, 3, 4, 5].map((n) => {
        const filled = value != null && n <= value;
        return (
          <button
            key={n}
            type="button"
            role="radio"
            aria-checked={value === n}
            aria-label={`${n} yıldız`}
            disabled={disabled}
            onClick={() => onChange?.(n)}
            className={cn(
              'transition-transform',
              !disabled && 'hover:scale-110 cursor-pointer',
              disabled && 'cursor-default'
            )}
          >
            <Star
              className={cn(
                SIZE_MAP[size],
                filled ? 'fill-amber-400 text-amber-400' : 'fill-none text-muted-foreground/40'
              )}
            />
          </button>
        );
      })}
    </div>
  );
}
