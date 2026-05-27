import { useEffect, useMemo, useState, type ReactNode } from 'react';
import { AlertCircle, Search, ShieldAlert, UserCheck, Users, X } from 'lucide-react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Skeleton } from '@/components/ui/skeleton';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { TablePagination } from '@/components/ui/table-pagination';
import BulkOperationResultPanel from '@/components/academic-operations/BulkOperationResultPanel';
import DepartmentOperationsLayout from '@/components/academic-operations/DepartmentOperationsLayout';
import { resolveInitialDepartmentId } from '@/components/academic-operations/utils';
import { getAcademicOperationsErrorMessage } from '../api/academicOperations.api';
import {
  useAcademicOperationsContext,
  useBulkAdvisorAssignment,
  useDepartmentCurriculumOptions,
  useDepartmentStudents,
} from '../hooks/useAcademicOperations';
import { useDepartmentInstructors } from '../hooks/useCourseOpening';
import { usePermissions } from '../hooks/usePermissions';
import type { BulkAcademicOperationResult, DepartmentStudentListItem } from '../types/academicOperations';
import type { InstructorOptionDto } from '../types/courseOpening';

const DEFAULT_PAGE_SIZE = 25;

const STUDENT_STATUS_LABELS: Record<string, string> = {
  Active: 'Aktif',
  Frozen: 'Dondurulmuş',
  Suspended: 'Uzaklaştırılmış',
  Passive: 'Pasif',
  Dismissed: 'İlişiği Kesilmiş',
  Graduated: 'Mezun',
};

const STUDENT_STATUS_OPTIONS = [
  { value: 'all', label: 'Tüm öğrenci durumları' },
  ...Object.entries(STUDENT_STATUS_LABELS).map(([value, label]) => ({ value, label })),
];

const CLASS_OPTIONS = [
  { value: 'all', label: 'Tüm sınıflar' },
  { value: '1', label: '1. sınıf' },
  { value: '2', label: '2. sınıf' },
  { value: '3', label: '3. sınıf' },
  { value: '4', label: '4. sınıf' },
];

const ADVISOR_FILTER_OPTIONS = [
  { value: 'all', label: 'Tüm öğrenciler' },
  { value: 'with', label: 'Danışmanlı' },
  { value: 'none', label: 'Danışmansız' },
];

const TABLE_COLUMN_COUNT = 7;
const EMPTY_STUDENTS: DepartmentStudentListItem[] = [];
const EMPTY_INSTRUCTORS: InstructorOptionDto[] = [];

/** Sayfalar arası seçim için tutulan minimal öğrenci bilgisi. */
interface SelectedStudent {
  studentId: number;
  studentNumber: string;
  studentName: string;
}

