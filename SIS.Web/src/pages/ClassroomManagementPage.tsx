import { useState, useMemo } from 'react';
import { Link } from 'react-router-dom';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  Tooltip, TooltipContent, TooltipProvider, TooltipTrigger,
} from '@/components/ui/tooltip';
import { Skeleton } from '@/components/ui/skeleton';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table';
import {
  AlertCircle, AlertTriangle, ArrowLeft, Building2, ChevronDown,
  Download, Layers, TrendingUp, X,
} from 'lucide-react';
import { cn } from '@/lib/utils';
import { useActiveTerm } from '@/hooks/useActiveTerm';
import { useClassroomDashboard, useSchedulingConflicts } from '@/hooks/useScheduling';
import { exportClassroomCsv } from '@/api/scheduling.api';
import { downloadBlob } from '@/lib/download';
import type { ClassroomInfo } from '../types/scheduling';

const TYPE_LABELS: Record<string, string> = {
  Lecture: 'Normal Sınıf',
  Lab: 'Laboratuvar',
  Computer: 'Bilgisayar Lab',
  Seminar: 'Seminer',
  Amphitheater: 'Amfi',
};

const TYPE_COLORS: Record<string, string> = {
  Lecture: 'bg-blue-100 text-blue-700 border-blue-200',
  Lab: 'bg-emerald-100 text-emerald-700 border-emerald-200',
  Computer: 'bg-violet-100 text-violet-700 border-violet-200',
  Seminar: 'bg-amber-100 text-amber-700 border-amber-200',
  Amphitheater: 'bg-rose-100 text-rose-700 border-rose-200',
};

const DAY_LABELS = ['Pzt', 'Sal', 'Çar', 'Per', 'Cum'];
const DAY_FULL: Record<number, string> = {
  1: 'Pazartesi', 2: 'Salı', 3: 'Çarşamba', 4: 'Perşembe', 5: 'Cuma',
};
const DEFAULT_HOURS = [8, 9, 10, 11, 12, 13, 14, 15, 16, 17];

const OCCUPANCY_COLORS = [
  { max: 0, bg: 'bg-gray-50', border: 'border-gray-100', label: '%0' },
  { max: 0.25, bg: 'bg-emerald-50', border: 'border-emerald-100', label: '%1-25' },
  { max: 0.50, bg: 'bg-emerald-200', border: 'border-emerald-300', label: '%25-50' },
  { max: 0.75, bg: 'bg-amber-200', border: 'border-amber-300', label: '%50-75' },
  { max: 1.01, bg: 'bg-red-300', border: 'border-red-400', label: '%75+' },
];

function getOccupancyStyle(occ: number) {
  for (const c of OCCUPANCY_COLORS) {
    if (occ <= c.max) return c;
  }
  return OCCUPANCY_COLORS[OCCUPANCY_COLORS.length - 1];
}

function getErrorMessage(error: unknown, fallbackMessage: string) {
  if (
    typeof error === 'object'
    && error !== null
    && 'response' in error
    && typeof (error as { response?: { data?: { error?: unknown } } }).response?.data?.error === 'string'
  ) {
    return (error as { response: { data: { error: string } } }).response.data.error;
  }

  if (error instanceof Error && error.message) {
    return error.message;
  }

  return fallbackMessage;
}

interface HeatmapOccupation { classroomName: string; courseCode: string; courseName: string; enrolledCount: number; capacity: number }
interface HeatmapDayCell { occupied?: boolean; courseCode?: string; courseName?: string; instructorName?: string; occupancy?: number; usedCount?: number; total?: number; occupations?: HeatmapOccupation[] }
interface HeatmapRow { hour: number; days: HeatmapDayCell[] }

// ── Main Page ──────────────────────────────────────────────────────

