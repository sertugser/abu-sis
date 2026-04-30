import { useMemo, useState } from 'react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Skeleton } from '@/components/ui/skeleton';
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/components/ui/select';
import {
  Dialog, DialogFooter, DialogHeader, DialogTitle,
} from '@/components/ui/dialog';
import ResizableDialogContent from '@/components/ui/resizable-dialog';
import { AlertCircle, Edit2, Loader2, Plus, Trash2 } from 'lucide-react';
import {
  usePromotionRules,
  useCreatePromotionRule,
  useUpdatePromotionRule,
  useDeletePromotionRule,
} from '../../hooks/usePromotionRule';
import { useSystemConfigs } from '../../hooks/useSystemConfig';
import type { PromotionRuleDto } from '../../api/promotionRule.api';

interface PromotionRuleForm {
  fromYear: number;
  minCgpa: string;
  conditionalCgpa: string;
  minEcts: string;
  minCourseCount: string;
  scopeAllProgram: boolean;
}

const emptyPromotionForm: PromotionRuleForm = {
  fromYear: 1,
  minCgpa: '',
  conditionalCgpa: '',
  minEcts: '',
  minCourseCount: '',
  scopeAllProgram: false,
};

interface Props {
  curriculumId: number | undefined;
  programId: number | undefined;
  fromYearOptions: number[];
  /** Modal modunda kullanılır — true ise dış Card kabuğunu çıkarır (zaten dialog kabuk sağlıyor) */
  bare?: boolean;
}

