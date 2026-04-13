import { useEffect, useMemo, useState } from 'react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Collapsible, CollapsibleContent, CollapsibleTrigger } from '@/components/ui/collapsible';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import {
  CheckCircle2, ChevronDown, ChevronRight, History, XCircle, Zap,
} from 'lucide-react';
import {
  useApplySchedule, useAutoSchedulePreview, useBatchUnassignSections,
  useScheduleRun, useScheduleRuns,
} from '../../hooks/useFacultyPlanning';
import type { AutoScheduleFailure, AutoSchedulePreview } from '../../types/facultyPlanning';

/* ── Failure grouping ──────────────────────────────────────────── */

interface FailureGroup {
  key: string;
  label: string;
  accent: string;
  message: string;
  failures: AutoScheduleFailure[];
}

const TYPE_META: Record<string, { label: string; accent: string }> = {
  CapacityIncrease: { label: 'Kapasite Yetersiz', accent: 'border-l-amber-500' },
  ClassroomAdd:     { label: 'Derslik Tipi Eksik', accent: 'border-l-violet-500' },
  TimeChange:       { label: 'Zaman Çakışması', accent: 'border-l-blue-500' },
  InstructorReassign: { label: 'Hoca Çakışması', accent: 'border-l-orange-500' },
  ConstraintRelax:  { label: 'Kısıt Sorunu', accent: 'border-l-red-500' },
  Unknown:          { label: 'Diğer', accent: 'border-l-slate-400' },
};

function groupFailures(failures: AutoScheduleFailure[]): FailureGroup[] {
  const groups = new Map<string, { failures: AutoScheduleFailure[]; message: string }>();
  for (const f of failures) {
    const type = f.suggestions?.[0]?.suggestionType ?? 'Unknown';
    const message = f.suggestions?.[0]?.message ?? f.reason;
    if (!groups.has(type)) groups.set(type, { failures: [], message });
    groups.get(type)!.failures.push(f);
  }
  return Array.from(groups.entries())
    .map(([key, { failures: items, message }]) => {
      const meta = TYPE_META[key] ?? TYPE_META.Unknown;
      return { key, label: meta.label, accent: meta.accent, message, failures: items };
    })
    .sort((a, b) => b.failures.length - a.failures.length);
}

/* ── Component ─────────────────────────────────────────────────── */

