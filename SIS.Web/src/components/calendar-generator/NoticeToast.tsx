import { cn } from '@/lib/utils';
import type { NoticeTone } from '@/lib/calendarGenerator/types';

interface NoticeToastProps {
  notice: string | null;
  tone: NoticeTone;
}

export function NoticeToast({ notice, tone }: NoticeToastProps) {
  if (!notice) return null;

  return (
    <div
      role="status"
      aria-live="polite"
      className={cn(
        'fixed right-4 top-4 z-[140] max-h-[40vh] w-[min(calc(100vw-2rem),32rem)] overflow-auto rounded-lg border px-4 py-3 text-sm shadow-xl',
        tone === 'success' && 'border-emerald-200 bg-emerald-50 text-emerald-900',
        tone === 'error' && 'border-red-200 bg-red-50 text-red-900',
        tone === 'info' && 'border-slate-200 bg-white text-slate-700',
      )}
    >
      {notice}
    </div>
  );
}
