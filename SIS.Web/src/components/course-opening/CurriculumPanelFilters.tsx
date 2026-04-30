import { Input } from '@/components/ui/input';
import type { AccessibleDepartment } from '../../types/academicOperations';
import type { FacultyDto, DepartmentDto, ProgramDto } from '../../hooks/useAdminAcademicStructure';
import { AcademicHierarchyFilter } from './AcademicHierarchyFilter';

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
  semester: 'fall' | 'spring' | 'summer' | null;
  onSemesterChange: (s: 'fall' | 'spring' | 'summer' | null) => void;

  includeUnmapped: boolean;
  onIncludeUnmappedChange: (v: boolean) => void;

  codeSearch: string;
  onCodeSearchChange: (v: string) => void;
  nameSearch: string;
  onNameSearchChange: (v: string) => void;
}

const SELECT_CLASS =
  'h-7 rounded-md border border-gray-200 bg-white px-2 text-xs focus:ring-1 focus:ring-gray-300 focus:outline-none';

export function CurriculumPanelFilters(props: CurriculumPanelFiltersProps) {
  return (
    <div className="border-b border-gray-200">
      {/* Proliz tarzı başlık çubuğu */}
      <div className="bg-slate-600 text-white text-center text-xs font-semibold py-1.5 tracking-wide">
        Referans Alınacak Müfredat Dersleri
      </div>

      {/* 2-sütun grid: label | input */}
      <div className="grid grid-cols-[80px_1fr] gap-x-3 gap-y-1.5 px-3 py-2.5 bg-slate-50/60">
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

        <span className="text-xs font-semibold text-gray-600 self-center">Dönem/Sınıf</span>
        <div className="flex items-center gap-2">
          <select
            className={SELECT_CLASS + ' min-w-[80px]'}
            value={props.semester ?? ''}
            onChange={(e) => {
              const v = e.target.value;
              props.onSemesterChange(
                v === 'fall' ? 'fall' : v === 'spring' ? 'spring' : v === 'summer' ? 'summer' : null,
              );
            }}
          >
            <option value="">Seç</option>
            <option value="fall">Güz</option>
            <option value="spring">Bahar</option>
            <option value="summer">Yaz</option>
          </select>
          <span className="text-xs text-gray-400 select-none">/</span>
          <select
            className={SELECT_CLASS + ' min-w-[80px]'}
            value={props.classYear ?? ''}
            onChange={(e) => props.onClassYearChange(e.target.value ? Number(e.target.value) : null)}
          >
            <option value="">Tümü</option>
            <option value="1">1. Sınıf</option>
            <option value="2">2. Sınıf</option>
            <option value="3">3. Sınıf</option>
            <option value="4">4. Sınıf</option>
          </select>
          <label className="flex items-center gap-1.5 cursor-pointer ml-auto">
            <input
              type="checkbox"
              checked={props.includeUnmapped}
              onChange={(e) => props.onIncludeUnmappedChange(e.target.checked)}
              className="h-3.5 w-3.5 rounded border-gray-300 text-blue-600 focus:ring-1 focus:ring-blue-300 cursor-pointer"
            />
            <span className="text-xs text-gray-700 select-none">İntibakı Yapılmayanlar</span>
          </label>
        </div>

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
