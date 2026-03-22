import { useEffect, useState, type ChangeEvent } from 'react';
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
  useDepartmentClassLists,
  useDepartmentCurriculumOverview,
} from '../hooks/useAcademicOperations';
import { useActiveTerm } from '../hooks/useActiveTerm';
import type {
  CourseDemandForecast,
  ProgramCurriculumOverview,
} from '../types/academicOperations';
import { AlertCircle, BookCopy, CheckCircle2, GraduationCap, RefreshCcw, Search, Users, Waypoints } from 'lucide-react';

type OperationsMode = 'planning' | 'class-lists' | 'curriculum';

const PAGE_META: Record<OperationsMode, { title: string; description: string }> = {
  planning: {
    title: 'Planlama Merkezi',
    description: 'Tahminleme, section ihtiyaci ve mevcut program validasyonu ayni ekranda izlenir.',
  },
  'class-lists': {
    title: 'Sinif Listeleri',
    description: 'Bolumde acilan sectionlar ve ogrenci listeleri ders bazinda goruntulenir.',
  },
  curriculum: {
    title: 'Mufredat Gorunumu',
    description: 'Program bazinda mufredat yapisi, semester dagilimi ve kredi yukleri izlenir.',
  },
};

const MODE_LINKS: { mode: OperationsMode; to: string; label: string }[] = [
  { mode: 'planning', to: ROUTES.MANAGEMENT_PLANNING, label: 'Planlama' },
  { mode: 'class-lists', to: ROUTES.MANAGEMENT_CLASS_LISTS, label: 'Sinif Listeleri' },
  { mode: 'curriculum', to: ROUTES.MANAGEMENT_CURRICULUM, label: 'Mufredat' },
];

