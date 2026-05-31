import { useState } from 'react';
import { AlertDialog, AlertDialogAction, AlertDialogCancel, AlertDialogContent, AlertDialogDescription, AlertDialogFooter, AlertDialogHeader, AlertDialogTitle } from '@/components/ui/alert-dialog';
import { useCloseSection } from '../../../hooks/useCourseOpening';
import type { OpenedCourseSectionDto } from '../../../types/courseOpening';

interface DeleteSectionDialogProps {
  section: OpenedCourseSectionDto | null;
  departmentId: number;
  termId: number;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

export function DeleteSectionDialog({ section, departmentId, termId, open, onOpenChange }: DeleteSectionDialogProps) {
  const [error, setError] = useState<string | null>(null);
  const mutation = useCloseSection(departmentId, termId);

  if (!section) return null;

  const handleDelete = async () => {
    setError(null);
    try {
      await mutation.mutateAsync({
        sectionId: section.sectionId,
        label: `${section.courseCode} — ${section.sectionNumber}. şube`,
      });
      onOpenChange(false);
    } catch (e) {
      const err = e as { response?: { data?: { error?: string } }; message?: string };
      setError(err?.response?.data?.error ?? err?.message ?? 'Bilinmeyen hata');
    }
  };

  return (
    <AlertDialog open={open} onOpenChange={onOpenChange}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>Şubeyi kapat?</AlertDialogTitle>
          <AlertDialogDescription asChild>
            <div className="space-y-2">
              <p>
                <strong className="font-mono">{section.courseCode}</strong> — Şube{' '}
                <strong>{section.sectionNumber}</strong> ({section.courseName}) kapatılacak.
              </p>
              {section.enrolledCount > 0 && (
                <p className="text-destructive text-sm">
                  ⚠ Bu şubede <strong>{section.enrolledCount} aktif kayıtlı öğrenci</strong> var. Önce öğrencileri başka şubelere aktarmanız gerekir, aksi halde kapatma reddedilir.
                </p>
              )}
              <p className="text-xs text-muted-foreground">Bu işlem geri alınabilir (yeniden açılabilir).</p>
              {error && <p className="text-xs text-destructive p-2 bg-destructive/10 rounded">{error}</p>}
            </div>
          </AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel disabled={mutation.isPending}>İptal</AlertDialogCancel>
          <AlertDialogAction
            onClick={(e) => {
              e.preventDefault();
              handleDelete();
            }}
            className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
            disabled={mutation.isPending}
          >
            {mutation.isPending ? 'Kapatılıyor...' : 'Kapat'}
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}
