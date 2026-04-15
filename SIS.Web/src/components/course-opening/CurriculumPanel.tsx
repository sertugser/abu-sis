import { useMemo, useRef, useState } from 'react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import { Collapsible, CollapsibleContent, CollapsibleTrigger } from '@/components/ui/collapsible';
import { cn } from '@/lib/utils';
import { BookOpen, ChevronDown, ChevronRight, Search } from 'lucide-react';
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
  onOpenCourseDetail: (course: CurriculumCourseForOpeningDto) => void;
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
  onOpenCourseDetail,
}: Props) {
  const [search, setSearch] = useState('');
  const [collapsedSemesters, setCollapsedSemesters] = useState<Set<number>>(new Set());
  const scrollRef = useRef<HTMLDivElement>(null);
  const [showTopShadow, setShowTopShadow] = useState(false);
  const [showBottomShadow, setShowBottomShadow] = useState(true);

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
      if (next.has(sem)) next.delete(sem); else next.add(sem);
      return next;
    });
  };

  const handleScroll = () => {
    const el = scrollRef.current;
    if (!el) return;
    setShowTopShadow(el.scrollTop > 4);
    setShowBottomShadow(el.scrollTop + el.clientHeight < el.scrollHeight - 4);
  };

  const selectableCount = selectedIds.size;

  return (
    <div className="flex flex-col min-h-0 h-full overflow-hidden">
      {/* Header — fixed */}
      <div className="shrink-0 bg-white border-b px-4 py-3 space-y-2.5 z-10">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            <div className="flex items-center justify-center w-6 h-6 rounded-md bg-blue-50">
              <BookOpen className="h-3.5 w-3.5 text-blue-600" />
            </div>
            <h2 className="font-semibold text-sm text-gray-800">Müfredat Dersleri</h2>
            <Badge variant="secondary" className="text-xs rounded-full">{filteredCourses.length}</Badge>
          </div>
        </div>
        <div className="relative">
          <Search className="absolute left-2.5 top-1/2 -translate-y-1/2 h-3.5 w-3.5 text-gray-400 pointer-events-none" />
          <Input
            placeholder="Ders kodu veya adı ile ara..."
            value={search}
            onChange={e => setSearch(e.target.value)}
            className="pl-8 h-8 text-xs rounded-lg"
          />
        </div>
      </div>

      {/* Scrollable content */}
      <div className="relative flex-1 min-h-0">
        {/* Top scroll shadow */}
        <div className={cn(
          'absolute top-0 left-0 right-0 h-3 bg-gradient-to-b from-black/[0.06] to-transparent z-[5] pointer-events-none transition-opacity',
          showTopShadow ? 'opacity-100' : 'opacity-0',
        )} />

        <div
          ref={scrollRef}
          onScroll={handleScroll}
          className="h-full overflow-y-auto overscroll-contain"
        >
          {isLoading ? (
            <div className="p-4 space-y-3">
              {Array.from({ length: 6 }).map((_, i) => <Skeleton key={i} className="h-12 rounded-lg" />)}
            </div>
          ) : grouped.length === 0 ? (
            <div className="flex flex-col items-center py-16 text-gray-400">
              <Search className="h-8 w-8 mb-2" />
              <p className="text-sm">Eşleşen ders bulunamadı.</p>
            </div>
          ) : (
            grouped.map(([semester, semCourses]) => {
              const isCollapsed = collapsedSemesters.has(semester);
              const openedCount = semCourses.filter(c => c.alreadyOpened).length;
              return (
                <Collapsible key={semester} open={!isCollapsed} onOpenChange={() => toggleCollapse(semester)}>
                  <CollapsibleTrigger asChild>
                    <button className="w-full flex items-center justify-between bg-gray-50/80 px-4 py-2 border-b hover:bg-gray-100/80 transition-colors">
                      <div className="flex items-center gap-2">
                        {isCollapsed
                          ? <ChevronRight className="h-3.5 w-3.5 text-gray-400" />
                          : <ChevronDown className="h-3.5 w-3.5 text-gray-400" />
                        }
                        <span className="text-xs font-semibold text-gray-700">
                          {getSemesterLabel(semester)}
                        </span>
                      </div>
                      <div className="flex items-center gap-2">
                        <span className="text-xs text-gray-500 tabular-nums">{openedCount}/{semCourses.length}</span>
                        <div className="w-14 h-1.5 bg-gray-200 rounded-full overflow-hidden">
                          <div
                            className="h-full bg-emerald-500 rounded-full transition-all"
                            style={{ width: `${semCourses.length > 0 ? (openedCount / semCourses.length) * 100 : 0}%` }}
                          />
                        </div>
                      </div>
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
                        onOpenDetail={onOpenCourseDetail}
                      />
                    ))}
                  </CollapsibleContent>
                </Collapsible>
              );
            })
          )}
        </div>

        {/* Bottom scroll shadow */}
        <div className={cn(
          'absolute bottom-0 left-0 right-0 h-3 bg-gradient-to-t from-black/[0.06] to-transparent z-[5] pointer-events-none transition-opacity',
          showBottomShadow ? 'opacity-100' : 'opacity-0',
        )} />
      </div>

      {/* Bottom action bar — fixed */}
      <div className={cn(
        'shrink-0 border-t px-4 py-2.5 flex items-center justify-between transition-colors z-10',
        selectableCount > 0 ? 'bg-blue-50 border-blue-200' : 'bg-white',
      )}>
        <span className={cn('text-xs', selectableCount > 0 ? 'text-blue-700 font-medium' : 'text-gray-500')}>
          <strong>{selectableCount}</strong> ders seçili
        </span>
        <Button size="sm" disabled={selectableCount === 0} className="rounded-lg gap-1.5" onClick={onOpenSelected}>
          Seçilenleri Aç
        </Button>
      </div>
    </div>
  );
}
