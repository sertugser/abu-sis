import { useEffect, useMemo, useState } from 'react';
import { AlertCircle, ListChecks, Loader2, Search, ShieldAlert, Users, X } from 'lucide-react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Skeleton } from '@/components/ui/skeleton';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import BulkOperationResultPanel from '@/components/academic-operations/BulkOperationResultPanel';
import DepartmentOperationsLayout from '@/components/academic-operations/DepartmentOperationsLayout';
import { resolveInitialDepartmentId } from '@/components/academic-operations/utils';
import { getAcademicOperationsErrorMessage } from '../api/academicOperations.api';
import {
  useAcademicOperationsContext,
  useBulkCurriculumAssignment,
  useDepartmentCurriculumOptions,
  useDepartmentStudents,
} from '../hooks/useAcademicOperations';
import { usePermissions } from '../hooks/usePermissions';
import type {
  BulkAcademicOperationResult,
  DepartmentCurriculumOption,
  DepartmentStudentListItem,
} from '../types/academicOperations';

const STUDENT_STATUS_LABELS: Record<string, string> = {
  Active: 'Aktif',
  Frozen: 'Dondurulmuş',
  Suspended: 'Uzaklaştırılmış',
  Passive: 'Pasif',
  Dismissed: 'İlişiği Kesilmiş',
  Graduated: 'Mezun',
};

const STUDENT_STATUS_ORDER = Object.keys(STUDENT_STATUS_LABELS);

