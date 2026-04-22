import { useCallback, useState } from 'react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/tooltip';
import { AlertCircle, Calendar, Download, FileText, LayoutList, Search, Upload, Wand2 } from 'lucide-react';
import { Input } from '@/components/ui/input';
import { STATUS_CONFIG } from '../components/faculty-planning/sectionUtils';
import { cn } from '@/lib/utils';
import { useAcademicOperationsContext } from '../hooks/useAcademicOperations';
import { useActiveTerm } from '../hooks/useActiveTerm';
import { useAdminFaculties } from '../hooks/useAdminAcademicStructure';
import { useExportExcel, useExportPdf } from '../hooks/useExcelSchedule';
import { ConflictBar } from '../components/faculty-planning/ConflictBar';
import { SectionListTab } from '../components/faculty-planning/SectionListTab';
import { AutoScheduleTab } from '../components/faculty-planning/AutoScheduleTab';
import { ExcelImportModal } from '../components/faculty-planning/ExcelImportModal';

type TabKey = 'list' | 'auto-schedule';
const PAGE_SIZES = [10, 25, 50, 100];

export default function FacultyPlanningPage() {
  const [activeTab, setActiveTab] = useState<TabKey>('auto-schedule');
  const [importOpen, setImportOpen] = useState(false);
  const contextQuery = useAcademicOperationsContext();
  const { data: activeTerm } = useActiveTerm();
  const { data: faculties, isLoading: facultiesLoading } = useAdminFaculties();

  const [selectedFacultyId, setSelectedFacultyId] = useState<number | null>(null);
  const facultyId = selectedFacultyId ?? faculties?.[0]?.id ?? null;
  const termId = activeTerm?.id ?? null;

  const exportExcel = useExportExcel();
  const exportPdf = useExportPdf();

  const [triggerRun, setTriggerRun] = useState(0);
  const [autoIsPending, setAutoIsPending] = useState(false);

  // Liste filtreleri (toolbar'da gösterilir)
  const [filterStatus, setFilterStatus] = useState<string | undefined>();
  const [searchText, setSearchText] = useState('');
  const [pageSize, setPageSize] = useState(25);

  const [actionBarNode, setActionBarNode] = useState<HTMLDivElement | null>(null);

  const handleRunStateChange = useCallback((isPending: boolean) => {
    setAutoIsPending(isPending);
  }, []);

  if (contextQuery.isLoading || facultiesLoading) return <Skeleton className="h-96 rounded-xl" />;

  if (contextQuery.isError || !facultyId) {
    return (
      <div className="flex items-center justify-center h-64">
        <Alert className="max-w-md">
          <AlertCircle className="h-4 w-4" />
          <AlertDescription>{contextQuery.isError ? 'Yetkili kapsam bilgileri alınamadı.' : 'Fakülte bulunamadı.'}</AlertDescription>
        </Alert>
      </div>
    );
  }

  const termStatus = activeTerm?.status;
  const isTermEditable = termStatus === 'Preparing';

  return (
    <div className="flex flex-col h-[calc(100vh-120px)] overflow-hidden">
      {/* ── Toolbar ── */}
      <div className="flex items-center gap-3 mb-2 shrink-0 bg-white border border-slate-200 rounded-xl px-4 py-2">
        {/* Filtreler */}
        <select
          className="h-8 rounded-md border border-slate-200 bg-white px-2.5 text-sm font-medium text-slate-700 shrink-0"
          value={facultyId}
          onChange={e => setSelectedFacultyId(Number(e.target.value))}
        >
          {(faculties ?? []).map(f => (
            <option key={f.id} value={f.id}>{f.code} — {f.name}</option>
          ))}
        </select>
        {activeTab === 'list' && (
          <>
            <div className="w-px h-5 bg-slate-200" />
            <div className="relative">
              <Search className="absolute left-2.5 top-1/2 -translate-y-1/2 h-3.5 w-3.5 text-slate-400" />
              <Input
                placeholder="Ders kodu veya adı..."
                value={searchText}
                onChange={e => setSearchText(e.target.value)}
                className="pl-8 h-8 w-44 text-sm border-slate-200"
              />
            </div>
            <select
              className="h-8 rounded-md border border-slate-200 bg-white px-2.5 text-sm text-slate-700 shrink-0"
              value={filterStatus ?? ''}
              onChange={e => setFilterStatus(e.target.value || undefined)}
            >
              <option value="">Tüm Durumlar</option>
              {Object.entries(STATUS_CONFIG).map(([k, v]) => <option key={k} value={k}>{v.label}</option>)}
            </select>
            <select
              className="h-8 rounded-md border border-slate-200 bg-white px-2.5 text-sm text-slate-700 shrink-0"
              value={pageSize}
              onChange={e => setPageSize(Number(e.target.value))}
            >
              {PAGE_SIZES.map(n => <option key={n} value={n}>{n} satır</option>)}
            </select>
            {(filterStatus !== undefined || searchText) && (
              <button
                className="text-xs text-muted-foreground hover:text-foreground underline underline-offset-2"
                onClick={() => { setFilterStatus(undefined); setSearchText(''); }}
              >
                Temizle
              </button>
            )}
          </>
        )}

        {/* AutoScheduleTab aksiyon bar'ı buraya portal ile render edilir */}
        <div ref={setActionBarNode} className="flex items-center gap-2" />

        <div className="flex-1" />

        {/* Segmented control */}
        <div className="flex bg-slate-100 rounded-lg p-0.5 shrink-0">
          <button
            onClick={() => setActiveTab('auto-schedule')}
            className={cn(
              'flex items-center gap-1.5 px-3 py-1 text-xs font-medium rounded-md transition-all',
              activeTab === 'auto-schedule' ? 'bg-white text-foreground shadow-sm' : 'text-muted-foreground hover:text-foreground',
            )}
          >
            <Calendar className="h-3.5 w-3.5" />
            Takvim
          </button>
          <button
            onClick={() => setActiveTab('list')}
            className={cn(
              'flex items-center gap-1.5 px-3 py-1 text-xs font-medium rounded-md transition-all',
              activeTab === 'list' ? 'bg-white text-foreground shadow-sm' : 'text-muted-foreground hover:text-foreground',
            )}
          >
            <LayoutList className="h-3.5 w-3.5" />
            Liste
          </button>
        </div>

        {/* Separator */}
        <div className="w-px h-5 bg-slate-200" />

        {/* Export */}
        <div className="flex items-center gap-0.5 shrink-0">
          <TooltipProvider delayDuration={100}>
            <Tooltip>
              <TooltipTrigger asChild>
                <Button variant="ghost" size="sm" className="h-8 w-8 p-0" disabled={exportExcel.isPending}
                  onClick={() => exportExcel.mutate({ termId: termId!, facultyId: facultyId! })}>
                  <Download className="h-4 w-4" />
                </Button>
              </TooltipTrigger>
              <TooltipContent side="bottom" className="text-xs">Excel İndir</TooltipContent>
            </Tooltip>
          </TooltipProvider>
          <TooltipProvider delayDuration={100}>
            <Tooltip>
              <TooltipTrigger asChild>
                <Button variant="ghost" size="sm" className="h-8 w-8 p-0" disabled={exportPdf.isPending}
                  onClick={() => exportPdf.mutate({ termId: termId!, facultyId: facultyId!, departmentId: undefined })}>
                  <FileText className="h-4 w-4" />
                </Button>
              </TooltipTrigger>
              <TooltipContent side="bottom" className="text-xs">PDF İndir</TooltipContent>
            </Tooltip>
          </TooltipProvider>
          {isTermEditable && (
            <TooltipProvider delayDuration={100}>
              <Tooltip>
                <TooltipTrigger asChild>
                  <Button variant="ghost" size="sm" className="h-8 w-8 p-0" onClick={() => setImportOpen(true)}>
                    <Upload className="h-4 w-4" />
                  </Button>
                </TooltipTrigger>
                <TooltipContent side="bottom" className="text-xs">Excel Yükle</TooltipContent>
              </Tooltip>
            </TooltipProvider>
          )}
        </div>

        {/* Separator */}
        <div className="w-px h-5 bg-slate-200" />

        {/* Otomatik Programla */}
        <Button
          size="sm"
          className="btn-magic-schedule h-8 text-sm text-white rounded-lg border-0"
          disabled={autoIsPending || !isTermEditable}
          onClick={() => {
            if (activeTab !== 'auto-schedule') setActiveTab('auto-schedule');
            setTriggerRun(n => n + 1);
          }}
        >
          <Wand2 className="h-3.5 w-3.5 mr-1.5" />
          {autoIsPending ? 'Hesaplanıyor...' : 'Otomatik Programla'}
        </Button>
      </div>

      {/* Term guard */}
      {termStatus && !isTermEditable && (
        <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-2 text-center mb-3">
          <p className="text-xs text-red-700 font-medium">
            Dönem durumu "{termStatus}" — atama işlemleri yalnızca "Planning" veya "Scheduling" aşamasında yapılabilir.
          </p>
        </div>
      )}

      {/* Tab content */}
      <div className="flex-1 min-h-0 flex flex-col overflow-hidden">
        {activeTab === 'list' && (
          <div className="flex-1 overflow-auto">
            <SectionListTab
              facultyId={facultyId}
              termId={termId}
              readOnly={!isTermEditable}
              filterDept={undefined}
              filterStatus={filterStatus}
              searchText={searchText}
              pageSize={pageSize}
            />
          </div>
        )}
        {activeTab === 'auto-schedule' && (
          <div className="flex-1 overflow-hidden">
            <AutoScheduleTab
              facultyId={facultyId}
              termId={termId}
              filterDept={undefined}
              triggerRun={triggerRun}
              onRunStateChange={handleRunStateChange}
              actionBarPortal={actionBarNode}
            />
          </div>
        )}
      </div>

      <ConflictBar facultyId={facultyId} termId={termId} />

      <ExcelImportModal
        open={importOpen}
        onClose={() => setImportOpen(false)}
        termId={termId!}
        facultyId={facultyId!}
      />
    </div>
  );
}
