// SIS.Web/src/components/grade/GradingCriteriaDialog.tsx
// Harf notu aralıkları düzenleme dialog'u (kilitli ise salt-okunur).

import { useState } from 'react';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Separator } from '@/components/ui/separator';
import { GradeChip } from './GradeChip';
import { CriteriaPreview } from './GradeStatsPanel';
import type { GradingCriteria } from '../../types/grade';

interface GradingCriteriaDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  initialCriteria: GradingCriteria;
  locked: boolean;
  hideStatistics: boolean;
  previewScores: number[];
  isSaving: boolean;
  onSave: (criteria: GradingCriteria) => void;
}

const FIELD_DEFS = [
  { key: 'aaMin' as const, label: 'AA' },
  { key: 'baMin' as const, label: 'BA' },
  { key: 'bbMin' as const, label: 'BB' },
  { key: 'cbMin' as const, label: 'CB' },
  { key: 'ccMin' as const, label: 'CC' },
  { key: 'dcMin' as const, label: 'DC' },
  { key: 'ddMin' as const, label: 'DD' },
];

export function GradingCriteriaDialog({
  open,
  onOpenChange,
  initialCriteria,
  locked,
  hideStatistics,
  previewScores,
  isSaving,
  onSave,
}: GradingCriteriaDialogProps) {
  const [draft, setDraft] = useState<GradingCriteria>(initialCriteria);

  // Dialog her açıldığında en güncel kriteri yansıt.
  const handleOpenChange = (next: boolean) => {
    if (next) setDraft(initialCriteria);
    onOpenChange(next);
  };

  const scale = [
    { label: 'AA', min: draft.aaMin },
    { label: 'BA', min: draft.baMin },
    { label: 'BB', min: draft.bbMin },
    { label: 'CB', min: draft.cbMin },
    { label: 'CC', min: draft.ccMin },
    { label: 'DC', min: draft.dcMin },
    { label: 'DD', min: draft.ddMin },
    { label: 'FF', min: 0 },
  ];

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Harf Notu Aralıkları</DialogTitle>
        </DialogHeader>
        <div className="space-y-4 pt-2">
          {locked && (
            <div className="rounded-md bg-muted px-4 py-3 text-sm text-muted-foreground">
              Bu dersin harf aralıkları sınav yönergesinde kilitli — değiştirilemez.
            </div>
          )}

          <p className="text-sm text-muted-foreground">
            Her harf notu için minimum puan değerini belirleyin.
          </p>

          <div className="grid grid-cols-4 gap-2">
            {scale.map(({ label, min }) => (
              <div key={label} className="flex items-center gap-2 py-1">
                <GradeChip grade={label} />
                <span className="text-xs text-muted-foreground font-mono">≥ {min}</span>
              </div>
            ))}
          </div>

          <Separator />

          <div className="grid grid-cols-2 gap-3">
            {FIELD_DEFS.map(({ key, label }) => (
              <div key={key} className="flex items-center gap-2">
                <Label className="w-8 text-sm font-mono font-bold shrink-0">{label}</Label>
                <Input
                  type="number"
                  min={0}
                  max={100}
                  value={draft[key]}
                  onChange={(e) => setDraft((prev) => ({ ...prev, [key]: Number(e.target.value) }))}
                  className="font-mono"
                  disabled={locked}
                />
              </div>
            ))}
          </div>

          {!hideStatistics && previewScores.length > 0 && (
            <CriteriaPreview scores={previewScores} criteria={draft} />
          )}
        </div>
        <DialogFooter className="pt-2">
          <Button variant="outline" onClick={() => onOpenChange(false)}>İptal</Button>
          <Button onClick={() => onSave(draft)} disabled={isSaving || locked}>
            Kaydet
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
