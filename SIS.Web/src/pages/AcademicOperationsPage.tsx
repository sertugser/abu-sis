import { useCallback, useEffect, useMemo, useState, type ChangeEvent } from 'react';
import { Link, useLocation, useSearchParams } from 'react-router-dom';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { ROUTES } from '@/lib/constants';
import { getAcademicOperationsErrorMessage } from '../api/academicOperations.api';
import {
  useAcademicOperationsContext,
  useAcademicPlanningSummary,
  useApproveCourseSections,
  useBulkApproveCourseSections,
  useDepartmentClassLists,
  useDepartmentCurriculumOverview,
  useRejectCourseSections,
  useSectionApprovals,
} from '../hooks/useAcademicOperations';
import { useActiveTerm } from '../hooks/useActiveTerm';
import type {
  CourseDemandForecast,
  CourseSectionApproval,
  ProgramCurriculumOverview,
} from '../types/academicOperations';
import type { SchedulingConflict } from '../types/scheduling';
import { AlertCircle, BookCopy, Check, CheckCircle2, ChevronDown, ChevronLeft, ChevronRight, GraduationCap, Minus, Plus, RefreshCcw, Search, Users, Waypoints, X } from 'lucide-react';

type OperationsMode = 'planning' | 'class-lists' | 'curriculum';

const PAGE_META: Record<OperationsMode, { title: string; description: string }> = {
  planning: {
    title: 'Planlama Merkezi',
    description: 'Tahminleme, şube ihtiyacı ve mevcut program doğrulaması aynı ekranda izlenir.',
  },
  'class-lists': {
    title: 'Sınıf Listeleri',
    description: 'Bölümde açılan şubeler ve öğrenci listeleri ders bazında görüntülenir.',
  },
  curriculum: {
    title: 'Müfredat Görünümü',
    description: 'Program bazında müfredat yapısı, dönem dağılımı ve kredi yükleri izlenir.',
  },
};

const MODE_LINKS: { mode: OperationsMode; to: string; label: string }[] = [
  { mode: 'planning', to: ROUTES.MANAGEMENT_PLANNING, label: 'Planlama' },
  { mode: 'class-lists', to: ROUTES.MANAGEMENT_CLASS_LISTS, label: 'Sınıf Listeleri' },
  { mode: 'curriculum', to: ROUTES.MANAGEMENT_CURRICULUM, label: 'Müfredat' },
];

const CONFLICT_BADGE_LABELS: Record<string, { label: string; variant: 'destructive' | 'warning' | 'outline' }> = {
  ClassroomConflict: { label: 'Derslik', variant: 'destructive' },
  InstructorConflict: { label: 'Öğretim Üyesi', variant: 'destructive' },
  CapacityConflict: { label: 'Kapasite', variant: 'warning' },
  InstructorAvailabilityConflict: { label: 'Müsaitlik', variant: 'warning' },
  WeeklyLoadConflict: { label: 'Yük Aşımı', variant: 'warning' },
  InvalidScheduleJson: { label: 'Veri', variant: 'outline' },
};

