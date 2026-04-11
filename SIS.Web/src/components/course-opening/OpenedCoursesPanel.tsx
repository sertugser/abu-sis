import { useMemo, useState } from 'react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import { Download, Search } from 'lucide-react';
import type { OpenedCourseSectionDto, InstructorOptionDto } from '../../types/courseOpening';
import type { CourseDemandForecastDto } from '../../types/forecast';
import type { InstructorWorkloadDto } from '../../types/courseOpening';
import { OpenedCourseRow } from './OpenedCourseRow';

interface Props {
  sections: OpenedCourseSectionDto[] | undefined;
  isLoading: boolean;
  forecastMap: Map<number, CourseDemandForecastDto>;
  instructors: InstructorOptionDto[];
  onUpdateSection: (sectionId: number, capacity: number, instructorId: number | undefined, internalQuota?: number | null, externalQuota?: number | null) => void;
  onCloseSection: (sectionId: number) => void;
  onAddSection: (courseId: number, capacity: number, instructorId: number | undefined, internalQuota?: number, externalQuota?: number) => void;
  updatePending: boolean;
  closePending: boolean;
  closeError: Error | null;
  workloadData: InstructorWorkloadDto | undefined;
  onInstructorSelect: (id: number | null) => void;
  onExportCsv: () => void;
}

interface CourseGroup {
  courseId: number;
  courseCode: string;
  courseName: string;
  sections: OpenedCourseSectionDto[];
}

export function OpenedCoursesPanel({
  sections,
  isLoading,
  forecastMap,
  instructors,
  onUpdateSection,
  onCloseSection,
  onAddSection,
  updatePending,
  closePending,
  closeError,
  workloadData,
  onInstructorSelect,
  onExportCsv,
}: Props) {
  const [search, setSearch] = useState('');

  const courseGroups: CourseGroup[] = useMemo(() => {
    if (!sections) return [];
    const map = new Map<number, CourseGroup>();
    for (const sec of sections) {
      if (!map.has(sec.courseId)) {
        map.set(sec.courseId, {
          courseId: sec.courseId,
          courseCode: sec.courseCode,
          courseName: sec.courseName,
          sections: [],
        });
      }
      map.get(sec.courseId)!.sections.push(sec);
    }
    const groups = [...map.values()];
    groups.sort((a, b) => a.courseCode.localeCompare(b.courseCode));
    return groups;
  }, [sections]);

  const filtered = useMemo(() => {
    if (!search) return courseGroups;
    const q = search.toLowerCase();
    return courseGroups.filter(
      g => g.courseCode.toLowerCase().includes(q) || g.courseName.toLowerCase().includes(q),
    );
  }, [courseGroups, search]);

  const totalSections = sections?.length ?? 0;
  const totalCapacity = (sections ?? []).reduce((s, sec) => s + sec.capacity, 0);
  const totalEnrolled = (sections ?? []).reduce((s, sec) => s + sec.enrolledCount, 0);

  return (
    <div className="flex flex-col h-full">
      {/* Header */}
      <div className="sticky top-0 z-10 bg-white border-b px-4 py-3 space-y-2">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            <h2 className="font-semibold text-sm">Açılan Dersler</h2>
            <Badge variant="secondary" className="text-[10px]">{filtered.length} ders</Badge>
            <Badge variant="outline" className="text-[10px]">{totalSections} şube</Badge>
          </div>
        </div>
        <div className="relative">
          <Search className="absolute left-2.5 top-1/2 -translate-y-1/2 h-3.5 w-3.5 text-muted-foreground" />
          <Input
            placeholder="Açılan derslerde ara..."
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
        ) : filtered.length === 0 ? (
          <div className="text-center py-12 text-muted-foreground text-sm">
            {courseGroups.length === 0 ? 'Henüz ders açılmamış.' : 'Eşleşen açılan ders bulunamadı.'}
          </div>
        ) : (
          filtered.map(group => (
            <OpenedCourseRow
              key={group.courseId}
              courseCode={group.courseCode}
              courseName={group.courseName}
              courseId={group.courseId}
              sections={group.sections}
              forecast={forecastMap.get(group.courseId)}
              instructors={instructors}
              onUpdateSection={onUpdateSection}
              onCloseSection={(sec) => onCloseSection(sec.sectionId)}
              onAddSection={onAddSection}
              updatePending={updatePending}
              closePending={closePending}
              closeError={closeError}
              workloadData={workloadData}
              onInstructorSelect={onInstructorSelect}
            />
          ))
        )}
      </div>

      {/* Bottom bar */}
      <div className="sticky bottom-0 bg-white border-t px-4 py-2.5 flex items-center justify-between">
        <div className="flex items-center gap-3 text-sm text-muted-foreground">
          <span><strong>{totalSections}</strong> şube</span>
          <span><strong>{totalCapacity}</strong> kontenjan</span>
          <span><strong>{totalEnrolled}</strong> kayıtlı</span>
        </div>
        <Button size="sm" variant="outline" onClick={onExportCsv}>
          <Download className="h-3.5 w-3.5 mr-1" /> CSV
        </Button>
      </div>
    </div>
  );
}
