import { useState } from 'react';
import { Dialog, DialogTitle } from '@/components/ui/dialog';
import ResizableDialogContent from '@/components/ui/resizable-dialog';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { CheckCircle, XCircle, AlertTriangle, BookOpen, ChevronDown, GraduationCap, ScrollText, TrendingUp, ListChecks, RotateCcw } from 'lucide-react';
import { DropdownMenu, DropdownMenuTrigger, DropdownMenuContent, DropdownMenuItem, DropdownMenuSeparator } from '@/components/ui/dropdown-menu';
import { useAdvisorStudents, usePendingApprovals, useApproveStudent, useRejectStudent } from '../../hooks/useAdvisor';
import { getErrorMessage } from '../../lib/errorMessage';
import { toApiError } from '../../lib/apiError';
import { formatDate, formatDateTime } from '../../lib/utils';
import type { ValidationError } from '../../types/registration';
import AdvisorCourseEditor from './AdvisorCourseEditor';
import AdvisorStudentInfoDialogs, { type AdvisorInfoView } from './AdvisorStudentInfoDialogs';
import { PLAN_STATUS_LABELS, PLAN_STATUS_VARIANTS } from './planStatus';

interface Props {
  studentId: number | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

/**
 * Danışmanın bir öğrencinin DERS KAYDINA odaklı, boyutlandırılabilir/taşınabilir modali
 * (sistem geneli ResizableDialogContent). Plan özeti + kayıtlı dersler (çıkar) + aday
 * dersler (ekle) + Onayla/Reddet.
 */
export default function AdvisorStudentDetailModal({ studentId, open, onOpenChange }: Props) {
  const { data: advisees = [] } = useAdvisorStudents();
  const { data: pendingData } = usePendingApprovals();

  const [actionError, setActionError] = useState('');
  const [checkErrors, setCheckErrors] = useState<ValidationError[]>([]);
  const [checkWarnings, setCheckWarnings] = useState<ValidationError[]>([]);
  const [showReject, setShowReject] = useState(false);
  const [note, setNote] = useState('');
  const [infoView, setInfoView] = useState<AdvisorInfoView>(null);
  const approve = useApproveStudent();
  const reject = useRejectStudent();
  const busy = approve.isPending || reject.isPending;

  const row = advisees.find(a => a.studentId === studentId);
  const pending = pendingData?.items.find(p => p.studentId === studentId) ?? null;

  const planStatus = row?.planStatus ?? (pending ? 'Submitted' : 'None');
  const name = pending?.studentName ?? row?.studentName ?? 'Öğrenci';
  const number = pending?.studentNumber ?? row?.studentNumber ?? '';
  const cgpa = pending?.cgpa ?? row?.cgpa;
  const program = row?.programName;

  function close() {
    setShowReject(false);
    setNote('');
    setActionError('');
    setCheckErrors([]);
    setCheckWarnings([]);
    onOpenChange(false);
  }

  // "Kontrol Et ve Onayla": backend önce akademik kontrol yapar; hata varsa onaylamaz ve
  // sorunları döner (HTTP 400 + validationResult). Temizse onaylar.
  async function doApprove() {
    if (studentId == null) return;
    setActionError('');
    setCheckErrors([]);
    setCheckWarnings([]);
    try {
      const res = await approve.mutateAsync({ studentId });
      // Onay başarılı — varsa uyarıları kısa süre göster, sonra kapat.
      const warnings = (res?.data?.validationResult?.warnings ?? []) as ValidationError[];
      if (warnings.length > 0) {
        setCheckWarnings(warnings);
        setTimeout(close, 1800);
      } else {
        close();
      }
    } catch (err) {
      const apiErr = toApiError(err, 'Onay başarısız.');
      if (apiErr.validationErrors.length > 0 || apiErr.validationWarnings.length > 0) {
        setCheckErrors(apiErr.validationErrors);
        setCheckWarnings(apiErr.validationWarnings);
      } else {
        setActionError(apiErr.message);
      }
    }
  }

  async function doReject() {
    if (studentId == null) return;
    setActionError('');
    try {
      await reject.mutateAsync({ studentId, note: note || undefined });
      close();
    } catch (err) {
      setActionError(getErrorMessage(err, 'Red başarısız.'));
    }
  }

  const totalEcts = pending?.totalEcts ?? 0;
  const ectsLimit = pending?.ectsLimit ?? null;
  const unselectedFailed = pending?.failedCourses.filter(f => !f.isSelectedInPlan) ?? [];
  const hasConflicts = pending?.registrations.some(r => r.hasScheduleConflict) ?? false;

  // Onay durumu başlığı (Proliz paritesi)
  const studentFinalized = planStatus === 'Submitted' || planStatus === 'Approved' || planStatus === 'Rejected';
  const advisorDecisionLabel =
    planStatus === 'Approved' ? 'Onaylandı'
      : planStatus === 'Rejected' ? 'Reddedildi'
        : planStatus === 'Submitted' ? 'Bekliyor'
          : '—';
  const advisorDecisionClass =
    planStatus === 'Approved' ? 'text-emerald-600 dark:text-emerald-400'
      : planStatus === 'Rejected' ? 'text-destructive'
        : planStatus === 'Submitted' ? 'text-amber-600 dark:text-amber-400'
          : 'text-muted-foreground';

  // Onay/Red, onaylanacak (PendingAdvisorApproval) kayıt varken gösterilir — danışman
  // onaylı plana ekleme yapsa bile (Proliz modeli) buton çıkar.
  const canDecide = pending != null;

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <ResizableDialogContent
        defaultWidth={920}
        defaultHeight={680}
        minWidth={560}
        minHeight={460}
        className="flex flex-col p-0"
      >
        <DialogTitle className="sr-only">{name} — Ders Kaydı</DialogTitle>

        {/* Başlık (üst-sağ kapat/tam ekran butonlarıyla çakışmasın diye pr-24) */}
        <div className="shrink-0 border-b px-5 py-3 pr-24">
          <div className="flex min-w-0 flex-wrap items-center gap-2">
            <span className="truncate font-heading text-lg font-bold text-foreground">{name}</span>
            {number && <span className="shrink-0 font-mono text-sm text-muted-foreground">({number})</span>}
            <Badge variant={PLAN_STATUS_VARIANTS[planStatus] ?? 'secondary'}>{PLAN_STATUS_LABELS[planStatus] ?? planStatus}</Badge>
            {row?.isAtRisk && <Badge variant="destructive">Riskli</Badge>}
          </div>
          {/* Öğrenci bilgi bloğu (Proliz "Öğrenci Bilgileri") */}
          <div className="mt-2 grid grid-cols-1 gap-x-8 gap-y-1 text-sm sm:grid-cols-2">
            <InfoLine label="Öğrenci No" value={number || '—'} />
            <InfoLine label="Fakülte - Program / Sınıf" value={`${row?.facultyName ? row.facultyName + ' - ' : ''}${program ?? '—'}${row?.classYear ? ' / ' + row.classYear : ''}`} />
            <InfoLine label="Kayıt Tarihi" value={row?.enrollmentDate ? formatDate(row.enrollmentDate) : '—'} />
            <InfoLine label="Bağlı Olunan Müfredat" value={row?.curriculumVersion || '—'} />
            <InfoLine label="Öğrenim Süre Durum" value={row?.studyDurationStatus || '—'} />
            <InfoLine label="Genel Ortalama" value={cgpa != null ? cgpa.toFixed(2) : '—'} />
          </div>

          {/* Ek İşlemler / Diğer İşlemler menüleri (Proliz) */}
          <div className="mt-2.5 flex flex-wrap items-center gap-2 text-xs">
            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <button type="button" className="inline-flex items-center gap-1 rounded-lg border border-border bg-background px-3 py-1.5 text-xs font-medium text-foreground transition-colors hover:bg-muted/40">
                  <ListChecks className="h-3.5 w-3.5" /> Ek İşlemler <ChevronDown className="h-3.5 w-3.5" />
                </button>
              </DropdownMenuTrigger>
              <DropdownMenuContent align="start" className="w-56">
                <DropdownMenuItem disabled><BookOpen className="mr-2 h-4 w-4" /> Ders Programı <span className="ml-auto text-[10px] text-muted-foreground">yakında</span></DropdownMenuItem>
                <DropdownMenuItem disabled><BookOpen className="mr-2 h-4 w-4" /> Bölüm Ders Programı <span className="ml-auto text-[10px] text-muted-foreground">yakında</span></DropdownMenuItem>
                <DropdownMenuItem onClick={() => setInfoView('curriculum')}><GraduationCap className="mr-2 h-4 w-4" /> Müfredat Durumu</DropdownMenuItem>
                <DropdownMenuItem onClick={() => setInfoView('history')}><BookOpen className="mr-2 h-4 w-4" /> Ders Kayıt Geçmişi</DropdownMenuItem>
                <DropdownMenuSeparator />
                <DropdownMenuItem disabled><ScrollText className="mr-2 h-4 w-4" /> Ders Kayıt Mesajları <span className="ml-auto text-[10px] text-muted-foreground">yakında</span></DropdownMenuItem>
                <DropdownMenuItem onClick={() => setInfoView('curriculum')}><RotateCcw className="mr-2 h-4 w-4" /> Tekrar Durum Göster</DropdownMenuItem>
              </DropdownMenuContent>
            </DropdownMenu>

            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <button type="button" className="inline-flex items-center gap-1 rounded-lg border border-border bg-background px-3 py-1.5 text-xs font-medium text-foreground transition-colors hover:bg-muted/40">
                  <ListChecks className="h-3.5 w-3.5" /> Diğer İşlemler <ChevronDown className="h-3.5 w-3.5" />
                </button>
              </DropdownMenuTrigger>
              <DropdownMenuContent align="start" className="w-56">
                <DropdownMenuItem onClick={() => setInfoView('history')}><BookOpen className="mr-2 h-4 w-4" /> Aldığı Dersler</DropdownMenuItem>
                <DropdownMenuItem onClick={() => setInfoView('transcript')}><ScrollText className="mr-2 h-4 w-4" /> Transkript</DropdownMenuItem>
                <DropdownMenuItem disabled><ScrollText className="mr-2 h-4 w-4" /> Mezuniyet Transkripti <span className="ml-auto text-[10px] text-muted-foreground">yakında</span></DropdownMenuItem>
                <DropdownMenuItem onClick={() => setInfoView('gpa')}><TrendingUp className="mr-2 h-4 w-4" /> Dönem Ortalamaları</DropdownMenuItem>
                <DropdownMenuItem disabled><ScrollText className="mr-2 h-4 w-4" /> Transkript Senaryosu <span className="ml-auto text-[10px] text-muted-foreground">yakında</span></DropdownMenuItem>
                <DropdownMenuItem disabled><GraduationCap className="mr-2 h-4 w-4" /> Akademik Faaliyetler <span className="ml-auto text-[10px] text-muted-foreground">yakında</span></DropdownMenuItem>
              </DropdownMenuContent>
            </DropdownMenu>
          </div>
        </div>

        {/* Gövde — Proliz sırası: aday grid (üstte) → kontrol (orta) → seçilen dersler (altta) */}
        <div className="min-h-0 flex-1 overflow-y-auto p-5">
          {studentId != null && (
            <AdvisorCourseEditor
              studentId={studentId}
              studentClass={row?.completedSemesters != null ? Math.floor(row.completedSemesters / 2) + 1 : undefined}
              controlSlot={
                <div className="space-y-2 rounded-lg border border-border bg-muted/20 p-3">
                  {ectsLimit != null && totalEcts > ectsLimit && (
                    <p className="text-xs font-medium text-destructive">Toplam {totalEcts} / {ectsLimit} AKTS — AKTS limiti aşıldı.</p>
                  )}
                  {hasConflicts && (
                    <Alert variant="destructive">
                      <AlertTriangle className="h-4 w-4" />
                      <AlertTitle>Zaman Çakışması</AlertTitle>
                      <AlertDescription>Seçilen dersler arasında zaman çakışması var.</AlertDescription>
                    </Alert>
                  )}
                  {unselectedFailed.length > 0 && (
                    <Alert>
                      <AlertTriangle className="h-4 w-4" />
                      <AlertTitle>Seçilmemiş Alttan Dersler</AlertTitle>
                      <AlertDescription>{unselectedFailed.map(f => f.courseCode).join(', ')} planda seçili değil.</AlertDescription>
                    </Alert>
                  )}
                  {actionError && (
                    <div className="rounded-lg border border-destructive/30 bg-destructive/10 px-3 py-2 text-sm text-destructive">
                      {actionError}
                    </div>
                  )}
                  {checkErrors.length > 0 && (
                    <div className="rounded-lg border border-destructive/30 bg-destructive/10 px-3 py-2">
                      <p className="mb-1 flex items-center gap-1.5 text-sm font-semibold text-destructive">
                        <XCircle className="h-4 w-4" /> Kontrol başarısız — onaylanamaz ({checkErrors.length})
                      </p>
                      <ul className="list-disc space-y-0.5 pl-6 text-xs text-destructive">
                        {checkErrors.map((e, i) => <li key={`${e.rule}-${i}`}>{e.message}</li>)}
                      </ul>
                    </div>
                  )}
                  {checkWarnings.length > 0 && (
                    <div className="rounded-lg border border-amber-500/40 bg-amber-500/10 px-3 py-2">
                      <p className="mb-1 flex items-center gap-1.5 text-sm font-semibold text-amber-700 dark:text-amber-400">
                        <AlertTriangle className="h-4 w-4" /> Uyarılar ({checkWarnings.length})
                      </p>
                      <ul className="list-disc space-y-0.5 pl-6 text-xs text-amber-700 dark:text-amber-400">
                        {checkWarnings.map((w, i) => <li key={`${w.rule}-${i}`}>{w.message}</li>)}
                      </ul>
                    </div>
                  )}

                  {/* Onay durumu satırı (Proliz: kontrol ile seçilen dersler arası) */}
                  <div className="flex flex-wrap items-center justify-between gap-2 border-t border-border pt-2 text-xs">
                    <span>
                      <span className="text-muted-foreground">Öğrenci Onay Durumu: </span>
                      <strong className={studentFinalized ? 'text-emerald-600 dark:text-emerald-400' : 'text-destructive'}>
                        {studentFinalized ? 'Kesinleştirildi' : 'Kesinleştirilmedi'}
                      </strong>
                      {studentFinalized && row?.planSubmittedAt && (
                        <span className="ml-1 font-mono text-[11px] text-muted-foreground">{formatDateTime(row.planSubmittedAt)}</span>
                      )}
                      {planStatus !== 'Approved' && (
                        <span className="ml-2 text-destructive">⚠ Onaylanıncaya kadar hiçbir ders kontenjanına dahil edilmez.</span>
                      )}
                    </span>
                    <span>
                      <span className="text-muted-foreground">Danışman Onay Durumu: </span>
                      <strong className={advisorDecisionClass}>{advisorDecisionLabel}</strong>
                      {row?.planDecidedAt && (planStatus === 'Approved' || planStatus === 'Rejected') && (
                        <span className="ml-1 font-mono text-[11px] text-muted-foreground">{formatDateTime(row.planDecidedAt)}</span>
                      )}
                    </span>
                  </div>

                  {/* Alttan Dersleri Göster (sol) + Kontrol Et ve Onayla / Reddet (sağ) */}
                  {canDecide && (
                    !showReject ? (
                      <div className="flex items-center gap-2">
                        <Button onClick={doApprove} disabled={busy} className="gap-1.5">
                          <CheckCircle className="h-4 w-4" /> {busy ? 'Kontrol ediliyor...' : 'Kontrol Et ve Onayla'}
                        </Button>
                        <Button
                          variant="outline"
                          onClick={() => setShowReject(true)}
                          disabled={busy}
                          className="gap-1.5 border-destructive/30 text-destructive hover:bg-destructive/10"
                        >
                          <XCircle className="h-4 w-4" /> Reddet
                        </Button>
                      </div>
                    ) : (
                      <div className="flex items-center gap-2">
                        <input
                          value={note}
                          onChange={e => setNote(e.target.value)}
                          placeholder="Red nedeni (opsiyonel)..."
                          className="flex-1 rounded-lg border border-border bg-background px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-ring"
                        />
                        <Button variant="destructive" onClick={doReject} disabled={busy}>Reddet</Button>
                        <Button variant="outline" onClick={() => { setShowReject(false); setNote(''); }}>İptal</Button>
                      </div>
                    )
                  )}
                </div>
              }
            />
          )}
        </div>

        {/* Danışan bilgi görünümleri (Ek/Diğer İşlemler) */}
        <AdvisorStudentInfoDialogs studentId={studentId} view={infoView} onClose={() => setInfoView(null)} />
      </ResizableDialogContent>
    </Dialog>
  );
}

function InfoLine({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex gap-2">
      <span className="shrink-0 text-muted-foreground">{label}:</span>
      <span className="truncate font-medium text-foreground">{value}</span>
    </div>
  );
}
