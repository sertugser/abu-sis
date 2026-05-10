import { useEffect, useMemo, useState, type ReactNode } from 'react';
import { AlertCircle, Loader2, Search, ShieldAlert, UserCheck, Users, X } from 'lucide-react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Skeleton } from '@/components/ui/skeleton';
import { Switch } from '@/components/ui/switch';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { TablePagination } from '@/components/ui/table-pagination';
import BulkOperationResultPanel from '@/components/academic-operations/BulkOperationResultPanel';
import DepartmentOperationsLayout from '@/components/academic-operations/DepartmentOperationsLayout';
import { resolveInitialDepartmentId } from '@/components/academic-operations/utils';
import { getAcademicOperationsErrorMessage } from '../api/academicOperations.api';
import { useAcademicOperationsContext, useBulkAdvisorAssignment, useDepartmentStudents } from '../hooks/useAcademicOperations';
import { useDepartmentInstructors } from '../hooks/useCourseOpening';
import { usePermissions } from '../hooks/usePermissions';
import type { BulkAcademicOperationResult, DepartmentStudentListItem } from '../types/academicOperations';
import type { InstructorOptionDto } from '../types/courseOpening';

const STUDENT_STATUS_LABELS: Record<string, string> = {
  Active: 'Aktif',
  Frozen: 'Dondurulmuş',
  Suspended: 'Uzaklaştırılmış',
  Passive: 'Pasif',
  Dismissed: 'İlişiği Kesilmiş',
  Graduated: 'Mezun',
};

const STUDENT_STATUS_ORDER = Object.keys(STUDENT_STATUS_LABELS);

