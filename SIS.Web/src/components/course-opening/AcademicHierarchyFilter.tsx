import { useMemo } from 'react';
import { useCurriculumList } from '../../hooks/useCurriculumManagement';
import type { AccessibleDepartment } from '../../types/academicOperations';
import type { FacultyDto, DepartmentDto, ProgramDto } from '../../hooks/useAdminAcademicStructure';

export interface AcademicHierarchyFilterProps {
  faculties: FacultyDto[];
  structureDepartments: DepartmentDto[];
  accessibleDepartments: AccessibleDepartment[];
  programs: ProgramDto[];

  facultyId: number | null;
  onFacultyChange: (id: number | null) => void;
  deptId: number | null;
  onDeptChange: (id: number | null) => void;
  programId: number | null;
  onProgramChange: (id: number | null) => void;

  curriculumId?: number | null;
  onCurriculumChange?: (id: number | null) => void;
  showCurriculum?: boolean;
}

const SELECT_CLASS =
  'h-7 rounded-md border border-gray-200 bg-gray-50 px-2 text-xs focus:ring-1 focus:ring-gray-300 focus:outline-none min-w-[140px]';
const LABEL_CLASS = 'text-xs font-semibold text-gray-500 whitespace-nowrap w-16';

export function AcademicHierarchyFilter({
  faculties,
  structureDepartments,
  accessibleDepartments,
  programs,
  facultyId,
  onFacultyChange,
  deptId,
  onDeptChange,
  programId,
  onProgramChange,
  curriculumId = null,
  onCurriculumChange,
  showCurriculum = false,
}: AcademicHierarchyFilterProps) {
  const accessibleDeptIds = useMemo(
    () => new Set(accessibleDepartments.map((d) => d.departmentId)),
    [accessibleDepartments],
  );

  const visibleFaculties = useMemo(() => {
    const facultyIds = new Set(
      structureDepartments
        .filter((sd) => accessibleDeptIds.has(sd.id))
        .map((sd) => sd.facultyId),
    );
    return faculties.filter((f) => facultyIds.has(f.id));
  }, [faculties, structureDepartments, accessibleDeptIds]);

  const visibleDepartments = useMemo(() => {
    const inFaculty = facultyId == null
      ? structureDepartments
      : structureDepartments.filter((sd) => sd.facultyId === facultyId);
    return inFaculty.filter((sd) => accessibleDeptIds.has(sd.id));
  }, [structureDepartments, accessibleDeptIds, facultyId]);

  const visiblePrograms = useMemo(() => {
    if (deptId == null) return [];
    return programs.filter((p) => p.departmentId === deptId);
  }, [programs, deptId]);

  // Müfredat dropdown — sadece program seçilince ve gösterilmesi istenirse
  const curriculumQuery = useCurriculumList(
    showCurriculum && programId != null ? { programId } : undefined,
  );
  const curriculumOptions = useMemo(() => curriculumQuery.data ?? [], [curriculumQuery.data]);

  return (
    <div className="flex items-center gap-2 flex-wrap">
      <div className="flex items-center gap-1.5">
        <span className={LABEL_CLASS}>Fakülte</span>
        <select
          className={SELECT_CLASS}
          value={facultyId ?? ''}
          onChange={(e) => {
            const v = e.target.value ? Number(e.target.value) : null;
            onFacultyChange(v);
            // Bölüm/Program bu fakülteye uymuyorsa sıfırla
            if (v != null) {
              const okDept = visibleDepartments.find((d) => d.facultyId === v);
              if (!okDept || okDept.id !== deptId) {
                onDeptChange(null);
                onProgramChange(null);
                onCurriculumChange?.(null);
              }
            }
          }}
        >
          <option value="">Tümü</option>
          {visibleFaculties.map((f) => (
            <option key={f.id} value={f.id}>{f.name}</option>
          ))}
        </select>
      </div>

      <div className="flex items-center gap-1.5">
        <span className={LABEL_CLASS}>Bölüm</span>
        <select
          className={SELECT_CLASS}
          value={deptId ?? ''}
          onChange={(e) => {
            const v = e.target.value ? Number(e.target.value) : null;
            onDeptChange(v);
            // Program/Müfredat bu bölüme uymuyorsa sıfırla
            onProgramChange(null);
            onCurriculumChange?.(null);
          }}
        >
          <option value="">Seçiniz</option>
          {visibleDepartments.map((d) => (
            <option key={d.id} value={d.id}>
              {d.code} — {d.name}
            </option>
          ))}
        </select>
      </div>

      <div className="flex items-center gap-1.5">
        <span className={LABEL_CLASS}>Program</span>
        <select
          className={SELECT_CLASS}
          value={programId ?? ''}
          onChange={(e) => {
            const v = e.target.value ? Number(e.target.value) : null;
            onProgramChange(v);
            onCurriculumChange?.(null);
          }}
          disabled={deptId == null}
        >
          <option value="">{deptId == null ? 'Önce bölüm' : 'Seçiniz'}</option>
          {visiblePrograms.map((p) => (
            <option key={p.id} value={p.id}>{p.name}</option>
          ))}
        </select>
      </div>

      {showCurriculum && (
        <div className="flex items-center gap-1.5">
          <span className={LABEL_CLASS}>Müfredat</span>
          <select
            className={SELECT_CLASS + ' min-w-[260px]'}
            value={curriculumId ?? ''}
            onChange={(e) => onCurriculumChange?.(e.target.value ? Number(e.target.value) : null)}
            disabled={programId == null || curriculumQuery.isLoading}
          >
            <option value="">{programId == null ? 'Önce program' : 'Seçiniz'}</option>
            {curriculumOptions.map((c) => (
              <option key={c.id} value={c.id}>
                {c.version ? `(${c.version}) ` : ''}
                {c.name ?? `Müfredat #${c.id}`}
              </option>
            ))}
          </select>
        </div>
      )}
    </div>
  );
}
