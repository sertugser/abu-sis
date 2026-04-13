import { useEffect, useMemo, useState } from 'react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Collapsible, CollapsibleContent, CollapsibleTrigger } from '@/components/ui/collapsible';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { CheckCircle2, ChevronDown, ChevronRight, Clock, Hash, History, XCircle, Zap } from 'lucide-react';
import {
  useApplySchedule, useAutoSchedulePreview, useBatchUnassignSections,
  useScheduleRun, useScheduleRuns,
} from '../../hooks/useFacultyPlanning';
import type { AutoScheduleFailure, AutoSchedulePreview } from '../../types/facultyPlanning';

/* ── Helpers ─────────────────────────────────────────────────────── */

interface FailureGroup {
  key: string;
  label: string;
  color: string;
  bgColor: string;
  message: string;
  failures: AutoScheduleFailure[];
}

const SUGGESTION_TYPE_META: Record<string, { label: string; color: string; bgColor: string }> = {
  CapacityIncrease: { label: 'Kapasite Yetersiz', color: 'bg-orange-500', bgColor: 'bg-orange-50 border-orange-200' },
  ClassroomAdd: { label: 'Derslik Tipi Eksik', color: 'bg-purple-500', bgColor: 'bg-purple-50 border-purple-200' },
  TimeChange: { label: 'Zaman Çakışması', color: 'bg-blue-500', bgColor: 'bg-blue-50 border-blue-200' },
  InstructorReassign: { label: 'Hoca Çakışması', color: 'bg-yellow-500', bgColor: 'bg-yellow-50 border-yellow-200' },
  ConstraintRelax: { label: 'Kısıt Sorunu', color: 'bg-red-500', bgColor: 'bg-red-50 border-red-200' },
  Unknown: { label: 'Diğer', color: 'bg-gray-500', bgColor: 'bg-gray-50 border-gray-200' },
};

function groupFailures(failures: AutoScheduleFailure[]): FailureGroup[] {
  const groups = new Map<string, { failures: AutoScheduleFailure[]; message: string }>();

  for (const f of failures) {
    const type = f.suggestions?.[0]?.suggestionType ?? 'Unknown';
    const message = f.suggestions?.[0]?.message ?? f.reason;
    if (!groups.has(type)) {
      groups.set(type, { failures: [], message });
    }
    groups.get(type)!.failures.push(f);
  }

  return Array.from(groups.entries()).map(([key, { failures: items, message }]) => {
    const meta = SUGGESTION_TYPE_META[key] ?? SUGGESTION_TYPE_META.Unknown;
    return { key, label: meta.label, color: meta.color, bgColor: meta.bgColor, message, failures: items };
  }).sort((a, b) => b.failures.length - a.failures.length);
}

/* ── Component ───────────────────────────────────────────────────── */

