import { useState, useRef } from 'react';
import { Button } from '@/components/ui/button';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/tooltip';
import { AlertCircle, Download, FileText, Grid3X3, List, Search } from 'lucide-react';
import { cn } from '@/lib/utils';
import { useActiveTerm } from '@/hooks/useActiveTerm';
import {
  useClassroomDashboard,
  useSchedulingConflicts,
  useClassroomFilterOptions,
} from '@/hooks/useScheduling';
import { exportClassroomCsv } from '@/api/scheduling.api';
import { downloadBlob } from '@/lib/download';
import { EMPTY_FILTERS, type ClassroomFilterState } from '@/components/classroom-dashboard/ClassroomFilters';
import OccupancyHeatmapTab from '@/components/classroom-dashboard/OccupancyHeatmapTab';
import ClassroomListTab from '@/components/classroom-dashboard/ClassroomListTab';
import AvailableClassroomSearchTab from '@/components/classroom-dashboard/AvailableClassroomSearchTab';
import ConflictDrawer from '@/components/classroom-dashboard/ConflictDrawer';
import ClassroomPrintReport from '@/components/classroom-dashboard/ClassroomPrintReport';

const TYPE_LABELS: Record<string, string> = {
  Lecture: 'Normal Sınıf',
  Lab: 'Laboratuvar',
  Computer: 'Bilgisayar Lab',
  Seminar: 'Seminer',
  Amphitheater: 'Amfi',
};

const CAPACITY_RANGES = [
  { label: 'Tümünü Göster', min: null, max: null },
  { label: '0-30', min: 0, max: 30 },
  { label: '31-60', min: 31, max: 60 },
  { label: '61-100', min: 61, max: 100 },
  { label: '100+', min: 101, max: null },
];

type TabKey = 'heatmap' | 'list' | 'search';

