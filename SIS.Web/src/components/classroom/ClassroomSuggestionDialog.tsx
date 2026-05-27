import { useEffect, useMemo } from 'react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  Dialog, DialogFooter, DialogHeader, DialogTitle, DialogDescription,
} from '@/components/ui/dialog';
import ResizableDialogContent from '@/components/ui/resizable-dialog';
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/components/ui/select';
import { Progress } from '@/components/ui/progress';
import {
  Collapsible, CollapsibleContent, CollapsibleTrigger,
} from '@/components/ui/collapsible';
import {
  Tooltip, TooltipContent, TooltipProvider, TooltipTrigger,
} from '@/components/ui/tooltip';
import {
  AlertCircle, Building2, CheckCircle2, ChevronDown, FlaskConical, GraduationCap,
  HelpCircle, Info, Lightbulb, Layers, Sparkles, TrendingUp, Users,
} from 'lucide-react';
import { useState } from 'react';
import type {
  ClassroomAssignmentSuggestionDto,
} from '../../api/classroomAssignment.api';
import {
  getClassroomTypeLabel,
  getClassroomTypeBadgeClass,
  getClassroomTypeBarClass,
} from '../../lib/classroomTypeUtils';

// ── Helpers ────────────────────────────────────────────────────────────────

const PRIORITY_META: Record<string, { label: string; cls: string; rank: number }> = {
  high:   { label: 'Yüksek', cls: 'bg-red-100 text-red-700 border-red-200',       rank: 0 },
  medium: { label: 'Orta',   cls: 'bg-amber-100 text-amber-700 border-amber-200', rank: 1 },
  normal: { label: 'Normal', cls: 'bg-blue-100 text-blue-700 border-blue-200',    rank: 2 },
  low:    { label: 'Düşük',  cls: 'bg-gray-100 text-gray-600 border-gray-200',    rank: 3 },
};

function priorityMeta(p: string) {
  return PRIORITY_META[p] ?? PRIORITY_META.normal;
}

function isLab(classroomType: string) {
  return classroomType === 'Lab' || classroomType === 'Computer'
    || classroomType === 'PhysicsLab' || classroomType === 'ChemistryLab'
    || classroomType === 'BiologyLab' || classroomType === 'ElectronicsLab';
}

// ── Props ──────────────────────────────────────────────────────────────────

interface ClassroomSuggestionDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  data: ClassroomAssignmentSuggestionDto | null;
  /** Seçili öneriler için bulk uygula. selectedIds undefined → hepsini uygula. */
  onApplyAll: (selectedClassroomIds?: number[]) => void;
  applying: boolean;
  applyError: boolean;
  /** Backend'den dönen kısmi/total atama hataları (HTTP 200 + errors[]). */
  applyErrors?: string[];
}

// ── Component ──────────────────────────────────────────────────────────────

