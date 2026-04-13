// SIS.Web/src/components/faculty-planning/AutoScheduleTab.tsx
import { useCallback, useEffect, useMemo, useState } from 'react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { useNavigate } from 'react-router-dom';
import {
  useApplySchedule, useAutoSchedulePreview, useAvailableClassrooms,
  useBatchUnassignSections, useFacultyPlanningOverview, useFacultyPlanningSections,
  useResetAllSchedules, useScheduleRun, useScheduleRuns,
} from '../../hooks/useFacultyPlanning';
import type { AutoSchedulePreview } from '../../types/facultyPlanning';
import { ScheduleTopBar } from './auto-schedule/ScheduleTopBar';
import { ScheduleListPanel } from './auto-schedule/ScheduleListPanel';
import { ScheduleDetailPanel } from './auto-schedule/ScheduleDetailPanel';

interface AutoScheduleTabProps {
  facultyId: number;
  termId: number | null;
  onTabChange?: (tab: string) => void;
}

export function AutoScheduleTab({ facultyId, termId, onTabChange }: AutoScheduleTabProps) {
  const navigate = useNavigate();
  const previewMutation = useAutoSchedulePreview();
  const applyMutation = useApplySchedule();
  const batchUnassign = useBatchUnassignSections();
  const resetAll = useResetAllSchedules();

  // Queries
  const runsQuery = useScheduleRuns(facultyId, termId);
  const overviewQuery = useFacultyPlanningOverview(facultyId, termId);
  const unscheduledQuery = useFacultyPlanningSections(facultyId, termId, undefined, 'Unscheduled', 1, 200);
  const classroomsQuery = useAvailableClassrooms(termId);

  // State
  const [selectedRunId, setSelectedRunId] = useState<number | null>(null);
  const [selectedItemId, setSelectedItemId] = useState<number | null>(null);
  const [selectedIds, setSelectedIds] = useState<Set<number>>(new Set());
  const [lastAppliedIds, setLastAppliedIds] = useState<number[]>([]);

  const runDetailQuery = useScheduleRun(selectedRunId);

  // Auto-select first run
  useEffect(() => {
    if (runsQuery.data?.length && selectedRunId === null && !previewMutation.data)
      setSelectedRunId(runsQuery.data[0].id);
  }, [runsQuery.data, selectedRunId, previewMutation.data]);

  const previewData: AutoSchedulePreview | undefined =
    previewMutation.data ?? runDetailQuery.data?.result;

  // Computed metrics
  const ov = overviewQuery.data;
  const unscheduledCount = ov?.unscheduledSections ?? 0;
  const availableClassroomCount = classroomsQuery.data?.length;
  const occupancyPct = ov && ov.totalSections > 0
    ? Math.round((ov.scheduledSections / ov.totalSections) * 100)
    : 0;

  // Handlers
  const handleRun = useCallback(() => {
    if (!termId) return;
    setSelectedRunId(null);
    setSelectedItemId(null);
    previewMutation.mutate({ facultyId, termId });
  }, [facultyId, termId, previewMutation]);

  const handleRunSelect = useCallback((id: number) => {
    setSelectedRunId(id);
    setSelectedItemId(null);
    previewMutation.reset();
    setSelectedIds(new Set());
    setLastAppliedIds([]);
  }, [previewMutation]);

  const handleApply = useCallback((onlySelected: boolean) => {
    const source = previewData?.assignments ?? [];
    const items = (onlySelected ? source.filter(a => selectedIds.has(a.sectionId)) : source)
      .map(a => ({
        sectionId: a.sectionId,
        classroomId: a.classroomId,
        scheduleSlots: a.scheduleSlots,
        instructorId: a.instructorId ?? undefined,
      }));
    if (items.length > 0) {
      applyMutation.mutate({ assignments: items }, {
        onSuccess: () => setLastAppliedIds(items.map(a => a.sectionId)),
      });
    }
  }, [previewData, selectedIds, applyMutation]);

  const handleToggleAll = useCallback(() => {
    if (!previewData) return;
    const allIds = previewData.assignments.map(a => a.sectionId);
    const allSelected = allIds.every(id => selectedIds.has(id));
    setSelectedIds(allSelected ? new Set() : new Set(allIds));
  }, [previewData, selectedIds]);

  const toggleCheck = useCallback((id: number) => {
    setSelectedIds(prev => {
      const n = new Set(prev);
      n.has(id) ? n.delete(id) : n.add(id);
      return n;
    });
  }, []);

  const handleNavigate = useCallback((target: string) => {
    if (target === 'calendar' || target === 'list') {
      onTabChange?.(target);
    } else {
      navigate(target);
    }
  }, [navigate, onTabChange]);

  // Selected item lookup
  const selectedSuccess = useMemo(
    () => previewData?.assignments.find(a => a.sectionId === selectedItemId),
    [previewData, selectedItemId],
  );
  const selectedFailure = useMemo(
    () => previewData?.failures.find(f => f.sectionId === selectedItemId),
    [previewData, selectedItemId],
  );

  if (!termId) return null;

  return (
    <div className="flex flex-col h-full gap-3">
      {/* Üst bar */}
      <ScheduleTopBar
        unscheduledCount={unscheduledCount}
        availableClassroomCount={availableClassroomCount}
        occupancyPct={occupancyPct}
        isPending={previewMutation.isPending}
        onRun={handleRun}
        runs={runsQuery.data ?? []}
        activeRunId={selectedRunId}
        onRunSelect={handleRunSelect}
      />

      {/* Ana içerik: Sol panel + Sağ panel */}
      <div className="flex flex-1 min-h-0 border border-border/50 rounded-lg overflow-hidden bg-background">
        {/* Sol panel */}
        <ScheduleListPanel
          previewData={previewData}
          unscheduledSections={unscheduledQuery.data?.items}
          selectedItemId={selectedItemId}
          onSelectItem={(id) => setSelectedItemId(id)}
          selectedIds={selectedIds}
          onToggleAll={handleToggleAll}
          onApply={handleApply}
          applyPending={applyMutation.isPending}
        />

        {/* Sağ panel */}
        <div className="flex-1 min-w-0">
          <ScheduleDetailPanel
            selectedSuccess={selectedSuccess}
            selectedFailure={selectedFailure}
            isChecked={selectedItemId != null && selectedIds.has(selectedItemId)}
            onToggleCheck={() => selectedItemId && toggleCheck(selectedItemId)}
            onNavigate={handleNavigate}
            unscheduledCount={unscheduledCount}
            availableClassroomCount={availableClassroomCount}
            hasPreviewData={!!previewData}
            previewData={previewData}
          />
        </div>
      </div>

      {/* Apply result + Undo */}
      {applyMutation.data && (
        <Alert variant={applyMutation.data.failedCount > 0 ? 'destructive' : 'default'}>
          <AlertTitle>Uygulama Sonucu</AlertTitle>
          <AlertDescription>
            {applyMutation.data.successCount} başarılı, {applyMutation.data.failedCount} başarısız
            {applyMutation.data.errors.map((e, i) => <p key={i} className="text-xs mt-1">{e}</p>)}
          </AlertDescription>
        </Alert>
      )}

      <div className="flex gap-2">
        {lastAppliedIds.length > 0 && (
          <Button
            variant="outline"
            size="sm"
            onClick={() => {
              if (confirm(`${lastAppliedIds.length} şubenin atamasını kaldırmak istediğinize emin misiniz?`))
                batchUnassign.mutate(lastAppliedIds, { onSuccess: () => setLastAppliedIds([]) });
            }}
            disabled={batchUnassign.isPending}
          >
            {batchUnassign.isPending ? 'Geri alınıyor...' : `Geri Al (${lastAppliedIds.length} şube)`}
          </Button>
        )}

        {termId && (ov?.scheduledSections ?? 0) > 0 && (
          <Button
            variant="destructive"
            size="sm"
            onClick={() => {
              if (confirm('Fakültenin TÜM ders atamalarını sıfırlamak istediğinize emin misiniz? (Pinlenmiş ve kilitli dersler hariç)'))
                resetAll.mutate({ facultyId, termId }, {
                  onSuccess: (data) => {
                    setLastAppliedIds([]);
                    previewMutation.reset();
                    alert(`${data.resetCount} şubenin ataması sıfırlandı.`);
                  },
                });
            }}
            disabled={resetAll.isPending}
          >
            {resetAll.isPending ? 'Sıfırlanıyor...' : 'Tüm Atamaları Sıfırla'}
          </Button>
        )}
      </div>
    </div>
  );
}
