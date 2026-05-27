import { useMemo, useState } from 'react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import {
  Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle, DialogDescription,
} from '@/components/ui/dialog';
import {
  Collapsible, CollapsibleContent, CollapsibleTrigger,
} from '@/components/ui/collapsible';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table';
import {
  AlertCircle, BarChart3, Building2, Calendar, CheckCircle2, ChevronDown, ChevronRight,
  Plus, Search, Sparkles, Trash2, X, ArrowRight,
} from 'lucide-react';
import { useAdminTerms, type AdminTermDto } from '../hooks/useAdminTerms';
import { useFacultyLookup } from '../hooks/useAdminLookup';
import { useClassrooms } from '../hooks/useClassrooms';
import {
  useClassroomAssignments,
  useDeleteClassroomAssignment,
  useBulkAssignClassrooms,
  useSuggestClassroomAssignments,
} from '../hooks/useClassroomAssignment';
import type {
  ClassroomTermAssignmentDto,
  ClassroomAssignmentSuggestionDto,
} from '../api/classroomAssignment.api';
import {
  CLASSROOM_TYPE_CODES,
  getClassroomTypeLabel,
  getClassroomTypeBadgeClass,
  getClassroomTypeBarClass,
} from '../lib/classroomTypeUtils';
import ClassroomSuggestionDialog from '../components/classroom/ClassroomSuggestionDialog';
import ClassroomAssignmentStatsDialog from '../components/classroom/ClassroomAssignmentStatsDialog';

// ── UI tuning constants ─────────────────────────────────────────────────────
// Toplu atama onay modalında derslik chip'i olarak en fazla kaç tane gösterilir
// (üstündeki "+N daha" rozetiyle özetlenir).
const BULK_PREVIEW_CHIP_LIMIT = 50;
// Ana çalışma kartının yüksekliği — viewport'un büyük kısmını kaplar,
// sadece sayfa header'ı + üst padding kadar eksilir.
// 100dvh = dynamic viewport: mobil/tablet adres çubuğu davranışına duyarlı.
// Min değer küçük telefon ekranlarında okunabilirliği korur.
const WORKBENCH_HEIGHT = 'calc(100dvh - 120px)';
const WORKBENCH_MIN_HEIGHT = '480px';

// ── Faculty palette (deterministic hash-based) ─────────────────────────────

const FACULTY_PALETTE = [
  { ring: 'border-blue-200/70',    bg: 'from-blue-50/70 to-white',     iconBg: 'bg-blue-100',     iconColor: 'text-blue-700',     dot: 'bg-blue-500',    accent: 'text-blue-700' },
  { ring: 'border-emerald-200/70', bg: 'from-emerald-50/70 to-white',  iconBg: 'bg-emerald-100',  iconColor: 'text-emerald-700',  dot: 'bg-emerald-500', accent: 'text-emerald-700' },
  { ring: 'border-amber-200/70',   bg: 'from-amber-50/70 to-white',    iconBg: 'bg-amber-100',    iconColor: 'text-amber-700',    dot: 'bg-amber-500',   accent: 'text-amber-700' },
  { ring: 'border-violet-200/70',  bg: 'from-violet-50/70 to-white',   iconBg: 'bg-violet-100',   iconColor: 'text-violet-700',   dot: 'bg-violet-500',  accent: 'text-violet-700' },
  { ring: 'border-rose-200/70',    bg: 'from-rose-50/70 to-white',     iconBg: 'bg-rose-100',     iconColor: 'text-rose-700',     dot: 'bg-rose-500',    accent: 'text-rose-700' },
  { ring: 'border-cyan-200/70',    bg: 'from-cyan-50/70 to-white',     iconBg: 'bg-cyan-100',     iconColor: 'text-cyan-700',     dot: 'bg-cyan-500',    accent: 'text-cyan-700' },
  { ring: 'border-teal-200/70',    bg: 'from-teal-50/70 to-white',     iconBg: 'bg-teal-100',     iconColor: 'text-teal-700',     dot: 'bg-teal-500',    accent: 'text-teal-700' },
  { ring: 'border-fuchsia-200/70', bg: 'from-fuchsia-50/70 to-white',  iconBg: 'bg-fuchsia-100',  iconColor: 'text-fuchsia-700',  dot: 'bg-fuchsia-500', accent: 'text-fuchsia-700' },
];

function paletteFor(id: number) {
  return FACULTY_PALETTE[Math.abs(id) % FACULTY_PALETTE.length];
}

// ── Page ───────────────────────────────────────────────────────────────────

