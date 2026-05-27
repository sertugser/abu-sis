import { useCallback, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/tooltip';
import { AlertCircle, Calendar, CheckCircle2, Download, FileText, LayoutList, Maximize2, Search, ShieldCheck, Upload, Wand2, XCircle } from 'lucide-react';
import { Input } from '@/components/ui/input';
import { STATUS_CONFIG } from '../components/faculty-planning/sectionUtils';
import { cn } from '@/lib/utils';
import { useAcademicOperationsContext } from '../hooks/useAcademicOperations';
import { useActiveTerm } from '../hooks/useActiveTerm';
import { useFacultyLookup } from '../hooks/useAdminLookup';
import { useExportExcel, useExportPdf } from '../hooks/useExcelSchedule';
import { useScheduleApprovalStatus, useApproveSchedule, useRevokeScheduleApproval } from '../hooks/useScheduleApproval';
import { usePermissions } from '../hooks/usePermissions';
import { ConflictBar } from '../components/faculty-planning/ConflictBar';
import { SectionListTab } from '../components/faculty-planning/SectionListTab';
import { AutoScheduleTab } from '../components/faculty-planning/AutoScheduleTab';
import { ExcelImportModal } from '../components/faculty-planning/ExcelImportModal';
import { Badge } from '@/components/ui/badge';
import { Textarea } from '@/components/ui/textarea';
import {
  AlertDialog, AlertDialogAction, AlertDialogCancel, AlertDialogContent,
  AlertDialogDescription, AlertDialogFooter, AlertDialogHeader, AlertDialogTitle, AlertDialogTrigger,
} from '@/components/ui/alert-dialog';

type TabKey = 'list' | 'auto-schedule';
const PAGE_SIZES = [10, 25, 50, 100];

export default function FacultyPlanningPage() {
  const navigate = useNavigate();
  const [activeTab, setActiveTab] = useState<TabKey>('auto-schedule');
  const [importOpen, setImportOpen] = useState(false);
  const contextQuery = useAcademicOperationsContext();
  const { data: activeTerm } = useActiveTerm();
  const { data: faculties, isLoading: facultiesLoading } = useFacultyLookup();

  const [selectedFacultyId, setSelectedFacultyId] = useState<number | null>(null);
  const facultyId = selectedFacultyId ?? faculties?.[0]?.id ?? null;
  const termId = activeTerm?.id ?? null;

  const exportExcel = useExportExcel();
  const exportPdf = useExportPdf();
  const { data: approvalStatus } = useScheduleApprovalStatus(facultyId, termId ?? undefined);
  const approveMutation = useApproveSchedule(facultyId ?? 0, termId ?? undefined);
  const revokeMutation = useRevokeScheduleApproval(facultyId ?? 0, termId ?? undefined);
  const { hasPermission } = usePermissions();
  const [approveNote, setApproveNote] = useState('');

  const [triggerRun, setTriggerRun] = useState(0);
  // Hangi triggerRun değeri çocuk komponentte işlendi.
  // Tab değişimlerinde AutoScheduleTab unmount + remount oluyor; "consumed" bilgisi
  // çocukta tutulursa kayboluyor ve remount'ta useEffect aynı trigger'ı yeniden tetikleyip
  // "iptal ettim ama tab dönünce kendiliğinden başladı" bug'ına yol açıyor. Parent state'i
  // remount'tan sağ çıkar → trigger bir kez işlendi mi bilgisi kalıcı.
  const [consumedTriggerRun, setConsumedTriggerRun] = useState(0);
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
  const isApproved = approvalStatus?.isApproved ?? false;
  const canApprove = hasPermission('menu.program-onayi.approve');
  // Yayın kilidi: dönem Active DEĞİLSE veya program onaylı (yayında) ise düzenleme kapalı.
  const isTermEditable = termStatus === 'Active' && !isApproved;

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

        {/* Düzenleyiciyi Aç — ayrı tam-ekran sayfaya geçiş */}
        <TooltipProvider delayDuration={100}>
          <Tooltip>
            <TooltipTrigger asChild>
              <Button
                variant="outline"
                size="sm"
                className="h-8 gap-1.5"
                onClick={() => {
                  if (termId == null) return;
                  navigate(`/ders-programi-olusturma/duzenle?facultyId=${facultyId}&termId=${termId}`);
                }}
                disabled={termId == null || !isTermEditable}
              >
                <Maximize2 className="h-3.5 w-3.5" />
                Düzenleyici
              </Button>
            </TooltipTrigger>
            <TooltipContent side="bottom" className="text-xs">
              Tam ekran sürükle-bırak düzenleyici
            </TooltipContent>
          </Tooltip>
        </TooltipProvider>

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

      {/* Term guard — yalnızca dönem gerçekten Active değilken (iptal/pasif) */}
      {termStatus && termStatus !== 'Active' && (
        <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-2 text-center mb-3">
          <p className="text-xs text-red-700 font-medium">
            Dönem durumu "{termStatus}" — atama işlemleri yalnızca dönem "Active" (tarih aralığı içinde ve iptal edilmemiş) iken yapılabilir.
          </p>
        </div>
      )}

      {/* ── Program Onay (yayın) barı ── */}
      {(canApprove || isApproved) && (
        <div className={cn(
          'flex flex-wrap items-center gap-3 rounded-lg border px-4 py-2 mb-3',
          isApproved ? 'bg-emerald-50 border-emerald-200' : 'bg-amber-50 border-amber-200',
        )}>
          {isApproved ? (
            <>
              <ShieldCheck className="h-4 w-4 text-emerald-600 shrink-0" />
              <span className="text-xs font-medium text-emerald-800">
                Program onaylandı (yayında) — öğrencilere açık
                {approvalStatus?.approvedByName ? ` · ${approvalStatus.approvedByName}` : ''}
                {approvalStatus?.approvedAt ? ` · ${new Date(approvalStatus.approvedAt).toLocaleString('tr-TR')}` : ''}
              </span>
              <span className="text-xs text-emerald-700/70">Değiştirmek için önce onayı geri alın.</span>
              {canApprove && (
                <div className="ml-auto">
                  <AlertDialog>
                    <AlertDialogTrigger asChild>
                      <Button variant="outline" size="sm" className="h-8 gap-1.5 text-red-600 border-red-200 hover:bg-red-50 hover:text-red-700" disabled={revokeMutation.isPending}>
                        <XCircle className="h-3.5 w-3.5" />
                        {revokeMutation.isPending ? 'Geri alınıyor...' : 'Onayı Geri Al'}
                      </Button>
                    </AlertDialogTrigger>
                    <AlertDialogContent>
                      <AlertDialogHeader>
                        <AlertDialogTitle>Onayı Geri Al</AlertDialogTitle>
                        <AlertDialogDescription>
                          Bu fakültenin programı öğrencilerden geri çekilecek ve yeniden düzenlemeye açılacak.
                          Yeniden onaylanana kadar öğrenciler bu fakültenin derslerini göremez. Emin misiniz?
                        </AlertDialogDescription>
                      </AlertDialogHeader>
                      <AlertDialogFooter>
                        <AlertDialogCancel>Vazgeç</AlertDialogCancel>
                        <AlertDialogAction onClick={() => revokeMutation.mutate()} className="bg-red-600 hover:bg-red-700">
                          Onayı Geri Al
                        </AlertDialogAction>
                      </AlertDialogFooter>
                    </AlertDialogContent>
                  </AlertDialog>
                </div>
              )}
            </>
          ) : (
            <>
              <AlertCircle className="h-4 w-4 text-amber-600 shrink-0" />
              <span className="text-xs font-medium text-amber-800">
                Program henüz yayınlanmadı — öğrenciler bu fakültenin derslerini göremez.
              </span>
              {canApprove && (
                <div className="ml-auto">
                  <AlertDialog>
                    <AlertDialogTrigger asChild>
                      <Button size="sm" className="h-8 gap-1.5 bg-emerald-600 hover:bg-emerald-700 text-white" disabled={approveMutation.isPending || !isTermEditable}>
                        <CheckCircle2 className="h-3.5 w-3.5" />
                        {approveMutation.isPending ? 'Onaylanıyor...' : 'Programı Onayla'}
                      </Button>
                    </AlertDialogTrigger>
                    <AlertDialogContent>
                      <AlertDialogHeader>
                        <AlertDialogTitle>Programı Onayla ve Yayınla</AlertDialogTitle>
                        <AlertDialogDescription>
                          Bu fakültenin programı öğrencilere açılacak (yalnızca tam atanmış/Active şubeler görünür).
                          Onayladıktan sonra program kilitlenir; değiştirmek için onayı geri almanız gerekir.
                        </AlertDialogDescription>
                      </AlertDialogHeader>
                      <Textarea
                        placeholder="Onay notu (opsiyonel)..."
                        value={approveNote}
                        onChange={e => setApproveNote(e.target.value)}
                        rows={2}
                        className="resize-none"
                      />
                      <AlertDialogFooter>
                        <AlertDialogCancel>Vazgeç</AlertDialogCancel>
                        <AlertDialogAction
                          onClick={() => approveMutation.mutate(approveNote || undefined, { onSuccess: () => setApproveNote('') })}
                          className="bg-emerald-600 hover:bg-emerald-700"
                        >
                          Onayla ve Yayınla
                        </AlertDialogAction>
                      </AlertDialogFooter>
                    </AlertDialogContent>
                  </AlertDialog>
                </div>
              )}
            </>
          )}
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
              consumedTriggerRun={consumedTriggerRun}
              onTriggerConsumed={setConsumedTriggerRun}
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