export default function ClassroomManagementPage() {
  const { data: activeTerm } = useActiveTerm();
  const { data: dashboard, isLoading } = useClassroomDashboard(activeTerm?.id);
  const { data: conflictsList } = useSchedulingConflicts(activeTerm?.id);

  const [typeFilter, setTypeFilter] = useState<string | null>(null);
  const [instructorFilter, setInstructorFilter] = useState<string | null>(null);
  const [selectedClassroom, setSelectedClassroom] = useState<number | null>(null);
  const [conflictsOpen, setConflictsOpen] = useState(false);
  const [isExporting, setIsExporting] = useState(false);
  const [exportError, setExportError] = useState<string | null>(null);
  const [selectedCell, setSelectedCell] = useState<{ day: number; hour: number } | null>(null);

  const classrooms = useMemo(() => dashboard?.classrooms ?? [], [dashboard]);

  const instructors = useMemo(() => {
    const names = new Set<string>();
    for (const cr of classrooms) {
      for (const s of cr.schedules) {
        if (s.instructorName) names.add(s.instructorName);
      }
    }
    return [...names].sort((a, b) => a.localeCompare(b, 'tr'));
  }, [classrooms]);

  const hours = useMemo(() => {
    if (!dashboard?.heatmap?.length) return DEFAULT_HOURS;
    const unique = [...new Set(dashboard.heatmap.map(h => h.hour))].filter(h => h >= 6 && h <= 22).sort((a, b) => a - b);
    return unique.length ? unique : DEFAULT_HOURS;
  }, [dashboard]);
  const filteredClassrooms = useMemo(() => {
    let result = classrooms;
    if (typeFilter) result = result.filter(c => c.type === typeFilter);
    if (instructorFilter) result = result.filter(c => c.schedules.some(s => s.instructorName === instructorFilter));
    return result;
  }, [classrooms, typeFilter, instructorFilter]);

  const assignedCount = classrooms.filter(c => c.schedules.length > 0).length;

  async function handleExportCsv() {
    setExportError(null);
    setIsExporting(true);

    try {
      const file = await exportClassroomCsv(activeTerm?.id);
      downloadBlob(file, 'classroom-occupancy.csv');
    } catch (error) {
      setExportError(getErrorMessage(error, 'Derslik doluluk CSV dosyası indirilemedi.'));
    } finally {
      setIsExporting(false);
    }
  }

  // Type distribution
  const typeGroups = useMemo(() => {
    const map: Record<string, number> = {};
    for (const c of classrooms) map[c.type] = (map[c.type] ?? 0) + 1;
    return map;
  }, [classrooms]);

  // Heatmap data (filtered by type or selected classroom)
  const heatmapData = useMemo(() => {
    if (!dashboard) return [];

    if (selectedClassroom) {
      const cls = classrooms.find(c => c.id === selectedClassroom);
      if (!cls) return [];
      return hours.map(hour => ({
        hour,
        days: DAY_LABELS.map((_, dayIdx) => {
          const slot = cls.schedules.find(s => s.day - 1 === dayIdx && hour >= s.startHour && hour <= s.endHour);
          return {
            occupied: !!slot,
            courseCode: slot?.courseCode ?? '',
            courseName: slot?.courseName ?? '',
            instructorName: slot?.instructorName ?? '',
          };
        }),
      }));
    }

    // Global or filtered heatmap
    const sourceClassrooms = filteredClassrooms;
    const total = sourceClassrooms.length || 1;
    const needsClientFilter = typeFilter || instructorFilter;

    return hours.map(hour => ({
      hour,
      days: DAY_LABELS.map((_, dayIdx) => {
        const cell = dashboard.heatmap.find(h => h.day === dayIdx && h.hour === hour);
        if (!cell) return { occupancy: 0, usedCount: 0, total, occupations: [] };

        if (needsClientFilter) {
          const filteredNames = new Set(sourceClassrooms.map(c => c.name));
          const filteredOcc = (cell.occupations ?? []).filter(o => filteredNames.has(o.classroomName));
          return { occupancy: filteredOcc.length / total, usedCount: filteredOcc.length, total, occupations: filteredOcc };
        }

        return { occupancy: cell.occupancy, usedCount: cell.usedClassrooms, total, occupations: cell.occupations ?? [] };
      }),
    }));
  }, [dashboard, classrooms, hours, selectedClassroom, typeFilter, instructorFilter, filteredClassrooms]);

  if (isLoading) {
    return (
      <div className="space-y-5">
        <div className="grid grid-cols-2 lg:grid-cols-4 gap-3">
          {[1, 2, 3, 4].map(i => <Skeleton key={i} className="h-24 rounded-xl" />)}
        </div>
        <Skeleton className="h-96 rounded-xl" />
      </div>
    );
  }

  const conflicts = conflictsList ?? [];
  const selectedCls = selectedClassroom ? classrooms.find(c => c.id === selectedClassroom) ?? null : null;

  return (
    <div className="space-y-5">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h1 className="text-xl font-bold">Derslik Doluluk Tablosu</h1>
          <p className="text-sm text-muted-foreground">
            {activeTerm?.name ? `${activeTerm.name} dönemi için derslik kullanımı` : 'Aktif dönem bilgisi yükleniyor'}
          </p>
        </div>
        <Button variant="outline" size="sm" onClick={handleExportCsv} disabled={!activeTerm?.id || isExporting}>
          <Download className="mr-1.5 h-3.5 w-3.5" />
          {isExporting ? 'Hazırlanıyor...' : 'CSV İndir'}
        </Button>
      </div>

      {exportError && (
        <Alert variant="destructive">
          <AlertCircle className="h-4 w-4" />
          <AlertDescription>{exportError}</AlertDescription>
        </Alert>
      )}

      {/* ── Summary Kartlar ──────────────────────────────────────── */}
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-3">
        <div className="bg-white border rounded-xl p-4 flex items-center gap-3">
          <div className="bg-blue-50 p-2 rounded-lg"><Building2 className="h-4 w-4 text-blue-600" /></div>
          <div>
            <p className="text-xl font-bold">{classrooms.length}</p>
            <p className="text-[11px] text-muted-foreground">Toplam Derslik</p>
          </div>
        </div>
        <TooltipProvider delayDuration={100}>
          <Tooltip>
            <TooltipTrigger asChild>
              <div className="bg-white border rounded-xl p-4 flex items-center gap-3 cursor-default">
                <div className={`p-2 rounded-lg ${(dashboard?.occupancyRate ?? 0) > 75 ? 'bg-red-50' : (dashboard?.occupancyRate ?? 0) > 50 ? 'bg-amber-50' : 'bg-emerald-50'}`}>
                  <TrendingUp className={`h-4 w-4 ${(dashboard?.occupancyRate ?? 0) > 75 ? 'text-red-600' : (dashboard?.occupancyRate ?? 0) > 50 ? 'text-amber-600' : 'text-emerald-600'}`} />
                </div>
                <div>
                  <p className="text-xl font-bold">%{dashboard?.occupancyRate ?? 0}</p>
                  <p className="text-[11px] text-muted-foreground">Doluluk Oranı</p>
                </div>
              </div>
            </TooltipTrigger>
            <TooltipContent side="bottom" className="text-xs">
              Tüm derslik-saat slotları üzerinden ortalama doluluk
            </TooltipContent>
          </Tooltip>
        </TooltipProvider>
        <div className="bg-white border rounded-xl p-4 flex items-center gap-3">
          <div className={`p-2 rounded-lg ${conflicts.length > 0 ? 'bg-red-50' : 'bg-green-50'}`}>
            <AlertCircle className={`h-4 w-4 ${conflicts.length > 0 ? 'text-red-600' : 'text-green-600'}`} />
          </div>
          <div>
            <p className="text-xl font-bold">{conflicts.length}</p>
            <p className="text-[11px] text-muted-foreground">Çakışma</p>
          </div>
        </div>
        <div className="bg-white border rounded-xl p-4 flex items-center gap-3">
          <div className="bg-violet-50 p-2 rounded-lg"><Layers className="h-4 w-4 text-violet-600" /></div>
          <div>
            <p className="text-xl font-bold">{assignedCount}<span className="text-sm font-normal text-muted-foreground">/{classrooms.length}</span></p>
            <p className="text-[11px] text-muted-foreground">Programı Olan</p>
          </div>
        </div>
      </div>

      {/* ── Çakışma Uyarısı ──────────────────────────────────────── */}
      {conflicts.length > 0 && (
        <div className="rounded-xl border border-red-200 bg-red-50 overflow-hidden">
          <button onClick={() => setConflictsOpen(p => !p)} className="w-full flex items-center justify-between px-4 py-3 text-left">
            <div className="flex items-center gap-2">
              <AlertTriangle className="h-4 w-4 text-red-600" />
              <span className="text-sm font-medium text-red-700">{conflicts.length} çakışma tespit edildi</span>
            </div>
            <ChevronDown className={cn('h-4 w-4 text-red-600 transition-transform', conflictsOpen && 'rotate-180')} />
          </button>
          {conflictsOpen && (
            <div className="px-4 pb-4 space-y-2 border-t border-red-200 pt-3">
              {conflicts.map((c, i) => (
                <div key={i} className="flex items-center justify-between gap-2 text-sm py-1.5 border-b border-red-100 last:border-0">
                  <div>
                    <span className="font-mono text-xs font-bold text-[#003366]">{c.courseCode}</span>
                    <span className="ml-2">{c.courseName}</span>
                    <span className="text-gray-500 ml-2">— {c.message}</span>
                  </div>
                  <div className="flex items-center gap-2 shrink-0">
                    {c.classroomName && <span className="text-xs text-gray-500">{c.classroomName}</span>}
                    <Link
                      to="/ders-programi-olusturma"
                      className="text-xs text-blue-600 hover:text-blue-800 hover:underline whitespace-nowrap"
                    >
                      Programlamaya Git →
                    </Link>
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      )}

      {/* ── Ana İçerik: Sol Liste + Sağ Heatmap ─────────────────── */}
      <div className="grid grid-cols-1 xl:grid-cols-[380px_1fr] gap-5 items-start">
        {/* Sol: Tip filtre + Derslik listesi */}
        <div className="space-y-4">
          {/* Tip filtreleri */}
          <div className="flex flex-wrap gap-1.5">
            <button
              onClick={() => { setTypeFilter(null); setSelectedClassroom(null); }}
              className={`px-3 py-1.5 text-xs rounded-full border transition ${!typeFilter ? 'bg-[#003366] text-white border-[#003366]' : 'bg-white text-gray-600 hover:bg-gray-50'}`}
            >
              Tümü ({classrooms.length})
            </button>
            {Object.entries(typeGroups).sort((a, b) => b[1] - a[1]).map(([type, count]) => (
              <button
                key={type}
                onClick={() => { setTypeFilter(typeFilter === type ? null : type); setSelectedClassroom(null); }}
                className={`px-3 py-1.5 text-xs rounded-full border transition ${typeFilter === type ? 'bg-[#003366] text-white border-[#003366]' : 'bg-white text-gray-600 hover:bg-gray-50'}`}
              >
                {TYPE_LABELS[type] ?? type} ({count})
              </button>
            ))}
          </div>

          {/* Öğretim üyesi filtresi */}
          <select
            value={instructorFilter ?? ''}
            onChange={(e) => setInstructorFilter(e.target.value || null)}
            className="w-full h-8 rounded-md border border-gray-300 bg-white px-2 text-xs"
          >
            <option value="">Tüm öğretim üyeleri</option>
            {instructors.map((name) => (
              <option key={name} value={name}>{name}</option>
            ))}
          </select>

          {/* Derslik listesi */}
          <div className="bg-white border rounded-xl overflow-hidden">
            <div className="max-h-[520px] overflow-y-auto">
              {filteredClassrooms.map(c => {
                const isSelected = selectedClassroom === c.id;
                const hasSchedule = c.schedules.length > 0;
                return (
                  <button
                    key={c.id}
                    onClick={() => setSelectedClassroom(isSelected ? null : c.id)}
                    className={cn(
                      'w-full text-left px-4 py-3 flex items-center justify-between border-b last:border-b-0 transition-colors',
                      isSelected ? 'bg-[#003366]/5 border-l-2 border-l-[#003366]' : 'hover:bg-gray-50',
                    )}
                  >
                    <div className="flex items-center gap-3 min-w-0">
                      <div className={cn(
                        'w-2 h-2 rounded-full shrink-0',
                        hasSchedule ? 'bg-emerald-500' : 'bg-gray-300',
                      )} />
                      <div className="min-w-0">
                        <p className="text-sm font-medium truncate">{c.name}</p>
                        <div className="flex items-center gap-2 mt-0.5">
                          <span className={`text-[10px] px-1.5 py-0 rounded-full border ${TYPE_COLORS[c.type] ?? 'bg-gray-100'}`}>
                            {TYPE_LABELS[c.type] ?? c.type}
                          </span>
                          <span className="text-[11px] text-muted-foreground">{c.capacity} kişi</span>
                        </div>
                      </div>
                    </div>
                    <div className="text-right shrink-0">
                      <span className="text-xs font-mono font-semibold">{c.schedules.length}</span>
                      <span className="text-[10px] text-muted-foreground ml-0.5">ders</span>
                    </div>
                  </button>
                );
              })}
              {filteredClassrooms.length === 0 && (
                <div className="text-center py-8 text-muted-foreground text-sm">Derslik bulunamadı.</div>
              )}
            </div>
          </div>
        </div>

        {/* Sağ: Heatmap veya Detay */}
        <div className="min-w-0">
          {selectedCls ? (
            <ClassroomDetail classroom={selectedCls} onBack={() => setSelectedClassroom(null)} />
          ) : (
            <div className="bg-white border rounded-xl p-5">
              <h3 className="text-sm font-semibold mb-4">
                {typeFilter ? `${TYPE_LABELS[typeFilter] ?? typeFilter} Doluluk Haritası` : 'Genel Doluluk Haritası'}
              </h3>

              {/* Heatmap grid */}
              <div className="overflow-x-auto">
                <table className="w-full border-collapse">
                  <thead>
                    <tr>
                      <th className="w-14 text-xs text-muted-foreground font-medium text-left py-1">Saat</th>
                      {DAY_LABELS.map(d => (
                        <th key={d} className="text-xs text-muted-foreground font-medium text-center py-1">{d}</th>
                      ))}
                    </tr>
                  </thead>
                  <tbody>
                    {heatmapData.map((row: HeatmapRow) => (
                      <tr key={row.hour}>
                        <td className="text-xs text-muted-foreground font-mono py-0.5 pr-2">
                          {String(row.hour).padStart(2, '0')}:00
                        </td>
                        {row.days.map((cell: HeatmapDayCell, dayIdx: number) => {
                          if (selectedClassroom) {
                            // Single classroom view
                            return (
                              <td key={dayIdx} className="p-0.5">
                                <TooltipProvider delayDuration={100}>
                                  <Tooltip>
                                    <TooltipTrigger asChild>
                                      <div className={cn(
                                        'h-10 rounded-md border flex items-center justify-center text-[10px] font-medium transition-colors',
                                        cell.occupied
                                          ? 'bg-blue-100 border-blue-200 text-blue-800'
                                          : 'bg-gray-50 border-gray-100 text-gray-300',
                                      )}>
                                        {cell.courseCode || '—'}
                                      </div>
                                    </TooltipTrigger>
                                    <TooltipContent side="top" className="text-xs">
                                      {cell.occupied ? (
                                        <>
                                          <p className="font-semibold">{cell.courseCode} — {cell.courseName}</p>
                                          <p className="text-muted-foreground">{cell.instructorName || 'Hoca atanmamış'}</p>
                                        </>
                                      ) : (
                                        <p>Boş slot</p>
                                      )}
                                    </TooltipContent>
                                  </Tooltip>
                                </TooltipProvider>
                              </td>
                            );
                          }

                          // Global heatmap
                          const style = getOccupancyStyle(cell.occupancy ?? 0);
                          const isSelected = selectedCell?.day === dayIdx && selectedCell?.hour === row.hour;
                          return (
                            <td key={dayIdx} className="p-0.5">
                              <TooltipProvider delayDuration={100}>
                                <Tooltip>
                                  <TooltipTrigger asChild>
                                    <div
                                      className={cn(
                                        'h-10 rounded-md border flex items-center justify-center text-[10px] font-semibold transition-colors cursor-pointer',
                                        style.bg, style.border,
                                        isSelected && 'ring-2 ring-[#003366] ring-offset-1',
                                      )}
                                      onClick={() => setSelectedCell(isSelected ? null : { day: dayIdx, hour: row.hour })}
                                    >
                                      {(cell.usedCount ?? 0) > 0 ? cell.usedCount : ''}
                                    </div>
                                  </TooltipTrigger>
                                  <TooltipContent side="top" className="text-xs max-w-xs">
                                    <p className="font-semibold">{DAY_LABELS[dayIdx]} {String(row.hour).padStart(2, '0')}:00</p>
                                    <p>{cell.usedCount}/{cell.total} derslik dolu (%{Math.round((cell.occupancy ?? 0) * 100)})</p>
                                    {cell.occupations?.slice(0, 5).map((o: HeatmapOccupation, i: number) => (
                                      <p key={i} className="text-muted-foreground">{o.classroomName}: {o.courseCode} ({o.enrolledCount}/{o.capacity})</p>
                                    ))}
                                    {(cell.occupations?.length ?? 0) > 5 && (
                                      <p className="text-muted-foreground">+{(cell.occupations?.length ?? 0) - 5} daha...</p>
                                    )}
                                  </TooltipContent>
                                </Tooltip>
                              </TooltipProvider>
                            </td>
                          );
                        })}
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>

              {/* Cell Detail Panel */}
              {selectedCell && (() => {
                const row = heatmapData.find((r: HeatmapRow) => r.hour === selectedCell.hour);
                const cell = row?.days[selectedCell.day];
                const occupations: HeatmapOccupation[] = (cell && 'occupations' in cell && cell.occupations) ? cell.occupations : [];
                const dayName = DAY_FULL[selectedCell.day + 1] ?? DAY_LABELS[selectedCell.day];
                return (
                  <div className="mt-4 border rounded-lg overflow-hidden">
                    <div className="flex items-center justify-between px-4 py-2.5 bg-[#003366]/5 border-b">
                      <div className="flex items-center gap-2">
                        <span className="text-sm font-semibold text-[#003366]">
                          {dayName} — {String(selectedCell.hour).padStart(2, '0')}:00
                        </span>
                        <span className="text-xs text-muted-foreground">
                          {occupations.length} ders
                        </span>
                      </div>
                      <button
                        onClick={() => setSelectedCell(null)}
                        className="h-6 w-6 flex items-center justify-center rounded-md hover:bg-gray-200 transition-colors"
                      >
                        <X className="h-3.5 w-3.5 text-muted-foreground" />
                      </button>
                    </div>
                    {occupations.length === 0 ? (
                      <p className="text-sm text-muted-foreground text-center py-6">Bu saatte ders yok.</p>
                    ) : (
                      <Table>
                        <TableHeader>
                          <TableRow>
                            <TableHead className="text-xs">Derslik</TableHead>
                            <TableHead className="text-xs">Ders</TableHead>
                            <TableHead className="text-xs text-right">Doluluk</TableHead>
                          </TableRow>
                        </TableHeader>
                        <TableBody>
                          {occupations.map((o: HeatmapOccupation, i: number) => (
                            <TableRow key={i}>
                              <TableCell className="text-xs font-medium">{o.classroomName}</TableCell>
                              <TableCell className="text-xs">
                                <span className="font-mono font-semibold">{o.courseCode}</span>
                                <span className="ml-1.5 text-muted-foreground">{o.courseName}</span>
                              </TableCell>
                              <TableCell className="text-xs text-right font-mono">
                                {o.enrolledCount}/{o.capacity}
                              </TableCell>
                            </TableRow>
                          ))}
                        </TableBody>
                      </Table>
                    )}
                  </div>
                );
              })()}

              {/* Legend */}
              <div className="flex items-center gap-3 mt-4 pt-3 border-t">
                <span className="text-[11px] text-muted-foreground font-medium">Doluluk:</span>
                {OCCUPANCY_COLORS.map(c => (
                  <div key={c.label} className="flex items-center gap-1">
                    <div className={cn('w-4 h-3 rounded-sm border', c.bg, c.border)} />
                    <span className="text-[10px] text-muted-foreground">{c.label}</span>
                  </div>
                ))}
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}

// ── Classroom Detail Panel ─────────────────────────────────────────

function ClassroomDetail({ classroom, onBack }: { classroom: ClassroomInfo; onBack: () => void }) {
  const schedules = [...classroom.schedules].sort((a, b) => a.day - b.day || a.startHour - b.startHour);
  const uniqueCourses = new Set(schedules.map(s => s.courseCode)).size;
  const totalHours = schedules.length;

  // Gün bazında gruplama
  const byDay = useMemo(() => {
    const map = new Map<number, typeof schedules>();
    for (const s of schedules) {
      if (!map.has(s.day)) map.set(s.day, []);
      map.get(s.day)!.push(s);
    }
    return [...map.entries()].sort((a, b) => a[0] - b[0]);
  }, [schedules]);

  return (
    <div className="bg-white border rounded-xl overflow-hidden">
      {/* Header */}
      <div className="flex items-center gap-3 px-5 py-4 border-b">
        <Button variant="ghost" size="sm" onClick={onBack} className="h-8 w-8 p-0">
          <ArrowLeft className="h-4 w-4" />
        </Button>
        <div className="flex-1">
          <h3 className="text-base font-semibold">{classroom.name}</h3>
          <div className="flex items-center gap-2 mt-0.5">
            <span className={`text-[10px] px-2 py-0.5 rounded-full border ${TYPE_COLORS[classroom.type] ?? 'bg-gray-100'}`}>
              {TYPE_LABELS[classroom.type] ?? classroom.type}
            </span>
            <span className="text-xs text-muted-foreground">{classroom.capacity} kişi</span>
          </div>
        </div>
        <div className="flex gap-3 text-center">
          <div>
            <p className="text-lg font-bold text-[#003366]">{uniqueCourses}</p>
            <p className="text-[10px] text-muted-foreground">Ders</p>
          </div>
          <div>
            <p className="text-lg font-bold text-[#003366]">{totalHours}</p>
            <p className="text-[10px] text-muted-foreground">Saat/Hafta</p>
          </div>
        </div>
      </div>

      {/* Schedule */}
      {schedules.length === 0 ? (
        <div className="text-center py-12 text-muted-foreground text-sm">
          Bu dersliğe henüz ders atanmamış.
        </div>
      ) : (
        <div className="p-4 space-y-3">
          {byDay.map(([day, slots]) => (
            <div key={day}>
              <p className="text-xs font-semibold text-gray-500 uppercase tracking-wider mb-1.5">
                {DAY_FULL[day] ?? `Gün ${day}`}
              </p>
              <div className="space-y-1">
                {slots.map((slot, idx) => (
                  <div key={idx} className="flex items-center gap-3 bg-gray-50 rounded-lg px-3 py-2">
                    <span className="text-xs font-mono text-muted-foreground w-24 shrink-0">
                      {String(slot.startHour).padStart(2, '0')}:00 – {String(slot.endHour + 1).padStart(2, '0')}:00
                    </span>
                    <Badge variant="outline" className="font-mono text-xs shrink-0">{slot.courseCode}</Badge>
                    <span className="text-sm truncate">{slot.courseName}</span>
                    <span className="text-xs text-muted-foreground ml-auto shrink-0">
                      {slot.instructorName || <span className="italic">Atanmamış</span>}
                    </span>
                  </div>
                ))}
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
