// SIS.Web/src/pages/FacultyPlanningPage.tsx
import { useState } from 'react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import {
  AlertCircle, AlertTriangle, Building2, Calendar, Check, CheckCircle2,
  ChevronDown, Minus, Plus, RefreshCcw, Search, X, Zap,
} from 'lucide-react';
import { useAcademicOperationsContext } from '../hooks/useAcademicOperations';
import { useActiveTerm } from '../hooks/useActiveTerm';
import {
  useApplySchedule, useAssignSection, useAutoSchedulePreview,
  useFacultyPlanningConflicts, useFacultyPlanningOverview,
  useFacultyPlanningSections, useUnassignSection,
} from '../hooks/useFacultyPlanning';
import type {
  AutoSchedulePreviewItem, FacultyPlanningSection,
} from '../types/facultyPlanning';
import type { SchedulingConflict } from '../types/scheduling';

type TabKey = 'overview' | 'sections' | 'auto-schedule' | 'conflicts';

const TABS: { key: TabKey; label: string }[] = [
  { key: 'overview', label: 'Genel Bakış' },
  { key: 'sections', label: 'Şube Listesi' },
  { key: 'auto-schedule', label: 'Otomatik Zamanlama' },
  { key: 'conflicts', label: 'Çakışma Analizi' },
];

export default function FacultyPlanningPage() {
  const [activeTab, setActiveTab] = useState<TabKey>('overview');
  const contextQuery = useAcademicOperationsContext();
  const { data: activeTerm } = useActiveTerm();

  // Resolve facultyId from user's department context
  // For now, derive from first accessible department; backend resolves faculty from department
  const departments = contextQuery.data?.accessibleDepartments ?? [];
  const defaultDeptId = contextQuery.data?.defaultDepartmentId ?? null;

  // Faculty ID will come from a faculty-level API or be derived
  // Placeholder: use 1 until faculty selector is built from /api/faculty-planning/overview
  const [facultyId] = useState<number>(1); // TODO: resolve from context
  const termId = activeTerm?.id ?? null;

  if (contextQuery.isLoading) return <Skeleton className="h-96 rounded-xl" />;

  if (contextQuery.isError) {
    return (
      <Alert variant="destructive">
        <AlertCircle className="h-4 w-4" />
        <AlertTitle>Fakülte planlama sayfası yüklenemedi</AlertTitle>
        <AlertDescription>Yetkili kapsam bilgileri alınamadı.</AlertDescription>
      </Alert>
    );
  }

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap gap-2">
        {TABS.map(tab => (
          <Button
            key={tab.key}
            variant={activeTab === tab.key ? 'default' : 'outline'}
            size="sm"
            onClick={() => setActiveTab(tab.key)}
          >
            {tab.label}
          </Button>
        ))}
      </div>

      {activeTab === 'overview' && <OverviewTab facultyId={facultyId} termId={termId} />}
      {activeTab === 'sections' && (
        <SectionListTab facultyId={facultyId} termId={termId} departments={departments} />
      )}
      {activeTab === 'auto-schedule' && <AutoScheduleTab facultyId={facultyId} termId={termId} />}
      {activeTab === 'conflicts' && <ConflictAnalysisTab facultyId={facultyId} termId={termId} />}
    </div>
  );
}

