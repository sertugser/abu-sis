import { useMemo, useState } from 'react';
import { Button } from '@/components/ui/button';
import { Dialog, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import ResizableDialogContent from '@/components/ui/resizable-dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/components/ui/select';
import { Skeleton } from '@/components/ui/skeleton';
import { ChevronRight, Loader2, Plus, Search, Trash2 } from 'lucide-react';
import {
  useCurriculumTermRules,
  useCreateTermRule,
  useUpdateTermRule,
  useDeleteTermRule,
} from '../../hooks/useCurriculumTermRules';
import {
  ALL_CLASS_YEARS,
  ALL_RULE_TYPES,
  ALL_TERM_HALVES,
  CLASS_YEAR_LABELS,
  GPA_RULE_TYPES,
  RULE_TYPE_LABELS,
  TERM_HALF_LABELS,
} from '../../types/curriculumTermRule';
import type {
  CurriculumClassYear,
  CurriculumTermHalf,
  CurriculumTermRuleType,
} from '../../types/curriculumTermRule';

interface Props {
  curriculumId: number;
  curriculumLabel: string;
  open: boolean;
  onClose: () => void;
}

export function CurriculumTermRulesDialog({ curriculumId, curriculumLabel, open, onClose }: Props) {
  const [search, setSearch] = useState('');
  const [classYear, setClassYear] = useState<CurriculumClassYear>(1);
  const [termHalf, setTermHalf] = useState<CurriculumTermHalf>(0);
  const [selectedRuleType, setSelectedRuleType] = useState<CurriculumTermRuleType | null>(null);
  const [valueInput, setValueInput] = useState('');

  const { data: rules, isLoading } = useCurriculumTermRules(open ? curriculumId : null);
  const createMut = useCreateTermRule();
  const updateMut = useUpdateTermRule();
  const deleteMut = useDeleteTermRule();

  // Seçili Sınıf + Dönem için halihazırda eklenmiş kurallar
  const currentRules = useMemo(
    () => (rules ?? []).filter(r => r.classYear === classYear && r.termHalf === termHalf),
    [rules, classYear, termHalf],
  );

  // Sol panelde gösterilecek kural listesi (arama + zaten eklenmiş olanları filtrele)
  const availableRuleTypes = useMemo(() => {
    const existingTypes = new Set(currentRules.map(r => r.ruleType));
    const lower = search.toLowerCase().trim();
    return ALL_RULE_TYPES.filter(rt => {
      if (existingTypes.has(rt)) return false;
      if (!lower) return true;
      return RULE_TYPE_LABELS[rt].toLowerCase().includes(lower);
    });
  }, [search, currentRules]);

  function handleAddRule() {
    if (selectedRuleType === null || !valueInput.trim()) return;
    const numValue = Number(valueInput);
    if (isNaN(numValue) || numValue < 0) return;

    createMut.mutate(
      {
        curriculumId,
        classYear,
        termHalf,
        ruleType: selectedRuleType,
        value: numValue,
      },
      {
        onSuccess: () => {
          setSelectedRuleType(null);
          setValueInput('');
        },
      },
    );
  }

  function handleUpdateValue(ruleId: number, newValue: string) {
    const numValue = Number(newValue);
    if (isNaN(numValue) || numValue < 0) return;
    updateMut.mutate({ curriculumId, ruleId, value: numValue });
  }

  function handleDelete(ruleId: number) {
    deleteMut.mutate({ curriculumId, ruleId });
  }

  function handleClose() {
    setSelectedRuleType(null);
    setValueInput('');
    setSearch('');
    onClose();
  }

  const isGpaRule = selectedRuleType !== null && GPA_RULE_TYPES.includes(selectedRuleType);

  return (
    <Dialog open={open} onOpenChange={(o) => !o && handleClose()}>
      <ResizableDialogContent defaultWidth={1100} minWidth={800} defaultHeight={650} minHeight={500} className="flex flex-col p-0 gap-0">
        <DialogHeader className="px-6 pt-5 pb-3 shrink-0 border-b">
          <DialogTitle className="text-base">Müfredat Dönem Kuralları Düzenle</DialogTitle>
          <p className="text-sm text-muted-foreground mt-0.5">{curriculumLabel}</p>
        </DialogHeader>

        <div className="flex-1 overflow-hidden grid grid-cols-1 lg:grid-cols-2 gap-0 min-h-0">
          {/* ── Sol panel: Eklenebilecek Kurallar/Tanımlar ── */}
          <div className="flex flex-col border-r min-h-0">
            <div className="px-4 py-3 border-b bg-muted/30 space-y-2">
              <div className="flex items-center gap-2">
                <div className="relative flex-1">
                  <Search className="absolute left-2 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
                  <Input
                    value={search}
                    onChange={e => setSearch(e.target.value)}
                    placeholder="Kural Açıklama ara..."
                    className="h-9 pl-8 text-sm"
                  />
                </div>
              </div>
              <div className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
                Eklenebilecek Kurallar/Tanımlar
              </div>
            </div>
            <div className="flex-1 overflow-y-auto">
              {availableRuleTypes.length === 0 ? (
                <div className="py-8 text-center text-sm text-muted-foreground px-4">
                  {search
                    ? 'Aramayla eşleşen kural bulunamadı'
                    : 'Bu sınıf/dönem için tüm kurallar eklenmiş'}
                </div>
              ) : (
                <ul className="divide-y">
                  {availableRuleTypes.map(rt => (
                    <li key={rt}>
                      <button
                        type="button"
                        onClick={() => setSelectedRuleType(rt)}
                        className={`w-full text-left px-4 py-2.5 text-sm hover:bg-muted/50 transition-colors flex items-center justify-between ${
                          selectedRuleType === rt ? 'bg-primary/10' : ''
                        }`}
                      >
                        <span>{RULE_TYPE_LABELS[rt]}</span>
                        {selectedRuleType === rt && <ChevronRight className="h-4 w-4 text-primary" />}
                      </button>
                    </li>
                  ))}
                </ul>
              )}
            </div>
          </div>

          {/* ── Sağ panel: Sınıf+Dönem seçici + Eklenmiş Kurallar ── */}
          <div className="flex flex-col min-h-0">
            {/* Sınıf + Dönem + Ekleme formu */}
            <div className="px-4 py-3 border-b bg-muted/30 space-y-3">
              <div className="grid grid-cols-2 gap-2">
                <div className="space-y-1">
                  <Label className="text-xs">Sınıf</Label>
                  <Select value={classYear.toString()} onValueChange={v => setClassYear(Number(v) as CurriculumClassYear)}>
                    <SelectTrigger className="h-9 text-sm">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      {ALL_CLASS_YEARS.map(cy => (
                        <SelectItem key={cy} value={cy.toString()}>{CLASS_YEAR_LABELS[cy]}</SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
                <div className="space-y-1">
                  <Label className="text-xs">Dönem</Label>
                  <Select value={termHalf.toString()} onValueChange={v => setTermHalf(Number(v) as CurriculumTermHalf)}>
                    <SelectTrigger className="h-9 text-sm">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      {ALL_TERM_HALVES.map(th => (
                        <SelectItem key={th} value={th.toString()}>{TERM_HALF_LABELS[th]}</SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
              </div>

              {selectedRuleType !== null && (
                <div className="rounded-md border bg-background p-2 space-y-2">
                  <div className="text-xs text-muted-foreground">
                    Seçilen: <span className="font-medium text-foreground">{RULE_TYPE_LABELS[selectedRuleType]}</span>
                  </div>
                  <div className="flex items-center gap-2">
                    <Input
                      type="number"
                      min={0}
                      step={isGpaRule ? '0.01' : '1'}
                      value={valueInput}
                      onChange={e => setValueInput(e.target.value)}
                      placeholder={isGpaRule ? 'Ortalama (ör. 2.50)' : 'Değer'}
                      className="h-9 text-sm"
                      onKeyDown={e => {
                        if (e.key === 'Enter') handleAddRule();
                      }}
                    />
                    <Button
                      size="sm"
                      className="h-9"
                      onClick={handleAddRule}
                      disabled={!valueInput.trim() || createMut.isPending}
                    >
                      {createMut.isPending
                        ? <Loader2 className="h-4 w-4 animate-spin" />
                        : <Plus className="h-4 w-4" />}
                      <span className="ml-1">Ekle</span>
                    </Button>
                  </div>
                </div>
              )}

              <div className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
                {CLASS_YEAR_LABELS[classYear]} • {TERM_HALF_LABELS[termHalf]} — Eklenen Kurallar
              </div>
            </div>

            {/* Eklenmiş kurallar listesi */}
            <div className="flex-1 overflow-y-auto">
              {isLoading ? (
                <div className="p-4 space-y-2">
                  {[1, 2, 3].map(i => <Skeleton key={i} className="h-10 w-full" />)}
                </div>
              ) : currentRules.length === 0 ? (
                <div className="py-8 text-center text-sm text-muted-foreground px-4">
                  Bu sınıf/dönem için henüz kural eklenmemiş. Soldan kural seçip değerini girin.
                </div>
              ) : (
                <table className="w-full text-sm">
                  <tbody className="divide-y">
                    {currentRules.map(rule => {
                      const isGpa = GPA_RULE_TYPES.includes(rule.ruleType);
                      return (
                        <tr key={rule.id} className="hover:bg-muted/30">
                          <td className="px-4 py-2.5 text-sm">{RULE_TYPE_LABELS[rule.ruleType]}</td>
                          <td className="px-2 py-2.5 w-28">
                            <Input
                              type="number"
                              min={0}
                              step={isGpa ? '0.01' : '1'}
                              defaultValue={isGpa ? rule.value.toFixed(2) : rule.value.toString()}
                              className="h-8 text-sm text-right"
                              onBlur={e => {
                                const newVal = e.target.value;
                                if (Number(newVal) !== rule.value) {
                                  handleUpdateValue(rule.id, newVal);
                                }
                              }}
                            />
                          </td>
                          <td className="px-2 py-2.5 w-10">
                            <Button
                              variant="ghost"
                              size="icon"
                              className="h-8 w-8 text-destructive hover:text-destructive"
                              onClick={() => handleDelete(rule.id)}
                              disabled={deleteMut.isPending}
                              title="Kuralı sil"
                            >
                              <Trash2 className="h-4 w-4" />
                            </Button>
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              )}
            </div>
          </div>
        </div>

        <DialogFooter className="shrink-0 px-6 py-3 border-t">
          <Button variant="outline" onClick={handleClose}>Kapat</Button>
        </DialogFooter>
      </ResizableDialogContent>
    </Dialog>
  );
}
