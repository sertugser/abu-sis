import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import type { AccessibleDepartment } from '../../types/academicOperations';
import type { FacultyDto, DepartmentDto, ProgramDto } from '../../hooks/useAdminAcademicStructure';
import type { TermSummaryDto } from '../../types/term';
import { AcademicHierarchyFilter } from './AcademicHierarchyFilter';

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

const SELECT_CLASS =
  'h-7 rounded-md border border-gray-200 bg-white px-2 text-xs focus:ring-1 focus:ring-gray-300 focus:outline-none';

export function OpenedCoursesPanelFilters(props: OpenedCoursesPanelFiltersProps) {
  return (
    <div className="border-b border-gray-200">
      {/* Proliz tarzı başlık çubuğu */}
      <div className="bg-slate-600 text-white text-center text-xs font-semibold py-1.5 tracking-wide">
        Açılan Dersler
      </div>

      <div className="grid grid-cols-[80px_1fr] gap-x-3 gap-y-1.5 px-3 py-2.5 bg-slate-50/60">
        <span className="text-xs font-semibold text-gray-600 self-center">Dönem</span>
        <div className="flex items-center gap-2">
          <select
            className="h-7 flex-1 rounded-md border border-gray-200 bg-white px-2 text-xs focus:ring-1 focus:ring-gray-300 focus:outline-none"
            value={props.termId ?? ''}
            onChange={(e) => props.onTermChange(e.target.value ? Number(e.target.value) : null)}
          >
            <option value="">Seçiniz</option>
            {props.availableTerms.map((t) => (
              <option key={t.id} value={t.id}>{t.name}</option>
            ))}
          </select>
          {props.isHistoricalTerm && (
            <Badge variant="outline" className="text-[10px] text-amber-700 border-amber-300 bg-amber-50 whitespace-nowrap">
              Geçmiş — sadece görüntüleme
            </Badge>
          )}
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

        <span className="text-xs font-semibold text-gray-600 self-center">Sınıf</span>
        <select
          className={SELECT_CLASS + ' w-fit min-w-[100px]'}
          value={props.classYear ?? ''}
          onChange={(e) => props.onClassYearChange(e.target.value ? Number(e.target.value) : null)}
        >
          <option value="">Tümü</option>
          <option value="1">1. Sınıf</option>
          <option value="2">2. Sınıf</option>
          <option value="3">3. Sınıf</option>
          <option value="4">4. Sınıf</option>
        </select>

        <span className="text-xs font-semibold text-gray-600 self-center">Ders Kod</span>
        <div className="flex items-center gap-2">
          <Input
            value={props.codeSearch}
            onChange={(e) => props.onCodeSearchChange(e.target.value)}
            className="h-7 w-32 text-xs rounded-md"
            placeholder="örn. BLG101"
          />
          <span className="text-xs font-semibold text-gray-600 ml-2">Ders Ad</span>
          <Input
            value={props.nameSearch}
            onChange={(e) => props.onNameSearchChange(e.target.value)}
            className="h-7 flex-1 text-xs rounded-md"
            placeholder="ders adıyla ara..."
          />
        </div>
      </div>
    </div>
  );
}
