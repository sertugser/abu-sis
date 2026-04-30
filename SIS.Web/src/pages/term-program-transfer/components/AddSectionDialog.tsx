import { useState } from 'react';
import { AlertDialog, AlertDialogAction, AlertDialogCancel, AlertDialogContent, AlertDialogDescription, AlertDialogFooter, AlertDialogHeader, AlertDialogTitle } from '@/components/ui/alert-dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { useOpenCourse } from '../../../hooks/useCourseOpening';
import type { OpenedCourseSectionDto } from '../../../types/courseOpening';

interface AddSectionDialogProps {
  /** Referans alınacak mevcut şube (yeni şubenin kontenjanı/hocası bundan kopyalanır). */
  section: OpenedCourseSectionDto | null;
  departmentId: number;
  termId: number;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

/** Mevcut bir derse aynı kontenjan/hoca ile yeni 1 şube daha ekler. */
export function AddSectionDialog({ section, departmentId, termId, open, onOpenChange }: AddSectionDialogProps) {
  const [capacity, setCapacity] = useState<number>(40);
  const [error, setError] = useState<string | null>(null);
  const mutation = useOpenCourse(departmentId, termId);

  // Section değişince capacity'yi sync et
  if (section && capacity === 40 && section.capacity !== 40) {
    setCapacity(section.capacity);
  }

  if (!section) return null;

  const handleAdd = async () => {
    setError(null);
    try {
      await mutation.mutateAsync({
        courseId: section.courseId,
        sectionCount: 1,
        capacityPerSection: capacity,
        instructorId: section.instructorId ?? undefined,
        internalQuota: section.internalQuota ?? undefined,
        externalQuota: section.externalQuota ?? undefined,
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
          <AlertDialogTitle>Yeni şube ekle</AlertDialogTitle>
          <AlertDialogDescription asChild>
            <div className="space-y-3">
              <p>
                <strong className="font-mono">{section.courseCode}</strong> ({section.courseName}) dersine yeni 1 şube
                eklenecek.
              </p>
              <div className="space-y-1">
                <Label className="text-xs">Şube Kontenjanı</Label>
                <Input
                  type="number"
                  min={1}
                  value={capacity}
                  onChange={(e) => setCapacity(Math.max(1, Number(e.target.value) || 1))}
                  className="h-9"
                />
              </div>
              <p className="text-xs text-muted-foreground">
                Hoca, iç/dış kota gibi bilgiler şu anki şubeden kopyalanır. Yeni şubenin numarası mevcut en yüksek
                şube numarasının +1'i olur.
              </p>
              {error && <p className="text-xs text-destructive p-2 bg-destructive/10 rounded">{error}</p>}
            </div>
          </AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel disabled={mutation.isPending}>İptal</AlertDialogCancel>
          <AlertDialogAction
            onClick={(e) => {
              e.preventDefault();
              handleAdd();
            }}
            disabled={mutation.isPending}
          >
            {mutation.isPending ? 'Ekleniyor...' : 'Şube Ekle'}
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}