export default function ClassroomSuggestionDialog({
  open, onOpenChange, data, onApplyAll, applying, applyError, applyErrors = [],
}: ClassroomSuggestionDialogProps) {
  // Filter state
  const [facultyFilter, setFacultyFilter] = useState<string>('all');
  const [priorityFilter, setPriorityFilter] = useState<string>('all');
  const [typeFilter, setTypeFilter] = useState<string>('all');

  // Selection state — varsayılan: tüm öneriler seçili
  const [selectedIds, setSelectedIds] = useState<Set<number>>(new Set());

  // "Nasıl çalışır" panel — varsayılan kapalı
  const [howItWorksOpen, setHowItWorksOpen] = useState(false);

  // Yeni veri geldiğinde seçimi resetle (hepsi seçili başlasın)
  useEffect(() => {
    if (data) {
      setSelectedIds(new Set(data.suggestions.map(s => s.classroomId)));
      setFacultyFilter('all');
      setPriorityFilter('all');
      setTypeFilter('all');
    } else {
      setSelectedIds(new Set());
    }
  }, [data]);

  // ── Türetilmiş veriler ────────────────────────────────────────────

  const suggestions = data?.suggestions ?? [];
  const facultyLoads = data?.facultyLoads ?? [];

  // Tip bazlı özet — kaç amfi, kaç lab, kaç normal önerildi
  const typeSummary = useMemo(() => {
    const summary = { amphi: 0, lab: 0, normal: 0 };
    for (const s of suggestions) {
      // Eşik bilgisi yok burada, basit tip kontrolü yeterli
      if (s.classroomType === 'Amphitheater') summary.amphi++;
      else if (isLab(s.classroomType)) summary.lab++;
      else summary.normal++;
    }
    return summary;
  }, [suggestions]);

  // ── Fakülte başına öneri özeti ───────────────────────────────────────────
  // Her fakülteye önerilen derslik listesi: tip bazında kaç adet + isimler.
  // Bu sayede "Mühendislik'e ne kadar/neyden vericem?" sorusu kartta cevaplanır.
  const suggestionsByFaculty = useMemo(() => {
    const map = new Map<number, {
      total: number;
      amphi: { name: string; capacity: number }[];
      lab: { name: string; capacity: number }[];
      normal: { name: string; capacity: number }[];
    }>();
    for (const s of suggestions) {
      let entry = map.get(s.facultyId);
      if (!entry) {
        entry = { total: 0, amphi: [], lab: [], normal: [] };
        map.set(s.facultyId, entry);
      }
      entry.total++;
      const item = { name: s.classroomName, capacity: s.classroomCapacity };
      if (s.classroomType === 'Amphitheater') entry.amphi.push(item);
      else if (isLab(s.classroomType)) entry.lab.push(item);
      else entry.normal.push(item);
    }
    return map;
  }, [suggestions]);

  // Filtre seçenekleri
  const uniqueFaculties = useMemo(() => {
    const map = new Map<number, string>();
    for (const s of suggestions) map.set(s.facultyId, s.facultyName);
    return Array.from(map.entries())
      .map(([id, name]) => ({ id, name }))
      .sort((a, b) => a.name.localeCompare(b.name, 'tr'));
  }, [suggestions]);

  const uniqueTypes = useMemo(() => {
    const set = new Set<string>();
    suggestions.forEach(s => set.add(s.classroomType));
    return Array.from(set).sort((a, b) =>
      getClassroomTypeLabel(a).localeCompare(getClassroomTypeLabel(b), 'tr'),
    );
  }, [suggestions]);

  const uniquePriorities = useMemo(() => {
    const set = new Set<string>();
    suggestions.forEach(s => set.add(s.priority));
    return Array.from(set).sort((a, b) => priorityMeta(a).rank - priorityMeta(b).rank);
  }, [suggestions]);

  // Filtrelenmiş öneriler — sıralama: önce öncelik, sonra fakülte, sonra kapasite (desc)
  const filteredSuggestions = useMemo(() => {
    return suggestions
      .filter(s => facultyFilter === 'all' || String(s.facultyId) === facultyFilter)
      .filter(s => priorityFilter === 'all' || s.priority === priorityFilter)
      .filter(s => typeFilter === 'all' || s.classroomType === typeFilter)
      .sort((a, b) => {
        const pr = priorityMeta(a.priority).rank - priorityMeta(b.priority).rank;
        if (pr !== 0) return pr;
        const fc = a.facultyName.localeCompare(b.facultyName, 'tr');
        if (fc !== 0) return fc;
        return b.classroomCapacity - a.classroomCapacity;
      });
  }, [suggestions, facultyFilter, priorityFilter, typeFilter]);

  // Filtrelenmiş içinde seçili sayım
  const filteredSelectedCount = useMemo(
    () => filteredSuggestions.filter(s => selectedIds.has(s.classroomId)).length,
    [filteredSuggestions, selectedIds],
  );
  const allFilteredSelected = filteredSuggestions.length > 0
    && filteredSelectedCount === filteredSuggestions.length;

  // ── Handlers ──────────────────────────────────────────────────────

  const toggleId = (id: number) => {
    setSelectedIds(prev => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id); else next.add(id);
      return next;
    });
  };

  const toggleFilteredSelection = () => {
    if (allFilteredSelected) {
      // Filtrelenmiş olanları seçimden kaldır
      setSelectedIds(prev => {
        const next = new Set(prev);
        filteredSuggestions.forEach(s => next.delete(s.classroomId));
        return next;
      });
    } else {
      // Filtrelenmiş olanları seçime ekle
      setSelectedIds(prev => {
        const next = new Set(prev);
        filteredSuggestions.forEach(s => next.add(s.classroomId));
        return next;
      });
    }
  };

  const handleApply = () => {
    onApplyAll(Array.from(selectedIds));
  };

  // ── Render ────────────────────────────────────────────────────────

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <ResizableDialogContent
        defaultWidth={1180}
        defaultHeight={820}
        minWidth={780}
        minHeight={560}
        className="flex flex-col p-0 gap-0 overflow-hidden"
      >
        <DialogHeader className="shrink-0 border-b border-slate-100 bg-gradient-to-r from-amber-50/70 via-amber-50/30 to-transparent px-6 py-4 pr-28">
          <DialogTitle className="flex items-center gap-2">
            <span className="flex h-7 w-7 items-center justify-center rounded-lg bg-amber-100 text-amber-700">
              <Lightbulb className="h-4 w-4" />
            </span>
            Akıllı Derslik Atama Önerisi
            {data?.termName && (
              <span className="ml-2 text-sm font-normal text-slate-500">— {data.termName}</span>
            )}
          </DialogTitle>
          <DialogDescription>
            Müfredat analizi, öğrenci yoğunluğu, lab/amfi ihtiyacı ve geçmiş veriye dayanır.
            <span className="ml-2 text-[10px] text-slate-400">(Pencereyi sürüklemek için çift tık · kenarlardan boyutlandır)</span>
          </DialogDescription>
        </DialogHeader>

        <div className="flex-1 overflow-y-auto px-6 py-5">
          {!data ? (
            <div className="py-12 text-center text-sm text-slate-500">
              Öneri yükleniyor...
            </div>
          ) : (
            <TooltipProvider delayDuration={200}>
            <div className="space-y-6">

              {/* ── "Bu öneri nasıl üretildi?" açılır panel ──────── */}
              <Collapsible open={howItWorksOpen} onOpenChange={setHowItWorksOpen}>
                <div className="overflow-hidden rounded-xl border border-blue-200/60 bg-gradient-to-br from-blue-50/40 to-white">
                  <CollapsibleTrigger asChild>
                    <button
                      type="button"
                      className="flex w-full items-center justify-between gap-2 px-4 py-2.5 text-left transition-colors hover:bg-blue-50/30"
                    >
                      <div className="flex items-center gap-2 text-sm">
                        <Info className="h-4 w-4 text-blue-600" />
                        <span className="font-semibold text-slate-900">Bu öneri nasıl üretildi?</span>
                        <span className="text-[11px] text-slate-500">— algoritma adımlarını gör</span>
                      </div>
                      <ChevronDown className={`h-4 w-4 text-slate-500 transition-transform ${howItWorksOpen ? 'rotate-180' : ''}`} />
                    </button>
                  </CollapsibleTrigger>
                  <CollapsibleContent>
                    <div className="border-t border-blue-200/40 px-4 py-3 text-xs leading-relaxed text-slate-700">
                      <ol className="space-y-2.5">
                        <li className="flex gap-2.5">
                          <span className="flex h-5 w-5 shrink-0 items-center justify-center rounded-full bg-blue-100 text-[10px] font-bold text-blue-700">1</span>
                          <div>
                            <strong className="text-slate-900">Veri toplandı:</strong> Her fakülte için aktif öğrenci sayısı, açılacak ders sayısı, lab/amfi gerektiren ders sayısı ve geçen dönemin gerçek derslik kullanımı incelendi.
                          </div>
                        </li>
                        <li className="flex gap-2.5">
                          <span className="flex h-5 w-5 shrink-0 items-center justify-center rounded-full bg-blue-100 text-[10px] font-bold text-blue-700">2</span>
                          <div>
                            <strong className="text-slate-900">Talep puanı hesaplandı:</strong> Öğrenci sayısı, ders sayısı ve lab dersi sayısı ağırlıklarla birleştirilip her fakülteye bir <em>talep puanı</em> verildi. Tüm ağırlıklar SystemConfig'den okunur — admin değiştirebilir.
                          </div>
                        </li>
                        <li className="flex gap-2.5">
                          <span className="flex h-5 w-5 shrink-0 items-center justify-center rounded-full bg-blue-100 text-[10px] font-bold text-blue-700">3</span>
                          <div>
                            <strong className="text-slate-900">Geçmiş veriyle karıştırıldı:</strong> Geçen dönem kullanılan derslik sayısı ile bu dönemin tahmini harmanlandı (varsayılan %60 geçmiş + %40 tahmin). Anomalileri filtrelemek için tahminin 2 katı tavan uygulandı.
                          </div>
                        </li>
                        <li className="flex gap-2.5">
                          <span className="flex h-5 w-5 shrink-0 items-center justify-center rounded-full bg-blue-100 text-[10px] font-bold text-blue-700">4</span>
                          <div>
                            <strong className="text-slate-900">4-fazlı dağıtım yapıldı:</strong>
                            <span className="ml-1">
                              <span className="inline-flex items-center gap-1 rounded-full bg-rose-100 px-1.5 py-0.5 text-[10px] text-rose-700">① Amfiler</span>
                              {' → '}
                              <span className="inline-flex items-center gap-1 rounded-full bg-violet-100 px-1.5 py-0.5 text-[10px] text-violet-700">② Lablar</span>
                              {' → '}
                              <span className="inline-flex items-center gap-1 rounded-full bg-blue-100 px-1.5 py-0.5 text-[10px] text-blue-700">③ Normal Sınıflar</span>
                              {' → '}
                              <span className="inline-flex items-center gap-1 rounded-full bg-slate-100 px-1.5 py-0.5 text-[10px] text-slate-700">④ Kalan</span>
                            </span>
                            <span className="block text-[11px] text-slate-500 mt-1">
                              Kıt kaynaklar (amfi/lab) round-robin dağıtılır — hiçbir fakülte sıfırlanmaz. Sonraki fazlarda önceki atamalar dikkate alınır.
                            </span>
                          </div>
                        </li>
                        <li className="flex gap-2.5">
                          <span className="flex h-5 w-5 shrink-0 items-center justify-center rounded-full bg-blue-100 text-[10px] font-bold text-blue-700">5</span>
                          <div>
                            <strong className="text-slate-900">Her öneri için gerekçe yazıldı:</strong> Aşağıdaki öneri tablosunun "Gerekçe" sütununda her atamanın <em>neden yapıldığı</em> görülebilir.
                          </div>
                        </li>
                      </ol>
                    </div>
                  </CollapsibleContent>
                </div>
              </Collapsible>

              {/* ── Üst özet rozetleri ───────────────────────────── */}
              <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
                <KpiCard
                  icon={<Building2 className="h-4 w-4" />}
                  label="Toplam Derslik"
                  value={data.totalClassrooms}
                  tooltip="Sistemde tanımlı tüm derslik sayısı"
                  color="blue"
                />
                <KpiCard
                  icon={<CheckCircle2 className="h-4 w-4" />}
                  label="Zaten Atanmış"
                  value={data.alreadyAssigned}
                  tooltip="Bu dönemde bir fakülteye atanmış derslik sayısı"
                  color="slate"
                />
                <KpiCard
                  icon={<Sparkles className="h-4 w-4" />}
                  label="Önerilen"
                  value={data.suggestions.length}
                  tooltip="Algoritmanın atanmamış derslikler için ürettiği öneri sayısı"
                  color="amber"
                />
                <KpiCard
                  icon={<Users className="h-4 w-4" />}
                  label="Talep Eden Fakülte"
                  value={facultyLoads.filter(f => f.loadPercentage > 0).length}
                  tooltip="Bu dönem aktif ders açan, derslik talebi olan fakülte sayısı"
                  color="violet"
                />
              </div>

              {/* ── Tip bazlı öneri özeti ───────────────────────── */}
              {data.suggestions.length > 0 && (
                <div className="grid grid-cols-1 gap-2.5 sm:grid-cols-3">
                  <TypeSummaryCard
                    icon={<GraduationCap className="h-4 w-4" />}
                    label="Amfi"
                    value={typeSummary.amphi}
                    total={data.suggestions.length}
                    color="rose"
                  />
                  <TypeSummaryCard
                    icon={<FlaskConical className="h-4 w-4" />}
                    label="Laboratuvar"
                    value={typeSummary.lab}
                    total={data.suggestions.length}
                    color="violet"
                  />
                  <TypeSummaryCard
                    icon={<Building2 className="h-4 w-4" />}
                    label="Normal Sınıf"
                    value={typeSummary.normal}
                    total={data.suggestions.length}
                    color="blue"
                  />
                </div>
              )}

              {/* ── Fakülte Talep Analizi ───────────────────────── */}
              {facultyLoads.some(f => f.loadPercentage > 0) && (
                <section>
                  <SectionHeader
                    icon={<TrendingUp className="h-4 w-4 text-blue-600" />}
                    title="Fakülte Talep Analizi"
                    subtitle="Her fakültenin bu döneme ait derslik ihtiyacı"
                  />
                  <div className="grid grid-cols-1 gap-3 md:grid-cols-2">
                    {facultyLoads
                      .filter(fl => fl.loadPercentage > 0)
                      .sort((a, b) => b.loadPercentage - a.loadPercentage)
                      .map(fl => (
                        <div key={fl.facultyId} className="space-y-3 rounded-lg border border-slate-200 bg-white p-4 transition-shadow hover:shadow-sm">
                          <div className="flex items-center justify-between gap-2">
                            <span className="truncate text-sm font-semibold text-slate-900">{fl.facultyName}</span>
                            <Tooltip>
                              <TooltipTrigger asChild>
                                <Badge variant="secondary" className="shrink-0 cursor-help text-xs">
                                  %{fl.loadPercentage.toFixed(1)} talep payı
                                </Badge>
                              </TooltipTrigger>
                              <TooltipContent side="left" className="max-w-xs text-xs">
                                Tüm fakültelerin talep puanları içinde bu fakültenin payı.
                                Yüksek pay → daha çok derslik öneriliyor.
                              </TooltipContent>
                            </Tooltip>
                          </div>
                          <Progress value={fl.loadPercentage} className="h-2" />
                          <div className="grid grid-cols-3 gap-2 text-xs">
                            <MicroStat
                              color="blue"
                              value={fl.studentCount}
                              label="Aktif Öğrenci"
                              tooltip="Bu fakültenin bölümlerine kayıtlı aktif öğrenci sayısı"
                            />
                            <MicroStat
                              color="emerald"
                              value={fl.curriculumCourseCount}
                              label="Açılan Ders"
                              tooltip="Bu dönem müfredata göre açılacak benzersiz ders sayısı"
                            />
                            <MicroStat
                              color="amber"
                              value={fl.totalWeeklyHours}
                              label="Haftalık Ders Saati"
                              tooltip="Tüm açılan derslerin haftalık toplam saati (teori + lab)"
                            />
                          </div>
                          <div className="flex flex-wrap gap-1.5">
                            <Tooltip>
                              <TooltipTrigger asChild>
                                <span className="inline-block">
                                  <ChipBadge color="gray">
                                    ~{fl.estimatedSectionCount} şube açılacak
                                  </ChipBadge>
                                </span>
                              </TooltipTrigger>
                              <TooltipContent side="bottom" className="max-w-xs text-xs">
                                Öğrenci sayısı ve derslik kapasitesine göre tahmini şube (section) sayısı
                              </TooltipContent>
                            </Tooltip>
                            <Tooltip>
                              <TooltipTrigger asChild>
                                <span className="inline-block">
                                  <ChipBadge color="gray">
                                    {fl.parallelClassroomNeed} eş zamanlı derslik gerekli
                                  </ChipBadge>
                                </span>
                              </TooltipTrigger>
                              <TooltipContent side="bottom" className="max-w-xs text-xs">
                                Tüm dersleri haftalık takvime sığdırmak için aynı anda kaç derslik gerekiyor
                              </TooltipContent>
                            </Tooltip>
                            {fl.labCourseCount > 0 && (
                              <Tooltip>
                                <TooltipTrigger asChild>
                                  <span className="inline-block">
                                    <ChipBadge color="violet">
                                      {fl.labCourseCount} lab dersi → {fl.parallelLabNeed} lab gerekli
                                    </ChipBadge>
                                  </span>
                                </TooltipTrigger>
                                <TooltipContent side="bottom" className="max-w-xs text-xs">
                                  Laboratuvar gerektiren ders sayısı ve bu dersler için eş zamanlı lab ihtiyacı
                                </TooltipContent>
                              </Tooltip>
                            )}
                            {fl.amphiCourseCount > 0 && (
                              <Tooltip>
                                <TooltipTrigger asChild>
                                  <span className="inline-block">
                                    <ChipBadge color="rose">
                                      {fl.amphiCourseCount} büyük sınıf dersi → {fl.parallelAmphiNeed} amfi gerekli
                                    </ChipBadge>
                                  </span>
                                </TooltipTrigger>
                                <TooltipContent side="bottom" className="max-w-xs text-xs">
                                  100+ öğrencili (amfi gerektiren) dersler ve bu dersler için eş zamanlı amfi ihtiyacı
                                </TooltipContent>
                              </Tooltip>
                            )}
                            {fl.historicalClassroomUsage > 0 && (
                              <Tooltip>
                                <TooltipTrigger asChild>
                                  <span className="inline-block">
                                    <ChipBadge color="sky">
                                      Geçen dönem: {fl.historicalClassroomUsage} derslik kullandı
                                    </ChipBadge>
                                  </span>
                                </TooltipTrigger>
                                <TooltipContent side="bottom" className="max-w-xs text-xs">
                                  Bu fakültenin önceki dönemde gerçekten kaç farklı derslik kullandığı (tahminle harmanlanır)
                                </TooltipContent>
                              </Tooltip>
                            )}
                          </div>

                          {/* ── BU FAKÜLTEYE ÖNERİLEN — kartın asıl cevabı ── */}
                          <FacultySuggestionSummary
                            assignment={suggestionsByFaculty.get(fl.facultyId)}
                          />
                        </div>
                      ))}
                  </div>
                </section>
              )}

              {/* ── Öneriler tablosu + filtreler ─────────────── */}
              {data.suggestions.length > 0 ? (
                <section>
                  <SectionHeader
                    icon={<Layers className="h-4 w-4 text-amber-600" />}
                    title="Öneri Listesi"
                    subtitle={`${filteredSuggestions.length} / ${data.suggestions.length} öneri görüntüleniyor`}
                  />

                  {/* Filter toolbar */}
                  <div className="mb-3 flex flex-wrap items-center gap-2 rounded-xl border border-slate-200 bg-white p-2.5">
                    <Select value={facultyFilter} onValueChange={setFacultyFilter}>
                      <SelectTrigger className="h-8 w-[200px] text-xs">
                        <SelectValue placeholder="Tüm Fakülteler" />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="all">Tüm Fakülteler ({uniqueFaculties.length})</SelectItem>
                        {uniqueFaculties.map(f => (
                          <SelectItem key={f.id} value={String(f.id)}>{f.name}</SelectItem>
                        ))}
                      </SelectContent>
                    </Select>

                    <Select value={typeFilter} onValueChange={setTypeFilter}>
                      <SelectTrigger className="h-8 w-[170px] text-xs">
                        <SelectValue placeholder="Tüm Tipler" />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="all">Tüm Tipler ({uniqueTypes.length})</SelectItem>
                        {uniqueTypes.map(t => (
                          <SelectItem key={t} value={t}>
                            <span className="inline-flex items-center gap-2">
                              <span className={`h-2 w-2 rounded-full ${getClassroomTypeBarClass(t)}`} />
                              {getClassroomTypeLabel(t)}
                            </span>
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>

                    <Select value={priorityFilter} onValueChange={setPriorityFilter}>
                      <SelectTrigger className="h-8 w-[150px] text-xs">
                        <SelectValue placeholder="Tüm Öncelikler" />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="all">Tüm Öncelikler</SelectItem>
                        {uniquePriorities.map(p => (
                          <SelectItem key={p} value={p}>{priorityMeta(p).label}</SelectItem>
                        ))}
                      </SelectContent>
                    </Select>

                    <div className="ml-auto flex items-center gap-2 text-xs text-slate-600">
                      <span className="rounded-md bg-amber-50 px-2 py-1 font-medium text-amber-700">
                        {selectedIds.size} / {data.suggestions.length} seçili
                      </span>
                    </div>
                  </div>

                  {/* Tablo */}
                  <div className="overflow-hidden rounded-xl border border-slate-200 bg-white">
                    <table className="w-full text-sm">
                      <thead className="border-b border-slate-200 bg-slate-50/60">
                        <tr>
                          <th className="w-10 px-3 py-2.5">
                            <input
                              type="checkbox"
                              className="h-4 w-4 cursor-pointer rounded border-slate-300 text-amber-600 focus:ring-1 focus:ring-amber-500/40"
                              checked={allFilteredSelected}
                              ref={el => {
                                if (el) el.indeterminate = !allFilteredSelected && filteredSelectedCount > 0;
                              }}
                              onChange={toggleFilteredSelection}
                              aria-label="Görünenleri seç"
                            />
                          </th>
                          <th className="px-3 py-2.5 text-left text-xs font-semibold text-slate-600">Derslik</th>
                          <th className="px-3 py-2.5 text-center text-xs font-semibold text-slate-600">Kapasite</th>
                          <th className="px-3 py-2.5 text-left text-xs font-semibold text-slate-600">Tip</th>
                          <th className="px-3 py-2.5 text-left text-xs font-semibold text-slate-600">Fakülte</th>
                          <th className="px-3 py-2.5 text-left text-xs font-semibold text-slate-600">Öncelik</th>
                          <th className="px-3 py-2.5 text-left text-xs font-semibold text-slate-600">Gerekçe</th>
                        </tr>
                      </thead>
                      <tbody className="divide-y divide-slate-100">
                        {filteredSuggestions.length === 0 ? (
                          <tr>
                            <td colSpan={7} className="py-8 text-center text-sm text-slate-500">
                              Filtrelere uyan öneri yok.
                            </td>
                          </tr>
                        ) : (
                          filteredSuggestions.map(s => {
                            const checked = selectedIds.has(s.classroomId);
                            return (
                              <tr
                                key={s.classroomId}
                                className={`group transition-colors ${
                                  checked ? 'bg-amber-50/40 hover:bg-amber-50/60' : 'hover:bg-slate-50/60'
                                }`}
                              >
                                <td className="px-3 py-2.5">
                                  <input
                                    type="checkbox"
                                    className="h-4 w-4 cursor-pointer rounded border-slate-300 text-amber-600 focus:ring-1 focus:ring-amber-500/40"
                                    checked={checked}
                                    onChange={() => toggleId(s.classroomId)}
                                  />
                                </td>
                                <td className="px-3 py-2.5 font-medium text-slate-900">{s.classroomName}</td>
                                <td className="px-3 py-2.5 text-center font-mono text-xs tabular-nums">
                                  {s.classroomCapacity}
                                </td>
                                <td className="px-3 py-2.5">
                                  <span className={`inline-flex items-center gap-1 rounded-full border px-2 py-0.5 text-[10px] ${getClassroomTypeBadgeClass(s.classroomType)}`}>
                                    <span className={`h-1.5 w-1.5 rounded-full ${getClassroomTypeBarClass(s.classroomType)}`} />
                                    {getClassroomTypeLabel(s.classroomType)}
                                  </span>
                                </td>
                                <td className="px-3 py-2.5 text-sm text-slate-700">{s.facultyName}</td>
                                <td className="px-3 py-2.5">
                                  <span className={`inline-flex rounded-full border px-2 py-0.5 text-[11px] font-medium ${priorityMeta(s.priority).cls}`}>
                                    {priorityMeta(s.priority).label}
                                  </span>
                                </td>
                                <td className="max-w-[280px] truncate px-3 py-2.5 text-[11px] text-slate-500" title={s.reason}>
                                  {s.reason}
                                </td>
                              </tr>
                            );
                          })
                        )}
                      </tbody>
                    </table>
                  </div>
                </section>
              ) : (
                <div className="overflow-hidden rounded-xl border border-emerald-200/60 bg-gradient-to-br from-emerald-50/40 via-white to-white">
                  <div className="px-6 py-8 text-center">
                    <div className="mx-auto mb-4 flex h-16 w-16 items-center justify-center rounded-full bg-emerald-100">
                      <CheckCircle2 className="h-8 w-8 text-emerald-600" />
                    </div>
                    <h3 className="text-base font-semibold text-slate-900">
                      Bu dönem için yeni öneri yok
                    </h3>
                    <p className="mx-auto mt-1.5 max-w-md text-sm text-slate-600">
                      Sistemdeki <strong>{data.totalClassrooms}</strong> dersliğin tamamı
                      ({data.alreadyAssigned} adet) bir fakülteye atanmış durumda.
                      Algoritmanın önereceği yeni atanmamış derslik kalmadı.
                    </p>
                  </div>
                  <div className="border-t border-emerald-200/40 bg-white/60 px-6 py-3">
                    <p className="mb-2 text-[11px] font-semibold uppercase tracking-wide text-slate-600">
                      Sonraki adımlar
                    </p>
                    <ul className="space-y-1.5 text-xs text-slate-700">
                      <li className="flex gap-2">
                        <span className="text-emerald-600">→</span>
                        <span><strong>Yeni derslik eklemek istersen</strong> Derslik Yönetimi sayfasından ekleyebilirsin — sonra bu öneriyi tekrar çalıştır.</span>
                      </li>
                      <li className="flex gap-2">
                        <span className="text-emerald-600">→</span>
                        <span><strong>Mevcut atamaları değiştirmek istersen</strong> dialog'u kapat, atanmış paneldeki dersliği seçip kaldır, sonra tekrar öneri al.</span>
                      </li>
                      <li className="flex gap-2">
                        <span className="text-emerald-600">→</span>
                        <span><strong>Başka dönem için öneri istersen</strong> dialog'u kapat ve dönem seçicisinden farklı bir dönem seç.</span>
                      </li>
                    </ul>
                  </div>
                </div>
              )}

              {/* Bulk apply error (ağ/sunucu hatası) */}
              {applyError && (
                <Alert variant="destructive">
                  <AlertCircle className="h-4 w-4" />
                  <AlertDescription>Toplu atama sırasında hata oluştu.</AlertDescription>
                </Alert>
              )}

              {/* Kısmi/total atama hataları (HTTP 200 + errors[]) */}
              {applyErrors.length > 0 && (
                <Alert variant="destructive">
                  <AlertCircle className="h-4 w-4" />
                  <AlertDescription>
                    <div className="font-medium">
                      {applyErrors.length} öneri uygulanamadı:
                    </div>
                    <ul className="mt-1 max-h-32 list-disc space-y-0.5 overflow-y-auto pl-4 text-xs">
                      {applyErrors.map((e, i) => (
                        <li key={i}>{e}</li>
                      ))}
                    </ul>
                  </AlertDescription>
                </Alert>
              )}
            </div>
            </TooltipProvider>
          )}
        </div>

        <DialogFooter className="shrink-0 border-t border-slate-100 bg-slate-50/40 px-6 py-3">
          <Button variant="outline" onClick={() => onOpenChange(false)}>Kapat</Button>
          {data && data.suggestions.length > 0 && (
            <Button
              onClick={handleApply}
              disabled={applying || selectedIds.size === 0}
              className="bg-gradient-to-br from-[#003366] to-[#004080] text-white hover:from-[#002a52] hover:to-[#003366]"
            >
              {applying
                ? 'Uygulanıyor...'
                : selectedIds.size === data.suggestions.length
                  ? `Tümünü Uygula (${selectedIds.size})`
                  : `Seçili ${selectedIds.size} Öneriyi Uygula`}
            </Button>
          )}
        </DialogFooter>
      </ResizableDialogContent>
    </Dialog>
  );
}

// ── Sub-components ─────────────────────────────────────────────────────────

interface KpiCardProps {
  icon: React.ReactNode;
  label: string;
  value: number;
  tooltip?: string;
  color: 'blue' | 'amber' | 'slate' | 'violet';
}

function KpiCard({ icon, label, value, tooltip, color }: KpiCardProps) {
  const palette = {
    blue:   { ring: 'border-blue-200/60',   bg: 'from-blue-50 via-blue-50/60 to-white',     iconBg: 'bg-blue-100',   iconColor: 'text-blue-700' },
    amber:  { ring: 'border-amber-200/60',  bg: 'from-amber-50 via-amber-50/60 to-white',   iconBg: 'bg-amber-100',  iconColor: 'text-amber-700' },
    slate:  { ring: 'border-slate-200/60',  bg: 'from-slate-50 to-white',                   iconBg: 'bg-slate-100',  iconColor: 'text-slate-600' },
    violet: { ring: 'border-violet-200/60', bg: 'from-violet-50 via-violet-50/60 to-white', iconBg: 'bg-violet-100', iconColor: 'text-violet-700' },
  }[color];

  return (
    <div className={`relative overflow-hidden rounded-xl border ${palette.ring} bg-gradient-to-br ${palette.bg} px-4 py-3 transition-transform hover:-translate-y-0.5`}>
      <div className="flex items-center justify-between gap-3">
        <div className="min-w-0">
          <div className="flex items-center gap-1 text-[11px] font-semibold uppercase tracking-wide text-slate-600">
            <span>{label}</span>
            {tooltip && (
              <Tooltip>
                <TooltipTrigger asChild>
                  <span className="cursor-help">
                    <HelpCircle className="h-3 w-3 text-slate-400 hover:text-slate-600" />
                  </span>
                </TooltipTrigger>
                <TooltipContent side="bottom" className="max-w-xs text-xs">
                  {tooltip}
                </TooltipContent>
              </Tooltip>
            )}
          </div>
          <div className="mt-1 text-2xl font-bold tabular-nums text-slate-900">{value}</div>
        </div>
        <div className={`flex h-9 w-9 shrink-0 items-center justify-center rounded-lg ${palette.iconBg} ${palette.iconColor} shadow-sm`}>
          {icon}
        </div>
      </div>
      <div className="pointer-events-none absolute -right-6 -top-6 h-20 w-20 rounded-full bg-white/30 blur-2xl" />
    </div>
  );
}

interface TypeSummaryCardProps {
  icon: React.ReactNode;
  label: string;
  value: number;
  total: number;
  color: 'blue' | 'violet' | 'rose';
}

function TypeSummaryCard({ icon, label, value, total, color }: TypeSummaryCardProps) {
  const palette = {
    blue:   { ring: 'border-blue-200/60',   iconBg: 'bg-blue-100',   iconColor: 'text-blue-700',   barColor: 'bg-blue-500' },
    violet: { ring: 'border-violet-200/60', iconBg: 'bg-violet-100', iconColor: 'text-violet-700', barColor: 'bg-violet-500' },
    rose:   { ring: 'border-rose-200/60',   iconBg: 'bg-rose-100',   iconColor: 'text-rose-700',   barColor: 'bg-rose-500' },
  }[color];
  const pct = total > 0 ? Math.round((value / total) * 100) : 0;

  return (
    <div className={`rounded-lg border ${palette.ring} bg-white p-3`}>
      <div className="flex items-center justify-between gap-2">
        <div className="flex items-center gap-2">
          <span className={`flex h-7 w-7 items-center justify-center rounded-md ${palette.iconBg} ${palette.iconColor}`}>
            {icon}
          </span>
          <div>
            <div className="text-[11px] font-semibold uppercase tracking-wide text-slate-600">{label}</div>
            <div className="font-mono text-base font-bold tabular-nums text-slate-900">{value}</div>
          </div>
        </div>
        <span className="text-[11px] text-slate-500">%{pct}</span>
      </div>
      <div className="mt-2 h-1 overflow-hidden rounded-full bg-slate-100">
        <div className={`h-full ${palette.barColor} transition-all duration-500`} style={{ width: `${pct}%` }} />
      </div>
    </div>
  );
}

interface MicroStatProps {
  color: 'blue' | 'emerald' | 'amber';
  value: number;
  label: string;
  tooltip?: string;
}

function MicroStat({ color, value, label, tooltip }: MicroStatProps) {
  const palette = {
    blue:    { bg: 'bg-blue-50',    fg: 'text-blue-700',    sub: 'text-blue-600/70' },
    emerald: { bg: 'bg-emerald-50', fg: 'text-emerald-700', sub: 'text-emerald-600/70' },
    amber:   { bg: 'bg-amber-50',   fg: 'text-amber-700',   sub: 'text-amber-600/70' },
  }[color];

  const body = (
    <div className={`rounded-md ${palette.bg} p-2 text-center ${tooltip ? 'cursor-help' : ''}`}>
      <p className={`font-bold ${palette.fg}`}>{value}</p>
      <p className={`leading-tight ${palette.sub}`}>{label}</p>
    </div>
  );

  if (!tooltip) return body;
  return (
    <Tooltip>
      <TooltipTrigger asChild>{body}</TooltipTrigger>
      <TooltipContent side="bottom" className="max-w-xs text-xs">{tooltip}</TooltipContent>
    </Tooltip>
  );
}

// ── Fakülte öneri özeti — kartın ana cevabı ─────────────────────────────────

interface FacultySuggestionSummaryProps {
  assignment?: {
    total: number;
    amphi: { name: string; capacity: number }[];
    lab: { name: string; capacity: number }[];
    normal: { name: string; capacity: number }[];
  };
}

function FacultySuggestionSummary({ assignment }: FacultySuggestionSummaryProps) {
  // Hiç öneri yoksa: empty state mesajı
  if (!assignment || assignment.total === 0) {
    return (
      <div className="rounded-lg border border-dashed border-slate-200 bg-slate-50/40 px-3 py-2.5 text-center">
        <p className="text-[11px] text-slate-500">
          <span className="font-medium text-slate-600">Bu fakülteye yeni öneri yok</span>
          <span className="ml-1 text-slate-400">— mevcut atamalar yeterli görünüyor.</span>
        </p>
      </div>
    );
  }

  return (
    <div className="rounded-lg border border-emerald-200/60 bg-gradient-to-br from-emerald-50/40 to-white p-3">
      <div className="mb-2 flex items-center justify-between gap-2">
        <span className="inline-flex items-center gap-1.5 text-[11px] font-semibold uppercase tracking-wide text-emerald-700">
          <Sparkles className="h-3 w-3" />
          Bu fakülteye önerilen
        </span>
        <span className="rounded-full bg-emerald-100 px-2 py-0.5 text-[11px] font-bold text-emerald-700">
          {assignment.total} derslik
        </span>
      </div>

      {/* Tip bazlı kırılım — net rakam + isim listesi */}
      <div className="space-y-1.5">
        {assignment.amphi.length > 0 && (
          <SuggestionRow
            icon={<GraduationCap className="h-3.5 w-3.5" />}
            label="Amfi"
            count={assignment.amphi.length}
            items={assignment.amphi}
            color="rose"
          />
        )}
        {assignment.lab.length > 0 && (
          <SuggestionRow
            icon={<FlaskConical className="h-3.5 w-3.5" />}
            label="Laboratuvar"
            count={assignment.lab.length}
            items={assignment.lab}
            color="violet"
          />
        )}
        {assignment.normal.length > 0 && (
          <SuggestionRow
            icon={<Building2 className="h-3.5 w-3.5" />}
            label="Normal Sınıf"
            count={assignment.normal.length}
            items={assignment.normal}
            color="blue"
          />
        )}
      </div>
    </div>
  );
}

interface SuggestionRowProps {
  icon: React.ReactNode;
  label: string;
  count: number;
  items: { name: string; capacity: number }[];
  color: 'rose' | 'violet' | 'blue';
}

function SuggestionRow({ icon, label, count, items, color }: SuggestionRowProps) {
  const palette = {
    rose:   { iconBg: 'bg-rose-100',   iconColor: 'text-rose-700',   chip: 'bg-rose-50 text-rose-700 border-rose-200' },
    violet: { iconBg: 'bg-violet-100', iconColor: 'text-violet-700', chip: 'bg-violet-50 text-violet-700 border-violet-200' },
    blue:   { iconBg: 'bg-blue-100',   iconColor: 'text-blue-700',   chip: 'bg-blue-50 text-blue-700 border-blue-200' },
  }[color];

  // En fazla 6 isim göster, üstü "+N daha" özetiyle
  const VISIBLE_LIMIT = 6;
  const visible = items.slice(0, VISIBLE_LIMIT);
  const extra = items.length - visible.length;

  return (
    <div className="flex items-start gap-2">
      <span className={`mt-0.5 flex h-6 w-6 shrink-0 items-center justify-center rounded-md ${palette.iconBg} ${palette.iconColor}`}>
        {icon}
      </span>
      <div className="min-w-0 flex-1">
        <div className="text-xs font-medium text-slate-800">
          <span className="font-bold tabular-nums">{count}</span>
          <span className="ml-1">{label}</span>
        </div>
        <div className="mt-1 flex flex-wrap gap-1">
          {visible.map((item, idx) => (
            <span
              key={`${item.name}-${idx}`}
              className={`inline-flex items-center gap-1 rounded-full border px-1.5 py-0.5 text-[10px] ${palette.chip}`}
              title={`Kapasite: ${item.capacity} kişi`}
            >
              {item.name}
              <span className="text-slate-400">·</span>
              <span className="font-mono tabular-nums">{item.capacity}</span>
            </span>
          ))}
          {extra > 0 && (
            <span className="rounded-full bg-slate-100 px-1.5 py-0.5 text-[10px] text-slate-600">
              +{extra} daha
            </span>
          )}
        </div>
      </div>
    </div>
  );
}

interface ChipBadgeProps {
  color: 'gray' | 'violet' | 'rose' | 'sky';
  children: React.ReactNode;
}

function ChipBadge({ color, children }: ChipBadgeProps) {
  const cls = {
    gray:   'bg-gray-100 text-gray-600',
    violet: 'bg-violet-100 text-violet-700',
    rose:   'bg-rose-100 text-rose-700',
    sky:    'bg-sky-100 text-sky-700',
  }[color];
  return (
    <span className={`rounded-full px-2 py-0.5 text-[11px] ${cls}`}>
      {children}
    </span>
  );
}

interface SectionHeaderProps {
  icon: React.ReactNode;
  title: string;
  subtitle?: string;
}

function SectionHeader({ icon, title, subtitle }: SectionHeaderProps) {
  return (
    <div className="mb-3 flex items-baseline gap-2">
      <span className="inline-flex items-center gap-1.5 text-sm font-semibold text-slate-900">
        {icon}
        {title}
      </span>
      {subtitle && <span className="text-[11px] text-slate-500">— {subtitle}</span>}
    </div>
  );
}
