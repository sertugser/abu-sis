import { Loader2 } from 'lucide-react';
import {
  AlertDialog, AlertDialogAction, AlertDialogCancel, AlertDialogContent,
  AlertDialogDescription, AlertDialogFooter, AlertDialogHeader, AlertDialogTitle,
} from '@/components/ui/alert-dialog';

interface Props {
  target: { id: number; label: string } | null;
  onCancel: () => void;
  onConfirm: () => void;
  isPending: boolean;
}

export default function ActivateCurriculumDialog({ target, onCancel, onConfirm, isPending }: Props) {
  return (
    <AlertDialog open={target !== null} onOpenChange={(open) => !open && onCancel()}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>Müfredat aktif edilsin mi?</AlertDialogTitle>
          <AlertDialogDescription>
            <span className="font-medium">{target?.label}</span> müfredatı yeni kayıtlar için
            varsayılan yapılacak. Aynı programın mevcut aktif müfredatı otomatik pasif olur.
            Mevcut öğrenciler kendi müfredatlarında kalır, etkilenmez.
          </AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel disabled={isPending}>İptal</AlertDialogCancel>
          <AlertDialogAction onClick={onConfirm} disabled={isPending}>
            {isPending && <Loader2 className="h-4 w-4 animate-spin mr-2" />}
            Aktif Et
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}
