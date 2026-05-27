import { AlertTriangle } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';

interface ErrorDialogProps {
  message: string | null;
  onClose: () => void;
}

export function ErrorDialog({ message, onClose }: ErrorDialogProps) {
  return (
    <Dialog open={!!message} onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="z-[150] max-w-md border-red-200">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2 text-red-900">
            <AlertTriangle className="h-5 w-5 text-red-600" />
            Hata
          </DialogTitle>
          <DialogDescription className="text-sm leading-6 text-slate-700">
            {message}
          </DialogDescription>
        </DialogHeader>
        <DialogFooter>
          <Button type="button" onClick={onClose}>
            Tamam
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
