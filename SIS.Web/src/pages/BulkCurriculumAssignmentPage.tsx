import { useEffect, useMemo, useState } from 'react';
import { AlertCircle, ChevronLeft, ChevronRight, ListChecks, Loader2, Search, ShieldAlert, Users } from 'lucide-react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Skeleton } from '@/components/ui/skeleton';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import BulkOperationResultPanel from '@/components/academic-operations/BulkOperationResultPanel';
import { getAcademicOperationsErrorMessage } from '../api/academicOperations.api';
import {
  useBulkCurriculumAssignment,
  useDepartmentCurriculumOptions,
  useDepartmentStudents,
} from '../hooks/useAcademicOperations';
import { useAdminDepartments, useAdminPrograms, useFacultyLookup } from '../hooks/useAdminLookup';
import { usePermissions } from '../hooks/usePermissions';
import type {
  BulkAcademicOperationResult,
  DepartmentCurriculumOption,
  DepartmentStudentListItem,
} from '../types/academicOperations';

const PAGE_SIZE = 50;

const STUDENT_STATUS_LABELS: Record<string, string> = {
  Active: 'Aktif',
  Frozen: 'Dondurulmuş',
  Suspended: 'Uzaklaştırılmış',
  Passive: 'Pasif',
  Dismissed: 'İlişiği Kesilmiş',
  Graduated: 'Mezun',
};

const STUDENT_STATUS_OPTIONS = [
  { value: 'all', label: 'Tüm öğrenim durumları' },
  ...Object.entries(STUDENT_STATUS_LABELS).map(([value, label]) => ({ value, label })),
];

const CLASS_OPTIONS = [
  { value: 'all', label: 'Tüm sınıflar' },
  { value: '1', label: '1. sınıf' },
  { value: '2', label: '2. sınıf' },
  { value: '3', label: '3. sınıf' },
  { value: '4', label: '4. sınıf' },
];

const FAILING_OPTIONS = [
  { value: 'all', label: 'Tüm ders başarı durumları' },
  { value: 'with', label: 'Başarısız (FF/NA) dersi olan' },
  { value: 'without', label: 'Başarısız dersi olmayan' },
];

const HEDEF_OPTIONS = [
  { value: 'active', label: 'Aktif Öğrenciler' },
  { value: 'all', label: 'Tüm Öğrenciler' },
];

