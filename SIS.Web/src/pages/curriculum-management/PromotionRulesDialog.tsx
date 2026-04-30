import { Button } from '@/components/ui/button';
import { Dialog, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import ResizableDialogContent from '@/components/ui/resizable-dialog';
import PromotionRulesPanel from './PromotionRulesPanel';

interface Props {
  open: boolean;
  onClose: () => void;
  curriculumId: number;
  programId: number;
  curriculumLabel: string;
  fromYearOptions: number[];
}

export default function PromotionRulesDialog({
  open,
  onClose,
  curriculumId,
  programId,
  curriculumLabel,
  fromYearOptions,
}: Props) {
  return (
    <Dialog open={open} onOpenChange={(o) => { if (!o) onClose(); }}>
      <ResizableDialogContent
        defaultWidth={900}
        minWidth={600}
        minHeight={400}
        className="flex flex-col p-0 gap-0"
      >
        <DialogHeader className="px-6 pt-5 pb-3 shrink-0 border-b">
          <DialogTitle className="text-base">
            Sınıf Atlatma Kuralları — {curriculumLabel}
          </DialogTitle>
        </DialogHeader>
        <div className="flex-1 overflow-y-auto px-6 py-4">
          <PromotionRulesPanel
            curriculumId={curriculumId}
            programId={programId}
            fromYearOptions={fromYearOptions}
            bare
          />
        </div>
        <DialogFooter className="shrink-0 px-6 py-4 border-t bg-background">
          <Button variant="outline" onClick={onClose}>Kapat</Button>
        </DialogFooter>
      </ResizableDialogContent>
    </Dialog>
  );
}