export default function BulkAdvisorAssignmentPage() {
  const contextQuery = useAcademicOperationsContext();
  const { hasPermission } = usePermissions();
  const canAssignAdvisors = hasPermission('menu.toplu-danisman-atama.assign');
  const [departmentId, setDepartmentId] = useState<number | null>(null);
  const [search, setSearch] = useState('');
  const [statusFilter, setStatusFilter] = useState('all');
  const [programFilter, setProgramFilter] = useState('all');
  const [classFilter, setClassFilter] = useState('all');
  const [curriculumFilter, setCurriculumFilter] = useState('all');
  const [advisorFilter, setAdvisorFilter] = useState('all');
  const [selectedAdvisorId, setSelectedAdvisorId] = useState('none');
  const [replaceExisting, setReplaceExisting] = useState(false);
  const [selectedStudentIds, setSelectedStudentIds] = useState<Set<number>>(() => new Set());
  const [lastResult, setLastResult] = useState<BulkAcademicOperationResult | null>(null);
  const [currentPage, setCurrentPage] = useState(1);
  const [pageSize, setPageSize] = useState(25);
  // Hoca bazlı kapasite ve dahil/değil overrides — initial value backend'den, kullanıcı override edebilir
  const [capacityOverrides, setCapacityOverrides] = useState<Record<number, string>>({});
  const [excludedAdvisors, setExcludedAdvisors] = useState<Record<number, boolean>>({});
  // Atama modu: 'single' = tek hoca tümüne, 'per-student' = her öğrenci için ayrı
  const [assignmentMode, setAssignmentMode] = useState<'single' | 'per-student'>('single');
  const [perStudentAdvisorIds, setPerStudentAdvisorIds] = useState<Record<number, number>>({});

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
    setCurriculumFilter('all');
    setAdvisorFilter('all');
    setSelectedAdvisorId('none');
    setReplaceExisting(false);
    setSelectedStudentIds(new Set());
    setLastResult(null);
  }, [departmentId]);

  const studentsQuery = useDepartmentStudents(departmentId, {}, departmentId !== null);
  const instructorsQuery = useDepartmentInstructors(departmentId);
  const assignmentMutation = useBulkAdvisorAssignment(departmentId);

  const students = studentsQuery.data?.students ?? [];
  const instructors = instructorsQuery.data ?? [];
  const departmentOptions = contextQuery.data?.accessibleDepartments ?? [];

  // Backend'den gelen kapasite/dahil değerleriyle local override'ları senkronize et
  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setCapacityOverrides(prev => {
      const next: Record<number, string> = { ...prev };
      instructors.forEach(inst => {
        if (next[inst.instructorId] === undefined) {
          next[inst.instructorId] = inst.advisorAssignmentCapacity == null
            ? ''
            : String(inst.advisorAssignmentCapacity);
        }
      });
      return next;
    });
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setExcludedAdvisors(prev => {
      const next: Record<number, boolean> = { ...prev };
      instructors.forEach(inst => {
        if (next[inst.instructorId] === undefined) {
          next[inst.instructorId] = !inst.isAvailableForAdvisorAssignment;
        }
      });
      return next;
    });
  }, [instructors]);

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setSelectedStudentIds(previous => {
      const validIds = new Set(
        students
          .filter(student => replaceExisting || student.advisorId === null)
          .map(student => student.studentId),
      );
      const next = new Set([...previous].filter(studentId => validIds.has(studentId)));
      return next.size === previous.size ? previous : next;
    });
  }, [replaceExisting, students]);

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

  const curriculumOptions = useMemo(() => {
    const map = new Map<number, { id: number; label: string }>();
    students.forEach(student => {
      map.set(student.curriculumId, {
        id: student.curriculumId,
        label: `${student.programCode} - ${student.curriculumName} (${student.curriculumVersion})`,
      });
    });
    return [...map.values()].sort((a, b) => a.label.localeCompare(b.label, 'tr'));
  }, [students]);

  const advisorFilterOptions = useMemo(() => {
    const map = new Map<number, string>();
    students.forEach(student => {
      if (student.advisorId !== null && student.advisorName?.trim()) {
        map.set(student.advisorId, student.advisorName.trim());
      }
    });

    const assignedAdvisorOptions = [...map.entries()]
      .map(([advisorId, advisorName]) => ({ value: String(advisorId), label: advisorName }))
      .sort((a, b) => a.label.localeCompare(b.label, 'tr'));

    return [
      { value: 'all', label: 'Tüm öğrenciler' },
      { value: 'none', label: 'Danışmansız' },
      ...assignedAdvisorOptions,
    ];
  }, [students]);

  const filteredStudents = useMemo(() => {
    const query = search.trim().toLocaleLowerCase('tr-TR');
    return students.filter(student => {
      if (statusFilter !== 'all' && student.status !== statusFilter) return false;
      if (programFilter !== 'all' && student.programId !== Number(programFilter)) return false;
      if (classFilter !== 'all' && getStudentClassYear(student) !== Number(classFilter)) return false;
      if (curriculumFilter !== 'all' && student.curriculumId !== Number(curriculumFilter)) return false;
      if (advisorFilter === 'none' && student.advisorId !== null) return false;
      if (advisorFilter !== 'all' && advisorFilter !== 'none' && student.advisorId !== Number(advisorFilter)) return false;
      if (!query) return true;

      const advisorSearchValue = student.advisorName?.trim() || 'danismansiz atanmamis';

      return [
        student.studentNumber,
        student.studentName,
        student.email,
        student.programCode,
        student.programName,
        student.curriculumName,
        student.curriculumVersion,
        advisorSearchValue,
      ].some(value => value.toLocaleLowerCase('tr-TR').includes(query));
    });
  }, [advisorFilter, classFilter, curriculumFilter, programFilter, search, statusFilter, students]);

  const selectedStudents = useMemo(
    () => students.filter(student => selectedStudentIds.has(student.studentId)),
    [selectedStudentIds, students],
  );

  const selectableFilteredStudents = useMemo(
    () => filteredStudents.filter(student => replaceExisting || student.advisorId === null),
    [filteredStudents, replaceExisting],
  );

  // Filtre değişimleri → ilk sayfaya dön
  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setCurrentPage(1);
  }, [search, statusFilter, programFilter, classFilter, curriculumFilter, advisorFilter]);

  // Liste daralırsa açık sayfa son sayfanın ötesine düşmesin
  const totalPages = Math.max(1, Math.ceil(filteredStudents.length / pageSize));
  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    if (currentPage > totalPages) setCurrentPage(totalPages);
  }, [currentPage, totalPages]);
  const safePage = Math.min(currentPage, totalPages);
  const pageStudents = useMemo(
    () => filteredStudents.slice((safePage - 1) * pageSize, safePage * pageSize),
    [filteredStudents, safePage, pageSize],
  );

  const visibleAllSelected = selectableFilteredStudents.length > 0
    && selectableFilteredStudents.every(student => selectedStudentIds.has(student.studentId));

  const selectedAdvisor = selectedAdvisorId === 'none'
    ? null
    : instructors.find(instructor => instructor.instructorId === Number(selectedAdvisorId)) ?? null;

  const stats = useMemo(
    () => ({ withoutAdvisor: students.filter(student => student.advisorId === null).length }),
    [students],
  );

  function canSelectStudent(student: DepartmentStudentListItem) {
    return canAssignAdvisors && (replaceExisting || student.advisorId === null);
  }

  function handleReplaceExistingChange(checked: boolean) {
    setReplaceExisting(checked);

    if (!checked) {
      const advisorlessIds = new Set(
        students
          .filter(student => student.advisorId === null)
          .map(student => student.studentId),
      );
      setSelectedStudentIds(previous => new Set([...previous].filter(studentId => advisorlessIds.has(studentId))));
    }
  }

  function toggleStudent(studentId: number) {
    const student = students.find(currentStudent => currentStudent.studentId === studentId);
    if (!student || !canSelectStudent(student)) return;

    setSelectedStudentIds(previous => {
      const next = new Set(previous);
      if (next.has(studentId)) next.delete(studentId);
      else next.add(studentId);
      return next;
    });
  }

  function toggleVisibleStudents() {
    if (!canAssignAdvisors) return;
    setSelectedStudentIds(previous => {
      const next = new Set(previous);
      if (visibleAllSelected) {
        selectableFilteredStudents.forEach(student => next.delete(student.studentId));
      } else {
        selectableFilteredStudents.forEach(student => next.add(student.studentId));
      }
      return next;
    });
  }

  function clearSelection() {
    setSelectedStudentIds(new Set());
  }

  function runAssignment() {
    if (!canAssignAdvisors || departmentId === null || selectedStudentIds.size === 0) return;

    if (assignmentMode === 'single') {
      if (selectedAdvisor === null) return;
      assignmentMutation.mutate(
        {
          departmentId,
          advisorId: selectedAdvisor.instructorId,
          replaceExisting,
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
      return;
    }

    // Per-student modu: aynı hocaya gidenleri grupla, her grup için ayrı API çağrısı
    const groups = new Map<number, number[]>();
    for (const studentId of selectedStudentIds) {
      const advisorId = perStudentAdvisorIds[studentId];
      if (!advisorId) continue;
      const list = groups.get(advisorId) ?? [];
      list.push(studentId);
      groups.set(advisorId, list);
    }
    if (groups.size === 0) return;

    Promise.all(
      [...groups.entries()].map(([advisorId, studentIds]) =>
        assignmentMutation.mutateAsync({
          departmentId,
          advisorId,
          replaceExisting,
          studentIds,
        }),
      ),
    ).then(results => {
      // Tüm sonuçları birleştir
      const merged: BulkAcademicOperationResult = {
        requestedCount: results.reduce((sum, r) => sum + r.requestedCount, 0),
        successCount: results.reduce((sum, r) => sum + r.successCount, 0),
        failedCount: results.reduce((sum, r) => sum + r.failedCount, 0),
        results: results.flatMap(r => r.results),
        errors: results.flatMap(r => r.errors ?? []),
      };
      setLastResult(merged);
      const failedIds = merged.results.filter(item => !item.success).map(item => item.studentId);
      setSelectedStudentIds(new Set(failedIds));
    });
  }

  const mutationError = assignmentMutation.isError
    ? getAcademicOperationsErrorMessage(assignmentMutation.error, 'Danışman ataması tamamlanamadı.')
    : null;

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
        {(studentsQuery.isError || instructorsQuery.isError || mutationError) && (
          <Alert variant="destructive">
            <AlertCircle className="h-4 w-4" />
            <AlertDescription>
              {mutationError
                ?? getAcademicOperationsErrorMessage(studentsQuery.error ?? instructorsQuery.error, 'Toplu danışman atama verisi yüklenemedi.')}
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

        <Card>
          <CardContent className="p-3">
            <div className="grid gap-3 xl:grid-cols-[minmax(0,1.45fr)_minmax(360px,0.85fr)]">
              <div className="space-y-2">
                <div className="grid gap-2 md:grid-cols-2">
                  {departmentOptions.length > 1 && (
                    <div className="space-y-1">
                      <Label className="text-xs">Bölüm</Label>
                      <select
                        value={departmentId ?? ''}
                        onChange={event => setDepartmentId(Number(event.target.value))}
                        className="h-8 w-full rounded-md border border-input bg-background px-2 text-xs outline-none focus:ring-2 focus:ring-ring"
                      >
                        {departmentOptions.map(department => (
                          <option key={department.departmentId} value={department.departmentId}>
                            {department.departmentCode} - {department.departmentName}
                          </option>
                        ))}
                      </select>
                    </div>
                  )}
                  <div className={`space-y-1 ${departmentOptions.length > 1 ? '' : 'md:col-span-2'}`}>
                    <Label className="text-xs">Arama</Label>
                    <div className="relative">
                      <Search className="pointer-events-none absolute left-2.5 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
                      <Input
                        value={search}
                        onChange={event => setSearch(event.target.value)}
                        placeholder="Öğrenci, numara veya danışman ara..."
                        className="h-8 pl-8 text-xs"
                      />
                    </div>
                  </div>
                </div>

                <div className="grid gap-2 md:grid-cols-2 xl:grid-cols-3 2xl:grid-cols-5">
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
                    label="Müfredat"
                    value={curriculumFilter}
                    onValueChange={setCurriculumFilter}
                    options={[
                      { value: 'all', label: 'Tüm müfredatlar' },
                      ...curriculumOptions.map(curriculum => ({ value: String(curriculum.id), label: curriculum.label })),
                    ]}
                  />
                  <FilterSelect
                    label="Mevcut danışman"
                    value={advisorFilter}
                    onValueChange={setAdvisorFilter}
                    options={advisorFilterOptions}
                  />
                </div>
              </div>

              <div className="grid gap-2 sm:grid-cols-2">
                <div className="space-y-1 sm:col-span-2">
                  <Label className="text-xs">Atama Modu</Label>
                  <div className="flex h-8 rounded-md border bg-background overflow-hidden">
                    <button
                      type="button"
                      onClick={() => setAssignmentMode('single')}
                      disabled={!canAssignAdvisors}
                      className={`flex-1 text-xs font-medium px-3 transition-colors ${
                        assignmentMode === 'single'
                          ? 'bg-primary text-primary-foreground'
                          : 'hover:bg-muted'
                      } disabled:opacity-50 disabled:cursor-not-allowed`}
                    >
                      Tek Hoca, Tümüne Uygula
                    </button>
                    <button
                      type="button"
                      onClick={() => setAssignmentMode('per-student')}
                      disabled={!canAssignAdvisors}
                      className={`flex-1 text-xs font-medium px-3 transition-colors border-l ${
                        assignmentMode === 'per-student'
                          ? 'bg-primary text-primary-foreground'
                          : 'hover:bg-muted'
                      } disabled:opacity-50 disabled:cursor-not-allowed`}
                    >
                      Her Öğrenciye Ayrı
                    </button>
                  </div>
                </div>
                {assignmentMode === 'single' ? (
                  <FilterSelect
                    label="Yeni danışman"
                    value={selectedAdvisorId}
                    onValueChange={setSelectedAdvisorId}
                    disabled={!canAssignAdvisors}
                    options={[
                      { value: 'none', label: 'Danışman seçin' },
                      ...instructors
                        .filter(instructor => !excludedAdvisors[instructor.instructorId])
                        .map(instructor => ({
                          value: String(instructor.instructorId),
                          label: instructor.fullName,
                        })),
                    ]}
                  />
                ) : (
                  <div className="space-y-1">
                    <Label className="text-xs">Danışman Seçimi</Label>
                    <div className="flex h-8 items-center rounded-md border border-dashed px-2.5 text-xs text-muted-foreground">
                      Tabloda her öğrenci için ayrı seçilir
                    </div>
                  </div>
                )}

                <div className="flex min-h-[52px] items-end">
                  <div className="flex h-8 w-full items-center justify-between rounded-md border px-2.5">
                    <Label htmlFor="replace-existing" className="text-xs">Danışmanı olan öğrencileri de yeniden ata</Label>
                    <Switch
                      id="replace-existing"
                      checked={replaceExisting}
                      onCheckedChange={handleReplaceExistingChange}
                      disabled={!canAssignAdvisors}
                    />
                  </div>
                </div>

                <div className="grid grid-cols-3 gap-1.5 text-center text-xs sm:col-span-2">
                  <SmallStat label="Toplam" value={students.length} loading={studentsQuery.isLoading} />
                  <SmallStat label="Danışmansız" value={stats.withoutAdvisor} loading={studentsQuery.isLoading} />
                  <SmallStat label="Seçili" value={selectedStudentIds.size} loading={studentsQuery.isLoading} />
                </div>

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
                      !canAssignAdvisors
                      || assignmentMutation.isPending
                      || (assignmentMode === 'single' && selectedAdvisor === null)
                      || (assignmentMode === 'per-student' && [...selectedStudentIds].every(id => !perStudentAdvisorIds[id]))
                      || selectedStudentIds.size === 0
                    }
                  >
                    {assignmentMutation.isPending ? <Loader2 className="h-4 w-4 animate-spin" /> : <UserCheck className="h-4 w-4" />}
                    {!canAssignAdvisors
                      ? 'Atama Yetkisi Yok'
                      : assignmentMutation.isPending
                        ? 'Atanıyor'
                        : assignmentMode === 'per-student'
                          ? 'Per-Öğrenci Ata'
                          : replaceExisting
                            ? 'Seçili Öğrencileri Yeniden Ata'
                            : 'Danışmansızlara Ata'}
                  </Button>
                </div>
              </div>
            </div>
          </CardContent>
        </Card>

        <AdvisorCapacityPanel
          instructors={instructors}
          loading={instructorsQuery.isLoading}
          capacityOverrides={capacityOverrides}
          excludedAdvisors={excludedAdvisors}
          onCapacityChange={(id, value) => setCapacityOverrides(prev => ({ ...prev, [id]: value }))}
          onExcludeChange={(id, excluded) => setExcludedAdvisors(prev => ({ ...prev, [id]: excluded }))}
          disabled={!canAssignAdvisors}
        />

        <StudentTable
          students={pageStudents}
          loading={studentsQuery.isLoading}
          selectedStudentIds={selectedStudentIds}
          visibleAllSelected={visibleAllSelected}
          canSelect={canAssignAdvisors}
          canSelectStudent={canSelectStudent}
          onToggleStudent={toggleStudent}
          onToggleVisible={toggleVisibleStudents}
          perStudentMode={assignmentMode === 'per-student'}
          availableAdvisors={instructors.filter(i => !excludedAdvisors[i.instructorId])}
          perStudentAdvisorIds={perStudentAdvisorIds}
          onPerStudentAdvisorChange={(studentId, advisorId) =>
            setPerStudentAdvisorIds(prev => ({ ...prev, [studentId]: advisorId }))}
          paginationFooter={
            <TablePagination
              totalCount={filteredStudents.length}
              page={safePage}
              pageSize={pageSize}
              onPageChange={setCurrentPage}
              onPageSizeChange={(size) => { setPageSize(size); setCurrentPage(1); }}
              itemLabel="öğrenci"
            />
          }
        />

        <BulkOperationResultPanel result={lastResult ?? null} title="Son Danışman Atama Sonucu" />
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
  paginationFooter,
  perStudentMode = false,
  availableAdvisors = [],
  perStudentAdvisorIds = {},
  onPerStudentAdvisorChange,
}: {
  students: DepartmentStudentListItem[];
  loading: boolean;
  selectedStudentIds: Set<number>;
  visibleAllSelected: boolean;
  canSelect: boolean;
  canSelectStudent: (student: DepartmentStudentListItem) => boolean;
  onToggleStudent: (studentId: number) => void;
  onToggleVisible: () => void;
  paginationFooter?: ReactNode;
  perStudentMode?: boolean;
  availableAdvisors?: InstructorOptionDto[];
  perStudentAdvisorIds?: Record<number, number>;
  onPerStudentAdvisorChange?: (studentId: number, advisorId: number) => void;
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
            <p className="mt-1 text-sm text-muted-foreground">Arama, program veya danışman filtresini değiştirin.</p>
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
                  aria-label="Görünen öğrencileri seç"
                />
              </TableHead>
              <TableHead>Öğrenci</TableHead>
              <TableHead>Program / Müfredat</TableHead>
              <TableHead className="w-[90px]">Sınıf</TableHead>
              <TableHead>Mevcut danışman</TableHead>
              {perStudentMode && <TableHead className="w-[220px]">Atanacak danışman</TableHead>}
              <TableHead className="w-[140px]">Öğrenci durumu</TableHead>
              <TableHead className="w-[120px] text-right">GNO</TableHead>
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
                      onChange={() => onToggleStudent(student.studentId)}
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
                    {student.advisorName ? student.advisorName : <span className="text-muted-foreground">Atanmamış</span>}
                  </TableCell>
                  {perStudentMode && (
                    <TableCell>
                      <select
                        value={perStudentAdvisorIds[student.studentId] ?? ''}
                        onChange={event => onPerStudentAdvisorChange?.(student.studentId, Number(event.target.value))}
                        disabled={!studentSelectable}
                        className="h-8 w-full rounded-md border border-input bg-background px-2 text-xs outline-none focus:ring-2 focus:ring-ring disabled:opacity-50"
                      >
                        <option value="">Seçiniz...</option>
                        {availableAdvisors.map(advisor => (
                          <option key={advisor.instructorId} value={advisor.instructorId}>
                            {advisor.fullName}
                          </option>
                        ))}
                      </select>
                    </TableCell>
                  )}
                  <TableCell>
                    <StatusBadge status={student.status} />
                  </TableCell>
                  <TableCell className="text-right tabular-nums">{student.cgpa.toFixed(2)}</TableCell>
                </TableRow>
              );
            })}
          </TableBody>
        </Table>
        )}
      </CardContent>
      {paginationFooter}
    </Card>
  );
}

