import { useState } from 'react';
import { useDebounce } from '../../hooks/useDebounce';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { Table, TableBody, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Input } from '@/components/ui/input';
import { AlertCircle, CheckCircle2, ChevronLeft, ChevronRight, Search, X } from 'lucide-react';
import {
  useAssignSection, useFacultyPlanningSections, useFacultyPlanningTimeSlots, useUnassignSection,
} from '../../hooks/useFacultyPlanning';
// Excel/PDF export butonları artık sayfa seviyesinde (FacultyPlanningPage)
import type { AssignmentResult } from '../../types/facultyPlanning';
import type { SchedulingConflict } from '../../types/scheduling';
import { InlineSectionRow } from './InlineSectionRow';
import { STATUS_CONFIG } from './sectionUtils';
// ExcelImportModal artık sayfa seviyesinde

const PAGE_SIZES = [10, 25, 50, 100];

interface Feedback {
  kind: 'success' | 'error';
  message: string;
  details?: string[];
}

function extractError(error: unknown, fallback: string) {
  const data = (error as { response?: { data?: { error?: string; message?: string; title?: string } } })?.response?.data;
  return data?.error ?? data?.message ?? data?.title ?? fallback;
}

export function SectionListTab({
  facultyId, termId, departments, readOnly = false,
}: {
  facultyId: number;
  termId: number | null;
  departments: { departmentId: number; departmentCode: string; departmentName: string }[];
  readOnly?: boolean;
}) {
  const [filterDept, setFilterDept] = useState<number | undefined>();
  const [filterStatus, setFilterStatus] = useState<string | undefined>();
  const [searchText, setSearchText] = useState('');
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(25);
  const [feedback, setFeedback] = useState<Feedback | null>(null);
  const debouncedSearch = useDebounce(searchText, 400);
  const sectionsQuery = useFacultyPlanningSections(facultyId, termId, filterDept, filterStatus, page, pageSize, debouncedSearch || undefined);
  const timeSlotsQuery = useFacultyPlanningTimeSlots(termId);
  const assignMutation = useAssignSection();
  const unassignMutation = useUnassignSection();
  // Export butonları sayfa seviyesinde

  const totalCount = sectionsQuery.data?.totalCount ?? 0;
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));
  const items = sectionsQuery.data?.items ?? [];
  const from = totalCount > 0 ? (page - 1) * pageSize + 1 : 0;
  const to = Math.min(page * pageSize, totalCount);

  const handleAssign = (
    sectionId: number, classroomId?: number,
    slots?: { day: number; startTime: string; endTime: string }[], instructorId?: number,
  ) => {
    setFeedback(null);
    assignMutation.mutate(
      { sectionId, classroomId, scheduleSlots: slots, instructorId },
      {
        onSuccess: (result: AssignmentResult) => {
          const hasConflicts = result.conflicts.length > 0;
          setFeedback({
            kind: hasConflicts ? 'error' : 'success',
            message: hasConflicts ? `Atandı — ${result.conflicts.length} çakışma var` : 'Şube başarıyla atandı.',
            details: [
              ...result.warnings,
              ...result.conflicts.map((c: SchedulingConflict) => `${c.conflictType}: ${c.message}`),
            ].filter(Boolean),
          });
        },
        onError: (error: unknown) => setFeedback({ kind: 'error', message: extractError(error, 'Atama başarısız.') }),
      },
    );
  };

  const handleUnassign = (sectionId: number) => {
    setFeedback(null);
    unassignMutation.mutate(sectionId, {
      onSuccess: () => setFeedback({ kind: 'success', message: 'Atama kaldırıldı.' }),
      onError: (error: unknown) => setFeedback({ kind: 'error', message: extractError(error, 'Kaldırılamadı.') }),
    });
  };

  if (!termId) {
    return <p className="text-center text-slate-500 py-12">Aktif dönem bulunamadı.</p>;
  }

  return (
    <div className="space-y-4">
      {/* ── Feedback ── */}
      {feedback && (
        <div className={`flex items-start gap-2.5 rounded-lg border px-4 py-3 ${
          feedback.kind === 'error'
            ? 'bg-red-50 border-red-200 text-red-800'
            : 'bg-emerald-50 border-emerald-200 text-emerald-800'
        }`}>
          {feedback.kind === 'error' ? <AlertCircle className="h-4 w-4 mt-0.5 shrink-0" /> : <CheckCircle2 className="h-4 w-4 mt-0.5 shrink-0" />}
          <div className="flex-1">
            <p className="text-sm font-medium">{feedback.message}</p>
            {feedback.details?.map((d, i) => <p key={i} className="text-xs mt-0.5 opacity-80">{d}</p>)}
          </div>
          <button className="p-0.5 rounded hover:bg-black/5" onClick={() => setFeedback(null)}>
            <X className="h-3.5 w-3.5" />
          </button>
        </div>
      )}

      {/* ── Filtre + Sayfalama — tek satır ── */}
      <div className="flex items-center gap-3 flex-wrap">
        {/* Sol: Filtreler */}
        <div className="relative">
          <Search className="absolute left-2.5 top-1/2 -translate-y-1/2 h-4 w-4 text-slate-400" />
          <Input
            placeholder="Ders kodu veya adı..."
            value={searchText}
            onChange={e => { setSearchText(e.target.value); setPage(1); }}
            className="pl-9 h-9 w-52 text-sm border-slate-200"
          />
        </div>
        <select
          className="h-9 rounded-md border border-slate-200 bg-white px-3 text-sm text-slate-700"
          value={filterDept ?? ''}
          onChange={e => { setFilterDept(e.target.value ? Number(e.target.value) : undefined); setPage(1); }}
        >
          <option value="">Tüm Bölümler</option>
          {departments.map(d => <option key={d.departmentId} value={d.departmentId}>{d.departmentCode}</option>)}
        </select>
        <select
          className="h-9 rounded-md border border-slate-200 bg-white px-3 text-sm text-slate-700"
          value={filterStatus ?? ''}
          onChange={e => { setFilterStatus(e.target.value || undefined); setPage(1); }}
        >
          <option value="">Tüm Durumlar</option>
          {Object.entries(STATUS_CONFIG).map(([k, v]) => <option key={k} value={k}>{v.label}</option>)}
        </select>
        <select
          className="h-9 rounded-md border border-slate-200 bg-white px-3 text-sm text-slate-700"
          value={pageSize}
          onChange={e => { setPageSize(Number(e.target.value)); setPage(1); }}
        >
          {PAGE_SIZES.map(n => <option key={n} value={n}>{n} satır</option>)}
        </select>
        {(filterDept !== undefined || filterStatus !== undefined || searchText) && (
          <button
            className="text-xs text-muted-foreground hover:text-foreground underline underline-offset-2"
            onClick={() => {
              setFilterDept(undefined);
              setFilterStatus(undefined);
              setSearchText('');
              setPage(1);
            }}
          >
            Temizle
          </button>
        )}

        {/* Sağ: Sayfalama */}
        <div className="flex items-center gap-2 ml-auto">
          <span className="text-sm text-slate-500 tabular-nums">
            {totalCount > 0 ? `${from}–${to} / ${totalCount}` : '0 sonuç'}
          </span>
          <Button size="icon" variant="outline" className="h-8 w-8 border-slate-200" disabled={page <= 1} onClick={() => setPage(p => p - 1)}>
            <ChevronLeft className="h-4 w-4" />
          </Button>
          <Button size="icon" variant="outline" className="h-8 w-8 border-slate-200" disabled={page >= totalPages} onClick={() => setPage(p => p + 1)}>
            <ChevronRight className="h-4 w-4" />
          </Button>
        </div>
      </div>

      {/* ── Tablo ── */}
      {sectionsQuery.isLoading ? (
        <div className="space-y-px">
          {Array.from({ length: pageSize > 15 ? 15 : pageSize }).map((_, i) => <Skeleton key={i} className="h-[52px]" />)}
        </div>
      ) : (
        <div className="border border-slate-200 rounded-lg overflow-hidden bg-white">
          <Table>
            <TableHeader>
              <TableRow className="border-b-2 border-slate-100">
                <TableHead className="text-xs font-semibold text-slate-500 py-3 pl-4">DERS</TableHead>
                <TableHead className="text-xs font-semibold text-slate-500 py-3 w-[170px]">DERSLİK</TableHead>
                <TableHead className="text-xs font-semibold text-slate-500 py-3 w-[185px]">GÜN + SAAT</TableHead>
                <TableHead className="text-xs font-semibold text-slate-500 py-3 w-[170px]">HOCA</TableHead>
                <TableHead className="text-xs font-semibold text-slate-500 py-3 w-[100px]">DURUM</TableHead>
                <TableHead className="py-3 w-10" />
              </TableRow>
            </TableHeader>
            <TableBody>
              {items.length === 0 ? (
                <TableRow>
                  <td colSpan={6} className="py-20 text-center text-slate-400">
                    Eşleşen şube bulunamadı.
                  </td>
                </TableRow>
              ) : (
                items.map((s, idx) => (
                  <InlineSectionRow
                    key={s.sectionId}
                    section={s}
                    termId={termId}
                    timeSlots={timeSlotsQuery.data ?? []}
                    timeSlotsLoaded={timeSlotsQuery.isSuccess}
                    onAssign={(cId, slots, iId) => handleAssign(s.sectionId, cId, slots, iId)}
                    onUnassign={() => handleUnassign(s.sectionId)}
                    isAssigning={assignMutation.isPending}
                    isUnassigning={unassignMutation.isPending}
                    readOnly={readOnly}
                    even={idx % 2 === 0}
                  />
                ))
              )}
            </TableBody>
          </Table>
        </div>
      )}
    </div>
  );
}
