import { useState } from 'react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { Table, TableBody, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { AlertCircle, CheckCircle2 } from 'lucide-react';
import {
  useAssignSection, useFacultyPlanningSections, useFacultyPlanningTimeSlots, useUnassignSection,
} from '../../hooks/useFacultyPlanning';
import type { AssignmentResult } from '../../types/facultyPlanning';
import type { SchedulingConflict } from '../../types/scheduling';
import { SectionRow } from './SectionRow';
import { STATUS_CONFIG } from './sectionUtils';

interface SectionActionFeedback {
  kind: 'success' | 'error';
  title: string;
  message: string;
  warnings?: string[];
  conflicts?: SchedulingConflict[];
}

function getApiErrorMessage(error: unknown, fallback: string) {
  return (error as { response?: { data?: { error?: string; message?: string; title?: string } } })?.response?.data?.error
    ?? (error as { response?: { data?: { error?: string; message?: string; title?: string } } })?.response?.data?.message
    ?? (error as { response?: { data?: { error?: string; message?: string; title?: string } } })?.response?.data?.title
    ?? fallback;
}

export function SectionListTab({
  facultyId, termId, departments,
}: {
  facultyId: number;
  termId: number | null;
  departments: { departmentId: number; departmentCode: string; departmentName: string }[];
}) {
  const [filterDept, setFilterDept] = useState<number | undefined>();
  const [filterStatus, setFilterStatus] = useState<string | undefined>();
  const [page, setPage] = useState(1);
  const [expandedId, setExpandedId] = useState<number | null>(null);
  const [actionFeedback, setActionFeedback] = useState<SectionActionFeedback | null>(null);

  const sectionsQuery = useFacultyPlanningSections(facultyId, termId, filterDept, filterStatus, page);
  const timeSlotsQuery = useFacultyPlanningTimeSlots(termId);
  const assignMutation = useAssignSection();
  const unassignMutation = useUnassignSection();

  const handleAssign = (
    sectionId: number,
    classroomId?: number,
    slots?: { day: number; startTime: string; endTime: string }[],
    instructorId?: number,
  ) => {
    setActionFeedback(null);
    assignMutation.mutate(
      { sectionId, classroomId, scheduleSlots: slots, instructorId },
      {
        onSuccess: (result: AssignmentResult) => {
          const hasConflicts = result.conflicts.length > 0;
          const hasWarnings = result.warnings.length > 0;

          setActionFeedback({
            kind: hasConflicts ? 'error' : 'success',
            title: hasConflicts ? 'Atama kaydedildi, çakışma bulundu' : 'Atama başarılı',
            message: hasConflicts
              ? 'Şube atandı ancak çakışma analizi uyarı üretti. Detaylar aşağıda.'
              : hasWarnings
                ? 'Şube atandı. Uyarılar aşağıda listelendi.'
                : 'Şube başarıyla atandı.',
            warnings: result.warnings,
            conflicts: result.conflicts,
          });
        },
        onError: (error: unknown) => {
          setActionFeedback({
            kind: 'error',
            title: 'Atama başarısız',
            message: getApiErrorMessage(error, 'Şube atanamadı.'),
          });
        },
      },
    );
  };

  const handleUnassign = (sectionId: number) => {
    setActionFeedback(null);
    unassignMutation.mutate(sectionId, {
      onSuccess: () => {
        setActionFeedback({
          kind: 'success',
          title: 'Atama kaldırıldı',
          message: 'Şubenin derslik ve zaman ataması kaldırıldı.',
        });
      },
      onError: (error: unknown) => {
        setActionFeedback({
          kind: 'error',
          title: 'Kaldırma başarısız',
          message: getApiErrorMessage(error, 'Atama kaldırılamadı.'),
        });
      },
    });
  };

  if (!termId) return <Card><CardContent className="py-8 text-center text-muted-foreground">Aktif dönem bulunamadı.</CardContent></Card>;

  return (
    <div className="space-y-4">
      {actionFeedback && (
        <Alert variant={actionFeedback.kind === 'error' ? 'destructive' : 'default'}>
          {actionFeedback.kind === 'error' ? <AlertCircle className="h-4 w-4" /> : <CheckCircle2 className="h-4 w-4" />}
          <AlertTitle>{actionFeedback.title}</AlertTitle>
          <AlertDescription className="space-y-1">
            <p>{actionFeedback.message}</p>
            {(actionFeedback.warnings ?? []).map((warning, index) => <p key={`warning-${index}`} className="text-xs">{warning}</p>)}
            {(actionFeedback.conflicts ?? []).map((conflict, index) => <p key={`conflict-${index}`} className="text-xs">{conflict.conflictType}: {conflict.message}</p>)}
          </AlertDescription>
        </Alert>
      )}

      {timeSlotsQuery.isError && (
        <Alert variant="destructive">
          <AlertCircle className="h-4 w-4" />
          <AlertTitle>Akademik saat tablosu yüklenemedi</AlertTitle>
          <AlertDescription>Manuel atama seçenekleri şu an üretilemedi. Sayfayı yenileyip tekrar deneyin.</AlertDescription>
        </Alert>
      )}

      {/* Filters */}
      <div className="flex flex-wrap gap-3">
        <select
          className="h-9 rounded-md border border-input bg-background px-3 text-sm"
          value={filterDept ?? ''}
          onChange={e => { setFilterDept(e.target.value ? Number(e.target.value) : undefined); setPage(1); }}
        >
          <option value="">Tüm Bölümler</option>
          {departments.map(d => <option key={d.departmentId} value={d.departmentId}>{d.departmentCode}</option>)}
        </select>
        <select
          className="h-9 rounded-md border border-input bg-background px-3 text-sm"
          value={filterStatus ?? ''}
          onChange={e => { setFilterStatus(e.target.value || undefined); setPage(1); }}
        >
          <option value="">Tüm Durumlar</option>
          {Object.entries(STATUS_CONFIG).map(([k, v]) => <option key={k} value={k}>{v.label}</option>)}
        </select>
      </div>

      {/* Table */}
      {sectionsQuery.isLoading ? <Skeleton className="h-64" /> : (
        <Card>
          <CardContent className="pt-4">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Ders</TableHead>
                  <TableHead>Bölüm</TableHead>
                  <TableHead>Kapasite</TableHead>
                  <TableHead>Derslik</TableHead>
                  <TableHead>Saat</TableHead>
                  <TableHead>Hoca</TableHead>
                  <TableHead>Durum</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {(sectionsQuery.data?.items ?? []).map(s => (
                  <SectionRow
                    key={s.sectionId}
                    section={s}
                    termId={termId}
                    timeSlots={timeSlotsQuery.data ?? []}
                    timeSlotsLoaded={timeSlotsQuery.isSuccess}
                    expanded={expandedId === s.sectionId}
                    onToggle={() => setExpandedId(prev => prev === s.sectionId ? null : s.sectionId)}
                    onAssign={(classroomId, slots, instructorId) => handleAssign(s.sectionId, classroomId, slots, instructorId)}
                    onUnassign={() => handleUnassign(s.sectionId)}
                    isAssigning={assignMutation.isPending}
                    isUnassigning={unassignMutation.isPending}
                  />
                ))}
              </TableBody>
            </Table>

            {/* Pagination */}
            {sectionsQuery.data && sectionsQuery.data.totalCount > 50 && (
              <div className="flex justify-center gap-2 mt-4">
                <Button size="sm" variant="outline" disabled={page <= 1} onClick={() => setPage(p => p - 1)}>Önceki</Button>
                <span className="text-sm py-1">Sayfa {page} / {Math.ceil(sectionsQuery.data.totalCount / 50)}</span>
                <Button size="sm" variant="outline" disabled={page * 50 >= sectionsQuery.data.totalCount} onClick={() => setPage(p => p + 1)}>Sonraki</Button>
              </div>
            )}
          </CardContent>
        </Card>
      )}
    </div>
  );
}