export function AutoScheduleTab({ facultyId, termId }: { facultyId: number; termId: number | null }) {
  const previewMutation = useAutoSchedulePreview();
  const applyMutation   = useApplySchedule();
  const batchUnassign   = useBatchUnassignSections();

  const [selectedIds, setSelectedIds]       = useState<Set<number>>(new Set());
  const [lastAppliedIds, setLastAppliedIds] = useState<number[]>([]);
  const [selectedRunId, setSelectedRunId]   = useState<number | null>(null);
  const [expandedGroups, setExpandedGroups] = useState<Set<string>>(new Set());

  const runsQuery      = useScheduleRuns(facultyId, termId);
  const runDetailQuery = useScheduleRun(selectedRunId);

  useEffect(() => {
    if (runsQuery.data?.length && selectedRunId === null && !previewMutation.data)
      setSelectedRunId(runsQuery.data[0].id);
  }, [runsQuery.data, selectedRunId, previewMutation.data]);

  const previewData: AutoSchedulePreview | undefined =
    previewMutation.data ?? runDetailQuery.data?.result;

  const failureGroups = useMemo(
    () => (previewData?.failures ? groupFailures(previewData.failures) : []),
    [previewData?.failures],
  );

  useEffect(() => {
    if (failureGroups.length > 0) setExpandedGroups(new Set([failureGroups[0].key]));
  }, [failureGroups]);

  if (!termId) return null;

  /* handlers */
  const handlePreview = () => {
    setSelectedRunId(null);
    previewMutation.mutate({ facultyId, termId });
  };

  const handleApply = (onlySelected: boolean) => {
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
  };

  const handleRunSelect = (v: string) => {
    setSelectedRunId(parseInt(v, 10));
    previewMutation.reset();
    setSelectedIds(new Set());
    setLastAppliedIds([]);
  };

  const toggle = (id: number) => setSelectedIds(prev => {
    const n = new Set(prev);
    n.has(id) ? n.delete(id) : n.add(id);
    return n;
  });

  const toggleGroup = (key: string) => setExpandedGroups(prev => {
    const n = new Set(prev);
    n.has(key) ? n.delete(key) : n.add(key);
    return n;
  });

  const fmtDate = (s: string) =>
    new Date(s).toLocaleString('tr-TR', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' });

  const total = previewData ? previewData.successCount + previewData.failedCount : 0;
  const successPct = total > 0 ? Math.round((previewData!.successCount / total) * 100) : 0;

  return (
    <div className="space-y-4">

      {/* ── Header ──────────────────────────────────────────────── */}
      <div className="flex items-center justify-between">
        <div>
          <h2 className="text-lg font-semibold">Otomatik Zamanlama</h2>
          <p className="text-xs text-muted-foreground">Zamanlanmamış şubeler için derslik ve saat önerisi üretir.</p>
        </div>
        <Button onClick={handlePreview} disabled={previewMutation.isPending}>
          <Zap className="h-4 w-4 mr-1.5" />
          {previewMutation.isPending ? 'Hesaplanıyor...' : 'Otomatik Ata'}
        </Button>
      </div>

      {/* ── Run history ─────────────────────────────────────────── */}
      {runsQuery.data && runsQuery.data.length > 0 && (
        <div className="flex items-center gap-2">
          <History className="h-3.5 w-3.5 text-muted-foreground" />
          <Select value={selectedRunId?.toString() ?? ''} onValueChange={handleRunSelect}>
            <SelectTrigger className="h-8 w-[380px] text-xs">
              <SelectValue placeholder="Geçmiş çalıştırma seçin" />
            </SelectTrigger>
            <SelectContent>
              {runsQuery.data.map(r => (
                <SelectItem key={r.id} value={r.id.toString()} className="text-xs">
                  {fmtDate(r.createdAt)} — {r.successCount}/{r.successCount + r.failedCount} başarılı
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
          {runDetailQuery.isLoading && <span className="text-xs text-muted-foreground animate-pulse">Yükleniyor...</span>}
        </div>
      )}

      {previewData && (
        <>
          {/* ── Summary strip ───────────────────────────────────── */}
          <Card>
            <CardContent className="py-4 px-5">
              <div className="flex items-center gap-6">
                {/* Success / Fail counts */}
                <div className="flex items-center gap-2">
                  <CheckCircle2 className="h-4 w-4 text-emerald-600" />
                  <span className="text-sm font-semibold text-emerald-700">{previewData.successCount}</span>
                  <span className="text-xs text-muted-foreground">başarılı</span>
                </div>
                <div className="flex items-center gap-2">
                  <XCircle className="h-4 w-4 text-red-500" />
                  <span className="text-sm font-semibold text-red-600">{previewData.failedCount}</span>
                  <span className="text-xs text-muted-foreground">başarısız</span>
                </div>

                {/* Mini progress bar */}
                <div className="flex items-center gap-2 flex-1 max-w-xs">
                  <div className="flex-1 h-2 bg-slate-100 rounded-full overflow-hidden">
                    <div
                      className="h-full bg-emerald-500 rounded-full transition-all duration-300"
                      style={{ width: `${successPct}%` }}
                    />
                  </div>
                  <span className="text-xs font-medium text-muted-foreground">%{successPct}</span>
                </div>

                {/* Meta */}
                <div className="ml-auto flex items-center gap-4 text-xs text-muted-foreground">
                  <span>{previewData.stats?.elapsedSeconds?.toFixed(1)}s</span>
                  <span className="font-mono">Seed {previewData.seed}</span>
                </div>
              </div>

              {/* Failure breakdown — compact inline pills */}
              {failureGroups.length > 0 && (
                <div className="flex items-center gap-2 mt-3 pt-3 border-t border-border/60">
                  <span className="text-[11px] text-muted-foreground uppercase tracking-wider font-medium shrink-0">Sorunlar:</span>
                  {failureGroups.map(g => (
                    <Badge key={g.key} variant="outline" className="text-[11px] gap-1 font-normal">
                      {g.label}
                      <span className="font-semibold">{g.failures.length}</span>
                    </Badge>
                  ))}
                </div>
              )}
            </CardContent>
          </Card>

          {/* ── Başarılı Atamalar ───────────────────────────────── */}
          {previewData.assignments.length > 0 && (
            <Card>
              <CardHeader className="py-3 px-5 flex flex-row items-center justify-between border-b border-border/60">
                <CardTitle className="text-sm font-semibold">
                  Başarılı Atamalar
                  <Badge variant="success" className="ml-2 text-[10px]">{previewData.assignments.length}</Badge>
                </CardTitle>
                <div className="flex gap-2">
                  <Button variant="outline" size="sm" onClick={() => handleApply(true)} disabled={selectedIds.size === 0 || applyMutation.isPending}>
                    Seçilenleri Uygula ({selectedIds.size})
                  </Button>
                  <Button size="sm" onClick={() => handleApply(false)} disabled={applyMutation.isPending}>
                    Tümünü Uygula
                  </Button>
                </div>
              </CardHeader>
              <CardContent className="p-0">
                <div className="border rounded-b-2xl overflow-hidden">
                  <Table>
                    <TableHeader>
                      <TableRow className="bg-muted/30">
                        <TableHead className="w-10 pl-4" />
                        <TableHead className="text-xs font-semibold">Ders</TableHead>
                        <TableHead className="text-xs font-semibold">Bölüm</TableHead>
                        <TableHead className="text-xs font-semibold">Derslik</TableHead>
                        <TableHead className="text-xs font-semibold">Program</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {previewData.assignments.map(a => (
                        <TableRow key={a.sectionId} className="hover:bg-muted/30 transition-colors">
                          <TableCell className="pl-4">
                            <input
                              type="checkbox"
                              className="rounded border-slate-300"
                              checked={selectedIds.has(a.sectionId)}
                              onChange={() => toggle(a.sectionId)}
                            />
                          </TableCell>
                          <TableCell>
                            <span className="font-mono text-xs font-medium">{a.courseCode}-{a.sectionNumber}</span>
                            <span className="text-xs text-muted-foreground ml-1.5">{a.courseName}</span>
                          </TableCell>
                          <TableCell className="text-xs">{a.departmentCode}</TableCell>
                          <TableCell className="text-xs">{a.classroomName}</TableCell>
                          <TableCell className="text-xs font-mono">{a.scheduleSummary}</TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </div>
              </CardContent>
            </Card>
          )}

          {/* ── Atanamayan Şubeler ──────────────────────────────── */}
          {failureGroups.length > 0 && (
            <div className="space-y-2">
              <h3 className="text-sm font-semibold text-destructive flex items-center gap-2">
                Atanamayan Şubeler
                <Badge variant="destructive" className="text-[10px]">{previewData.failures.length}</Badge>
              </h3>

              {failureGroups.map(group => {
                const isOpen = expandedGroups.has(group.key);
                return (
                  <Collapsible key={group.key} open={isOpen} onOpenChange={() => toggleGroup(group.key)}>
                    <Card className={`border-l-4 ${group.accent} transition-all`}>
                      <CollapsibleTrigger asChild>
                        <button className="w-full text-left">
                          <div className="flex items-center gap-2 px-4 py-2.5 cursor-pointer hover:bg-muted/30 transition-colors">
                            {isOpen
                              ? <ChevronDown className="h-3.5 w-3.5 text-muted-foreground shrink-0" />
                              : <ChevronRight className="h-3.5 w-3.5 text-muted-foreground shrink-0" />
                            }
                            <span className="text-sm font-medium">{group.label}</span>
                            <Badge variant="secondary" className="text-[10px] ml-1">{group.failures.length}</Badge>
                            {!isOpen && (
                              <span className="text-xs text-muted-foreground ml-2 truncate max-w-md">{group.message}</span>
                            )}
                          </div>
                        </button>
                      </CollapsibleTrigger>
                      <CollapsibleContent>
                        <div className="px-4 pb-3 space-y-2">
                          <p className="text-xs text-muted-foreground pl-6">{group.message}</p>
                          <div className="pl-6">
                            <Table>
                              <TableHeader>
                                <TableRow className="border-b border-border/40">
                                  <TableHead className="text-[11px] font-semibold py-1.5 pl-0">Ders Kodu</TableHead>
                                  <TableHead className="text-[11px] font-semibold py-1.5">Ad</TableHead>
                                  <TableHead className="text-[11px] font-semibold py-1.5 text-right pr-0">Kapasite</TableHead>
                                </TableRow>
                              </TableHeader>
                              <TableBody>
                                {group.failures.map(f => (
                                  <TableRow key={f.sectionId} className="border-b border-border/20">
                                    <TableCell className="font-mono text-xs py-1 pl-0">
                                      {f.courseCode}-{f.sectionNumber || '?'}
                                    </TableCell>
                                    <TableCell className="text-xs py-1 text-muted-foreground">{f.courseName}</TableCell>
                                    <TableCell className="text-xs py-1 text-right font-mono pr-0">{f.capacity || '?'}</TableCell>
                                  </TableRow>
                                ))}
                              </TableBody>
                            </Table>
                          </div>
                        </div>
                      </CollapsibleContent>
                    </Card>
                  </Collapsible>
                );
              })}
            </div>
          )}

          {/* ── Apply result / Undo ─────────────────────────────── */}
          {applyMutation.data && (
            <Alert variant={applyMutation.data.failedCount > 0 ? 'destructive' : 'default'}>
              <AlertTitle>Uygulama Sonucu</AlertTitle>
              <AlertDescription>
                {applyMutation.data.successCount} başarılı, {applyMutation.data.failedCount} başarısız
                {applyMutation.data.errors.map((e, i) => <p key={i} className="text-xs mt-1">{e}</p>)}
              </AlertDescription>
            </Alert>
          )}

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
        </>
      )}
    </div>
  );
}
