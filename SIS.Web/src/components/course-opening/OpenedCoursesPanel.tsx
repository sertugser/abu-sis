import { useMemo, useRef, useState, type ReactNode } from 'react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { cn } from '@/lib/utils';
import { Download, ExternalLink, Layers, Search, Users } from 'lucide-react';
import type { OpenedCourseSectionDto, InstructorOptionDto } from '../../types/courseOpening';
import type { CourseDemandForecastDto } from '../../types/forecast';
import type { InstructorWorkloadDto } from '../../types/courseOpening';
import type { ExternalCourseDto } from '../../api/courseOpening.api';
import { OpenedCourseRow } from './OpenedCourseRow';

type SortField = 'code' | 'sections' | 'capacity' | 'enrolled' | 'demand';
type SortDir = 'asc' | 'desc';

interface Props {
  sections: OpenedCourseSectionDto[] | undefined;
  isLoading: boolean;
  termId: number;
  forecastMap: Map<number, CourseDemandForecastDto>;
  instructors: InstructorOptionDto[];

  // Parent'tan gelen filtreler
  classYear: number | null;
  codeSearch: string;
  nameSearch: string;

  onUpdateSection: (sectionId: number, capacity: number, instructorId: number | undefined, internalQuota?: number | null, externalQuota?: number | null) => void;
  onCloseSection: (sectionId: number) => void;
  onCloseAllSections: (courseId: number) => Promise<unknown>;
  onReactivateSection: (sectionId: number) => Promise<unknown>;
  onAddSection: (courseId: number, capacity: number, instructorId: number | undefined, internalQuota?: number, externalQuota?: number) => void;
  updatePending: boolean;
  closePending: boolean;
  closeError: Error | null;
  workloadData: InstructorWorkloadDto | undefined;
  onInstructorSelect: (id: number | null) => void;
  onExportCsv: () => void;

