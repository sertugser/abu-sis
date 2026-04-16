import { useMemo } from 'react';
import { cn } from '@/lib/utils';
import type { AccessibleDepartment } from '../../types/academicOperations';
import type { FacultyDto, DepartmentDto } from '../../hooks/useAdminAcademicStructure';

interface FilterBarProps {
  departments: AccessibleDepartment[];
  faculties: FacultyDto[];
  structureDepartments: DepartmentDto[];
  selectedFacultyId: number | null;
  onFacultyChange: (id: number | null) => void;
  selectedDeptId: number | null;
  onDeptChange: (id: number) => void;
  semesterFilter: number | null;
  onSemesterChange: (s: number | null) => void;
  typeFilter: 'all' | 'compulsory' | 'elective';
  onTypeChange: (t: 'all' | 'compulsory' | 'elective') => void;
  stats: {
    curriculumCount: number;
    openedCourseCount: number;
    openedSectionCount: number;
    pendingCount: number;
    totalCapacity: number;
    totalDemand: number;
  };
}

const CLASS_YEARS = [
  { value: null, label: 'Tümü' },
  { value: 1, label: '1. Sınıf' },
  { value: 2, label: '2. Sınıf' },
  { value: 3, label: '3. Sınıf' },
  { value: 4, label: '4. Sınıf' },
];

const TERMS = [
  { value: null, label: 'Tümü' },
  { value: 'fall', label: 'Güz' },
  { value: 'spring', label: 'Bahar' },
];

const TYPE_TABS: { value: FilterBarProps['typeFilter']; label: string }[] = [
  { value: 'all', label: 'Tümü' },
  { value: 'compulsory', label: 'Zorunlu' },
  { value: 'elective', label: 'Seçmeli' },
];

export function CourseFilterBar({
  departments,
  faculties,
  structureDepartments,
  selectedFacultyId,
  onFacultyChange,
  selectedDeptId,
  onDeptChange,
  semesterFilter,
  onSemesterChange,
  typeFilter,
  onTypeChange,
  onExportCsv,
}: FilterBarProps) {
  const accessibleDeptIds = useMemo(() => new Set(departments.map(d => d.departmentId)), [departments]);

  const availableFaculties = useMemo(() => {
    const facultyIds = new Set(
      structureDepartments
        .filter(sd => accessibleDeptIds.has(sd.id))
        .map(sd => sd.facultyId)
    );
    return faculties.filter(f => facultyIds.has(f.id));
  }, [faculties, structureDepartments, accessibleDeptIds]);

  const filteredDepartments = useMemo(() => {
    if (!selectedFacultyId) return departments;
    const deptIdsInFaculty = new Set(
      structureDepartments
        .filter(sd => sd.facultyId === selectedFacultyId)
        .map(sd => sd.id)
    );
    return departments.filter(d => deptIdsInFaculty.has(d.departmentId));
  }, [departments, structureDepartments, selectedFacultyId]);

  // Derive classYear and term from semesterFilter
  const activeClassYear = semesterFilter !== null ? Math.ceil(semesterFilter / 2) : null;
  const activeTerm = semesterFilter !== null ? (semesterFilter % 2 === 1 ? 'fall' : 'spring') : null;

  const handleClassYearChange = (year: number | null) => {
    if (year === null) {
      onSemesterChange(null);
    } else {
      // Default to fall when selecting a class year, or keep current term
      const term = activeTerm ?? 'fall';
      onSemesterChange((year - 1) * 2 + (term === 'fall' ? 1 : 2));
    }
  };

  const handleTermChange = (term: 'fall' | 'spring' | null) => {
    if (term === null) {
      onSemesterChange(null);
    } else {
      const year = activeClassYear ?? 1;
      onSemesterChange((year - 1) * 2 + (term === 'fall' ? 1 : 2));
    }
  };

  return (
    <div className="flex items-center gap-2 rounded-xl border border-gray-200 bg-white px-3 py-2 shadow-sm">
      {/* Fakülte */}
      <span className="text-xs font-semibold text-gray-500 whitespace-nowrap">Fakülte</span>
      <select
        className="h-7 rounded-md border border-gray-200 bg-gray-50 px-2 text-xs focus:ring-1 focus:ring-gray-300 focus:outline-none min-w-[140px]"
        value={selectedFacultyId ?? ''}
        onChange={e => onFacultyChange(e.target.value ? Number(e.target.value) : null)}
      >
        <option value="">Tümü</option>
        {availableFaculties.map(f => (
          <option key={f.id} value={f.id}>{f.name}</option>
        ))}
      </select>

      <div className="w-px self-stretch bg-gray-200 mx-1" />

      {/* Bölüm */}
      <span className="text-xs font-semibold text-gray-500 whitespace-nowrap">Bölüm</span>
      <select
        className="h-7 rounded-md border border-gray-200 bg-gray-50 px-2 text-xs focus:ring-1 focus:ring-gray-300 focus:outline-none min-w-[200px]"
        value={selectedDeptId ?? ''}
        onChange={e => onDeptChange(Number(e.target.value))}
      >
        {filteredDepartments.map(d => (
          <option key={d.departmentId} value={d.departmentId}>
            {d.departmentCode} — {d.departmentName}
          </option>
        ))}
      </select>

      <div className="w-px self-stretch bg-gray-200 mx-1" />

      {/* Sınıf */}
      <span className="text-xs font-semibold text-gray-500 whitespace-nowrap">Sınıf</span>
      <div className="flex rounded-lg bg-gray-100 p-0.5 gap-0.5 items-center">
        {CLASS_YEARS.map(c => (
          <button
            key={c.label}
            onClick={() => handleClassYearChange(c.value)}
            className={cn(
              'h-6 px-2.5 text-[11px] rounded-md font-medium transition-colors whitespace-nowrap',
              activeClassYear === c.value || (c.value === null && semesterFilter === null)
                ? 'bg-white shadow-sm text-gray-900'
                : 'text-gray-500 hover:text-gray-700',
            )}
          >
            {c.label}
          </button>
        ))}
      </div>

      <div className="w-px self-stretch bg-gray-200 mx-1" />

      {/* Dönem */}
      <span className="text-xs font-semibold text-gray-500 whitespace-nowrap">Dönem</span>
      <div className="flex rounded-lg bg-gray-100 p-0.5 gap-0.5 items-center">
        {TERMS.map(t => (
          <button
            key={t.label}
            onClick={() => handleTermChange(t.value as 'fall' | 'spring' | null)}
            className={cn(
              'h-6 px-2.5 text-[11px] rounded-md font-medium transition-colors whitespace-nowrap',
              activeTerm === t.value || (t.value === null && semesterFilter === null)
                ? 'bg-white shadow-sm text-gray-900'
                : 'text-gray-500 hover:text-gray-700',
            )}
          >
            {t.label}
          </button>
        ))}
      </div>

      {/* Type toggle — pushed right */}
      <div className="flex items-center ml-auto">
        <div className="flex rounded-lg bg-gray-100 p-0.5 gap-0.5">
          {TYPE_TABS.map(t => (
            <button
              key={t.value}
              onClick={() => onTypeChange(t.value)}
              className={cn(
                'h-6 px-3 text-[11px] rounded-md font-medium transition-colors',
                typeFilter === t.value
                  ? 'bg-white shadow-sm text-gray-900'
                  : 'text-gray-500 hover:text-gray-700',
              )}
            >
              {t.label}
            </button>
          ))}
        </div>
      </div>
    </div>
  );
}
