import { AlertTriangle, CheckCircle2, Info } from 'lucide-react';
import { cn } from '@/lib/utils';
import type { NoticeTone } from '@/lib/calendarGenerator/types';

interface NoticeToastProps {
  notice: string | null;
  tone: NoticeTone;
}

const TONE_ICON = {
  success: CheckCircle2,
  error: AlertTriangle,
  info: Info,
} as const;

export function NoticeToast({ notice, tone }: NoticeToastProps) {
  if (!notice) return null;

  const Icon = TONE_ICON[tone];

  return (
    <div
      role="status"
      aria-live="polite"
      className={cn(
        'fixed right-4 top-4 z-[140] flex max-h-[40vh] w-[min(calc(100vw-2rem),32rem)] items-start gap-2.5 overflow-auto rounded-xl border px-4 py-3 text-sm shadow-xl',
        'animate-in fade-in-0 slide-in-from-top-2 duration-200',
        tone === 'success' && 'border-emerald-200 bg-emerald-50 text-emerald-900',
        tone === 'error' && 'border-red-200 bg-red-50 text-red-900',
        tone === 'info' && 'border-slate-200 bg-white text-slate-700',
      )}
    >
      <Icon
        className={cn(
          'mt-0.5 h-4 w-4 shrink-0',
          tone === 'success' && 'text-emerald-600',
          tone === 'error' && 'text-red-600',
          tone === 'info' && 'text-[#003366]',
        )}
      />
      <span className="min-w-0">{notice}</span>
    </div>
  );
}
