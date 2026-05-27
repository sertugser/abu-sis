import { useMemo, useState } from 'react';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Label } from '@/components/ui/label';
import { Loader2, Info } from 'lucide-react';
import { useAcademicCalendarTerms } from '@/hooks/useAcademicCalendar';

interface Props {
  open: boolean;
  currentTermId: number | null;
  isSubmitting: boolean;
  onClose: () => void;
  onSubmit: (sourceTermId: number) => void;
}

export function CopyPreviousTermDialog({ open, currentTermId, isSubmitting, onClose, onSubmit }: Props) {
  const termsQuery = useAcademicCalendarTerms();

  // Mevcut dönem ve iptal edilmiş dönemler kaynak olamaz; en yeniden eskiye sırala.
  const sourceTerms = useMemo(() => {
    const all = termsQuery.data ?? [];
    return all
      .filter(t => t.termId !== currentTermId && !t.isCancelled)
      .sort((a, b) => new Date(b.termStart).getTime() - new Date(a.termStart).getTime());
  }, [termsQuery.data, currentTermId]);

  // Varsayılan: aynı TermType bir önceki yıl (örn. Güz 2025-26 ← Güz 2024-25); yoksa en yeni önceki dönem.
  const defaultSourceId = useMemo(() => {
    const all = termsQuery.data ?? [];
    const current = all.find(t => t.termId === currentTermId);
    if (current) {
      const sameTypePrevYear = sourceTerms.find(
        t => t.termTypeLabel === current.termTypeLabel && t.year === current.year - 1,
      );
      if (sameTypePrevYear) return String(sameTypePrevYear.termId);
    }
    return sourceTerms[0] ? String(sourceTerms[0].termId) : '';
  }, [termsQuery.data, currentTermId, sourceTerms]);

  // Kullanıcı seçim yapmadıysa varsayılana düş; bu sayede effect'e gerek kalmaz.
  const [picked, setPicked] = useState<string>('');
  const selectedId = picked || defaultSourceId;

  const handleClose = () => {
    setPicked('');
    onClose();
  };

  const handleSubmit = () => {
    const id = Number(selectedId);
    if (!Number.isFinite(id) || id <= 0) return;
    onSubmit(id);
  };

  return (
    <Dialog open={open} onOpenChange={isOpen => { if (!isOpen) handleClose(); }}>
      <DialogContent className="sm:max-w-[500px]">
        <DialogHeader>
          <DialogTitle>Önceki Dönemden Hoca Atamalarını Kopyala</DialogTitle>
          <DialogDescription>
            Seçtiğiniz kaynak dönemde her dersin şubesine atanmış öğretim elemanları, bu dönemin{' '}
            <strong>hocası atanmamış</strong> şubelerine ders ve şube sırasına göre kopyalanır. Mevcut atamalara dokunulmaz.
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-3">
          <Label className="text-sm font-medium">Kaynak Dönem</Label>
          {termsQuery.isLoading ? (
            <div className="flex items-center gap-2 text-sm text-gray-500">
              <Loader2 className="h-4 w-4 animate-spin" />
              Dönem listesi yükleniyor...
            </div>
          ) : sourceTerms.length === 0 ? (
            <div className="flex items-start gap-2 rounded-md border border-amber-200 bg-amber-50 p-3 text-sm text-amber-800">
              <Info className="mt-0.5 h-4 w-4 shrink-0" />
              Kopyalanabilecek başka dönem bulunamadı.
            </div>
          ) : (
            <select
              value={selectedId}
              onChange={e => setPicked(e.target.value)}
              className="h-10 w-full rounded-md border border-input bg-background px-3 text-sm outline-none focus:ring-2 focus:ring-ring"
              disabled={isSubmitting}
            >
              {sourceTerms.map(t => (
                <option key={t.termId} value={t.termId}>
                  {t.termName}
                </option>
              ))}
            </select>
          )}
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={handleClose} disabled={isSubmitting}>
            İptal
          </Button>
          <Button onClick={handleSubmit} disabled={!selectedId || isSubmitting || sourceTerms.length === 0}>
            {isSubmitting && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
            Kopyala
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
