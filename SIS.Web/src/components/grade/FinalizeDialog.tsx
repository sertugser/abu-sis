// SIS.Web/src/components/grade/FinalizeDialog.tsx
// Kesinleştirme onay + sonuç dialog'u.

import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { AlertTriangle, CheckCircle } from 'lucide-react';
import { GradeChip } from './GradeChip';
import type { FinalizePreviewItem, StudentGradeRow } from '../../types/grade';

interface FinalizeDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  results: FinalizePreviewItem[] | null;
  students: StudentGradeRow[];
  hideStatistics: boolean;
  isPending: boolean;
  onConfirm: () => void;
  onClose: () => void;
}

export function FinalizeDialog({
  open,
  onOpenChange,
  results,
  students,
  hideStatistics,
  isPending,
  onConfirm,
  onClose,
}: FinalizeDialogProps) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-2xl">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            {results ? (
              <><CheckCircle className="h-5 w-5 text-emerald-500" /> Notlar Kesinleştirildi</>
            ) : (
              <><AlertTriangle className="h-5 w-5 text-amber-500" /> Kesinleştirme Onayı</>
            )}
          </DialogTitle>
        </DialogHeader>

        {!results ? (
          <div className="space-y-4 pt-1">
            <div className="rounded-lg bg-amber-50 border border-amber-200 p-4">
              <p className="text-sm text-foreground leading-relaxed">
                Bu işlem tüm öğrencilerin ağırlıklı ortalamalarını hesaplayıp harf notlarını belirleyecektir.
                Eksik notu olan öğrenciler <span className="font-mono font-bold text-red-600">NA</span> alacaktır.{' '}
                Mazeretli (sınava mazeretli girmeyen) öğrenciler <span className="font-mono font-bold text-amber-600">I</span> (Eksik) alır; mazeret sınavı sonrası yeniden sonuçlandırılır.
                {!hideStatistics && (
                  <> Tüm öğrencilerin <span className="font-semibold">CGPA/SGPA</span> değerleri güncellenecektir.</>
                )}
              </p>
            </div>
            <p className="text-xs text-muted-foreground">Bu işlemi daha sonra geri alabilirsiniz.</p>
            <DialogFooter>
              <Button variant="outline" onClick={() => onOpenChange(false)}>İptal</Button>
              <Button onClick={onConfirm} disabled={isPending} className="gap-1.5">
                <CheckCircle className="h-4 w-4" />
                {isPending ? 'Hesaplanıyor...' : 'Onayla ve Kesinleştir'}
              </Button>
            </DialogFooter>
          </div>
        ) : (
          <div className="space-y-4 pt-1">
            <div className="rounded-lg border overflow-hidden max-h-80 overflow-y-auto">
              <table className="w-full text-sm">
                <thead className="sticky top-0 bg-background">
                  <tr className="border-b">
                    <th className="text-left py-2.5 px-4 font-medium text-muted-foreground">Öğrenci</th>
                    {!hideStatistics && (
                      <th className="text-center py-2.5 px-4 font-medium text-muted-foreground">Ağ. Ortalama</th>
                    )}
                    <th className="text-center py-2.5 px-4 font-medium text-muted-foreground">Harf Notu</th>
                  </tr>
                </thead>
                <tbody>
                  {results.map((r) => {
                    const student = students.find((s) => s.studentId === r.studentId);
                    return (
                      <tr key={r.studentId} className="border-b last:border-0">
                        <td className="py-2.5 px-4 font-medium">
                          {student ? `${student.firstName} ${student.lastName}` : `#${r.studentId}`}
                        </td>
                        {!hideStatistics && (
                          <td className="py-2.5 px-4 text-center font-mono">{r.weightedAverage.toFixed(1)}</td>
                        )}
                        <td className="py-2.5 px-4 text-center">
                          <GradeChip grade={r.letterGrade} />
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
            <DialogFooter>
              <Button variant="outline" onClick={onClose}>Kapat</Button>
            </DialogFooter>
          </div>
        )}
      </DialogContent>
    </Dialog>
  );
}
