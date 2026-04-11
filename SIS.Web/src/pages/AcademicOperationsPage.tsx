import { useCallback, useMemo, useState, type ChangeEvent } from 'react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Progress } from '@/components/ui/progress';
import { Skeleton } from '@/components/ui/skeleton';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/tooltip';
import { cn } from '@/lib/utils';
import { getAcademicOperationsErrorMessage } from '../api/academicOperations.api';
import {
  useAcademicOperationsContext,
  useAcademicPlanningSummary,
  useApproveCourseSections,
  useBulkApproveCourseSections,
  useRejectCourseSections,
  useSectionApprovals,
} from '../hooks/useAcademicOperations';
import { useActiveTerm } from '../hooks/useActiveTerm';
import { useAdminFaculties, useAdminDepartmentsByStructure } from '../hooks/useAdminAcademicStructure';
import type {
  ApprovalWorkflowStatus,
  CourseDemandForecast,
  CourseSectionApproval,
} from '../types/academicOperations';
import type { SchedulingConflict } from '../types/scheduling';
import {
  AlertCircle,
  AlertTriangle,
  Check,
  ChevronDown,
  ChevronLeft,
  ChevronRight,
  Minus,
  Plus,
  Search,
  X,
} from 'lucide-react';

const CONFLICT_CFG: Record<string, { label: string; variant: 'destructive' | 'warning' | 'outline' }> = {
  ClassroomConflict: { label: 'Derslik', variant: 'destructive' },
  InstructorConflict: { label: 'Öğretim Üyesi', variant: 'destructive' },
  CapacityConflict: { label: 'Kapasite', variant: 'warning' },
  WeeklyLoadConflict: { label: 'Yük Aşımı', variant: 'warning' },
  InvalidScheduleJson: { label: 'Veri Hatası', variant: 'outline' },
};

const DAY_LABELS: Record<number, string> = {
  0: 'Pazar', 1: 'Pazartesi', 2: 'Salı', 3: 'Çarşamba', 4: 'Perşembe', 5: 'Cuma', 6: 'Cumartesi',
};

const ITEMS_PER_PAGE = 20;

const WORKFLOW_ORDER: Record<ApprovalWorkflowStatus, number> = {
  Pending: 0,
  PartiallyOpened: 1,
  Approved: 2,
  Completed: 3,
  Rejected: 4,
};

function getWorkflowStatus(approval: CourseSectionApproval | null): ApprovalWorkflowStatus {
  return approval?.workflowStatus ?? approval?.status ?? 'Pending';
}

function getWorkflowLabel(status: ApprovalWorkflowStatus) {
  switch (status) {
    case 'Approved':
      return 'Onaylı';
    case 'PartiallyOpened':
      return 'Kısmi Açıldı';
    case 'Completed':
      return 'Tamamlandı';
    case 'Rejected':
      return 'Red';
    default:
      return 'Bekliyor';
  }
}

function getWorkflowVariant(status: ApprovalWorkflowStatus): 'success' | 'warning' | 'destructive' | 'secondary' | 'outline' {
  switch (status) {
    case 'Completed':
      return 'success';
    case 'PartiallyOpened':
      return 'warning';
    case 'Rejected':
      return 'destructive';
    case 'Approved':
      return 'outline';
    default:
      return 'secondary';
  }
}

/* ═══════════════════════════════════════════════════════════════════════
   Main Page — only department selector + tabs, nothing else
   ═══════════════════════════════════════════════════════════════════════ */