  // Filter slot — parent'tan render edilen filter bar
  filtersSlot?: ReactNode;
  // Dış dersler — drawer açma
  externalCourses?: ExternalCourseDto[];
  onOpenExternalDrawer?: () => void;
  // Geçmiş dönem read-only
  readOnly?: boolean;
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
  termId,
  forecastMap,
  instructors,
  classYear,
  codeSearch,
  nameSearch,
  onUpdateSection,
  onCloseSection,
  onCloseAllSections,
  onReactivateSection,
  onAddSection,
  updatePending,
  closePending,
  closeError,
  workloadData,
  onInstructorSelect,
  onExportCsv,
  filtersSlot,
  externalCourses,
  onOpenExternalDrawer,
  readOnly = false,
}: Props) {
  const [sortField, setSortField] = useState<SortField>('code');
  const [sortDir, setSortDir] = useState<SortDir>('asc');
  const scrollRef = useRef<HTMLDivElement>(null);
  const [showTopShadow, setShowTopShadow] = useState(false);
  const [showBottomShadow, setShowBottomShadow] = useState(true);

  const toggleSort = (field: SortField) => {
    if (sortField === field) setSortDir(d => d === 'asc' ? 'desc' : 'asc');
    else { setSortField(field); setSortDir('asc'); }
  };

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
    let result = courseGroups;
    const code = codeSearch.trim().toLowerCase();
    const name = nameSearch.trim().toLowerCase();
    if (code) result = result.filter((g) => g.courseCode.toLowerCase().includes(code));
    if (name) result = result.filter((g) => g.courseName.toLowerCase().includes(name));
    // Sınıf filtresi: ders kodundaki ilk rakamdan sınıf çıkar (BLG101 → 1, BLG201 → 2 vb.)
    // Mükemmel değil ama Proliz konvansiyonuna yakın
    if (classYear !== null) {
      result = result.filter((g) => {
        const match = g.courseCode.match(/(\d)\d{2}/);
        if (!match) return true;
        return Number(match[1]) === classYear;
      });
    }
    const dir = sortDir === 'asc' ? 1 : -1;
    result = [...result].sort((a, b) => {
      switch (sortField) {
        case 'code': return a.courseCode.localeCompare(b.courseCode) * dir;
        case 'sections': return (a.sections.length - b.sections.length) * dir;
        case 'capacity': return (a.sections.reduce((s, x) => s + x.capacity, 0) - b.sections.reduce((s, x) => s + x.capacity, 0)) * dir;
        case 'enrolled': return (a.sections.reduce((s, x) => s + x.enrolledCount, 0) - b.sections.reduce((s, x) => s + x.enrolledCount, 0)) * dir;
        case 'demand': {
          const aDemand = forecastMap.get(a.courseId)?.totalPredictedDemand ?? 0;
          const bDemand = forecastMap.get(b.courseId)?.totalPredictedDemand ?? 0;
          return (aDemand - bDemand) * dir;
        }
        default: return 0;
      }
    });
    return result;
  }, [courseGroups, codeSearch, nameSearch, classYear, sortField, sortDir, forecastMap]);

  const totalSections = sections?.length ?? 0;
  const totalCapacity = (sections ?? []).reduce((s, sec) => s + sec.capacity, 0);
  const totalEnrolled = (sections ?? []).reduce((s, sec) => s + sec.enrolledCount, 0);

  const handleScroll = () => {
    const el = scrollRef.current;
    if (!el) return;
    setShowTopShadow(el.scrollTop > 4);
    setShowBottomShadow(el.scrollTop + el.clientHeight < el.scrollHeight - 4);
  };

  return (
    <div className="flex flex-col min-h-0 h-full overflow-hidden">
      {/* Filtreler — parent'tan render edilen filter bar */}
      {filtersSlot && <div className="shrink-0">{filtersSlot}</div>}

      {/* Başlık + sıralama — fixed */}
      <div className="shrink-0 bg-white border-b px-4 py-2 space-y-2 z-10">
        <div className="flex items-center justify-between gap-2">
          <div className="flex items-center gap-2">
            <h2 className="font-semibold text-sm text-gray-800">Açılan Dersler</h2>
            <Badge variant="secondary" className="text-xs rounded-full">{filtered.length} ders</Badge>
            <Badge variant="outline" className="text-xs rounded-full">{totalSections} şube</Badge>
          </div>
          {externalCourses && externalCourses.length > 0 && onOpenExternalDrawer && (
            <Button
              size="sm"
              variant="ghost"
              className="h-7 px-2 text-xs gap-1.5 text-violet-700 hover:bg-violet-50"
              onClick={onOpenExternalDrawer}
              title="Bölümünüzün müfredatında olan ama başka bölüme ait dersler"
            >
              <ExternalLink className="h-3.5 w-3.5" />
              Dış Dersler ({externalCourses.length})
            </Button>
          )}
        </div>
        <div className="flex items-center gap-1 flex-wrap">
          <span className="text-[11px] text-gray-400 mr-1">Sırala:</span>
          {([['code', 'Kod'], ['sections', 'Şube'], ['capacity', 'Kont.'], ['enrolled', 'Kayıt'], ['demand', 'Talep']] as [SortField, string][]).map(([field, label]) => (
            <button
              key={field}
              onClick={() => toggleSort(field)}
              className={cn(
                'h-6 px-2 text-[11px] rounded-md font-medium transition-colors',
                sortField === field ? 'bg-gray-900 text-white' : 'text-gray-500 hover:bg-gray-100',
              )}
            >
              {label}{sortField === field && (sortDir === 'asc' ? ' ↑' : ' ↓')}
            </button>
          ))}
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
          ) : filtered.length === 0 ? (
            <div className="flex flex-col items-center py-16 text-gray-400">
              {courseGroups.length === 0 ? (
                <>
                  <Layers className="h-8 w-8 mb-2" />
                  <p className="text-sm">Henüz ders açılmamış.</p>
                  <p className="text-xs mt-1">Sol panelden müfredat derslerini seçip açabilirsiniz.</p>
                </>
              ) : (
                <>
                  <Search className="h-8 w-8 mb-2" />
                  <p className="text-sm">Eşleşen açılan ders bulunamadı.</p>
                </>
              )}
            </div>
          ) : (
            filtered.map(group => (
              <OpenedCourseRow
                key={group.courseId}
                courseCode={group.courseCode}
                courseName={group.courseName}
                courseId={group.courseId}
                termId={termId}
                sections={group.sections}
                forecast={forecastMap.get(group.courseId)}
                instructors={instructors}
                onUpdateSection={readOnly ? () => {} : onUpdateSection}
                onCloseSection={(sec) => readOnly ? undefined : onCloseSection(sec.sectionId)}
                onCloseAllSections={readOnly ? () => Promise.resolve() : onCloseAllSections}
                onReactivateSection={readOnly ? () => Promise.resolve() : onReactivateSection}
                onAddSection={readOnly ? () => {} : onAddSection}
                updatePending={updatePending}
                closePending={closePending}
                closeError={closeError}
                workloadData={workloadData}
                onInstructorSelect={onInstructorSelect}
              />
            ))
          )}
        </div>

        {/* Bottom scroll shadow */}
        <div className={cn(
          'absolute bottom-0 left-0 right-0 h-3 bg-gradient-to-t from-black/[0.06] to-transparent z-[5] pointer-events-none transition-opacity',
          showBottomShadow ? 'opacity-100' : 'opacity-0',
        )} />
      </div>

      {/* Bottom bar — fixed */}
      <div className="shrink-0 bg-white border-t px-4 py-2.5 flex items-center justify-between z-10">
        <div className="flex items-center gap-4 text-xs text-gray-500">
          <span className="flex items-center gap-1">
            <Layers className="h-3 w-3" />
            <strong className="text-gray-700">{totalSections}</strong> şube
          </span>
          <span className="flex items-center gap-1">
            <Users className="h-3 w-3" />
            <strong className="text-gray-700">{totalCapacity}</strong> kontenjan
          </span>
          <span>
            <strong className={cn('tabular-nums', totalEnrolled > 0 ? 'text-gray-700' : 'text-gray-400')}>{totalEnrolled}</strong> kayıtlı
          </span>
        </div>
        <Button size="sm" variant="outline" className="h-7 text-xs gap-1.5 rounded-lg" onClick={onExportCsv}>
          <Download className="h-3 w-3" /> CSV
        </Button>
      </div>
    </div>
  );
}