function OverviewTab({ facultyId, termId }: { facultyId: number; termId: number | null }) {
  const overviewQuery = useFacultyPlanningOverview(facultyId, termId);

  if (!termId || overviewQuery.isLoading) return <Skeleton className="h-64 rounded-xl" />;
  if (overviewQuery.isError) return <Alert variant="destructive"><AlertTitle>Genel bakış yüklenemedi</AlertTitle></Alert>;

  const ov = overviewQuery.data;
  if (!ov) return null;

  const progressPercent = ov.totalSections > 0
    ? Math.round((ov.scheduledSections / ov.totalSections) * 100) : 0;

  return (
    <div className="space-y-6">
      {/* Progress bar */}
      <Card>
        <CardHeader>
          <CardTitle>{ov.facultyName} — {ov.termName}</CardTitle>
          <CardDescription>{ov.scheduledSections}/{ov.totalSections} şube zamanlandı (%{progressPercent})</CardDescription>
        </CardHeader>
        <CardContent>
          <div className="h-3 bg-muted rounded-full overflow-hidden">
            <div className="h-full bg-primary rounded-full transition-all" style={{ width: `${progressPercent}%` }} />
          </div>
        </CardContent>
      </Card>

      {/* Pending approval warnings */}
      {ov.pendingApprovalWarnings.length > 0 && (
        <Alert>
          <AlertTriangle className="h-4 w-4" />
          <AlertTitle>Onay Bekleyen Bölümler</AlertTitle>
          <AlertDescription className="space-y-1">
            {ov.pendingApprovalWarnings.map(w => (
              <p key={w.departmentId}>{w.departmentName}: <strong>{w.pendingCount}</strong> ders onay bekliyor</p>
            ))}
          </AlertDescription>
        </Alert>
      )}

      {/* Department progress cards */}
      <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-4">
        {ov.departmentProgress.map(dept => (
          <Card key={dept.departmentId}>
            <CardHeader className="pb-2">
              <CardTitle className="text-sm">{dept.departmentCode} — {dept.departmentName}</CardTitle>
            </CardHeader>
            <CardContent>
              <div className="flex justify-between text-sm">
                <span>Zamanlanmış: <strong>{dept.scheduledSections}/{dept.totalSections}</strong></span>
                {dept.pendingApprovals > 0 && (
                  <Badge variant="warning">{dept.pendingApprovals} onay bekliyor</Badge>
                )}
              </div>
            </CardContent>
          </Card>
        ))}
      </div>

      {/* Conflict summary */}
      {ov.conflictCount > 0 && (
        <Card>
          <CardHeader><CardTitle className="text-sm">Çakışma Özeti</CardTitle></CardHeader>
          <CardContent className="flex flex-wrap gap-2">
            {Object.entries(ov.conflictCountByType).map(([type, count]) => (
              <Badge key={type} variant="destructive">{type}: {count}</Badge>
            ))}
          </CardContent>
        </Card>
      )}

      {/* Recent assignments */}
      {ov.recentAssignments.length > 0 && (
        <Card>
          <CardHeader><CardTitle className="text-sm">Son İşlemler</CardTitle></CardHeader>
          <CardContent>
            <div className="space-y-2 text-sm">
              {ov.recentAssignments.map((log, i) => (
                <div key={i} className="flex justify-between text-muted-foreground">
                  <span>
                    <strong>{log.assignedByUserName}</strong> — {log.courseCode} →{' '}
                    {log.classroomName ?? 'Derslik yok'} {log.scheduleSummary ?? ''}
                  </span>
                  <span>{new Date(log.assignedAt).toLocaleString('tr-TR')}</span>
                </div>
              ))}
            </div>
          </CardContent>
        </Card>
      )}
    </div>
  );
}

const STATUS_CONFIG: Record<string, { label: string; variant: 'default' | 'success' | 'destructive' | 'warning' | 'outline' }> = {
  Unscheduled: { label: 'Zamanlanmamış', variant: 'outline' },
  Scheduled: { label: 'Zamanlanmış', variant: 'success' },
  Conflicted: { label: 'Çakışmalı', variant: 'destructive' },
  Partial: { label: 'Kısmi', variant: 'warning' },
};

function SectionListTab({
  facultyId, termId, departments,
}: {
  facultyId: number;
  termId: number | null;
  departments: { departmentId: number; departmentCode: string; departmentName: string }[];
}) {
  const [filterDept, setFilterDept] = useState<number | undefined>();
  const [filterStatus, setFilterStatus] = useState<string | undefined>();
  const [page, setPage] = useState(1);
  const [expandedId, setExpandedId] = useState<number | null>(null);

  const sectionsQuery = useFacultyPlanningSections(facultyId, termId, filterDept, filterStatus, page);
  const assignMutation = useAssignSection(facultyId, termId ?? 0);
  const unassignMutation = useUnassignSection(facultyId, termId ?? 0);

  if (!termId) return <Card><CardContent className="py-8 text-center text-muted-foreground">Aktif dönem bulunamadı.</CardContent></Card>;

  return (
    <div className="space-y-4">
      {/* Filters */}
      <div className="flex flex-wrap gap-3">
        <select
          className="h-9 rounded-md border border-input bg-background px-3 text-sm"
          value={filterDept ?? ''}
          onChange={e => { setFilterDept(e.target.value ? Number(e.target.value) : undefined); setPage(1); }}
        >
          <option value="">Tüm Bölümler</option>
          {departments.map(d => <option key={d.departmentId} value={d.departmentId}>{d.departmentCode}</option>)}
        </select>
        <select
          className="h-9 rounded-md border border-input bg-background px-3 text-sm"
          value={filterStatus ?? ''}
          onChange={e => { setFilterStatus(e.target.value || undefined); setPage(1); }}
        >
          <option value="">Tüm Durumlar</option>
          {Object.entries(STATUS_CONFIG).map(([k, v]) => <option key={k} value={k}>{v.label}</option>)}
        </select>
      </div>

      {/* Table */}
      {sectionsQuery.isLoading ? <Skeleton className="h-64" /> : (
        <Card>
          <CardContent className="pt-4">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Ders</TableHead>
                  <TableHead>Bölüm</TableHead>
                  <TableHead>Kapasite</TableHead>
                  <TableHead>Derslik</TableHead>
                  <TableHead>Saat</TableHead>
                  <TableHead>Hoca</TableHead>
                  <TableHead>Durum</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {(sectionsQuery.data?.items ?? []).map(s => (
                  <SectionRow
                    key={s.sectionId}
                    section={s}
                    expanded={expandedId === s.sectionId}
                    onToggle={() => setExpandedId(prev => prev === s.sectionId ? null : s.sectionId)}
                    onAssign={(classroomId, slots, instructorId) =>
                      assignMutation.mutate({ sectionId: s.sectionId, classroomId, scheduleSlots: slots, instructorId })
                    }
                    onUnassign={() => unassignMutation.mutate(s.sectionId)}
                    isAssigning={assignMutation.isPending}
                  />
                ))}
              </TableBody>
            </Table>

            {/* Pagination */}
            {sectionsQuery.data && sectionsQuery.data.totalCount > 50 && (
              <div className="flex justify-center gap-2 mt-4">
                <Button size="sm" variant="outline" disabled={page <= 1} onClick={() => setPage(p => p - 1)}>Önceki</Button>
                <span className="text-sm py-1">Sayfa {page} / {Math.ceil(sectionsQuery.data.totalCount / 50)}</span>
                <Button size="sm" variant="outline" disabled={page * 50 >= sectionsQuery.data.totalCount} onClick={() => setPage(p => p + 1)}>Sonraki</Button>
              </div>
            )}
          </CardContent>
        </Card>
      )}
    </div>
  );
}