export default function PromotionRulesPanel({
  curriculumId,
  programId,
  fromYearOptions,
  bare = false,
}: Props) {
  // ── Data ──
  const { data: promotionRules = [], isLoading: loadingRules } = usePromotionRules(
    curriculumId !== undefined ? { curriculumId } : undefined,
  );
  const createRuleMut = useCreatePromotionRule();
  const updateRuleMut = useUpdatePromotionRule();
  const deleteRuleMut = useDeletePromotionRule();

  // GPA üst sınırı SystemConfig'ten
  const { data: systemConfigs } = useSystemConfigs();
  const maxGpaScale = useMemo(() => {
    const cfg = systemConfigs?.find(c => c.key === 'GPA.MaxScale');
    if (!cfg) return 4.00;
    const parsed = Number(cfg.value);
    return Number.isFinite(parsed) && parsed > 0 ? parsed : 4.00;
  }, [systemConfigs]);

  // Dialog state
  const [showPromotionDialog, setShowPromotionDialog] = useState(false);
  const [editingRule, setEditingRule] = useState<PromotionRuleDto | null>(null);
  const [promotionForm, setPromotionForm] = useState<PromotionRuleForm>(emptyPromotionForm);
  const [promotionError, setPromotionError] = useState('');
  const [deleteTarget, setDeleteTarget] = useState<PromotionRuleDto | null>(null);

  function openAddPromotion(fromYear?: number) {
    setEditingRule(null);
    setPromotionForm({ ...emptyPromotionForm, fromYear: fromYear ?? 1 });
    setPromotionError('');
    setShowPromotionDialog(true);
  }

  function openEditPromotion(rule: PromotionRuleDto) {
    setEditingRule(rule);
    setPromotionForm({
      fromYear: rule.fromYear,
      minCgpa: rule.minCgpa != null ? String(rule.minCgpa) : '',
      conditionalCgpa: '',
      minEcts: rule.minEcts != null ? String(rule.minEcts) : '',
      minCourseCount: rule.minCourseCount != null ? String(rule.minCourseCount) : '',
      scopeAllProgram: rule.curriculumId == null,
    });
    setPromotionError('');
    setShowPromotionDialog(true);
  }

  function toNullableNumber(val: string): number | null {
    if (val.trim() === '') return null;
    const n = Number(val);
    return Number.isNaN(n) ? null : n;
  }

  function handleSavePromotion() {
    if (!programId) {
      setPromotionError('Program seçilmemiş.');
      return;
    }

    const minCgpa = toNullableNumber(promotionForm.minCgpa);
    const minEcts = toNullableNumber(promotionForm.minEcts);
    const minCourseCount = toNullableNumber(promotionForm.minCourseCount);

    if (minCgpa === null && minEcts === null && minCourseCount === null) {
      setPromotionError('En az bir kriter (Ort/AKTS/Ders Say) doldurulmalı.');
      return;
    }

    const targetCurriculumId = promotionForm.scopeAllProgram ? null : (curriculumId ?? null);

    const dto = {
      fromYear: promotionForm.fromYear,
      toYear: promotionForm.fromYear + 1,
      minCgpa,
      minEcts,
      minCourseCount,
      maxFailedCourses: null,
      minCompulsoryCoursePassed: null,
      curriculumId: targetCurriculumId,
    };

    setPromotionError('');

    if (editingRule) {
      updateRuleMut.mutate(
        { id: editingRule.id, dto },
        {
          onSuccess: () => setShowPromotionDialog(false),
          onError: (err: unknown) => {
            const msg = (err as { response?: { data?: { error?: string } } })?.response?.data?.error;
            setPromotionError(msg ?? 'Kayıt başarısız.');
          },
        },
      );
    } else {
      createRuleMut.mutate(
        { ...dto, programId },
        {
          onSuccess: () => setShowPromotionDialog(false),
          onError: (err: unknown) => {
            const msg = (err as { response?: { data?: { error?: string } } })?.response?.data?.error;
            setPromotionError(msg ?? 'Kayıt başarısız.');
          },
        },
      );
    }
  }

  function handleDelete() {
    if (!deleteTarget) return;
    deleteRuleMut.mutate(deleteTarget.id, {
      onSuccess: () => setDeleteTarget(null),
    });
  }

  // FromYear bazlı grupla — müfredata özel rule varsa onu göster, yoksa program-default'u göster
  const promotionRowsByFromYear = useMemo(() => {
    const map = new Map<number, PromotionRuleDto>();
    for (const r of promotionRules) {
      if (r.curriculumId == null) map.set(r.fromYear, r);
    }
    for (const r of promotionRules) {
      if (r.curriculumId === curriculumId) map.set(r.fromYear, r);
    }
    return map;
  }, [promotionRules, curriculumId]);

  // ── Tablo içeriği ──
  const tableContent = (
    <>
      {!curriculumId ? (
        <p className="text-sm text-muted-foreground italic py-8 text-center">
          Atlatma kurallarını görmek için bir müfredat seçin.
        </p>
      ) : loadingRules ? (
        <Skeleton className="h-32 w-full" />
      ) : (
        <div className="overflow-x-auto rounded border">
          <table className="w-full text-sm">
            <thead className="bg-muted/50 border-b">
              <tr className="text-left">
                <th className="px-3 py-2.5 font-medium">Sınıf</th>
                <th className="px-3 py-2.5 font-medium text-right">En Az Ort.</th>
                <th className="px-3 py-2.5 font-medium text-right text-muted-foreground">Şartlı Ort.</th>
                <th className="px-3 py-2.5 font-medium text-right">En Az AKTS</th>
                <th className="px-3 py-2.5 font-medium text-right">En Az Ders</th>
                <th className="px-3 py-2.5 font-medium">Kapsam</th>
                <th className="px-3 py-2.5 font-medium text-center w-20">İşlem</th>
              </tr>
            </thead>
            <tbody>
              {fromYearOptions.map(fromYear => {
                const rule = promotionRowsByFromYear.get(fromYear);
                return (
                  <tr key={fromYear} className="border-b hover:bg-muted/30">
                    <td className="px-3 py-2 font-medium whitespace-nowrap">{fromYear}. Sınıf</td>
                    <td className="px-3 py-2 text-right">
                      {rule?.minCgpa != null ? Number(rule.minCgpa).toFixed(2) : '—'}
                    </td>
                    <td className="px-3 py-2 text-right text-muted-foreground italic">—</td>
                    <td className="px-3 py-2 text-right">{rule?.minEcts ?? '—'}</td>
                    <td className="px-3 py-2 text-right">{rule?.minCourseCount ?? '—'}</td>
                    <td className="px-3 py-2">
                      {rule == null ? (
                        <span className="text-muted-foreground italic text-xs">—</span>
                      ) : rule.curriculumId == null ? (
                        <Badge variant="secondary" className="text-xs">Genel</Badge>
                      ) : (
                        <Badge variant="default" className="text-xs">Özel</Badge>
                      )}
                    </td>
                    <td className="px-3 py-2">
                      <div className="flex items-center justify-center gap-0.5">
                        {rule ? (
                          <>
                            <Button
                              variant="ghost" size="icon" className="h-7 w-7"
                              title="Düzenle"
                              onClick={() => openEditPromotion(rule)}
                            >
                              <Edit2 className="h-3.5 w-3.5" />
                            </Button>
                            <Button
                              variant="ghost" size="icon"
                              className="h-7 w-7 text-destructive hover:text-destructive"
                              title="Sil"
                              onClick={() => setDeleteTarget(rule)}
                            >
                              <Trash2 className="h-3.5 w-3.5" />
                            </Button>
                          </>
                        ) : (
                          <Button
                            variant="ghost" size="sm" className="h-7 text-xs"
                            onClick={() => openAddPromotion(fromYear)}
                          >
                            <Plus className="h-3.5 w-3.5" />
                          </Button>
                        )}
                      </div>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
    </>
  );

  // ── Header (Card mode için) — modal mode'da dialog kendi başlığını taşır ──
  const addButton = (
    <Button
      size="sm" onClick={() => openAddPromotion()}
      disabled={!curriculumId}
    >
      <Plus className="h-4 w-4 mr-1" /> Ekle
    </Button>
  );

  return (
    <>
      {bare ? (
        <div className="space-y-3">
          <div className="flex items-center justify-end">
            {addButton}
          </div>
          {tableContent}
        </div>
      ) : (
        <Card>
          <CardHeader className="pb-3">
            <div className="flex items-center justify-between gap-2">
              <CardTitle className="text-base">Müfredat Dönem Sınıf Atlatma Kuralları</CardTitle>
              {addButton}
            </div>
          </CardHeader>
          <CardContent>
            {tableContent}
          </CardContent>
        </Card>
      )}

      {/* ── Promotion Rule Dialog (Sınıf Atlatma Ekle/Düzenle) ── */}
      <Dialog
        open={showPromotionDialog}
        onOpenChange={(open) => { if (!open) setShowPromotionDialog(false); }}
      >
        <ResizableDialogContent
          defaultWidth={500} minWidth={400} minHeight={400}
          className="flex flex-col p-0 gap-0"
        >
          <DialogHeader className="px-6 pt-5 pb-3 shrink-0 border-b">
            <DialogTitle>
              {editingRule ? 'Sınıf Atlatma Kuralını Düzenle' : 'Sınıf Atlatma Kuralı Ekle'}
            </DialogTitle>
          </DialogHeader>
          <div className="flex-1 overflow-y-auto px-6 py-4 space-y-4">
            {promotionError && (
              <div className="rounded-md border border-destructive/40 bg-destructive/5 px-3 py-2 text-sm text-destructive flex items-start gap-2">
                <AlertCircle className="h-4 w-4 mt-0.5 shrink-0" />
                <span>{promotionError}</span>
              </div>
            )}

            <div className="space-y-1">
              <Label className="text-xs">Sınıf</Label>
              <Select
                value={promotionForm.fromYear.toString()}
                onValueChange={(v) => setPromotionForm({ ...promotionForm, fromYear: Number(v) })}
                disabled={editingRule !== null}
              >
                <SelectTrigger className="h-9 text-sm">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {fromYearOptions.map(y => (
                    <SelectItem key={y} value={y.toString()}>
                      {y}. Sınıf → {y + 1}. Sınıf
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div className="grid grid-cols-2 gap-3">
              <div className="space-y-1">
                <Label className="text-xs">En Az Ort.</Label>
                <Input
                  type="number" step="0.01" min={0} max={maxGpaScale}
                  value={promotionForm.minCgpa}
                  onChange={(e) => setPromotionForm({ ...promotionForm, minCgpa: e.target.value })}
                  placeholder={`0 — ${maxGpaScale.toFixed(2)}`}
                  className="h-9"
                />
              </div>
              <div className="space-y-1">
                <Label className="text-xs text-muted-foreground">Şartlı Ort.</Label>
                <Input
                  type="number" step="0.01" min={0} max={maxGpaScale}
                  value={promotionForm.conditionalCgpa}
                  disabled
                  placeholder="—"
                  className="h-9"
                  title="Şartlı Ortalama: backend desteği yok, ileride eklenecek"
                />
                <p className="text-[11px] text-muted-foreground">Yakında</p>
              </div>
            </div>

            <div className="grid grid-cols-2 gap-3">
              <div className="space-y-1">
                <Label className="text-xs">En Az AKTS</Label>
                <Input
                  type="number" min={0}
                  value={promotionForm.minEcts}
                  onChange={(e) => setPromotionForm({ ...promotionForm, minEcts: e.target.value })}
                  placeholder="örn. 60"
                  className="h-9"
                />
              </div>
              <div className="space-y-1">
                <Label className="text-xs">En Az Ders Say.</Label>
                <Input
                  type="number" min={0}
                  value={promotionForm.minCourseCount}
                  onChange={(e) => setPromotionForm({ ...promotionForm, minCourseCount: e.target.value })}
                  placeholder="örn. 8"
                  className="h-9"
                />
              </div>
            </div>

            <div className="rounded-md border bg-muted/20 p-3">
              <label className="flex items-start gap-2 cursor-pointer select-none">
                <input
                  type="checkbox"
                  className="h-4 w-4 mt-0.5 rounded border-input accent-primary"
                  checked={promotionForm.scopeAllProgram}
                  onChange={(e) => setPromotionForm({ ...promotionForm, scopeAllProgram: e.target.checked })}
                />
                <div className="text-sm">
                  <div className="font-medium">Programın tüm müfredatlarına uygula</div>
                  <p className="text-xs text-muted-foreground mt-0.5">
                    İşaretliyse bu kural seçili programın tüm müfredat versiyonları için default olarak uygulanır.
                    Belirli bir müfredat için override etmek istersen seçimi kaldır.
                  </p>
                </div>
              </label>
            </div>
          </div>
          <DialogFooter className="shrink-0 px-6 py-4 border-t bg-background">
            <Button variant="outline" onClick={() => setShowPromotionDialog(false)}>İptal</Button>
            <Button
              onClick={handleSavePromotion}
              disabled={createRuleMut.isPending || updateRuleMut.isPending}
            >
              {(createRuleMut.isPending || updateRuleMut.isPending) && (
                <Loader2 className="h-4 w-4 animate-spin mr-2" />
              )}
              Kaydet
            </Button>
          </DialogFooter>
        </ResizableDialogContent>
      </Dialog>

      {/* ── Delete Confirm Dialog ── */}
      <Dialog
        open={deleteTarget !== null}
        onOpenChange={(open) => { if (!open) setDeleteTarget(null); }}
      >
        <ResizableDialogContent defaultWidth={420} className="p-6">
          <DialogHeader className="mb-3">
            <DialogTitle>Kuralı Sil</DialogTitle>
          </DialogHeader>
          <p className="text-sm">
            <strong>{deleteTarget?.fromYear}. Sınıf → {deleteTarget?.toYear}. Sınıf</strong> atlatma kuralı silinecek. Onaylıyor musun?
          </p>
          <DialogFooter className="mt-4 gap-2">
            <Button variant="outline" onClick={() => setDeleteTarget(null)}>İptal</Button>
            <Button
              variant="destructive"
              onClick={handleDelete}
              disabled={deleteRuleMut.isPending}
            >
              {deleteRuleMut.isPending && <Loader2 className="h-4 w-4 animate-spin mr-2" />}
              Sil
            </Button>
          </DialogFooter>
        </ResizableDialogContent>
      </Dialog>
    </>
  );
}