export default function ClassroomAssignmentPage() {
  const { data: terms = [], isLoading: termsLoading } = useAdminTerms();
  const { data: faculties = [] } = useFacultyLookup();
  const { data: classrooms = [] } = useClassrooms();

  // Term selection: default to active term
  const [selectedTermId, setSelectedTermId] = useState<number | null>(null);
  const activeTerm = terms.find(t => t.isActive);
  const termId = selectedTermId ?? activeTerm?.id ?? null;
  const selectedTerm = terms.find(t => t.id === termId);

  const { data: assignments = [], isLoading: assignmentsLoading, isError } = useClassroomAssignments(termId);

  const deleteMutation = useDeleteClassroomAssignment();
  const bulkMutation = useBulkAssignClassrooms();
  const suggestMutation = useSuggestClassroomAssignments();

  // Bulk assignment UI state
  const [selectedClassroomIds, setSelectedClassroomIds] = useState<Set<number>>(new Set());
  const [bulkFacultyId, setBulkFacultyId] = useState<number>(0);
  const [bulkError, setBulkError] = useState('');
  const [bulkConfirmOpen, setBulkConfirmOpen] = useState(false);

  // Filters for unassigned panel
  const [unassignedSearch, setUnassignedSearch] = useState('');
  const [unassignedTypeFilter, setUnassignedTypeFilter] = useState<string>('all');

  // Delete confirm
  const [deleteTarget, setDeleteTarget] = useState<ClassroomTermAssignmentDto | null>(null);

  // Suggestion data
  const [suggestionData, setSuggestionData] = useState<ClassroomAssignmentSuggestionDto | null>(null);
  const [suggestDialogOpen, setSuggestDialogOpen] = useState(false);
  // Öneri uygulamada backend'den dönen kısmi hatalar (200 OK + errors[]).
  const [suggestApplyErrors, setSuggestApplyErrors] = useState<string[]>([]);

  // Stats dialog
  const [statsDialogOpen, setStatsDialogOpen] = useState(false);

  // Collapsible faculty groups (default all open)
  const [collapsedFaculties, setCollapsedFaculties] = useState<Set<number>>(new Set());

  // ── Derived data ────────────────────────────────────────────────────────

  const assignedClassroomIds = useMemo(
    () => new Set(assignments.map(a => a.classroomId)),
    [assignments],
  );

  const unassignedClassrooms = useMemo(
    () => classrooms.filter(c => !assignedClassroomIds.has(c.id)),
    [classrooms, assignedClassroomIds],
  );

  const unassignedTypeCounts = useMemo(() => {
    return unassignedClassrooms.reduce((acc, c) => {
      acc[c.type] = (acc[c.type] || 0) + 1;
      return acc;
    }, {} as Record<string, number>);
  }, [unassignedClassrooms]);

  // DB'den gelen tiplerden türetilir; backend enum'a yeni tip eklendiğinde
  // veya bilinmeyen bir tip kaydedildiğinde otomatik chip listesine düşer.
  const unassignedSortedTypes = useMemo(() => {
    const known = (CLASSROOM_TYPE_CODES as readonly string[]).filter(t => (unassignedTypeCounts[t] || 0) > 0);
    const knownSet = new Set<string>(CLASSROOM_TYPE_CODES);
    const extras = Object.keys(unassignedTypeCounts)
      .filter(t => !knownSet.has(t))
      .sort((a, b) => getClassroomTypeLabel(a).localeCompare(getClassroomTypeLabel(b), 'tr'));
    return [...known, ...extras];
  }, [unassignedTypeCounts]);

  const filteredUnassigned = useMemo(() => {
    const q = unassignedSearch.trim().toLowerCase();
    return unassignedClassrooms.filter(c => {
      if (unassignedTypeFilter !== 'all' && c.type !== unassignedTypeFilter) return false;
      if (q) {
        const hay = `${c.name} ${c.buildingName ?? ''}`.toLowerCase();
        if (!hay.includes(q)) return false;
      }
      return true;
    });
  }, [unassignedClassrooms, unassignedSearch, unassignedTypeFilter]);

  const groupedByFaculty = useMemo(() => {
    const map = new Map<number, { facultyName: string; items: ClassroomTermAssignmentDto[] }>();
    for (const a of assignments) {
      if (!map.has(a.facultyId)) {
        map.set(a.facultyId, { facultyName: a.facultyName, items: [] });
      }
      map.get(a.facultyId)!.items.push(a);
    }
    return Array.from(map.entries()).sort((a, b) => a[1].facultyName.localeCompare(b[1].facultyName, 'tr'));
  }, [assignments]);


  // ── Handlers ────────────────────────────────────────────────────────────

  const toggleFaculty = (id: number) => {
    setCollapsedFaculties(prev => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id); else next.add(id);
      return next;
    });
  };

  const toggleClassroom = (id: number) => {
    setSelectedClassroomIds(prev => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id); else next.add(id);
      return next;
    });
  };

  const selectAllFiltered = () => {
    setSelectedClassroomIds(new Set(filteredUnassigned.map(c => c.id)));
  };

  const clearSelection = () => {
    setSelectedClassroomIds(new Set());
  };

  const handleDelete = (a: ClassroomTermAssignmentDto) => {
    setDeleteTarget(a);
  };

  const confirmDelete = () => {
    if (!deleteTarget) return;
    deleteMutation.mutate(deleteTarget.id, {
      onSuccess: () => setDeleteTarget(null),
    });
  };

  const handleSuggest = () => {
    if (!termId) return;
    suggestMutation.mutate(termId, {
      onSuccess: (data) => {
        setSuggestionData(data);
        setSuggestApplyErrors([]);
        setSuggestDialogOpen(true);
      },
    });
  };

  const handleApplyAllSuggestions = (selectedClassroomIds?: number[]) => {
    if (!suggestionData || !termId) return;
    // Eğer seçim verilmişse sadece o id'leri uygula, yoksa tüm önerileri.
    const selectedSet = selectedClassroomIds ? new Set(selectedClassroomIds) : null;
    const itemsToApply = selectedSet
      ? suggestionData.suggestions.filter(s => selectedSet.has(s.classroomId))
      : suggestionData.suggestions;

    if (itemsToApply.length === 0) return;

    bulkMutation.mutate(
      {
        termId,
        assignments: itemsToApply.map(s => ({
          classroomId: s.classroomId,
          facultyId: s.facultyId,
        })),
      },
      {
        onSuccess: (result) => {
          // Backend hepsini reddetse bile HTTP 200 döner; errors[] sessizce yutulmamalı.
          if (result.errors.length > 0) {
            setSuggestApplyErrors(result.errors);
            return; // dialog açık kalsın ki kullanıcı kısmi/total hatayı görsün
          }
          setSuggestApplyErrors([]);
          setSuggestDialogOpen(false);
          setSuggestionData(null);
        },
      },
    );
  };

  const handleBulkAssign = () => {
    setBulkError('');
    if (!termId) return;
    if (selectedClassroomIds.size === 0) {
      setBulkError('En az bir derslik seçmelisiniz.');
      return;
    }
    if (!bulkFacultyId) {
      setBulkError('Lütfen bir fakülte seçin.');
      return;
    }
    bulkMutation.mutate(
      {
        termId,
        assignments: Array.from(selectedClassroomIds).map(classroomId => ({
          classroomId,
          facultyId: bulkFacultyId,
        })),
      },
      {
        onSuccess: (result) => {
          // Başarıyla atananları seçimden çıkar (kısmi başarı durumunda).
          const createdIds = new Set(result.created.map(c => c.classroomId));
          setSelectedClassroomIds(prev => {
            const next = new Set(prev);
            createdIds.forEach(id => next.delete(id));
            return next;
          });
          // Backend HTTP 200 + errors[] dönebilir — sessizce yutma, kullanıcıya göster.
          if (result.errors.length > 0) {
            setBulkError(
              `${result.created.length} derslik atandı, ${result.errors.length} başarısız: ${result.errors.join(' ')}`,
            );
            return; // dialog açık kalsın
          }
          setBulkFacultyId(0);
          setBulkConfirmOpen(false);
        },
        onError: (err: unknown) => {
          const msg = (err as { response?: { data?: { error?: string } } })?.response?.data?.error;
          setBulkError(msg ?? 'Toplu atama oluşturulamadı.');
        },
      },
    );
  };

  // ── Render ──────────────────────────────────────────────────────────────

  if (termsLoading) return <Skeleton className="h-96 rounded-xl" />;

  const allFilteredSelected = filteredUnassigned.length > 0
    && filteredUnassigned.every(c => selectedClassroomIds.has(c.id));
  const someFilteredSelected = filteredUnassigned.some(c => selectedClassroomIds.has(c.id));
  const targetFaculty = faculties.find(f => f.id === bulkFacultyId);

  return (
    <div className="space-y-4">
      {/* ── Tek bütün kart: header + iki panel + footer ────────────────── */}
      <div
        className="flex flex-col overflow-hidden rounded-xl border border-slate-200 bg-white shadow-sm"
        style={{ height: WORKBENCH_HEIGHT, minHeight: WORKBENCH_MIN_HEIGHT }}
      >
        {/* Üst: Dönem seçicisi + butonlar (sabit, scroll etmez) */}
        <div className="flex shrink-0 flex-wrap items-center justify-between gap-3 border-b border-slate-200 bg-gradient-to-r from-white via-slate-50/30 to-white px-4 py-3">
          <div className="flex items-center gap-3">
            <span className="flex h-9 w-9 items-center justify-center rounded-lg bg-gradient-to-br from-blue-100 to-blue-50 text-blue-700 shadow-sm">
              <Calendar className="h-4 w-4" />
            </span>
            <div>
              <div className="text-[11px] font-semibold uppercase tracking-wide text-slate-500">Dönem</div>
              <select
                className="mt-0.5 h-7 rounded-md border-0 bg-transparent pl-0 pr-6 text-sm font-semibold text-slate-900 focus:outline-none focus:ring-0"
                value={termId ?? ''}
                onChange={e => setSelectedTermId(Number(e.target.value) || null)}
              >
                {terms.map((t: AdminTermDto) => (
                  <option key={t.id} value={t.id}>
                    {t.name} {t.isActive ? '· Aktif' : ''}
                  </option>
                ))}
              </select>
            </div>
            {selectedTerm?.isActive && (
              <Badge className="border-emerald-200 bg-emerald-50 text-emerald-700">Aktif Dönem</Badge>
            )}
          </div>

          <div className="flex flex-wrap gap-2">
            <Button
              variant="outline"
              onClick={() => setStatsDialogOpen(true)}
              disabled={!termId}
              className="gap-1.5 border-slate-200 hover:border-violet-300 hover:bg-violet-50/60 hover:text-violet-700"
            >
              <BarChart3 className="h-4 w-4" />
              İstatistikler
            </Button>
            <Button
              variant="outline"
              onClick={handleSuggest}
              disabled={!termId || suggestMutation.isPending}
              className="gap-1.5 border-amber-200 bg-gradient-to-br from-amber-50 to-white text-amber-800 hover:border-amber-300 hover:from-amber-100 hover:to-amber-50"
            >
              <Sparkles className="h-4 w-4" />
              {suggestMutation.isPending ? 'Hesaplanıyor...' : 'Akıllı Öneri'}
            </Button>
          </div>
        </div>

        {/* Error state */}
        {isError && (
          <div className="shrink-0 px-4 py-3">
            <Alert variant="destructive">
              <AlertCircle className="h-4 w-4" />
              <AlertDescription>Atamalar yüklenemedi.</AlertDescription>
            </Alert>
          </div>
        )}

        {/* Loading */}
        {assignmentsLoading && (
          <div className="flex-1 p-4">
            <Skeleton className="h-full rounded-xl" />
          </div>
        )}

        {/* No term selected */}
        {!termId && (
          <div className="flex flex-1 flex-col items-center justify-center text-center">
            <Calendar className="mb-3 h-10 w-10 text-slate-300" />
            <p className="text-sm text-slate-600">Lütfen bir dönem seçin.</p>
          </div>
        )}

        {/* Main two-panel layout — flex-1 ile dış kartın kalan yüksekliğini doldurur */}
        {termId && !assignmentsLoading && !isError && (
          <div className="grid flex-1 grid-cols-1 overflow-hidden lg:grid-cols-2 lg:divide-x lg:divide-slate-200">
            {/* ── LEFT: Unassigned classrooms (multi-select) ──────────── */}
            <div className="flex flex-col overflow-hidden border-b border-slate-200 lg:border-b-0">
            <div className="flex shrink-0 items-center justify-between gap-2 border-b border-slate-200 bg-gradient-to-r from-rose-50/70 via-rose-50/30 to-transparent px-4 py-3">
              <div className="flex items-center gap-2">
                <span className="flex h-7 w-7 items-center justify-center rounded-lg bg-rose-100 text-rose-700">
                  <Building2 className="h-3.5 w-3.5" />
                </span>
                <div>
                  <h3 className="text-sm font-semibold text-slate-900">Atanmamış Derslikler</h3>
                  <p className="text-[11px] text-slate-500">
                    {selectedClassroomIds.size > 0
                      ? `${selectedClassroomIds.size} seçili · ${filteredUnassigned.length} görüntülenen`
                      : `${filteredUnassigned.length} / ${unassignedClassrooms.length} derslik`}
                  </p>
                </div>
              </div>
              {selectedClassroomIds.size > 0 && (
                <Button variant="ghost" size="sm" onClick={clearSelection} className="h-7 gap-1 text-xs">
                  <X className="h-3 w-3" /> Seçimi Temizle
                </Button>
              )}
            </div>

            {/* Filters */}
            <div className="shrink-0 space-y-2 border-b border-slate-100 px-4 py-2.5">
              <div className="relative">
                <Search className="pointer-events-none absolute left-2.5 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-slate-400" />
                <Input
                  placeholder="Derslik adı veya bina ara..."
                  value={unassignedSearch}
                  onChange={e => setUnassignedSearch(e.target.value)}
                  className="h-8 pl-8 pr-8 text-xs"
                  disabled={unassignedClassrooms.length === 0}
                />
                {unassignedSearch && (
                  <button
                    type="button"
                    onClick={() => setUnassignedSearch('')}
                    className="absolute right-2 top-1/2 -translate-y-1/2 rounded-full p-0.5 text-slate-400 hover:bg-slate-100 hover:text-slate-700"
                    aria-label="Aramayı temizle"
                  >
                    <X className="h-3.5 w-3.5" />
                  </button>
                )}
              </div>
              <div className="flex flex-wrap items-center gap-1.5">
                <button
                  type="button"
                  onClick={() => setUnassignedTypeFilter('all')}
                  className={`rounded-full border px-2.5 py-0.5 text-[11px] font-medium transition-colors ${
                    unassignedTypeFilter === 'all'
                      ? 'border-slate-900 bg-slate-900 text-white'
                      : 'border-slate-200 bg-white text-slate-600 hover:bg-slate-50'
                  }`}
                >
                  Tümü ({unassignedClassrooms.length})
                </button>
                {unassignedSortedTypes.map(t => {
                  const active = unassignedTypeFilter === t;
                  return (
                    <button
                      key={t}
                      type="button"
                      onClick={() => setUnassignedTypeFilter(t)}
                      className={`inline-flex items-center gap-1 rounded-full border px-2.5 py-0.5 text-[11px] font-medium transition-colors ${
                        active
                          ? 'border-slate-900 bg-slate-900 text-white'
                          : 'border-slate-200 bg-white text-slate-600 hover:bg-slate-50'
                      }`}
                    >
                      <span className={`h-1.5 w-1.5 rounded-full ${getClassroomTypeBarClass(t)}`} />
                      {getClassroomTypeLabel(t)} ({unassignedTypeCounts[t]})
                    </button>
                  );
                })}
              </div>
            </div>

            {/* Select-all bar */}
            {filteredUnassigned.length > 0 && (
              <div className="flex shrink-0 items-center gap-2 border-b border-slate-100 bg-slate-50/40 px-4 py-2">
                <input
                  id="select-all-unassigned"
                  type="checkbox"
                  className="h-4 w-4 cursor-pointer rounded border-slate-300 text-[#003366] focus:ring-1 focus:ring-[#003366]/40"
                  checked={allFilteredSelected}
                  ref={el => {
                    if (el) el.indeterminate = !allFilteredSelected && someFilteredSelected;
                  }}
                  onChange={() => {
                    if (allFilteredSelected) clearSelection(); else selectAllFiltered();
                  }}
                />
                <label htmlFor="select-all-unassigned" className="cursor-pointer text-xs font-medium text-slate-700">
                  {allFilteredSelected
                    ? 'Tüm seçimi kaldır'
                    : someFilteredSelected
                      ? `${selectedClassroomIds.size} seçili — tümünü seç`
                      : 'Tümünü Seç'}
                </label>
              </div>
            )}

            {/* List — kalan alanı doldurur, içeride scroll */}
            <div className="flex flex-1 flex-col overflow-y-auto">
              {unassignedClassrooms.length === 0 ? (
                <div className="flex flex-1 flex-col items-center justify-center px-6 text-center">
                  <CheckCircle2 className="mb-3 h-10 w-10 text-emerald-300" />
                  <p className="text-sm font-medium text-slate-700">Tüm derslikler atanmış 🎉</p>
                  <p className="mt-1 text-xs text-slate-500">Bu dönemde atama bekleyen derslik kalmadı.</p>
                </div>
              ) : filteredUnassigned.length === 0 ? (
                <div className="flex flex-1 items-center justify-center px-6 text-center text-sm text-slate-500">
                  Filtrelere uyan derslik bulunamadı.
                </div>
              ) : (
                <ul className="divide-y divide-slate-100">
                  {filteredUnassigned.map(c => {
                    const checked = selectedClassroomIds.has(c.id);
                    const barClass = getClassroomTypeBarClass(c.type);
                    return (
                      <li key={c.id}>
                        <label
                          className={`flex cursor-pointer items-center gap-3 px-4 py-2.5 transition-colors duration-150 ${
                            checked ? 'bg-blue-50/60 hover:bg-blue-50' : 'hover:bg-slate-50'
                          }`}
                        >
                          <input
                            type="checkbox"
                            className="h-4 w-4 cursor-pointer rounded border-slate-300 text-[#003366] focus:ring-1 focus:ring-[#003366]/40"
                            checked={checked}
                            onChange={() => toggleClassroom(c.id)}
                          />
                          <span className={`h-2 w-2 shrink-0 rounded-full ${barClass}`} />
                          <div className="min-w-0 flex-1">
                            <div className="truncate text-sm font-medium text-slate-900">{c.name}</div>
                            <div className="truncate text-[11px] text-slate-500">
                              {c.buildingName || '—'} · <span className="font-mono tabular-nums">{c.capacity}</span> kişi
                            </div>
                          </div>
                          <span className={`shrink-0 rounded-full border px-2 py-0.5 text-[10px] ${getClassroomTypeBadgeClass(c.type)}`}>
                            {getClassroomTypeLabel(c.type)}
                          </span>
                        </label>
                      </li>
                    );
                  })}
                </ul>
              )}
            </div>

            {/* Bulk action footer */}
            {unassignedClassrooms.length > 0 && (
              <div className="shrink-0 space-y-2 border-t border-slate-200 bg-gradient-to-br from-slate-50 to-white px-4 py-3">
                <div className="grid grid-cols-1 gap-2 sm:grid-cols-[1fr_auto]">
                  <select
                    className="h-9 w-full rounded-md border border-slate-200 bg-white px-3 text-sm shadow-sm focus:border-[#003366] focus:outline-none focus:ring-1 focus:ring-[#003366]/30 disabled:bg-slate-50 disabled:text-slate-400"
                    value={bulkFacultyId}
                    onChange={e => setBulkFacultyId(Number(e.target.value))}
                    disabled={selectedClassroomIds.size === 0}
                  >
                    <option value={0}>Fakülte seç...</option>
                    {faculties.map(f => (
                      <option key={f.id} value={f.id}>{f.name}</option>
                    ))}
                  </select>
                  <Button
                    onClick={() => { setBulkError(''); setBulkConfirmOpen(true); }}
                    disabled={selectedClassroomIds.size === 0 || !bulkFacultyId || bulkMutation.isPending}
                    className="h-9 gap-1.5 bg-gradient-to-br from-[#003366] to-[#004080] text-white shadow-sm hover:from-[#002a52] hover:to-[#003366] hover:shadow-md"
                  >
                    <ArrowRight className="h-4 w-4" />
                    {selectedClassroomIds.size > 0
                      ? `${selectedClassroomIds.size} Dersliği Ata`
                      : 'Toplu Ata'}
                  </Button>
                </div>
                <p className="text-[11px] text-slate-500">
                  Derslikleri seçin, fakülte belirtin ve tek seferde atayın.
                </p>
              </div>
            )}
          </div>

          {/* ── RIGHT: Assigned (grouped by faculty) ────────────────── */}
          <div className="flex flex-col overflow-hidden">
            <div className="flex shrink-0 items-center justify-between gap-2 border-b border-slate-200 bg-gradient-to-r from-emerald-50/70 via-emerald-50/30 to-transparent px-4 py-3">
              <div className="flex items-center gap-2">
                <span className="flex h-7 w-7 items-center justify-center rounded-lg bg-emerald-100 text-emerald-700">
                  <CheckCircle2 className="h-3.5 w-3.5" />
                </span>
                <div>
                  <h3 className="text-sm font-semibold text-slate-900">Atanmış Derslikler</h3>
                  <p className="text-[11px] text-slate-500">
                    {groupedByFaculty.length} fakülte · {assignments.length} derslik
                  </p>
                </div>
              </div>
            </div>

            <div className="flex-1 overflow-y-auto p-3">
              {groupedByFaculty.length === 0 ? (
                <Card className="border-dashed">
                  <CardContent className="py-10 text-center">
                    <Sparkles className="mx-auto mb-3 h-10 w-10 text-amber-300" />
                    <p className="text-sm font-medium text-slate-700">Henüz atama yapılmamış</p>
                    <p className="mt-1 text-xs text-slate-500">
                      Soldan derslikleri seçip atayın veya "Akıllı Öneri" ile başlayın.
                    </p>
                  </CardContent>
                </Card>
              ) : (
                <div className="space-y-2.5">
                  {groupedByFaculty.map(([facultyId, group]) => {
                    const palette = paletteFor(facultyId);
                    const isCollapsed = collapsedFaculties.has(facultyId);
                    return (
                      <Collapsible
                        key={facultyId}
                        open={!isCollapsed}
                        onOpenChange={() => toggleFaculty(facultyId)}
                      >
                        <div className={`overflow-hidden rounded-xl border ${palette.ring} bg-gradient-to-br ${palette.bg} transition-all duration-200 hover:shadow-md`}>
                          <CollapsibleTrigger asChild>
                            <button
                              type="button"
                              className="flex w-full items-center justify-between gap-3 px-4 py-3 text-left transition-colors hover:bg-white/40"
                            >
                              <div className="flex items-center gap-3">
                                <span className={`flex h-8 w-8 items-center justify-center rounded-lg ${palette.iconBg} ${palette.iconColor} shadow-sm`}>
                                  <Building2 className="h-4 w-4" />
                                </span>
                                <div>
                                  <div className="text-sm font-semibold text-slate-900">{group.facultyName}</div>
                                  <div className={`text-[11px] font-medium ${palette.accent}`}>
                                    {group.items.length} derslik atanmış
                                  </div>
                                </div>
                              </div>
                              <div className="flex items-center gap-1.5 text-slate-400">
                                <Badge variant="outline" className="border-white/70 bg-white/60 font-mono text-xs tabular-nums backdrop-blur-sm">
                                  {group.items.length}
                                </Badge>
                                {isCollapsed ? <ChevronRight className="h-4 w-4" /> : <ChevronDown className="h-4 w-4" />}
                              </div>
                            </button>
                          </CollapsibleTrigger>
                          <CollapsibleContent>
                            <div className="border-t border-white/60 bg-white/70 backdrop-blur-sm">
                              <Table>
                                <TableHeader>
                                  <TableRow className="border-slate-200/60 hover:bg-transparent">
                                    <TableHead className="text-xs">Derslik</TableHead>
                                    <TableHead className="text-xs">Kapasite</TableHead>
                                    <TableHead className="text-xs">Tip</TableHead>
                                    <TableHead className="w-12 text-xs" />
                                  </TableRow>
                                </TableHeader>
                                <TableBody>
                                  {group.items
                                    .slice()
                                    .sort((x, y) => x.classroomName.localeCompare(y.classroomName, 'tr'))
                                    .map(a => {
                                      const cr = classrooms.find(c => c.id === a.classroomId);
                                      return (
                                        <TableRow key={a.id} className="border-slate-100 transition-colors hover:bg-slate-50/70">
                                          <TableCell className="font-medium text-slate-900">{a.classroomName}</TableCell>
                                          <TableCell className="font-mono text-xs tabular-nums">
                                            {cr?.capacity ?? '—'}
                                          </TableCell>
                                          <TableCell>
                                            {cr && (
                                              <span className={`inline-flex items-center gap-1 rounded-full border px-2 py-0.5 text-[10px] ${getClassroomTypeBadgeClass(cr.type)}`}>
                                                <span className={`h-1.5 w-1.5 rounded-full ${getClassroomTypeBarClass(cr.type)}`} />
                                                {getClassroomTypeLabel(cr.type)}
                                              </span>
                                            )}
                                          </TableCell>
                                          <TableCell>
                                            <Button
                                              size="icon"
                                              variant="ghost"
                                              className="h-7 w-7 text-slate-400 transition-colors hover:bg-red-50 hover:text-red-600"
                                              onClick={() => handleDelete(a)}
                                              disabled={deleteMutation.isPending}
                                              aria-label="Atamayı kaldır"
                                            >
                                              <Trash2 className="h-3.5 w-3.5" />
                                            </Button>
                                          </TableCell>
                                        </TableRow>
                                      );
                                    })}
                                </TableBody>
                              </Table>
                            </div>
                          </CollapsibleContent>
                        </div>
                      </Collapsible>
                    );
                  })}
                </div>
              )}
            </div>
          </div>
        </div>
        )}
      </div>

      {/* ── Bulk Confirm Dialog ──────────────────────────────────────────── */}
      <Dialog open={bulkConfirmOpen} onOpenChange={setBulkConfirmOpen}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle className="flex items-center gap-2">
              <span className="flex h-7 w-7 items-center justify-center rounded-lg bg-blue-100 text-blue-700">
                <Plus className="h-4 w-4" />
              </span>
              Toplu Derslik Ataması
            </DialogTitle>
            <DialogDescription>
              Seçili derslikleri tek seferde belirtilen fakülteye atayın.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-3 py-2">
            <div className="rounded-lg border border-slate-200 bg-slate-50/60 p-3 text-sm">
              <div className="flex items-center justify-between">
                <span className="text-slate-600">Seçili derslik</span>
                <span className="font-mono font-semibold text-slate-900">{selectedClassroomIds.size}</span>
              </div>
              <div className="mt-1.5 flex items-center justify-between">
                <span className="text-slate-600">Hedef fakülte</span>
                <span className="font-semibold text-slate-900">{targetFaculty?.name ?? '—'}</span>
              </div>
            </div>
            {selectedClassroomIds.size > 0 && (
              <div className="max-h-32 overflow-y-auto rounded-lg border border-slate-100 bg-white p-2">
                <div className="flex flex-wrap gap-1">
                  {Array.from(selectedClassroomIds).slice(0, BULK_PREVIEW_CHIP_LIMIT).map(id => {
                    const cr = classrooms.find(c => c.id === id);
                    if (!cr) return null;
                    return (
                      <span
                        key={id}
                        className="inline-flex items-center gap-1 rounded-full bg-slate-100 px-2 py-0.5 text-[11px] text-slate-700"
                      >
                        <span className={`h-1.5 w-1.5 rounded-full ${getClassroomTypeBarClass(cr.type)}`} />
                        {cr.name}
                      </span>
                    );
                  })}
                  {selectedClassroomIds.size > BULK_PREVIEW_CHIP_LIMIT && (
                    <span className="text-[11px] text-slate-500">
                      +{selectedClassroomIds.size - BULK_PREVIEW_CHIP_LIMIT} daha
                    </span>
                  )}
                </div>
              </div>
            )}
            {bulkError && (
              <Alert variant="destructive">
                <AlertCircle className="h-4 w-4" />
                <AlertDescription>{bulkError}</AlertDescription>
              </Alert>
            )}
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setBulkConfirmOpen(false)}>İptal</Button>
            <Button
              onClick={handleBulkAssign}
              disabled={bulkMutation.isPending}
              className="bg-gradient-to-br from-[#003366] to-[#004080] text-white hover:from-[#002a52] hover:to-[#003366]"
            >
              {bulkMutation.isPending ? 'Atanıyor...' : `${selectedClassroomIds.size} Dersliği Ata`}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* ── Delete confirmation ──────────────────────────────────────────── */}
      <Dialog open={deleteTarget !== null} onOpenChange={(open) => { if (!open) setDeleteTarget(null); }}>
        <DialogContent className="sm:max-w-sm">
          <DialogHeader>
            <DialogTitle className="flex items-center gap-2">
              <span className="flex h-7 w-7 items-center justify-center rounded-lg bg-red-100 text-red-600">
                <Trash2 className="h-4 w-4" />
              </span>
              Atamayı Kaldır
            </DialogTitle>
          </DialogHeader>
          <p className="text-sm text-slate-600">
            <strong className="text-slate-900">{deleteTarget?.classroomName}</strong> dersliğinin{' '}
            <strong className="text-slate-900">{deleteTarget?.facultyName}</strong> fakültesine atamasını kaldırmak istiyor musunuz?
          </p>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDeleteTarget(null)}>İptal</Button>
            <Button variant="destructive" onClick={confirmDelete} disabled={deleteMutation.isPending}>
              {deleteMutation.isPending ? 'Kaldırılıyor...' : 'Kaldır'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* ── Stats Dialog ─────────────────────────────────────────────────── */}
      <ClassroomAssignmentStatsDialog
        open={statsDialogOpen}
        onOpenChange={setStatsDialogOpen}
        termName={selectedTerm?.name}
        classrooms={classrooms}
        assignments={assignments}
        facultyCount={faculties.length}
      />

      {/* ── Suggestion Dialog ────────────────────────────────────────────── */}
      <ClassroomSuggestionDialog
        open={suggestDialogOpen}
        onOpenChange={(open) => {
          setSuggestDialogOpen(open);
          if (!open) setSuggestApplyErrors([]);
        }}
        data={suggestionData}
        onApplyAll={handleApplyAllSuggestions}
        applying={bulkMutation.isPending}
        applyError={bulkMutation.isError}
        applyErrors={suggestApplyErrors}
      />
    </div>
  );
}