export default function AcademicOperationsPage() {
  const [searchTerm, setSearchTerm] = useState('');
  const contextQuery = useAcademicOperationsContext();
  const { data: activeTerm } = useActiveTerm();
  const { data: faculties } = useAdminFaculties();
  const { data: structureDepartments } = useAdminDepartmentsByStructure();

  const departments = contextQuery.data?.accessibleDepartments ?? [];

  const [selectedFacultyId, setSelectedFacultyId] = useState<number | null>(null);
  const [selectedDeptId, setSelectedDeptId] = useState<number | null>(null);

  // Faculty filtering
  const accessibleDeptIds = useMemo(() => new Set(departments.map(d => d.departmentId)), [departments]);

  const availableFaculties = useMemo(() => {
    if (!faculties || !structureDepartments) return [];
    const facultyIds = new Set(
      structureDepartments
        .filter(sd => accessibleDeptIds.has(sd.id))
        .map(sd => sd.facultyId)
    );
    return faculties.filter(f => facultyIds.has(f.id));
  }, [faculties, structureDepartments, accessibleDeptIds]);

  const filteredDepartments = useMemo(() => {
    if (!selectedFacultyId || !structureDepartments) return departments;
    const deptIdsInFaculty = new Set(
      structureDepartments
        .filter(sd => sd.facultyId === selectedFacultyId)
        .map(sd => sd.id)
    );
    return departments.filter(d => deptIdsInFaculty.has(d.departmentId));
  }, [departments, structureDepartments, selectedFacultyId]);

  const currentDepartmentId = useMemo(() => {
    if (selectedDeptId && filteredDepartments.some(d => d.departmentId === selectedDeptId)) {
      return selectedDeptId;
    }
    return filteredDepartments[0]?.departmentId ?? contextQuery.data?.defaultDepartmentId ?? null;
  }, [selectedDeptId, filteredDepartments, contextQuery.data?.defaultDepartmentId]);

  const handleFacultyChange = useCallback((e: ChangeEvent<HTMLSelectElement>) => {
    const val = e.target.value ? Number(e.target.value) : null;
    setSelectedFacultyId(val);
    setSelectedDeptId(null);
  }, []);

  const handleDepartmentChange = useCallback((e: ChangeEvent<HTMLSelectElement>) => {
    setSelectedDeptId(Number(e.target.value));
  }, []);

  const planningQuery = useAcademicPlanningSummary(currentDepartmentId, activeTerm?.id, true);

  if (contextQuery.isLoading) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-10 w-72" />
        <Skeleton className="h-10 rounded-lg" />
        <Skeleton className="h-80 rounded-xl" />
      </div>
    );
  }

  if (contextQuery.isError || !contextQuery.data) {
    return (
      <Alert variant="destructive">
        <AlertCircle className="h-4 w-4" />
        <AlertTitle>Sayfa yüklenemedi</AlertTitle>
        <AlertDescription>
          {getAcademicOperationsErrorMessage(contextQuery.error, 'Yetkili kapsam bilgileri alınamadı.')}
        </AlertDescription>
      </Alert>
    );
  }

  const selectedDepartment = departments.find(d => d.departmentId === currentDepartmentId) ?? null;

  return (
    <div className="space-y-5">
      {/* Faculty + Department selector */}
      <div className="flex flex-wrap items-center gap-2 text-xs">
        {availableFaculties.length > 1 && (
          <div className="flex items-center gap-1.5">
            <label className="font-medium whitespace-nowrap">Fakülte:</label>
            <select
              className="h-7 rounded-md border border-input bg-background px-2 text-xs"
              value={selectedFacultyId ?? ''}
              onChange={handleFacultyChange}
            >
              <option value="">Tümü</option>
              {availableFaculties.map(f => (
                <option key={f.id} value={f.id}>{f.name}</option>
              ))}
            </select>
          </div>
        )}

        <div className="flex items-center gap-1.5">
          <label className="font-medium whitespace-nowrap">Bölüm:</label>
          <select
            className="h-7 rounded-md border border-input bg-background px-2 text-xs"
            value={currentDepartmentId ?? ''}
            onChange={handleDepartmentChange}
          >
            {filteredDepartments.map(d => (
              <option key={d.departmentId} value={d.departmentId}>
                {d.departmentCode} — {d.departmentName}
              </option>
            ))}
          </select>
        </div>
      </div>

      {/* Content */}
      {!selectedDepartment ? (
        <Card>
          <CardContent className="py-16 text-center text-gray-500">
            Bu rol için görüntülenebilir bölüm bulunamadı.
          </CardContent>
        </Card>
      ) : (
        <PlanningMode
          departmentId={currentDepartmentId!}
          termId={activeTerm?.id ?? null}
          departmentName={selectedDepartment.departmentName}
          isLoading={planningQuery.isLoading}
          errorMessage={planningQuery.isError ? getAcademicOperationsErrorMessage(planningQuery.error, 'Planlama verisi yüklenemedi.') : null}
          summary={planningQuery.data ?? null}
          searchTerm={searchTerm}
          onSearchTermChange={setSearchTerm}
          termStatus={activeTerm?.status}
        />
      )}
    </div>
  );
}

/* ═══════════════════════════════════════════════════════════════════════
   PLANLAMA — Action-oriented: "What needs my attention?"

   Layout:
   1. If conflicts → red alert banner at TOP (this is urgent)
   2. Approval progress bar (compact, inline)
   3. Course table sorted: pending first, then by demand desc
   4. Expanded detail: breakdown, confidence, historical
   ═══════════════════════════════════════════════════════════════════════ */

