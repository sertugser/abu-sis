import { useState } from 'react';
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Switch } from '@/components/ui/switch';
import { Tabs, TabsList, TabsTrigger, TabsContent } from '@/components/ui/tabs';
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/components/ui/select';
import { Loader2 } from 'lucide-react';
import { useAdminPrograms } from '../../hooks/useAdminLookup';
import { useCreateExamRegulation, useUpdateExamRegulation } from '../../hooks/useExamRegulations';
import type { ExamRegulation, SaveExamRegulationRequest } from '../../types/examRegulation';
import { TERM_TYPES, TERM_LABELS, LEVEL_LABELS } from '../../lib/examRegulationLabels';
import { GRADE_BADGE_CLASS } from '../../lib/gradeUtils';

const GRADE_CODES = Object.keys(GRADE_BADGE_CLASS) as string[];
const LEVEL_CODES = Object.keys(LEVEL_LABELS) as string[];

interface Props { open: boolean; onOpenChange: (v: boolean) => void; editTarget: ExamRegulation | null; }

const emptyForm: SaveExamRegulationRequest = {
  programId: 0, degreeLevel: 'Bachelor', termType: 'Fall', year: new Date().getFullYear(),
  isActive: true, name: '', midtermCount: 1, finalCount: 1,
  minMidtermWeightPercent: 40, maxMidtermWeightPercent: 60,
  minFinalWeightPercent: 40, maxFinalWeightPercent: 60,
  passingGrade: 50, rawScoreFloor1: 0, rawScoreFloor2: 0, finalExamFloor: 0,
  aaMin: 91, baMin: 81, bbMin: 71, cbMin: 61, ccMin: 55, dcMin: 50, ddMin: 45,
  letterRangesEditable: true, belowFloorGrade: 'FF',
  evaluationType: 'Absolute',
  relativeMinStudentCount: 10, relativeInclusionLimit: 0, relativeAbsoluteFloor: 0,
  tAAMin: 70, tBAMin: 63, tBBMin: 57, tCBMin: 50, tCCMin: 45, tDCMin: 40, tDDMin: 35,
  blockFinalIfNoMidterm: false,
  failIfNoFinal: false,
  blockMakeupIfNoFinal: false,
  zeroIfMissedMakeup: false,
  makeupReplacesFinal: false,
  autoTransferFailedToMakeup: false,
  autoTransferExcusedToMakeup: false,
};

const LETTER_FIELDS = [
  { key: 'aaMin', label: 'AA' }, { key: 'baMin', label: 'BA' }, { key: 'bbMin', label: 'BB' },
  { key: 'cbMin', label: 'CB' }, { key: 'ccMin', label: 'CC' }, { key: 'dcMin', label: 'DC' },
  { key: 'ddMin', label: 'DD' },
] as const;

const T_LETTER_FIELDS = [
  { key: 'tAAMin', label: 'T-AA' }, { key: 'tBAMin', label: 'T-BA' }, { key: 'tBBMin', label: 'T-BB' },
  { key: 'tCBMin', label: 'T-CB' }, { key: 'tCCMin', label: 'T-CC' }, { key: 'tDCMin', label: 'T-DC' },
  { key: 'tDDMin', label: 'T-DD' },
] as const;

