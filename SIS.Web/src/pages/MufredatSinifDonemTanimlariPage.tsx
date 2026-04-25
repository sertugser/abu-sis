import { useState, useMemo } from 'react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardFooter, CardHeader, CardTitle } from '@/components/ui/card';
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
import {
  AlertCircle, Copy, Edit2, Loader2, Plus, Scissors, Trash2, Users,
} from 'lucide-react';
import {
  useAdminFaculties,
  useAdminDepartmentsByStructure,
  useAdminProgramsByStructure,
} from '../hooks/useAdminAcademicStructure';
import { useCurriculumList } from '../hooks/useCurriculumManagement';
import {
  usePromotionRules,
  useCreatePromotionRule,
  useUpdatePromotionRule,
  useDeletePromotionRule,
} from '../hooks/usePromotionRule';
import { useCurriculumTermRules } from '../hooks/useCurriculumTermRules';
import { useSystemConfigs } from '../hooks/useSystemConfig';
import type { PromotionRuleDto } from '../api/promotionRule.api';
import {
  ALL_CLASS_YEARS,
  ALL_RULE_TYPES,
  ALL_TERM_HALVES,
  CLASS_YEAR_LABELS,
  RULE_TYPE_LABELS,
  TERM_HALF_LABELS,
  GPA_RULE_TYPES,
} from '../types/curriculumTermRule';
import type {
  CurriculumTermRuleType,
} from '../types/curriculumTermRule';
import { CurriculumTermRulesDialog } from '../components/curriculum-management/CurriculumTermRulesDialog';

// ─── Sınıf Atlatma Kuralı dialog form state ───────────────────────────
interface PromotionRuleForm {
  fromYear: number;
  minCgpa: string;
  // Şartlı Ort: backend'de yok, UI'da disabled gösterilir (Proliz görselle uyum için)
  conditionalCgpa: string;
  minEcts: string;
  minCourseCount: string;
  scopeAllProgram: boolean; // true = program-default (CurriculumId null), false = müfredata özel
}

const emptyPromotionForm: PromotionRuleForm = {
  fromYear: 1,
  minCgpa: '',
  conditionalCgpa: '',
  minEcts: '',
  minCourseCount: '',
  scopeAllProgram: false,
};

// fromYearOptions dinamik olarak program.maxStudyDurationSemesters'tan türetilir.
// Lisans (8 yarıyıl) → max sınıf 4 → atlatma 1→2, 2→3, 3→4
// Tıp (12 yarıyıl) → max sınıf 6 → atlatma 1→2, ..., 5→6
// Önlisans (4 yarıyıl) → max sınıf 2 → atlatma 1→2
const DEFAULT_MAX_YEAR = 4;