export default function BulkAdvisorAssignmentPage() {
  const contextQuery = useAcademicOperationsContext();
  const { hasPermission } = usePermissions();
  const canAssignAdvisors = hasPermission('menu.toplu-danisman-atama.assign');

  const [departmentId, setDepartmentId] = useState<number | null>(null);
  const [searchInput, setSearchInput] = useState('');
  const [search, setSearch] = useState('');
  const [statusFilter, setStatusFilter] = useState('all');
  const [programFilter, setProgramFilter] = useState('all');
  const [classFilter, setClassFilter] = useState('all');
  const [curriculumFilter, setCurriculumFilter] = useState('all');
  const [advisorFilter, setAdvisorFilter] = useState('all');
  const [studentNoFrom, setStudentNoFrom] = useState('');
  const [studentNoTo, setStudentNoTo] = useState('');
  const [enrollmentFrom, setEnrollmentFrom] = useState('');
  const [enrollmentTo, setEnrollmentTo] = useState('');
  const [selectedStudents, setSelectedStudents] = useState<Map<number, SelectedStudent>>(() => new Map());
  const [assignOpen, setAssignOpen] = useState(false);
  const [lastResult, setLastResult] = useState<BulkAcademicOperationResult | null>(null);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(DEFAULT_PAGE_SIZE);

  useEffect(() => {
    if (departmentId === null && contextQuery.data) {
      // eslint-disable-next-line react-hooks/set-state-in-effect
      setDepartmentId(resolveInitialDepartmentId(contextQuery.data));
    }
  }, [contextQuery.data, departmentId]);

  // Bölüm değişince filtre/seçim/sonuç sıfırlanır.
  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setSearchInput('');
    setSearch('');
    setStatusFilter('all');
    setProgramFilter('all');
    setClassFilter('all');
    setCurriculumFilter('all');
    setAdvisorFilter('all');
    setStudentNoFrom('');
    setStudentNoTo('');
    setEnrollmentFrom('');
    setEnrollmentTo('');
    setSelectedStudents(new Map());
    setLastResult(null);
    setPage(1);
  }, [departmentId]);

  // Arama debounce (server-side).
  useEffect(() => {
    const handle = setTimeout(() => setSearch(searchInput.trim()), 350);
    return () => clearTimeout(handle);
  }, [searchInput]);

  // Filtre değişince ilk sayfaya dön.
  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setPage(1);
  }, [search, statusFilter, programFilter, classFilter, curriculumFilter, advisorFilter, studentNoFrom, studentNoTo, enrollmentFrom, enrollmentTo]);

  const hasAdvisorParam = advisorFilter === 'with' ? true : advisorFilter === 'none' ? false : undefined;

  const studentsQuery = useDepartmentStudents(
    departmentId,
    {
      search: search || undefined,
      status: statusFilter !== 'all' ? statusFilter : undefined,
      programId: programFilter !== 'all' ? Number(programFilter) : undefined,
      curriculumId: curriculumFilter !== 'all' ? Number(curriculumFilter) : undefined,
      classYear: classFilter !== 'all' ? Number(classFilter) : undefined,
      hasAdvisor: hasAdvisorParam,
      studentNoFrom: studentNoFrom.trim() || undefined,
      studentNoTo: studentNoTo.trim() || undefined,
      enrollmentFrom: enrollmentFrom || undefined,
      enrollmentTo: enrollmentTo || undefined,
      page,
      pageSize,
    },
    departmentId !== null,
  );
  const curriculaQuery = useDepartmentCurriculumOptions(departmentId, departmentId !== null);
  // Atanacak danışman: erişilebilir TÜM hocalar (atama modalında aranır).
  const advisorsQuery = useDepartmentInstructors(null, null, true);
  const assignmentMutation = useBulkAdvisorAssignment(departmentId);

  const students = studentsQuery.data?.students ?? EMPTY_STUDENTS;
  const totalStudents = studentsQuery.data?.totalStudents ?? 0;
  const withoutAdvisorCount = studentsQuery.data?.withoutAdvisorCount ?? 0;
  const instructors = advisorsQuery.data ?? EMPTY_INSTRUCTORS;
  const departmentOptions = contextQuery.data?.accessibleDepartments ?? [];
  const curricula = useMemo(() => curriculaQuery.data ?? [], [curriculaQuery.data]);

  const pageCount = Math.max(1, Math.ceil(totalStudents / pageSize));
  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    if (page > pageCount) setPage(pageCount);
  }, [page, pageCount]);

  const programOptions = useMemo(() => {
    const map = new Map<number, string>();
    curricula.forEach(curriculum => {
      map.set(curriculum.programId, `${curriculum.programCode} - ${curriculum.programName}`);
    });
    return [...map.entries()]
      .map(([id, label]) => ({ value: String(id), label }))
      .sort((a, b) => a.label.localeCompare(b.label, 'tr'));
  }, [curricula]);

  const curriculumOptions = useMemo(
    () => curricula
      .map(curriculum => ({
        value: String(curriculum.curriculumId),
        label: `${curriculum.programCode} - ${curriculum.curriculumName} (${curriculum.curriculumVersion})`,
      }))
      .sort((a, b) => a.label.localeCompare(b.label, 'tr')),
    [curricula],
  );

  const pageAllSelected = canAssignAdvisors && students.length > 0
    && students.every(student => selectedStudents.has(student.studentId));

  function toggleStudent(student: DepartmentStudentListItem) {
    if (!canAssignAdvisors) return;
    setSelectedStudents(previous => {
      const next = new Map(previous);
      if (next.has(student.studentId)) {
        next.delete(student.studentId);
      } else {
        next.set(student.studentId, {
          studentId: student.studentId,
          studentNumber: student.studentNumber,
          studentName: student.studentName,
        });
      }
      return next;
    });
  }

  function togglePageSelection() {
    if (!canAssignAdvisors || students.length === 0) return;
    setSelectedStudents(previous => {
      const next = new Map(previous);
      if (pageAllSelected) {
        students.forEach(student => next.delete(student.studentId));
      } else {
        students.forEach(student => next.set(student.studentId, {
          studentId: student.studentId,
          studentNumber: student.studentNumber,
          studentName: student.studentName,
        }));
      }
      return next;
    });
  }

  function clearSelection() {
    setSelectedStudents(new Map());
  }

  // Modalda danışman seçilince atama yapılır.
  function assignAdvisor(advisor: InstructorOptionDto) {
    if (!canAssignAdvisors || departmentId === null || selectedStudents.size === 0) return;

    assignmentMutation.mutate(
      {
        departmentId,
        advisorId: advisor.instructorId,
        replaceExisting: true, // atama her zaman mevcut danışmanın üzerine yazar (Proliz davranışı)
        studentIds: [...selectedStudents.keys()],
      },
      {
        onSuccess: result => {
          setLastResult(result);
          setAssignOpen(false);
          const failed = new Map<number, SelectedStudent>();
          result.results
            .filter(item => !item.success)
            .forEach(item => failed.set(item.studentId, {
              studentId: item.studentId,
              studentNumber: item.studentNumber,
              studentName: item.studentName,
            }));
          setSelectedStudents(failed);
        },
      },
    );
  }

  const mutationError = assignmentMutation.isError
    ? getAcademicOperationsErrorMessage(assignmentMutation.error, 'Danışman ataması tamamlanamadı.')
    : null;
  const loading = studentsQuery.isLoading;

  return (
    <DepartmentOperationsLayout
      icon={UserCheck}
      title="Toplu Danışman Atama"
      description="Bölüm öğrencilerini filtreleyip seçili gruba akademik danışman atayın."
      context={contextQuery.data ?? null}
      departmentId={departmentId}
      onDepartmentChange={setDepartmentId}
      isContextLoading={contextQuery.isLoading}
      contextError={contextQuery.isError ? getAcademicOperationsErrorMessage(contextQuery.error, 'Kapsam bilgileri alınamadı.') : null}
      showDepartmentBadge={false}
      showDepartmentSelect={false}
      compactHeader
      hideHeader
    >
      <div className="space-y-3">
        {(studentsQuery.isError || advisorsQuery.isError || mutationError) && (
          <Alert variant="destructive">
            <AlertCircle className="h-4 w-4" />
            <AlertDescription>
              {mutationError
                ?? getAcademicOperationsErrorMessage(studentsQuery.error ?? advisorsQuery.error, 'Toplu danışman atama verisi yüklenemedi.')}
            </AlertDescription>
          </Alert>
        )}

        {!canAssignAdvisors && (
          <Alert variant="warning">
            <ShieldAlert className="h-4 w-4" />
            <AlertDescription>
              Bu ekranda öğrenci ve danışman listesini inceleyebilirsiniz. Toplu danışman ataması için işlem yetkisi gerekir.
            </AlertDescription>
          </Alert>
        )}

        {/* Tek bütün panel — Proliz tarzı */}
        <div className="flex min-h-[calc(100vh-9rem)] flex-col overflow-hidden rounded-lg border bg-card shadow-sm">
          {/* Filtre bandı */}
          <div className="border-b bg-muted/30 p-3">
            <div className="grid gap-x-3 gap-y-2 md:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
              {departmentOptions.length > 1 && (
                <Field label="Bölüm">
                  <select
                    value={departmentId ?? ''}
                    onChange={event => setDepartmentId(Number(event.target.value))}
                    className="h-9 w-full rounded-md border border-input bg-background px-2 text-sm outline-none focus:ring-2 focus:ring-ring"
                  >
                    {departmentOptions.map(department => (
                      <option key={department.departmentId} value={department.departmentId}>
                        {department.departmentCode} - {department.departmentName}
                      </option>
                    ))}
                  </select>
                </Field>
              )}
              <FilterSelect label="Program" value={programFilter} onValueChange={setProgramFilter}
                options={[{ value: 'all', label: 'Tüm programlar' }, ...programOptions]} />
              <FilterSelect label="Müfredat" value={curriculumFilter} onValueChange={setCurriculumFilter}
                options={[{ value: 'all', label: 'Tüm müfredatlar' }, ...curriculumOptions]} />
              <FilterSelect label="Sınıf" value={classFilter} onValueChange={setClassFilter} options={CLASS_OPTIONS} />
              <FilterSelect label="Öğrenci durumu" value={statusFilter} onValueChange={setStatusFilter} options={STUDENT_STATUS_OPTIONS} />
              <FilterSelect label="Mevcut danışman" value={advisorFilter} onValueChange={setAdvisorFilter} options={ADVISOR_FILTER_OPTIONS} />
              <Field label="Öğrenci No aralığı">
                <div className="flex items-center gap-1">
                  <Input value={studentNoFrom} onChange={e => setStudentNoFrom(e.target.value)} placeholder="Baş." className="h-9 text-sm" inputMode="numeric" />
                  <span className="text-muted-foreground">–</span>
                  <Input value={studentNoTo} onChange={e => setStudentNoTo(e.target.value)} placeholder="Bitiş" className="h-9 text-sm" inputMode="numeric" />
                </div>
              </Field>
              <Field label="Kayıt tarihi aralığı">
                <div className="flex items-center gap-1">
                  <Input type="date" value={enrollmentFrom} onChange={e => setEnrollmentFrom(e.target.value)} className="h-9 text-sm" />
                  <span className="text-muted-foreground">–</span>
                  <Input type="date" value={enrollmentTo} onChange={e => setEnrollmentTo(e.target.value)} className="h-9 text-sm" />
                </div>
              </Field>
              <Field label="Arama" className="md:col-span-2">
                <div className="relative">
                  <Search className="pointer-events-none absolute left-2.5 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
                  <Input value={searchInput} onChange={event => setSearchInput(event.target.value)}
                    placeholder="Öğrenci, numara veya e-posta ara..." className="h-9 pl-8 text-sm" />
                </div>
              </Field>
            </div>
          </div>

          {/* Aksiyon şeridi — butonlar sağda (Proliz düzeni) */}
          <div className="flex flex-wrap items-center justify-between gap-3 border-b px-3 py-2.5">
            <div className="flex items-center gap-3 text-xs">
              <Stat label="Toplam" value={totalStudents} loading={loading} />
              <span className="h-5 w-px bg-border" />
              <Stat label="Danışmansız" value={withoutAdvisorCount} loading={loading} />
              <span className="h-5 w-px bg-border" />
              <Stat label="Seçili" value={selectedStudents.size} loading={false} />
            </div>
            <div className="flex items-center gap-2">
              <Button type="button" variant="outline" className="h-9 px-3 text-xs" onClick={clearSelection} disabled={selectedStudents.size === 0}>
                <X className="h-3.5 w-3.5" />
                Temizle
              </Button>
              <Button
                type="button"
                className="h-9 px-4 text-xs"
                onClick={() => setAssignOpen(true)}
                disabled={!canAssignAdvisors || selectedStudents.size === 0 || assignmentMutation.isPending}
              >
                <UserCheck className="h-3.5 w-3.5" />
                Danışman Ata
              </Button>
            </div>
          </div>

          {/* Liste başlık şeridi */}
          <div className="flex items-center justify-between border-b px-3 py-2">
            <span className="text-sm font-semibold">Öğrenciler</span>
            <Badge variant="outline">{totalStudents} kayıt</Badge>
          </div>

          {/* Tablo — sayfayı dolduran tek parça */}
          <div className="flex-1 overflow-auto">
            <Table className="[&_td]:px-4 [&_td]:py-3 [&_th]:px-4 [&_th]:py-3">
              <TableHeader className="sticky top-0 z-10 bg-card">
                <TableRow>
                  <TableHead className="w-[44px]">
                    <input
                      type="checkbox"
                      className="h-4 w-4 rounded border-input"
                      checked={pageAllSelected}
                      disabled={!canAssignAdvisors || students.length === 0}
                      onChange={togglePageSelection}
                      aria-label="Bu sayfadaki öğrencileri seç"
                    />
                  </TableHead>
                  <TableHead className="w-[110px]">Öğrenci No</TableHead>
                  <TableHead>Ad Soyad</TableHead>
                  <TableHead className="w-[70px] text-center">Sınıf</TableHead>
                  <TableHead className="w-[110px] text-center">Kayıt Tarihi</TableHead>
                  <TableHead>Danışman</TableHead>
                  <TableHead>Program</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {loading ? (
                  Array.from({ length: 8 }).map((_, index) => (
                    <TableRow key={`s-${index}`}>
                      <TableCell colSpan={TABLE_COLUMN_COUNT}><Skeleton className="h-6 w-full" /></TableCell>
                    </TableRow>
                  ))
                ) : students.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={TABLE_COLUMN_COUNT}>
                      <div className="flex flex-col items-center justify-center py-24 text-center">
                        <Users className="mb-3 h-10 w-10 text-muted-foreground/40" />
                        <p className="text-sm font-medium">Filtreye uygun öğrenci bulunamadı.</p>
                        <p className="mt-1 text-sm text-muted-foreground">Arama, program veya danışman filtresini değiştirin.</p>
                      </div>
                    </TableCell>
                  </TableRow>
                ) : (
                  students.map(student => (
                    <TableRow
                      key={student.studentId}
                      data-state={selectedStudents.has(student.studentId) ? 'selected' : undefined}
                    >
                      <TableCell>
                        <input
                          type="checkbox"
                          className="h-4 w-4 rounded border-input"
                          checked={selectedStudents.has(student.studentId)}
                          disabled={!canAssignAdvisors}
                          onChange={() => toggleStudent(student)}
                          aria-label={`${student.studentName} seç`}
                        />
                      </TableCell>
                      <TableCell className="font-mono text-xs">{student.studentNumber}</TableCell>
                      <TableCell className="font-medium">{student.studentName}</TableCell>
                      <TableCell className="text-center text-sm">{getStudentClassYear(student)}</TableCell>
                      <TableCell className="text-center text-xs text-muted-foreground">{formatDate(student.enrollmentDate)}</TableCell>
                      <TableCell>
                        {student.advisorName ? student.advisorName : <span className="text-muted-foreground">Atanmamış</span>}
                      </TableCell>
                      <TableCell className="text-xs">{student.programCode} - {student.programName}</TableCell>
                    </TableRow>
                  ))
                )}
              </TableBody>
            </Table>
          </div>

          {/* Sayfalama */}
          <div className="border-t">
            <TablePagination
              totalCount={totalStudents}
              page={page}
              pageSize={pageSize}
              onPageChange={setPage}
              onPageSizeChange={(size) => { setPageSize(size); setPage(1); }}
              itemLabel="öğrenci"
            />
          </div>
        </div>

        <BulkOperationResultPanel result={lastResult ?? null} title="Son Danışman Atama Sonucu" />
      </div>

      <AssignAdvisorModal
        open={assignOpen}
        onOpenChange={setAssignOpen}
        instructors={instructors}
        selectedCount={selectedStudents.size}
        onAssign={assignAdvisor}
        isPending={assignmentMutation.isPending}
      />
    </DepartmentOperationsLayout>
  );
}

