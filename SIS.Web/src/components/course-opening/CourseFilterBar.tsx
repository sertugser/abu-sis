import { useMemo } from 'react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Download } from 'lucide-react';
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
  onExportCsv: () => void;
}

const SEMESTERS = [
  { value: null, label: 'Tümü' },
  { value: 1, label: '1' },
  { value: 2, label: '2' },
  { value: 3, label: '3' },
  { value: 4, label: '4' },
  { value: 5, label: '5' },
  { value: 6, label: '6' },
  { value: 7, label: '7' },
  { value: 8, label: '8' },
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
  stats,
  onExportCsv,
}: FilterBarProps) {
  // Map accessible department IDs to their faculty via structureDepartments
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

  return (
    <div className="space-y-2">
      {/* Filter Row */}
      <div className="flex flex-wrap items-center gap-2 text-xs">
        <div className="flex items-center gap-1.5">
          <label className="font-medium whitespace-nowrap">Fakülte:</label>
          <select
            className="h-7 rounded-md border border-input bg-background px-2 text-xs"
            value={selectedFacultyId ?? ''}
            onChange={e => {
              const val = e.target.value ? Number(e.target.value) : null;
              onFacultyChange(val);
            }}
          >
            <option value="">Tümü</option>
            {availableFaculties.map(f => (
              <option key={f.id} value={f.id}>{f.name}</option>
            ))}
          </select>
        </div>

        <div className="flex items-center gap-1.5">
          <label className="font-medium whitespace-nowrap">Bölüm:</label>
          <select
            className="h-7 rounded-md border border-input bg-background px-2 text-xs"
            value={selectedDeptId ?? ''}
            onChange={e => onDeptChange(Number(e.target.value))}
          >
            {filteredDepartments.map(d => (
              <option key={d.departmentId} value={d.departmentId}>
                {d.departmentCode} — {d.departmentName}
              </option>
            ))}
          </select>
        </div>

        <div className="flex items-center gap-1">
          <label className="font-medium whitespace-nowrap">Yarıyıl:</label>
          <div className="flex gap-0.5">
            {SEMESTERS.map(s => (
              <button
                key={s.label}
                onClick={() => onSemesterChange(s.value)}
                className={`h-6 px-1.5 text-[11px] rounded border transition-colors ${
                  semesterFilter === s.value
                    ? 'bg-primary text-primary-foreground border-primary'
                    : 'bg-background border-input hover:bg-accent'
                }`}
              >
                {s.label}
              </button>
            ))}
          </div>
        </div>

        <div className="flex items-center gap-1 ml-auto">
          <div className="flex gap-0.5 bg-muted rounded-lg p-0.5">
            {TYPE_TABS.map(t => (
              <button
                key={t.value}
                onClick={() => onTypeChange(t.value)}
                className={`h-6 px-2 text-[11px] rounded transition-colors ${
                  typeFilter === t.value
                    ? 'bg-background shadow-sm font-medium'
                    : 'hover:bg-background/50'
                }`}
              >
                {t.label}
              </button>
            ))}
          </div>
        </div>
      </div>

      {/* Stats Row */}
      <div className="flex flex-wrap items-center gap-2 bg-white border rounded-lg px-3 py-1.5">
        <div className="flex flex-wrap items-center gap-x-3 gap-y-1 text-xs flex-1">
          <span>
            Müfredat: <strong>{stats.curriculumCount}</strong> ders
          </span>
          <span className="text-muted-foreground hidden sm:inline">|</span>
          <span>
            Açılan: <strong className="text-emerald-700">{stats.openedCourseCount}</strong> ders / <strong>{stats.openedSectionCount}</strong> şube
          </span>
          <span className="text-muted-foreground hidden sm:inline">|</span>
          <span>
            Açılmamış: <Badge variant={stats.pendingCount > 0 ? 'outline' : 'secondary'} className="text-[10px] ml-0.5">{stats.pendingCount}</Badge>
          </span>
          <span className="text-muted-foreground hidden sm:inline">|</span>
          <span>
            Kontenjan: <strong>{stats.totalCapacity}</strong>
          </span>
          <span className="text-muted-foreground hidden sm:inline">|</span>
          <span>
            Talep: <strong className={stats.totalDemand > stats.totalCapacity ? 'text-red-600' : 'text-emerald-700'}>{stats.totalDemand}</strong>
          </span>
        </div>
        <Button size="sm" variant="outline" className="h-7 text-xs" onClick={onExportCsv}>
          <Download className="h-3 w-3 mr-1" /> CSV
        </Button>
      </div>
    </div>
  );
}