export default function AcademicOperationsPage({ mode }: { mode: OperationsMode }) {
  const [searchParams, setSearchParams] = useSearchParams();
  const [searchTerm, setSearchTerm] = useState('');
  const location = useLocation();
  const meta = PAGE_META[mode];
  const contextQuery = useAcademicOperationsContext();
  const { data: activeTerm } = useActiveTerm();

  const selectedDepartmentId = Number(searchParams.get('departmentId'));
  const departments = contextQuery.data?.accessibleDepartments ?? [];
  const currentDepartmentId = departments.some(department => department.departmentId === selectedDepartmentId)
    ? selectedDepartmentId
    : contextQuery.data?.defaultDepartmentId ?? null;

  useEffect(() => {
    if (!currentDepartmentId) {
      return;
    }

    if (searchParams.get('departmentId') === String(currentDepartmentId)) {
      return;
    }

    const nextSearchParams = new URLSearchParams(searchParams);
    nextSearchParams.set('departmentId', String(currentDepartmentId));
    setSearchParams(nextSearchParams, { replace: true });
  }, [currentDepartmentId, searchParams, setSearchParams]);

  const [debouncedSearch, setDebouncedSearch] = useState('');
  useEffect(() => {
    const timer = setTimeout(() => setDebouncedSearch(searchTerm), 400);
    return () => clearTimeout(timer);
  }, [searchTerm]);

  const planningQuery = useAcademicPlanningSummary(currentDepartmentId, activeTerm?.id, mode === 'planning');
  const classListsQuery = useDepartmentClassLists(currentDepartmentId, activeTerm?.id, mode === 'class-lists', debouncedSearch || undefined);
  const curriculumQuery = useDepartmentCurriculumOverview(currentDepartmentId, mode === 'curriculum');

  const handleDepartmentChange = (event: ChangeEvent<HTMLSelectElement>) => {
    const nextSearchParams = new URLSearchParams(searchParams);
    nextSearchParams.set('departmentId', event.target.value);
    setSearchParams(nextSearchParams);
  };

  if (contextQuery.isLoading) {
    return (
      <div className="space-y-6">
        <Skeleton className="h-10 w-72" />
        <Skeleton className="h-12 rounded-xl" />
        <Skeleton className="h-80 rounded-xl" />
      </div>
    );
  }

  if (contextQuery.isError || !contextQuery.data) {
    return (
      <Alert variant="destructive">
        <AlertCircle className="h-4 w-4" />
        <AlertTitle>Akademik operasyon sayfası yüklenemedi</AlertTitle>
        <AlertDescription>
          {getAcademicOperationsErrorMessage(contextQuery.error, 'Yetkili kapsam bilgileri alınamadı.')}
        </AlertDescription>
      </Alert>
    );
  }

  const context = contextQuery.data;
  const selectedDepartment = departments.find(department => department.departmentId === currentDepartmentId) ?? null;

  return (
    <div className="space-y-6">
      <div className="flex flex-col xl:flex-row xl:items-start xl:justify-between gap-4">
        <div className="flex flex-col sm:flex-row gap-3">
          <select
            value={currentDepartmentId ?? ''}
            onChange={handleDepartmentChange}
            className="h-10 rounded-md border border-input bg-background px-3 text-sm text-foreground"
            disabled={departments.length <= 1}
          >
            {departments.map(department => (
              <option key={department.departmentId} value={department.departmentId}>
                {department.departmentCode} - {department.departmentName}
              </option>
            ))}
          </select>
          <Button
            variant="outline"
            onClick={() => {
              if (mode === 'planning') planningQuery.refetch();
              if (mode === 'class-lists') classListsQuery.refetch();
              if (mode === 'curriculum') curriculumQuery.refetch();
            }}
            className="gap-2"
          >
            <RefreshCcw className="h-4 w-4" />
            Yenile
          </Button>
        </div>
      </div>

      <div className="flex flex-wrap gap-2">
        {MODE_LINKS.map(link => (
          <Button
            key={link.mode}
            asChild
            variant={location.pathname === link.to ? 'default' : 'outline'}
            size="sm"
          >
            <Link to={link.to + (currentDepartmentId ? `?departmentId=${currentDepartmentId}` : '')}>{link.label}</Link>
          </Button>
        ))}
      </div>

      {!selectedDepartment ? (
        <Card>
          <CardContent className="py-16 text-center text-muted-foreground">
            Bu rol için görüntülenebilir bölüm bulunamadı.
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
              errorMessage={planningQuery.isError ? getAcademicOperationsErrorMessage(planningQuery.error, 'Planlama özeti yüklenemedi.') : null}
              summary={planningQuery.data ?? null}
              searchTerm={searchTerm}
              onSearchTermChange={setSearchTerm}
            />
          )}
          {mode === 'class-lists' && (
            <ClassListsMode
              isLoading={classListsQuery.isLoading}
              errorMessage={classListsQuery.isError ? getAcademicOperationsErrorMessage(classListsQuery.error, 'Sınıf listeleri yüklenemedi.') : null}
              classLists={classListsQuery.data ?? null}
              searchTerm={searchTerm}
              onSearchTermChange={setSearchTerm}
            />
          )}
          {mode === 'curriculum' && (
            <CurriculumMode
              isLoading={curriculumQuery.isLoading}
              errorMessage={curriculumQuery.isError ? getAcademicOperationsErrorMessage(curriculumQuery.error, 'Müfredat görünümü yüklenemedi.') : null}
              overview={curriculumQuery.data ?? null}
              searchTerm={searchTerm}
              onSearchTermChange={setSearchTerm}
            />
          )}
        </>
      )}
    </div>
  );
}

