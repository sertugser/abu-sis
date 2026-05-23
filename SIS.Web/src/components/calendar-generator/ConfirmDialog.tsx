import { Button } from '@/components/ui/button';
import type { PendingConfirm } from '@/lib/calendarGenerator/types';

interface ConfirmDialogProps {
  confirm: PendingConfirm | null;
  onClose: (value: boolean) => void;
}

export function ConfirmDialog({ confirm, onClose }: ConfirmDialogProps) {
  if (!confirm) return null;

  return (
    <div className="fixed inset-0 z-[160] flex items-center justify-center bg-slate-950/50 px-4 py-6">
      <div
        className="w-full max-w-lg rounded-lg border border-slate-200 bg-white p-5 shadow-xl"
        role="dialog"
        aria-modal="true"
        aria-labelledby="calendar-confirm-title"
      >
        <h2 id="calendar-confirm-title" className="text-lg font-semibold text-slate-900">
          {confirm.title}
        </h2>
        <p className="mt-3 whitespace-pre-line text-sm leading-6 text-slate-600">
          {confirm.message}
        </p>
        <div className="mt-5 flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
          {confirm.showCancel !== false && (
            <Button type="button" variant="outline" onClick={() => onClose(false)}>
              {confirm.cancelLabel ?? 'Vazgeç'}
            </Button>
          )}
          <Button
            type="button"
            variant={confirm.tone === 'danger' ? 'destructive' : 'default'}
            className={confirm.tone === 'info' ? 'bg-cyan-700 hover:bg-cyan-800' : undefined}
            onClick={() => onClose(true)}
          >
            {confirm.confirmLabel}
          </Button>
        </div>
      </div>
    </div>
  );
}
