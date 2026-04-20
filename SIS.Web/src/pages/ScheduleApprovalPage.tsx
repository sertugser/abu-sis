import { useState } from 'react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogTrigger,
} from '@/components/ui/alert-dialog';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Dialog } from '@/components/ui/dialog';
import ResizableDialogContent from '@/components/ui/resizable-dialog';
import { Progress } from '@/components/ui/progress';
import { Skeleton } from '@/components/ui/skeleton';
import { Textarea } from '@/components/ui/textarea';
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/tooltip';
import {
  AlertCircle,
  AlertTriangle,
  Calendar,
  CalendarDays,
  CheckCircle2,
  Clock,
  Eye,
  GraduationCap,
  LayoutGrid,
  LayoutList,
  ShieldCheck,
  User,
  XCircle,
} from 'lucide-react';
import { cn } from '@/lib/utils';
import { useActiveTerm } from '../hooks/useActiveTerm';
import { useAdminFaculties } from '../hooks/useAdminAcademicStructure';
import {
  useScheduleApprovalStatus,
  useApproveSchedule,
  useRevokeScheduleApproval,
} from '../hooks/useScheduleApproval';
import {
  useFacultyPlanningOverview,
  useFacultyPlanningSections,
  useFacultyPlanningTimeSlots,
} from '../hooks/useFacultyPlanning';
import { CalendarGrid } from '../components/faculty-planning/CalendarGrid';
import { SectionListTab } from '../components/faculty-planning/SectionListTab';
import { ConflictBar } from '../components/faculty-planning/ConflictBar';

type ViewTab = 'calendar' | 'list';

/* ─── Stat Card ─── */

function StatCard({ icon: Icon, label, value, sub, color = 'blue', badge }: {
  icon: React.ElementType;
  label: string;
  value: string | number;
  sub?: string;
  color?: 'blue' | 'emerald' | 'amber' | 'red' | 'gray' | 'violet';
  badge?: React.ReactNode;
}) {
  const palette: Record<string, string> = {
    blue: 'bg-blue-50 text-blue-600 border-blue-100',
    emerald: 'bg-emerald-50 text-emerald-600 border-emerald-100',
    amber: 'bg-amber-50 text-amber-600 border-amber-100',
    red: 'bg-red-50 text-red-600 border-red-100',
    gray: 'bg-gray-50 text-gray-500 border-gray-100',
    violet: 'bg-violet-50 text-violet-600 border-violet-100',
  };
  return (
    <div className={cn('flex items-center gap-3 rounded-xl border px-4 py-3', palette[color])}>
      <div className="shrink-0 rounded-lg bg-white/70 p-2 shadow-sm">
        <Icon className="h-4 w-4" />
      </div>
      <div className="min-w-0 flex-1">
        <p className="text-[11px] font-medium opacity-75 leading-none mb-0.5">{label}</p>
        <div className="flex items-center gap-2">
          <p className="text-lg font-bold leading-none">{value}</p>
          {badge}
        </div>
        {sub && <p className="text-[10px] opacity-60 mt-0.5">{sub}</p>}
      </div>
    </div>
  );
}

/* ─── Main Page ─── */

