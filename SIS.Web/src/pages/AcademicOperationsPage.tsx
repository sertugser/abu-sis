import { useCallback, useEffect, useMemo, useState, type ChangeEvent } from 'react';
import { Link, useLocation, useSearchParams } from 'react-router-dom';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Progress } from '@/components/ui/progress';
import { Skeleton } from '@/components/ui/skeleton';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/tooltip';
import { ROUTES } from '@/lib/constants';
import { cn } from '@/lib/utils';
import { getAcademicOperationsErrorMessage } from '../api/academicOperations.api';
import {
  useAcademicOperationsContext,
  useAcademicPlanningSummary,
  useApproveCourseSections,
  useBulkApproveCourseSections,
  useDepartmentClassLists,
  useRejectCourseSections,
  useSectionApprovals,
} from '../hooks/useAcademicOperations';
import { useActiveTerm } from '../hooks/useActiveTerm';
import type {
  CourseDemandForecast,
  CourseSectionApproval,
  DepartmentSectionClassList,
} from '../types/academicOperations';
import type { SchedulingConflict } from '../types/scheduling';
import {
  AlertCircle,
  AlertTriangle,
  BarChart3,
  Check,
  ChevronDown,
  ChevronLeft,
  ChevronRight,
  Mail,
  Minus,
  Plus,
  Search,
  Users,
  X,
} from 'lucide-react';

type OperationsMode = 'planning' | 'class-lists';

const MODE_LINKS: { mode: OperationsMode; to: string; label: string; icon: typeof Users }[] = [
  { mode: 'planning', to: ROUTES.COURSE_APPROVAL, label: 'Planlama', icon: BarChart3 },
  { mode: 'class-lists', to: ROUTES.CLASS_LISTS, label: 'Sinif Listeleri', icon: Users },
];

const CONFLICT_CFG: Record<string, { label: string; variant: 'destructive' | 'warning' | 'outline' }> = {
  ClassroomConflict: { label: 'Derslik', variant: 'destructive' },
  InstructorConflict: { label: 'Ogretim Uyesi', variant: 'destructive' },
  CapacityConflict: { label: 'Kapasite', variant: 'warning' },
  WeeklyLoadConflict: { label: 'Yuk Asimi', variant: 'warning' },
  InvalidScheduleJson: { label: 'Veri Hatasi', variant: 'outline' },
};

const DAY_LABELS: Record<number, string> = {
  0: 'Pazar', 1: 'Pazartesi', 2: 'Sali', 3: 'Carsamba', 4: 'Persembe', 5: 'Cuma', 6: 'Cumartesi',
};

const ITEMS_PER_PAGE = 20;

/* ═══════════════════════════════════════════════════════════════════════
   Main Page — only department selector + tabs, nothing else
   ═══════════════════════════════════════════════════════════════════════ */