function AdvisorCapacityPanel({
  instructors,
  loading,
  capacityOverrides,
  excludedAdvisors,
  onCapacityChange,
  onExcludeChange,
  disabled,
}: {
  instructors: InstructorOptionDto[];
  loading: boolean;
  capacityOverrides: Record<number, string>;
  excludedAdvisors: Record<number, boolean>;
  onCapacityChange: (instructorId: number, value: string) => void;
  onExcludeChange: (instructorId: number, excluded: boolean) => void;
  disabled: boolean;
}) {
  if (loading) return null;
  if (instructors.length === 0) return null;

  return (
    <Card>
      <CardHeader className="pb-2">
        <div className="flex items-center justify-between gap-3">
          <CardTitle className="text-sm">Hoca Kapasiteleri</CardTitle>
          <Badge variant="outline">{instructors.length} hoca</Badge>
        </div>
      </CardHeader>
      <CardContent className="p-0">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Hoca</TableHead>
              <TableHead className="w-[100px] text-center">Mevcut Yük</TableHead>
              <TableHead className="w-[110px] text-center">Atamaya Dahil</TableHead>
              <TableHead className="w-[140px] text-center">Kapasite</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {instructors.map(instructor => {
              const isExcluded = excludedAdvisors[instructor.instructorId] ?? false;
              const capacityText = capacityOverrides[instructor.instructorId] ?? '';
              return (
                <TableRow key={instructor.instructorId} className={isExcluded ? 'opacity-60' : undefined}>
                  <TableCell className="font-medium">{instructor.fullName}</TableCell>
                  <TableCell className="text-center tabular-nums">{instructor.currentStudentCount}</TableCell>
                  <TableCell className="text-center">
                    <input
                      type="checkbox"
                      className="h-4 w-4 rounded border-input"
                      checked={!isExcluded}
                      disabled={disabled}
                      onChange={event => onExcludeChange(instructor.instructorId, !event.target.checked)}
                      aria-label={`${instructor.fullName} atamaya dahil`}
                    />
                  </TableCell>
                  <TableCell className="text-center">
                    <Input
                      type="number"
                      min={0}
                      placeholder="Sınırsız"
                      value={capacityText}
                      disabled={disabled || isExcluded}
                      onChange={event => onCapacityChange(instructor.instructorId, event.target.value)}
                      className="h-8 w-24 mx-auto text-center text-xs"
                    />
                  </TableCell>
                </TableRow>
              );
            })}
          </TableBody>
        </Table>
      </CardContent>
    </Card>
  );
}

function getStudentClassYear(student: DepartmentStudentListItem) {
  return Math.min(4, Math.max(1, Math.floor(student.completedSemesters / 2) + 1));
}

function StatusBadge({ status }: { status: string }) {
  const variant: 'success' | 'info' | 'secondary' = status === 'Active'
    ? 'success'
    : status === 'Graduated'
      ? 'info'
      : 'secondary';
  return <Badge variant={variant}>{STUDENT_STATUS_LABELS[status] ?? status}</Badge>;
}