function SectionRow({ section: s, expanded, onToggle, onAssign, onUnassign, isAssigning }: {
  section: FacultyPlanningSection;
  expanded: boolean;
  onToggle: () => void;
  onAssign: (classroomId?: number, slots?: any[], instructorId?: number) => void;
  onUnassign: () => void;
  isAssigning: boolean;
}) {
  const [classroomId, setClassroomId] = useState<string>(s.classroomId?.toString() ?? '');
  const [day, setDay] = useState('1');
  const [startHour, setStartHour] = useState('9');
  const totalHours = s.theoryHours + s.labHours || 3;

  const statusCfg = STATUS_CONFIG[s.status] ?? STATUS_CONFIG.Unscheduled;

  return (
    <>
      <TableRow className="cursor-pointer hover:bg-muted/30" onClick={onToggle}>
        <TableCell>
          <div>
            <span className="font-mono text-xs font-semibold text-primary">{s.courseCode}</span>
            <span className="text-sm ml-2">{s.courseName}</span>
            <span className="text-xs text-muted-foreground ml-1">Şube {s.sectionIndex}</span>
          </div>
        </TableCell>
        <TableCell>{s.departmentCode}</TableCell>
        <TableCell>{s.capacity}</TableCell>
        <TableCell>{s.classroomName ?? '—'}</TableCell>
        <TableCell>{s.scheduleSummary || '—'}</TableCell>
        <TableCell>{s.instructorName ?? '—'}</TableCell>
        <TableCell><Badge variant={statusCfg.variant}>{statusCfg.label}</Badge></TableCell>
      </TableRow>
      {expanded && (
        <TableRow className="bg-muted/20">
          <TableCell colSpan={7} className="p-4">
            <div className="flex flex-wrap items-end gap-4">
              <div className="space-y-1">
                <label className="text-xs font-medium">Derslik ID</label>
                <Input className="h-8 w-24" value={classroomId} onChange={e => setClassroomId(e.target.value)} placeholder="Derslik ID" />
              </div>
              <div className="space-y-1">
                <label className="text-xs font-medium">Gün</label>
                <select className="h-8 rounded-md border px-2 text-sm" value={day} onChange={e => setDay(e.target.value)}>
                  <option value="1">Pzt</option><option value="2">Sal</option><option value="3">Çar</option>
                  <option value="4">Per</option><option value="5">Cum</option>
                </select>
              </div>
              <div className="space-y-1">
                <label className="text-xs font-medium">Saat</label>
                <select className="h-8 rounded-md border px-2 text-sm" value={startHour} onChange={e => setStartHour(e.target.value)}>
                  {Array.from({ length: 10 }, (_, i) => i + 8).map(h => (
                    <option key={h} value={h}>{`${h}:00-${h + totalHours}:00`}</option>
                  ))}
                </select>
              </div>
              <div className="flex gap-2">
                <Button size="sm" disabled={isAssigning} onClick={() => {
                  const slots = [{ day: Number(day), startTime: `${startHour.padStart(2, '0')}:00`, endTime: `${(Number(startHour) + totalHours).toString().padStart(2, '0')}:00` }];
                  onAssign(classroomId ? Number(classroomId) : undefined, slots, undefined);
                }}>
                  <Check className="h-4 w-4 mr-1" /> Ata
                </Button>
                {s.status !== 'Unscheduled' && (
                  <Button size="sm" variant="destructive" onClick={onUnassign}>
                    <X className="h-4 w-4 mr-1" /> Kaldır
                  </Button>
                )}
              </div>
            </div>
          </TableCell>
        </TableRow>
      )}
    </>
  );
}

