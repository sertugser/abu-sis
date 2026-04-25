import { useState, useMemo } from 'react';
import { Badge } from '@/components/ui/badge';
import { Alert, AlertTitle, AlertDescription } from '@/components/ui/alert';
import { useAcademicOperationsContext } from '@/hooks/useAcademicOperations';
import { useAdminFaculties, useAdminDepartmentsByStructure } from '@/hooks/useAdminAcademicStructure';
import { useTermClosingStatus } from '@/hooks/useTermClosing';
import type { AccessibleDepartment } from '@/types/academicOperations';
import { Loader2, CheckCircle2, FileText, Shield } from 'lucide-react';

import { DepartmentOverview } from './components/DepartmentOverview';
import { GradeLockStep } from './components/GradeLockStep';
import { GpaStep } from './components/GpaStep';
import { PromotionStep } from './components/PromotionStep';
import { NewTermStep } from './components/NewTermStep';

export default function TermClosingPage() {
  const opsContext = useAcademicOperationsContext();
  // useMemo ile stable referans (?? [] her render'da yeni array yaratırdı)
  const departments = useMemo(
    () => opsContext.data?.accessibleDepartments ?? [],
    [opsContext.data],
  );
  const { data: faculties } = useAdminFaculties();
  const { data: structureDepartments } = useAdminDepartmentsByStructure();

  const [selectedFacultyId, setSelectedFacultyId] = useState<number | null>(null);
  const [selectedDeptId, setSelectedDeptId] = useState<number | null>(null);

  const isDeanOrAdmin = opsContext.data?.roleLabel === 'Dean' || opsContext.data?.roleLabel === 'SystemAdministrator';

  // Accessible faculties
  const accessibleDeptIds = useMemo(() => new Set(departments.map(d => d.departmentId)), [departments]);
  const availableFaculties = useMemo(() => {
    if (!faculties || !structureDepartments) return [];
    const facultyIds = new Set(
      structureDepartments.filter(sd => accessibleDeptIds.has(sd.id)).map(sd => sd.facultyId)
    );
    return faculties.filter(f => facultyIds.has(f.id));
  }, [faculties, structureDepartments, accessibleDeptIds]);

  // Departments filtered by faculty
  const filteredDepartments = useMemo(() => {
    if (!selectedFacultyId || !structureDepartments) return departments;
    const deptIdsInFaculty = new Set(
      structureDepartments.filter(sd => sd.facultyId === selectedFacultyId).map(sd => sd.id)
    );
    return departments.filter(d => deptIdsInFaculty.has(d.departmentId));
  }, [departments, structureDepartments, selectedFacultyId]);

  const deptId = useMemo(() => {
    if (selectedDeptId && filteredDepartments.some(d => d.departmentId === selectedDeptId)) return selectedDeptId;
    return filteredDepartments[0]?.departmentId ?? null;
  }, [selectedDeptId, filteredDepartments]);

  const statusQuery = useTermClosingStatus(deptId);
  const status = statusQuery.data;

  const completedSet = useMemo(() => {
    const set = new Set<number>();
    status?.steps.forEach((s, i) => { if (s.isCompleted) set.add(i + 1); });
    return set;
  }, [status]);

  const completedCount = completedSet.size;

  // Find next active step
  const activeStep = useMemo(() => {
    if (!status) return 1;
    for (let i = 0; i < status.steps.length; i++) {
      if (!status.steps[i].isCompleted) return i + 1;
    }
    return 4; // all done
  }, [status]);

  const handleFacultyChange = (e: React.ChangeEvent<HTMLSelectElement>) => {
    setSelectedFacultyId(e.target.value ? Number(e.target.value) : null);
    setSelectedDeptId(null);
  };

  const handleDeptChange = (newDeptId: number) => {
    setSelectedDeptId(newDeptId);
  };

  if (!opsContext.data) {
    return (
      <div className="flex items-center justify-center py-24 text-muted-foreground gap-2">
        <Loader2 className="h-5 w-5 animate-spin" />
        <span>Yukleniyor...</span>
      </div>
    );
  }

  const roleLabelTr = opsContext.data.roleLabel === 'SystemAdministrator'
    ? 'Sistem Yoneticisi'
    : opsContext.data.roleLabel === 'Dean'
      ? 'Dekan'
      : 'Bolum Baskani';

  const getStep = (n: number) => status?.steps[n - 1];

  return (
    <div className="space-y-5">
      {/* ── Toolbar ── */}
      <div className="flex flex-wrap items-center justify-between gap-3">
        {/* Left: filters */}
        <div className="flex flex-wrap items-center gap-2">
          {/* Faculty filter — her zaman göster */}
          {availableFaculties.length > 0 && (
            <div className="flex items-center rounded-lg border border-gray-200 bg-white shadow-sm overflow-hidden">
              <span className="px-3 py-1.5 text-xs font-semibold text-gray-600 bg-gray-50 border-r border-gray-200 whitespace-nowrap">
                Fakulte
              </span>
              <select
                className="h-8 border-0 bg-transparent px-2.5 text-xs focus:ring-0 focus:outline-none min-w-[160px]"
                value={selectedFacultyId ?? (availableFaculties.length === 1 ? availableFaculties[0].id : '')}
                onChange={handleFacultyChange}
              >
                {availableFaculties.length > 1 && <option value="">Tumu</option>}
                {availableFaculties.map(f => (
                  <option key={f.id} value={f.id}>{f.name}</option>
                ))}
              </select>
            </div>
          )}

          {/* Department filter */}
          <div className="flex items-center rounded-lg border border-gray-200 bg-white shadow-sm overflow-hidden">
            <span className="px-3 py-1.5 text-xs font-semibold text-gray-600 bg-gray-50 border-r border-gray-200 whitespace-nowrap">
              Bolum
            </span>
            <select
              className="h-8 border-0 bg-transparent px-2.5 text-xs focus:ring-0 focus:outline-none min-w-[220px]"
              value={deptId ?? ''}
              onChange={e => handleDeptChange(Number(e.target.value))}
            >
              {filteredDepartments.map((d: AccessibleDepartment) => (
                <option key={d.departmentId} value={d.departmentId}>
                  {d.departmentCode} — {d.departmentName}
                </option>
              ))}
            </select>
          </div>

          {/* Term info badge */}
          {status?.termName && (
            <Badge variant="outline" className="font-mono text-xs gap-1.5 h-8 px-3">
              <FileText className="h-3 w-3" />
              {status.termName}
            </Badge>
          )}
        </div>

        {/* Right: status badges + role */}
        <div className="flex items-center gap-2">
          {completedCount === 4 ? (
            <Badge variant="success" className="gap-1 h-7">
              <CheckCircle2 className="h-3 w-3" />
              Tamamlandi
            </Badge>
          ) : completedCount > 0 ? (
            <Badge variant="secondary" className="gap-1 h-7 font-mono">
              {completedCount}/4 adim
            </Badge>
          ) : null}

          <div className="flex items-center gap-1.5 text-xs text-muted-foreground border-l border-border/60 pl-2 ml-1">
            <Shield className="h-3.5 w-3.5" />
            <span>{roleLabelTr}</span>
          </div>
        </div>
      </div>

      {/* ── Dean overview (multi-dept grid) ── */}
      {isDeanOrAdmin && (
        <DepartmentOverview
          facultyId={selectedFacultyId ?? undefined}
          selectedDeptId={deptId}
          onSelectDepartment={handleDeptChange}
        />
      )}

      {/* ── Accordion Steps ── */}
      {deptId && (
        <div className="space-y-3">
          <GradeLockStep
            key={`lock-${deptId}`}
            departmentId={deptId}
            isCompleted={completedSet.has(1)}
            isActive={activeStep === 1}
            completedAt={getStep(1)?.completedAt}
            completedBy={getStep(1)?.completedByName}
            completedNotes={getStep(1)?.notes}
            onStepDone={() => statusQuery.refetch()}
          />

          <GpaStep
            key={`gpa-${deptId}`}
            departmentId={deptId}
            isCompleted={completedSet.has(2)}
            isActive={activeStep === 2}
            step1Completed={completedSet.has(1)}
            completedAt={getStep(2)?.completedAt}
            completedBy={getStep(2)?.completedByName}
            completedNotes={getStep(2)?.notes}
            onStepDone={() => statusQuery.refetch()}
          />

          <PromotionStep
            key={`promote-${deptId}`}
            departmentId={deptId}
            isCompleted={completedSet.has(3)}
            isActive={activeStep === 3}
            step2Completed={completedSet.has(2)}
            completedAt={getStep(3)?.completedAt}
            completedBy={getStep(3)?.completedByName}
            completedNotes={getStep(3)?.notes}
            onStepDone={() => statusQuery.refetch()}
          />

          <NewTermStep
            key={`term-${deptId}`}
            departmentId={deptId}
            isCompleted={completedSet.has(4)}
            isActive={activeStep === 4}
            step3Completed={completedSet.has(3)}
            completedAt={getStep(4)?.completedAt}
            completedBy={getStep(4)?.completedByName}
            completedNotes={getStep(4)?.notes}
          />
        </div>
      )}

      {/* ── Full Completion Banner ── */}
      {completedCount === 4 && (
        <Alert variant="success" className="animate-fade-in">
          <CheckCircle2 className="h-5 w-5" />
          <AlertTitle>Donem Kapatma Tamamlandi</AlertTitle>
          <AlertDescription>
            Tum adimlar basariyla tamamlandi. Yeni donem acildi ve kullanima hazir.
          </AlertDescription>
        </Alert>
      )}
    </div>
  );
}
