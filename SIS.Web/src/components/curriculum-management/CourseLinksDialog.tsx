import { useMemo, useState } from 'react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Dialog, DialogHeader, DialogTitle, DialogFooter,
} from '@/components/ui/dialog';
import ResizableDialogContent from '@/components/ui/resizable-dialog';
import { Copy, Loader2, Plus, Save, Search, Trash2 } from 'lucide-react';
import {
  useAdminCourseDetail,
  useAdminCourses,
  useAddPrerequisite,
  useRemovePrerequisite,
  useAddEquivalency,
  useRemoveEquivalency,
  useUpdateCoursePrereqRules,
} from '../../hooks/useAdminCourseCatalog';

type LinkMode = 'prerequisite' | 'equivalency';

interface Props {
  mode: LinkMode;
  open: boolean;
  courseId: number | null;
  courseLabel: string;
  onClose: () => void;
}

const COPY = {
  prerequisite: {
    title: 'Ders Ön Koşul Tanımları',
    listHeader: 'Mevcut Ön Koşullar',
    emptyText: 'Bu dersin ön koşulu yok',
    pickerLabel: 'Yeni Koşul Ekle',
    pickerPlaceholder: 'Eklemek için ders kodu veya adı...',
  },
  equivalency: {
    title: 'İntibak (Eşdeğer Ders) İşlemleri',
    listHeader: 'Mevcut Eşdeğer Dersler',
    emptyText: 'Bu derse eşdeğer ders tanımlı değil',
    pickerLabel: 'Yeni Eşdeğer Ekle',
    pickerPlaceholder: 'Eklemek için ders kodu veya adı...',
  },
} as const;