export default function ScheduleApprovalPage() {
  const { data: activeTerm } = useActiveTerm();
  const { data: faculties, isLoading: facultiesLoading } = useAdminFaculties();
  const [selectedFacultyId, setSelectedFacultyId] = useState<number | null>(null);
  const facultyId = selectedFacultyId ?? faculties?.[0]?.id ?? null;
  const termId = activeTerm?.id;

  const { data: status, isLoading, isError } = useScheduleApprovalStatus(facultyId, termId);
  const approveMutation = useApproveSchedule(facultyId ?? 0, termId);
  const revokeMutation = useRevokeScheduleApproval(facultyId ?? 0, termId);

  const [note, setNote] = useState('');
  const [viewOpen, setViewOpen] = useState(false);
  const [viewTab, setViewTab] = useState<ViewTab>('calendar');
  const [viewFilterDept, setViewFilterDept] = useState<number | undefined>();

  const overviewQuery = useFacultyPlanningOverview(
    viewOpen ? facultyId : null,
    viewOpen ? (termId ?? null) : null,
  );
  const ov = overviewQuery.data;

  const allSectionsQuery = useFacultyPlanningSections(
    viewOpen ? facultyId : null,
    viewOpen ? (termId ?? null) : null,
    viewFilterDept, undefined, 1, 500,
  );
  const timeSlotsQuery = useFacultyPlanningTimeSlots(viewOpen ? (termId ?? null) : null);

  const handleApprove = () => {
    approveMutation.mutate(note || undefined, {
      onSuccess: () => setNote(''),
    });
  };

  /* Loading */
  if (facultiesLoading) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-12 rounded-lg" />
        <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
          {Array.from({ length: 4 }).map((_, i) => <Skeleton key={i} className="h-20 rounded-xl" />)}
        </div>
        <Skeleton className="h-48 rounded-xl" />
      </div>
    );
  }

  if (!facultyId) {
    return (
      <Alert>
        <AlertCircle className="h-4 w-4" />
        <AlertDescription>Sistemde tanımlı fakülte bulunmamaktadır.</AlertDescription>
      </Alert>
    );
  }

  const schedulePct = status && status.totalSections > 0
    ? Math.round((status.scheduledSections / status.totalSections) * 100)
    : 0;
  const unscheduledCount = status ? status.totalSections - status.scheduledSections : 0;

  const dialogProgressPct = ov && ov.totalSections > 0
    ? Math.round((ov.scheduledSections / ov.totalSections) * 100)
    : 0;

  return (
    <TooltipProvider>
      <div className="space-y-5">
        {/* ── Filter Bar ── */}
        <div className="flex flex-wrap items-center gap-3">
          <div className="flex items-center rounded-lg border border-gray-200 bg-white shadow-sm overflow-hidden">
            <span className="px-3 py-1.5 text-xs font-semibold text-gray-600 bg-gray-50 border-r border-gray-200 whitespace-nowrap">Fakülte</span>
            <select
              className="h-8 border-0 bg-transparent px-2.5 text-xs focus:ring-0 focus:outline-none min-w-[240px]"
              value={facultyId}
              onChange={e => setSelectedFacultyId(Number(e.target.value))}
            >
              {(faculties ?? []).map(f => (
                <option key={f.id} value={f.id}>{f.code} — {f.name}</option>
              ))}
            </select>
          </div>

          {activeTerm && (
            <Badge variant="outline" className="gap-1.5 h-8 px-3 text-xs rounded-lg">
              <Calendar className="h-3 w-3" />
              {activeTerm.name}
            </Badge>
          )}

          <div className="ml-auto">
            <Button
              variant="outline"
              size="sm"
              className="gap-1.5 h-8 text-xs rounded-lg"
              onClick={() => setViewOpen(true)}
            >
              <Eye className="h-3 w-3" />
              Ders Programını İncele
            </Button>
          </div>
        </div>

        {/* ── Content ── */}
        {isLoading ? (
          <div className="space-y-4">
            <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
              {Array.from({ length: 4 }).map((_, i) => <Skeleton key={i} className="h-20 rounded-xl" />)}
            </div>
            <Skeleton className="h-48 rounded-xl" />
          </div>
        ) : isError ? (
          <Alert variant="destructive">
            <AlertCircle className="h-4 w-4" />
            <AlertDescription>Onay durumu yüklenemedi. Lütfen sayfayı yenileyin.</AlertDescription>
          </Alert>
        ) : status ? (
          <>
            {/* ── Stat Cards ── */}
            <div className="grid grid-cols-2 lg:grid-cols-4 gap-3">
              <StatCard icon={LayoutGrid} label="Toplam Şube" value={status.totalSections} color="blue" />
              <StatCard
                icon={Clock}
                label="Zamanlanan"
                value={status.scheduledSections}
                sub={`/ ${status.totalSections} şube`}
                color={schedulePct === 100 ? 'emerald' : schedulePct >= 80 ? 'amber' : 'red'}
              />
              <StatCard
                icon={AlertTriangle}
                label="Programsız"
                value={unscheduledCount}
                color={unscheduledCount === 0 ? 'emerald' : 'amber'}
              />
              <StatCard
                icon={ShieldCheck}
                label="Onay Durumu"
                value={status.isApproved ? 'Onaylı' : 'Bekliyor'}
                badge={
                  status.isApproved
                    ? <Badge variant="success" className="text-[10px] px-1.5">Onaylı</Badge>
                    : <Badge variant="warning" className="text-[10px] px-1.5">Bekliyor</Badge>
                }
                color="violet"
              />
            </div>

            {/* ── Progress Bar ── */}
            {status.totalSections > 0 && (
              <div className="flex items-center gap-3 rounded-xl border bg-white px-4 py-3 shadow-sm">
                <span className="text-xs font-semibold text-gray-600 whitespace-nowrap">Programlama İlerlemesi</span>
                <div className="flex-1 relative">
                  <Progress value={schedulePct} className="h-2.5" />
                </div>
                <span className={cn(
                  'text-sm font-bold tabular-nums whitespace-nowrap',
                  schedulePct === 100 ? 'text-emerald-600' : schedulePct >= 80 ? 'text-amber-600' : 'text-gray-700',
                )}>
                  %{schedulePct}
                </span>
              </div>
            )}

            {/* ── Unscheduled Warning ── */}
            {unscheduledCount > 0 && (
              <div className="flex items-center gap-2.5 rounded-xl border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-800">
                <AlertTriangle className="h-4 w-4 shrink-0 text-amber-600" />
                <span><strong>{unscheduledCount} şube</strong> henüz programlanmamış. Onay vermeden önce tüm şubelerin programlanması önerilir.</span>
              </div>
            )}

            {/* ── Approval Card ── */}
            <Card className="overflow-hidden rounded-xl shadow-sm">
              {/* Header */}
              {status.isApproved ? (
                <div className="flex flex-wrap items-center gap-x-5 gap-y-2 border-b bg-emerald-50/70 px-5 py-3.5">
                  <div className="flex items-center gap-2 text-emerald-700">
                    <CheckCircle2 className="h-4.5 w-4.5" />
                    <span className="text-sm font-bold">Program Onaylandı</span>
                  </div>
                  {status.approvedByName && (
                    <div className="flex items-center gap-1.5 text-xs text-emerald-700/80">
                      <User className="h-3 w-3" />
                      <span>{status.approvedByName}</span>
                    </div>
                  )}
                  {status.approvedAt && (
                    <div className="flex items-center gap-1.5 text-xs text-emerald-700/80">
                      <Calendar className="h-3 w-3" />
                      <span>{new Date(status.approvedAt).toLocaleString('tr-TR')}</span>
                    </div>
                  )}
                </div>
              ) : (
                <div className="flex items-center gap-2.5 border-b bg-amber-50/70 px-5 py-3.5">
                  <Clock className="h-4 w-4 text-amber-600" />
                  <span className="text-sm font-bold text-amber-800">Onay Bekliyor</span>
                  <span className="text-xs text-amber-600/80">— Ders programını inceleyip onaylayabilirsiniz</span>
                </div>
              )}

              {/* Existing Note */}
              {status.note && (
                <div className="border-b px-5 py-3 bg-gray-50/50">
                  <div className="flex items-start gap-2.5">
                    <GraduationCap className="h-4 w-4 text-gray-500 mt-0.5 shrink-0" />
                    <div>
                      <p className="text-[11px] font-medium text-gray-500 mb-0.5">Onay Notu</p>
                      <p className="text-sm text-gray-800">{status.note}</p>
                    </div>
                  </div>
                </div>
              )}

              {/* Actions */}
              <div className="px-5 py-5">
                {!status.isApproved ? (
                  <div className="space-y-4">
                    <div>
                      <label className="text-xs font-semibold text-gray-600 mb-1.5 block">
                        Onay Notu <span className="font-normal text-gray-400">(Opsiyonel)</span>
                      </label>
                      <Textarea
                        placeholder="Programa dair notunuzu ekleyebilirsiniz..."
                        value={note}
                        onChange={e => setNote(e.target.value)}
                        rows={3}
                        className="resize-none rounded-lg"
                      />
                    </div>

                    <div className="flex items-center gap-3">
                      <Button
                        onClick={handleApprove}
                        disabled={approveMutation.isPending}
                        className="gap-2 rounded-lg"
                      >
                        <CheckCircle2 className="h-4 w-4" />
                        {approveMutation.isPending ? 'Onaylanıyor...' : 'Programı Onayla'}
                      </Button>
                      {unscheduledCount > 0 && (
                        <span className="text-xs text-gray-500">{unscheduledCount} programsız şube mevcut</span>
                      )}
                    </div>
                  </div>
                ) : (
                  <div className="flex items-center gap-3">
                    <AlertDialog>
                      <AlertDialogTrigger asChild>
                        <Button
                          variant="outline"
                          size="sm"
                          className="gap-2 text-red-600 border-red-200 hover:bg-red-50 hover:text-red-700 rounded-lg"
                          disabled={revokeMutation.isPending}
                        >
                          <XCircle className="h-3.5 w-3.5" />
                          {revokeMutation.isPending ? 'Geri alınıyor...' : 'Onayı Geri Al'}
                        </Button>
                      </AlertDialogTrigger>
                      <AlertDialogContent>
                        <AlertDialogHeader>
                          <AlertDialogTitle>Onayı Geri Al</AlertDialogTitle>
                          <AlertDialogDescription>
                            {status.facultyName} fakültesinin ders programı onayını geri almak
                            istediğinize emin misiniz? Bu işlem sonrası programın yeniden
                            onaylanması gerekecektir.
                          </AlertDialogDescription>
                        </AlertDialogHeader>
                        <AlertDialogFooter>
                          <AlertDialogCancel>Vazgeç</AlertDialogCancel>
                          <AlertDialogAction
                            onClick={() => revokeMutation.mutate()}
                            className="bg-red-600 hover:bg-red-700"
                          >
                            Onayı Geri Al
                          </AlertDialogAction>
                        </AlertDialogFooter>
                      </AlertDialogContent>
                    </AlertDialog>
                    <span className="text-xs text-gray-500">Geri almak programı tekrar onay bekler durumuna getirir</span>
                  </div>
                )}

                {/* Mutation Feedback */}
                {approveMutation.isError && (
                  <Alert variant="destructive" className="mt-4 rounded-lg">
                    <AlertCircle className="h-4 w-4" />
                    <AlertDescription>Onay işlemi başarısız oldu.</AlertDescription>
                  </Alert>
                )}
                {approveMutation.isSuccess && (
                  <Alert variant="success" className="mt-4 rounded-lg">
                    <CheckCircle2 className="h-4 w-4" />
                    <AlertDescription>Program başarıyla onaylandı.</AlertDescription>
                  </Alert>
                )}
                {revokeMutation.isError && (
                  <Alert variant="destructive" className="mt-4 rounded-lg">
                    <AlertCircle className="h-4 w-4" />
                    <AlertDescription>Onay geri alma işlemi başarısız oldu.</AlertDescription>
                  </Alert>
                )}
              </div>
            </Card>
          </>
        ) : null}

        {/* ── Schedule View Dialog ── */}
        <Dialog open={viewOpen} onOpenChange={setViewOpen}>
          <ResizableDialogContent
            defaultWidth={Math.min(1800, window.innerWidth - 40)}
            defaultHeight={Math.min(1000, window.innerHeight - 40)}
            minWidth={700}
            minHeight={500}
            showFullscreenToggle
            className="flex flex-col !p-0 !gap-0 !overflow-hidden"
          >
            {/* Row 1: Title + Progress */}
            <div className="shrink-0 border-b bg-gray-50/80 px-5 pt-3.5 pb-2">
              <div className="flex items-center gap-3 pr-16">
                <h2 className="text-sm font-semibold text-gray-800 whitespace-nowrap">
                  {status?.facultyName ?? 'Fakülte'}
                </h2>
                <Badge variant="outline" className="text-[10px] gap-1 shrink-0">
                  <Calendar className="h-2.5 w-2.5" />
                  {activeTerm?.name}
                </Badge>
                <div className="w-px h-5 bg-gray-200" />
                {ov && (
                  <div className="flex items-center gap-2 min-w-[140px]">
                    <Progress value={dialogProgressPct} className="h-1.5 flex-1" />
                    <span className="text-xs font-semibold text-gray-600 tabular-nums whitespace-nowrap">
                      {ov.scheduledSections}/{ov.totalSections}
                    </span>
                  </div>
                )}
                {ov && ov.conflictCount > 0 && (
                  <Badge variant="destructive" className="text-[10px]">
                    {ov.conflictCount} çakışma
                  </Badge>
                )}
              </div>
            </div>

            {/* Row 2: Filters + Toggle */}
            <div className="shrink-0 border-b bg-white px-5 py-2 flex items-center gap-3">
              {faculties && faculties.length > 1 && (
                <div className="flex items-center gap-2 shrink-0">
                  <span className="text-xs text-gray-500 font-medium">Fakülte</span>
                  <select
                    className="h-8 rounded-md border bg-white px-2.5 text-xs min-w-[180px]"
                    value={facultyId ?? ''}
                    onChange={e => {
                      setSelectedFacultyId(Number(e.target.value));
                      setViewFilterDept(undefined);
                    }}
                  >
                    {faculties.map(f => (
                      <option key={f.id} value={f.id}>{f.name}</option>
                    ))}
                  </select>
                </div>
              )}

              <div className="flex items-center gap-2 shrink-0">
                <span className="text-xs text-gray-500 font-medium">Bölüm</span>
                <select
                  className="h-8 rounded-md border bg-white px-2.5 text-xs min-w-[220px]"
                  value={viewFilterDept ?? ''}
                  onChange={e => setViewFilterDept(e.target.value ? Number(e.target.value) : undefined)}
                >
                  <option value="">Tüm Bölümler</option>
                  {ov?.departmentProgress.map(dp => (
                    <option key={dp.departmentId} value={dp.departmentId}>
                      {dp.departmentCode} — {dp.departmentName}
                    </option>
                  ))}
                </select>
              </div>

              <div className="flex gap-1 flex-wrap flex-1 min-w-0 justify-end">
                {ov?.departmentProgress.map(dp => (
                  <Tooltip key={dp.departmentId}>
                    <TooltipTrigger asChild>
                      <div>
                        <Badge
                          variant={dp.scheduledSections === dp.totalSections ? 'success' : 'outline'}
                          className="text-[10px] tabular-nums cursor-default"
                        >
                          {dp.departmentCode}: {dp.scheduledSections}/{dp.totalSections}
                        </Badge>
                      </div>
                    </TooltipTrigger>
                    <TooltipContent>
                      <p className="text-xs">{dp.departmentName}</p>
                    </TooltipContent>
                  </Tooltip>
                ))}
              </div>

              <div className="flex border rounded-lg overflow-hidden shrink-0 ml-1">
                <button
                  onClick={() => setViewTab('calendar')}
                  className={cn(
                    'flex items-center gap-1 px-2.5 py-1.5 text-xs font-medium transition-colors border-r',
                    viewTab === 'calendar' ? 'bg-gray-900 text-white' : 'bg-white text-gray-600 hover:bg-gray-50',
                  )}
                >
                  <CalendarDays className="h-3 w-3" />
                  Takvim
                </button>
                <button
                  onClick={() => setViewTab('list')}
                  className={cn(
                    'flex items-center gap-1 px-2.5 py-1.5 text-xs font-medium transition-colors',
                    viewTab === 'list' ? 'bg-gray-900 text-white' : 'bg-white text-gray-600 hover:bg-gray-50',
                  )}
                >
                  <LayoutList className="h-3 w-3" />
                  Liste
                </button>
              </div>
            </div>

            {/* Body */}
            <div className="flex-1 min-h-0 overflow-hidden">
              {viewTab === 'calendar' ? (
                <div className="h-full p-3">
                  {allSectionsQuery.isLoading || timeSlotsQuery.isLoading ? (
                    <Skeleton className="h-full rounded-xl" />
                  ) : (
                    <CalendarGrid
                      sections={allSectionsQuery.data?.items ?? []}
                      timeSlots={timeSlotsQuery.data ?? []}
                    />
                  )}
                </div>
              ) : (
                <div className="h-full overflow-auto p-3">
                  {facultyId && (
                    <SectionListTab
                      facultyId={facultyId}
                      termId={termId ?? null}
                      searchText=""
                      pageSize={25}
                      readOnly
                    />
                  )}
                </div>
              )}
            </div>

            {/* Conflict bar */}
            {facultyId && (
              <ConflictBar facultyId={facultyId} termId={termId ?? null} />
            )}
          </ResizableDialogContent>
        </Dialog>
      </div>
    </TooltipProvider>
  );
}
