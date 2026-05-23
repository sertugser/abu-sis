import { AlertTriangle, Info } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { cn } from '@/lib/utils';
import type { PendingConfirm } from '@/lib/calendarGenerator/types';

interface ConfirmDialogProps {
  confirm: PendingConfirm | null;
  onClose: (value: boolean) => void;
}

export function ConfirmDialog({ confirm, onClose }: ConfirmDialogProps) {
  if (!confirm) return null;

  const tone = confirm.tone ?? 'default';

  return (
    <div className="fixed inset-0 z-[160] flex items-center justify-center bg-slate-950/50 px-4 py-6 backdrop-blur-sm">
      <div
        className="w-full max-w-lg rounded-xl border border-slate-200 bg-white p-5 shadow-2xl animate-in fade-in-0 zoom-in-95 duration-200"
        role="dialog"
        aria-modal="true"
        aria-labelledby="calendar-confirm-title"
      >
        <div className="flex items-start gap-3">
          {tone !== 'default' && (
            <span
              className={cn(
                'mt-0.5 flex h-9 w-9 shrink-0 items-center justify-center rounded-full',
                tone === 'danger' ? 'bg-red-100 text-red-600' : 'bg-cyan-100 text-cyan-700',
              )}
            >
              {tone === 'danger' ? <AlertTriangle className="h-5 w-5" /> : <Info className="h-5 w-5" />}
            </span>
          )}
          <div className="min-w-0 flex-1">
            <h2 id="calendar-confirm-title" className="text-lg font-semibold text-slate-900">
              {confirm.title}
            </h2>
            <p className="mt-2 whitespace-pre-line text-sm leading-6 text-slate-600">
              {confirm.message}
            </p>
          </div>
        </div>
        <div className="mt-5 flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
          {confirm.showCancel !== false && (
            <Button type="button" variant="outline" onClick={() => onClose(false)}>
              {confirm.cancelLabel ?? 'Vazgeç'}
            </Button>
          )}
          <Button
            type="button"
            variant={tone === 'danger' ? 'destructive' : 'default'}
            className={tone === 'info' ? 'bg-cyan-700 hover:bg-cyan-800' : undefined}
            onClick={() => onClose(true)}
          >
            {confirm.confirmLabel}
          </Button>
        </div>
      </div>
    </div>
  );
}
