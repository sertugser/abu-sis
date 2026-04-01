import { useState } from 'react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import {
  Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle,
} from '@/components/ui/dialog';
import { AlertCircle, Edit2, Plus, Trash2, ArrowRight, ShieldCheck } from 'lucide-react';
import { usePromotionRules, useCreatePromotionRule, useUpdatePromotionRule, useDeletePromotionRule } from '../hooks/usePromotionRule';
import { useAdminPrograms } from '../hooks/useAdminLookup';
import type { PromotionRuleDto } from '../api/promotionRule.api';

interface RuleForm {
  fromYear: number;
  toYear: number;
  minCgpa: string;
  minEcts: string;
  minCourseCount: string;
  maxFailedCourses: string;
  minCompulsoryCoursePassed: string;
}

const emptyForm: RuleForm = {
  fromYear: 1, toYear: 2,
  minCgpa: '1.80', minEcts: '60', minCourseCount: '',
  maxFailedCourses: '', minCompulsoryCoursePassed: '',
};

function toNullableNumber(val: string): number | null {
  if (val === '' || val === undefined) return null;
  const n = Number(val);
  return isNaN(n) ? null : n;
}

const YEAR_COLORS: Record<number, string> = {
  1: 'bg-blue-100 text-blue-700 border-blue-200',
  2: 'bg-emerald-100 text-emerald-700 border-emerald-200',
  3: 'bg-amber-100 text-amber-700 border-amber-200',
  4: 'bg-violet-100 text-violet-700 border-violet-200',
  5: 'bg-rose-100 text-rose-700 border-rose-200',
  6: 'bg-cyan-100 text-cyan-700 border-cyan-200',
};

function yearBadgeClass(year: number) {
  return YEAR_COLORS[year] ?? 'bg-gray-100 text-gray-700 border-gray-200';
}