const WORKFLOW_STATUS_HELP: Record<string, string> = {
  'Pending': 'Henüz onay verilmemiş, karar bekleniyor.',
  'Approved': 'Onaylandı, ders açma işlemi bekleniyor.',
  'PartiallyOpened': 'Onaylanan şubelerin bir kısmı açıldı.',
  'Completed': 'Tüm onaylanan şubeler açıldı.',
  'Rejected': 'Reddedildi, bu dönem açılmayacak.',
};

function PlanningMode({
  departmentId, termId, departmentName, isLoading, errorMessage, summary, searchTerm, onSearchTermChange, termStatus,
}: {
  departmentId: number; termId: number | null; departmentName: string;
  isLoading: boolean; errorMessage: string | null;
  summary: ReturnType<typeof useAcademicPlanningSummary>['data'] | null;
  searchTerm: string; onSearchTermChange: (v: string) => void;
  termStatus: string | undefined;
}) {
  const effectiveTermId = termId ?? summary?.termId ?? null;
  const isPlanning = termStatus === 'Planning';
  const approvalsQuery = useSectionApprovals(departmentId, effectiveTermId, !!summary);
  const approveMut = useApproveCourseSections(departmentId, effectiveTermId ?? 0);
  const rejectMut = useRejectCourseSections(departmentId, effectiveTermId ?? 0);
  const bulkApproveMut = useBulkApproveCourseSections(departmentId, effectiveTermId ?? 0);
  const [page, setPage] = useState(0);
  const [conflictsOpen, setConflictsOpen] = useState(false);

  const approvalMap = useMemo(() => {
    const map = new Map<number, CourseSectionApproval>();
    for (const a of approvalsQuery.data ?? []) map.set(a.courseId, a);
    return map;
  }, [approvalsQuery.data]);

  const handleApprove = useCallback((courseId: number, count: number, note?: string) => {
    approveMut.mutate({ courseId, sectionCount: count, note });
  }, [approveMut]);

  const handleReject = useCallback((courseId: number, note?: string) => {
    rejectMut.mutate({ courseId, note });
  }, [rejectMut]);

  if (isLoading) {
    return <div className="space-y-4"><Skeleton className="h-12 rounded-lg" /><Skeleton className="h-96 rounded-xl" /></div>;
  }
  if (errorMessage) {
    return <Alert variant="destructive"><AlertCircle className="h-4 w-4" /><AlertTitle>Hata</AlertTitle><AlertDescription>{errorMessage}</AlertDescription></Alert>;
  }
  if (!summary) {
    return <Card><CardContent className="py-16 text-center text-gray-500">{departmentName} için veri bulunamadı.</CardContent></Card>;
  }

  const allCourses = summary.forecastSummary?.courseSummaries ?? [];
  const conflicts = summary.scheduleValidation.conflicts;

  // Filter
  const filtered = allCourses.filter(c => {
    if (!searchTerm.trim()) return true;
    return `${c.courseCode} ${c.courseName}`.toLowerCase().includes(searchTerm.toLowerCase());
  });

  // Sort: pending first, then by demand desc
  const sorted = [...filtered].sort((a, b) => {
    const aStatus = getWorkflowStatus(approvalMap.get(a.courseId) ?? null);
    const bStatus = getWorkflowStatus(approvalMap.get(b.courseId) ?? null);
    const orderDiff = WORKFLOW_ORDER[aStatus] - WORKFLOW_ORDER[bStatus];
    if (orderDiff !== 0) return orderDiff;
    return b.totalPredictedDemand - a.totalPredictedDemand;
  });

  const workflowCounts = allCourses.reduce<Record<ApprovalWorkflowStatus, number>>((acc, course) => {
    const workflowStatus = getWorkflowStatus(approvalMap.get(course.courseId) ?? null);
    acc[workflowStatus] += 1;
    return acc;
  }, {
    Pending: 0,
    Approved: 0,
    PartiallyOpened: 0,
    Completed: 0,
    Rejected: 0,
  });

  const approvedCount = workflowCounts.Approved;
  const partialCount = workflowCounts.PartiallyOpened;
  const completedCount = workflowCounts.Completed;
  const rejectedCount = workflowCounts.Rejected;
  const pendingCount = workflowCounts.Pending;
  const progressPct = allCourses.length > 0 ? ((allCourses.length - pendingCount) / allCourses.length) * 100 : 0;

  const pendingFiltered = sorted.filter(c => {
    return getWorkflowStatus(approvalMap.get(c.courseId) ?? null) === 'Pending';
  });

  const totalPages = Math.max(1, Math.ceil(sorted.length / ITEMS_PER_PAGE));
  const paginated = sorted.slice(page * ITEMS_PER_PAGE, (page + 1) * ITEMS_PER_PAGE);
  const maxDemand = Math.max(...allCourses.map(c => c.totalPredictedDemand), 1);

  const handleBulkApprove = () => {
    if (pendingFiltered.length === 0) return;
    bulkApproveMut.mutate({
      approvals: pendingFiltered.map(c => ({ courseId: c.courseId, sectionCount: c.recommendedSectionCount })),
    });
  };

  return (
    <div className="space-y-4">
      {/* 0. Planning status warning */}
      {!isPlanning && termStatus !== undefined && (
        <Alert>
          <AlertCircle className="h-4 w-4" />
          <AlertDescription>
            Onay işlemleri yalnızca dönem &apos;Planlama&apos; aşamasında yapılabilir.
          </AlertDescription>
        </Alert>
      )}

      {/* 1. Conflicts — shown FIRST if they exist, because they're urgent */}
      {conflicts.length > 0 && (
        <div className="rounded-lg border border-red-200 bg-red-50">
          <button
            onClick={() => setConflictsOpen(p => !p)}
            className="w-full flex items-center justify-between px-4 py-3 text-left"
          >
            <div className="flex items-center gap-2">
              <AlertTriangle className="h-4 w-4 text-red-600" />
              <span className="text-sm font-medium text-red-700">
                {conflicts.length} çakışma tespit edildi
              </span>
              <div className="flex gap-1 ml-2">
                {Array.from(groupConflicts(conflicts).entries()).map(([type, count]) => (
                  <Badge key={type} variant={CONFLICT_CFG[type]?.variant ?? 'outline'} className="text-[10px] px-1.5 py-0">
                    {CONFLICT_CFG[type]?.label ?? type} {count}
                  </Badge>
                ))}
              </div>
            </div>
            <ChevronDown className={cn('h-4 w-4 text-red-600 transition-transform', conflictsOpen && 'rotate-180')} />
          </button>
          {conflictsOpen && (
            <div className="px-4 pb-4 space-y-2 border-t border-red-200 pt-3">
              {conflicts.map((c, i) => (
                <div key={i} className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-1 text-sm py-2 border-b border-red-100 last:border-0">
                  <div>
                    <span className="font-mono text-xs font-bold text-blue-700">{c.courseCode}</span>
                    <span className="ml-2">{c.courseName}</span>
                    <span className="text-gray-500 ml-2">— {c.message}</span>
                  </div>
                  <span className="text-xs text-gray-500 shrink-0">
                    {fmtConflictTime(c.day, c.startTime, c.endTime)}
                    {c.classroomName && ` · ${c.classroomName}`}
                  </span>
                </div>
              ))}
            </div>
          )}
        </div>
      )}

      {/* 2. Compact approval progress + bulk action */}
      <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3">
        <div className="flex items-center gap-3 min-w-0">
          <div className="flex items-center gap-2">
            <Progress value={progressPct} className="w-32 h-2" indicatorClassName={progressPct === 100 ? 'bg-emerald-600' : 'bg-blue-600'} />
            <span className="text-xs text-gray-500 whitespace-nowrap">
              <span className="font-medium text-gray-900">{approvedCount}</span> onaylı
              {partialCount > 0 && <>, <span className="font-medium text-gray-900">{partialCount}</span> kısmi</>}
              {completedCount > 0 && <>, <span className="font-medium text-gray-900">{completedCount}</span> tamamlandı</>}
              {rejectedCount > 0 && <>, <span className="font-medium text-gray-900">{rejectedCount}</span> red</>}
              , <span className="font-medium text-gray-900">{pendingCount}</span> bekliyor
            </span>
          </div>
        </div>
        <div className="flex items-center gap-2">
          <div className="relative">
            <Search className="h-3.5 w-3.5 text-gray-500 absolute left-2.5 top-1/2 -translate-y-1/2" />
            <Input
              value={searchTerm}
              onChange={e => { onSearchTermChange(e.target.value); setPage(0); }}
              placeholder="Ders ara..."
              className="pl-8 h-8 w-48 text-sm"
            />
          </div>
          {pendingFiltered.length > 0 && (
            <TooltipProvider>
              <Tooltip>
                <TooltipTrigger asChild>
                  <span>
                    <Button size="sm" onClick={handleBulkApprove} disabled={bulkApproveMut.isPending || !isPlanning} className="h-8 gap-1 text-xs">
                      <Check className="h-3 w-3" />
                      Tümünü Onayla ({pendingFiltered.length})
                    </Button>
                  </span>
                </TooltipTrigger>
                {!isPlanning && (
                  <TooltipContent>Onay işlemleri yalnızca Planlama aşamasında yapılabilir.</TooltipContent>
                )}
              </Tooltip>
            </TooltipProvider>
          )}
        </div>
      </div>

      {/* 3. Course table */}
      {sorted.length === 0 ? (
        <p className="text-sm text-gray-500 py-8 text-center">Eşleşen ders bulunamadı.</p>
      ) : (
        <Card className="overflow-hidden">
          <div className="overflow-x-auto">
            <Table>
              <TableHeader>
                <TableRow className="bg-gray-50 text-xs">
                  <TableHead className="w-8" />
                  <TableHead>Ders</TableHead>
                  <TableHead className="text-right w-24">Talep</TableHead>
                  <TableHead className="text-center w-16">Şube</TableHead>
                  <TableHead className="text-center w-24">Durum</TableHead>
                  <TableHead className="text-center w-20">İşlem</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {paginated.map(course => (
                  <ForecastRow
                    key={course.courseId}
                    course={course}
                    approval={approvalMap.get(course.courseId) ?? null}
                    onApprove={handleApprove}
                    onReject={handleReject}
                    isApproving={approveMut.isPending}
                    isRejecting={rejectMut.isPending}
                    maxDemand={maxDemand}
                    isPlanning={isPlanning}
                  />
                ))}
              </TableBody>
            </Table>
          </div>
          {totalPages > 1 && <Pagination current={page} total={totalPages} count={sorted.length} onPage={setPage} />}
        </Card>
      )}
    </div>
  );
}