function AutoScheduleTab({ facultyId, termId }: { facultyId: number; termId: number | null }) {
  const previewMutation = useAutoSchedulePreview();
  const applyMutation = useApplySchedule(facultyId, termId ?? 0);
  const [selectedIds, setSelectedIds] = useState<Set<number>>(new Set());

  if (!termId) return null;

  const handlePreview = () => {
    previewMutation.mutate({ facultyId, termId });
  };

  const handleApplySelected = () => {
    const items = (previewMutation.data?.assignments ?? [])
      .filter(a => selectedIds.has(a.sectionId))
      .map(a => ({ sectionId: a.sectionId, classroomId: a.classroomId, scheduleSlots: a.scheduleSlots, instructorId: a.instructorId ?? undefined }));
    if (items.length > 0) applyMutation.mutate({ assignments: items });
  };

  const handleApplyAll = () => {
    const items = (previewMutation.data?.assignments ?? [])
      .map(a => ({ sectionId: a.sectionId, classroomId: a.classroomId, scheduleSlots: a.scheduleSlots, instructorId: a.instructorId ?? undefined }));
    if (items.length > 0) applyMutation.mutate({ assignments: items });
  };

  const toggleSelect = (id: number) => setSelectedIds(prev => {
    const next = new Set(prev);
    next.has(id) ? next.delete(id) : next.add(id);
    return next;
  });

  return (
    <div className="space-y-4">
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

      {previewMutation.data && (
        <>
          <div className="flex gap-2">
            <Badge variant="success">{previewMutation.data.successCount} başarılı</Badge>
            <Badge variant="destructive">{previewMutation.data.failedCount} başarısız</Badge>
          </div>

          {previewMutation.data.assignments.length > 0 && (
            <Card>
              <CardHeader className="flex flex-row items-center justify-between">
                <CardTitle className="text-sm">Önizleme</CardTitle>
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
                    {previewMutation.data.assignments.map(a => (
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

          {previewMutation.data.failures.length > 0 && (
            <Card>
              <CardHeader><CardTitle className="text-sm text-destructive">Atanamayan Şubeler</CardTitle></CardHeader>
              <CardContent>
                {previewMutation.data.failures.map(f => (
                  <div key={f.sectionId} className="text-sm py-1">
                    <span className="font-mono">{f.courseCode}</span> — {f.reason}
                  </div>
                ))}
              </CardContent>
            </Card>
          )}

          {applyMutation.data && (
            <Alert variant={applyMutation.data.failedCount > 0 ? 'destructive' : 'default'}>
              <AlertTitle>Uygulama Sonucu</AlertTitle>
              <AlertDescription>
                {applyMutation.data.successCount} başarılı, {applyMutation.data.failedCount} başarısız
                {applyMutation.data.errors.map((e, i) => <p key={i} className="text-xs">{e}</p>)}
              </AlertDescription>
            </Alert>
          )}
        </>
      )}
    </div>
  );
}

function ConflictAnalysisTab({ facultyId, termId }: { facultyId: number; termId: number | null }) {
  const conflictsQuery = useFacultyPlanningConflicts(facultyId, termId);

  if (!termId || conflictsQuery.isLoading) return <Skeleton className="h-64" />;

  const conflicts = conflictsQuery.data ?? [];

  if (conflicts.length === 0) {
    return (
      <Card>
        <CardContent className="py-16 text-center text-muted-foreground">
          <CheckCircle2 className="h-8 w-8 mx-auto mb-2 text-green-500" />
          Çakışma bulunamadı
        </CardContent>
      </Card>
    );
  }

  const grouped = conflicts.reduce<Record<string, SchedulingConflict[]>>((acc, c) => {
    (acc[c.conflictType] ??= []).push(c);
    return acc;
  }, {});

  return (
    <div className="space-y-4">
      {Object.entries(grouped).map(([type, items]) => (
        <Card key={type}>
          <CardHeader>
            <CardTitle className="text-sm flex items-center gap-2">
              <Badge variant="destructive">{items.length}</Badge>
              {type}
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-2">
            {items.map((c, i) => (
              <div key={i} className="text-sm p-3 bg-muted/30 rounded-md">
                <p className="font-medium">{c.courseCode} — {c.courseName}</p>
                <p className="text-muted-foreground">{c.message}</p>
                {c.classroomName && <p className="text-xs">Derslik: {c.classroomName}</p>}
                {c.instructorName && <p className="text-xs">Hoca: {c.instructorName}</p>}
              </div>
            ))}
          </CardContent>
        </Card>
      ))}
    </div>
  );
}
