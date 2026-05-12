import { useEffect, useState } from 'react';
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
import { Loader2 } from 'lucide-react';
import { useDepartmentInstructors } from '@/hooks/useCourseOpening';
import type { CourseGroup } from '../types';

interface BulkAssignInstructorDialogProps {
  open: boolean;
  course: CourseGroup | null;
  departmentId: number | null;
  termId: number | null;
  isSubmitting: boolean;
  onClose: () => void;
  onSubmit: (instructorId: number) => void;
}

export function BulkAssignInstructorDialog({
  open,
  course,
  departmentId,
  termId,
  isSubmitting,
  onClose,
  onSubmit,
}: BulkAssignInstructorDialogProps) {
  const instructorsQuery = useDepartmentInstructors(departmentId, termId, true);
  const instructors = instructorsQuery.data ?? [];
  const [selectedId, setSelectedId] = useState<string>('');

  useEffect(() => {
    setSelectedId('');
  }, [course?.courseCode, open]);

  const handleSubmit = () => {
    const id = Number(selectedId);
    if (!Number.isFinite(id) || id <= 0) return;
    onSubmit(id);
  };

  const sectionCount = course?.sections.length ?? 0;

  return (
    <Dialog open={open} onOpenChange={isOpen => { if (!isOpen) onClose(); }}>
      <DialogContent className="sm:max-w-[480px]">
        <DialogHeader>
          <DialogTitle>Tüm Şubelere Hoca Ata</DialogTitle>
          <DialogDescription>
            {course ? (
              <>
                <span className="font-mono font-semibold text-blue-700">{course.courseCode}</span> dersinin{' '}
                <strong>{sectionCount}</strong> şubesine seçtiğiniz öğretim elemanı atanacak. Mevcut atamalar üzerine yazılır.
              </>
            ) : (
              'Ders seçilmedi.'
            )}
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-3">
          <Label className="text-sm font-medium">Öğretim Elemanı</Label>
          {instructorsQuery.isLoading ? (
            <div className="flex items-center gap-2 text-sm text-gray-500">
              <Loader2 className="h-4 w-4 animate-spin" />
              Hoca listesi yükleniyor...
            </div>
          ) : (
            <select
              value={selectedId}
              onChange={e => setSelectedId(e.target.value)}
              className="h-10 w-full rounded-md border border-input bg-background px-3 text-sm outline-none focus:ring-2 focus:ring-ring"
              disabled={isSubmitting}
            >
              <option value="">— Seçin —</option>
              {instructors.map(instructor => (
                <option key={instructor.instructorId} value={instructor.instructorId}>
                  {instructor.fullName}
                  {instructor.departmentCode ? ` (${instructor.departmentCode})` : ''}
                </option>
              ))}
            </select>
          )}
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={onClose} disabled={isSubmitting}>
            İptal
          </Button>
          <Button onClick={handleSubmit} disabled={!selectedId || isSubmitting}>
            {isSubmitting && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
            {sectionCount > 0 ? `${sectionCount} Şubeye Ata` : 'Ata'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
