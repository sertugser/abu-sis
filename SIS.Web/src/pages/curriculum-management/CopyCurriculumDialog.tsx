import { useState, useEffect } from 'react';
import { Loader2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Dialog, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import ResizableDialogContent from '@/components/ui/resizable-dialog';

interface Props {
  target: { id: number; label: string } | null;
  onCancel: () => void;
  onConfirm: (newVersion: string) => void;
  isPending: boolean;
}

export default function CopyCurriculumDialog({ target, onCancel, onConfirm, isPending }: Props) {
  const [newVersion, setNewVersion] = useState('');

  // Dialog kapanınca form alanını sıfırla
  useEffect(() => {
    if (target === null) setNewVersion('');
  }, [target]);

  function handleConfirm() {
    if (!newVersion.trim()) return;
    onConfirm(newVersion.trim());
  }

  return (
    <Dialog
      open={target !== null}
      onOpenChange={(open) => { if (!open) onCancel(); }}
    >
      <ResizableDialogContent defaultWidth={500} minWidth={400} minHeight={220} className="flex flex-col p-0 gap-0">
        <DialogHeader className="px-6 pt-6 pb-3 shrink-0">
          <DialogTitle>Müfredatı Kopyala</DialogTitle>
        </DialogHeader>
        <div className="flex-1 px-6 pb-3 space-y-3">
          <div className="rounded-md border bg-muted/50 p-2 text-xs">
            <span className="text-muted-foreground">Kaynak: </span>
            <span className="font-medium">{target?.label}</span>
          </div>
          <div className="space-y-1">
            <Label className="text-xs">Yeni Yıl *</Label>
            <Input
              value={newVersion}
              onChange={e => setNewVersion(e.target.value)}
              placeholder="2025"
              autoFocus
            />
            <p className="text-[11px] text-muted-foreground">
              Kaynak müfredatın tüm dersleri yeni versiyona kopyalanır. Yeni müfredat Pasif olarak oluşur.
            </p>
          </div>
        </div>
        <DialogFooter className="shrink-0 px-6 py-4 border-t gap-2">
          <Button variant="outline" onClick={onCancel}>
            İptal
          </Button>
          <Button onClick={handleConfirm} disabled={isPending || !newVersion.trim()}>
            {isPending && <Loader2 className="h-4 w-4 animate-spin mr-2" />}
            Kopyala
          </Button>
        </DialogFooter>
      </ResizableDialogContent>
    </Dialog>
  );
}