function Field({ label, children, className }: { label: string; children: ReactNode; className?: string }) {
  return (
    <div className={`space-y-1 ${className ?? ''}`}>
      <Label className="text-xs text-muted-foreground">{label}</Label>
      {children}
    </div>
  );
}

function FilterSelect({
  label,
  value,
  onValueChange,
  options,
  disabled = false,
}: {
  label: string;
  value: string;
  onValueChange: (value: string) => void;
  options: { value: string; label: string }[];
  disabled?: boolean;
}) {
  return (
    <Field label={label}>
      <Select value={value} onValueChange={onValueChange} disabled={disabled}>
        <SelectTrigger className="h-9 text-sm">
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          {options.map(option => (
            <SelectItem key={option.value} value={option.value}>
              {option.label}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
    </Field>
  );
}

function Stat({ label, value, loading }: { label: string; value: number; loading: boolean }) {
  return (
    <div className="flex items-center gap-1.5">
      <span className="text-muted-foreground">{label}:</span>
      {loading ? <Skeleton className="h-4 w-6" /> : <span className="font-semibold">{value}</span>}
    </div>
  );
}

/**
 * Proliz'deki "Danışman Ata → arama ekranı" karşılığı: önce öğrenciler seçilir, "Danışman Ata"ya
 * basınca bu modal açılır; ada/bölüme göre aranan danışman seçilince atama yapılır.
 */
function AssignAdvisorModal({
  open,
  onOpenChange,
  instructors,
  selectedCount,
  onAssign,
  isPending,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  instructors: InstructorOptionDto[];
  selectedCount: number;
  onAssign: (advisor: InstructorOptionDto) => void;
  isPending: boolean;
}) {
  const [query, setQuery] = useState('');
  const [facultyFilter, setFacultyFilter] = useState('all');
  const [titleFilter, setTitleFilter] = useState('all');

  const facultyOptions = useMemo(
    () => [...new Set(instructors.map(i => i.facultyName).filter(Boolean))].sort((a, b) => a.localeCompare(b, 'tr')),
    [instructors],
  );
  const titleOptions = useMemo(
    () => [...new Set(instructors.map(i => i.title).filter(Boolean))].sort((a, b) => a.localeCompare(b, 'tr')),
    [instructors],
  );

  const filtered = useMemo(() => {
    const normalized = query.trim().toLocaleLowerCase('tr-TR');
    return instructors.filter(instructor => {
      if (facultyFilter !== 'all' && instructor.facultyName !== facultyFilter) return false;
      if (titleFilter !== 'all' && instructor.title !== titleFilter) return false;
      if (!normalized) return true;
      return instructor.personName.toLocaleLowerCase('tr-TR').includes(normalized)
        || instructor.title.toLocaleLowerCase('tr-TR').includes(normalized)
        || (instructor.departmentCode || '').toLocaleLowerCase('tr-TR').includes(normalized)
        || (instructor.departmentName || '').toLocaleLowerCase('tr-TR').includes(normalized)
        || (instructor.facultyName || '').toLocaleLowerCase('tr-TR').includes(normalized);
    });
  }, [instructors, query, facultyFilter, titleFilter]);

  // Modal kapanınca arama/filtreleri sıfırla.
  useEffect(() => {
    if (!open) { setQuery(''); setFacultyFilter('all'); setTitleFilter('all'); }
  }, [open]);

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-3xl">
        <DialogHeader>
          <DialogTitle>Danışman Ata</DialogTitle>
          <DialogDescription>Seçili {selectedCount} öğrenciye atanacak danışmanı arayıp seçin.</DialogDescription>
        </DialogHeader>

        <div className="grid gap-2 sm:grid-cols-[1fr_180px_180px]">
          <div className="relative">
            <Search className="pointer-events-none absolute left-2.5 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
            <Input value={query} onChange={event => setQuery(event.target.value)} placeholder="Ad, bölüm veya fakülte ara..." className="h-9 pl-8" autoFocus />
          </div>
          <Select value={facultyFilter} onValueChange={setFacultyFilter}>
            <SelectTrigger className="h-9 text-sm"><SelectValue placeholder="Fakülte" /></SelectTrigger>
            <SelectContent>
              <SelectItem value="all">Tüm fakülteler</SelectItem>
              {facultyOptions.map(faculty => <SelectItem key={faculty} value={faculty}>{faculty}</SelectItem>)}
            </SelectContent>
          </Select>
          <Select value={titleFilter} onValueChange={setTitleFilter}>
            <SelectTrigger className="h-9 text-sm"><SelectValue placeholder="Unvan" /></SelectTrigger>
            <SelectContent>
              <SelectItem value="all">Tüm unvanlar</SelectItem>
              {titleOptions.map(title => <SelectItem key={title} value={title}>{title}</SelectItem>)}
            </SelectContent>
          </Select>
        </div>

        <div className="text-xs text-muted-foreground">{filtered.length} danışman</div>

        <div className="max-h-[440px] overflow-auto rounded-md border">
          <Table className="[&_td]:px-3 [&_td]:py-2.5 [&_th]:px-3">
            <TableHeader className="sticky top-0 z-10 bg-card">
              <TableRow>
                <TableHead className="w-[120px]">Unvan</TableHead>
                <TableHead>Ad Soyad</TableHead>
                <TableHead className="w-[90px]">Bölüm</TableHead>
                <TableHead className="w-[170px]">Fakülte</TableHead>
                <TableHead className="w-[70px] text-center">Yük</TableHead>
                <TableHead className="w-[80px]" />
              </TableRow>
            </TableHeader>
            <TableBody>
              {filtered.length === 0 ? (
                <TableRow>
                  <TableCell colSpan={6} className="py-10 text-center text-sm text-muted-foreground">Eşleşen danışman bulunamadı.</TableCell>
                </TableRow>
              ) : (
                filtered.map(instructor => (
                  <TableRow key={instructor.instructorId} className="hover:bg-muted/40">
                    <TableCell className="text-xs text-muted-foreground">{instructor.title || '—'}</TableCell>
                    <TableCell className="font-medium">{instructor.personName}</TableCell>
                    <TableCell className="text-xs text-muted-foreground">{instructor.departmentCode || instructor.departmentName}</TableCell>
                    <TableCell className="text-xs text-muted-foreground">{instructor.facultyName || '—'}</TableCell>
                    <TableCell className="text-center text-xs tabular-nums">{instructor.currentStudentCount}</TableCell>
                    <TableCell className="text-right">
                      <Button type="button" size="sm" className="h-7 px-4 text-xs" disabled={isPending} onClick={() => onAssign(instructor)}>
                        {isPending ? '...' : 'Seç'}
                      </Button>
                    </TableCell>
                  </TableRow>
                ))
              )}
            </TableBody>
          </Table>
        </div>
      </DialogContent>
    </Dialog>
  );
}

function getStudentClassYear(student: DepartmentStudentListItem) {
  return Math.min(4, Math.max(1, Math.floor(student.completedSemesters / 2) + 1));
}

function formatDate(value: string) {
  if (!value) return '—';
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? '—' : date.toLocaleDateString('tr-TR');
}
