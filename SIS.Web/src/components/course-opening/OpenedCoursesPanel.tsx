import { useMemo, useRef, useState } from 'react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import { cn } from '@/lib/utils';
import { AlertTriangle, Download, ExternalLink, Layers, Search, Users } from 'lucide-react';
import type { OpenedCourseSectionDto, InstructorOptionDto } from '../../types/courseOpening';
import type { CourseDemandForecastDto } from '../../types/forecast';
import type { InstructorWorkloadDto } from '../../types/courseOpening';
import type { ExternalCourseDto } from '../../api/courseOpening.api';
import { OpenedCourseRow } from './OpenedCourseRow';

type SortField = 'code' | 'sections' | 'capacity' | 'enrolled' | 'demand';
type SortDir = 'asc' | 'desc';
type ViewTab = 'own' | 'external';

interface Props {
  sections: OpenedCourseSectionDto[] | undefined;
  isLoading: boolean;
  termId: number;
  forecastMap: Map<number, CourseDemandForecastDto>;
  instructors: InstructorOptionDto[];
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
  externalCourses?: ExternalCourseDto[];
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
  externalCourses,
}: Props) {
  const [search, setSearch] = useState('');
  const [sortField, setSortField] = useState<SortField>('code');
  const [sortDir, setSortDir] = useState<SortDir>('asc');
  const [viewTab, setViewTab] = useState<ViewTab>('own');
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
    if (search) {
      const q = search.toLowerCase();
      result = result.filter(
        g => g.courseCode.toLowerCase().includes(q) || g.courseName.toLowerCase().includes(q),
      );
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
  }, [courseGroups, search, sortField, sortDir, forecastMap]);

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
      {/* Header — fixed */}
      <div className="shrink-0 bg-white border-b px-4 py-3 space-y-2.5 z-10">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            <h2 className="font-semibold text-sm text-gray-800">Açılan Dersler</h2>
            <Badge variant="secondary" className="text-xs rounded-full">{filtered.length} ders</Badge>
            <Badge variant="outline" className="text-xs rounded-full">{totalSections} şube</Badge>
          </div>
          <div className="flex rounded-lg border border-gray-200 bg-gray-100 p-0.5">
            <button
              onClick={() => setViewTab('own')}
              className={cn(
                'h-6 px-2.5 text-[11px] rounded-md font-medium transition-colors',
                viewTab === 'own' ? 'bg-white shadow-sm text-gray-900' : 'text-gray-500 hover:text-gray-700',
              )}
            >
              Bölüm Dersleri
            </button>
            <button
              onClick={() => setViewTab('external')}
              className={cn(
                'h-6 px-2.5 text-[11px] rounded-md font-medium transition-colors',
                viewTab === 'external' ? 'bg-white shadow-sm text-gray-900' : 'text-gray-500 hover:text-gray-700',
              )}
            >
              Dış Dersler{externalCourses && externalCourses.length > 0 && ` (${externalCourses.length})`}
            </button>
          </div>
        </div>
        <div className="relative">
          <Search className="absolute left-2.5 top-1/2 -translate-y-1/2 h-3.5 w-3.5 text-gray-400 pointer-events-none" />
          <Input
            placeholder="Açılan derslerde ara..."
            value={search}
            onChange={e => setSearch(e.target.value)}
            className="pl-8 h-8 text-xs rounded-lg"
          />
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
          {viewTab === 'own' ? (
            isLoading ? (
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
                  onUpdateSection={onUpdateSection}
                  onCloseSection={(sec) => onCloseSection(sec.sectionId)}
                  onCloseAllSections={onCloseAllSections}
                  onReactivateSection={onReactivateSection}
                  onAddSection={onAddSection}
                  updatePending={updatePending}
                  closePending={closePending}
                  closeError={closeError}
                  workloadData={workloadData}
                  onInstructorSelect={onInstructorSelect}
                />
              ))
            )
          ) : (
            /* ── External Courses Tab ── */
            !externalCourses || externalCourses.length === 0 ? (
              <div className="flex flex-col items-center py-16 text-gray-400">
                <ExternalLink className="h-8 w-8 mb-2" />
                <p className="text-sm">Başka bölümün açtığı dış ders bulunamadı.</p>
                <p className="text-xs mt-1">Müfredatınızda olan ama başka bölüme ait dersler burada görünür.</p>
              </div>
            ) : (
              externalCourses.map(ec => (
                <div key={ec.courseId} className="flex items-center gap-2 px-3 py-3 border-b last:border-b-0 hover:bg-gray-50/80">
                  <ExternalLink className="h-3.5 w-3.5 text-gray-400 shrink-0" />
                  <div className="flex-1 min-w-0">
                    <div className="flex items-center gap-2">
                      <span className="font-mono text-xs font-bold text-blue-700 shrink-0">{ec.courseCode}</span>
                      <span className="text-sm text-gray-800 truncate">{ec.courseName}</span>
                      <Badge variant="outline" className="text-[11px] shrink-0 text-violet-600 border-violet-200">{ec.ownerDepartmentName}</Badge>
                    </div>
                    <div className="flex items-center gap-1.5 mt-1">
                      <span className="text-xs text-gray-500"><strong>{ec.sectionCount}</strong> şube</span>
                      <span className="text-gray-300">·</span>
                      <span className="text-xs text-gray-500 tabular-nums">Kont: <strong className="text-gray-700">{ec.totalCapacity}</strong></span>
                      <span className="text-gray-300">·</span>
                      <span className="text-xs text-gray-500 tabular-nums">Kayıt: <strong>{ec.totalEnrolled}</strong></span>
                      {ec.predictedDemand > 0 && (
                        <>
                          <span className="text-gray-300">·</span>
                          <span className={cn('text-xs tabular-nums font-medium', ec.predictedDemand > ec.totalCapacity ? 'text-red-600' : 'text-gray-500')}>
                            Talep: {ec.predictedDemand}
                          </span>
                        </>
                      )}
                    </div>
                    {ec.predictedDemand > ec.totalCapacity && (
                      <div className="flex items-center gap-1 mt-1 text-[11px] text-amber-600">
                        <AlertTriangle className="h-3 w-3 shrink-0" />
                        Talep kontenjanı aşıyor
                      </div>
                    )}
                  </div>
                </div>
              ))
            )
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
