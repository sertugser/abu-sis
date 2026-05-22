import { useEffect, useState } from 'react';
import { AlertTriangle, ShieldAlert } from 'lucide-react';
import { cn } from '@/lib/utils';
import {
  AlertDialog, AlertDialogAction, AlertDialogCancel, AlertDialogContent,
  AlertDialogDescription, AlertDialogFooter, AlertDialogHeader, AlertDialogTitle,
} from '@/components/ui/alert-dialog';

export type DelayedConfirmSeverity = 'warning' | 'danger';

export interface DelayedConfirmDialogItem {
  /** Liste girişi için unique key (id veya index). */
  key: string | number;
  /** Tek satırlık görsel etiket — backend'den gelen mesaj olmalı, hardcoded değil. */
  label: string;
}

export interface DelayedConfirmDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  /** Dialog başlığı — kullanıcının ilk gördüğü şey, kısa ve net. */
  title: string;
  /** Üst açıklama — ne olacağını dinamik söyler ("X kart silinecek" gibi). */
  description?: React.ReactNode;
  /** Backend'den gelen detaylar (conflict mesajları, etkilenecek kayıt listesi). DB-driven, jenerik metin değil. */
  items?: DelayedConfirmDialogItem[];
  /** Devam butonu metni. Geri sayım sırasında parantez içinde saniye eklenir. */
  confirmLabel: string;
  cancelLabel?: string;
  onConfirm: () => void;
  onCancel?: () => void;
  /** Onay butonunun kilitli kalacağı saniye. Default 5 — kullanıcı uyarıları okusun. */
  delaySeconds?: number;
  /** Renk teması. `warning` (amber) — kural ihlali, override; `danger` (kırmızı) — silme/sıfırlama. */
  severity?: DelayedConfirmSeverity;
  /** Alt italik açıklama — kararın sonucunu bildirir ("Bu işlem geri alınamaz" gibi). */
  footnote?: React.ReactNode;
}

/**
 * Kritik kararların reflexle onaylanmaması için zaman gecikmeli onay dialog'u.
 *
 * Dialog açılır açılmaz "Devam" butonu `delaySeconds` boyunca kilitlidir; üzerinde geri sayım
 * gösterilir. Kullanıcı uyarıları okuyana kadar tıklayamaz. İptal butonu her zaman aktiftir
 * — kullanıcı vazgeçmek isterse beklemek zorunda kalmaz.
 *
 * Yıkıcı (delete/reset/cancel) işlemler için `severity='danger'` ile kırmızı tema;
 * kural ihlali override gibi durumlar için `severity='warning'` ile amber.
 */
export function DelayedConfirmDialog({
  open, onOpenChange,
  title, description, items, footnote,
  confirmLabel, cancelLabel = 'İptal',
  onConfirm, onCancel,
  delaySeconds = 5,
  severity = 'warning',
}: DelayedConfirmDialogProps) {
  const [countdown, setCountdown] = useState(0);

  useEffect(() => {
    if (!open) {
      setCountdown(0);
      return;
    }
    setCountdown(delaySeconds);
    const interval = setInterval(() => {
      setCountdown(prev => (prev <= 1 ? 0 : prev - 1));
    }, 1000);
    return () => clearInterval(interval);
  }, [open, delaySeconds]);

  const handleOpenChange = (next: boolean) => {
    if (!next && onCancel) onCancel();
    onOpenChange(next);
  };

  const handleCancel = () => {
    if (onCancel) onCancel();
    onOpenChange(false);
  };

  const isWarning = severity === 'warning';
  const Icon = isWarning ? AlertTriangle : ShieldAlert;

  return (
    <AlertDialog open={open} onOpenChange={handleOpenChange}>
      <AlertDialogContent className="max-w-2xl">
        <AlertDialogHeader>
          <AlertDialogTitle className="flex items-center gap-2">
            <Icon className={cn(
              'h-5 w-5',
              isWarning ? 'text-amber-500' : 'text-red-600',
            )} />
            {title}
          </AlertDialogTitle>
          <AlertDialogDescription asChild>
            <div className="space-y-3 mt-2">
              {description && (
                <div className="text-sm text-slate-700">{description}</div>
              )}
              {items && items.length > 0 && (
                <ul className="space-y-2 max-h-64 overflow-y-auto">
                  {items.map(item => (
                    <li
                      key={item.key}
                      className={cn(
                        'flex gap-2 text-sm border rounded px-3 py-2',
                        isWarning
                          ? 'bg-amber-50 border-amber-200'
                          : 'bg-red-50 border-red-200',
                      )}
                    >
                      <Icon className={cn(
                        'h-4 w-4 shrink-0 mt-0.5',
                        isWarning ? 'text-amber-600' : 'text-red-600',
                      )} />
                      <span className="text-slate-800">{item.label}</span>
                    </li>
                  ))}
                </ul>
              )}
              {footnote && (
                <p className="text-xs text-slate-500 italic">{footnote}</p>
              )}
              {countdown > 0 && (
                <p className={cn(
                  'text-xs font-medium rounded px-2 py-1',
                  isWarning
                    ? 'text-amber-700 bg-amber-100/60 border border-amber-200'
                    : 'text-red-700 bg-red-100/60 border border-red-200',
                )}>
                  Lütfen yukarıdaki uyarıları okuyun — "{confirmLabel}" butonu {countdown} saniye sonra aktif olacak.
                </p>
              )}
            </div>
          </AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel onClick={handleCancel}>{cancelLabel}</AlertDialogCancel>
          <AlertDialogAction
            onClick={onConfirm}
            disabled={countdown > 0}
            aria-disabled={countdown > 0}
            title={countdown > 0 ? 'Uyarıyı okumanız için lütfen bekleyin' : undefined}
            className={cn(
              isWarning
                ? 'bg-amber-600 hover:bg-amber-700 focus-visible:ring-amber-500'
                : 'bg-red-600 hover:bg-red-700 focus-visible:ring-red-500',
              countdown > 0 && cn(
                'opacity-60 cursor-not-allowed',
                isWarning ? 'hover:bg-amber-600' : 'hover:bg-red-600',
              ),
            )}
          >
            {countdown > 0 ? `${confirmLabel} (${countdown})` : confirmLabel}
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}
