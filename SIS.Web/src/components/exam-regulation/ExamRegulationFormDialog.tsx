import { useState } from 'react';
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Switch } from '@/components/ui/switch';
import { useAdminPrograms } from '../../hooks/useAdminLookup';
import { useCreateExamRegulation, useUpdateExamRegulation } from '../../hooks/useExamRegulations';
import type { ExamRegulation, SaveExamRegulationRequest } from '../../types/examRegulation';

const TERM_TYPES = ['Fall','Spring','Summer','FallResit','SpringResit','SummerResit','SpringOther','SummerOther','Exemption'];
const TERM_LABELS: Record<string,string> = { Fall:'Güz', Spring:'Bahar', Summer:'Yaz', FallResit:'Güz Bütünleme', SpringResit:'Bahar Bütünleme', SummerResit:'Yaz Bütünleme', SpringOther:'Bahar Diğer', SummerOther:'Yaz Diğer', Exemption:'Muafiyet' };

interface Props { open: boolean; onOpenChange: (v: boolean) => void; editTarget: ExamRegulation | null; }

const emptyForm: SaveExamRegulationRequest = {
  programId: 0, degreeLevel: 'Bachelor', termType: 'Fall', year: new Date().getFullYear(),
  isActive: true, name: '', midtermCount: 1, finalCount: 1,
  minMidtermWeightPercent: 40, maxMidtermWeightPercent: 60,
  minFinalWeightPercent: 40, maxFinalWeightPercent: 60,
  passingGrade: 50, rawScoreFloor1: 0, rawScoreFloor2: 0, finalExamFloor: 0,
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
            <Input type="number" value={form.year} onChange={(e) => set('year', Number(e.target.value))} /></div>
          <div className="space-y-1 md:col-span-2"><Label>Adı</Label>
            <Input value={form.name} onChange={(e) => set('name', e.target.value)} /></div>
          <div className="flex items-center gap-2"><Switch checked={form.isActive} onCheckedChange={(v) => set('isActive', v)} /><Label>Aktif</Label></div>
          <div className="space-y-1"><Label>Ara Sınav Sayısı</Label>
            <Input type="number" value={form.midtermCount} onChange={(e) => set('midtermCount', Number(e.target.value))} /></div>
          <div className="space-y-1"><Label>YYS Sınav Sayısı</Label>
            <Input type="number" value={form.finalCount} onChange={(e) => set('finalCount', Number(e.target.value))} /></div>
          <div className="space-y-1"><Label>En Az Ara Sınav Etki %</Label>
            <Input type="number" value={form.minMidtermWeightPercent} onChange={(e) => set('minMidtermWeightPercent', Number(e.target.value))} /></div>
          <div className="space-y-1"><Label>En Çok Ara Sınav Etki %</Label>
            <Input type="number" value={form.maxMidtermWeightPercent} onChange={(e) => set('maxMidtermWeightPercent', Number(e.target.value))} /></div>
          <div className="space-y-1"><Label>En Az YYS Etki %</Label>
            <Input type="number" value={form.minFinalWeightPercent} onChange={(e) => set('minFinalWeightPercent', Number(e.target.value))} /></div>
          <div className="space-y-1"><Label>En Çok YYS Etki %</Label>
            <Input type="number" value={form.maxFinalWeightPercent} onChange={(e) => set('maxFinalWeightPercent', Number(e.target.value))} /></div>
          <div className="space-y-1"><Label>Geçme Notu</Label>
            <Input type="number" value={form.passingGrade} onChange={(e) => set('passingGrade', Number(e.target.value))} /></div>
          <div className="space-y-1"><Label>YYS Limiti (finalden baraj)</Label>
            <Input type="number" value={form.finalExamFloor} onChange={(e) => set('finalExamFloor', Number(e.target.value))} /></div>
          <div className="space-y-1"><Label>Ham Başarı Alt Limit 1</Label>
            <Input type="number" value={form.rawScoreFloor1} onChange={(e) => set('rawScoreFloor1', Number(e.target.value))} /></div>
          <div className="space-y-1"><Label>Ham Başarı Alt Limit 2</Label>
            <Input type="number" value={form.rawScoreFloor2} onChange={(e) => set('rawScoreFloor2', Number(e.target.value))} /></div>
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