function ForecastRow({ course, approval, onApprove, onReject, isApproving, isRejecting, maxDemand, isPlanning }: {
  course: CourseDemandForecast;
  approval: CourseSectionApproval | null;
  onApprove: (id: number, count: number, note?: string) => void;
  onReject: (id: number, note?: string) => void;
  isApproving: boolean; isRejecting: boolean; maxDemand: number; isPlanning: boolean;
}) {
  const [expanded, setExpanded] = useState(false);
  const [sectionCount, setSectionCount] = useState(approval?.approvedSectionCount ?? course.recommendedSectionCount);
  const [note, setNote] = useState(approval?.note ?? '');

  const workflowStatus = getWorkflowStatus(approval);
  const demandPct = (course.totalPredictedDemand / maxDemand) * 100;
  const openedSectionCount = approval?.openedSectionCount ?? 0;
  const remainingSectionCount = approval?.remainingSectionCount ?? 0;

  const rowBg = workflowStatus === 'Completed'
    ? 'bg-green-50'
    : workflowStatus === 'PartiallyOpened'
      ? 'bg-amber-50'
      : workflowStatus === 'Approved'
        ? 'bg-blue-50/40'
        : workflowStatus === 'Rejected'
          ? 'bg-red-50'
          : '';

  return (
    <>
      <TableRow className={cn('cursor-pointer transition-colors hover:bg-gray-50', rowBg)} onClick={() => setExpanded(p => !p)}>
        <TableCell className="px-2">
          <ChevronDown className={cn('h-3.5 w-3.5 text-gray-400 transition-transform', expanded && 'rotate-180')} />
        </TableCell>
        <TableCell>
          <span className="font-mono text-xs font-bold text-blue-700">{course.courseCode}</span>
          <span className="ml-2 text-sm text-gray-900">{course.courseName}</span>
        </TableCell>
        <TableCell className="text-right">
          <div className="flex items-center justify-end gap-2">
            <div className="w-14 h-2 rounded-full bg-gray-200 overflow-hidden hidden sm:block">
              <div className="h-full rounded-full bg-blue-600" style={{ width: `${demandPct}%` }} />
            </div>
            <span className="font-bold tabular-nums text-sm text-gray-900">{course.totalPredictedDemand}</span>
          </div>
        </TableCell>
        <TableCell className="text-center">
          <span className={cn(
            'inline-flex items-center justify-center h-7 w-7 rounded text-xs font-bold',
            course.recommendedSectionCount >= 3 ? 'bg-orange-100 text-orange-800 border border-orange-300' : 'bg-gray-100 text-gray-800 border border-gray-300',
          )}>
            {course.recommendedSectionCount}
          </span>
        </TableCell>
        <TableCell className="text-center">
          <TooltipProvider>
            <Tooltip>
              <TooltipTrigger asChild>
                <Badge variant={getWorkflowVariant(workflowStatus)} className="cursor-default">
                  {getWorkflowLabel(workflowStatus)}
                </Badge>
              </TooltipTrigger>
              <TooltipContent>{WORKFLOW_STATUS_HELP[workflowStatus] ?? workflowStatus}</TooltipContent>
            </Tooltip>
          </TooltipProvider>
        </TableCell>
        <TableCell className="text-center" onClick={e => e.stopPropagation()}>
          {workflowStatus === 'Pending' ? (
            <div className="flex justify-center gap-1">
              <button
                className="h-8 w-8 inline-flex items-center justify-center rounded-md bg-green-50 text-green-700 border border-green-200 hover:bg-green-100 transition-colors disabled:opacity-40"
                onClick={() => onApprove(course.courseId, sectionCount, note || undefined)}
                disabled={isApproving || !isPlanning}
                title={!isPlanning ? 'Onay işlemleri yalnızca Planlama aşamasında yapılabilir.' : undefined}
              >
                <Check className="h-4 w-4" />
              </button>
              <button
                className="h-8 w-8 inline-flex items-center justify-center rounded-md bg-red-50 text-red-700 border border-red-200 hover:bg-red-100 transition-colors disabled:opacity-40"
                onClick={() => onReject(course.courseId, note || undefined)}
                disabled={isRejecting || !isPlanning}
                title={!isPlanning ? 'Onay işlemleri yalnızca Planlama aşamasında yapılabilir.' : undefined}
              >
                <X className="h-4 w-4" />
              </button>
            </div>
          ) : (
            <button
              className="text-xs text-blue-600 hover:text-blue-800 font-medium underline transition-colors disabled:opacity-40"
              onClick={() => onApprove(course.courseId, sectionCount, note || undefined)}
              disabled={isApproving || !isPlanning}
              title={!isPlanning ? 'Onay işlemleri yalnızca Planlama aşamasında yapılabilir.' : undefined}
            >
              Değiştir
            </button>
          )}
        </TableCell>
      </TableRow>

      {/* Expanded detail */}
      {expanded && (
        <TableRow className="bg-gray-50 hover:bg-gray-50">
          <TableCell colSpan={6} className="p-0">
            <div className="px-6 py-4 space-y-4">
              {approval && workflowStatus !== 'Pending' && (
                <div className="flex flex-wrap items-center gap-2 text-xs text-gray-600">
                  <TooltipProvider>
                    <Tooltip>
                      <TooltipTrigger asChild>
                        <Badge variant={getWorkflowVariant(workflowStatus)} className="cursor-default">{getWorkflowLabel(workflowStatus)}</Badge>
                      </TooltipTrigger>
                      <TooltipContent>{WORKFLOW_STATUS_HELP[workflowStatus] ?? workflowStatus}</TooltipContent>
                    </Tooltip>
                  </TooltipProvider>
                  <span>Onaylanan: <strong className="text-gray-900">{approval.approvedSectionCount ?? 0}</strong></span>
                  <span>Açılan: <strong className="text-gray-900">{openedSectionCount}</strong></span>
                  {remainingSectionCount > 0 && <span>Kalan: <strong className="text-gray-900">{remainingSectionCount}</strong></span>}
                  {approval.decidedByUserName && (
                    <span className="text-gray-500">
                      {workflowStatus === 'Rejected' ? 'Reddeden' : 'Onaylayan'}:{' '}
                      <strong className="text-gray-700">{approval.decidedByUserName}</strong>
                    </span>
                  )}
                </div>
              )}

              {/* Approval control row */}
              <div className="flex flex-wrap items-end gap-3 pb-3 border-b border-gray-200">
                <div>
                  <p className="text-[10px] font-semibold text-gray-500 uppercase mb-1">Şube</p>
                  <div className="flex items-center gap-0.5">
                    <Button size="sm" variant="outline" className="h-7 w-7 p-0" onClick={() => setSectionCount(c => Math.max(1, c - 1))} disabled={sectionCount <= 1}>
                      <Minus className="h-3 w-3" />
                    </Button>
                    <span className="w-8 text-center font-bold tabular-nums">{sectionCount}</span>
                    <Button size="sm" variant="outline" className="h-7 w-7 p-0" onClick={() => setSectionCount(c => Math.min(20, c + 1))} disabled={sectionCount >= 20}>
                      <Plus className="h-3 w-3" />
                    </Button>
                  </div>
                </div>
                <div className="flex-1 min-w-[12rem]">
                  <p className="text-[10px] font-semibold text-gray-500 uppercase mb-1">Not</p>
                  <Input value={note} onChange={e => setNote(e.target.value)} placeholder="Karar gerekçesi..." className="h-7 text-xs" />
                </div>
                <Button size="sm" className="h-7 text-xs gap-1" onClick={() => onApprove(course.courseId, sectionCount, note || undefined)} disabled={isApproving || !isPlanning} title={!isPlanning ? 'Onay işlemleri yalnızca Planlama aşamasında yapılabilir.' : undefined}>
                  <Check className="h-3 w-3" />{sectionCount} Şube Onayla
                </Button>
                <Button size="sm" variant="destructive" className="h-7 text-xs gap-1" onClick={() => onReject(course.courseId, note || undefined)} disabled={isRejecting || !isPlanning} title={!isPlanning ? 'Onay işlemleri yalnızca Planlama aşamasında yapılabilir.' : undefined}>
                  <X className="h-3 w-3" />Reddet
                </Button>
              </div>

              {/* Data grid */}
              <div className="grid grid-cols-1 md:grid-cols-3 gap-5 text-sm">
                <DetailDemandBreakdown course={course} />
                <DetailConfidenceAndDistribution course={course} />
                <DetailHistory course={course} />
              </div>
            </div>
          </TableCell>
        </TableRow>
      )}
    </>
  );
}

