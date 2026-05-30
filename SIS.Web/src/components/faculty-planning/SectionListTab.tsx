import { useState } from 'react';
import { useDebounce } from '../../hooks/useDebounce';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { Table, TableBody, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { AlertCircle, CheckCircle2, ChevronLeft, ChevronRight, X } from 'lucide-react';
import {
  useAssignSection, useFacultyPlanningSections, useFacultyPlanningTimeSlots, useUnassignSection,
  useSuspendEnrollment, useResumeEnrollment,
} from '../../hooks/useFacultyPlanning';
// Excel/PDF export butonları artık sayfa seviyesinde (FacultyPlanningPage)
import type { AssignmentResult } from '../../types/facultyPlanning';
import type { SchedulingConflict } from '../../types/scheduling';
import { InlineSectionRow } from './InlineSectionRow';
// ExcelImportModal artık sayfa seviyesinde

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
  facultyId, termId, readOnly = false,
  filterDept, filterStatus, searchText, pageSize, publishedEditMode = false,
}: {
  facultyId: number;
  termId: number | null;
  readOnly?: boolean;
  filterDept?: number;
  filterStatus?: string;
  searchText: string;
  pageSize: number;
  /** Faz 3b: onaylı programda section-bazı durdur/bitir aksiyonlarını aktifleştirir. */
  publishedEditMode?: boolean;
}) {
  const [page, setPage] = useState(1);
  const [feedback, setFeedback] = useState<Feedback | null>(null);
  const debouncedSearch = useDebounce(searchText, 400);
  const sectionsQuery = useFacultyPlanningSections(facultyId, termId, filterDept, filterStatus, page, pageSize, debouncedSearch || undefined);
  const timeSlotsQuery = useFacultyPlanningTimeSlots(termId);
  const assignMutation = useAssignSection();
  const unassignMutation = useUnassignSection();
  const suspendMutation = useSuspendEnrollment();
  const resumeMutation = useResumeEnrollment();

  const handleSuspend = (sectionId: number) => {
    setFeedback(null);
    suspendMutation.mutate(sectionId, {
      onSuccess: () => setFeedback({ kind: 'success', message: 'Şube düzenleme için durduruldu (yeni kayda kapalı, mevcut kayıtlılar korunuyor).' }),
      onError: (error: unknown) => setFeedback({ kind: 'error', message: extractError(error, 'Durdurulamadı.') }),
    });
  };

  const handleResume = (sectionId: number) => {
    setFeedback(null);
    resumeMutation.mutate(sectionId, {
      onSuccess: (result) => setFeedback({
        kind: 'success',
        message: `Şube yeniden kayda açıldı${result?.notifiedStudents ? ` — ${result.notifiedStudents} öğrenci bilgilendirildi` : ''}.`,
      }),
      onError: (error: unknown) => setFeedback({ kind: 'error', message: extractError(error, 'Açılamadı.') }),
    });
  };
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
    <div className="flex flex-col gap-3">
      {/* ── Feedback ── */}
      {feedback && (
        <div className={`flex items-start gap-2.5 rounded-lg border px-4 py-3 shrink-0 ${
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

      {/* ── Tablo + Sayfalama ── */}
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
                    publishedEditMode={publishedEditMode}
                    onSuspend={() => handleSuspend(s.sectionId)}
                    onResume={() => handleResume(s.sectionId)}
                    isSuspending={suspendMutation.isPending}
                    isResuming={resumeMutation.isPending}
                  />
                ))
              )}
            </TableBody>
          </Table>

          {/* Sayfalama — tablonun alt kenarı */}
          <div className="flex items-center justify-between border-t border-slate-100 bg-slate-50/60 px-4 py-2">
            <span className="text-xs text-slate-500 tabular-nums">
              {totalCount > 0 ? `${from}–${to} / ${totalCount} kayıt` : '0 sonuç'}
            </span>
            <div className="flex items-center gap-1.5">
              <Button size="icon" variant="outline" className="h-7 w-7 border-slate-200" disabled={page <= 1} onClick={() => setPage(p => p - 1)}>
                <ChevronLeft className="h-3.5 w-3.5" />
              </Button>
              <span className="text-xs text-slate-500 tabular-nums min-w-[3rem] text-center">
                {page} / {totalPages}
              </span>
              <Button size="icon" variant="outline" className="h-7 w-7 border-slate-200" disabled={page >= totalPages} onClick={() => setPage(p => p + 1)}>
                <ChevronRight className="h-3.5 w-3.5" />
              </Button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