export default function AcademicOperationsPage({ mode }: { mode: OperationsMode }) {
  const [searchParams, setSearchParams] = useSearchParams();
  const [searchTerm, setSearchTerm] = useState('');
  const location = useLocation();
  const contextQuery = useAcademicOperationsContext();
  const { data: activeTerm } = useActiveTerm();

  const selectedDepartmentId = Number(searchParams.get('departmentId'));
  const departments = contextQuery.data?.accessibleDepartments ?? [];
  const currentDepartmentId = departments.some(d => d.departmentId === selectedDepartmentId)
    ? selectedDepartmentId
    : contextQuery.data?.defaultDepartmentId ?? null;

  useEffect(() => {
    if (!currentDepartmentId) return;
    if (searchParams.get('departmentId') === String(currentDepartmentId)) return;
    const next = new URLSearchParams(searchParams);
    next.set('departmentId', String(currentDepartmentId));
    setSearchParams(next, { replace: true });
  }, [currentDepartmentId, searchParams, setSearchParams]);

  const [debouncedSearch, setDebouncedSearch] = useState('');
  useEffect(() => {
    const t = setTimeout(() => setDebouncedSearch(searchTerm), 400);
    return () => clearTimeout(t);
  }, [searchTerm]);

  const planningQuery = useAcademicPlanningSummary(currentDepartmentId, activeTerm?.id, mode === 'planning');
  const classListsQuery = useDepartmentClassLists(currentDepartmentId, activeTerm?.id, mode === 'class-lists', debouncedSearch || undefined);

  const handleDepartmentChange = (e: ChangeEvent<HTMLSelectElement>) => {
    const next = new URLSearchParams(searchParams);
    next.set('departmentId', e.target.value);
    setSearchParams(next);
  };

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
        <AlertTitle>Sayfa yuklenemedi</AlertTitle>
        <AlertDescription>
          {getAcademicOperationsErrorMessage(contextQuery.error, 'Yetkili kapsam bilgileri alinamadi.')}
        </AlertDescription>
      </Alert>
    );
  }

  const selectedDepartment = departments.find(d => d.departmentId === currentDepartmentId) ?? null;

  return (
    <div className="space-y-5">
      {/* Department selector + tabs — single compact row */}
      <div className="flex flex-col sm:flex-row sm:items-center gap-3">
        {departments.length > 1 && (
          <select
            value={currentDepartmentId ?? ''}
            onChange={handleDepartmentChange}
            className="h-9 rounded-lg border border-gray-300 bg-white px-3 pr-8 text-sm font-medium shadow-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
          >
            {departments.map(d => (
              <option key={d.departmentId} value={d.departmentId}>
                {d.departmentCode} — {d.departmentName}
              </option>
            ))}
          </select>
        )}
        <div className="flex gap-1 rounded-lg bg-gray-100 p-1">
          {MODE_LINKS.map(link => {
            const active = location.pathname === link.to;
            const Icon = link.icon;
            return (
              <Link
                key={link.mode}
                to={link.to + (currentDepartmentId ? `?departmentId=${currentDepartmentId}` : '')}
                className={cn(
                  'flex items-center gap-1.5 rounded-md px-3 py-1.5 text-sm font-medium transition-all',
                  active ? 'bg-white text-gray-900 shadow-sm' : 'text-gray-500 hover:text-gray-900',
                )}
              >
                <Icon className="h-3.5 w-3.5" />
                {link.label}
              </Link>
            );
          })}
        </div>
      </div>

      {/* Content */}
      {!selectedDepartment ? (
        <Card>
          <CardContent className="py-16 text-center text-gray-500">
            Bu rol icin goruntulenebilir bolum bulunamadi.
          </CardContent>
        </Card>
      ) : (
        <>
          {mode === 'planning' && (
            <PlanningMode
              departmentId={currentDepartmentId!}
              termId={activeTerm?.id ?? null}
              departmentName={selectedDepartment.departmentName}
              isLoading={planningQuery.isLoading}
              errorMessage={planningQuery.isError ? getAcademicOperationsErrorMessage(planningQuery.error, 'Planlama verisi yuklenemedi.') : null}
              summary={planningQuery.data ?? null}
              searchTerm={searchTerm}
              onSearchTermChange={setSearchTerm}
            />
          )}
          {mode === 'class-lists' && (
            <ClassListsMode
              isLoading={classListsQuery.isLoading}
              errorMessage={classListsQuery.isError ? getAcademicOperationsErrorMessage(classListsQuery.error, 'Sinif listeleri yuklenemedi.') : null}
              classLists={classListsQuery.data ?? null}
              searchTerm={searchTerm}
              onSearchTermChange={setSearchTerm}
            />
          )}
        </>
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

function PlanningMode({
  departmentId, termId, departmentName, isLoading, errorMessage, summary, searchTerm, onSearchTermChange,
}: {
  departmentId: number; termId: number | null; departmentName: string;
  isLoading: boolean; errorMessage: string | null;
  summary: ReturnType<typeof useAcademicPlanningSummary>['data'] | null;
  searchTerm: string; onSearchTermChange: (v: string) => void;
}) {
  const effectiveTermId = termId ?? summary?.termId ?? null;
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
    return <Card><CardContent className="py-16 text-center text-gray-500">{departmentName} icin veri bulunamadi.</CardContent></Card>;
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
    const aStatus = approvalMap.get(a.courseId)?.status ?? 'Pending';
    const bStatus = approvalMap.get(b.courseId)?.status ?? 'Pending';
    if (aStatus === 'Pending' && bStatus !== 'Pending') return -1;
    if (aStatus !== 'Pending' && bStatus === 'Pending') return 1;
    return b.totalPredictedDemand - a.totalPredictedDemand;
  });

  const approvedCount = allCourses.filter(c => approvalMap.get(c.courseId)?.status === 'Approved').length;
  const rejectedCount = allCourses.filter(c => approvalMap.get(c.courseId)?.status === 'Rejected').length;
  const pendingCount = allCourses.length - approvedCount - rejectedCount;
  const progressPct = allCourses.length > 0 ? ((approvedCount + rejectedCount) / allCourses.length) * 100 : 0;

  const pendingFiltered = sorted.filter(c => {
    const a = approvalMap.get(c.courseId);
    return !a || a.status === 'Pending';
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
                {conflicts.length} cakisma tespit edildi
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
              <span className="font-medium text-gray-900">{approvedCount}</span> onayli
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
            <Button size="sm" onClick={handleBulkApprove} disabled={bulkApproveMut.isPending} className="h-8 gap-1 text-xs">
              <Check className="h-3 w-3" />
              Tumunu Onayla ({pendingFiltered.length})
            </Button>
          )}
        </div>
      </div>

      {/* 3. Course table */}
      {sorted.length === 0 ? (
        <p className="text-sm text-gray-500 py-8 text-center">Eslesen ders bulunamadi.</p>
      ) : (
        <Card className="overflow-hidden">
          <div className="overflow-x-auto">
            <Table>
              <TableHeader>
                <TableRow className="bg-gray-50 text-xs">
                  <TableHead className="w-8" />
                  <TableHead>Ders</TableHead>
                  <TableHead className="text-right w-24">Talep</TableHead>
                  <TableHead className="text-center w-16">Sube</TableHead>
                  <TableHead className="text-center w-24">Durum</TableHead>
                  <TableHead className="text-center w-20">Islem</TableHead>
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

function ForecastRow({ course, approval, onApprove, onReject, isApproving, isRejecting, maxDemand }: {
  course: CourseDemandForecast;
  approval: CourseSectionApproval | null;
  onApprove: (id: number, count: number, note?: string) => void;
  onReject: (id: number, note?: string) => void;
  isApproving: boolean; isRejecting: boolean; maxDemand: number;
}) {
  const [expanded, setExpanded] = useState(false);
  const [sectionCount, setSectionCount] = useState(approval?.approvedSectionCount ?? course.recommendedSectionCount);
  const [note, setNote] = useState(approval?.note ?? '');

  const status = (approval?.status ?? 'Pending') as 'Pending' | 'Approved' | 'Rejected';
  const demandPct = (course.totalPredictedDemand / maxDemand) * 100;

  const rowBg = status === 'Approved' ? 'bg-green-50' : status === 'Rejected' ? 'bg-red-50' : '';

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
          <Badge variant={status === 'Approved' ? 'success' : status === 'Rejected' ? 'destructive' : 'secondary'}>
            {status === 'Approved' ? 'Onayli' : status === 'Rejected' ? 'Red' : 'Bekliyor'}
          </Badge>
        </TableCell>
        <TableCell className="text-center" onClick={e => e.stopPropagation()}>
          {status === 'Pending' ? (
            <div className="flex justify-center gap-1">
              <button
                className="h-8 w-8 inline-flex items-center justify-center rounded-md bg-green-50 text-green-700 border border-green-200 hover:bg-green-100 transition-colors disabled:opacity-40"
                onClick={() => onApprove(course.courseId, sectionCount, note || undefined)}
                disabled={isApproving}
              >
                <Check className="h-4 w-4" />
              </button>
              <button
                className="h-8 w-8 inline-flex items-center justify-center rounded-md bg-red-50 text-red-700 border border-red-200 hover:bg-red-100 transition-colors disabled:opacity-40"
                onClick={() => onReject(course.courseId, note || undefined)}
                disabled={isRejecting}
              >
                <X className="h-4 w-4" />
              </button>
            </div>
          ) : (
            <button
              className="text-xs text-blue-600 hover:text-blue-800 font-medium underline transition-colors disabled:opacity-40"
              onClick={() => onApprove(course.courseId, sectionCount, note || undefined)}
              disabled={isApproving}
            >
              Degistir
            </button>
          )}
        </TableCell>
      </TableRow>

      {/* Expanded detail */}
      {expanded && (
        <TableRow className="bg-gray-50 hover:bg-gray-50">
          <TableCell colSpan={6} className="p-0">
            <div className="px-6 py-4 space-y-4">
              {/* Approval control row */}
              <div className="flex flex-wrap items-end gap-3 pb-3 border-b border-gray-200">
                <div>
                  <p className="text-[10px] font-semibold text-gray-500 uppercase mb-1">Sube</p>
                  <div className="flex items-center gap-0.5">
                    <Button size="sm" variant="outline" className="h-7 w-7 p-0" onClick={() => setSectionCount(c => Math.max(1, c - 1))}>
                      <Minus className="h-3 w-3" />
                    </Button>
                    <span className="w-8 text-center font-bold tabular-nums">{sectionCount}</span>
                    <Button size="sm" variant="outline" className="h-7 w-7 p-0" onClick={() => setSectionCount(c => c + 1)}>
                      <Plus className="h-3 w-3" />
                    </Button>
                  </div>
                </div>
                <div className="flex-1 min-w-[12rem]">
                  <p className="text-[10px] font-semibold text-gray-500 uppercase mb-1">Not</p>
                  <Input value={note} onChange={e => setNote(e.target.value)} placeholder="Karar gerekcesi..." className="h-7 text-xs" />
                </div>
                <Button size="sm" className="h-7 text-xs gap-1" onClick={() => onApprove(course.courseId, sectionCount, note || undefined)} disabled={isApproving}>
                  <Check className="h-3 w-3" />{sectionCount} Sube Onayla
                </Button>
                <Button size="sm" variant="destructive" className="h-7 text-xs gap-1" onClick={() => onReject(course.courseId, note || undefined)} disabled={isRejecting}>
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
    ...(bd.upperCourse > 0 ? [{ label: 'Ustten', value: bd.upperCourse, color: 'bg-violet-500' }] : []),
  ].filter(i => i.value > 0);

  return (
    <div>
      <p className="text-[11px] font-bold uppercase tracking-wider text-gray-500 mb-2">Talep Kirilimi</p>
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
          <AlertTriangle className="h-3 w-3" />{bd.ectsLimited} ogrenci AKTS limitinde
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
          <p className="text-[11px] font-bold uppercase tracking-wider text-gray-500 mb-2">Guven Araligi</p>
          <div className="rounded-md border p-2.5 text-xs space-y-1">
            <Row label="Aralik" value={`${conf.lowerBound} – ${conf.upperBound}`} />
            <Row label="Marj" value={`±${conf.margin}`} />
            <Row label="Yontem" value={conf.method} mono />
          </div>
        </div>
      )}
      {sd && (
        <div>
          <p className="text-[11px] font-bold uppercase tracking-wider text-gray-500 mb-2">Sube Dagilimi</p>
          <div className="rounded-md border p-2.5 text-xs space-y-1">
            <Row label="Kapasite/sube" value={String(sd.effectiveCapacityPerSection)} />
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
                      <TooltipContent>Sube {i + 1}: {n} ogrenci</TooltipContent>
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
      <p className="text-[11px] font-bold uppercase tracking-wider text-gray-500 mb-2">Gecmis Donemler</p>
      <div className="space-y-2">
        {hist.map(h => {
          const avg = h.sectionCount > 0 ? Math.round(h.actualEnrollment / h.sectionCount) : 0;
          return (
            <div key={h.termId} className="rounded-md border p-2.5">
              <div className="flex items-center justify-between text-xs mb-1">
                <span className="font-medium">{h.termName}</span>
                <span className="text-gray-500">{h.sectionCount} sube · ort {avg}/sube</span>
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
   SINIF LISTELERI — Lookup-oriented: "Show me who's where + problems"

   Layout:
   1. Compact stats line + search (NOT big hero cards)
   2. Problems banner if any (no instructor, empty, overfull)
   3. Table with capacity bars
   4. Expandable student rosters
   ═══════════════════════════════════════════════════════════════════════ */

function ClassListsMode({
  isLoading, errorMessage, classLists, searchTerm, onSearchTermChange,
}: {
  isLoading: boolean; errorMessage: string | null;
  classLists: ReturnType<typeof useDepartmentClassLists>['data'] | null;
  searchTerm: string; onSearchTermChange: (v: string) => void;
}) {
  const [expandedId, setExpandedId] = useState<number | null>(null);
  const [page, setPage] = useState(0);

  if (isLoading) return <div className="space-y-3"><Skeleton className="h-10 rounded-lg" /><Skeleton className="h-96 rounded-xl" /></div>;
  if (errorMessage) return <Alert variant="destructive"><AlertCircle className="h-4 w-4" /><AlertTitle>Hata</AlertTitle><AlertDescription>{errorMessage}</AlertDescription></Alert>;
  if (!classLists) return <Card><CardContent className="py-16 text-center text-gray-500">Veri bulunamadi.</CardContent></Card>;

  const sections = classLists.sections;
  const totalPages = Math.max(1, Math.ceil(sections.length / ITEMS_PER_PAGE));
  const paginated = sections.slice(page * ITEMS_PER_PAGE, (page + 1) * ITEMS_PER_PAGE);

  // Detect problems
  const noInstructor = sections.filter(s => !s.instructorName);
  const noClassroom = sections.filter(s => !s.classroomName);
  const overfull = sections.filter(s => s.capacity > 0 && s.activeStudentCount > s.capacity);
  const empty = sections.filter(s => s.activeStudentCount === 0);
  const hasProblems = noInstructor.length > 0 || noClassroom.length > 0 || overfull.length > 0 || empty.length > 0;

  const avgOccupancy = sections.length > 0
    ? Math.round(sections.reduce((sum, s) => sum + (s.capacity > 0 ? s.activeStudentCount / s.capacity : 0), 0) / sections.length * 100)
    : 0;

  return (
    <div className="space-y-4">
      {/* Compact info bar + search */}
      <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3">
        <p className="text-sm text-gray-500">
          <span className="font-semibold text-gray-900">{sections.length}</span> sube ·{' '}
          <span className="font-semibold text-gray-900">{classLists.totalStudents}</span> ogrenci ·{' '}
          %{avgOccupancy} doluluk
        </p>
        <div className="relative">
          <Search className="h-3.5 w-3.5 text-gray-500 absolute left-2.5 top-1/2 -translate-y-1/2" />
          <Input
            value={searchTerm}
            onChange={e => { onSearchTermChange(e.target.value); setPage(0); }}
            placeholder="Ders, ogretim uyesi veya derslik..."
            className="pl-8 h-8 w-64 text-sm"
          />
        </div>
      </div>

      {/* Problems banner — only shown when problems exist */}
      {hasProblems && (
        <div className="flex flex-wrap gap-2 text-xs">
          {noInstructor.length > 0 && (
            <span className="inline-flex items-center gap-1 rounded-md bg-amber-50 border border-amber-200 px-2 py-1 text-amber-700">
              <AlertTriangle className="h-3 w-3" />
              {noInstructor.length} sube ogretim uyesi atanmamis
            </span>
          )}
          {noClassroom.length > 0 && (
            <span className="inline-flex items-center gap-1 rounded-md bg-amber-50 border border-amber-200 px-2 py-1 text-amber-700">
              <AlertTriangle className="h-3 w-3" />
              {noClassroom.length} sube derslik atanmamis
            </span>
          )}
          {overfull.length > 0 && (
            <span className="inline-flex items-center gap-1 rounded-md bg-red-50 border border-red-200 px-2 py-1 text-red-700">
              <AlertCircle className="h-3 w-3" />
              {overfull.length} sube kapasite asiminda
            </span>
          )}
          {empty.length > 0 && (
            <span className="inline-flex items-center gap-1 rounded-md bg-gray-100 border border-gray-300 px-2 py-1 text-gray-500">
              {empty.length} sube bos
            </span>
          )}
        </div>
      )}

      {/* Table */}
      {sections.length === 0 ? (
        <p className="text-sm text-gray-500 py-8 text-center">Eslesen sube bulunamadi.</p>
      ) : (
        <Card className="overflow-hidden">
          <div className="overflow-x-auto">
            <Table>
              <TableHeader>
                <TableRow className="bg-gray-50 text-xs">
                  <TableHead className="w-8" />
                  <TableHead>Ders</TableHead>
                  <TableHead>Ogretim Uyesi</TableHead>
                  <TableHead>Derslik</TableHead>
                  <TableHead className="hidden lg:table-cell">Program</TableHead>
                  <TableHead className="text-center w-28">Doluluk</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {paginated.map(section => (
                  <SectionRow
                    key={section.sectionId}
                    section={section}
                    isExpanded={expandedId === section.sectionId}
                    onToggle={() => setExpandedId(c => c === section.sectionId ? null : section.sectionId)}
                  />
                ))}
              </TableBody>
            </Table>
          </div>
          {totalPages > 1 && <Pagination current={page} total={totalPages} count={sections.length} onPage={setPage} />}
        </Card>
      )}
    </div>
  );
}

function SectionRow({ section, isExpanded, onToggle }: {
  section: DepartmentSectionClassList; isExpanded: boolean; onToggle: () => void;
}) {
  const pct = section.capacity > 0 ? (section.activeStudentCount / section.capacity) * 100 : 0;
  const barColor = pct >= 100 ? 'bg-red-500' : pct >= 80 ? 'bg-amber-500' : 'bg-emerald-500';
  const hasIssue = !section.instructorName || !section.classroomName || pct >= 100;

  return (
    <>
      <TableRow
        className={cn('cursor-pointer transition-colors hover:bg-gray-100', hasIssue && 'bg-amber-50')}
        onClick={onToggle}
      >
        <TableCell className="px-2">
          <ChevronDown className={cn('h-3.5 w-3.5 text-gray-500 transition-transform', isExpanded && 'rotate-180')} />
        </TableCell>
        <TableCell>
          <div className="flex items-center gap-2">
            <div>
              <span className="font-mono text-xs font-bold text-blue-700">{section.courseCode}</span>
              <span className="ml-1.5 text-sm">{section.courseName}</span>
            </div>
            {!section.isActive && <Badge variant="secondary" className="text-[10px] px-1 py-0">Pasif</Badge>}
          </div>
        </TableCell>
        <TableCell className={cn('text-sm', !section.instructorName && 'text-amber-600 italic text-xs')}>
          {section.instructorName || 'Atanmamis'}
        </TableCell>
        <TableCell className={cn('text-sm', !section.classroomName && 'text-amber-600 italic text-xs')}>
          {section.classroomName || 'Atanmamis'}
        </TableCell>
        <TableCell className="hidden lg:table-cell text-xs text-gray-500">
          {section.scheduleSummary || <span className="italic">Tanimlanmamis</span>}
        </TableCell>
        <TableCell>
          <div className="flex items-center gap-2 justify-center">
            <div className="w-12 h-1.5 rounded-full bg-gray-200 overflow-hidden">
              <div className={cn('h-full rounded-full', barColor)} style={{ width: `${Math.min(100, pct)}%` }} />
            </div>
            <span className="text-xs font-semibold tabular-nums whitespace-nowrap">
              {section.activeStudentCount}/{section.capacity}
            </span>
          </div>
        </TableCell>
      </TableRow>

      {isExpanded && (
        <TableRow className="bg-gray-50 hover:bg-gray-50">
          <TableCell colSpan={6} className="p-0">
            <div className="px-6 py-3">
              {section.students.length === 0 ? (
                <p className="text-sm text-gray-500 text-center py-3">Bu subede ogrenci yok.</p>
              ) : (
                <div className="rounded-md border overflow-hidden">
                  <Table>
                    <TableHeader>
                      <TableRow className="bg-gray-50 text-xs">
                        <TableHead>No</TableHead>
                        <TableHead>Ad Soyad</TableHead>
                        <TableHead>E-posta</TableHead>
                        <TableHead className="text-center">Durum</TableHead>
                        <TableHead className="text-center">Not</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {section.students.map(st => (
                        <TableRow key={st.studentId}>
                          <TableCell className="font-mono text-xs">{st.studentNumber}</TableCell>
                          <TableCell className="text-sm">{st.studentName}</TableCell>
                          <TableCell className="text-xs text-gray-500">
                            {st.email ? <span className="flex items-center gap-1"><Mail className="h-3 w-3" />{st.email}</span> : '—'}
                          </TableCell>
                          <TableCell className="text-center">
                            <Badge variant={st.status === 'Registered' ? 'success' : 'outline'} className="text-[10px]">{st.status}</Badge>
                          </TableCell>
                          <TableCell className="text-center font-mono text-xs">
                            {st.grade ? (
                              <span className={cn((st.grade === 'FF' || st.grade === 'NA') && 'text-red-600 font-bold')}>{st.grade}</span>
                            ) : '—'}
                          </TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </div>
              )}
            </div>
          </TableCell>
        </TableRow>
      )}
    </>
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
    <div className="flex items-center justify-between px-4 py-2.5 border-t text-xs text-gray-500">
      <span>{count} kayit · sayfa {current + 1}/{total}</span>
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
