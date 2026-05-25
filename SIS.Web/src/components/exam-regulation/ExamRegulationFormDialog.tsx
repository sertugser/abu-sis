import { useState } from 'react';
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Switch } from '@/components/ui/switch';
import { useAdminPrograms } from '../../hooks/useAdminLookup';
import { useCreateExamRegulation, useUpdateExamRegulation } from '../../hooks/useExamRegulations';
import type { ExamRegulation, SaveExamRegulationRequest } from '../../types/examRegulation';
import { TERM_TYPES, TERM_LABELS } from '../../lib/examRegulationLabels';
import { GRADE_BADGE_CLASS } from '../../lib/gradeUtils';

const GRADE_CODES = Object.keys(GRADE_BADGE_CLASS) as string[];

interface Props { open: boolean; onOpenChange: (v: boolean) => void; editTarget: ExamRegulation | null; }

const emptyForm: SaveExamRegulationRequest = {
  programId: 0, degreeLevel: 'Bachelor', termType: 'Fall', year: new Date().getFullYear(),
  isActive: true, name: '', midtermCount: 1, finalCount: 1,
  minMidtermWeightPercent: 40, maxMidtermWeightPercent: 60,
  minFinalWeightPercent: 40, maxFinalWeightPercent: 60,
  passingGrade: 50, rawScoreFloor1: 0, rawScoreFloor2: 0, finalExamFloor: 0,
  aaMin: 91, baMin: 81, bbMin: 71, cbMin: 61, ccMin: 55, dcMin: 50, ddMin: 45,
  letterRangesEditable: true, belowFloorGrade: 'FF',
};

