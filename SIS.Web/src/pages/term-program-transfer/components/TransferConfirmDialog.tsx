import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog';
import type { PreviousTermCourseDto, InstructorOptionDto } from '../../../types/courseOpening';
import type { CourseOverride } from '../types';

interface TransferConfirmDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  selectedCourses: PreviousTermCourseDto[];
  getOverride: (course: PreviousTermCourseDto) => CourseOverride;
  instructors: InstructorOptionDto[];
  /** Hedef bölüm adı — kullanıcının nereye aktardığı net görsün diye. */
  targetDepartmentName: string | null;
  /** Hedef dönem adı — kullanıcının hangi döneme aktardığı net görsün diye. */
  targetTermName: string | null;
  onConfirm: () => void;
}

export function TransferConfirmDialog({
  open,
  onOpenChange,
  selectedCourses,
  getOverride,
  instructors,
  targetDepartmentName,
  targetTermName,
  onConfirm,
}: TransferConfirmDialogProps) {
  const totalSections = selectedCourses.reduce((sum, c) => sum + getOverride(c).sectionCount, 0);

  return (
    <AlertDialog open={open} onOpenChange={onOpenChange}>
      <AlertDialogContent className="max-w-lg">
        <AlertDialogHeader>
          <AlertDialogTitle>Aktarımı Onayla</AlertDialogTitle>
          <AlertDialogDescription asChild>
            <div className="space-y-3">
              <div className="grid grid-cols-2 gap-2 p-3 bg-muted/30 rounded-md text-sm">
                <div>
                  <div className="text-[10px] text-muted-foreground uppercase tracking-wide">Hedef Bölüm</div>
                  <div className="font-medium">{targetDepartmentName ?? '—'}</div>
                </div>
                <div>
                  <div className="text-[10px] text-muted-foreground uppercase tracking-wide">Hedef Dönem</div>
                  <div className="font-medium">{targetTermName ?? '—'}</div>
                </div>
              </div>
              <p>
                Aşağıdaki <strong>{selectedCourses.length} ders</strong> bu bölüme aktarılacak:
              </p>
              <div className="max-h-48 overflow-y-auto space-y-1 border rounded-md p-2 bg-muted/30">
                {selectedCourses.map((c) => {
                  const ov = getOverride(c);
                  const inst = instructors.find((i) => i.instructorId === ov.instructorId);
                  return (
                    <div key={c.courseId} className="flex items-center justify-between text-sm py-1">
                      <span className="font-mono font-medium">{c.courseCode}</span>
                      <span className="text-muted-foreground text-xs">
                        {ov.sectionCount} şube × {ov.capacityPerSection} kişi
                        {inst ? ` — ${inst.fullName}` : ''}
                      </span>
                    </div>
                  );
                })}
              </div>
              <p className="text-sm text-muted-foreground">
                Toplam <strong>{totalSections} şube</strong> oluşturulacak. Bu işlem geri alınamaz.
              </p>
            </div>
          </AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel>İptal</AlertDialogCancel>
          <AlertDialogAction onClick={onConfirm}>Aktarımı Başlat</AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}