export default function ClassroomManagementPage() {
  const { data: activeTerm } = useActiveTerm();
  const [filters, setFilters] = useState<ClassroomFilterState>(EMPTY_FILTERS);
  const [activeTab, setActiveTab] = useState<TabKey>('heatmap');
  const [isExporting, setIsExporting] = useState(false);
  const [exportError, setExportError] = useState<string | null>(null);
  const [showPrintReport, setShowPrintReport] = useState(false);
  const printRef = useRef<HTMLDivElement>(null);

  const dashboardParams = {
    termId: activeTerm?.id,
    facultyId: filters.facultyId ?? undefined,
    buildingName: filters.buildingName ?? undefined,
    classroomType: filters.classroomType ?? undefined,
    minCapacity: filters.minCapacity ?? undefined,
    maxCapacity: filters.maxCapacity ?? undefined,
  };

  const { data: dashboard, isLoading } = useClassroomDashboard(dashboardParams);
  const { data: conflictsList } = useSchedulingConflicts(activeTerm?.id);
  const { data: filterOptions } = useClassroomFilterOptions(activeTerm?.id);

  const conflicts = conflictsList ?? [];

  const hasActiveFilters = filters.facultyId !== null || filters.buildingName !== null
    || filters.classroomType !== null || filters.minCapacity !== null;

  async function handleExportCsv() {
    setExportError(null);
    setIsExporting(true);
    try {
      const file = await exportClassroomCsv(activeTerm?.id);
      downloadBlob(file, 'classroom-occupancy.csv');
    } catch (error) {
      setExportError(error instanceof Error ? error.message : 'CSV dosyası indirilemedi.');
    } finally {
      setIsExporting(false);
    }
  }

  function handlePrintReport() {
    setShowPrintReport(true);
    setTimeout(() => {
      window.print();
      setShowPrintReport(false);
    }, 300);
  }

  return (
    <>
      <div className={showPrintReport ? 'hidden' : 'space-y-3'}>
        {/* ── Toolbar ── */}
        <div className="flex items-center gap-3 bg-white border border-slate-200 rounded-xl px-4 py-2">
          {/* Filtreler */}
          <select
            className="h-8 rounded-md border border-slate-200 bg-white px-2.5 text-sm text-slate-700 shrink-0"
            value={filters.facultyId ?? ''}
            onChange={e => setFilters({
              ...filters,
              facultyId: e.target.value ? Number(e.target.value) : null,
              buildingName: null,
            })}
          >
            <option value="">Tüm Fakülteler</option>
            {filterOptions?.faculties.map(f => (
              <option key={f.id} value={f.id}>{f.name}</option>
            ))}
          </select>

          <select
            className="h-8 rounded-md border border-slate-200 bg-white px-2.5 text-sm text-slate-700 shrink-0"
            value={filters.buildingName ?? ''}
            onChange={e => setFilters({ ...filters, buildingName: e.target.value || null })}
          >
            <option value="">Tüm Binalar</option>
            {(filterOptions?.buildings ?? []).map(b => (
              <option key={b} value={b}>{b}</option>
            ))}
          </select>

          <select
            className="h-8 rounded-md border border-slate-200 bg-white px-2.5 text-sm text-slate-700 shrink-0"
            value={filters.classroomType ?? ''}
            onChange={e => setFilters({ ...filters, classroomType: e.target.value || null })}
          >
            <option value="">Tüm Tipler</option>
            {(filterOptions?.classroomTypes ?? []).map(t => (
              <option key={t} value={t}>{TYPE_LABELS[t] ?? t}</option>
            ))}
          </select>

          <select
            className="h-8 rounded-md border border-slate-200 bg-white px-2.5 text-sm text-slate-700 shrink-0"
            value={filters.minCapacity !== null ? `${filters.minCapacity}-${filters.maxCapacity ?? ''}` : ''}
            onChange={e => {
              if (!e.target.value) {
                setFilters({ ...filters, minCapacity: null, maxCapacity: null });
                return;
              }
              const range = CAPACITY_RANGES.find(r => `${r.min}-${r.max ?? ''}` === e.target.value);
              setFilters({
                ...filters,
                minCapacity: range?.min ?? null,
                maxCapacity: range?.max ?? null,
              });
            }}
          >
            {CAPACITY_RANGES.map(r => (
              <option key={r.label} value={r.min !== null ? `${r.min}-${r.max ?? ''}` : ''}>
                {r.label}
              </option>
            ))}
          </select>

          {hasActiveFilters && (
            <button
              className="text-xs text-muted-foreground hover:text-foreground underline underline-offset-2"
              onClick={() => setFilters(EMPTY_FILTERS)}
            >
              Temizle
            </button>
          )}

          <div className="flex-1" />

          {/* Segmented control */}
          <div className="flex bg-slate-100 rounded-lg p-0.5 shrink-0">
            <button
              onClick={() => setActiveTab('heatmap')}
              className={cn(
                'flex items-center gap-1.5 px-3 py-1 text-xs font-medium rounded-md transition-all',
                activeTab === 'heatmap' ? 'bg-white text-foreground shadow-sm' : 'text-muted-foreground hover:text-foreground',
              )}
            >
              <Grid3X3 className="h-3.5 w-3.5" />
              Doluluk
            </button>
            <button
              onClick={() => setActiveTab('list')}
              className={cn(
                'flex items-center gap-1.5 px-3 py-1 text-xs font-medium rounded-md transition-all',
                activeTab === 'list' ? 'bg-white text-foreground shadow-sm' : 'text-muted-foreground hover:text-foreground',
              )}
            >
              <List className="h-3.5 w-3.5" />
              Liste
            </button>
            <button
              onClick={() => setActiveTab('search')}
              className={cn(
                'flex items-center gap-1.5 px-3 py-1 text-xs font-medium rounded-md transition-all',
                activeTab === 'search' ? 'bg-white text-foreground shadow-sm' : 'text-muted-foreground hover:text-foreground',
              )}
            >
              <Search className="h-3.5 w-3.5" />
              Boş Ara
            </button>
          </div>

          {/* Separator */}
          <div className="w-px h-5 bg-slate-200" />

          {/* Export + Conflict */}
          <div className="flex items-center gap-0.5 shrink-0">
            <TooltipProvider delayDuration={100}>
              <Tooltip>
                <TooltipTrigger asChild>
                  <Button variant="ghost" size="sm" className="h-8 w-8 p-0" onClick={handleExportCsv} disabled={!activeTerm?.id || isExporting}>
                    <Download className="h-4 w-4" />
                  </Button>
                </TooltipTrigger>
                <TooltipContent side="bottom" className="text-xs">CSV İndir</TooltipContent>
              </Tooltip>
            </TooltipProvider>
            <TooltipProvider delayDuration={100}>
              <Tooltip>
                <TooltipTrigger asChild>
                  <Button variant="ghost" size="sm" className="h-8 w-8 p-0" onClick={handlePrintReport} disabled={!dashboard}>
                    <FileText className="h-4 w-4" />
                  </Button>
                </TooltipTrigger>
                <TooltipContent side="bottom" className="text-xs">PDF İndir</TooltipContent>
              </Tooltip>
            </TooltipProvider>
            <ConflictDrawer conflicts={conflicts} />
          </div>
        </div>

        {exportError && (
          <Alert variant="destructive">
            <AlertCircle className="h-4 w-4" />
            <AlertDescription>{exportError}</AlertDescription>
          </Alert>
        )}

        {/* Tab content */}
        {activeTab === 'heatmap' && (
          <OccupancyHeatmapTab dashboard={dashboard} isLoading={isLoading} />
        )}
        {activeTab === 'list' && (
          <ClassroomListTab classrooms={dashboard?.classrooms ?? []} isLoading={isLoading} />
        )}
        {activeTab === 'search' && (
          activeTerm?.id ? (
            <AvailableClassroomSearchTab
              termId={activeTerm.id}
              filters={filters}
              classroomTypes={filterOptions?.classroomTypes ?? []}
            />
          ) : (
            <p className="text-center text-muted-foreground py-8">Aktif dönem bulunamadı.</p>
          )
        )}
      </div>

      {/* Print Report */}
      {showPrintReport && dashboard && (
        <div ref={printRef}>
          <ClassroomPrintReport
            dashboard={dashboard}
            termName={activeTerm?.name ?? ''}
            conflictsCount={conflicts.length}
            filterSummary={
              [
                filters.facultyId && filterOptions?.faculties.find(f => f.id === filters.facultyId)?.name,
                filters.buildingName,
                filters.classroomType,
              ].filter(Boolean).join(' | ') || undefined
            }
          />
        </div>
      )}
    </>
  );
}
