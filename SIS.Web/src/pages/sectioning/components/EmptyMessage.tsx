import { cn } from '@/lib/utils';

interface EmptyMessageProps {
  text: string;
  emphasis?: boolean;
}

export function EmptyMessage({ text, emphasis = false }: EmptyMessageProps) {
  return (
    <div
      className={cn(
        'flex items-center justify-center py-16 text-sm',
        emphasis ? 'text-gray-600 font-medium' : 'text-gray-400',
      )}
    >
      {text}
    </div>
  );
}
