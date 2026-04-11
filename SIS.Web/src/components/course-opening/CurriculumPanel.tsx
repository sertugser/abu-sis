import { useMemo, useState } from 'react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import { Collapsible, CollapsibleContent, CollapsibleTrigger } from '@/components/ui/collapsible';
import { ChevronDown, ChevronRight, Search } from 'lucide-react';
import type { CurriculumCourseForOpeningDto } from '../../types/courseOpening';
import type { CourseDemandForecastDto } from '../../types/forecast';
import { CurriculumCourseRow } from './CurriculumCourseRow';

interface Props {
  courses: CurriculumCourseForOpeningDto[] | undefined;
  isLoading: boolean;
  forecastMap: Map<number, CourseDemandForecastDto>;
  semesterFilter: number | null;
  typeFilter: 'all' | 'compulsory' | 'elective';
  selectedIds: Set<number>;
  onToggleSelect: (courseId: number) => void;
  onOpenSelected: () => void;
}

function getSemesterLabel(semester: number) {
  if (semester === 0) return 'Diğer Dersler';
  const year = Math.ceil(semester / 2);
  const isFall = semester % 2 === 1;
  return `${year}. Sınıf ${isFall ? 'Güz' : 'Bahar'}`;
}

export function CurriculumPanel({
  courses,
  isLoading,
  forecastMap,
  semesterFilter,
  typeFilter,
  selectedIds,
  onToggleSelect,
  onOpenSelected,
}: Props) {
  const [search, setSearch] = useState('');
  const [collapsedSemesters, setCollapsedSemesters] = useState<Set<number>>(new Set());

  const filteredCourses = useMemo(() => {
    if (!courses) return [];
    return courses.filter(c => {
      if (semesterFilter !== null && c.suggestedSemester !== semesterFilter) return false;
      if (typeFilter === 'compulsory' && !c.isCompulsory) return false;
      if (typeFilter === 'elective' && c.isCompulsory) return false;
      if (search) {
        const q = search.toLowerCase();
        if (!c.courseCode.toLowerCase().includes(q) && !c.courseName.toLowerCase().includes(q)) return false;
      }
      return true;
    });
  }, [courses, semesterFilter, typeFilter, search]);

  const grouped = useMemo(() => {
    const map = new Map<number, CurriculumCourseForOpeningDto[]>();
    for (const c of filteredCourses) {
      const sem = c.suggestedSemester || 0;
      if (!map.has(sem)) map.set(sem, []);
      map.get(sem)!.push(c);
    }
    return [...map.entries()].sort((a, b) => a[0] - b[0]);
  }, [filteredCourses]);

  const toggleCollapse = (sem: number) => {
    setCollapsedSemesters(prev => {
      const next = new Set(prev);
      if (next.has(sem)) next.delete(sem);
      else next.add(sem);
      return next;
    });
  };

  const selectableCount = selectedIds.size;

  return (
    <div className="flex flex-col h-full">
      {/* Header */}
      <div className="sticky top-0 z-10 bg-white border-b px-4 py-3 space-y-2">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            <h2 className="font-semibold text-sm">Müfredat Dersleri</h2>
            <Badge variant="secondary" className="text-[10px]">{filteredCourses.length}</Badge>
          </div>
        </div>
        <div className="relative">
          <Search className="absolute left-2.5 top-1/2 -translate-y-1/2 h-3.5 w-3.5 text-muted-foreground" />
          <Input
            placeholder="Ders kodu veya adı ile ara..."
            value={search}
            onChange={e => setSearch(e.target.value)}
            className="pl-8 h-8 text-sm"
          />
        </div>
      </div>

      {/* Content */}
      <div className="flex-1 overflow-y-auto">
        {isLoading ? (
          <div className="p-4 space-y-3">
            <Skeleton className="h-10" />
            <Skeleton className="h-10" />
            <Skeleton className="h-10" />
          </div>
        ) : grouped.length === 0 ? (
          <div className="text-center py-12 text-muted-foreground text-sm">
            Eşleşen ders bulunamadı.
          </div>
        ) : (
          grouped.map(([semester, semCourses]) => {
            const isCollapsed = collapsedSemesters.has(semester);
            return (
              <Collapsible key={semester} open={!isCollapsed} onOpenChange={() => toggleCollapse(semester)}>
                <CollapsibleTrigger asChild>
                  <button className="w-full flex items-center justify-between bg-gray-50 px-4 py-2 border-b hover:bg-gray-100 transition-colors">
                    <div className="flex items-center gap-2">
                      {isCollapsed ? (
                        <ChevronRight className="h-3.5 w-3.5 text-muted-foreground" />
                      ) : (
                        <ChevronDown className="h-3.5 w-3.5 text-muted-foreground" />
                      )}
                      <span className="text-xs font-semibold text-gray-700">
                        {getSemesterLabel(semester)}
                      </span>
                    </div>
                    <Badge variant="outline" className="text-[10px]">{semCourses.length}</Badge>
                  </button>
                </CollapsibleTrigger>
                <CollapsibleContent>
                  {semCourses.map(course => (
                    <CurriculumCourseRow
                      key={course.courseId}
                      course={course}
                      forecast={forecastMap.get(course.courseId)}
                      selected={selectedIds.has(course.courseId)}
                      onToggle={onToggleSelect}
                    />
                  ))}
                </CollapsibleContent>
              </Collapsible>
            );
          })
        )}
      </div>

      {/* Bottom Action Bar */}
      <div className="sticky bottom-0 bg-white border-t px-4 py-2.5 flex items-center justify-between">
        <span className="text-sm text-muted-foreground">
          <strong>{selectableCount}</strong> ders seçili
        </span>
        <Button size="sm" disabled={selectableCount === 0} onClick={onOpenSelected}>
          Seçilenleri Aç
        </Button>
      </div>
    </div>
  );
}
