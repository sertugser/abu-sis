import { Input } from '@/components/ui/input';
import { Checkbox } from '@/components/ui/checkbox';
import { cn } from '@/lib/utils';
import type { AccessibleDepartment } from '../../types/academicOperations';
import type { FacultyDto, DepartmentDto, ProgramDto } from '../../hooks/useAdminAcademicStructure';
import { AcademicHierarchyFilter } from './AcademicHierarchyFilter';

const CLASS_YEARS: { value: number | null; label: string }[] = [
  { value: null, label: 'Tümü' },
  { value: 1, label: '1' },
  { value: 2, label: '2' },
  { value: 3, label: '3' },
  { value: 4, label: '4' },
];

const SEMESTERS: { value: 'fall' | 'spring' | null; label: string }[] = [
  { value: null, label: 'Tümü' },
  { value: 'fall', label: 'Güz' },
  { value: 'spring', label: 'Bahar' },
];

export interface CurriculumPanelFiltersProps {
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
  curriculumId: number | null;
  onCurriculumChange: (id: number | null) => void;

  classYear: number | null;
  onClassYearChange: (year: number | null) => void;
  semester: 'fall' | 'spring' | null;
  onSemesterChange: (s: 'fall' | 'spring' | null) => void;

  includeUnmapped: boolean;
  onIncludeUnmappedChange: (v: boolean) => void;

  codeSearch: string;
  onCodeSearchChange: (v: string) => void;
  nameSearch: string;
  onNameSearchChange: (v: string) => void;
}

export function CurriculumPanelFilters(props: CurriculumPanelFiltersProps) {
  return (
    <div className="space-y-2 px-3 py-2 bg-blue-50/40 border-b">
      <AcademicHierarchyFilter
        faculties={props.faculties}
        structureDepartments={props.structureDepartments}
        accessibleDepartments={props.accessibleDepartments}
        programs={props.programs}
        facultyId={props.facultyId}
        onFacultyChange={props.onFacultyChange}
        deptId={props.deptId}
        onDeptChange={props.onDeptChange}
        programId={props.programId}
        onProgramChange={props.onProgramChange}
        curriculumId={props.curriculumId}
        onCurriculumChange={props.onCurriculumChange}
        showCurriculum
      />

      <div className="flex items-center gap-3 flex-wrap">
        <div className="flex items-center gap-1.5">
          <span className="text-xs font-semibold text-gray-500 whitespace-nowrap w-16">Dönem</span>
          <div className="flex rounded-lg bg-gray-100 p-0.5 gap-0.5">
            {SEMESTERS.map((s) => (
              <button
                key={s.label}
                onClick={() => props.onSemesterChange(s.value)}
                className={cn(
                  'h-6 px-2.5 text-[11px] rounded-md font-medium transition-colors whitespace-nowrap',
                  props.semester === s.value
                    ? 'bg-white shadow-sm text-gray-900'
                    : 'text-gray-500 hover:text-gray-700',
                )}
              >
                {s.label}
              </button>
            ))}
          </div>
        </div>

        <div className="flex items-center gap-1.5">
          <span className="text-xs font-semibold text-gray-500 whitespace-nowrap w-12">Sınıf</span>
          <div className="flex rounded-lg bg-gray-100 p-0.5 gap-0.5">
            {CLASS_YEARS.map((y) => (
              <button
                key={y.label}
                onClick={() => props.onClassYearChange(y.value)}
                className={cn(
                  'h-6 px-2.5 text-[11px] rounded-md font-medium transition-colors',
                  props.classYear === y.value
                    ? 'bg-white shadow-sm text-gray-900'
                    : 'text-gray-500 hover:text-gray-700',
                )}
              >
                {y.label}
              </button>
            ))}
          </div>
        </div>

        <label className="flex items-center gap-1.5 cursor-pointer">
          <Checkbox
            checked={props.includeUnmapped}
            onCheckedChange={(v) => props.onIncludeUnmappedChange(v === true)}
          />
          <span className="text-xs text-gray-700">İntibakı Yapılmayanlar</span>
        </label>
      </div>

      <div className="flex items-center gap-2 flex-wrap">
        <div className="flex items-center gap-1.5">
          <span className="text-xs font-semibold text-gray-500 whitespace-nowrap w-16">Ders Kod</span>
          <Input
            value={props.codeSearch}
            onChange={(e) => props.onCodeSearchChange(e.target.value)}
            className="h-7 w-32 text-xs rounded-md"
            placeholder="örn. BLG101"
          />
        </div>
        <div className="flex items-center gap-1.5">
          <span className="text-xs font-semibold text-gray-500 whitespace-nowrap w-14">Ders Ad</span>
          <Input
            value={props.nameSearch}
            onChange={(e) => props.onNameSearchChange(e.target.value)}
            className="h-7 flex-1 min-w-[140px] text-xs rounded-md"
            placeholder="ders adıyla ara..."
          />
        </div>
      </div>
    </div>
  );
}
