import { useState, useMemo, Fragment } from 'react';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table';
import { Skeleton } from '@/components/ui/skeleton';
import { LayoutGrid, List, Search } from 'lucide-react';
import { cn } from '@/lib/utils';
import ClassroomWeeklyGrid from './ClassroomWeeklyGrid';
import { classroomTypeLabel, CLASSROOM_TYPE_COLORS } from '@/lib/classroomLabels';
import type { ClassroomInfo, TimeSlotSummary } from '@/types/scheduling';

type SortField = 'name' | 'occupancy' | 'courseCount' | 'capacity';
type ViewMode = 'card' | 'table';

interface ClassroomListTabProps {
  classrooms: ClassroomInfo[];
  slots: TimeSlotSummary[];
  isLoading: boolean;
}

function getUniqueCoursesCount(c: ClassroomInfo): number {
  return new Set(c.schedules.map(s => s.courseCode)).size;
}

export default function ClassroomListTab({ classrooms, slots, isLoading }: ClassroomListTabProps) {
  const [search, setSearch] = useState('');
  const [sortField, setSortField] = useState<SortField>('name');
  const [viewMode, setViewMode] = useState<ViewMode>('card');
  const [expandedId, setExpandedId] = useState<number | null>(null);

  const filtered = useMemo(() => {
    const q = search.toLowerCase().trim();
    let result = classrooms;
    if (q) {
      result = result.filter(c =>
        c.name.toLowerCase().includes(q) ||
        c.schedules.some(s => s.courseCode.toLowerCase().includes(q) || s.courseName.toLowerCase().includes(q))
      );
    }

    result = [...result].sort((a, b) => {
      switch (sortField) {
        case 'name': return a.name.localeCompare(b.name, 'tr');
        case 'occupancy': return b.weeklyOccupancyRate - a.weeklyOccupancyRate;
        case 'courseCount': return getUniqueCoursesCount(b) - getUniqueCoursesCount(a);
        case 'capacity': return b.capacity - a.capacity;
        default: return 0;
      }
    });
    return result;
  }, [classrooms, search, sortField]);

  if (isLoading) {
    return (
      <div className="space-y-3">
        {[1, 2, 3].map(i => <Skeleton key={i} className="h-28 rounded-xl" />)}
      </div>
    );
  }

  return (
    <div className="space-y-4">
      {/* Controls */}
      <div className="flex flex-col sm:flex-row gap-3 sm:items-center sm:justify-between">
        <div className="relative flex-1 max-w-sm">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
          <input
            type="text"
            placeholder="Derslik adı veya ders kodu ara..."
            value={search}
            onChange={e => setSearch(e.target.value)}
            className="h-9 w-full rounded-lg border bg-white pl-9 pr-3 text-sm"
          />
        </div>
        <div className="flex items-center gap-2">
          <select
            value={sortField}
            onChange={e => setSortField(e.target.value as SortField)}
            className="h-9 rounded-lg border bg-white px-3 text-sm"
          >
            <option value="name">Ada Göre</option>
            <option value="occupancy">Doluluk Oranına Göre</option>
            <option value="courseCount">Ders Sayısına Göre</option>
            <option value="capacity">Kapasiteye Göre</option>
          </select>
          <div className="flex border rounded-lg overflow-hidden">
            <button
              onClick={() => setViewMode('card')}
              className={cn('p-2', viewMode === 'card' ? 'bg-[#003366] text-white' : 'bg-white text-gray-600')}
            >
              <LayoutGrid className="h-4 w-4" />
            </button>
            <button
              onClick={() => setViewMode('table')}
              className={cn('p-2', viewMode === 'table' ? 'bg-[#003366] text-white' : 'bg-white text-gray-600')}
            >
              <List className="h-4 w-4" />
            </button>
          </div>
        </div>
      </div>

      {filtered.length === 0 ? (
        <div className="text-center py-12 text-muted-foreground text-sm bg-white border rounded-xl">
          Aramanıza uygun derslik bulunamadı.
        </div>
      ) : viewMode === 'card' ? (
        /* Card View */
        <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-3">
          {filtered.map(c => {
            const occ = c.weeklyOccupancyRate;
            const courseCount = getUniqueCoursesCount(c);
            const totalHours = c.schedules.length;
            const isExpanded = expandedId === c.id;

            return (
              <div key={c.id} className="bg-white border rounded-xl overflow-hidden">
                <button
                  onClick={() => setExpandedId(isExpanded ? null : c.id)}
                  className="w-full text-left p-4 hover:bg-gray-50/50 transition-colors"
                >
                  <div className="flex items-start justify-between mb-3">
                    <div>
                      <p className="font-semibold text-sm">{c.name}</p>
                      <div className="flex items-center gap-2 mt-1">
                        <span className={cn('text-[10px] px-1.5 py-0.5 rounded-full border', CLASSROOM_TYPE_COLORS[c.type] ?? 'bg-gray-100')}>
                          {classroomTypeLabel(c.type)}
                        </span>
                        <span className="text-[11px] text-muted-foreground">{c.capacity} kişi</span>
                      </div>
                    </div>
                  </div>
                  <div className="space-y-2">
                    <div className="flex items-center justify-between text-xs">
                      <span className="text-muted-foreground">Haftalık Doluluk</span>
                      <span className="font-semibold">%{occ}</span>
                    </div>
                    <div className="h-2 bg-gray-100 rounded-full overflow-hidden">
                      <div
                        className={cn(
                          'h-full rounded-full transition-all',
                          occ > 75 ? 'bg-red-500' : occ > 50 ? 'bg-amber-500' : 'bg-emerald-500',
                        )}
                        style={{ width: `${Math.min(occ, 100)}%` }}
                      />
                    </div>
                    <p className="text-xs text-muted-foreground">{courseCount} ders · {totalHours} saat/hafta</p>
                  </div>
                </button>

                {isExpanded && (
                  <div className="border-t p-4 space-y-3">
                    <ClassroomWeeklyGrid schedules={c.schedules} slots={slots} />
                    {c.schedules.length > 0 && (
                      <div className="space-y-1">
                        <p className="text-xs font-semibold text-muted-foreground">Ders Listesi</p>
                        {[...new Map(c.schedules.map(s => [s.courseCode, s])).values()].map(s => (
                          <div key={s.courseCode} className="flex items-center justify-between text-xs py-1 border-b last:border-0">
                            <div>
                              <span className="font-mono font-semibold">{s.courseCode}</span>
                              <span className="ml-1.5 text-muted-foreground">{s.courseName}</span>
                            </div>
                            <span className="text-muted-foreground">{s.instructorName || 'Atanmamış'}</span>
                          </div>
                        ))}
                      </div>
                    )}
                  </div>
                )}
              </div>
            );
          })}
        </div>
      ) : (
        /* Table View */
        <div className="bg-white border rounded-xl overflow-hidden">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead className="text-xs">Derslik</TableHead>
                <TableHead className="text-xs">Tip</TableHead>
                <TableHead className="text-xs text-right">Kapasite</TableHead>
                <TableHead className="text-xs text-right">Ders</TableHead>
                <TableHead className="text-xs text-right">Saat/Hafta</TableHead>
                <TableHead className="text-xs text-right">Doluluk</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {filtered.map(c => {
                const occ = c.weeklyOccupancyRate;
                const isExpanded = expandedId === c.id;
                return (
                  <Fragment key={c.id}>
                    <TableRow
                      className="cursor-pointer hover:bg-gray-50"
                      onClick={() => setExpandedId(isExpanded ? null : c.id)}
                    >
                      <TableCell className="text-xs font-medium">{c.name}</TableCell>
                      <TableCell>
                        <span className={cn('text-[10px] px-1.5 py-0.5 rounded-full border', CLASSROOM_TYPE_COLORS[c.type])}>
                          {classroomTypeLabel(c.type)}
                        </span>
                      </TableCell>
                      <TableCell className="text-xs text-right">{c.capacity}</TableCell>
                      <TableCell className="text-xs text-right">{getUniqueCoursesCount(c)}</TableCell>
                      <TableCell className="text-xs text-right">{c.schedules.length}</TableCell>
                      <TableCell className="text-xs text-right">
                        <span className={cn(
                          'font-semibold',
                          occ > 75 ? 'text-red-600' : occ > 50 ? 'text-amber-600' : 'text-emerald-600',
                        )}>
                          %{occ}
                        </span>
                      </TableCell>
                    </TableRow>
                    {isExpanded && (
                      <TableRow>
                        <TableCell colSpan={6} className="p-4 bg-gray-50/50">
                          <ClassroomWeeklyGrid schedules={c.schedules} slots={slots} />
                        </TableCell>
                      </TableRow>
                    )}
                  </Fragment>
                );
              })}
            </TableBody>
          </Table>
        </div>
      )}
    </div>
  );
}
