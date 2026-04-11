import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Dialog, DialogHeader, DialogTitle, DialogFooter,
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
  const [semesterStart, setSemesterStart] = useState<string>('1');
  const [semesterEnd, setSemesterEnd] = useState<string>('8');
  const [requiredCourseCount, setRequiredCourseCount] = useState<string>('1');
  const [requiredEcts, setRequiredEcts] = useState<string>('5');

  const { data: groups, isLoading: loadingGroups } = useElectiveGroupList();
  const createPool = useCreatePool();

  function resetForm() {
    setElectiveGroupId('');
    setSemesterStart('1');
    setSemesterEnd('8');
    setRequiredCourseCount('1');
    setRequiredEcts('5');
  }

  function handleSubmit() {
    if (!electiveGroupId) return;
    createPool.mutate(
      {
        curriculumId,
        electiveGroupId: Number(electiveGroupId),
        suggestedSemesterStart: Number(semesterStart),
        suggestedSemesterEnd: Number(semesterEnd),
        requiredCourseCount: Number(requiredCourseCount),
        requiredEcts: Number(requiredEcts),
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
      <ResizableDialogContent defaultWidth={480} minWidth={380} minHeight={300} className="flex flex-col p-0 gap-0">
        <DialogHeader className="px-6 pt-6 pb-3 shrink-0">
          <DialogTitle>Seçmeli Havuzu Ekle</DialogTitle>
        </DialogHeader>
        <div className="flex-1 overflow-y-auto px-6 space-y-4">
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

          {/* Semester Range */}
          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1">
              <Label className="text-xs">Baslangic Yariyili</Label>
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
              <Label className="text-xs">Bitis Yariyili</Label>
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
              <Label className="text-xs">Alinmasi Gereken Ders Sayisi</Label>
              <Input
                type="number"
                min={1}
                value={requiredCourseCount}
                onChange={e => setRequiredCourseCount(e.target.value)}
                className="h-8 text-sm"
              />
            </div>
            <div className="space-y-1">
              <Label className="text-xs">Alinmasi Gereken AKTS</Label>
              <Input
                type="number"
                min={1}
                value={requiredEcts}
                onChange={e => setRequiredEcts(e.target.value)}
                className="h-8 text-sm"
              />
            </div>
          </div>
        </div>
        <DialogFooter className="shrink-0 px-6 py-4 border-t gap-2">
          <Button variant="outline" onClick={() => onOpenChange(false)}>
            Iptal
          </Button>
          <Button
            onClick={handleSubmit}
            disabled={createPool.isPending || !electiveGroupId}
          >
            {createPool.isPending && <Loader2 className="h-4 w-4 animate-spin mr-2" />}
            Ekle
          </Button>
        </DialogFooter>
      </ResizableDialogContent>
    </Dialog>
  );
}
