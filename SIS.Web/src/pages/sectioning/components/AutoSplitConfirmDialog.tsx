import { Loader2 } from 'lucide-react';
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
import type { CourseGroup } from '../types';
import { SPLIT_STRATEGIES, type SplitStrategy } from '../constants';

interface AutoSplitConfirmDialogProps {
  open: boolean;
  course: CourseGroup | null;
  targetCount: number;
  strategy: SplitStrategy;
  isSubmitting: boolean;
  onCancel: () => void;
  onConfirm: () => void;
}

export function AutoSplitConfirmDialog({
  open,
  course,
  targetCount,
  strategy,
  isSubmitting,
  onCancel,
  onConfirm,
}: AutoSplitConfirmDialogProps) {
  const strategyLabel = SPLIT_STRATEGIES.find(s => s.value === strategy)?.label ?? strategy;
  const currentCount = course?.sections.length ?? 0;
  const studentCount = course?.totalStudents ?? 0;

  const delta = targetCount - currentCount;
  const action =
    delta > 0
      ? `${delta} yeni şube açılacak`
      : delta < 0
        ? `${Math.abs(delta)} şube kapatılacak (en az öğrenciye sahip olanlar)`
        : 'Şube sayısı aynı kalacak — sadece öğrenciler yeniden dağıtılacak';

  return (
    <AlertDialog open={open} onOpenChange={isOpen => { if (!isOpen) onCancel(); }}>
      <AlertDialogContent className="sm:max-w-[480px]">
        <AlertDialogHeader>
          <AlertDialogTitle>Otomatik Şubelendirme</AlertDialogTitle>
          <AlertDialogDescription asChild>
            <div className="space-y-3 text-sm text-muted-foreground">
              {course && (
                <div className="rounded-md border bg-gray-50 p-3">
                  <div className="flex items-center gap-2">
                    <span className="font-mono font-semibold text-blue-700">{course.courseCode}</span>
                    <span className="font-medium text-gray-800">{course.courseName}</span>
                  </div>
                  <div className="mt-1 text-xs text-gray-500 tabular-nums">
                    Mevcut: <strong>{currentCount} şube</strong> · <strong>{studentCount} öğrenci</strong>
                  </div>
                </div>
              )}

              <ul className="space-y-1 text-sm">
                <li>• Hedef şube sayısı: <strong className="text-gray-900">{targetCount}</strong></li>
                <li>• Strateji: <strong className="text-gray-900">{strategyLabel}</strong></li>
                <li>• {action}</li>
                <li>• Tüm <strong>aktif kayıtlı öğrenciler</strong> yeniden dağıtılır</li>
              </ul>

              <p className="rounded-md border border-amber-200 bg-amber-50 p-2 text-xs text-amber-800">
                ⚠ Bu işlem öğrencilerin şubelerini değiştirir. Devam etmeden önce onaylayın.
              </p>
            </div>
          </AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel disabled={isSubmitting}>İptal</AlertDialogCancel>
          <AlertDialogAction onClick={onConfirm} disabled={isSubmitting}>
            {isSubmitting && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
            Şubelendir
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}