function DetailDemandBreakdown({ course }: { course: CourseDemandForecast }) {
  const bd = course.demandBreakdown;
  if (!bd) return null;
  const total = course.totalPredictedDemand || 1;
  const items: { label: string; value: number; color: string }[] = [
    { label: 'Normal', value: bd.regularOnSchedule, color: 'bg-emerald-500' },
    { label: 'FF Tekrar', value: bd.retakeFF, color: 'bg-red-500' },
    { label: 'NA Tekrar', value: bd.retakeNA, color: 'bg-orange-500' },
    ...(bd.retakeU > 0 ? [{ label: 'U Tekrar', value: bd.retakeU, color: 'bg-amber-500' }] : []),
    ...(bd.behindSchedule > 0 ? [{ label: 'Geride', value: bd.behindSchedule, color: 'bg-blue-500' }] : []),
    ...(bd.upperCourse > 0 ? [{ label: 'Üstten', value: bd.upperCourse, color: 'bg-violet-500' }] : []),
  ].filter(i => i.value > 0);

  return (
    <div>
      <p className="text-[11px] font-bold uppercase tracking-wider text-gray-500 mb-2">Talep Kırılımı</p>
      <div className="space-y-1.5">
        {items.map(item => (
          <div key={item.label} className="flex items-center gap-2">
            <span className="w-16 text-xs text-gray-500">{item.label}</span>
            <div className="flex-1 h-1.5 rounded-full bg-gray-200 overflow-hidden">
              <div className={cn('h-full rounded-full', item.color)} style={{ width: `${(item.value / total) * 100}%` }} />
            </div>
            <span className="w-6 text-right text-xs font-semibold tabular-nums">{item.value}</span>
          </div>
        ))}
      </div>
      {bd.ectsLimited > 0 && (
        <p className="text-[10px] text-amber-600 mt-2 flex items-center gap-1">
          <AlertTriangle className="h-3 w-3" />{bd.ectsLimited} öğrenci AKTS limitinde
        </p>
      )}
    </div>
  );
}