export default function CourseLinksDialog({ mode, open, courseId, courseLabel, onClose }: Props) {
  const copy = COPY[mode];

  const { data: course, isLoading } = useAdminCourseDetail(open ? courseId : null);
  const addPrereqMut = useAddPrerequisite();
  const removePrereqMut = useRemovePrerequisite();
  const addEquivMut = useAddEquivalency();
  const removeEquivMut = useRemoveEquivalency();
  const updateRulesMut = useUpdateCoursePrereqRules();

  // Yeni Koşul Ekle paneli — toggle (Proliz: button açar/kapar)
  const [showPicker, setShowPicker] = useState(false);
  const [search, setSearch] = useState('');
  const { data: pickerCourses } = useAdminCourses({
    search: search.length >= 2 ? search : undefined,
  });

  // Diğer Ön Koşul Tanımları (sadece prerequisite mode)
  const [minCredits, setMinCredits] = useState<string>('');
  const [minEcts, setMinEcts] = useState<string>('');
  const [requiresNoFailedLower, setRequiresNoFailedLower] = useState(false);
  const [canBeStandalone, setCanBeStandalone] = useState(false);

  // Course detail yüklenince form alanlarını hydrate et — adjust-state-during-render
  // (React 19+: useEffect içinde setState yasaklandı, prev-tracking ile render'da update)
  const [prevHydrationKey, setPrevHydrationKey] = useState<string | null>(null);
  const hydrationKey = course && mode === 'prerequisite' ? `${course.id}-${mode}` : null;
  if (hydrationKey !== prevHydrationKey) {
    setPrevHydrationKey(hydrationKey);
    if (course && mode === 'prerequisite') {
      setMinCredits(course.minCreditsRequired?.toString() ?? '');
      setMinEcts(course.minEctsRequired?.toString() ?? '');
      setRequiresNoFailedLower(course.requiresNoFailedLowerCourses);
      setCanBeStandalone(course.canBeTakenStandalone);
    }
  }

  function handleClose() {
    setSearch('');
    setShowPicker(false);
    onClose();
  }

  const links = useMemo(() => {
    if (!course) return [];
    return mode === 'prerequisite' ? course.prerequisites : course.equivalencies;
  }, [course, mode]);

  const linkedIds = useMemo(() => new Set(links.map(l => l.courseId)), [links]);

  const filteredPicker = useMemo(() => {
    if (!pickerCourses || !courseId) return [];
    return pickerCourses
      .filter(c => c.id !== courseId && !linkedIds.has(c.id))
      .slice(0, 20);
  }, [pickerCourses, courseId, linkedIds]);

  const isAdding = addPrereqMut.isPending || addEquivMut.isPending;
  const isRemoving = removePrereqMut.isPending || removeEquivMut.isPending;
  const isSavingRules = updateRulesMut.isPending;

  // Form dirty check — orijinal değerle karşılaştır
  const isRulesDirty = useMemo(() => {
    if (!course || mode !== 'prerequisite') return false;
    const origMinCredits = course.minCreditsRequired?.toString() ?? '';
    const origMinEcts = course.minEctsRequired?.toString() ?? '';
    return (
      minCredits.trim() !== origMinCredits ||
      minEcts.trim() !== origMinEcts ||
      requiresNoFailedLower !== course.requiresNoFailedLowerCourses ||
      canBeStandalone !== course.canBeTakenStandalone
    );
  }, [course, mode, minCredits, minEcts, requiresNoFailedLower, canBeStandalone]);

  function handleAdd(targetId: number) {
    if (!courseId) return;
    if (mode === 'prerequisite') {
      addPrereqMut.mutate({ courseId, requiredCourseId: targetId }, {
        onSuccess: () => { setSearch(''); setShowPicker(false); },
      });
    } else {
      addEquivMut.mutate({ courseId, equivalentCourseId: targetId }, {
        onSuccess: () => { setSearch(''); setShowPicker(false); },
      });
    }
  }

  function handleRemove(targetId: number) {
    if (!courseId) return;
    if (mode === 'prerequisite') {
      removePrereqMut.mutate({ courseId, requiredCourseId: targetId });
    } else {
      removeEquivMut.mutate({ courseId, equivalentCourseId: targetId });
    }
  }

  function handleSaveRules() {
    if (!courseId) return;
    const parsedCredits = minCredits.trim() === '' ? null : Number(minCredits);
    const parsedEcts = minEcts.trim() === '' ? null : Number(minEcts);
    if (parsedCredits !== null && (Number.isNaN(parsedCredits) || parsedCredits < 0)) return;
    if (parsedEcts !== null && (Number.isNaN(parsedEcts) || parsedEcts < 0)) return;

    updateRulesMut.mutate({
      courseId,
      dto: {
        minCreditsRequired: parsedCredits,
        minEctsRequired: parsedEcts,
        requiresNoFailedLowerCourses: requiresNoFailedLower,
        canBeTakenStandalone: canBeStandalone,
      },
    });
  }

  // Course header bilgisi (Proliz "Seçilen Ders: ENG 102 Academic English-II Kredi:2 AKTS:2")
  const headerInfo = course
    ? `Seçilen Ders: ${course.code} ${course.name}  Kredi:${course.nationalCredit ?? '-'}  AKTS:${course.ects}`
    : courseLabel;

  return (
    <Dialog open={open} onOpenChange={v => { if (!v) handleClose(); }}>
      <ResizableDialogContent
        defaultWidth={720}
        minWidth={520}
        minHeight={420}
        className="flex flex-col p-0 gap-0"
      >
        <DialogHeader className="px-6 pt-6 pb-3 shrink-0">
          <DialogTitle>{copy.title}</DialogTitle>
        </DialogHeader>

        <div className="flex-1 overflow-y-auto px-6 pb-2 space-y-5">
          {/* ── Header bar: Seçilen ders bilgisi + Yeni Koşul Ekle ── */}
          <div className="flex flex-wrap items-center justify-between gap-2 pb-2 border-b">
            <p className="text-sm">
              {isLoading ? (
                <span className="text-muted-foreground inline-flex items-center gap-2">
                  <Loader2 className="h-4 w-4 animate-spin" /> Yükleniyor...
                </span>
              ) : (
                headerInfo
              )}
            </p>
            <Button
              size="sm"
              variant="outline"
              className="h-8 text-xs"
              onClick={() => setShowPicker(v => !v)}
              disabled={!course || isAdding}
            >
              <Plus className="h-3.5 w-3.5 mr-1" />
              {copy.pickerLabel}
            </Button>
          </div>

          {/* ── Yeni Koşul Ekle paneli (toggle ile açılır) ── */}
          {showPicker && (
            <section className="rounded border bg-muted/20 p-3">
              <div className="relative mb-2">
                <Search className="absolute left-2.5 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
                <Input
                  placeholder={copy.pickerPlaceholder}
                  className="pl-8 h-9 text-sm"
                  value={search}
                  onChange={e => setSearch(e.target.value)}
                  disabled={isAdding}
                  autoFocus
                />
              </div>

              {search.length < 2 ? (
                <p className="text-xs text-muted-foreground italic">
                  Aramaya başlamak için en az 2 karakter girin
                </p>
              ) : filteredPicker.length === 0 ? (
                <p className="text-xs text-muted-foreground italic">
                  Eşleşen ders bulunamadı
                </p>
              ) : (
                <ul className="space-y-1 max-h-56 overflow-y-auto bg-background border rounded">
                  {filteredPicker.map(c => (
                    <li
                      key={c.id}
                      className="flex items-center justify-between gap-2 px-3 py-1.5 text-sm hover:bg-muted/40"
                    >
                      <div className="flex items-center gap-2 min-w-0">
                        <span className="font-mono text-xs shrink-0">{c.code}</span>
                        <span className="text-muted-foreground">—</span>
                        <span className="truncate">{c.name}</span>
                      </div>
                      <Button
                        size="sm"
                        variant="ghost"
                        className="h-7 px-2 text-xs shrink-0"
                        onClick={() => handleAdd(c.id)}
                        disabled={isAdding}
                      >
                        <Plus className="h-3.5 w-3.5 mr-1" />
                        Ekle
                      </Button>
                    </li>
                  ))}
                </ul>
              )}
            </section>
          )}

          {/* ── Mevcut bağlantılar ── */}
          <section>
            <h4 className="text-sm font-semibold mb-2">{copy.listHeader}</h4>
            {isLoading ? (
              <div className="flex items-center gap-2 text-sm text-muted-foreground py-2">
                <Loader2 className="h-4 w-4 animate-spin" />
                Yükleniyor...
              </div>
            ) : links.length === 0 ? (
              <p className="text-sm text-muted-foreground italic py-1">{copy.emptyText}</p>
            ) : (
              <ul className="space-y-1.5">
                {links.map(l => (
                  <li
                    key={l.courseId}
                    className="flex items-center justify-between gap-2 rounded border bg-muted/30 px-3 py-2 text-sm"
                  >
                    <div className="flex items-center gap-2 min-w-0">
                      <span className="font-mono text-xs shrink-0">{l.code}</span>
                      <span className="text-muted-foreground">—</span>
                      <span className="truncate">{l.name}</span>
                    </div>
                    <Button
                      variant="ghost"
                      size="icon"
                      className="h-7 w-7 text-destructive hover:text-destructive shrink-0"
                      title="Kaldır"
                      onClick={() => handleRemove(l.courseId)}
                      disabled={isRemoving}
                    >
                      <Trash2 className="h-3.5 w-3.5" />
                    </Button>
                  </li>
                ))}
              </ul>
            )}
          </section>

          {/* ── Diğer Ön Koşul Tanımları (Proliz) — sadece prerequisite mode ── */}
          {mode === 'prerequisite' && (
            <section className="rounded border p-4 space-y-3">
              <h4 className="text-sm font-semibold">Diğer Ön Koşul Tanımları</h4>

              {/* Kredi/AKTS Ön Koşul */}
              <div className="grid grid-cols-12 items-center gap-3">
                <Label className="col-span-3 text-xs text-muted-foreground">
                  Kredi/AKTS Ön Koşul
                </Label>
                <div className="col-span-9 flex flex-wrap gap-2">
                  <Input
                    type="number"
                    min={0}
                    placeholder="Kredi Ön Koşul"
                    className="h-9 w-40 text-sm"
                    value={minCredits}
                    onChange={e => setMinCredits(e.target.value)}
                    disabled={isLoading || isSavingRules}
                  />
                  <Input
                    type="number"
                    min={0}
                    placeholder="AKTS Ön Koşul"
                    className="h-9 w-40 text-sm"
                    value={minEcts}
                    onChange={e => setMinEcts(e.target.value)}
                    disabled={isLoading || isSavingRules}
                  />
                </div>
              </div>

              {/* Alttan Ders Durum */}
              <div className="grid grid-cols-12 items-center gap-3">
                <Label className="col-span-3 text-xs text-muted-foreground">
                  Alttan Ders Durum
                </Label>
                <label className="col-span-9 flex items-center gap-2 text-sm cursor-pointer select-none">
                  <input
                    type="checkbox"
                    className="h-4 w-4 rounded border-input accent-primary cursor-pointer"
                    checked={requiresNoFailedLower}
                    onChange={e => setRequiresNoFailedLower(e.target.checked)}
                    disabled={isLoading || isSavingRules}
                  />
                  <span>Alttan Dersi Olmayanlar Alabilir</span>
                </label>
              </div>

              {/* Tekil Alma */}
              <div className="grid grid-cols-12 items-center gap-3">
                <Label className="col-span-3 text-xs text-muted-foreground">
                  Tekil Alma
                </Label>
                <label className="col-span-9 flex items-center gap-2 text-sm cursor-pointer select-none">
                  <input
                    type="checkbox"
                    className="h-4 w-4 rounded border-input accent-primary cursor-pointer"
                    checked={canBeStandalone}
                    onChange={e => setCanBeStandalone(e.target.checked)}
                    disabled={isLoading || isSavingRules}
                  />
                  <span>Bu Ders Tek Başına Alınabilir</span>
                </label>
              </div>
            </section>
          )}
        </div>

        <DialogFooter className="shrink-0 px-6 py-4 border-t bg-background gap-2 sm:justify-between">
          <Button variant="outline" onClick={handleClose}>
            Kapat
          </Button>
          {mode === 'prerequisite' && (
            <div className="flex gap-2">
              <Button variant="outline" disabled title="Yakında">
                <Copy className="h-4 w-4 mr-1" /> Kopyala
              </Button>
              <Button
                onClick={handleSaveRules}
                disabled={!isRulesDirty || isSavingRules || isLoading}
              >
                {isSavingRules && <Loader2 className="h-4 w-4 animate-spin mr-2" />}
                <Save className="h-4 w-4 mr-1" /> Kaydet
              </Button>
            </div>
          )}
        </DialogFooter>
      </ResizableDialogContent>
    </Dialog>
  );
}
