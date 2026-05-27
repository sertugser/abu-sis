import type { ReactNode } from 'react';
import { AlertTriangle, Loader2, Trash2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from '@/components/ui/dialog';

export interface DeletionStat {
  label: string;
  value: number;
}

interface AcademicDeleteDialogProps {
  open: boolean;
  /** Başlık, örn. "Fakülteyi Sil". */
  title: string;
  /** Giriş cümlesi (ad vurgusu içerebilir). */
  intro: ReactNode;
  /** Bağlı kayıt sayıları (yüklendiğinde gösterilir). */
  stats: DeletionStat[] | undefined;
  /** Backend guard sonucu; undefined iken yükleniyor demektir. */
  canDelete: boolean | undefined;
  isLoading: boolean;
  isError: boolean;
  /** canDelete=false iken gösterilecek "neden silinemez" açıklaması. */
  blockReason: ReactNode;
  /** canDelete=true iken gösterilecek onay metni. */
  emptyReason: ReactNode;
  onClose: () => void;
  onConfirm: () => void;
  isPending: boolean;
}

function Stat({ label, value }: DeletionStat) {
  const hasValue = value > 0;
  return (
    <div
      className={`rounded-lg border px-3 py-2 ${
        hasValue ? 'border-amber-300 bg-amber-50/60' : 'border-slate-200 bg-slate-50/60'
      }`}
    >
      <div className="text-xs text-muted-foreground">{label}</div>
      <div className={`text-xl font-semibold tabular-nums ${hasValue ? 'text-amber-800' : 'text-slate-400'}`}>
        {value}
      </div>
    </div>
  );
}

/**
 * Akademik yapı (Fakülte/Bölüm/Program) için ortak silme onay dialog'u.
 * Hardcoded mesaj yerine ilgili /deletion-summary endpoint'inden gelen gerçek
 * sayıları gösterir; canDelete=false ise silme butonunu kilitler.
 */
export function AcademicDeleteDialog({
  open,
  title,
  intro,
  stats,
  canDelete,
  isLoading,
  isError,
  blockReason,
  emptyReason,
  onClose,
  onConfirm,
  isPending,
}: AcademicDeleteDialogProps) {
  const confirmDisabled = isLoading || isError || !canDelete || isPending;

  return (
    <Dialog open={open} onOpenChange={o => !o && onClose()}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <AlertTriangle className="h-5 w-5 text-amber-500" />
            {title}
          </DialogTitle>
        </DialogHeader>

        <div className="space-y-4">
          <p className="text-sm text-muted-foreground">{intro}</p>

          {isLoading && (
            <div className="flex items-center gap-2 text-sm text-muted-foreground py-2">
              <Loader2 className="h-4 w-4 animate-spin" />
              Bağlı kayıtlar hesaplanıyor...
            </div>
          )}

          {isError && (
            <div className="rounded-lg border border-destructive/30 bg-destructive/10 p-3 text-sm text-destructive">
              Bağlı kayıt özeti alınamadı. Güvenlik için silme devre dışı.
            </div>
          )}

          {stats && (
            <>
              <div className="grid grid-cols-3 gap-2">
                {stats.map(s => (
                  <Stat key={s.label} label={s.label} value={s.value} />
                ))}
              </div>

              {canDelete ? (
                <div className="rounded-lg border border-slate-200 bg-slate-50/60 p-3 text-sm text-slate-700">
                  {emptyReason}
                </div>
              ) : (
                <div className="rounded-lg border border-amber-300 bg-amber-50/60 p-3 text-sm text-amber-900">
                  {blockReason}
                </div>
              )}
            </>
          )}
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={onClose}>
            İptal
          </Button>
          <Button variant="destructive" onClick={onConfirm} disabled={confirmDisabled}>
            <Trash2 className="h-4 w-4 mr-1.5" />
            {isPending ? 'Siliniyor...' : 'Sil'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
