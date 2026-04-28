import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import { cn } from '@/lib/utils';
import type { AccessibleDepartment } from '../../types/academicOperations';
import type { FacultyDto, DepartmentDto, ProgramDto } from '../../hooks/useAdminAcademicStructure';
import type { TermSummaryDto } from '../../types/term';
import { AcademicHierarchyFilter } from './AcademicHierarchyFilter';

const CLASS_YEARS: { value: number | null; label: string }[] = [
  { value: null, label: 'Tümü' },
  { value: 1, label: '1' },
  { value: 2, label: '2' },
  { value: 3, label: '3' },
  { value: 4, label: '4' },
];

export interface OpenedCoursesPanelFiltersProps {
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

  termId: number | null;
  onTermChange: (id: number | null) => void;
  availableTerms: TermSummaryDto[];

  classYear: number | null;
  onClassYearChange: (year: number | null) => void;

  codeSearch: string;
  onCodeSearchChange: (v: string) => void;
  nameSearch: string;
  onNameSearchChange: (v: string) => void;

  isHistoricalTerm?: boolean;
}

export function OpenedCoursesPanelFilters(props: OpenedCoursesPanelFiltersProps) {
  return (
    <div className="space-y-2 px-3 py-2 bg-emerald-50/40 border-b">
      <div className="flex items-center gap-2 flex-wrap">
        <div className="flex items-center gap-1.5">
          <span className="text-xs font-semibold text-gray-500 whitespace-nowrap w-16">Dönem</span>
          <select
            className="h-7 rounded-md border border-gray-200 bg-gray-50 px-2 text-xs focus:ring-1 focus:ring-gray-300 focus:outline-none min-w-[160px]"
            value={props.termId ?? ''}
            onChange={(e) => props.onTermChange(e.target.value ? Number(e.target.value) : null)}
          >
            <option value="">Seçiniz</option>
            {props.availableTerms.map((t) => (
              <option key={t.id} value={t.id}>{t.name}</option>
            ))}
          </select>
          {props.isHistoricalTerm && (
            <Badge variant="outline" className="text-[10px] text-amber-700 border-amber-300 bg-amber-50">
              Geçmiş — sadece görüntüleme
            </Badge>
          )}
        </div>
      </div>

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
      />

      <div className="flex items-center gap-3 flex-wrap">
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
