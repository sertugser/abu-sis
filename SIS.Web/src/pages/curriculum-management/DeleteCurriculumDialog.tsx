import { DelayedConfirmDialog } from '@/components/ui/delayed-confirm-dialog';

interface Props {
  target: { id: number; label: string } | null;
  onCancel: () => void;
  onConfirm: () => void;
  isPending: boolean;
}

/**
 * Müfredat silme — kalıcı işlem, müfredata bağlı tüm course-curriculum kayıtları kopar.
 * 5sn delay zorunlu (kullanıcının yanlış müfredatı silmesini önler).
 */
export default function DeleteCurriculumDialog({ target, onCancel, onConfirm, isPending }: Props) {
  return (
    <DelayedConfirmDialog
      open={target !== null}
      onOpenChange={(open) => { if (!open) onCancel(); }}
      title={`Müfredat silinsin mi: ${target?.label ?? ''}`}
      description="Bu müfredat ve içindeki tüm ders ilişkileri (zorunlu/seçmeli/önkoşul bağları) kalıcı olarak silinecek."
      footnote="Bu işlem geri alınamaz. Müfredata bağlı öğrenci varsa silme işlemi backend tarafından reddedilir."
      confirmLabel={isPending ? 'Siliniyor...' : 'Müfredatı sil'}
      delaySeconds={5}
      severity="danger"
      onConfirm={onConfirm}
      onCancel={onCancel}
    />
  );
}
