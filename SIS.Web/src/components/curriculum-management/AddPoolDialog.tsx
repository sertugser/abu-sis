import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Dialog, DialogHeader, DialogTitle, DialogDescription, DialogFooter,
} from '@/components/ui/dialog';
import ResizableDialogContent from '@/components/ui/resizable-dialog';
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/components/ui/select';
import { Loader2 } from 'lucide-react';
import { useElectiveGroupList } from '../../hooks/useElectiveGroups';
import { useCreatePool } from '../../hooks/useCurriculumManagement';
import { ELECTIVE_SUBTYPE_SHORT } from '../../types/electiveGroup';
import type { ElectiveSubtype } from '../../types/electiveGroup';

interface Props {
  curriculumId: number;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

export default function AddPoolDialog({ curriculumId, open, onOpenChange }: Props) {
  const [electiveGroupId, setElectiveGroupId] = useState<string>('');
  const [slotName, setSlotName] = useState<string>('');
  const [semesterStart, setSemesterStart] = useState<string>('1');
  const [semesterEnd, setSemesterEnd] = useState<string>('8');
  const [requiredCourseCount, setRequiredCourseCount] = useState<string>('1');
  const [requiredEcts, setRequiredEcts] = useState<string>('5');
  const [ectsPerCourse, setEctsPerCourse] = useState<string>('');
  const [validationError, setValidationError] = useState<string | null>(null);

  const { data: groups, isLoading: loadingGroups } = useElectiveGroupList();
  const createPool = useCreatePool();

  function resetForm() {
    setElectiveGroupId('');
    setSlotName('');
    setSemesterStart('1');
    setSemesterEnd('8');
    setRequiredCourseCount('1');
    setRequiredEcts('5');
    setEctsPerCourse('');
    setValidationError(null);
  }

  function handleSubmit() {
    if (!electiveGroupId) {
      setValidationError('Grup seçilmelidir.');
      return;
    }
    if (!slotName.trim()) {
      setValidationError('Slot adı boş olamaz (örn. TE-1).');
      return;
    }
    if (Number(semesterStart) > Number(semesterEnd)) {
      setValidationError('Başlangıç yarıyılı bitişten büyük olamaz.');
      return;
    }
    const reqCount = Number(requiredCourseCount);
    const reqEcts = Number(requiredEcts);
    if (reqCount <= 0 && reqEcts <= 0) {
      setValidationError('Gerekli ders sayısı veya AKTS en az biri 0\'dan büyük olmalıdır.');
      return;
    }
    setValidationError(null);

    const parsedEcts = ectsPerCourse.trim() ? Number(ectsPerCourse) : undefined;
    if (parsedEcts !== undefined && (Number.isNaN(parsedEcts) || parsedEcts < 0)) {
      setValidationError('Ders başına AKTS negatif olamaz.');
      return;
    }

    createPool.mutate(
      {
        curriculumId,
        electiveGroupId: Number(electiveGroupId),
        slotName: slotName.trim(),
        suggestedSemesterStart: Number(semesterStart),
        suggestedSemesterEnd: Number(semesterEnd),
        requiredCourseCount: reqCount,
        requiredEcts: reqEcts,
        ...(parsedEcts !== undefined ? { ectsPerCourse: parsedEcts } : {}),
      },
      {
        onSuccess: () => {
          resetForm();
          onOpenChange(false);
        },
      },
    );
  }

  return (
    <Dialog open={open} onOpenChange={(v) => { onOpenChange(v); if (!v) resetForm(); }}>
      <ResizableDialogContent defaultWidth={500} minWidth={400} minHeight={400} className="flex flex-col p-0 gap-0">
        <DialogHeader className="px-6 pt-6 pb-3 shrink-0">
          <DialogTitle>Grup Ekle</DialogTitle>
          <DialogDescription className="text-xs">
            Müfredata bir grup slotu ekle (örn. TE-1, OC-2). Aynı grup farklı slot adlarıyla
            birden fazla kez bağlanabilir.
          </DialogDescription>
        </DialogHeader>
        <div className="flex-1 overflow-y-auto px-6 space-y-4 pb-2">
          {/* Elective Group Select */}
          <div className="space-y-1">
            <Label className="text-xs">Seçmeli Grubu *</Label>
            <Select value={electiveGroupId} onValueChange={setElectiveGroupId}>
              <SelectTrigger className="h-8 text-sm">
                {loadingGroups
                  ? <Loader2 className="h-4 w-4 animate-spin" />
                  : <SelectValue placeholder="Grup seçin" />}
              </SelectTrigger>
              <SelectContent>
                {(groups ?? []).map(g => (
                  <SelectItem key={g.id} value={g.id.toString()}>
                    {g.name} ({g.code}) — {ELECTIVE_SUBTYPE_SHORT[g.electiveSubtype as ElectiveSubtype] ?? g.electiveSubtype}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          {/* Slot Name */}
          <div className="space-y-1">
            <Label className="text-xs">Slot Adı *</Label>
            <Input
              value={slotName}
              onChange={e => setSlotName(e.target.value)}
              placeholder="örn. TE-1"
              className="h-8 text-sm"
            />
            <p className="text-[10px] text-muted-foreground">
              Müfredat içinde benzersiz olmalı. Aynı grubu birden fazla slot olarak (TE-1, TE-2…) bağlamak için kullanılır.
            </p>
          </div>

          {/* Semester Range */}
          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1">
              <Label className="text-xs">Başlangıç Yarıyılı</Label>
              <Input
                type="number"
                min={1}
                max={8}
                value={semesterStart}
                onChange={e => setSemesterStart(e.target.value)}
                className="h-8 text-sm"
              />
            </div>
            <div className="space-y-1">
              <Label className="text-xs">Bitiş Yarıyılı</Label>
              <Input
                type="number"
                min={1}
                max={8}
                value={semesterEnd}
                onChange={e => setSemesterEnd(e.target.value)}
                className="h-8 text-sm"
              />
            </div>
          </div>

          {/* Required counts */}
          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1">
              <Label className="text-xs">Alınması Gereken Ders Sayısı</Label>
              <Input
                type="number"
                min={0}
                value={requiredCourseCount}
                onChange={e => setRequiredCourseCount(e.target.value)}
                className="h-8 text-sm"
              />
            </div>
            <div className="space-y-1">
              <Label className="text-xs">Alınması Gereken AKTS</Label>
              <Input
                type="number"
                min={0}
                value={requiredEcts}
                onChange={e => setRequiredEcts(e.target.value)}
                className="h-8 text-sm"
              />
            </div>
          </div>

          {/* EctsPerCourse override */}
          <div className="space-y-1">
            <Label className="text-xs">Ders Başına AKTS (opsiyonel)</Label>
            <Input
              type="number"
              min={0}
              value={ectsPerCourse}
              onChange={e => setEctsPerCourse(e.target.value)}
              placeholder="boş bırak = ders kataloğu AKTS'i"
              className="h-8 text-sm"
            />
            <p className="text-[10px] text-muted-foreground">
              Boş bırakılırsa her dersin kendi AKTS'i kullanılır. Override için sayı gir
              (örn. tüm seçmeli dersler bu havuzda 6 AKTS sayılsın).
            </p>
          </div>

          {validationError && (
            <p className="text-xs text-destructive">{validationError}</p>
          )}
          {createPool.isError && !validationError && (
            <p className="text-xs text-destructive">Grup eklenemedi.</p>
          )}
        </div>
        <DialogFooter className="shrink-0 px-6 py-4 border-t gap-2">
          <Button variant="outline" onClick={() => onOpenChange(false)}>
            İptal
          </Button>
          <Button
            onClick={handleSubmit}
            disabled={createPool.isPending || !electiveGroupId || !slotName.trim()}
          >
            {createPool.isPending && <Loader2 className="h-4 w-4 animate-spin mr-2" />}
            Ekle
          </Button>
        </DialogFooter>
      </ResizableDialogContent>
    </Dialog>
  );
}
