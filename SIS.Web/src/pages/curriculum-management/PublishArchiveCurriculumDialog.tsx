import { Loader2 } from 'lucide-react';
import {
  AlertDialog, AlertDialogAction, AlertDialogCancel, AlertDialogContent,
  AlertDialogDescription, AlertDialogFooter, AlertDialogHeader, AlertDialogTitle,
} from '@/components/ui/alert-dialog';

export type PublishArchiveMode = 'publish' | 'archive';

interface Props {
  mode: PublishArchiveMode;
  target: { id: number; label: string } | null;
  onCancel: () => void;
  onConfirm: () => void;
  isPending: boolean;
}

const COPY: Record<PublishArchiveMode, { title: string; body: string; action: string }> = {
  publish: {
    title: 'Müfredat yayınlansın mı?',
    body: 'Yayınlandıktan sonra bu müfredata ders veya grup eklenip çıkarılamaz (kilitlenir). '
      + 'Değişiklik gerekirse yeni bir sürüm oluşturmanız gerekir.',
    action: 'Yayınla',
  },
  archive: {
    title: 'Müfredat arşive alınsın mı?',
    body: 'Arşivlenen müfredat geçmiş olarak işaretlenir; yeni öğrenci bağlanamaz ve tekrar '
      + 'yayınlanamaz. Mevcut öğrenciler kendi müfredatlarında kalır, etkilenmez.',
    action: 'Arşivle',
  },
};

export default function PublishArchiveCurriculumDialog({
  mode, target, onCancel, onConfirm, isPending,
}: Props) {
  const copy = COPY[mode];
  return (
    <AlertDialog open={target !== null} onOpenChange={(open) => !open && onCancel()}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>{copy.title}</AlertDialogTitle>
          <AlertDialogDescription>
            <span className="font-medium">{target?.label}</span> — {copy.body}
          </AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel disabled={isPending}>İptal</AlertDialogCancel>
          <AlertDialogAction onClick={onConfirm} disabled={isPending}>
            {isPending && <Loader2 className="h-4 w-4 animate-spin mr-2" />}
            {copy.action}
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}