export function AutoScheduleTab({ facultyId, termId }: { facultyId: number; termId: number | null }) {
  const previewMutation = useAutoSchedulePreview();
  const applyMutation = useApplySchedule();
  const batchUnassign = useBatchUnassignSections();
  const [selectedIds, setSelectedIds] = useState<Set<number>>(new Set());
  const [lastAppliedIds, setLastAppliedIds] = useState<number[]>([]);
  const [selectedRunId, setSelectedRunId] = useState<number | null>(null);
  const [expandedGroups, setExpandedGroups] = useState<Set<string>>(new Set());

  const runsQuery = useScheduleRuns(facultyId, termId);
  const runDetailQuery = useScheduleRun(selectedRunId);

  useEffect(() => {
    if (runsQuery.data && runsQuery.data.length > 0 && selectedRunId === null && !previewMutation.data) {
      setSelectedRunId(runsQuery.data[0].id);
    }
  }, [runsQuery.data, selectedRunId, previewMutation.data]);

  const previewData: AutoSchedulePreview | undefined =
    previewMutation.data ?? runDetailQuery.data?.result;

  const failureGroups = useMemo(
    () => (previewData?.failures ? groupFailures(previewData.failures) : []),
    [previewData?.failures],
  );

  // Auto-expand the first group when data changes
  useEffect(() => {
    if (failureGroups.length > 0) {
      setExpandedGroups(new Set([failureGroups[0].key]));
    }
  }, [failureGroups]);

  if (!termId) return null;

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

  const toggleGroup = (key: string) => setExpandedGroups(prev => {
    const next = new Set(prev);
    if (next.has(key)) { next.delete(key); } else { next.add(key); }
    return next;
  });

  const formatDate = (dateStr: string) => {
    const d = new Date(dateStr);
    return d.toLocaleString('tr-TR', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' });
  };

  return (
    <div className="space-y-4">
      {/* ── Section 1: Header + Controls ─────────────────────────── */}
      <Card>
        <CardHeader className="flex flex-row items-center justify-between">
          <div>
            <CardTitle>Otomatik Zamanlama</CardTitle>
            <CardDescription>Zamanlanmamış şubeler için derslik ve saat önerisi üretir.</CardDescription>
          </div>
          <Button onClick={handlePreview} disabled={previewMutation.isPending}>
            <Zap className="h-4 w-4 mr-1" />
            {previewMutation.isPending ? 'Hesaplanıyor...' : 'Otomatik Ata'}
          </Button>
        </CardHeader>
      </Card>

      {/* Run history selector */}
      {runsQuery.data && runsQuery.data.length > 0 && (
        <div className="flex items-center gap-3">
          <History className="h-4 w-4 text-muted-foreground" />
          <span className="text-sm text-muted-foreground">Geçmiş:</span>
          <Select
            value={selectedRunId?.toString() ?? ''}
            onValueChange={handleRunSelect}
          >
            <SelectTrigger className="w-[400px]">
              <SelectValue placeholder="Bir çalıştırma seçin" />
            </SelectTrigger>
            <SelectContent>
              {runsQuery.data.map(run => (
                <SelectItem key={run.id} value={run.id.toString()}>
                  {formatDate(run.createdAt)} — {run.successCount} başarılı, {run.failedCount} başarısız (Seed: {run.seed})
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
          {runDetailQuery.isLoading && (
            <span className="text-xs text-muted-foreground">Yükleniyor...</span>
          )}
        </div>
      )}

      {previewData && (
        <>
          {/* ── Section 2: Summary ───────────────────────────────── */}
          <div className="grid grid-cols-4 gap-3">
            <Card>
              <CardContent className="pt-4 pb-3 px-4 flex items-center gap-3">
                <CheckCircle2 className="h-5 w-5 text-green-600" />
                <div>
                  <p className="text-2xl font-bold text-green-700">{previewData.successCount}</p>
                  <p className="text-xs text-muted-foreground">Başarılı</p>
                </div>
              </CardContent>
            </Card>
            <Card>
              <CardContent className="pt-4 pb-3 px-4 flex items-center gap-3">
                <XCircle className="h-5 w-5 text-red-600" />
                <div>
                  <p className="text-2xl font-bold text-red-700">{previewData.failedCount}</p>
                  <p className="text-xs text-muted-foreground">Başarısız</p>
                </div>
              </CardContent>
            </Card>
            <Card>
              <CardContent className="pt-4 pb-3 px-4 flex items-center gap-3">
                <Clock className="h-5 w-5 text-muted-foreground" />
                <div>
                  <p className="text-2xl font-bold">{previewData.stats?.elapsedSeconds?.toFixed(1)}s</p>
                  <p className="text-xs text-muted-foreground">Süre</p>
                </div>
              </CardContent>
            </Card>
            <Card>
              <CardContent className="pt-4 pb-3 px-4 flex items-center gap-3">
                <Hash className="h-5 w-5 text-muted-foreground" />
                <div>
                  <p className="text-2xl font-bold">{previewData.seed}</p>
                  <p className="text-xs text-muted-foreground">Seed</p>
                </div>
              </CardContent>
            </Card>
          </div>

          {/* Failure breakdown bar */}
          {failureGroups.length > 0 && (
            <Card>
              <CardContent className="pt-4 pb-3 px-4 space-y-2">
                <p className="text-xs font-medium text-muted-foreground mb-2">Başarısızlık Dağılımı</p>
                {failureGroups.map(group => {
                  const pct = previewData.failedCount > 0
                    ? (group.failures.length / previewData.failedCount) * 100
                    : 0;
                  return (
                    <div key={group.key} className="flex items-center gap-2">
                      <span className="text-xs w-36 truncate">{group.label}</span>
                      <div className="flex-1 h-4 bg-muted rounded overflow-hidden">
                        <div
                          className={`h-full ${group.color} rounded transition-all`}
                          style={{ width: `${pct}%` }}
                        />
                      </div>
                      <span className="text-xs font-mono w-8 text-right">{group.failures.length}</span>
                    </div>
                  );
                })}
              </CardContent>
            </Card>
          )}

          {/* ── Section 3a: Başarılı Atamalar ────────────────────── */}
          {previewData.assignments.length > 0 && (
            <Card>
              <CardHeader className="flex flex-row items-center justify-between">
                <CardTitle className="text-sm">Başarılı Atamalar</CardTitle>
                <div className="flex gap-2">
                  <Button size="sm" onClick={handleApplySelected} disabled={selectedIds.size === 0 || applyMutation.isPending}>
                    Seçilenleri Uygula ({selectedIds.size})
                  </Button>
                  <Button size="sm" onClick={handleApplyAll} disabled={applyMutation.isPending}>
                    Tümünü Uygula
                  </Button>
                </div>
              </CardHeader>
              <CardContent>
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead className="w-8" />
                      <TableHead>Ders</TableHead>
                      <TableHead>Bölüm</TableHead>
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
                        <TableCell>
                          <span className="font-mono text-xs">{a.courseCode}-{a.sectionNumber}</span>{' '}
                          <span className="text-muted-foreground">{a.courseName}</span>
                        </TableCell>
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

          {/* ── Section 3b: Atanamayan Şubeler (grouped) ─────────── */}
          {failureGroups.length > 0 && (
            <div className="space-y-2">
              <h3 className="text-sm font-semibold text-destructive">
                Atanamayan Şubeler ({previewData.failures.length})
              </h3>
              {failureGroups.map(group => {
                const isOpen = expandedGroups.has(group.key);
                return (
                  <Collapsible key={group.key} open={isOpen} onOpenChange={() => toggleGroup(group.key)}>
                    <Card className={`border ${isOpen ? group.bgColor : ''}`}>
                      <CollapsibleTrigger asChild>
                        <button className="w-full text-left">
                          <CardHeader className="py-3 px-4 flex flex-row items-center gap-2 cursor-pointer hover:bg-muted/50 transition-colors">
                            {isOpen
                              ? <ChevronDown className="h-4 w-4 shrink-0" />
                              : <ChevronRight className="h-4 w-4 shrink-0" />
                            }
                            <div className={`h-3 w-3 rounded-full ${group.color} shrink-0`} />
                            <span className="text-sm font-medium">{group.label}</span>
                            <Badge variant="secondary" className="ml-1">{group.failures.length} şube</Badge>
                          </CardHeader>
                        </button>
                      </CollapsibleTrigger>
                      <CollapsibleContent>
                        <CardContent className="pt-0 pb-4 px-4 space-y-3">
                          <p className="text-xs text-muted-foreground border-l-2 border-muted pl-2">
                            {group.message}
                          </p>
                          <div className="flex flex-wrap gap-1.5">
                            {group.failures.map(f => (
                              <Badge key={f.sectionId} variant="outline" className="font-mono text-xs px-2 py-0.5">
                                {f.courseCode}-{f.sectionNumber} ({f.capacity})
                              </Badge>
                            ))}
                          </div>
                        </CardContent>
                      </CollapsibleContent>
                    </Card>
                  </Collapsible>
                );
              })}
            </div>
          )}

          {/* ── Apply result ─────────────────────────────────────── */}
          {applyMutation.data && (
            <Alert variant={applyMutation.data.failedCount > 0 ? 'destructive' : 'default'}>
              <AlertTitle>Uygulama Sonucu</AlertTitle>
              <AlertDescription>
                {applyMutation.data.successCount} başarılı, {applyMutation.data.failedCount} başarısız
                {applyMutation.data.errors.map((e, i) => <p key={i} className="text-xs">{e}</p>)}
              </AlertDescription>
            </Alert>
          )}

          {lastAppliedIds.length > 0 && (
            <Button
              variant="outline"
              size="sm"
              onClick={() => {
                if (confirm(`${lastAppliedIds.length} şubenin atamasını kaldırmak istediğinize emin misiniz?`)) {
                  batchUnassign.mutate(lastAppliedIds, {
                    onSuccess: () => setLastAppliedIds([]),
                  });
                }
              }}
              disabled={batchUnassign.isPending}
            >
              {batchUnassign.isPending
                ? 'Geri alınıyor...'
                : `Son Uygulamayı Geri Al (${lastAppliedIds.length} şube)`}
            </Button>
          )}
        </>
      )}
    </div>
  );
}