export default function MufredatSinifDonemTanimlariPage() {
  // ── Cascade state ──
  const [facultyId, setFacultyId] = useState<number | undefined>();
  const [departmentId, setDepartmentId] = useState<number | undefined>();
  const [programId, setProgramId] = useState<number | undefined>();
  const [curriculumId, setCurriculumId] = useState<number | undefined>();

  // ── Cascade queries ──
  const { data: faculties = [], isLoading: loadingFaculties } = useAdminFaculties();
  const { data: departments = [] } = useAdminDepartmentsByStructure(facultyId);
  const { data: programs = [] } = useAdminProgramsByStructure(departmentId);
  const { data: curricula = [] } = useCurriculumList({ programId });

  // ── Data queries (only when curriculum selected) ──
  const { data: promotionRules = [], isLoading: loadingRules } = usePromotionRules(
    curriculumId !== undefined ? { curriculumId } : undefined,
  );
  const { data: termRules = [] } = useCurriculumTermRules(curriculumId ?? null);

  const createRuleMut = useCreatePromotionRule();
  const updateRuleMut = useUpdatePromotionRule();
  const deleteRuleMut = useDeletePromotionRule();

  // ── GPA üst sınırı SystemConfig'ten (4.00 default — 4'lük sistem) ──
  const { data: systemConfigs } = useSystemConfigs();
  const maxGpaScale = useMemo(() => {
    const cfg = systemConfigs?.find(c => c.key === 'GPA.MaxScale');
    if (!cfg) return 4.00;
    const parsed = Number(cfg.value);
    return Number.isFinite(parsed) && parsed > 0 ? parsed : 4.00;
  }, [systemConfigs]);

  // ── Dialog state ──
  const [showPromotionDialog, setShowPromotionDialog] = useState(false);
  const [editingRule, setEditingRule] = useState<PromotionRuleDto | null>(null);
  const [promotionForm, setPromotionForm] = useState<PromotionRuleForm>(emptyPromotionForm);
  const [promotionError, setPromotionError] = useState('');
  const [deleteTarget, setDeleteTarget] = useState<PromotionRuleDto | null>(null);

  const [showTermRulesDialog, setShowTermRulesDialog] = useState(false);

  // ── Cascade handlers ──
  function handleFacultyChange(val: string) {
    setFacultyId(Number(val));
    setDepartmentId(undefined);
    setProgramId(undefined);
    setCurriculumId(undefined);
  }
  function handleDepartmentChange(val: string) {
    setDepartmentId(Number(val));
    setProgramId(undefined);
    setCurriculumId(undefined);
  }
  function handleProgramChange(val: string) {
    setProgramId(Number(val));
    setCurriculumId(undefined);
  }

  // ── Promotion rule dialog handlers ──
  function openAddPromotion() {
    setEditingRule(null);
    setPromotionForm(emptyPromotionForm);
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

  // ── Derived: from year options (program'ın AKTS yüküne göre normal süre) ──
  // YÖK standardı: 60 AKTS = 1 yıl (lisans 240 → 4 yıl, tıp 360 → 6 yıl, önlisans 120 → 2 yıl)
  // maxStudyDurationSemesters AZAMİ süredir (lisans 14 dönem = 7 yıl), atlatma için yanlış kaynak.
  const fromYearOptions = useMemo<number[]>(() => {
    const program = programs.find(p => p.id === programId);
    const ects = program?.totalRequiredEcts;
    const maxYear = ects && ects > 0 ? Math.ceil(ects / 60) : DEFAULT_MAX_YEAR;
    return Array.from({ length: Math.max(1, maxYear - 1) }, (_, i) => i + 1);
  }, [programs, programId]);

  // ── Derived: gösterilecek atlatma kural satırları ──
  // FromYear bazlı grupla — müfredata özel rule varsa onu göster, yoksa program-default'u göster
  const promotionRowsByFromYear = useMemo(() => {
    const map = new Map<number, PromotionRuleDto>();
    // Önce program-default'lar (CurriculumId == null)
    for (const r of promotionRules) {
      if (r.curriculumId == null) map.set(r.fromYear, r);
    }
    // Sonra müfredat-özel'ler (override)
    for (const r of promotionRules) {
      if (r.curriculumId === curriculumId) map.set(r.fromYear, r);
    }
    return map;
  }, [promotionRules, curriculumId]);

  const selectedCurriculum = curricula.find(c => c.id === curriculumId);
  const curriculumLabel = selectedCurriculum
    ? `(${selectedCurriculum.version}) ${selectedCurriculum.name ?? selectedCurriculum.programName}`
    : '';

  // Cascade row helper — tutarlı label-value yatay düzeni
  const cascadeRow = (
    label: string,
    selectNode: React.ReactNode,
  ) => (
    <div className="grid grid-cols-[100px_1fr] items-center gap-3">
      <Label className="text-sm font-medium text-muted-foreground">{label}</Label>
      {selectNode}
    </div>
  );

  return (
    <div className="space-y-4">
      {/* ── Üst row: Cascade selector (sol, 2/5) + Sınıf Atlatma Kuralları (sağ, 3/5) — Proliz layout
          Responsive: lg ve üstü → yan yana 2/5 + 3/5; md → yan yana eşit; sm/mobile → alt alta tam genişlik ── */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-5 gap-4">
        {/* Sol: Müfredat cascade selector */}
        <Card className="lg:col-span-2">
          <CardHeader className="pb-3">
            <CardTitle className="text-base">Müfredat Seçimi</CardTitle>
          </CardHeader>
          <CardContent className="space-y-3">
            {cascadeRow('Fakülte',
              <Select
                value={facultyId?.toString() ?? ''}
                onValueChange={handleFacultyChange}
                disabled={loadingFaculties}
              >
                <SelectTrigger className="h-9 text-sm">
                  <SelectValue placeholder="Fakülte seçin..." />
                </SelectTrigger>
                <SelectContent>
                  {faculties.map(f => (
                    <SelectItem key={f.id} value={f.id.toString()}>{f.name}</SelectItem>
                  ))}
                </SelectContent>
              </Select>,
            )}
            {cascadeRow('Bölüm',
              <Select
                value={departmentId?.toString() ?? ''}
                onValueChange={handleDepartmentChange}
                disabled={!facultyId}
              >
                <SelectTrigger className="h-9 text-sm">
                  <SelectValue placeholder="Bölüm seçin..." />
                </SelectTrigger>
                <SelectContent>
                  {departments.map(d => (
                    <SelectItem key={d.id} value={d.id.toString()}>{d.name}</SelectItem>
                  ))}
                </SelectContent>
              </Select>,
            )}
            {cascadeRow('Program',
              <Select
                value={programId?.toString() ?? ''}
                onValueChange={handleProgramChange}
                disabled={!departmentId}
              >
                <SelectTrigger className="h-9 text-sm">
                  <SelectValue placeholder="Program seçin..." />
                </SelectTrigger>
                <SelectContent>
                  {programs.map(p => (
                    <SelectItem key={p.id} value={p.id.toString()}>{p.name}</SelectItem>
                  ))}
                </SelectContent>
              </Select>,
            )}
            {cascadeRow('Müfredat',
              <Select
                value={curriculumId?.toString() ?? ''}
                onValueChange={(val) => setCurriculumId(Number(val))}
                disabled={!programId}
              >
                <SelectTrigger className="h-9 text-sm">
                  <SelectValue placeholder="Müfredat seçin..." />
                </SelectTrigger>
                <SelectContent>
                  {curricula.map(c => (
                    <SelectItem key={c.id} value={c.id.toString()}>
                      ({c.version}) {c.name ?? c.programName}
                      {c.isActive && ' • Aktif'}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>,
            )}
          </CardContent>
        </Card>

        {/* Sağ: Sınıf Atlatma Kuralları flat tablo (3/5) */}
        <Card className="lg:col-span-3">
          <CardHeader className="pb-3">
            <div className="flex items-center justify-between gap-2">
              <CardTitle className="text-base">Müfredat Dönem Sınıf Atlatma Kuralları</CardTitle>
              <Button
                size="sm" onClick={openAddPromotion}
                disabled={!curriculumId}
              >
                <Plus className="h-4 w-4 mr-1" /> Ekle
              </Button>
            </div>
          </CardHeader>
          <CardContent>
            {!curriculumId ? (
              <p className="text-sm text-muted-foreground italic py-8 text-center">
                Atlatma kurallarını görmek için soldan bir müfredat seçin.
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
                                  onClick={() => {
                                    setPromotionForm({ ...emptyPromotionForm, fromYear });
                                    setEditingRule(null);
                                    setPromotionError('');
                                    setShowPromotionDialog(true);
                                  }}
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
          </CardContent>
        </Card>
      </div>

      {!curriculumId ? null : (
        <>
          {/* ── Müfredatın Dönem Tanımları matrix view ── */}
          <Card>
            <CardHeader className="pb-3">
              <div className="flex items-center justify-between gap-2">
                <CardTitle className="text-base">Müfredatın Dönem Tanımları</CardTitle>
                <Button
                  size="sm" variant="outline"
                  onClick={() => setShowTermRulesDialog(true)}
                >
                  <Edit2 className="h-4 w-4 mr-1" /> Kural Düzenleyici
                </Button>
              </div>
            </CardHeader>
            <CardContent>
              <div className="overflow-x-auto rounded border">
                <table className="w-full text-xs">
                  <thead className="bg-muted/50 border-b">
                    <tr className="text-left">
                      <th className="px-2 py-2 font-medium whitespace-nowrap sticky left-0 bg-muted/50 z-10">Sınıf</th>
                      <th className="px-2 py-2 font-medium whitespace-nowrap">Dönem</th>
                      {ALL_RULE_TYPES.map(rt => (
                        <th key={rt} className="px-2 py-2 font-medium text-right whitespace-nowrap">
                          {RULE_TYPE_LABELS[rt]}
                        </th>
                      ))}
                    </tr>
                  </thead>
                  <tbody>
                    {ALL_CLASS_YEARS.flatMap(cy =>
                      ALL_TERM_HALVES.map(th => {
                        const rulesForCell = termRules.filter(r => r.classYear === cy && r.termHalf === th);
                        return (
                          <tr
                            key={`${cy}-${th}`}
                            className="border-b hover:bg-muted/30"
                          >
                            <td className="px-2 py-1.5 font-medium whitespace-nowrap sticky left-0 bg-background z-10">
                              {CLASS_YEAR_LABELS[cy]}
                            </td>
                            <td className="px-2 py-1.5 whitespace-nowrap">{TERM_HALF_LABELS[th]}</td>
                            {ALL_RULE_TYPES.map(rt => {
                              const rule = rulesForCell.find(r => r.ruleType === rt);
                              const isGpa = GPA_RULE_TYPES.includes(rt as CurriculumTermRuleType);
                              return (
                                <td key={rt} className="px-2 py-1.5 text-right">
                                  {rule
                                    ? (isGpa ? Number(rule.value).toFixed(2) : rule.value)
                                    : <span className="text-muted-foreground italic">—</span>}
                                </td>
                              );
                            })}
                          </tr>
                        );
                      }),
                    )}
                  </tbody>
                </table>
              </div>
              <p className="text-xs text-muted-foreground italic mt-3">
                Hücreleri düzenlemek için sağ üstteki <span className="font-medium">"Kural Düzenleyici"</span> butonunu kullanın.
              </p>
            </CardContent>
            <CardFooter className="border-t pt-4 flex flex-wrap items-center gap-2">
              <Button
                size="sm" variant="outline" disabled
                title="Yakında — Enstitü öğrencileri için ayrı kriterler tanımlanır"
              >
                <Scissors className="h-4 w-4 mr-1" /> Enstitü İlişik Kesme Kriterleri
              </Button>
              <Button
                size="sm" variant="outline" disabled
                title="Yakında — Bu müfredatın tüm tanımlarını başka müfredata kopyala"
              >
                <Copy className="h-4 w-4 mr-1" /> Müfredat Kopyalama İşlemleri
              </Button>
              <Button
                size="sm" variant="outline" disabled
                title="Yakında — Sınıf+dönem grup kopyalama"
              >
                <Users className="h-4 w-4 mr-1" /> Grup Kopyalama
              </Button>
            </CardFooter>
          </Card>
        </>
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

      {/* ── Curriculum Term Rules Edit Dialog (mevcut, reuse) ── */}
      {curriculumId && (
        <CurriculumTermRulesDialog
          curriculumId={curriculumId}
          curriculumLabel={curriculumLabel}
          open={showTermRulesDialog}
          onClose={() => setShowTermRulesDialog(false)}
        />
      )}
    </div>
  );
}