export default function BulkCurriculumAssignmentPage() {
  const { hasPermission } = usePermissions();
  const canAssignCurriculum = hasPermission('menu.toplu-mufredat-atama.assign');

  // Cascade: Fakülte → Bölüm → Program
  const facultiesQuery = useFacultyLookup();
  const [facultyId, setFacultyId] = useState<number | null>(null);
  const departmentsQuery = useAdminDepartments(facultyId ?? undefined);
  const [departmentId, setDepartmentId] = useState<number | null>(null);
  const programsQuery = useAdminPrograms(departmentId ?? undefined);

  // Filtreler
  const [searchInput, setSearchInput] = useState('');
  const [search, setSearch] = useState('');
  const [statusFilter, setStatusFilter] = useState('all');
  const [classFilter, setClassFilter] = useState('all');
  const [programFilter, setProgramFilter] = useState('all');
  const [currentCurriculumFilter, setCurrentCurriculumFilter] = useState('all');
  const [failingFilter, setFailingFilter] = useState('all');
  const [hedef, setHedef] = useState('active');
  const [studentNoFrom, setStudentNoFrom] = useState('');
  const [studentNoTo, setStudentNoTo] = useState('');
  const [enrollmentFrom, setEnrollmentFrom] = useState('');
  const [enrollmentTo, setEnrollmentTo] = useState('');
  const [page, setPage] = useState(1);

  // Atama
  const [targetCurriculumId, setTargetCurriculumId] = useState('none');
  const [selectedStudentIds, setSelectedStudentIds] = useState<Set<number>>(() => new Set());
  const [lastResult, setLastResult] = useState<BulkAcademicOperationResult | null>(null);

  const faculties = useMemo(() => facultiesQuery.data ?? [], [facultiesQuery.data]);
  const departments = useMemo(() => departmentsQuery.data ?? [], [departmentsQuery.data]);
  const programs = useMemo(() => programsQuery.data ?? [], [programsQuery.data]);

  // Varsayılan fakülte / bölüm seçimi
  useEffect(() => {
    if (facultyId === null && faculties.length > 0) {
      // eslint-disable-next-line react-hooks/set-state-in-effect
      setFacultyId(faculties[0].id);
    }
  }, [faculties, facultyId]);

  useEffect(() => {
    if (departments.length === 0) return;
    if (!departments.some(department => department.id === departmentId)) {
      // eslint-disable-next-line react-hooks/set-state-in-effect
      setDepartmentId(departments[0].id);
    }
  }, [departments, departmentId]);

  // Arama debounce (server-side)
  useEffect(() => {
    const handle = setTimeout(() => setSearch(searchInput.trim()), 350);
    return () => clearTimeout(handle);
  }, [searchInput]);

  // Filtre değişince ilk sayfaya dön
  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setPage(1);
  }, [search, statusFilter, classFilter, programFilter, currentCurriculumFilter, failingFilter, hedef, studentNoFrom, studentNoTo, enrollmentFrom, enrollmentTo, departmentId]);

  // Bölüm değişince seçim / hedef / filtreler sıfırlanır
  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setProgramFilter('all');
    setCurrentCurriculumFilter('all');
    setStatusFilter('all');
    setClassFilter('all');
    setFailingFilter('all');
    setHedef('active');
    setStudentNoFrom('');
    setStudentNoTo('');
    setEnrollmentFrom('');
    setEnrollmentTo('');
    setTargetCurriculumId('none');
    setSelectedStudentIds(new Set());
    setLastResult(null);
  }, [departmentId]);

  const studentsQuery = useDepartmentStudents(
    departmentId,
    {
      search: search || undefined,
      // Öğrenim Durumu seçiliyse o; değilse "Hedef" popülasyonu (Aktif Öğrenciler → Active).
      status: statusFilter !== 'all' ? statusFilter : hedef === 'active' ? 'Active' : undefined,
      programId: programFilter !== 'all' ? Number(programFilter) : undefined,
      curriculumId: currentCurriculumFilter !== 'all' ? Number(currentCurriculumFilter) : undefined,
      hasFailingCourse: failingFilter === 'all' ? undefined : failingFilter === 'with',
      classYear: classFilter !== 'all' ? Number(classFilter) : undefined,
      studentNoFrom: studentNoFrom.trim() || undefined,
      studentNoTo: studentNoTo.trim() || undefined,
      enrollmentFrom: enrollmentFrom || undefined,
      enrollmentTo: enrollmentTo || undefined,
      page,
      pageSize: PAGE_SIZE,
    },
    departmentId !== null,
  );
  const curriculaQuery = useDepartmentCurriculumOptions(departmentId, departmentId !== null);
  const assignmentMutation = useBulkCurriculumAssignment(departmentId);

  const students = studentsQuery.data?.students ?? [];
  const totalStudents = studentsQuery.data?.totalStudents ?? 0;
  const pageCount = Math.max(1, Math.ceil(totalStudents / PAGE_SIZE));
  const curricula = useMemo(() => curriculaQuery.data ?? [], [curriculaQuery.data]);

  const targetCurriculum = targetCurriculumId === 'none'
    ? null
    : curricula.find(curriculum => curriculum.curriculumId === Number(targetCurriculumId)) ?? null;

  // Hedef listesinde tüm müfredatlar (statü kısıtı yok — Proliz paritesi).
  const targetCurriculumOptions = curricula;

  const currentCurriculumOptions = useMemo(
    () => curricula.map(curriculum => ({
      value: String(curriculum.curriculumId),
      label: `${curriculum.programCode} - ${curriculum.curriculumName} (${curriculum.curriculumVersion})`,
    })),
    [curricula],
  );

  const visiblePageAllSelected = students.length > 0
    && students.every(student => selectedStudentIds.has(student.studentId));

  function toggleStudent(studentId: number) {
    if (!canAssignCurriculum) return;
    setSelectedStudentIds(previous => {
      const next = new Set(previous);
      if (next.has(studentId)) next.delete(studentId);
      else next.add(studentId);
      return next;
    });
  }

  function togglePageSelection() {
    if (!canAssignCurriculum) return;
    setSelectedStudentIds(previous => {
      const next = new Set(previous);
      if (visiblePageAllSelected) students.forEach(student => next.delete(student.studentId));
      else students.forEach(student => next.add(student.studentId));
      return next;
    });
  }

  function runAssignment() {
    if (!canAssignCurriculum || departmentId === null || targetCurriculum === null || selectedStudentIds.size === 0) return;

    assignmentMutation.mutate(
      {
        departmentId,
        curriculumId: targetCurriculum.curriculumId,
        studentIds: [...selectedStudentIds],
      },
      {
        onSuccess: result => {
          setLastResult(result);
          const failedIds = result.results.filter(item => !item.success).map(item => item.studentId);
          setSelectedStudentIds(new Set(failedIds));
        },
      },
    );
  }

  const loadError = studentsQuery.isError || curriculaQuery.isError || facultiesQuery.isError;
  const mutationError = assignmentMutation.isError
    ? getAcademicOperationsErrorMessage(assignmentMutation.error, 'Müfredat ataması tamamlanamadı.')
    : null;

  return (
    <div className="flex flex-1 flex-col gap-3">
      {(loadError || mutationError) && (
        <Alert variant="destructive">
          <AlertCircle className="h-4 w-4" />
          <AlertDescription>
            {mutationError
              ?? getAcademicOperationsErrorMessage(
                studentsQuery.error ?? curriculaQuery.error ?? facultiesQuery.error,
                'Toplu müfredat atama verisi yüklenemedi.',
              )}
          </AlertDescription>
        </Alert>
      )}

      {!canAssignCurriculum && (
        <Alert variant="warning">
          <ShieldAlert className="h-4 w-4" />
          <AlertDescription>
            Bu ekranda öğrencilerin müfredat durumunu inceleyebilirsiniz. Toplu müfredat ataması için işlem yetkisi gerekir.
          </AlertDescription>
        </Alert>
      )}

      {/* Tek bütün panel — Proliz tarzı */}
      <Card className="flex flex-1 flex-col overflow-hidden">
        {/* Filtreler */}
        <div className="border-b bg-muted/30 p-3">
          <div className="grid gap-2 md:grid-cols-3 xl:grid-cols-4">
            <FilterSelect
              label="Fakülte"
              value={facultyId !== null ? String(facultyId) : ''}
              onValueChange={value => setFacultyId(Number(value))}
              placeholder={facultiesQuery.isLoading ? 'Yükleniyor...' : 'Fakülte seçin'}
              options={faculties.map(faculty => ({ value: String(faculty.id), label: `${faculty.code} - ${faculty.name}` }))}
            />
            <FilterSelect
              label="Bölüm"
              value={departmentId !== null ? String(departmentId) : ''}
              onValueChange={value => setDepartmentId(Number(value))}
              placeholder={departmentsQuery.isLoading ? 'Yükleniyor...' : 'Bölüm seçin'}
              options={departments.map(department => ({ value: String(department.id), label: `${department.code} - ${department.name}` }))}
            />
            <FilterSelect
              label="Program"
              value={programFilter}
              onValueChange={setProgramFilter}
              options={[
                { value: 'all', label: 'Tüm programlar' },
                ...programs.map(program => ({ value: String(program.id), label: `${program.code} - ${program.name}` })),
              ]}
            />
            <FilterSelect
              label="Müfredat"
              value={currentCurriculumFilter}
              onValueChange={setCurrentCurriculumFilter}
              options={[{ value: 'all', label: 'Tüm müfredatlar' }, ...currentCurriculumOptions]}
            />
            <DisabledFilter label="Kayıt nedeni" placeholder="Veri yok" />
            <FilterSelect label="Öğrenim durumu" value={statusFilter} onValueChange={setStatusFilter} options={STUDENT_STATUS_OPTIONS} />
            <FilterSelect label="Sınıf" value={classFilter} onValueChange={setClassFilter} options={CLASS_OPTIONS} />
            <RangeFilter
              label="Kayıt tarih aralığı"
              type="date"
              from={enrollmentFrom}
              to={enrollmentTo}
              onFrom={setEnrollmentFrom}
              onTo={setEnrollmentTo}
            />
            <RangeFilter
              label="Öğrenci no aralığı"
              from={studentNoFrom}
              to={studentNoTo}
              onFrom={setStudentNoFrom}
              onTo={setStudentNoTo}
              placeholderFrom="Baş."
              placeholderTo="Bit."
            />
            <FilterSelect label="Ders başarı durumu" value={failingFilter} onValueChange={setFailingFilter} options={FAILING_OPTIONS} />
            <FilterSelect label="Hedef" value={hedef} onValueChange={setHedef} options={HEDEF_OPTIONS} />
            <div className="space-y-1">
              <Label className="text-xs">Arama</Label>
              <div className="relative">
                <Search className="pointer-events-none absolute left-2.5 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
                <Input
                  value={searchInput}
                  onChange={event => setSearchInput(event.target.value)}
                  placeholder="Öğrenci no veya ad ara..."
                  className="h-8 pl-8 text-xs"
                />
              </div>
            </div>
          </div>
        </div>

        {/* Öğrenci başlık şeridi */}
        <div className="flex flex-wrap items-center justify-between gap-3 border-b px-3 py-2">
          <span className="text-sm font-semibold">Öğrenciler</span>
          <div className="flex flex-wrap items-center gap-2 text-xs">
            <Badge variant="outline">{totalStudents} kayıt</Badge>
            <Badge variant="secondary">{selectedStudentIds.size} seçili</Badge>
          </div>
        </div>

        <div className="flex-1 overflow-auto">
          {departmentId === null ? (
            <EmptyState message="Önce fakülte ve bölüm seçin." />
          ) : studentsQuery.isLoading ? (
            <div className="p-3">
              <Skeleton className="h-[360px] rounded-lg" />
            </div>
          ) : students.length === 0 ? (
            <EmptyState message="Filtreye uygun öğrenci bulunamadı." hint="Arama veya filtre kriterlerini değiştirin." />
          ) : (
            <Table className="[&_td]:py-2 [&_th]:h-9">
              <TableHeader>
                <TableRow>
                  <TableHead className="w-[44px]">
                    <input
                      type="checkbox"
                      className="h-4 w-4 rounded border-input"
                      checked={visiblePageAllSelected}
                      disabled={!canAssignCurriculum}
                      onChange={togglePageSelection}
                      aria-label="Bu sayfadaki öğrencileri seç"
                    />
                  </TableHead>
                  <TableHead className="w-[96px]">Öğrenci No</TableHead>
                  <TableHead className="w-[200px]">Ad Soyad</TableHead>
                  <TableHead className="w-[180px]">Fakülte</TableHead>
                  <TableHead>Program</TableHead>
                  <TableHead className="w-[72px] text-center">Sınıf</TableHead>
                  <TableHead className="w-[96px]">K.Tarihi</TableHead>
                  <TableHead className="w-[150px]">Bağlı Müfredat</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {students.map(student => (
                  <TableRow
                    key={student.studentId}
                    data-state={selectedStudentIds.has(student.studentId) ? 'selected' : undefined}
                  >
                    <TableCell>
                      <input
                        type="checkbox"
                        className="h-4 w-4 rounded border-input"
                        checked={selectedStudentIds.has(student.studentId)}
                        disabled={!canAssignCurriculum}
                        onChange={() => toggleStudent(student.studentId)}
                        aria-label={`${student.studentName} seç`}
                      />
                    </TableCell>
                    <TableCell className="font-mono text-xs">{student.studentNumber}</TableCell>
                    <TableCell>
                      <div className="font-medium">{student.studentName}</div>
                      <StatusBadge status={student.status} />
                    </TableCell>
                    <TableCell className="text-xs text-muted-foreground">{student.facultyName}</TableCell>
                    <TableCell className="text-xs">{student.programCode} - {student.programName}</TableCell>
                    <TableCell className="text-center text-xs">{getStudentClassYear(student)}. sınıf</TableCell>
                    <TableCell className="text-xs text-muted-foreground">{formatDate(student.enrollmentDate)}</TableCell>
                    <TableCell className="text-xs">{student.curriculumName} ({student.curriculumVersion})</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </div>

        {/* Sayfalama */}
        {departmentId !== null && totalStudents > 0 && (
          <div className="flex items-center justify-between border-t px-3 py-2 text-xs text-muted-foreground">
            <span>Toplam {totalStudents} öğrenci · Sayfa {page} / {pageCount}</span>
            <div className="flex items-center gap-2">
              <Button
                type="button"
                variant="outline"
                size="sm"
                className="h-8"
                onClick={() => setPage(current => Math.max(1, current - 1))}
                disabled={page <= 1 || studentsQuery.isFetching}
              >
                <ChevronLeft className="h-4 w-4" /> Önceki
              </Button>
              <Button
                type="button"
                variant="outline"
                size="sm"
                className="h-8"
                onClick={() => setPage(current => Math.min(pageCount, current + 1))}
                disabled={page >= pageCount || studentsQuery.isFetching}
              >
                Sonraki <ChevronRight className="h-4 w-4" />
              </Button>
            </div>
          </div>
        )}

        {/* Atama çubuğu (altta — Proliz düzeni) */}
        <div className="flex items-center gap-2 border-t bg-muted/30 px-3 py-2">
          <Label className="shrink-0 text-xs text-muted-foreground">Atanacak Müfredat</Label>
          <Select value={targetCurriculumId} onValueChange={setTargetCurriculumId} disabled={!canAssignCurriculum}>
            <SelectTrigger className="h-8 flex-1 text-xs">
              <SelectValue placeholder="Hedef müfredat seçin" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="none">Hedef müfredat seçin</SelectItem>
              {targetCurriculumOptions.map(curriculum => (
                <SelectItem key={curriculum.curriculumId} value={String(curriculum.curriculumId)}>
                  {formatCurriculumOption(curriculum)}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
          <Button
            size="sm"
            className="h-8 shrink-0"
            onClick={runAssignment}
            disabled={!canAssignCurriculum || assignmentMutation.isPending || targetCurriculum === null || selectedStudentIds.size === 0}
          >
            {assignmentMutation.isPending ? <Loader2 className="h-4 w-4 animate-spin" /> : <ListChecks className="h-4 w-4" />}
            {!canAssignCurriculum
              ? 'Atama Yetkisi Yok'
              : assignmentMutation.isPending
                ? 'Atanıyor'
                : `Müfredat Ata (${selectedStudentIds.size})`}
          </Button>
        </div>
      </Card>

      <BulkOperationResultPanel result={lastResult} title="Son Müfredat Atama Sonucu" />
    </div>
  );
}

function FilterSelect({
  label,
  value,
  onValueChange,
  options,
  placeholder,
  disabled = false,
}: {
  label: string;
  value: string;
  onValueChange: (value: string) => void;
  options: { value: string; label: string }[];
  placeholder?: string;
  disabled?: boolean;
}) {
  return (
    <div className="space-y-1">
      <Label className="text-xs">{label}</Label>
      <Select value={value || undefined} onValueChange={onValueChange} disabled={disabled}>
        <SelectTrigger className="h-8 text-xs">
          <SelectValue placeholder={placeholder} />
        </SelectTrigger>
        <SelectContent>
          {options.map(option => (
            <SelectItem key={option.value} value={option.value}>
              {option.label}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
    </div>
  );
}

function DisabledFilter({ label, placeholder }: { label: string; placeholder: string }) {
  return (
    <div className="space-y-1">
      <Label className="text-xs text-muted-foreground">{label}</Label>
      <div className="flex h-8 items-center rounded-md border border-input bg-muted/40 px-2.5 text-xs text-muted-foreground">
        {placeholder}
      </div>
    </div>
  );
}

function RangeFilter({
  label,
  from,
  to,
  onFrom,
  onTo,
  type = 'text',
  placeholderFrom,
  placeholderTo,
}: {
  label: string;
  from: string;
  to: string;
  onFrom: (value: string) => void;
  onTo: (value: string) => void;
  type?: string;
  placeholderFrom?: string;
  placeholderTo?: string;
}) {
  return (
    <div className="space-y-1">
      <Label className="text-xs">{label}</Label>
      <div className="flex items-center gap-1">
        <Input type={type} value={from} onChange={event => onFrom(event.target.value)} placeholder={placeholderFrom} className="h-8 text-xs" />
        <span className="text-muted-foreground">–</span>
        <Input type={type} value={to} onChange={event => onTo(event.target.value)} placeholder={placeholderTo} className="h-8 text-xs" />
      </div>
    </div>
  );
}

function EmptyState({ message, hint }: { message: string; hint?: string }) {
  return (
    <div className="flex flex-col items-center justify-center py-16 text-center">
      <Users className="mb-3 h-9 w-9 text-muted-foreground/45" />
      <p className="text-sm font-medium">{message}</p>
      {hint && <p className="mt-1 text-sm text-muted-foreground">{hint}</p>}
    </div>
  );
}

function getStudentClassYear(student: DepartmentStudentListItem) {
  return Math.min(4, Math.max(1, Math.floor(student.completedSemesters / 2) + 1));
}

function formatDate(iso: string) {
  if (!iso) return '—';
  const date = new Date(iso);
  return Number.isNaN(date.getTime()) ? '—' : date.toLocaleDateString('tr-TR');
}

function normalizeCurriculumStatus(status: number | string) {
  const normalized = String(status);
  if (normalized === '1' || normalized === 'Published') return 'published';
  if (normalized === '2' || normalized === 'Archived') return 'archived';
  return 'draft';
}

function curriculumStatusLabel(status: number | string) {
  const normalized = normalizeCurriculumStatus(status);
  return normalized === 'published' ? 'Yayında' : normalized === 'archived' ? 'Arşiv' : 'Taslak';
}

function StatusBadge({ status }: { status: string }) {
  const variant: 'success' | 'info' | 'secondary' = status === 'Active'
    ? 'success'
    : status === 'Graduated'
      ? 'info'
      : 'secondary';
  return <Badge variant={variant} className="mt-0.5 text-[10px]">{STUDENT_STATUS_LABELS[status] ?? status}</Badge>;
}

function formatCurriculumOption(curriculum: DepartmentCurriculumOption) {
  const activeLabel = curriculum.isActive ? ' - Aktif' : '';
  return `${curriculum.programCode} - ${curriculum.curriculumName} (${curriculum.curriculumVersion}) - ${curriculumStatusLabel(curriculum.status)}${activeLabel}`;
}