function PlanningMode({
  departmentId,
  termId,
  departmentName,
  isLoading,
  errorMessage,
  summary,
  searchTerm,
  onSearchTermChange,
}: {
  departmentId: number;
  termId: number | null;
  departmentName: string;
  isLoading: boolean;
  errorMessage: string | null;
  summary: ReturnType<typeof useAcademicPlanningSummary>['data'] | null;
  searchTerm: string;
  onSearchTermChange: (value: string) => void;
}) {
  const effectiveTermId = termId ?? summary?.termId ?? null;
  const approvalsQuery = useSectionApprovals(departmentId, effectiveTermId, !!summary);
  const approveMutation = useApproveCourseSections(departmentId, effectiveTermId ?? 0);
  const rejectMutation = useRejectCourseSections(departmentId, effectiveTermId ?? 0);
  const bulkApproveMutation = useBulkApproveCourseSections(departmentId, effectiveTermId ?? 0);

  const approvalMap = useMemo(() => {
    const map = new Map<number, CourseSectionApproval>();
    for (const a of approvalsQuery.data ?? []) map.set(a.courseId, a);
    return map;
  }, [approvalsQuery.data]);

  const handleApprove = useCallback((courseId: number, sectionCount: number, note?: string) => {
    approveMutation.mutate({ courseId, sectionCount, note });
  }, [approveMutation]);

  const handleReject = useCallback((courseId: number, note?: string) => {
    rejectMutation.mutate({ courseId, note });
  }, [rejectMutation]);

  if (isLoading) {
    return <Skeleton className="h-[32rem] rounded-xl" />;
  }

  if (errorMessage) {
    return (
      <Alert variant="destructive">
        <AlertCircle className="h-4 w-4" />
        <AlertTitle>Planlama ekranı açılamadı</AlertTitle>
        <AlertDescription>{errorMessage}</AlertDescription>
      </Alert>
    );
  }

  if (!summary) {
    return (
      <Card>
        <CardContent className="py-16 text-center text-muted-foreground">
          {departmentName} için planlama özeti bulunamadı.
        </CardContent>
      </Card>
    );
  }

  const forecastCourses = (summary.forecastSummary?.courseSummaries ?? []).filter(course => {
    if (!searchTerm.trim()) return true;
    const normalizedSearch = searchTerm.toLowerCase();
    return `${course.courseCode} ${course.courseName}`.toLowerCase().includes(normalizedSearch);
  });

  const pendingCourses = forecastCourses.filter(c => {
    const approval = approvalMap.get(c.courseId);
    return !approval || approval.status === 'Pending';
  });

  const handleBulkApprove = () => {
    if (pendingCourses.length === 0) return;
    bulkApproveMutation.mutate({
      approvals: pendingCourses.map(c => ({
        courseId: c.courseId,
        sectionCount: c.recommendedSectionCount,
      })),
    });
  };

  const approvedCount = forecastCourses.filter(c => approvalMap.get(c.courseId)?.status === 'Approved').length;
  const rejectedCount = forecastCourses.filter(c => approvalMap.get(c.courseId)?.status === 'Rejected').length;

  return (
    <div className="space-y-6">
      <Card>
        <CardHeader className="flex flex-col lg:flex-row lg:items-center lg:justify-between gap-4">
          <div>
            <CardTitle>Talep ve Şube Önerisi</CardTitle>
            <CardDescription>
              FF/tekrar, üstten alma ve kapasite bazlı şube ihtiyacı tek listede.
              {forecastCourses.length > 0 && (
                <span className="ml-2">
                  <Badge variant="success" className="mr-1">{approvedCount} onaylı</Badge>
                  <Badge variant="destructive" className="mr-1">{rejectedCount} reddedildi</Badge>
                  <Badge variant="outline">{pendingCourses.length} bekliyor</Badge>
                </span>
              )}
            </CardDescription>
          </div>
          <div className="flex flex-col sm:flex-row gap-2">
            {pendingCourses.length > 0 && (
              <Button
                size="sm"
                onClick={handleBulkApprove}
                disabled={bulkApproveMutation.isPending}
              >
                <Check className="h-4 w-4 mr-1" />
                Tümünü Onayla ({pendingCourses.length})
              </Button>
            )}
            <div className="relative w-full sm:w-64">
              <Search className="h-4 w-4 text-muted-foreground absolute left-3 top-1/2 -translate-y-1/2" />
              <Input
                value={searchTerm}
                onChange={event => onSearchTermChange(event.target.value)}
                placeholder="Ders kodu veya adına göre filtrele"
                className="pl-9"
              />
            </div>
          </div>
        </CardHeader>
        <CardContent>
          {forecastCourses.length === 0 ? (
            <p className="text-sm text-muted-foreground py-8 text-center">Filtreyle eşleşen tahminleme kaydı bulunamadı.</p>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Ders</TableHead>
                  <TableHead>İlk Talep</TableHead>
                  <TableHead>Tekrar</TableHead>
                  <TableHead>Üstten</TableHead>
                  <TableHead>Tahmini Talep</TableHead>
                  <TableHead>Önerilen Şube</TableHead>
                  <TableHead>Durum</TableHead>
                  <TableHead>İşlem</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {forecastCourses.map(course => (
                  <ForecastRow
                    key={course.courseId}
                    course={course}
                    approval={approvalMap.get(course.courseId) ?? null}
                    onApprove={handleApprove}
                    onReject={handleReject}
                    isApproving={approveMutation.isPending}
                    isRejecting={rejectMutation.isPending}
                  />
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      <ConflictAnalysisCard conflicts={summary.scheduleValidation.conflicts} />
    </div>
  );
}

function ClassListsMode({
  isLoading,
  errorMessage,
  classLists,
  searchTerm,
  onSearchTermChange,
}: {
  isLoading: boolean;
  errorMessage: string | null;
  classLists: ReturnType<typeof useDepartmentClassLists>['data'] | null;
  searchTerm: string;
  onSearchTermChange: (value: string) => void;
}) {
  const [expandedSectionId, setExpandedSectionId] = useState<number | null>(null);

  if (isLoading) {
    return <Skeleton className="h-[32rem] rounded-xl" />;
  }

  if (errorMessage) {
    return (
      <Alert variant="destructive">
        <AlertCircle className="h-4 w-4" />
        <AlertTitle>Sınıf listeleri yüklenemedi</AlertTitle>
        <AlertDescription>{errorMessage}</AlertDescription>
      </Alert>
    );
  }

  if (!classLists) {
    return (
      <Card>
        <CardContent className="py-16 text-center text-muted-foreground">
          Gösterilecek sınıf listesi bulunamadı.
        </CardContent>
      </Card>
    );
  }

  const filteredSections = classLists.sections;

  return (
    <div className="space-y-6">
      <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
        <MetricCard label="Toplam Şube" value={String(classLists.totalSections)} icon={BookCopy} />
        <MetricCard label="Toplam Öğrenci" value={String(classLists.totalStudents)} icon={Users} />
        <MetricCard label="Dönem" value={classLists.termName} icon={Waypoints} />
      </div>

      {classLists.notes.length > 0 && (
        <Alert>
          <AlertCircle className="h-4 w-4" />
          <AlertTitle>Sınıf listesi notları</AlertTitle>
          <AlertDescription className="space-y-1">
            {classLists.notes.map(note => <p key={note}>{note}</p>)}
          </AlertDescription>
        </Alert>
      )}

      <Card>
        <CardHeader className="flex flex-col lg:flex-row lg:items-center lg:justify-between gap-4">
          <div>
            <CardTitle>Şube Arama</CardTitle>
            <CardDescription>Ders, öğretim üyesi veya derslik adına göre filtreleyin.</CardDescription>
          </div>
          <div className="relative w-full lg:w-80">
            <Search className="h-4 w-4 text-muted-foreground absolute left-3 top-1/2 -translate-y-1/2" />
            <Input
              value={searchTerm}
              onChange={event => onSearchTermChange(event.target.value)}
              placeholder="Ders veya derslik ara"
              className="pl-9"
            />
          </div>
        </CardHeader>
      </Card>

      {filteredSections.length === 0 ? (
        <Card>
          <CardContent className="py-16 text-center text-muted-foreground">
            Filtreye uyan şube bulunamadı.
          </CardContent>
        </Card>
      ) : (
        <div className="space-y-4">
          {filteredSections.map(section => (
            <Card key={section.sectionId} className="overflow-hidden">
              <button
                className="w-full px-5 py-4 text-left hover:bg-muted/30 transition-colors"
                onClick={() => setExpandedSectionId(current => current === section.sectionId ? null : section.sectionId)}
              >
                <div className="flex flex-col xl:flex-row xl:items-center xl:justify-between gap-3">
                  <div>
                    <div className="flex items-center gap-2 flex-wrap">
                      <span className="font-mono text-sm font-semibold text-primary">{section.courseCode}</span>
                      <span className="font-medium text-foreground">{section.courseName}</span>
                      <Badge variant={section.isActive ? 'success' : 'secondary'}>
                        {section.isActive ? 'Aktif' : 'Pasif'}
                      </Badge>
                    </div>
                    <p className="text-sm text-muted-foreground mt-1">
                      {section.instructorName || 'Öğretim üyesi atanmamış'} • {section.classroomName || 'Derslik atanmamış'} • {section.scheduleSummary || 'Program tanımlanmamış'}
                    </p>
                  </div>
                  <div className="flex items-center gap-3 text-sm">
                    <Badge variant="outline">{section.activeStudentCount}/{section.capacity}</Badge>
                    <span className="text-muted-foreground">Şube #{section.sectionId}</span>
                  </div>
                </div>
              </button>

              {expandedSectionId === section.sectionId && (
                <CardContent className="border-t border-border/60 py-4">
                  {section.students.length === 0 ? (
                    <p className="text-sm text-muted-foreground">Bu şubede aktif öğrenci bulunmuyor.</p>
                  ) : (
                    <Table>
                      <TableHeader>
                        <TableRow>
                          <TableHead>Öğrenci No</TableHead>
                          <TableHead>Ad Soyad</TableHead>
                          <TableHead>E-posta</TableHead>
                          <TableHead>Durum</TableHead>
                          <TableHead>Not</TableHead>
                        </TableRow>
                      </TableHeader>
                      <TableBody>
                        {section.students.map(student => (
                          <TableRow key={`${section.sectionId}-${student.studentId}`}>
                            <TableCell className="font-mono text-xs">{student.studentNumber}</TableCell>
                            <TableCell>{student.studentName}</TableCell>
                            <TableCell>{student.email || '—'}</TableCell>
                            <TableCell>{student.status}</TableCell>
                            <TableCell>{student.grade ?? '—'}</TableCell>
                          </TableRow>
                        ))}
                      </TableBody>
                    </Table>
                  )}
                </CardContent>
              )}
            </Card>
          ))}
        </div>
      )}
    </div>
  );
}

function CurriculumMode({
  isLoading,
  errorMessage,
  overview,
  searchTerm,
  onSearchTermChange,
}: {
  isLoading: boolean;
  errorMessage: string | null;
  overview: ReturnType<typeof useDepartmentCurriculumOverview>['data'] | null;
  searchTerm: string;
  onSearchTermChange: (value: string) => void;
}) {
  if (isLoading) {
    return <Skeleton className="h-[32rem] rounded-xl" />;
  }

  if (errorMessage) {
    return (
      <Alert variant="destructive">
        <AlertCircle className="h-4 w-4" />
        <AlertTitle>Müfredat görünümü yüklenemedi</AlertTitle>
        <AlertDescription>{errorMessage}</AlertDescription>
      </Alert>
    );
  }

  if (!overview) {
    return (
      <Card>
        <CardContent className="py-16 text-center text-muted-foreground">
          Gösterilecek müfredat özeti bulunamadı.
        </CardContent>
      </Card>
    );
  }

  const filteredPrograms = overview.programs.filter(program => {
    if (!searchTerm.trim()) return true;
    const normalizedSearch = searchTerm.toLowerCase();
    return `${program.programCode} ${program.programName} ${program.curriculumVersion}`.toLowerCase().includes(normalizedSearch);
  });

  return (
    <div className="space-y-6">
      <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-4 gap-4">
        <MetricCard label="Program" value={String(overview.programCount)} icon={GraduationCap} />
        <MetricCard label="Aktif Müfredat" value={String(overview.activeCurriculumCount)} icon={CheckCircle2} />
        <MetricCard label="Toplam Ders" value={String(overview.totalCourses)} icon={BookCopy} />
        <MetricCard label="Toplam AKTS" value={String(overview.totalEcts)} icon={Waypoints} />
      </div>

      {overview.notes.length > 0 && (
        <Alert>
          <AlertCircle className="h-4 w-4" />
          <AlertTitle>Müfredat notları</AlertTitle>
          <AlertDescription className="space-y-1">
            {overview.notes.map(note => <p key={note}>{note}</p>)}
          </AlertDescription>
        </Alert>
      )}

      <Card>
        <CardHeader className="flex flex-col lg:flex-row lg:items-center lg:justify-between gap-4">
          <div>
            <CardTitle>Program Filtreleme</CardTitle>
            <CardDescription>Kod, program adı veya versiyon ile arama yapın.</CardDescription>
          </div>
          <div className="relative w-full lg:w-80">
            <Search className="h-4 w-4 text-muted-foreground absolute left-3 top-1/2 -translate-y-1/2" />
            <Input
              value={searchTerm}
              onChange={event => onSearchTermChange(event.target.value)}
              placeholder="Program veya versiyon ara"
              className="pl-9"
            />
          </div>
        </CardHeader>
      </Card>

      {filteredPrograms.length === 0 ? (
        <Card>
          <CardContent className="py-16 text-center text-muted-foreground">
            Filtreye uyan müfredat bulunamadı.
          </CardContent>
        </Card>
      ) : (
        <div className="space-y-4">
          {filteredPrograms.map(program => (
            <CurriculumProgramCard key={`${program.programId}-${program.curriculumId}`} program={program} />
          ))}
        </div>
      )}
    </div>
  );
}

const APPROVAL_STATUS_CONFIG = {
  Pending: { label: 'Bekliyor', variant: 'outline' as const },
  Approved: { label: 'Onaylandı', variant: 'success' as const },
  Rejected: { label: 'Reddedildi', variant: 'destructive' as const },
};

function ForecastRow({ course, approval, onApprove, onReject, isApproving, isRejecting }: {
  course: CourseDemandForecast;
  approval: CourseSectionApproval | null;
  onApprove: (courseId: number, sectionCount: number, note?: string) => void;
  onReject: (courseId: number, note?: string) => void;
  isApproving: boolean;
  isRejecting: boolean;
}) {
  const [expanded, setExpanded] = useState(false);
  const [sectionCount, setSectionCount] = useState(approval?.approvedSectionCount ?? course.recommendedSectionCount);
  const [note, setNote] = useState(approval?.note ?? '');
  const bd = course.demandBreakdown;
  const conf = course.confidence;
  const sd = course.sectionDistribution;
  const hist = course.historicalEnrollments;

  const status = approval?.status ?? 'Pending';
  const statusConfig = APPROVAL_STATUS_CONFIG[status];

  return (
    <>
      <TableRow
        className="cursor-pointer hover:bg-muted/30"
        onClick={() => setExpanded(prev => !prev)}
      >
        <TableCell>
          <div className="flex items-center gap-2">
            <ChevronDown className={`h-3.5 w-3.5 text-muted-foreground transition-transform ${expanded ? 'rotate-180' : ''}`} />
            <div>
              <div className="font-mono text-xs font-semibold text-primary">{course.courseCode}</div>
              <div className="text-sm text-foreground">{course.courseName}</div>
            </div>
          </div>
        </TableCell>
        <TableCell>{course.predictedEligibleCount}</TableCell>
        <TableCell>{course.predictedRetakeCount}</TableCell>
        <TableCell>{course.predictedOverloadOrUpperClassCount}</TableCell>
        <TableCell className="font-semibold">{course.totalPredictedDemand}</TableCell>
        <TableCell>
          <Badge variant={course.recommendedSectionCount > 1 ? 'warning' : 'outline'}>
            {course.recommendedSectionCount}
          </Badge>
        </TableCell>
        <TableCell>
          <Badge variant={statusConfig.variant}>{statusConfig.label}</Badge>
          {approval?.approvedSectionCount != null && status === 'Approved' && (
            <span className="text-xs text-muted-foreground ml-1">({approval.approvedSectionCount} şube)</span>
          )}
        </TableCell>
        <TableCell onClick={e => e.stopPropagation()}>
          {status === 'Pending' && (
            <div className="flex gap-1">
              <Button
                size="sm"
                variant="ghost"
                className="h-7 w-7 p-0 text-green-600 hover:text-green-700 hover:bg-green-50"
                onClick={() => onApprove(course.courseId, sectionCount, note || undefined)}
                disabled={isApproving}
                title="Onayla"
              >
                <Check className="h-4 w-4" />
              </Button>
              <Button
                size="sm"
                variant="ghost"
                className="h-7 w-7 p-0 text-red-600 hover:text-red-700 hover:bg-red-50"
                onClick={() => onReject(course.courseId, note || undefined)}
                disabled={isRejecting}
                title="Reddet"
              >
                <X className="h-4 w-4" />
              </Button>
            </div>
          )}
          {status !== 'Pending' && (
            <Button
              size="sm"
              variant="ghost"
              className="h-7 text-xs"
              onClick={() => onApprove(course.courseId, sectionCount, note || undefined)}
              disabled={isApproving}
            >
              Güncelle
            </Button>
          )}
        </TableCell>
      </TableRow>
      {expanded && (
        <TableRow className="bg-muted/20">
          <TableCell colSpan={8} className="p-4">
            {/* Onay kontrolleri */}
            <div className="flex flex-wrap items-end gap-4 mb-4 pb-4 border-b border-border/60">
              <div className="space-y-1">
                <label className="text-xs font-medium text-muted-foreground">Şube Sayısı</label>
                <div className="flex items-center gap-1">
                  <Button size="sm" variant="outline" className="h-8 w-8 p-0" onClick={() => setSectionCount(c => Math.max(1, c - 1))}>
                    <Minus className="h-3 w-3" />
                  </Button>
                  <span className="w-8 text-center font-semibold">{sectionCount}</span>
                  <Button size="sm" variant="outline" className="h-8 w-8 p-0" onClick={() => setSectionCount(c => c + 1)}>
                    <Plus className="h-3 w-3" />
                  </Button>
                </div>
              </div>
              <div className="space-y-1 flex-1 min-w-[16rem]">
                <label className="text-xs font-medium text-muted-foreground">Not (isteğe bağlı)</label>
                <Input
                  value={note}
                  onChange={e => setNote(e.target.value)}
                  placeholder="Karar gerekçesi veya açıklama..."
                  className="h-8 text-sm"
                />
              </div>
              <div className="flex gap-2">
                <Button
                  size="sm"
                  onClick={() => onApprove(course.courseId, sectionCount, note || undefined)}
                  disabled={isApproving}
                >
                  <Check className="h-4 w-4 mr-1" />
                  {sectionCount} Şube ile Onayla
                </Button>
                <Button
                  size="sm"
                  variant="destructive"
                  onClick={() => onReject(course.courseId, note || undefined)}
                  disabled={isRejecting}
                >
                  <X className="h-4 w-4 mr-1" />
                  Reddet
                </Button>
              </div>
            </div>
            <div className="grid grid-cols-1 md:grid-cols-3 gap-4 text-sm">
              {/* Talep Kırılımı */}
              {bd && (
                <div className="space-y-2">
                  <p className="font-semibold text-foreground">Talep Kırılımı</p>
                  <div className="space-y-1 text-muted-foreground">
                    <p>FF Tekrar: <span className="font-medium text-foreground">{bd.retakeFF}</span></p>
                    <p>NA Tekrar: <span className="font-medium text-foreground">{bd.retakeNA}</span></p>
                    <p>U Tekrar: <span className="font-medium text-foreground">{bd.retakeU}</span></p>
                    <p>Normal (zamanında): <span className="font-medium text-foreground">{bd.regularOnSchedule}</span></p>
                    <p>Geride kalan: <span className="font-medium text-foreground">{bd.behindSchedule}</span></p>
                    <p>Üstten alan: <span className="font-medium text-foreground">{bd.upperCourse}</span></p>
                    {bd.ectsLimited > 0 && (
                      <p className="text-warning">AKTS limitine takılanlar: <span className="font-medium">{bd.ectsLimited}</span></p>
                    )}
                  </div>
                </div>
              )}

              {/* Güven Aralığı ve Şube Dağılımı */}
              <div className="space-y-2">
                {conf && (
                  <>
                    <p className="font-semibold text-foreground">Güven Aralığı</p>
                    <div className="space-y-1 text-muted-foreground">
                      <p>Aralık: <span className="font-medium text-foreground">{conf.lowerBound} - {conf.upperBound}</span></p>
                      <p>Marj: <span className="font-medium text-foreground">±{conf.margin}</span></p>
                      <p>Yöntem: <span className="font-medium text-foreground">{conf.method}</span></p>
                    </div>
                  </>
                )}
                {sd && (
                  <>
                    <p className="font-semibold text-foreground mt-3">Şube Dağılımı</p>
                    <div className="space-y-1 text-muted-foreground">
                      <p>Şube başı kapasite: <span className="font-medium text-foreground">{sd.effectiveCapacityPerSection}</span></p>
                      <p>Toplam kapasite: <span className="font-medium text-foreground">{sd.totalCapacity}</span></p>
                      <p>Doluluk oranı: <span className="font-medium text-foreground">%{(sd.utilizationRate * 100).toFixed(0)}</span></p>
                      {sd.studentsPerSection.length > 0 && (
                        <p>Öğrenci dağılımı: <span className="font-medium text-foreground">[{sd.studentsPerSection.join(', ')}]</span></p>
                      )}
                      {sd.warnings.map((w, i) => (
                        <p key={i} className="text-warning text-xs">{w}</p>
                      ))}
                    </div>
                  </>
                )}
              </div>

              {/* Geçmiş Dönem Verileri */}
              {hist && hist.length > 0 && (
                <div className="space-y-2">
                  <p className="font-semibold text-foreground">Geçmiş Dönem Kayıtları</p>
                  <div className="space-y-1">
                    {hist.map(h => (
                      <div key={h.termId} className="flex justify-between text-muted-foreground">
                        <span>{h.termName}</span>
                        <span className="font-medium text-foreground">{h.actualEnrollment} öğrenci / {h.sectionCount} şube</span>
                      </div>
                    ))}
                  </div>
                </div>
              )}
            </div>
          </TableCell>
        </TableRow>
      )}
    </>
  );
}

function CurriculumProgramCard({ program }: { program: ProgramCurriculumOverview }) {
  return (
    <Card>
      <CardHeader className="flex flex-col xl:flex-row xl:items-start xl:justify-between gap-4">
        <div>
          <CardTitle className="flex items-center gap-2 flex-wrap">
            <span>{program.programName}</span>
            <Badge variant="outline">{program.programCode}</Badge>
            <Badge variant={program.isActiveCurriculum ? 'success' : 'secondary'}>
              {program.isActiveCurriculum ? 'Aktif Müfredat' : 'Arşiv'}
            </Badge>
          </CardTitle>
          <CardDescription className="mt-1">
            Versiyon {program.curriculumVersion} • {program.totalCourses} ders • {program.totalEcts} AKTS
          </CardDescription>
        </div>
        <div className="flex flex-wrap gap-2 text-sm">
          <Badge variant="outline">Zorunlu: {program.compulsoryCourseCount}</Badge>
          <Badge variant="outline">Seçmeli: {program.electiveCourseCount}</Badge>
          <Badge variant="outline">Kredi: {program.totalCredits}</Badge>
        </div>
      </CardHeader>
      <CardContent>
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Dönem</TableHead>
              <TableHead>Ders</TableHead>
              <TableHead>Kredi</TableHead>
              <TableHead>AKTS</TableHead>
              <TableHead>Zorunlu</TableHead>
              <TableHead>Seçmeli</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {program.semesterSummaries.map(semester => (
              <TableRow key={`${program.curriculumId}-${semester.semesterNumber}`}>
                <TableCell>{semester.label}</TableCell>
                <TableCell>{semester.courseCount}</TableCell>
                <TableCell>{semester.credits}</TableCell>
                <TableCell>{semester.ects}</TableCell>
                <TableCell>{semester.compulsoryCourseCount}</TableCell>
                <TableCell>{semester.electiveCourseCount}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </CardContent>
    </Card>
  );
}

function MetricCard({
  label,
  value,
  icon: Icon,
}: {
  label: string;
  value: string;
  icon: typeof AlertCircle;
}) {
  return (
    <Card className="border-l-4 border-l-primary">
      <CardContent className="p-5 flex items-center gap-4">
        <div className="flex h-11 w-11 items-center justify-center rounded-xl bg-primary/10 text-primary">
          <Icon className="h-5 w-5" />
        </div>
        <div>
          <p className="text-xs font-medium uppercase tracking-wider text-muted-foreground">{label}</p>
          <p className="text-xl font-heading font-bold text-foreground mt-1">{value}</p>
        </div>
      </CardContent>
    </Card>
  );
}

function formatConflictTime(day: number | null, startTime: string | null, endTime: string | null) {
  if (day === null || !startTime || !endTime) {
    return 'Zaman bilgisi yok';
  }

  return `${DAY_LABELS[day] ?? 'Gun'} ${startTime.slice(0, 5)}-${endTime.slice(0, 5)}`;
}

const DAY_LABELS: Record<number, string> = {
  0: 'Pazar',
  1: 'Pazartesi',
  2: 'Salı',
  3: 'Çarşamba',
  4: 'Perşembe',
  5: 'Cuma',
  6: 'Cumartesi',
};

/* ────────────────── Conflict Analysis with Pagination ────────────────── */

const CONFLICTS_PER_PAGE = 10;

function ConflictAnalysisCard({ conflicts }: { conflicts: SchedulingConflict[] }) {
  const [page, setPage] = useState(0);
  const totalPages = Math.max(1, Math.ceil(conflicts.length / CONFLICTS_PER_PAGE));
  const paginatedConflicts = useMemo(
    () => conflicts.slice(page * CONFLICTS_PER_PAGE, (page + 1) * CONFLICTS_PER_PAGE),
    [conflicts, page],
  );

  return (
    <Card>
      <CardHeader>
        <CardTitle>Çakışma Analizi</CardTitle>
        <CardDescription>Mevcut aktif şube programları bu bölüm kapsamında doğrulandı.</CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        {conflicts.length === 0 ? (
          <Alert>
            <CheckCircle2 className="h-4 w-4" />
            <AlertTitle>Çakışma bulunamadı</AlertTitle>
            <AlertDescription>Bölüm programları mevcut kurallar altında geçerli görünüyor.</AlertDescription>
          </Alert>
        ) : (
          <>
            <div className="space-y-3">
              {paginatedConflicts.map((conflict, idx) => (
                <div key={`${page}-${idx}-${conflict.sectionId}-${conflict.conflictType}`} className="rounded-xl border border-border/70 p-4">
                  <div className="flex flex-col lg:flex-row lg:items-center lg:justify-between gap-3">
                    <div>
                      <div className="flex items-center gap-2 flex-wrap">
                        <span className="font-mono text-sm font-semibold text-primary">{conflict.courseCode}</span>
                        <span className="text-sm font-medium">{conflict.courseName}</span>
                        <Badge variant={CONFLICT_BADGE_LABELS[conflict.conflictType]?.variant ?? 'outline'}>
                          {CONFLICT_BADGE_LABELS[conflict.conflictType]?.label ?? conflict.conflictType}
                        </Badge>
                      </div>
                      <p className="text-sm text-muted-foreground mt-1">{conflict.message}</p>
                    </div>
                    <div className="text-sm text-muted-foreground space-y-1">
                      <p>Şube: {conflict.sectionId}{conflict.relatedSectionId ? ` / İlgili: ${conflict.relatedSectionId}` : ''}</p>
                      <p>{formatConflictTime(conflict.day, conflict.startTime, conflict.endTime)}</p>
                      <p>{conflict.classroomName || 'Derslik yok'} • {conflict.instructorName || 'Öğretim üyesi yok'}</p>
                    </div>
                  </div>
                </div>
              ))}
            </div>
            {totalPages > 1 && (
              <div className="flex items-center justify-between pt-2">
                <p className="text-sm text-muted-foreground">
                  {conflicts.length} çakışma, sayfa {page + 1}/{totalPages}
                </p>
                <div className="flex gap-2">
                  <Button variant="outline" size="sm" disabled={page === 0} onClick={() => setPage(p => p - 1)}>
                    <ChevronLeft className="h-4 w-4" />
                  </Button>
                  <Button variant="outline" size="sm" disabled={page >= totalPages - 1} onClick={() => setPage(p => p + 1)}>
                    <ChevronRight className="h-4 w-4" />
                  </Button>
                </div>
              </div>
            )}
          </>
        )}
      </CardContent>
    </Card>
  );
}