const RULE_FIELDS = [
  { key: 'blockFinalIfNoMidterm', label: 'Hiçbir ara sınava girmeyen finale giremez', hint: 'Kapalıyken ara sınava girmeyen de finale girebilir.' },
  { key: 'failIfNoFinal', label: 'Finale girmeyen doğrudan kalır', hint: 'Kapalıyken geçme notunu tutturan öğrenci finale girmese de geçer.' },
  { key: 'blockMakeupIfNoFinal', label: 'Finale (mazeretsiz) girmeyen bütünlemeye giremez', hint: '' },
  { key: 'zeroIfMissedMakeup', label: 'Bütünleme listesinde olup puan girilmeyene 0 ver', hint: '' },
  { key: 'makeupReplacesFinal', label: 'Bütünleme notu finalin yerine değerlendirilir', hint: 'Aynı ağırlıkla final bileşeninin yerine geçer.' },
  { key: 'autoTransferFailedToMakeup', label: 'Kalanlar otomatik bütünleme listesine eklenir', hint: '' },
  { key: 'autoTransferExcusedToMakeup', label: 'Mazeretliler otomatik mazeret listesine eklenir', hint: '' },
] as const;

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

  const num = <K extends keyof SaveExamRegulationRequest>(k: K) =>
    (e: React.ChangeEvent<HTMLInputElement>) => set(k, Number(e.target.value) as SaveExamRegulationRequest[K]);

  const midtermWeightInvalid = form.minMidtermWeightPercent > form.maxMidtermWeightPercent;
  const finalWeightInvalid = form.minFinalWeightPercent > form.maxFinalWeightPercent;
  const noProgram = !form.programId;

  const submit = () => {
    if (editTarget) updateMut.mutate({ id: editTarget.id, dto: form }, { onSuccess: () => onOpenChange(false) });
    else createMut.mutate(form, { onSuccess: () => onOpenChange(false) });
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-3xl max-h-[88vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>{editTarget ? 'Sınav Yönergesi Düzenle' : 'Yeni Sınav Yönergesi'}</DialogTitle>
        </DialogHeader>

        <Tabs defaultValue="identity" className="w-full">
          <TabsList className="flex h-auto w-full flex-wrap justify-start gap-1">
            <TabsTrigger value="identity">Kimlik</TabsTrigger>
            <TabsTrigger value="exam">Sınav &amp; Ağırlık</TabsTrigger>
            <TabsTrigger value="passing">Geçme &amp; Baraj</TabsTrigger>
            <TabsTrigger value="letters">Harf Tablosu</TabsTrigger>
            <TabsTrigger value="evaluation">Değerlendirme</TabsTrigger>
            <TabsTrigger value="rules">Kurallar</TabsTrigger>
          </TabsList>

          {/* ── Kimlik ── */}
          <TabsContent value="identity" className="mt-4">
            <div className="grid gap-4 md:grid-cols-2">
              <div className="space-y-1.5 md:col-span-2">
                <Label className="text-sm font-medium">Program</Label>
                <Select
                  value={form.programId ? String(form.programId) : ''}
                  onValueChange={(v) => set('programId', Number(v))}
                >
                  <SelectTrigger className={noProgram ? 'border-destructive/60' : ''}>
                    <SelectValue placeholder="Program seçiniz" />
                  </SelectTrigger>
                  <SelectContent>
                    {(programs.data ?? []).map((p) => (
                      <SelectItem key={p.id} value={String(p.id)}>{p.name}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                {noProgram && <p className="text-xs text-destructive">Program seçilmeli.</p>}
              </div>

              <div className="space-y-1.5">
                <Label className="text-sm font-medium">Öğrenci Tipi</Label>
                <Select
                  value={form.degreeLevel}
                  onValueChange={(v) => set('degreeLevel', v as SaveExamRegulationRequest['degreeLevel'])}
                >
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>
                    {LEVEL_CODES.map((c) => <SelectItem key={c} value={c}>{LEVEL_LABELS[c]}</SelectItem>)}
                  </SelectContent>
                </Select>
              </div>

              <div className="space-y-1.5">
                <Label className="text-sm font-medium">Dönem Tipi</Label>
                <Select value={form.termType} onValueChange={(v) => set('termType', v)}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>
                    {TERM_TYPES.map((t) => <SelectItem key={t} value={t}>{TERM_LABELS[t]}</SelectItem>)}
                  </SelectContent>
                </Select>
              </div>

              <div className="space-y-1.5">
                <Label className="text-sm font-medium">Yıl</Label>
                <Input type="number" min={0} value={form.year} onChange={num('year')} />
              </div>

              <div className="flex items-center gap-2 pt-6">
                <Switch checked={form.isActive} onCheckedChange={(v) => set('isActive', v)} />
                <Label className="text-sm font-normal">Aktif</Label>
              </div>

              <div className="space-y-1.5 md:col-span-2">
                <Label className="text-sm font-medium">Adı</Label>
                <Input
                  value={form.name}
                  onChange={(e) => set('name', e.target.value)}
                  placeholder="örneğin: Lisans Güz Sınav Yönergesi 2026"
                />
              </div>
            </div>
          </TabsContent>

          {/* ── Sınav & Ağırlık ── */}
          <TabsContent value="exam" className="mt-4 space-y-4">
            <div className="grid gap-4 md:grid-cols-2">
              <div className="space-y-1.5">
                <Label className="text-sm font-medium">Ara Sınav Sayısı</Label>
                <Input type="number" min={0} value={form.midtermCount} onChange={num('midtermCount')} />
              </div>
              <div className="space-y-1.5">
                <Label className="text-sm font-medium">YYS (Final) Sınav Sayısı</Label>
                <Input type="number" min={0} value={form.finalCount} onChange={num('finalCount')} />
              </div>
            </div>

            <div className="rounded-lg border p-3 space-y-3">
              <p className="text-sm font-medium">Ara Sınav Ağırlık Aralığı (%)</p>
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-1.5">
                  <Label className="text-xs text-muted-foreground">En Az</Label>
                  <Input type="number" min={0} max={100} value={form.minMidtermWeightPercent} onChange={num('minMidtermWeightPercent')} />
                </div>
                <div className="space-y-1.5">
                  <Label className="text-xs text-muted-foreground">En Çok</Label>
                  <Input type="number" min={0} max={100} value={form.maxMidtermWeightPercent} onChange={num('maxMidtermWeightPercent')} />
                </div>
              </div>
              {midtermWeightInvalid && <p className="text-xs text-destructive">En az, en çoktan büyük olamaz.</p>}
            </div>

            <div className="rounded-lg border p-3 space-y-3">
              <p className="text-sm font-medium">YYS (Final) Ağırlık Aralığı (%)</p>
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-1.5">
                  <Label className="text-xs text-muted-foreground">En Az</Label>
                  <Input type="number" min={0} max={100} value={form.minFinalWeightPercent} onChange={num('minFinalWeightPercent')} />
                </div>
                <div className="space-y-1.5">
                  <Label className="text-xs text-muted-foreground">En Çok</Label>
                  <Input type="number" min={0} max={100} value={form.maxFinalWeightPercent} onChange={num('maxFinalWeightPercent')} />
                </div>
              </div>
              {finalWeightInvalid && <p className="text-xs text-destructive">En az, en çoktan büyük olamaz.</p>}
            </div>
            <p className="text-xs text-muted-foreground">
              Hoca, sınav ağırlıklarını bu aralıkların dışında belirleyemez.
            </p>
          </TabsContent>

          {/* ── Geçme & Baraj ── */}
          <TabsContent value="passing" className="mt-4">
            <div className="grid gap-4 md:grid-cols-2">
              <div className="space-y-1.5">
                <Label className="text-sm font-medium">Geçme Notu</Label>
                <Input type="number" min={0} max={100} value={form.passingGrade} onChange={num('passingGrade')} />
                <p className="text-xs text-muted-foreground">Ağırlıklı ham puanı bunun altında kalan öğrenci kalır.</p>
              </div>
              <div className="space-y-1.5">
                <Label className="text-sm font-medium">Baraj Altına Atanacak Harf</Label>
                <Select value={form.belowFloorGrade} onValueChange={(v) => set('belowFloorGrade', v)}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>
                    {GRADE_CODES.map((c) => <SelectItem key={c} value={c}>{c}</SelectItem>)}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-1.5">
                <Label className="text-sm font-medium">YYS (Final) Barajı</Label>
                <Input type="number" min={0} value={form.finalExamFloor} onChange={num('finalExamFloor')} />
                <p className="text-xs text-muted-foreground">Final puanı bunun altındaysa baraj uygulanır (0 = pasif).</p>
              </div>
              <div className="grid grid-cols-2 gap-4 md:col-span-2">
                <div className="space-y-1.5">
                  <Label className="text-sm font-medium">Ham Başarı Alt Limit 1</Label>
                  <Input type="number" min={0} value={form.rawScoreFloor1} onChange={num('rawScoreFloor1')} />
                </div>
                <div className="space-y-1.5">
                  <Label className="text-sm font-medium">Ham Başarı Alt Limit 2</Label>
                  <Input type="number" min={0} value={form.rawScoreFloor2} onChange={num('rawScoreFloor2')} />
                </div>
              </div>
            </div>
          </TabsContent>

          {/* ── Harf Tablosu ── */}
          <TabsContent value="letters" className="mt-4 space-y-3">
            <div className="flex items-center justify-between">
              <p className="text-sm text-muted-foreground">Her harf notu için ham puan alt sınırı.</p>
              <div className="flex items-center gap-2">
                <Switch checked={form.letterRangesEditable} onCheckedChange={(v) => set('letterRangesEditable', v)} />
                <Label className="text-sm font-normal">Hoca harf aralığını değiştirebilir</Label>
              </div>
            </div>
            <div className="grid grid-cols-2 gap-3 sm:grid-cols-4 lg:grid-cols-7">
              {LETTER_FIELDS.map(({ key, label }) => (
                <div key={key} className="space-y-1.5">
                  <Label className="font-mono text-xs">{label}</Label>
                  <Input type="number" min={0} max={100} value={form[key]} onChange={num(key)} />
                </div>
              ))}
            </div>
          </TabsContent>

          {/* ── Değerlendirme ── */}
          <TabsContent value="evaluation" className="mt-4 space-y-4">
            <div className="space-y-1.5">
              <Label className="text-sm font-medium">Değerlendirme Tipi</Label>
              <Select
                value={form.evaluationType}
                onValueChange={(v) => set('evaluationType', v as SaveExamRegulationRequest['evaluationType'])}
              >
                <SelectTrigger className="md:w-1/2"><SelectValue /></SelectTrigger>
                <SelectContent>
                  <SelectItem value="Absolute">Mutlak</SelectItem>
                  <SelectItem value="Relative">Bağıl (Çan Eğrisi)</SelectItem>
                </SelectContent>
              </Select>
            </div>

            {form.evaluationType === 'Relative' ? (
              <div className="space-y-4">
                <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
                  <div className="space-y-1.5">
                    <Label className="text-xs text-muted-foreground">En Az Öğrenci Sayısı</Label>
                    <Input type="number" min={0} value={form.relativeMinStudentCount} onChange={num('relativeMinStudentCount')} />
                  </div>
                  <div className="space-y-1.5">
                    <Label className="text-xs text-muted-foreground">Katma Limiti</Label>
                    <Input type="number" min={0} value={form.relativeInclusionLimit} onChange={num('relativeInclusionLimit')} />
                  </div>
                  <div className="space-y-1.5">
                    <Label className="text-xs text-muted-foreground">Mutlak Alt Sınır</Label>
                    <Input type="number" min={0} value={form.relativeAbsoluteFloor} onChange={num('relativeAbsoluteFloor')} />
                  </div>
                </div>
                <div>
                  <p className="mb-2 text-sm font-medium">T-Skor Harf Sınırları</p>
                  <div className="grid grid-cols-2 gap-3 sm:grid-cols-4 lg:grid-cols-7">
                    {T_LETTER_FIELDS.map(({ key, label }) => (
                      <div key={key} className="space-y-1.5">
                        <Label className="font-mono text-xs">{label}</Label>
                        <Input type="number" min={0} value={form[key]} onChange={num(key)} />
                      </div>
                    ))}
                  </div>
                </div>
                <p className="text-xs text-muted-foreground">
                  Çan eğrisi sınıfın ortalaması ve dağılımına göre harf verir. Öğrenci sayısı
                  &apos;En Az Öğrenci Sayısı&apos; altındaysa otomatik mutlak değerlendirmeye döner.
                </p>
              </div>
            ) : (
              <p className="text-sm text-muted-foreground">
                Mutlak değerlendirmede harfler doğrudan <span className="font-medium text-foreground">Harf Tablosu</span> sekmesindeki
                ham puan sınırlarına göre verilir.
              </p>
            )}
          </TabsContent>

          {/* ── Kurallar ── */}
          <TabsContent value="rules" className="mt-4">
            <div className="divide-y divide-border/60">
              {RULE_FIELDS.map(({ key, label, hint }) => (
                <div key={key} className="flex items-start justify-between gap-4 py-3">
                  <div>
                    <Label className="text-sm font-normal">{label}</Label>
                    {hint && <p className="mt-0.5 text-xs text-muted-foreground">{hint}</p>}
                  </div>
                  <Switch checked={form[key]} onCheckedChange={(v) => set(key, v)} className="mt-0.5 shrink-0" />
                </div>
              ))}
            </div>
          </TabsContent>
        </Tabs>

        {mutation.isError && (
          <p className="text-sm text-destructive">
            {(mutation.error as { response?: { data?: { message?: string } } })?.response?.data?.message ?? 'Kaydedilemedi.'}
          </p>
        )}

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>İptal</Button>
          <Button onClick={submit} disabled={mutation.isPending || noProgram}>
            {mutation.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
            Kaydet
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
