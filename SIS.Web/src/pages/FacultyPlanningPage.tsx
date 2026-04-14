import { useCallback, useState } from 'react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Progress } from '@/components/ui/progress';
import { Skeleton } from '@/components/ui/skeleton';
import { AlertCircle, LayoutList, Wand2, Zap } from 'lucide-react';
import { useAcademicOperationsContext } from '../hooks/useAcademicOperations';
import { useActiveTerm } from '../hooks/useActiveTerm';
import { useAdminFaculties } from '../hooks/useAdminAcademicStructure';
import { useFacultyPlanningOverview } from '../hooks/useFacultyPlanning';
import { ConflictBar } from '../components/faculty-planning/ConflictBar';
import { SectionListTab } from '../components/faculty-planning/SectionListTab';
import { AutoScheduleTab } from '../components/faculty-planning/AutoScheduleTab';

type TabKey = 'list' | 'auto-schedule';

export default function FacultyPlanningPage() {
  const [activeTab, setActiveTab] = useState<TabKey>('list');
  const contextQuery = useAcademicOperationsContext();
  const { data: activeTerm } = useActiveTerm();
  const { data: faculties, isLoading: facultiesLoading } = useAdminFaculties();

  const departments = contextQuery.data?.accessibleDepartments ?? [];
  const [selectedFacultyId, setSelectedFacultyId] = useState<number | null>(null);
  const [filterDept, setFilterDept] = useState<number | undefined>();
  const facultyId = selectedFacultyId ?? faculties?.[0]?.id ?? null;
  const termId = activeTerm?.id ?? null;

  const overviewQuery = useFacultyPlanningOverview(facultyId, termId);
  const ov = overviewQuery.data;

  // Auto-schedule: sadece primitif state (no objects/functions → no infinite loop)
  const [triggerRun, setTriggerRun] = useState(0);
  const [autoIsPending, setAutoIsPending] = useState(false);

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
  const isTermEditable = termStatus === 'Planning' || termStatus === 'Scheduling';
  const progressPct = ov && ov.totalSections > 0 ? Math.round((ov.scheduledSections / ov.totalSections) * 100) : 0;

  return (
    <div className="p-6 flex flex-col h-[calc(100vh-4rem)] overflow-hidden">
      {/* ── Üst bar ── */}
      <div className="flex items-center gap-4 bg-white border border-slate-200 rounded-lg px-4 py-2.5">
        {/* Fakülte */}
        <select
          className="h-8 rounded border border-slate-200 bg-white px-2 text-sm font-medium text-slate-700 shrink-0"
          value={facultyId}
          onChange={e => setSelectedFacultyId(Number(e.target.value))}
        >
          {(faculties ?? []).map(f => (
            <option key={f.id} value={f.id}>{f.code} — {f.name}</option>
          ))}
        </select>

        {/* Bölüm filtresi */}
        <select
          className="h-8 rounded border border-slate-200 bg-white px-2 text-sm text-slate-700 shrink-0"
          value={filterDept ?? ''}
          onChange={e => setFilterDept(e.target.value ? Number(e.target.value) : undefined)}
        >
          <option value="">Tüm Bölümler</option>
          {departments.map(d => (
            <option key={d.departmentId} value={d.departmentId}>{d.departmentCode}</option>
          ))}
        </select>

        <div className="w-px h-6 bg-slate-200" />

        {/* İlerleme */}
        {ov && (
          <>
            <div className="flex items-center gap-2 min-w-[180px]">
              <Progress value={progressPct} className="h-2 flex-1" />
              <span className="text-sm font-semibold text-slate-700 tabular-nums whitespace-nowrap">
                {ov.scheduledSections}/{ov.totalSections}
              </span>
            </div>
            {ov.conflictCount > 0 && (
              <Badge variant="destructive" className="text-[10px]">{ov.conflictCount} çakışma</Badge>
            )}

            <div className="w-px h-6 bg-slate-200" />

            <div className="flex gap-1 flex-wrap">
              {ov.departmentProgress.map(dp => (
                <Badge
                  key={dp.departmentId}
                  variant={dp.scheduledSections === dp.totalSections ? 'success' : 'outline'}
                  className="text-[10px] tabular-nums"
                >
                  {dp.departmentCode}: {dp.scheduledSections}/{dp.totalSections}
                </Badge>
              ))}
            </div>
          </>
        )}

        <div className="flex-1" />

        {/* Tab toggle */}
        <div className="flex border border-slate-200 rounded-lg overflow-hidden shrink-0">
          <button
            onClick={() => setActiveTab('list')}
            className={`flex items-center gap-1.5 px-3.5 py-1.5 text-sm font-medium transition-colors border-r border-slate-200 ${
              activeTab === 'list'
                ? 'bg-[#003366] text-white'
                : 'bg-white text-slate-600 hover:bg-slate-50'
            }`}
          >
            <LayoutList className="h-3.5 w-3.5" />
            Liste
          </button>

          <button
            onClick={() => {
              if (activeTab !== 'auto-schedule') {
                setActiveTab('auto-schedule');
              } else {
                setTriggerRun(n => n + 1);
              }
            }}
            disabled={activeTab === 'auto-schedule' && autoIsPending}
            className={`flex items-center gap-1.5 px-3.5 py-1.5 text-sm font-medium transition-colors disabled:opacity-50 ${
              activeTab === 'auto-schedule'
                ? 'bg-[#003366] text-white'
                : 'bg-white text-slate-600 hover:bg-slate-50'
            }`}
          >
            {activeTab === 'auto-schedule' ? <Zap className="h-3.5 w-3.5" /> : <Wand2 className="h-3.5 w-3.5" />}
            {activeTab === 'auto-schedule' && autoIsPending
              ? 'Hesaplanıyor...'
              : 'Otomatik Programla'}
          </button>
        </div>
      </div>

      {/* Term guard */}
      {termStatus && !isTermEditable && (
        <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-2.5 text-center">
          <p className="text-sm text-red-700 font-medium">
            Dönem durumu "{termStatus}" — atama işlemleri yalnızca "Planning" veya "Scheduling" aşamasında yapılabilir.
          </p>
        </div>
      )}

      {/* Tab content */}
      <div className="flex-1 min-h-0 mt-4 flex flex-col overflow-hidden">
        {activeTab === 'list' && (
          <div className="flex-1 overflow-auto">
            <SectionListTab facultyId={facultyId} termId={termId} departments={departments} readOnly={!isTermEditable} />
          </div>
        )}
        {activeTab === 'auto-schedule' && (
          <div className="flex-1 overflow-hidden relative">
            <AutoScheduleTab
              facultyId={facultyId}
              termId={termId}
              filterDept={filterDept}
              triggerRun={triggerRun}
              onRunStateChange={handleRunStateChange}
            />
          </div>
        )}
      </div>

      <ConflictBar facultyId={facultyId} termId={termId} />
    </div>
  );
}
