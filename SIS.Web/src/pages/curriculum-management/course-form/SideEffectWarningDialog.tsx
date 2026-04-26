import { Button } from '@/components/ui/button';
import { Dialog, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import ResizableDialogContent from '@/components/ui/resizable-dialog';
import { AlertTriangle, Loader2 } from 'lucide-react';
import type { CourseCurriculumUsageDto } from '../../../hooks/useAdminCourseCatalog';

export interface FieldChange {
  label: string;       // "AKTS"
  oldValue: string;    // "6"
  newValue: string;    // "5"
}

interface Props {
  open: boolean;
  changes: FieldChange[];
  curriculumUsage: CourseCurriculumUsageDto[];
  isSaving: boolean;
  onConfirm: () => void;
  onCancel: () => void;
}

/**
 * Confirms shared-field edits before they propagate to all curricula using the course.
 * Shown when fields tied to the Course table change (AKTS, Kredi, Ad, Dil, etc.).
 * Past student records (EctsSnapshot etc.) are unaffected — explained in the dialog.
 */
export default function SideEffectWarningDialog({
  open, changes, curriculumUsage, isSaving, onConfirm, onCancel,
}: Props) {
  return (
    <Dialog open={open} onOpenChange={(o) => { if (!o) onCancel(); }}>
      <ResizableDialogContent defaultWidth={520} className="p-6">
        <DialogHeader className="mb-3">
          <DialogTitle className="flex items-center gap-2">
            <AlertTriangle className="h-5 w-5 text-amber-600" />
            Paylaşımlı Alan Değişikliği
          </DialogTitle>
        </DialogHeader>

        <div className="space-y-3 text-sm">
          <p>Aşağıdaki değişiklikler dersi kullanan <strong>tüm müfredatlarda</strong> geçerli olur:</p>

          <ul className="rounded-md border bg-muted/30 px-3 py-2 space-y-1 list-disc list-inside">
            {changes.map((c, i) => (
              <li key={i}>
                <strong>{c.label}:</strong> {c.oldValue} → <strong>{c.newValue}</strong>
              </li>
            ))}
          </ul>

          <p>Bu ders şu <strong>{curriculumUsage.length} müfredatta</strong> kullanılıyor:</p>
          <ul className="rounded-md border bg-muted/30 px-3 py-2 space-y-0.5 max-h-40 overflow-y-auto">
            {curriculumUsage.map(u => (
              <li key={u.curriculumId} className="text-xs">
                {u.programName} ({u.curriculumVersion}){u.isActive ? ' • Aktif' : ''}
              </li>
            ))}
          </ul>

          <p className="text-xs text-muted-foreground">
            Not: Geçmiş kayıtlar (snapshot) etkilenmez — sadece bu değişiklikten sonra eklenen yeni kayıtlar yeni değerleri kullanır.
          </p>
        </div>

        <DialogFooter className="mt-4 gap-2">
          <Button variant="outline" onClick={onCancel} disabled={isSaving}>İptal</Button>
          <Button onClick={onConfirm} disabled={isSaving}>
            {isSaving && <Loader2 className="h-4 w-4 animate-spin mr-2" />}
            Onayla ve Kaydet
          </Button>
        </DialogFooter>
      </ResizableDialogContent>
    </Dialog>
  );
}
