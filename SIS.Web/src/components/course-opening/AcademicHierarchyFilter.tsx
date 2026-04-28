import { Fragment, useMemo } from 'react';
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
  'h-7 w-full rounded-md border border-gray-200 bg-white px-2 text-xs focus:ring-1 focus:ring-gray-300 focus:outline-none';
const LABEL_CLASS = 'text-xs font-semibold text-gray-600 self-center';

/**
 * Akademik hiyerarşi filtreleri (Fakülte → Bölüm → Program → [Müfredat]).
 * Parent grid container `grid-cols-[80px_1fr]` üzerinde fragment olarak render eder
 * — her satır 2 grid hücresi (label + select) kullanır, böylece her iki panel arası
 * mükemmel hizalama sağlanır.
 */
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
      structureDepartments.filter((sd) => accessibleDeptIds.has(sd.id)).map((sd) => sd.facultyId),
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

  const curriculumQuery = useCurriculumList(
    showCurriculum && programId != null ? { programId } : undefined,
  );
  const curriculumOptions = useMemo(() => curriculumQuery.data ?? [], [curriculumQuery.data]);

  return (
    <Fragment>
      <span className={LABEL_CLASS}>Fakülte</span>
      <select
        className={SELECT_CLASS}
        value={facultyId ?? ''}
        onChange={(e) => {
          const v = e.target.value ? Number(e.target.value) : null;
          onFacultyChange(v);
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

      <span className={LABEL_CLASS}>Bölüm</span>
      <select
        className={SELECT_CLASS}
        value={deptId ?? ''}
        onChange={(e) => {
          const v = e.target.value ? Number(e.target.value) : null;
          onDeptChange(v);
          onProgramChange(null);
          onCurriculumChange?.(null);
        }}
      >
        <option value="">Seçiniz</option>
        {visibleDepartments.map((d) => (
          <option key={d.id} value={d.id}>{d.code} — {d.name}</option>
        ))}
      </select>

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

      {showCurriculum && (
        <Fragment>
          <span className={LABEL_CLASS}>Müfredat</span>
          <select
            className={SELECT_CLASS}
            value={curriculumId ?? ''}
            onChange={(e) => onCurriculumChange?.(e.target.value ? Number(e.target.value) : null)}
            disabled={programId == null || curriculumQuery.isLoading}
          >
            <option value="">{programId == null ? 'Önce program' : 'Seçiniz'}</option>
            {curriculumOptions.map((c) => (
              <option key={c.id} value={c.id}>
                {c.version ? `(${c.version}) ` : ''}{c.name ?? `Müfredat #${c.id}`}
              </option>
            ))}
          </select>
        </Fragment>
      )}
    </Fragment>
  );
}