export function ExamRegulationFormDialog({ open, onOpenChange, editTarget }: Props) {
  const programs = useAdminPrograms();
  const createMut = useCreateExamRegulation();
  const updateMut = useUpdateExamRegulation();
  const mutation = editTarget ? updateMut : createMut;

  const [form, setForm] = useState<SaveExamRegulationRequest>(emptyForm);
  const [prevKey, setPrevKey] = useState<string | null>(null);
  const key = `${open}-${editTarget?.id ?? 'new'}`;
  if (open && key !== prevKey) {
    setPrevKey(key);
    setForm(editTarget ? { ...editTarget } : emptyForm);
  }

  const set = <K extends keyof SaveExamRegulationRequest>(k: K, v: SaveExamRegulationRequest[K]) =>
    setForm((f) => ({ ...f, [k]: v }));

  const submit = () => {
    if (editTarget) updateMut.mutate({ id: editTarget.id, dto: form }, { onSuccess: () => onOpenChange(false) });
    else createMut.mutate(form, { onSuccess: () => onOpenChange(false) });
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-3xl max-h-[85vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>{editTarget ? 'Sınav Yönergesi Düzenle' : 'Yeni Sınav Yönergesi'}</DialogTitle>
        </DialogHeader>

        <div className="grid gap-4 md:grid-cols-2">
          <div className="space-y-1">
            <Label>Program</Label>
            <select className="h-9 w-full rounded-md border px-3 text-sm" value={form.programId}
              onChange={(e) => set('programId', Number(e.target.value))}>
              <option value={0}>Program seçiniz</option>
              {(programs.data ?? []).map((p) => <option key={p.id} value={p.id}>{p.name}</option>)}
            </select>
          </div>
          <div className="space-y-1">
            <Label>Öğrenci Tipi</Label>
            <select className="h-9 w-full rounded-md border px-3 text-sm" value={form.degreeLevel}
              onChange={(e) => set('degreeLevel', e.target.value as SaveExamRegulationRequest['degreeLevel'])}>
              <option value="Bachelor">Lisans</option>
              <option value="Associate">Önlisans</option>
              <option value="Master">Yüksek Lisans</option>
              <option value="Doctorate">Doktora</option>
            </select>
          </div>
          <div className="space-y-1">
            <Label>Dönem Tipi</Label>
            <select className="h-9 w-full rounded-md border px-3 text-sm" value={form.termType}
              onChange={(e) => set('termType', e.target.value)}>
              {TERM_TYPES.map((t) => <option key={t} value={t}>{TERM_LABELS[t]}</option>)}
            </select>
          </div>
          <div className="space-y-1"><Label>Yıl</Label>
            <Input type="number" min={0} value={form.year} onChange={(e) => set('year', Number(e.target.value))} /></div>
          <div className="space-y-1 md:col-span-2"><Label>Adı</Label>
            <Input value={form.name} onChange={(e) => set('name', e.target.value)} /></div>
          <div className="flex items-center gap-2"><Switch checked={form.isActive} onCheckedChange={(v) => set('isActive', v)} /><Label>Aktif</Label></div>
          <div className="space-y-1"><Label>Ara Sınav Sayısı</Label>
            <Input type="number" min={0} value={form.midtermCount} onChange={(e) => set('midtermCount', Number(e.target.value))} /></div>
          <div className="space-y-1"><Label>YYS Sınav Sayısı</Label>
            <Input type="number" min={0} value={form.finalCount} onChange={(e) => set('finalCount', Number(e.target.value))} /></div>
          <div className="space-y-1"><Label>En Az Ara Sınav Etki %</Label>
            <Input type="number" min={0} max={100} value={form.minMidtermWeightPercent} onChange={(e) => set('minMidtermWeightPercent', Number(e.target.value))} /></div>
          <div className="space-y-1"><Label>En Çok Ara Sınav Etki %</Label>
            <Input type="number" min={0} max={100} value={form.maxMidtermWeightPercent} onChange={(e) => set('maxMidtermWeightPercent', Number(e.target.value))} /></div>
          <div className="space-y-1"><Label>En Az YYS Etki %</Label>
            <Input type="number" min={0} max={100} value={form.minFinalWeightPercent} onChange={(e) => set('minFinalWeightPercent', Number(e.target.value))} /></div>
          <div className="space-y-1"><Label>En Çok YYS Etki %</Label>
            <Input type="number" min={0} max={100} value={form.maxFinalWeightPercent} onChange={(e) => set('maxFinalWeightPercent', Number(e.target.value))} /></div>
          <div className="space-y-1"><Label>Geçme Notu</Label>
            <Input type="number" min={0} max={100} value={form.passingGrade} onChange={(e) => set('passingGrade', Number(e.target.value))} /></div>
          <div className="space-y-1">
            <Label>Baraj Altına Atanacak Harf</Label>
            <select className="h-9 w-full rounded-md border px-3 text-sm" value={form.belowFloorGrade}
              onChange={(e) => set('belowFloorGrade', e.target.value)}>
              {GRADE_CODES.map((code) => <option key={code} value={code}>{code}</option>)}
            </select>
          </div>
          <div className="space-y-1"><Label>YYS Limiti (finalden baraj)</Label>
            <Input type="number" min={0} value={form.finalExamFloor} onChange={(e) => set('finalExamFloor', Number(e.target.value))} /></div>
          <div className="space-y-1"><Label>Ham Başarı Alt Limit 1</Label>
            <Input type="number" min={0} value={form.rawScoreFloor1} onChange={(e) => set('rawScoreFloor1', Number(e.target.value))} /></div>
          <div className="space-y-1"><Label>Ham Başarı Alt Limit 2</Label>
            <Input type="number" min={0} value={form.rawScoreFloor2} onChange={(e) => set('rawScoreFloor2', Number(e.target.value))} /></div>
        </div>

        <div className="space-y-3 pt-2">
          <div className="flex items-center justify-between">
            <h3 className="text-sm font-semibold">Harf Aralıkları</h3>
            <div className="flex items-center gap-2">
              <Switch checked={form.letterRangesEditable} onCheckedChange={(v) => set('letterRangesEditable', v)} />
              <Label className="text-sm">Harf Aralığı Değişebilir</Label>
            </div>
          </div>
          <div className="grid grid-cols-2 gap-3 md:grid-cols-4">
            {([
              { key: 'aaMin' as const, label: 'AA min' },
              { key: 'baMin' as const, label: 'BA min' },
              { key: 'bbMin' as const, label: 'BB min' },
              { key: 'cbMin' as const, label: 'CB min' },
              { key: 'ccMin' as const, label: 'CC min' },
              { key: 'dcMin' as const, label: 'DC min' },
              { key: 'ddMin' as const, label: 'DD min' },
            ]).map(({ key, label }) => (
              <div key={key} className="space-y-1">
                <Label className="font-mono text-xs">{label}</Label>
                <Input
                  type="number"
                  min={0}
                  max={100}
                  value={form[key]}
                  onChange={(e) => set(key, Number(e.target.value))}
                />
              </div>
            ))}
          </div>
        </div>

        {mutation.isError && (
          <p className="text-sm text-destructive">
            {(mutation.error as { response?: { data?: { message?: string } } })?.response?.data?.message ?? 'Kaydedilemedi.'}
          </p>
        )}

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>İptal</Button>
          <Button onClick={submit} disabled={mutation.isPending}>Kaydet</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
