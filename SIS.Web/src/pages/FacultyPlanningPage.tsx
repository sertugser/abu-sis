import { useState } from 'react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Progress } from '@/components/ui/progress';
import { Skeleton } from '@/components/ui/skeleton';
import { AlertCircle, Calendar, LayoutList, Wand2 } from 'lucide-react';
import { useAcademicOperationsContext } from '../hooks/useAcademicOperations';
import { useActiveTerm } from '../hooks/useActiveTerm';
import { useAdminFaculties } from '../hooks/useAdminAcademicStructure';
import { useFacultyPlanningOverview } from '../hooks/useFacultyPlanning';
import { ConflictBar } from '../components/faculty-planning/ConflictBar';
import { SectionListTab } from '../components/faculty-planning/SectionListTab';
import { AutoScheduleTab } from '../components/faculty-planning/AutoScheduleTab';
import { CalendarViewTab } from '../components/faculty-planning/CalendarViewTab';

type TabKey = 'list' | 'calendar' | 'auto-schedule';

const TABS: { key: TabKey; label: string; icon: typeof LayoutList }[] = [
  { key: 'list', label: 'Liste', icon: LayoutList },
  { key: 'calendar', label: 'Takvim', icon: Calendar },
  { key: 'auto-schedule', label: 'Otomatik', icon: Wand2 },
];

export default function FacultyPlanningPage() {
  const [activeTab, setActiveTab] = useState<TabKey>('list');
  const contextQuery = useAcademicOperationsContext();
  const { data: activeTerm } = useActiveTerm();
  const { data: faculties, isLoading: facultiesLoading } = useAdminFaculties();

  const departments = contextQuery.data?.accessibleDepartments ?? [];
  const [selectedFacultyId, setSelectedFacultyId] = useState<number | null>(null);
  const facultyId = selectedFacultyId ?? faculties?.[0]?.id ?? null;
  const termId = activeTerm?.id ?? null;

  const overviewQuery = useFacultyPlanningOverview(facultyId, termId);
  const ov = overviewQuery.data;

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
      {/* ── Row 1: Fakülte seçici + İlerleme + Bölüm badge + Tab toggle ── */}
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

        {/* Ayırıcı */}
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

            {/* Ayırıcı */}
            <div className="w-px h-6 bg-slate-200" />

            {/* Bölüm badge'leri */}
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

        {/* Sağa it */}
        <div className="flex-1" />

        {/* Tab toggle */}
        <div className="flex border border-slate-200 rounded-lg overflow-hidden shrink-0">
          {TABS.map(tab => {
            const Icon = tab.icon;
            const active = activeTab === tab.key;
            return (
              <button
                key={tab.key}
                onClick={() => setActiveTab(tab.key)}
                className={`flex items-center gap-1.5 px-3.5 py-1.5 text-sm font-medium transition-colors border-r last:border-r-0 border-slate-200 ${
                  active
                    ? 'bg-[#003366] text-white'
                    : 'bg-white text-slate-600 hover:bg-slate-50'
                }`}
              >
                <Icon className="h-3.5 w-3.5" />
                {tab.label}
              </button>
            );
          })}
        </div>
      </div>

      {/* ── Term guard ── */}
      {termStatus && !isTermEditable && (
        <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-2.5 text-center">
          <p className="text-sm text-red-700 font-medium">
            Dönem durumu "{termStatus}" — atama işlemleri yalnızca "Planning" veya "Scheduling" aşamasında yapılabilir.
          </p>
        </div>
      )}

      {/* ── Tab content ── */}
      <div className="flex-1 min-h-0 mt-4 flex flex-col overflow-hidden">
        {activeTab === 'list' && (
          <div className="flex-1 overflow-auto">
            <SectionListTab facultyId={facultyId} termId={termId} departments={departments} readOnly={!isTermEditable} />
          </div>
        )}
        {activeTab === 'calendar' && (
          <CalendarViewTab facultyId={facultyId} termId={termId} departments={departments} />
        )}
        {activeTab === 'auto-schedule' && (
          <div className="flex-1 overflow-hidden">
            <AutoScheduleTab facultyId={facultyId} termId={termId} onTabChange={(tab) => setActiveTab(tab as TabKey)} />
          </div>
        )}
      </div>

      <ConflictBar facultyId={facultyId} termId={termId} />
    </div>
  );
}
