import { useState, useMemo, Fragment } from 'react';
import { Button } from '@/components/ui/button';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table';
import { Skeleton } from '@/components/ui/skeleton';
import { Search, RotateCcw } from 'lucide-react';
import { cn } from '@/lib/utils';
import ClassroomWeeklyGrid from './ClassroomWeeklyGrid';
import { useAvailableClassrooms } from '@/hooks/useScheduling';
import { classroomTypeLabel } from '@/lib/classroomLabels';
import type { AvailableClassroomSearchParams, TimeSlotSummary } from '@/types/scheduling';
import type { ClassroomFilterState } from './ClassroomFilters';

const DAY_OPTIONS = [
  { value: 1, label: 'Pzt' },
  { value: 2, label: 'Sal' },
  { value: 3, label: 'Çar' },
  { value: 4, label: 'Per' },
  { value: 5, label: 'Cum' },
];

interface AvailableClassroomSearchTabProps {
  termId: number;
  filters: ClassroomFilterState;
  classroomTypes: string[];
  /** Dönemin gerçek slotları (akşam dahil) — saat seçimini ve ızgarayı besler. */
  slots: TimeSlotSummary[];
}

export default function AvailableClassroomSearchTab({ termId, filters, classroomTypes, slots }: AvailableClassroomSearchTabProps) {
  const [selectedDays, setSelectedDays] = useState<number[]>([]);
  const [startTime, setStartTime] = useState('');
  const [endTime, setEndTime] = useState('');
  const [minCapacity, setMinCapacity] = useState<string>('');
  const [classroomType, setClassroomType] = useState<string>('');
  const [searchParams, setSearchParams] = useState<AvailableClassroomSearchParams | null>(null);
  const [expandedId, setExpandedId] = useState<number | null>(null);
  const [sortField, setSortField] = useState<'name' | 'capacity' | 'occupancy'>('capacity');

  const { data: results, isLoading, isFetched } = useAvailableClassrooms(searchParams);

  // Saat dropdown'ları dönemin gerçek slot sınırlarından beslenir. Varsayılan,
  // kullanıcı seçim yapana kadar ilk slot — effect yerine türetilmiş değerle
  // (cascading render'ı önler).
  const startOptions = useMemo(
    () => [...new Set(slots.map(s => s.startTime))].sort(),
    [slots],
  );
  const effectiveStart = startTime || startOptions[0] || '';
  const endOptions = useMemo(
    () => [...new Set(slots.map(s => s.endTime))].sort().filter(e => e > effectiveStart),
    [slots, effectiveStart],
  );
  const effectiveEnd = (endTime && endTime > effectiveStart ? endTime : '') || endOptions[0] || '';

  function handleSearch() {
    if (selectedDays.length === 0 || !effectiveStart || !effectiveEnd) return;
    setSearchParams({
      termId,
      days: selectedDays,
      startTime: effectiveStart,
      endTime: effectiveEnd,
      minCapacity: minCapacity ? Number(minCapacity) : undefined,
      classroomType: classroomType || filters.classroomType || undefined,
      facultyId: filters.facultyId ?? undefined,
      buildingName: filters.buildingName ?? undefined,
    });
    setExpandedId(null);
  }

  function handleClear() {
    setSelectedDays([]);
    setStartTime('');
    setEndTime('');
    setMinCapacity('');
    setClassroomType('');
    setSearchParams(null);
    setExpandedId(null);
  }

  function toggleDay(day: number) {
    setSelectedDays(prev =>
      prev.includes(day) ? prev.filter(d => d !== day) : [...prev, day]
    );
  }

  const sortedResults = [...(results ?? [])].sort((a, b) => {
    switch (sortField) {
      case 'name': return a.name.localeCompare(b.name, 'tr');
      case 'capacity': return b.capacity - a.capacity;
      case 'occupancy': return a.weeklyOccupancyRate - b.weeklyOccupancyRate;
      default: return 0;
    }
  });

  // Aranan zaman penceresiyle çakışan slotlar, seçili her gün için vurgulanır.
  const highlightSlots = useMemo(() => {
    if (!effectiveStart || !effectiveEnd) return [];
    const inWindow = slots.filter(s => s.startTime < effectiveEnd && effectiveStart < s.endTime);
    return selectedDays.flatMap(day => inWindow.map(s => ({ day, slotOrder: s.slotOrder })));
  }, [slots, selectedDays, effectiveStart, effectiveEnd]);

  return (
    <div className="space-y-5">
      {/* Search Form */}
      <div className="bg-white border rounded-xl p-5 space-y-4">
        <h3 className="text-sm font-semibold">Arama Kriterleri</h3>

        <div className="space-y-3">
          <div>
            <label className="text-xs font-medium text-muted-foreground mb-1.5 block">Gün</label>
            <div className="flex gap-2">
              {DAY_OPTIONS.map(d => (
                <button
                  key={d.value}
                  onClick={() => toggleDay(d.value)}
                  className={cn(
                    'px-4 py-2 rounded-lg border text-sm font-medium transition-colors',
                    selectedDays.includes(d.value)
                      ? 'bg-[#003366] text-white border-[#003366]'
                      : 'bg-white text-gray-600 hover:bg-gray-50',
                  )}
                >
                  {d.label}
                </button>
              ))}
            </div>
          </div>

          <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
            <div>
              <label className="text-xs font-medium text-muted-foreground mb-1.5 block">Başlangıç Saati</label>
              <select
                value={effectiveStart}
                onChange={e => setStartTime(e.target.value)}
                disabled={slots.length === 0}
                className="h-9 w-full rounded-lg border bg-white px-3 text-sm disabled:opacity-50"
              >
                {startOptions.map(t => (
                  <option key={t} value={t}>{t}</option>
                ))}
              </select>
            </div>
            <div>
              <label className="text-xs font-medium text-muted-foreground mb-1.5 block">Bitiş Saati</label>
              <select
                value={effectiveEnd}
                onChange={e => setEndTime(e.target.value)}
                disabled={slots.length === 0}
                className="h-9 w-full rounded-lg border bg-white px-3 text-sm disabled:opacity-50"
              >
                {endOptions.map(t => (
                  <option key={t} value={t}>{t}</option>
                ))}
              </select>
            </div>
            <div>
              <label className="text-xs font-medium text-muted-foreground mb-1.5 block">Min. Kapasite</label>
              <input
                type="number"
                value={minCapacity}
                onChange={e => setMinCapacity(e.target.value)}
                placeholder="Örn: 30"
                className="h-9 w-full rounded-lg border bg-white px-3 text-sm"
              />
            </div>
            <div>
              <label className="text-xs font-medium text-muted-foreground mb-1.5 block">Derslik Tipi</label>
              <select
                value={classroomType}
                onChange={e => setClassroomType(e.target.value)}
                className="h-9 w-full rounded-lg border bg-white px-3 text-sm"
              >
                <option value="">Tümü</option>
                {classroomTypes.map(t => (
                  <option key={t} value={t}>{classroomTypeLabel(t)}</option>
                ))}
              </select>
            </div>
          </div>

          <div className="flex gap-2">
            <Button onClick={handleSearch} disabled={selectedDays.length === 0 || !effectiveStart || !effectiveEnd}>
              <Search className="mr-1.5 h-4 w-4" />
              Ara
            </Button>
            <Button variant="outline" onClick={handleClear}>
              <RotateCcw className="mr-1.5 h-4 w-4" />
              Temizle
            </Button>
          </div>
        </div>
      </div>

      {/* Results */}
      {isLoading && (
        <div className="space-y-2">
          {[1, 2, 3].map(i => <Skeleton key={i} className="h-12 rounded-lg" />)}
        </div>
      )}

      {isFetched && !isLoading && (
        <div className="bg-white border rounded-xl overflow-hidden">
          <div className="flex items-center justify-between px-5 py-3 border-b">
            <p className="text-sm font-medium">
              {sortedResults.length > 0
                ? `${sortedResults.length} uygun derslik bulundu`
                : 'Seçilen kriterlere uygun boş derslik bulunamadı'}
            </p>
            {sortedResults.length > 0 && (
              <select
                value={sortField}
                onChange={e => setSortField(e.target.value as typeof sortField)}
                className="h-8 rounded-md border bg-white px-2 text-xs"
              >
                <option value="capacity">Kapasiteye Göre</option>
                <option value="occupancy">Doluluk Oranına Göre</option>
                <option value="name">Ada Göre</option>
              </select>
            )}
          </div>

          {sortedResults.length === 0 ? (
            <p className="text-sm text-muted-foreground text-center py-8">
              Filtreleri genişletmeyi deneyin.
            </p>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead className="text-xs">Derslik</TableHead>
                  <TableHead className="text-xs">Tip</TableHead>
                  <TableHead className="text-xs text-right">Kapasite</TableHead>
                  <TableHead className="text-xs text-right">Haftalık Doluluk</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {sortedResults.map(c => {
                  const isExpanded = expandedId === c.id;
                  return (
                    <Fragment key={c.id}>
                      <TableRow
                        className="cursor-pointer hover:bg-gray-50"
                        onClick={() => setExpandedId(isExpanded ? null : c.id)}
                      >
                        <TableCell className="text-xs font-medium">{c.name}</TableCell>
                        <TableCell className="text-xs">{classroomTypeLabel(c.type)}</TableCell>
                        <TableCell className="text-xs text-right">{c.capacity}</TableCell>
                        <TableCell className="text-xs text-right">
                          <div className="flex items-center justify-end gap-2">
                            <div className="w-16 h-2 bg-gray-100 rounded-full overflow-hidden">
                              <div
                                className={cn(
                                  'h-full rounded-full',
                                  c.weeklyOccupancyRate > 75 ? 'bg-red-500' : c.weeklyOccupancyRate > 50 ? 'bg-amber-500' : 'bg-emerald-500',
                                )}
                                style={{ width: `${Math.min(c.weeklyOccupancyRate, 100)}%` }}
                              />
                            </div>
                            <span className="font-mono">%{c.weeklyOccupancyRate}</span>
                          </div>
                        </TableCell>
                      </TableRow>
                      {isExpanded && (
                        <TableRow>
                          <TableCell colSpan={4} className="p-4 bg-gray-50/50">
                            <ClassroomWeeklyGrid schedules={c.schedules} slots={slots} highlightSlots={highlightSlots} />
                          </TableCell>
                        </TableRow>
                      )}
                    </Fragment>
                  );
                })}
              </TableBody>
            </Table>
          )}
        </div>
      )}
    </div>
  );
}
