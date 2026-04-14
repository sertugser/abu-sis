import { useState, useRef } from 'react';
import { Tabs, TabsList, TabsTrigger, TabsContent } from '@/components/ui/tabs';
import { Button } from '@/components/ui/button';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { AlertCircle, Download, FileText } from 'lucide-react';
import { useActiveTerm } from '@/hooks/useActiveTerm';
import {
  useClassroomDashboard,
  useSchedulingConflicts,
  useClassroomFilterOptions,
} from '@/hooks/useScheduling';
import { exportClassroomCsv } from '@/api/scheduling.api';
import { downloadBlob } from '@/lib/download';
import ClassroomFilters, { EMPTY_FILTERS, type ClassroomFilterState } from '@/components/classroom-dashboard/ClassroomFilters';
import OccupancyHeatmapTab from '@/components/classroom-dashboard/OccupancyHeatmapTab';
import ClassroomListTab from '@/components/classroom-dashboard/ClassroomListTab';
import AvailableClassroomSearchTab from '@/components/classroom-dashboard/AvailableClassroomSearchTab';
import ConflictDrawer from '@/components/classroom-dashboard/ConflictDrawer';
import ClassroomPrintReport from '@/components/classroom-dashboard/ClassroomPrintReport';

export default function ClassroomManagementPage() {
  const { data: activeTerm } = useActiveTerm();
  const [filters, setFilters] = useState<ClassroomFilterState>(EMPTY_FILTERS);
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
      <div className={showPrintReport ? 'hidden' : 'space-y-4'}>
        {/* Header */}
        <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <h1 className="text-xl font-bold">Derslik Doluluk Tablosu</h1>
            <p className="text-sm text-muted-foreground">
              {activeTerm?.name ? `${activeTerm.name} dönemi için derslik kullanımı` : 'Aktif dönem bilgisi yükleniyor'}
            </p>
          </div>
          <div className="flex items-center gap-2">
            <Button variant="outline" size="sm" onClick={handleExportCsv} disabled={!activeTerm?.id || isExporting}>
              <Download className="mr-1.5 h-3.5 w-3.5" />
              {isExporting ? 'Hazırlanıyor...' : 'CSV'}
            </Button>
            <Button variant="outline" size="sm" onClick={handlePrintReport} disabled={!dashboard}>
              <FileText className="mr-1.5 h-3.5 w-3.5" />
              PDF
            </Button>
            <ConflictDrawer conflicts={conflicts} />
          </div>
        </div>

        {exportError && (
          <Alert variant="destructive">
            <AlertCircle className="h-4 w-4" />
            <AlertDescription>{exportError}</AlertDescription>
          </Alert>
        )}

        {/* Filters */}
        <ClassroomFilters
          filters={filters}
          onChange={setFilters}
          options={filterOptions}
        />

        {/* Tabs */}
        <Tabs defaultValue="heatmap" className="space-y-4">
          <TabsList>
            <TabsTrigger value="heatmap">Doluluk Haritası</TabsTrigger>
            <TabsTrigger value="list">Derslik Listesi</TabsTrigger>
            <TabsTrigger value="search">Boş Derslik Ara</TabsTrigger>
          </TabsList>

          <TabsContent value="heatmap">
            <OccupancyHeatmapTab
              dashboard={dashboard}
              conflictsCount={conflicts.length}
              isLoading={isLoading}
            />
          </TabsContent>

          <TabsContent value="list">
            <ClassroomListTab
              classrooms={dashboard?.classrooms ?? []}
              isLoading={isLoading}
            />
          </TabsContent>

          <TabsContent value="search">
            {activeTerm?.id ? (
              <AvailableClassroomSearchTab
                termId={activeTerm.id}
                filters={filters}
                classroomTypes={filterOptions?.classroomTypes ?? []}
              />
            ) : (
              <p className="text-center text-muted-foreground py-8">Aktif dönem bulunamadı.</p>
            )}
          </TabsContent>
        </Tabs>
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