function DetailConfidenceAndDistribution({ course }: { course: CourseDemandForecast }) {
  const conf = course.confidence;
  const sd = course.sectionDistribution;
  return (
    <div className="space-y-3">
      {conf && (
        <div>
          <div className="flex items-center gap-2 mb-2">
            <p className="text-[11px] font-bold uppercase tracking-wider text-gray-500">Güven Aralığı</p>
            {conf.margin <= 10 ? (
              <Badge variant="success" className="text-[10px] px-1.5 py-0">Yüksek Güven</Badge>
            ) : conf.margin <= 25 ? (
              <Badge variant="warning" className="text-[10px] px-1.5 py-0">Orta Güven</Badge>
            ) : (
              <Badge variant="destructive" className="text-[10px] px-1.5 py-0">Düşük Güven</Badge>
            )}
          </div>
          <div className="rounded-md border p-2.5 text-xs space-y-1">
            <Row label="Aralık" value={`${conf.lowerBound} – ${conf.upperBound}`} />
            <Row label="Marj" value={`±${conf.margin}`} />
            <Row label="Yöntem" value={conf.method} mono />
          </div>
        </div>
      )}
      {sd && (
        <div>
          <p className="text-[11px] font-bold uppercase tracking-wider text-gray-500 mb-2">Şube Dağılımı</p>
          <div className="rounded-md border p-2.5 text-xs space-y-1">
            <Row label="Kapasite/şube" value={String(sd.effectiveCapacityPerSection)} />
            <Row
              label="Doluluk"
              value={`%${(sd.utilizationRate * 100).toFixed(0)}`}
              className={sd.utilizationRate > 0.9 ? 'text-red-600' : sd.utilizationRate > 0.7 ? 'text-amber-600' : 'text-green-600'}
            />
            {sd.studentsPerSection.length > 0 && (
              <div className="flex gap-1 pt-1">
                {sd.studentsPerSection.map((n, i) => (
                  <TooltipProvider key={i}>
                    <Tooltip>
                      <TooltipTrigger asChild>
                        <span className="flex-1 text-center py-0.5 rounded bg-sky-100 text-sky-800 text-[10px] font-semibold">{n}</span>
                      </TooltipTrigger>
                      <TooltipContent>Şube {i + 1}: {n} öğrenci</TooltipContent>
                    </Tooltip>
                  </TooltipProvider>
                ))}
              </div>
            )}
            {sd.warnings.map((w, i) => (
              <p key={i} className="text-[10px] text-amber-600 flex items-center gap-1 pt-0.5">
                <AlertTriangle className="h-2.5 w-2.5 shrink-0" />{w}
              </p>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}

function DetailHistory({ course }: { course: CourseDemandForecast }) {
  const hist = course.historicalEnrollments;
  if (!hist || hist.length === 0) return null;
  const pred = course.totalPredictedDemand || 1;
  return (
    <div>
      <p className="text-[11px] font-bold uppercase tracking-wider text-gray-500 mb-2">Geçmiş Dönemler</p>
      <div className="space-y-2">
        {hist.map(h => {
          const avg = h.sectionCount > 0 ? Math.round(h.actualEnrollment / h.sectionCount) : 0;
          return (
            <div key={h.termId} className="rounded-md border p-2.5">
              <div className="flex items-center justify-between text-xs mb-1">
                <span className="font-medium">{h.termName}</span>
                <span className="text-gray-500">{h.sectionCount} şube · ort {avg}/şube</span>
              </div>
              <div className="flex items-center gap-2">
                <div className="flex-1 h-1.5 rounded-full bg-gray-200 overflow-hidden">
                  <div className="h-full rounded-full bg-blue-600" style={{ width: `${Math.min(100, (h.actualEnrollment / pred) * 100)}%` }} />
                </div>
                <span className="text-xs font-semibold tabular-nums">{h.actualEnrollment}</span>
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
}

/* ═══════════════════════════════════════════════════════════════════════
   SHARED UTILITIES
   ═══════════════════════════════════════════════════════════════════════ */

function Row({ label, value, mono, className }: { label: string; value: string; mono?: boolean; className?: string }) {
  return (
    <div className="flex justify-between">
      <span className="text-gray-500">{label}</span>
      <span className={cn('font-medium', mono && 'font-mono', className)}>{value}</span>
    </div>
  );
}

function Pagination({ current, total, count, onPage }: { current: number; total: number; count: number; onPage: (p: number) => void }) {
  return (
    <div className="flex flex-wrap items-center justify-between gap-2 px-4 py-2.5 border-t text-xs text-gray-500">
      <span>{count} kayıt · sayfa {current + 1}/{total}</span>
      <div className="flex gap-1">
        <Button variant="ghost" size="sm" className="h-7 w-7 p-0" disabled={current === 0} onClick={() => onPage(current - 1)}>
          <ChevronLeft className="h-3.5 w-3.5" />
        </Button>
        <Button variant="ghost" size="sm" className="h-7 w-7 p-0" disabled={current >= total - 1} onClick={() => onPage(current + 1)}>
          <ChevronRight className="h-3.5 w-3.5" />
        </Button>
      </div>
    </div>
  );
}

function groupConflicts(conflicts: SchedulingConflict[]) {
  const map = new Map<string, number>();
  for (const c of conflicts) map.set(c.conflictType, (map.get(c.conflictType) ?? 0) + 1);
  return map;
}

function fmtConflictTime(day: number | null, start: string | null, end: string | null) {
  if (day === null || !start || !end) return '';
  return `${DAY_LABELS[day] ?? ''} ${start.slice(0, 5)}–${end.slice(0, 5)}`;
}
