import { useState } from 'react';
import { Dialog, DialogTitle } from '@/components/ui/dialog';
import ResizableDialogContent from '@/components/ui/resizable-dialog';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Progress } from '@/components/ui/progress';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { CheckCircle, XCircle, AlertTriangle, BookOpen } from 'lucide-react';
import { useAdvisorStudents, usePendingApprovals, useApproveStudent, useRejectStudent } from '../../hooks/useAdvisor';
import { getErrorMessage } from '../../lib/errorMessage';
import AdvisorCourseEditor from './AdvisorCourseEditor';
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
  const [showReject, setShowReject] = useState(false);
  const [note, setNote] = useState('');
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
    onOpenChange(false);
  }

  async function doApprove() {
    if (studentId == null) return;
    setActionError('');
    try {
      await approve.mutateAsync({ studentId });
      close();
    } catch (err) {
      setActionError(getErrorMessage(err, 'Onay başarısız.'));
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
  const curriculumCoursePercent = pending && pending.curriculumTotalCourses > 0
    ? Math.round((pending.curriculumCompletedCourses / pending.curriculumTotalCourses) * 100)
    : 0;
  const curriculumEctsPercent = pending && pending.curriculumTotalEcts > 0
    ? Math.round((pending.curriculumCompletedEcts / pending.curriculumTotalEcts) * 100)
    : 0;
  const unselectedFailed = pending?.failedCourses.filter(f => !f.isSelectedInPlan) ?? [];
  const hasConflicts = pending?.registrations.some(r => r.hasScheduleConflict) ?? false;

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
          <div className="mt-1 flex flex-wrap items-center gap-4 text-sm text-muted-foreground">
            {cgpa != null && <span>CGPA: <strong className="text-foreground">{cgpa.toFixed(2)}</strong></span>}
            {row?.completedSemesters != null && <span>Dönem: <strong className="text-foreground">{row.completedSemesters}</strong></span>}
            {program && <span className="truncate">{program}</span>}
          </div>
        </div>

        {/* Gövde — ders kaydı odaklı */}
        <div className="min-h-0 flex-1 space-y-5 overflow-y-auto p-5">
          {pending && (
            <div className="space-y-4 rounded-xl border border-border bg-card p-4">
              <div className="flex items-center gap-4">
                <BookOpen className="h-5 w-5 shrink-0 text-muted-foreground" />
                <div className="flex-1">
                  <div className="mb-1 flex justify-between text-sm">
                    <span>Toplam AKTS</span>
                    <span className={`font-semibold ${ectsLimit != null && totalEcts > ectsLimit ? 'text-destructive' : 'text-foreground'}`}>
                      {totalEcts}{ectsLimit != null ? ` / ${ectsLimit}` : ''} AKTS
                    </span>
                  </div>
                  {ectsLimit != null && <Progress value={Math.min((totalEcts / ectsLimit) * 100, 100)} className="h-2" />}
                </div>
              </div>
              <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
                <div className="space-y-1">
                  <p className="text-xs font-medium text-muted-foreground">Müfredat Ders İlerlemesi</p>
                  <div className="mb-1 flex justify-between text-sm">
                    <span>{pending.curriculumCompletedCourses} / {pending.curriculumTotalCourses} ders</span>
                    <span className="font-semibold">%{curriculumCoursePercent}</span>
                  </div>
                  <Progress value={curriculumCoursePercent} className="h-2" />
                </div>
                <div className="space-y-1">
                  <p className="text-xs font-medium text-muted-foreground">Müfredat AKTS İlerlemesi</p>
                  <div className="mb-1 flex justify-between text-sm">
                    <span>{pending.curriculumCompletedEcts} / {pending.curriculumTotalEcts} AKTS</span>
                    <span className="font-semibold">%{curriculumEctsPercent}</span>
                  </div>
                  <Progress value={curriculumEctsPercent} className="h-2" />
                </div>
              </div>
            </div>
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

          {studentId != null && <AdvisorCourseEditor studentId={studentId} />}
        </div>

        {/* Aksiyon çubuğu (footer) */}
        {planStatus === 'Submitted' && (
          <div className="shrink-0 space-y-2 border-t bg-background px-5 py-3">
            {actionError && (
              <div className="rounded-lg border border-destructive/30 bg-destructive/10 px-3 py-2 text-sm text-destructive">
                {actionError}
              </div>
            )}
            {!showReject ? (
              <div className="flex items-center gap-2">
                <Button onClick={doApprove} disabled={busy} className="gap-1.5">
                  <CheckCircle className="h-4 w-4" /> Onayla
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
            )}
          </div>
        )}
      </ResizableDialogContent>
    </Dialog>
  );
}