export default function BulkCurriculumAssignmentPage() {
  const contextQuery = useAcademicOperationsContext();
  const { hasPermission } = usePermissions();
  const canAssignCurriculum = hasPermission('menu.toplu-mufredat-atama.assign');
  const [departmentId, setDepartmentId] = useState<number | null>(null);
  const [search, setSearch] = useState('');
  const [statusFilter, setStatusFilter] = useState('all');
  const [programFilter, setProgramFilter] = useState('all');
  const [classFilter, setClassFilter] = useState('all');
  const [currentCurriculumFilter, setCurrentCurriculumFilter] = useState('all');
  const [targetCurriculumId, setTargetCurriculumId] = useState('none');
  const [selectedStudentIds, setSelectedStudentIds] = useState<Set<number>>(() => new Set());
  const [lastResult, setLastResult] = useState<BulkAcademicOperationResult | null>(null);

  useEffect(() => {
    if (departmentId === null && contextQuery.data) {
      // eslint-disable-next-line react-hooks/set-state-in-effect
      setDepartmentId(resolveInitialDepartmentId(contextQuery.data));
    }
  }, [contextQuery.data, departmentId]);

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setSearch('');
    setStatusFilter('all');
    setProgramFilter('all');
    setClassFilter('all');
    setCurrentCurriculumFilter('all');
    setTargetCurriculumId('none');
    setSelectedStudentIds(new Set());
    setLastResult(null);
  }, [departmentId]);

  const studentsQuery = useDepartmentStudents(departmentId, {}, departmentId !== null);
  const curriculaQuery = useDepartmentCurriculumOptions(departmentId, departmentId !== null);
  const assignmentMutation = useBulkCurriculumAssignment(departmentId);

  const students = studentsQuery.data?.students ?? [];
  const curricula = curriculaQuery.data ?? [];
  const targetCurriculum = targetCurriculumId === 'none'
    ? null
    : curricula.find(curriculum => curriculum.curriculumId === Number(targetCurriculumId)) ?? null;

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setSelectedStudentIds(previous => {
      const validIds = new Set(
        students
          .filter(student => canSelectStudentForTarget(student, targetCurriculum))
          .map(student => student.studentId),
      );
      const next = new Set([...previous].filter(studentId => validIds.has(studentId)));
      return next.size === previous.size ? previous : next;
    });
  }, [students, targetCurriculum]);

  const programOptions = useMemo(() => {
    const map = new Map<number, { id: number; label: string }>();
    students.forEach(student => {
      map.set(student.programId, { id: student.programId, label: `${student.programCode} - ${student.programName}` });
    });
    return [...map.values()].sort((a, b) => a.label.localeCompare(b.label, 'tr'));
  }, [students]);

  const statusOptions = useMemo(() => {
    const statuses = [...new Set(students.map(student => student.status).filter(Boolean))];
    const sortedStatuses = statuses.sort((a, b) => {
      const firstIndex = STUDENT_STATUS_ORDER.indexOf(a);
      const secondIndex = STUDENT_STATUS_ORDER.indexOf(b);
      if (firstIndex !== -1 || secondIndex !== -1) {
        return (firstIndex === -1 ? Number.MAX_SAFE_INTEGER : firstIndex)
          - (secondIndex === -1 ? Number.MAX_SAFE_INTEGER : secondIndex);
      }
      return a.localeCompare(b, 'tr');
    });

    return [
      { value: 'all', label: 'Tüm öğrenci durumları' },
      ...sortedStatuses.map(status => ({ value: status, label: STUDENT_STATUS_LABELS[status] ?? status })),
    ];
  }, [students]);

  const classOptions = useMemo(() => {
    const classYears = new Set(students.map(getStudentClassYear));
    return [...classYears]
      .sort((a, b) => a - b)
      .map(classYear => ({ value: String(classYear), label: `${classYear}. sınıf` }));
  }, [students]);

  const currentCurriculumOptions = useMemo(() => {
    const map = new Map<number, { id: number; label: string }>();
    students.forEach(student => {
      map.set(student.curriculumId, {
        id: student.curriculumId,
        label: `${student.programCode} - ${student.curriculumName} (${student.curriculumVersion})`,
      });
    });
    return [...map.values()].sort((a, b) => a.label.localeCompare(b.label, 'tr'));
  }, [students]);

  const targetCurriculumOptions = useMemo(
    () => curricula.filter(curriculum => !isArchivedStatus(curriculum.status)),
    [curricula],
  );

  const filteredStudents = useMemo(() => {
    const query = search.trim().toLocaleLowerCase('tr-TR');
    return students.filter(student => {
      if (statusFilter !== 'all' && student.status !== statusFilter) return false;
      if (programFilter !== 'all' && student.programId !== Number(programFilter)) return false;
      if (classFilter !== 'all' && getStudentClassYear(student) !== Number(classFilter)) return false;
      if (currentCurriculumFilter !== 'all' && student.curriculumId !== Number(currentCurriculumFilter)) return false;
      if (targetCurriculum && !canSelectStudentForTarget(student, targetCurriculum)) return false;
      if (!query) return true;

      return [
        student.studentNumber,
        student.studentName,
        student.email,
        student.programName,
        student.programCode,
        student.curriculumName,
        student.curriculumVersion,
      ].some(value => value.toLocaleLowerCase('tr-TR').includes(query));
    });
  }, [classFilter, currentCurriculumFilter, programFilter, search, statusFilter, students, targetCurriculum]);

  const selectableVisibleStudents = useMemo(
    () => filteredStudents.filter(student => canSelectStudent(student)),
    [filteredStudents, targetCurriculum],
  );

  const selectedStudents = useMemo(
    () => students.filter(student => selectedStudentIds.has(student.studentId)),
    [selectedStudentIds, students],
  );

  const visibleAllSelected = selectableVisibleStudents.length > 0
    && selectableVisibleStudents.every(student => selectedStudentIds.has(student.studentId));

  const studentsAlreadyInTarget = targetCurriculum
    ? students.filter(student => student.curriculumId === targetCurriculum.curriculumId).length
    : 0;

  const eligibleForTarget = targetCurriculum
    ? students.filter(student => canSelectStudentForTarget(student, targetCurriculum)).length
    : 0;

  function canSelectStudent(student: DepartmentStudentListItem) {
    return canAssignCurriculum && canSelectStudentForTarget(student, targetCurriculum);
  }

  function toggleStudent(student: DepartmentStudentListItem) {
    if (!canSelectStudent(student)) return;

    setSelectedStudentIds(previous => {
      const next = new Set(previous);
      if (next.has(student.studentId)) next.delete(student.studentId);
      else next.add(student.studentId);
      return next;
    });
  }

  function toggleVisibleStudents() {
    if (!canAssignCurriculum) return;
    setSelectedStudentIds(previous => {
      const next = new Set(previous);
      if (visibleAllSelected) {
        selectableVisibleStudents.forEach(student => next.delete(student.studentId));
      } else {
        selectableVisibleStudents.forEach(student => next.add(student.studentId));
      }
      return next;
    });
  }

  function clearSelection() {
    setSelectedStudentIds(new Set());
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

  const mutationError = assignmentMutation.isError
    ? getAcademicOperationsErrorMessage(assignmentMutation.error, 'Müfredat ataması tamamlanamadı.')
    : null;

  return (
    <DepartmentOperationsLayout
      icon={ListChecks}
      title=""
      description=""
      context={contextQuery.data ?? null}
      departmentId={departmentId}
      onDepartmentChange={setDepartmentId}
      isContextLoading={contextQuery.isLoading}
      contextError={contextQuery.isError ? getAcademicOperationsErrorMessage(contextQuery.error, 'Kapsam bilgileri alınamadı.') : null}
      showDepartmentBadge={false}
      compactHeader
      hideHeader
    >
      <div className="space-y-3">
        {(studentsQuery.isError || curriculaQuery.isError || mutationError) && (
          <Alert variant="destructive">
            <AlertCircle className="h-4 w-4" />
            <AlertDescription>
              {mutationError
                ?? getAcademicOperationsErrorMessage(studentsQuery.error ?? curriculaQuery.error, 'Toplu müfredat atama verisi yüklenemedi.')}
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

        <Card>
          <CardContent className="p-3">
            <div className="grid gap-3 xl:grid-cols-[minmax(0,1.65fr)_minmax(390px,0.9fr)]">
              <div className="grid gap-2 md:grid-cols-3 2xl:grid-cols-4">
                <div className="space-y-1 md:col-span-3 2xl:col-span-2">
                  <Label className="text-xs">Arama</Label>
                  <div className="relative">
                    <Search className="pointer-events-none absolute left-2.5 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
                    <Input
                      value={search}
                      onChange={event => setSearch(event.target.value)}
                      placeholder="Öğrenci, numara veya müfredat ara..."
                      className="h-8 pl-8 text-xs"
                    />
                  </div>
                </div>
                <FilterSelect label="Öğrenci durumu" value={statusFilter} onValueChange={setStatusFilter} options={statusOptions} />
                <FilterSelect
                  label="Program"
                  value={programFilter}
                  onValueChange={setProgramFilter}
                  options={[
                    { value: 'all', label: 'Tüm programlar' },
                    ...programOptions.map(program => ({ value: String(program.id), label: program.label })),
                  ]}
                />
                <FilterSelect
                  label="Sınıf"
                  value={classFilter}
                  onValueChange={setClassFilter}
                  options={[
                    { value: 'all', label: 'Tüm sınıflar' },
                    ...classOptions,
                  ]}
                />
                <FilterSelect
                  label="Mevcut müfredat"
                  value={currentCurriculumFilter}
                  onValueChange={setCurrentCurriculumFilter}
                  options={[
                    { value: 'all', label: 'Tüm müfredatlar' },
                    ...currentCurriculumOptions.map(curriculum => ({ value: String(curriculum.id), label: curriculum.label })),
                  ]}
                />
              </div>

              <div className="grid gap-2 sm:grid-cols-2">
                <div className="sm:col-span-2">
                  <FilterSelect
                    label="Hedef müfredat"
                    value={targetCurriculumId}
                    onValueChange={setTargetCurriculumId}
                    disabled={!canAssignCurriculum}
                    options={[
                      { value: 'none', label: 'Hedef seçin' },
                      ...targetCurriculumOptions.map(curriculum => ({
                        value: String(curriculum.curriculumId),
                        label: formatCurriculumOption(curriculum),
                      })),
                    ]}
                  />
                </div>

                <div className="grid grid-cols-4 gap-1.5 text-center text-xs sm:col-span-2">
                  <SmallStat label="Toplam" value={students.length} loading={studentsQuery.isLoading} />
                  <SmallStat label="Atanabilir" value={eligibleForTarget} loading={studentsQuery.isLoading} />
                  <SmallStat label="Hedefte" value={studentsAlreadyInTarget} loading={studentsQuery.isLoading} />
                  <SmallStat label="Seçili" value={selectedStudentIds.size} loading={studentsQuery.isLoading} />
                </div>

                {targetCurriculum && (
                  <div className="flex flex-wrap items-center gap-2 rounded-md border bg-muted/20 px-2.5 py-1.5 text-xs sm:col-span-2">
                    <span className="font-medium">{targetCurriculum.programCode}</span>
                    <span className="text-muted-foreground">{targetCurriculum.curriculumName} ({targetCurriculum.curriculumVersion})</span>
                    <CurriculumStatusBadge status={targetCurriculum.status} />
                    {targetCurriculum.isActive && <Badge variant="success">Aktif</Badge>}
                  </div>
                )}

                <div className="flex gap-2 sm:col-span-2">
                  <Button
                    type="button"
                    variant="outline"
                    className="h-8 px-3 text-xs"
                    onClick={clearSelection}
                    disabled={selectedStudents.length === 0}
                  >
                    <X className="h-3.5 w-3.5" />
                    Temizle
                  </Button>
                  <Button
                    className="h-8 flex-1 text-xs"
                    onClick={runAssignment}
                    disabled={
                      !canAssignCurriculum
                      || assignmentMutation.isPending
                      || targetCurriculum === null
                      || selectedStudentIds.size === 0
                    }
                  >
                    {assignmentMutation.isPending ? <Loader2 className="h-4 w-4 animate-spin" /> : <ListChecks className="h-4 w-4" />}
                    {!canAssignCurriculum ? 'Atama Yetkisi Yok' : assignmentMutation.isPending ? 'Atanıyor' : 'Müfredata Ata'}
                  </Button>
                </div>
              </div>
            </div>
          </CardContent>
        </Card>

        <StudentTable
          students={filteredStudents}
          loading={studentsQuery.isLoading}
          selectedStudentIds={selectedStudentIds}
          visibleAllSelected={visibleAllSelected}
          canSelect={canAssignCurriculum}
          canSelectStudent={canSelectStudent}
          onToggleStudent={toggleStudent}
          onToggleVisible={toggleVisibleStudents}
        />

        <BulkOperationResultPanel result={lastResult} title="Son Müfredat Atama Sonucu" />
      </div>
    </DepartmentOperationsLayout>
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
    <div className="space-y-1">
      <Label className="text-xs">{label}</Label>
      <Select value={value} onValueChange={onValueChange} disabled={disabled}>
        <SelectTrigger className="h-8 text-xs">
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
    </div>
  );
}

function SmallStat({ label, value, loading }: { label: string; value: number; loading: boolean }) {
  return (
    <div className="rounded-md border bg-muted/20 px-2 py-1.5">
      <div className="text-[11px] text-muted-foreground">{label}</div>
      {loading ? <Skeleton className="mx-auto mt-1 h-4 w-9" /> : <div className="text-base font-semibold leading-5">{value}</div>}
    </div>
  );
}

function StudentTable({
  students,
  loading,
  selectedStudentIds,
  visibleAllSelected,
  canSelect,
  canSelectStudent,
  onToggleStudent,
  onToggleVisible,
}: {
  students: DepartmentStudentListItem[];
  loading: boolean;
  selectedStudentIds: Set<number>;
  visibleAllSelected: boolean;
  canSelect: boolean;
  canSelectStudent: (student: DepartmentStudentListItem) => boolean;
  onToggleStudent: (student: DepartmentStudentListItem) => void;
  onToggleVisible: () => void;
}) {
  const hasSelectableStudents = students.some(canSelectStudent);

  return (
    <Card>
      <CardHeader className="pb-2">
        <div className="flex items-center justify-between gap-3">
          <CardTitle className="text-sm">Öğrenciler</CardTitle>
          <Badge variant="outline">{students.length} kayıt</Badge>
        </div>
      </CardHeader>
      <CardContent className="p-0">
        {loading ? (
          <div className="p-3">
            <Skeleton className="h-[360px] rounded-lg" />
          </div>
        ) : students.length === 0 ? (
          <div className="flex flex-col items-center justify-center py-16 text-center">
            <Users className="mb-3 h-9 w-9 text-muted-foreground/45" />
            <p className="text-sm font-medium">Filtreye uygun öğrenci bulunamadı.</p>
            <p className="mt-1 text-sm text-muted-foreground">Arama, program veya mevcut müfredat filtresini değiştirin.</p>
          </div>
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead className="w-[52px]">
                  <input
                    type="checkbox"
                    className="h-4 w-4 rounded border-input"
                    checked={visibleAllSelected}
                    disabled={!canSelect || !hasSelectableStudents}
                    onChange={onToggleVisible}
                    aria-label="Görünen uygun öğrencileri seç"
                  />
                </TableHead>
                <TableHead>Öğrenci</TableHead>
                <TableHead>Program / Mevcut müfredat</TableHead>
                <TableHead className="w-[90px]">Sınıf</TableHead>
                <TableHead className="w-[130px]">Öğrenci durumu</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {students.map(student => {
                const studentSelectable = canSelectStudent(student);

                return (
                  <TableRow
                    key={student.studentId}
                    data-state={selectedStudentIds.has(student.studentId) ? 'selected' : undefined}
                    className={studentSelectable ? undefined : 'opacity-60'}
                  >
                    <TableCell>
                      <input
                        type="checkbox"
                        className="h-4 w-4 rounded border-input"
                        checked={selectedStudentIds.has(student.studentId)}
                        disabled={!studentSelectable}
                        onChange={() => onToggleStudent(student)}
                        aria-label={`${student.studentName} seç`}
                      />
                    </TableCell>
                    <TableCell>
                      <div className="font-medium">{student.studentName}</div>
                      <div className="text-xs text-muted-foreground">{student.studentNumber} · {student.email}</div>
                    </TableCell>
                    <TableCell>
                      <div>{student.programCode} - {student.programName}</div>
                      <div className="text-xs text-muted-foreground">{student.curriculumName} ({student.curriculumVersion})</div>
                    </TableCell>
                    <TableCell>
                      <Badge variant="outline">{getStudentClassYear(student)}. sınıf</Badge>
                    </TableCell>
                    <TableCell>
                      <StatusBadge status={student.status} />
                    </TableCell>
                  </TableRow>
                );
              })}
            </TableBody>
          </Table>
        )}
      </CardContent>
    </Card>
  );
}

function getStudentClassYear(student: DepartmentStudentListItem) {
  return Math.min(4, Math.max(1, Math.floor(student.completedSemesters / 2) + 1));
}

function canSelectStudentForTarget(student: DepartmentStudentListItem, targetCurriculum: DepartmentCurriculumOption | null) {
  return !!targetCurriculum
    && student.programId === targetCurriculum.programId
    && student.curriculumId !== targetCurriculum.curriculumId;
}

function normalizeCurriculumStatus(status: number | string) {
  const normalized = String(status);
  if (normalized === '1' || normalized === 'Published') return 'published';
  if (normalized === '2' || normalized === 'Archived') return 'archived';
  return 'draft';
}

function StatusBadge({ status }: { status: string }) {
  const variant: 'success' | 'info' | 'secondary' = status === 'Active'
    ? 'success'
    : status === 'Graduated'
      ? 'info'
      : 'secondary';
  return <Badge variant={variant}>{STUDENT_STATUS_LABELS[status] ?? status}</Badge>;
}

function CurriculumStatusBadge({ status }: { status: number | string }) {
  const normalized = normalizeCurriculumStatus(status);
  const label = normalized === 'published'
    ? 'Yayında'
    : normalized === 'archived'
      ? 'Arşiv'
      : 'Taslak';
  const variant: 'success' | 'secondary' | 'warning' = normalized === 'published'
    ? 'success'
    : normalized === 'archived'
      ? 'secondary'
      : 'warning';

  return <Badge variant={variant}>{label}</Badge>;
}

function isArchivedStatus(status: number | string) {
  return normalizeCurriculumStatus(status) === 'archived';
}

function formatCurriculumOption(curriculum: DepartmentCurriculumOption) {
  const statusLabel = normalizeCurriculumStatus(curriculum.status) === 'published' ? 'Yayında' : 'Taslak';
  const activeLabel = curriculum.isActive ? ' - Aktif' : '';
  return `${curriculum.programCode} - ${curriculum.curriculumName} (${curriculum.curriculumVersion}) - ${statusLabel}${activeLabel}`;
}
