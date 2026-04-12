import { useEffect, useState } from 'react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { History, Zap } from 'lucide-react';
import {
  useApplySchedule, useAutoSchedulePreview, useBatchUnassignSections,
  useScheduleRun, useScheduleRuns,
} from '../../hooks/useFacultyPlanning';
import type { AutoSchedulePreview } from '../../types/facultyPlanning';

export function AutoScheduleTab({ facultyId, termId }: { facultyId: number; termId: number | null }) {
  const previewMutation = useAutoSchedulePreview();
  const applyMutation = useApplySchedule();
  const batchUnassign = useBatchUnassignSections();
  const [selectedIds, setSelectedIds] = useState<Set<number>>(new Set());
  const [lastAppliedIds, setLastAppliedIds] = useState<number[]>([]);
  const [selectedRunId, setSelectedRunId] = useState<number | null>(null);

  const runsQuery = useScheduleRuns(facultyId, termId);
  const runDetailQuery = useScheduleRun(selectedRunId);

  // Auto-select the most recent run on first load
  useEffect(() => {
    if (runsQuery.data && runsQuery.data.length > 0 && selectedRunId === null && !previewMutation.data) {
      setSelectedRunId(runsQuery.data[0].id);
    }
  }, [runsQuery.data, selectedRunId, previewMutation.data]);

  if (!termId) return null;

  // Determine which preview data to show:
  // - If a mutation just ran, show that
  // - Otherwise, show the selected past run
  const previewData: AutoSchedulePreview | undefined =
    previewMutation.data ?? runDetailQuery.data?.result;

  const handlePreview = () => {
    setSelectedRunId(null);
    previewMutation.mutate({ facultyId, termId });
  };

  const handleApplySelected = () => {
    const items = (previewData?.assignments ?? [])
      .filter(a => selectedIds.has(a.sectionId))
      .map(a => ({ sectionId: a.sectionId, classroomId: a.classroomId, scheduleSlots: a.scheduleSlots, instructorId: a.instructorId ?? undefined }));
    if (items.length > 0) {
      const request = { assignments: items };
      applyMutation.mutate(request, {
        onSuccess: () => setLastAppliedIds(request.assignments.map(a => a.sectionId)),
      });
    }
  };

  const handleApplyAll = () => {
    const items = (previewData?.assignments ?? [])
      .map(a => ({ sectionId: a.sectionId, classroomId: a.classroomId, scheduleSlots: a.scheduleSlots, instructorId: a.instructorId ?? undefined }));
    if (items.length > 0) {
      const request = { assignments: items };
      applyMutation.mutate(request, {
        onSuccess: () => setLastAppliedIds(request.assignments.map(a => a.sectionId)),
      });
    }
  };

  const handleRunSelect = (value: string) => {
    const id = parseInt(value, 10);
    setSelectedRunId(id);
    previewMutation.reset();
    setSelectedIds(new Set());
    setLastAppliedIds([]);
  };

  const toggleSelect = (id: number) => setSelectedIds(prev => {
    const next = new Set(prev);
    if (next.has(id)) { next.delete(id); } else { next.add(id); }
    return next;
  });

  const formatDate = (dateStr: string) => {
    const d = new Date(dateStr);
    return d.toLocaleString('tr-TR', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' });
  };

  return (
    <div className="space-y-4">
      <Card>
        <CardHeader className="flex flex-row items-center justify-between">
          <div>
            <CardTitle>Otomatik Zamanlama</CardTitle>
            <CardDescription>Zamanlanmamis subeler icin derslik ve saat onerisi uretir.</CardDescription>
          </div>
          <Button onClick={handlePreview} disabled={previewMutation.isPending}>
            <Zap className="h-4 w-4 mr-1" />
            {previewMutation.isPending ? 'Hesaplaniyor...' : 'Otomatik Ata'}
          </Button>
        </CardHeader>
      </Card>

      {/* Run history selector */}
      {runsQuery.data && runsQuery.data.length > 0 && (
        <div className="flex items-center gap-3">
          <History className="h-4 w-4 text-muted-foreground" />
          <span className="text-sm text-muted-foreground">Gecmis:</span>
          <Select
            value={selectedRunId?.toString() ?? ''}
            onValueChange={handleRunSelect}
          >
            <SelectTrigger className="w-[400px]">
              <SelectValue placeholder="Bir calistirma secin" />
            </SelectTrigger>
            <SelectContent>
              {runsQuery.data.map(run => (
                <SelectItem key={run.id} value={run.id.toString()}>
                  {formatDate(run.createdAt)} — {run.successCount} basarili, {run.failedCount} basarisiz (Seed: {run.seed})
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
          {runDetailQuery.isLoading && (
            <span className="text-xs text-muted-foreground">Yukleniyor...</span>
          )}
        </div>
      )}

      {previewData && (
        <>
          <div className="flex gap-2 items-center">
            <Badge variant="success">{previewData.successCount} basarili</Badge>
            <Badge variant="destructive">{previewData.failedCount} basarisiz</Badge>
            <span className="text-xs text-muted-foreground ml-2">
              {previewData.stats?.elapsedSeconds?.toFixed(1)}s | Seed: {previewData.seed}
            </span>
          </div>

          {previewData.assignments.length > 0 && (
            <Card>
              <CardHeader className="flex flex-row items-center justify-between">
                <CardTitle className="text-sm">Onizleme</CardTitle>
                <div className="flex gap-2">
                  <Button size="sm" onClick={handleApplySelected} disabled={selectedIds.size === 0 || applyMutation.isPending}>
                    Secilenleri Uygula ({selectedIds.size})
                  </Button>
                  <Button size="sm" onClick={handleApplyAll} disabled={applyMutation.isPending}>
                    Tumunu Uygula
                  </Button>
                </div>
              </CardHeader>
              <CardContent>
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead className="w-8" />
                      <TableHead>Ders</TableHead>
                      <TableHead>Bolum</TableHead>
                      <TableHead>Derslik</TableHead>
                      <TableHead>Saat</TableHead>
                      <TableHead>Uyum</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {previewData.assignments.map(a => (
                      <TableRow key={a.sectionId}>
                        <TableCell>
                          <input type="checkbox" checked={selectedIds.has(a.sectionId)} onChange={() => toggleSelect(a.sectionId)} />
                        </TableCell>
                        <TableCell><span className="font-mono text-xs">{a.courseCode}</span> {a.courseName}</TableCell>
                        <TableCell>{a.departmentCode}</TableCell>
                        <TableCell>{a.classroomName}</TableCell>
                        <TableCell>{a.scheduleSummary}</TableCell>
                        <TableCell>%{(a.fitScore * 100).toFixed(0)}</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </CardContent>
            </Card>
          )}

          {previewData.failures.length > 0 && (
            <Card>
              <CardHeader><CardTitle className="text-sm text-destructive">Atanamayan Subeler ({previewData.failures.length})</CardTitle></CardHeader>
              <CardContent className="space-y-2">
                {previewData.failures.map(f => (
                  <div key={f.sectionId} className="border-b pb-2 last:border-0">
                    <div className="text-sm font-medium">
                      <span className="font-mono">{f.courseCode}</span> {f.courseName}
                    </div>
                    {f.suggestions && f.suggestions.length > 0 ? (
                      <div className="mt-1 space-y-1">
                        {f.suggestions.map((s, i) => (
                          <div key={i} className="text-xs bg-muted rounded px-2 py-1 flex items-start gap-2">
                            <Badge variant="outline" className="text-[10px] shrink-0">
                              {s.suggestionType === 'CapacityIncrease' ? 'Kapasite' :
                               s.suggestionType === 'InstructorReassign' ? 'Hoca' :
                               s.suggestionType === 'ClassroomAdd' ? 'Derslik' :
                               s.suggestionType === 'TimeChange' ? 'Zaman' :
                               s.suggestionType === 'ConstraintRelax' ? 'Kisit' : s.suggestionType}
                            </Badge>
                            <span>{s.message}</span>
                          </div>
                        ))}
                      </div>
                    ) : (
                      <div className="text-xs text-muted-foreground mt-1">{f.reason}</div>
                    )}
                  </div>
                ))}
              </CardContent>
            </Card>
          )}

          {applyMutation.data && (
            <Alert variant={applyMutation.data.failedCount > 0 ? 'destructive' : 'default'}>
              <AlertTitle>Uygulama Sonucu</AlertTitle>
              <AlertDescription>
                {applyMutation.data.successCount} basarili, {applyMutation.data.failedCount} basarisiz
                {applyMutation.data.errors.map((e, i) => <p key={i} className="text-xs">{e}</p>)}
              </AlertDescription>
            </Alert>
          )}

          {lastAppliedIds.length > 0 && (
            <Button
              variant="outline"
              size="sm"
              onClick={() => {
                if (confirm(`${lastAppliedIds.length} subenin atamasini kaldirmak istediginize emin misiniz?`)) {
                  batchUnassign.mutate(lastAppliedIds, {
                    onSuccess: () => setLastAppliedIds([]),
                  });
                }
              }}
              disabled={batchUnassign.isPending}
            >
              {batchUnassign.isPending
                ? 'Geri aliniyor...'
                : `Son Uygulamayi Geri Al (${lastAppliedIds.length} sube)`}
            </Button>
          )}
        </>
      )}
    </div>
  );
}
