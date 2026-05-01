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

export default function DeleteCurriculumDialog({ target, onCancel, onConfirm, isPending }: Props) {
  return (
    <AlertDialog open={target !== null} onOpenChange={(open) => !open && onCancel()}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>Müfredat silinsin mi?</AlertDialogTitle>
          <AlertDialogDescription>
            <span className="font-medium">{target?.label}</span> müfredatı silinecek.
            Bu işlem geri alınamaz. Müfredata bağlı öğrenci varsa silme işlemi reddedilir.
          </AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel disabled={isPending}>İptal</AlertDialogCancel>
          <AlertDialogAction
            onClick={onConfirm}
            disabled={isPending}
            className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
          >
            {isPending && <Loader2 className="h-4 w-4 animate-spin mr-2" />}
            Sil
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}