const CONFLICT_BADGE_LABELS: Record<string, { label: string; variant: 'destructive' | 'warning' | 'outline' }> = {
  ClassroomConflict: { label: 'Derslik', variant: 'destructive' },
  InstructorConflict: { label: 'Ogretim Uyesi', variant: 'destructive' },
  CapacityConflict: { label: 'Kapasite', variant: 'warning' },
  InstructorAvailabilityConflict: { label: 'Musaitlik', variant: 'warning' },
  WeeklyLoadConflict: { label: 'Yuk Asimi', variant: 'warning' },
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

  const planningQuery = useAcademicPlanningSummary(currentDepartmentId, activeTerm?.id, mode === 'planning');
  const classListsQuery = useDepartmentClassLists(currentDepartmentId, activeTerm?.id, mode === 'class-lists');
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
        <AlertTitle>Akademik operasyon sayfasi yuklenemedi</AlertTitle>
        <AlertDescription>
          {getAcademicOperationsErrorMessage(contextQuery.error, 'Yetkili kapsam bilgileri alinamadi.')}
        </AlertDescription>
      </Alert>
    );
  }

  const context = contextQuery.data;
  const selectedDepartment = departments.find(department => department.departmentId === currentDepartmentId) ?? null;

  return (
    <div className="space-y-6">
      <div className="flex flex-col xl:flex-row xl:items-start xl:justify-between gap-4">
        <div>
          <h1 className="font-heading text-2xl font-extrabold text-foreground tracking-tight">{meta.title}</h1>
          <p className="text-muted-foreground mt-1">{meta.description}</p>
        </div>

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

      {context.notes.length > 0 && (
        <Alert>
          <AlertCircle className="h-4 w-4" />
          <AlertTitle>Sistem notlari</AlertTitle>
          <AlertDescription className="space-y-1">
            {context.notes.map(note => (
              <p key={note}>{note}</p>
            ))}
          </AlertDescription>
        </Alert>
      )}

      {!selectedDepartment ? (
        <Card>
          <CardContent className="py-16 text-center text-muted-foreground">
            Bu rol icin goruntulenebilir bolum bulunamadi.
          </CardContent>
        </Card>
      ) : (
        <>
          {mode === 'planning' && (
            <PlanningMode
              departmentName={selectedDepartment.departmentName}
              isLoading={planningQuery.isLoading}
              errorMessage={planningQuery.isError ? getAcademicOperationsErrorMessage(planningQuery.error, 'Planlama ozeti yuklenemedi.') : null}
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
          {mode === 'curriculum' && (
            <CurriculumMode
              isLoading={curriculumQuery.isLoading}
              errorMessage={curriculumQuery.isError ? getAcademicOperationsErrorMessage(curriculumQuery.error, 'Mufredat gorunumu yuklenemedi.') : null}
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
  departmentName,
  isLoading,
  errorMessage,
  summary,
  searchTerm,
  onSearchTermChange,
}: {
  departmentName: string;
  isLoading: boolean;
  errorMessage: string | null;
  summary: ReturnType<typeof useAcademicPlanningSummary>['data'] | null;
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
        <AlertTitle>Planlama ekrani acilamadi</AlertTitle>
        <AlertDescription>{errorMessage}</AlertDescription>
      </Alert>
    );
  }

  if (!summary) {
    return (
      <Card>
        <CardContent className="py-16 text-center text-muted-foreground">
          {departmentName} icin planlama ozeti bulunamadi.
        </CardContent>
      </Card>
    );
  }

  const forecastCourses = (summary.forecastSummary?.courseSummaries ?? []).filter(course => {
    if (!searchTerm.trim()) return true;
    const normalizedSearch = searchTerm.toLowerCase();
    return `${course.courseCode} ${course.courseName}`.toLowerCase().includes(normalizedSearch);
  });

  return (
    <div className="space-y-6">
      <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-4 gap-4">
        <MetricCard label="Tahmini Talep" value={String(summary.forecastSummary?.totalPredictedDemand ?? 0)} icon={Waypoints} />
        <MetricCard label="Onerilen Sube" value={String(summary.forecastSummary?.totalRecommendedSections ?? 0)} icon={GraduationCap} />
        <MetricCard label="Toplam Conflict" value={String(summary.scheduleValidation.conflicts.length)} icon={AlertCircle} />
        <MetricCard
          label="Program Gecerliligi"
          value={summary.scheduleValidation.isValid ? 'Gecerli' : 'Duzeltme Gerekli'}
          icon={summary.scheduleValidation.isValid ? CheckCircle2 : AlertCircle}
        />
      </div>

      {summary.notes.length > 0 && (
        <Alert>
          <AlertCircle className="h-4 w-4" />
          <AlertTitle>Planlama notlari</AlertTitle>
          <AlertDescription className="space-y-1">
            {summary.notes.map(note => <p key={note}>{note}</p>)}
          </AlertDescription>
        </Alert>
      )}

      {summary.scheduleValidation.notes.length > 0 && (
        <Alert>
          <AlertCircle className="h-4 w-4" />
          <AlertTitle>Schedule validasyon notlari</AlertTitle>
          <AlertDescription className="space-y-1">
            {summary.scheduleValidation.notes.map(note => <p key={note}>{note}</p>)}
          </AlertDescription>
        </Alert>
      )}

      <Card>
        <CardHeader className="flex flex-col lg:flex-row lg:items-center lg:justify-between gap-4">
          <div>
            <CardTitle>Talep ve Sube Onerisi</CardTitle>
            <CardDescription>FF/tekrar, ustten alma ve kapasite bazli section ihtiyaci tek listede.</CardDescription>
          </div>
          <div className="relative w-full lg:w-80">
            <Search className="h-4 w-4 text-muted-foreground absolute left-3 top-1/2 -translate-y-1/2" />
            <Input
              value={searchTerm}
              onChange={event => onSearchTermChange(event.target.value)}
              placeholder="Ders kodu veya adina gore filtrele"
              className="pl-9"
            />
          </div>
        </CardHeader>
        <CardContent>
          {forecastCourses.length === 0 ? (
            <p className="text-sm text-muted-foreground py-8 text-center">Filtreyle eslesen tahminleme kaydi bulunamadi.</p>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Ders</TableHead>
                  <TableHead>Ilk Talep</TableHead>
                  <TableHead>Tekrar</TableHead>
                  <TableHead>Ustten</TableHead>
                  <TableHead>Toplam</TableHead>
                  <TableHead>Onerilen Sube</TableHead>
                  <TableHead>Kapasite Bazisi</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {forecastCourses.map(course => (
                  <ForecastRow key={course.courseId} course={course} />
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Conflict Analizi</CardTitle>
          <CardDescription>Mevcut aktif section schedule’lari bu bolum kapsami icinde dogrulandi.</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          {summary.scheduleValidation.conflicts.length === 0 ? (
            <Alert>
              <CheckCircle2 className="h-4 w-4" />
              <AlertTitle>Conflict bulunmadi</AlertTitle>
              <AlertDescription>Bolum schedule’lari mevcut kurallar altinda gecerli gorunuyor.</AlertDescription>
            </Alert>
          ) : (
            <div className="space-y-3">
              {summary.scheduleValidation.conflicts.map(conflict => (
                <div key={`${conflict.sectionId}-${conflict.conflictType}-${conflict.relatedSectionId ?? 'self'}-${conflict.day ?? 'na'}-${conflict.startTime ?? 'na'}`} className="rounded-xl border border-border/70 p-4">
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
                      <p>Section: {conflict.sectionId}{conflict.relatedSectionId ? ` / Ilgili: ${conflict.relatedSectionId}` : ''}</p>
                      <p>{formatConflictTime(conflict.day, conflict.startTime, conflict.endTime)}</p>
                      <p>{conflict.classroomName || 'Derslik yok'} • {conflict.instructorName || 'Ogretim uyesi yok'}</p>
                    </div>
                  </div>
                </div>
              ))}
            </div>
          )}
        </CardContent>
      </Card>
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
        <AlertTitle>Sinif listeleri yuklenemedi</AlertTitle>
        <AlertDescription>{errorMessage}</AlertDescription>
      </Alert>
    );
  }

  if (!classLists) {
    return (
      <Card>
        <CardContent className="py-16 text-center text-muted-foreground">
          Gosterilecek sinif listesi bulunamadi.
        </CardContent>
      </Card>
    );
  }

  const filteredSections = classLists.sections.filter(section => {
    if (!searchTerm.trim()) return true;
    const normalizedSearch = searchTerm.toLowerCase();
    const studentMatch = section.students.some(student =>
      `${student.studentNumber} ${student.studentName}`.toLowerCase().includes(normalizedSearch));

    return studentMatch || `${section.courseCode} ${section.courseName} ${section.instructorName} ${section.classroomName}`.toLowerCase().includes(normalizedSearch);
  });

  return (
    <div className="space-y-6">
      <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
        <MetricCard label="Toplam Section" value={String(classLists.totalSections)} icon={BookCopy} />
        <MetricCard label="Toplam Ogrenci" value={String(classLists.totalStudents)} icon={Users} />
        <MetricCard label="Donem" value={classLists.termName} icon={Waypoints} />
      </div>

      {classLists.notes.length > 0 && (
        <Alert>
          <AlertCircle className="h-4 w-4" />
          <AlertTitle>Sinif listesi notlari</AlertTitle>
          <AlertDescription className="space-y-1">
            {classLists.notes.map(note => <p key={note}>{note}</p>)}
          </AlertDescription>
        </Alert>
      )}

      <Card>
        <CardHeader className="flex flex-col lg:flex-row lg:items-center lg:justify-between gap-4">
          <div>
            <CardTitle>Section Arama</CardTitle>
            <CardDescription>Ders, ogretim uyesi, derslik ya da ogrenciye gore filtreleyin.</CardDescription>
          </div>
          <div className="relative w-full lg:w-80">
            <Search className="h-4 w-4 text-muted-foreground absolute left-3 top-1/2 -translate-y-1/2" />
            <Input
              value={searchTerm}
              onChange={event => onSearchTermChange(event.target.value)}
              placeholder="Ders veya ogrenci ara"
              className="pl-9"
            />
          </div>
        </CardHeader>
      </Card>

      {filteredSections.length === 0 ? (
        <Card>
          <CardContent className="py-16 text-center text-muted-foreground">
            Filtreye uyan section bulunamadi.
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
                      {section.instructorName || 'Ogretim uyesi atanamamis'} • {section.classroomName || 'Derslik atanamamis'} • {section.scheduleSummary || 'Program tanimlanmamis'}
                    </p>
                  </div>
                  <div className="flex items-center gap-3 text-sm">
                    <Badge variant="outline">{section.activeStudentCount}/{section.capacity}</Badge>
                    <span className="text-muted-foreground">Section #{section.sectionId}</span>
                  </div>
                </div>
              </button>

              {expandedSectionId === section.sectionId && (
                <CardContent className="border-t border-border/60 py-4">
                  {section.students.length === 0 ? (
                    <p className="text-sm text-muted-foreground">Bu sectionda aktif ogrenci bulunmuyor.</p>
                  ) : (
                    <Table>
                      <TableHeader>
                        <TableRow>
                          <TableHead>Ogrenci No</TableHead>
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
        <AlertTitle>Mufredat gorunumu yuklenemedi</AlertTitle>
        <AlertDescription>{errorMessage}</AlertDescription>
      </Alert>
    );
  }

  if (!overview) {
    return (
      <Card>
        <CardContent className="py-16 text-center text-muted-foreground">
          Gosterilecek mufredat ozeti bulunamadi.
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
        <MetricCard label="Aktif Mufredat" value={String(overview.activeCurriculumCount)} icon={CheckCircle2} />
        <MetricCard label="Toplam Ders" value={String(overview.totalCourses)} icon={BookCopy} />
        <MetricCard label="Toplam AKTS" value={String(overview.totalEcts)} icon={Waypoints} />
      </div>

      {overview.notes.length > 0 && (
        <Alert>
          <AlertCircle className="h-4 w-4" />
          <AlertTitle>Mufredat notlari</AlertTitle>
          <AlertDescription className="space-y-1">
            {overview.notes.map(note => <p key={note}>{note}</p>)}
          </AlertDescription>
        </Alert>
      )}

      <Card>
        <CardHeader className="flex flex-col lg:flex-row lg:items-center lg:justify-between gap-4">
          <div>
            <CardTitle>Program Filtreleme</CardTitle>
            <CardDescription>Kod, program adi veya versiyon ile arama yapin.</CardDescription>
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
            Filtreye uyan mufredat bulunamadi.
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

function ForecastRow({ course }: { course: CourseDemandForecast }) {
  return (
    <TableRow>
      <TableCell>
        <div>
          <div className="font-mono text-xs font-semibold text-primary">{course.courseCode}</div>
          <div className="text-sm text-foreground">{course.courseName}</div>
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
      <TableCell className="max-w-[18rem] text-sm text-muted-foreground">{course.capacityBasis}</TableCell>
    </TableRow>
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
              {program.isActiveCurriculum ? 'Aktif Mufredat' : 'Arsiv'}
            </Badge>
          </CardTitle>
          <CardDescription className="mt-1">
            Versiyon {program.curriculumVersion} • {program.totalCourses} ders • {program.totalEcts} AKTS
          </CardDescription>
        </div>
        <div className="flex flex-wrap gap-2 text-sm">
          <Badge variant="outline">Zorunlu: {program.compulsoryCourseCount}</Badge>
          <Badge variant="outline">Secmeli: {program.electiveCourseCount}</Badge>
          <Badge variant="outline">Kredi: {program.totalCredits}</Badge>
        </div>
      </CardHeader>
      <CardContent>
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Semester</TableHead>
              <TableHead>Ders</TableHead>
              <TableHead>Kredi</TableHead>
              <TableHead>AKTS</TableHead>
              <TableHead>Zorunlu</TableHead>
              <TableHead>Secmeli</TableHead>
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
  2: 'Sali',
  3: 'Carsamba',
  4: 'Persembe',
  5: 'Cuma',
  6: 'Cumartesi',
};