export default function PromotionRulePage() {
  const { data: programs = [], isLoading: programsLoading } = useAdminPrograms();
  const [selectedProgramId, setSelectedProgramId] = useState<number | undefined>();
  const programId = selectedProgramId ?? programs[0]?.id;

  const { data: rules, isLoading, isError } = usePromotionRules(programId);
  const createMutation = useCreatePromotionRule();
  const updateMutation = useUpdatePromotionRule();
  const deleteMutation = useDeletePromotionRule();

  const [dialogOpen, setDialogOpen] = useState(false);
  const [deleteDialogOpen, setDeleteDialogOpen] = useState(false);
  const [deleteTarget, setDeleteTarget] = useState<PromotionRuleDto | null>(null);
  const [editingRule, setEditingRule] = useState<PromotionRuleDto | null>(null);
  const [form, setForm] = useState<RuleForm>(emptyForm);
  const [error, setError] = useState('');

  const openCreate = () => {
    setEditingRule(null);
    setForm(emptyForm);
    setError('');
    setDialogOpen(true);
  };

  const openEdit = (rule: PromotionRuleDto) => {
    setEditingRule(rule);
    setForm({
      fromYear: rule.fromYear,
      toYear: rule.toYear,
      minCgpa: rule.minCgpa != null ? String(rule.minCgpa) : '',
      minEcts: rule.minEcts != null ? String(rule.minEcts) : '',
      minCourseCount: rule.minCourseCount != null ? String(rule.minCourseCount) : '',
      maxFailedCourses: rule.maxFailedCourses != null ? String(rule.maxFailedCourses) : '',
      minCompulsoryCoursePassed: rule.minCompulsoryCoursePassed != null ? String(rule.minCompulsoryCoursePassed) : '',
    });
    setError('');
    setDialogOpen(true);
  };

  const confirmDelete = (rule: PromotionRuleDto) => {
    setDeleteTarget(rule);
    setDeleteDialogOpen(true);
  };

  const handleDelete = () => {
    if (!deleteTarget) return;
    deleteMutation.mutate(deleteTarget.id, {
      onSuccess: () => { setDeleteDialogOpen(false); setDeleteTarget(null); },
    });
  };

  const handleSubmit = () => {
    setError('');
    if (form.fromYear >= form.toYear) {
      setError('Hedef sınıf, kaynak sınıftan büyük olmalıdır.');
      return;
    }

    const payload = {
      fromYear: form.fromYear,
      toYear: form.toYear,
      minCgpa: toNullableNumber(form.minCgpa),
      minEcts: toNullableNumber(form.minEcts),
      minCourseCount: toNullableNumber(form.minCourseCount),
      maxFailedCourses: toNullableNumber(form.maxFailedCourses),
      minCompulsoryCoursePassed: toNullableNumber(form.minCompulsoryCoursePassed),
    };

    // En az bir kriter girilmiş mi?
    if (payload.minCgpa == null && payload.minEcts == null && payload.minCourseCount == null
      && payload.maxFailedCourses == null && payload.minCompulsoryCoursePassed == null) {
      setError('En az bir kural kriteri belirtilmelidir.');
      return;
    }

    if (editingRule) {
      updateMutation.mutate(
        { id: editingRule.id, dto: payload },
        {
          onSuccess: () => setDialogOpen(false),
          onError: (e: unknown) => {
            const msg = (e as { response?: { data?: { error?: string } } })?.response?.data?.error;
            setError(msg ?? 'Güncelleme başarısız oldu.');
          },
        },
      );
    } else {
      if (!programId) return;
      createMutation.mutate(
        { ...payload, programId },
        {
          onSuccess: () => setDialogOpen(false),
          onError: (e: unknown) => {
            const msg = (e as { response?: { data?: { error?: string } } })?.response?.data?.error;
            setError(msg ?? 'Oluşturma başarısız oldu.');
          },
        },
      );
    }
  };

  if (programsLoading) return <Skeleton className="h-96 rounded-xl" />;

  const ruleList = rules ?? [];

  return (
    <div className="space-y-5">
      {/* Program selector + action */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-3">
          <label className="text-sm font-medium">Program:</label>
          <select
            className="h-9 rounded-md border border-input bg-background px-3 text-sm min-w-[300px]"
            value={programId ?? ''}
            onChange={e => setSelectedProgramId(Number(e.target.value))}
          >
            {programs.map(p => (
              <option key={p.id} value={p.id}>{p.code} — {p.name}</option>
            ))}
          </select>
        </div>
        <Button onClick={openCreate} disabled={!programId}>
          <Plus className="h-4 w-4 mr-1" /> Yeni Kural
        </Button>
      </div>

      {/* Rules */}
      {isLoading ? (
        <Skeleton className="h-48 rounded-xl" />
      ) : isError ? (
        <Alert variant="destructive">
          <AlertCircle className="h-4 w-4" />
          <AlertDescription>Kurallar yüklenemedi.</AlertDescription>
        </Alert>
      ) : ruleList.length === 0 ? (
        <div className="text-center py-16 bg-white border rounded-xl">
          <ShieldCheck className="h-12 w-12 mx-auto text-gray-300 mb-3" />
          <p className="text-muted-foreground">Bu program için henüz sınıf atlatma kuralı tanımlanmamış.</p>
          <Button variant="outline" className="mt-4" onClick={openCreate}>
            <Plus className="h-4 w-4 mr-1" /> İlk Kuralı Ekle
          </Button>
        </div>
      ) : (
        <div className="space-y-3">
          {ruleList.map(rule => (
            <div key={rule.id} className="bg-white border rounded-xl p-5 group hover:shadow-sm transition-shadow">
              <div className="flex items-center justify-between mb-3">
                <div className="flex items-center gap-2">
                  <span className={`text-xs font-semibold px-2.5 py-1 rounded-full border ${yearBadgeClass(rule.fromYear)}`}>
                    {rule.fromYear}. Sınıf
                  </span>
                  <ArrowRight className="h-4 w-4 text-gray-400" />
                  <span className={`text-xs font-semibold px-2.5 py-1 rounded-full border ${yearBadgeClass(rule.toYear)}`}>
                    {rule.toYear}. Sınıf
                  </span>
                </div>
                <div className="flex gap-0.5 opacity-0 group-hover:opacity-100 transition-opacity">
                  <Button size="icon" variant="ghost" className="h-7 w-7" onClick={() => openEdit(rule)}>
                    <Edit2 className="h-3.5 w-3.5" />
                  </Button>
                  <Button
                    size="icon" variant="ghost"
                    className="h-7 w-7 text-destructive hover:text-destructive"
                    onClick={() => confirmDelete(rule)}
                  >
                    <Trash2 className="h-3.5 w-3.5" />
                  </Button>
                </div>
              </div>
              <div className="flex flex-wrap gap-2">
                {rule.minCgpa != null && (
                  <Badge variant="outline" className="text-xs py-1 px-2.5 font-normal">
                    Min GANO: <span className="font-semibold ml-1">{Number(rule.minCgpa).toFixed(2)}</span>
                  </Badge>
                )}
                {rule.minEcts != null && (
                  <Badge variant="outline" className="text-xs py-1 px-2.5 font-normal">
                    Min AKTS: <span className="font-semibold ml-1">{rule.minEcts}</span>
                  </Badge>
                )}
                {rule.minCourseCount != null && (
                  <Badge variant="outline" className="text-xs py-1 px-2.5 font-normal">
                    Min Ders: <span className="font-semibold ml-1">{rule.minCourseCount}</span>
                  </Badge>
                )}
                {rule.maxFailedCourses != null && (
                  <Badge variant="outline" className="text-xs py-1 px-2.5 font-normal border-red-200 text-red-700">
                    Max Başarısız: <span className="font-semibold ml-1">{rule.maxFailedCourses}</span>
                  </Badge>
                )}
                {rule.minCompulsoryCoursePassed != null && (
                  <Badge variant="outline" className="text-xs py-1 px-2.5 font-normal border-emerald-200 text-emerald-700">
                    Min Zorunlu Ders: <span className="font-semibold ml-1">{rule.minCompulsoryCoursePassed}</span>
                  </Badge>
                )}
                {rule.minCgpa == null && rule.minEcts == null && rule.minCourseCount == null
                  && rule.maxFailedCourses == null && rule.minCompulsoryCoursePassed == null && (
                  <span className="text-xs text-muted-foreground">Kriter tanımlanmamış</span>
                )}
              </div>
            </div>
          ))}
        </div>
      )}

      {/* Create/Edit Dialog */}
      <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
        <DialogContent className="sm:max-w-lg">
          <DialogHeader>
            <DialogTitle>{editingRule ? 'Kuralı Düzenle' : 'Yeni Kural Ekle'}</DialogTitle>
          </DialogHeader>
          <div className="space-y-4 py-2">
            {/* Sınıf Geçişi */}
            <div className="grid grid-cols-2 gap-3">
              <div className="space-y-1.5">
                <label className="text-sm font-medium">Kaynak Sınıf</label>
                <select
                  className="h-9 w-full rounded-md border border-input bg-background px-3 text-sm"
                  value={form.fromYear}
                  onChange={e => {
                    const from = Number(e.target.value);
                    setForm({ ...form, fromYear: from, toYear: Math.max(from + 1, form.toYear) });
                  }}
                >
                  {[1, 2, 3, 4, 5, 6].map(y => (
                    <option key={y} value={y}>{y}. Sınıf</option>
                  ))}
                </select>
              </div>
              <div className="space-y-1.5">
                <label className="text-sm font-medium">Hedef Sınıf</label>
                <select
                  className="h-9 w-full rounded-md border border-input bg-background px-3 text-sm"
                  value={form.toYear}
                  onChange={e => setForm({ ...form, toYear: Number(e.target.value) })}
                >
                  {[2, 3, 4, 5, 6, 7].filter(y => y > form.fromYear).map(y => (
                    <option key={y} value={y}>{y}. Sınıf</option>
                  ))}
                </select>
              </div>
            </div>

            <div className="border-t pt-4">
              <p className="text-xs text-muted-foreground mb-3">Aşağıdaki kriterlerden en az birini doldurun. Boş bırakılan kriterler değerlendirilmez.</p>

              {/* Akademik Performans */}
              <div className="space-y-3">
                <p className="text-xs font-semibold text-gray-500 uppercase tracking-wider">Akademik Performans</p>
                <div className="grid grid-cols-2 gap-3">
                  <div className="space-y-1.5">
                    <label className="text-sm font-medium">Min GANO</label>
                    <Input
                      type="number" step="0.01" min={0} max={4}
                      value={form.minCgpa}
                      onChange={e => setForm({ ...form, minCgpa: e.target.value })}
                      placeholder="ör: 1.80"
                    />
                  </div>
                  <div className="space-y-1.5">
                    <label className="text-sm font-medium">Max Başarısız Ders</label>
                    <Input
                      type="number" min={0}
                      value={form.maxFailedCourses}
                      onChange={e => setForm({ ...form, maxFailedCourses: e.target.value })}
                      placeholder="ör: 3"
                    />
                  </div>
                </div>
              </div>

              {/* Ders Tamamlama */}
              <div className="space-y-3 mt-4">
                <p className="text-xs font-semibold text-gray-500 uppercase tracking-wider">Ders Tamamlama</p>
                <div className="grid grid-cols-3 gap-3">
                  <div className="space-y-1.5">
                    <label className="text-sm font-medium">Min AKTS</label>
                    <Input
                      type="number" min={0}
                      value={form.minEcts}
                      onChange={e => setForm({ ...form, minEcts: e.target.value })}
                      placeholder="ör: 60"
                    />
                  </div>
                  <div className="space-y-1.5">
                    <label className="text-sm font-medium">Min Ders Sayısı</label>
                    <Input
                      type="number" min={0}
                      value={form.minCourseCount}
                      onChange={e => setForm({ ...form, minCourseCount: e.target.value })}
                      placeholder="ör: 10"
                    />
                  </div>
                  <div className="space-y-1.5">
                    <label className="text-sm font-medium">Min Zorunlu Ders</label>
                    <Input
                      type="number" min={0}
                      value={form.minCompulsoryCoursePassed}
                      onChange={e => setForm({ ...form, minCompulsoryCoursePassed: e.target.value })}
                      placeholder="ör: 8"
                    />
                  </div>
                </div>
              </div>
            </div>

            {error && (
              <Alert variant="destructive">
                <AlertCircle className="h-4 w-4" />
                <AlertDescription>{error}</AlertDescription>
              </Alert>
            )}
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDialogOpen(false)}>İptal</Button>
            <Button
              onClick={handleSubmit}
              disabled={createMutation.isPending || updateMutation.isPending}
            >
              {createMutation.isPending || updateMutation.isPending ? 'Kaydediliyor...' : editingRule ? 'Güncelle' : 'Oluştur'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Delete confirmation dialog */}
      <Dialog open={deleteDialogOpen} onOpenChange={setDeleteDialogOpen}>
        <DialogContent className="sm:max-w-sm">
          <DialogHeader>
            <DialogTitle>Kuralı Sil</DialogTitle>
          </DialogHeader>
          <p className="text-sm text-muted-foreground">
            <strong>{deleteTarget?.fromYear}. Sınıf → {deleteTarget?.toYear}. Sınıf</strong> geçiş kuralını silmek istediğinize emin misiniz?
          </p>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDeleteDialogOpen(false)}>İptal</Button>
            <Button variant="destructive" onClick={handleDelete} disabled={deleteMutation.isPending}>
              {deleteMutation.isPending ? 'Siliniyor...' : 'Sil'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
