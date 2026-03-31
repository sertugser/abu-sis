import { useState } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Progress } from '@/components/ui/progress';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { CheckCircle, XCircle, AlertTriangle, BookOpen, Info, ChevronDown, ChevronUp } from 'lucide-react';
import { usePendingApprovals, useApproveStudent, useRejectStudent } from '../hooks/useAdvisor';
import type { PendingApproval } from '../api/advisor.api';

// ── Helpers ──────────────────────────────────────────────────────────────────

function fmtDate(iso: string) {
  if (!iso) return '—';
  return new Date(iso).toLocaleDateString('tr-TR', {
    day: 'numeric',
    month: 'short',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
}

const PLAN_STATUS_LABELS: Record<string, string> = {
  Submitted: 'Gonderildi',
  Draft: 'Taslak',
  Approved: 'Onaylandi',
  Rejected: 'Reddedildi',
};

const PLAN_STATUS_VARIANTS: Record<string, 'default' | 'secondary' | 'destructive' | 'outline' | 'success' | 'warning'> = {
  Submitted: 'warning',
  Draft: 'secondary',
  Approved: 'success',
  Rejected: 'destructive',
};

// ── Student Card ─────────────────────────────────────────────────────────────

function StudentApprovalCard({ student }: { student: PendingApproval }) {
  const [rejectNote, setRejectNote] = useState('');
  const [showRejectInput, setShowRejectInput] = useState(false);
  const [expanded, setExpanded] = useState(true);
  const [actionError, setActionError] = useState('');

  const approveMutation = useApproveStudent();
  const rejectMutation = useRejectStudent();

  const curriculumCoursePercent = student.curriculumTotalCourses > 0
    ? Math.round((student.curriculumCompletedCourses / student.curriculumTotalCourses) * 100)
    : 0;
  const curriculumEctsPercent = student.curriculumTotalEcts > 0
    ? Math.round((student.curriculumCompletedEcts / student.curriculumTotalEcts) * 100)
    : 0;

  const hasConflicts = student.registrations.some(r => r.hasScheduleConflict);
  const unselectedFailedCourses = student.failedCourses.filter(f => !f.isSelectedInPlan);

  async function handleApprove() {
    setActionError('');
    try {
      await approveMutation.mutateAsync({ studentId: student.studentId });
    } catch (err: unknown) {
      const axiosErr = err as { response?: { data?: { error?: string; message?: string } } };
      setActionError(axiosErr?.response?.data?.error ?? axiosErr?.response?.data?.message ?? 'Onay basarisiz.');
    }
  }

  async function handleReject() {
    setActionError('');
    try {
      await rejectMutation.mutateAsync({ studentId: student.studentId, note: rejectNote || undefined });
      setShowRejectInput(false);
      setRejectNote('');
    } catch (err: unknown) {
      const axiosErr = err as { response?: { data?: { error?: string; message?: string } } };
      setActionError(axiosErr?.response?.data?.error ?? axiosErr?.response?.data?.message ?? 'Red basarisiz.');
    }
  }

  const isPending = approveMutation.isPending || rejectMutation.isPending;

  return (
    <Card className="border-border">
      {/* Header */}
      <CardHeader className="pb-3">
        <div className="flex items-start justify-between">
          <div className="space-y-1">
            <CardTitle className="text-lg font-semibold flex items-center gap-2">
              {student.studentName}
              <span className="text-sm font-normal text-muted-foreground">({student.studentNumber})</span>
            </CardTitle>
            <div className="flex flex-wrap items-center gap-3 text-sm text-muted-foreground">
              <span>CGPA: <strong className="text-foreground">{student.cgpa.toFixed(2)}</strong></span>
              <span>SGPA: <strong className="text-foreground">{student.sgpa.toFixed(2)}</strong></span>
              <span>Donem: <strong className="text-foreground">{student.completedSemesters}</strong></span>
              {student.isScholarshipStudent && (
                <Badge variant="info" className="text-xs">Burslu</Badge>
              )}
            </div>
          </div>
          <div className="flex items-center gap-2">
            <Badge variant={PLAN_STATUS_VARIANTS[student.planStatus] ?? 'secondary'}>
              {PLAN_STATUS_LABELS[student.planStatus] ?? student.planStatus}
            </Badge>
            <Button variant="ghost" size="sm" className="h-7 w-7 p-0" onClick={() => setExpanded(e => !e)}>
              {expanded ? <ChevronUp className="h-4 w-4" /> : <ChevronDown className="h-4 w-4" />}
            </Button>
          </div>
        </div>
      </CardHeader>

      {expanded && (
        <CardContent className="space-y-4 pt-0">
          {/* ECTS Summary */}
          <div className="flex items-center gap-4 p-3 rounded-lg bg-muted/30">
            <BookOpen className="h-5 w-5 text-muted-foreground shrink-0" />
            <div className="flex-1">
              <div className="flex justify-between text-sm mb-1">
                <span>Toplam AKTS</span>
                <span className={`font-semibold ${student.totalEcts > student.ectsLimit ? 'text-destructive' : 'text-foreground'}`}>
                  {student.totalEcts} / {student.ectsLimit} AKTS
                </span>
              </div>
              <Progress value={Math.min((student.totalEcts / student.ectsLimit) * 100, 100)} className="h-2" />
            </div>
          </div>

          {/* Curriculum Progress */}
          <div className="grid grid-cols-2 gap-4">
            <div className="space-y-1">
              <p className="text-xs font-medium text-muted-foreground">Mufredat Ders Ilerlemesi</p>
              <div className="flex justify-between text-sm mb-1">
                <span>{student.curriculumCompletedCourses} / {student.curriculumTotalCourses} ders</span>
                <span className="font-semibold">%{curriculumCoursePercent}</span>
              </div>
              <Progress value={curriculumCoursePercent} className="h-2" />
            </div>
            <div className="space-y-1">
              <p className="text-xs font-medium text-muted-foreground">Mufredat AKTS Ilerlemesi</p>
              <div className="flex justify-between text-sm mb-1">
                <span>{student.curriculumCompletedEcts} / {student.curriculumTotalEcts} AKTS</span>
                <span className="font-semibold">%{curriculumEctsPercent}</span>
              </div>
              <Progress value={curriculumEctsPercent} className="h-2" />
            </div>
          </div>

          {/* Warnings */}
          {hasConflicts && (
            <Alert variant="destructive">
              <AlertTriangle className="h-4 w-4" />
              <AlertTitle>Zaman Cakismasi</AlertTitle>
              <AlertDescription>
                Secilen dersler arasinda zaman cakismasi var. Asagidaki tabloda kirmizi ile isaretlenmistir.
              </AlertDescription>
            </Alert>
          )}

          {unselectedFailedCourses.length > 0 && (
            <Alert>
              <AlertTriangle className="h-4 w-4" />
              <AlertTitle>Secilmemis Alttan Dersler</AlertTitle>
              <AlertDescription>
                {unselectedFailedCourses.map(f => f.courseCode).join(', ')} dersleri planda secili degil.
              </AlertDescription>
            </Alert>
          )}

          {/* Failed Courses */}
          {student.failedCourses.length > 0 && (
            <div className="space-y-2">
              <p className="text-xs font-semibold text-muted-foreground uppercase tracking-wide">
                Alttan Dersler ({student.failedCourses.length})
              </p>
              <div className="flex flex-wrap gap-2">
                {student.failedCourses.map(fc => (
                  <div
                    key={fc.courseCode}
                    className={`flex items-center gap-1.5 px-2.5 py-1 rounded-lg border text-sm ${
                      fc.isSelectedInPlan
                        ? 'bg-green-50 border-green-200 text-green-800'
                        : 'bg-red-50 border-red-200 text-red-800'
                    }`}
                  >
                    <span className="font-medium">{fc.courseCode}</span>
                    <span className="text-xs">({fc.grade})</span>
                    <Badge variant={fc.isSelectedInPlan ? 'success' : 'destructive'} className="text-[10px] px-1 py-0">
                      {fc.isSelectedInPlan ? 'Secili' : 'Secilmedi'}
                    </Badge>
                  </div>
                ))}
              </div>
            </div>
          )}

          {/* Selected Courses Table */}
          <div className="space-y-2">
            <p className="text-xs font-semibold text-muted-foreground uppercase tracking-wide">
              Secilen Dersler ({student.registrations.length})
            </p>
            <div className="border border-border rounded-lg overflow-hidden">
              <table className="w-full text-sm">
                <thead className="bg-muted/30 border-b border-border">
                  <tr>
                    <th className="text-left px-3 py-2 font-medium text-muted-foreground">Kod</th>
                    <th className="text-left px-3 py-2 font-medium text-muted-foreground">Ders Adi</th>
                    <th className="text-center px-3 py-2 font-medium text-muted-foreground">AKTS</th>
                    <th className="text-left px-3 py-2 font-medium text-muted-foreground">Program</th>
                    <th className="text-left px-3 py-2 font-medium text-muted-foreground">Ogretim Uyesi</th>
                    <th className="text-center px-3 py-2 font-medium text-muted-foreground">Tekrar</th>
                    <th className="text-center px-3 py-2 font-medium text-muted-foreground">Cakisma</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-border">
                  {student.registrations.map(reg => (
                    <tr
                      key={reg.registrationId}
                      className={reg.hasScheduleConflict ? 'bg-red-50' : 'hover:bg-muted/20'}
                    >
                      <td className="px-3 py-2 font-medium">{reg.courseCode}</td>
                      <td className="px-3 py-2">{reg.courseName}</td>
                      <td className="px-3 py-2 text-center">{reg.ects}</td>
                      <td className="px-3 py-2 text-xs text-muted-foreground">{reg.schedule || '—'}</td>
                      <td className="px-3 py-2 text-xs">{reg.instructorName || '—'}</td>
                      <td className="px-3 py-2 text-center">
                        {reg.isRepeated && <Badge variant="warning" className="text-[10px] px-1 py-0">Tekrar</Badge>}
                      </td>
                      <td className="px-3 py-2 text-center">
                        {reg.hasScheduleConflict && (
                          <Badge variant="destructive" className="text-[10px] px-1 py-0">Cakisma</Badge>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>

          {/* Submission Info */}
          <p className="text-xs text-muted-foreground">
            Gonderilme zamani: {fmtDate(student.submittedAt)}
          </p>

          {/* Action Error */}
          {actionError && (
            <div className="p-3 bg-destructive/10 border border-destructive/30 rounded-lg text-sm text-destructive">
              {actionError}
            </div>
          )}

          {/* Action Buttons */}
          <div className="flex items-center gap-3 pt-2 border-t border-border">
            <Button
              onClick={handleApprove}
              disabled={isPending}
              className="gap-1.5"
            >
              <CheckCircle className="h-4 w-4" />
              {approveMutation.isPending ? 'Onaylaniyor...' : 'Onayla'}
            </Button>

            {!showRejectInput ? (
              <Button
                variant="outline"
                onClick={() => setShowRejectInput(true)}
                disabled={isPending}
                className="gap-1.5 text-destructive border-destructive/30 hover:bg-destructive/10"
              >
                <XCircle className="h-4 w-4" />
                Reddet
              </Button>
            ) : (
              <div className="flex-1 flex items-center gap-2">
                <textarea
                  value={rejectNote}
                  onChange={e => setRejectNote(e.target.value)}
                  placeholder="Red nedeni (opsiyonel)..."
                  rows={2}
                  className="flex-1 border border-border rounded-lg px-3 py-2 text-sm bg-background text-foreground resize-none focus:outline-none focus:ring-2 focus:ring-ring"
                />
                <Button
                  variant="destructive"
                  onClick={handleReject}
                  disabled={isPending}
                  size="sm"
                >
                  {rejectMutation.isPending ? 'Gonderiliyor...' : 'Reddet'}
                </Button>
                <Button
                  variant="outline"
                  onClick={() => { setShowRejectInput(false); setRejectNote(''); }}
                  disabled={isPending}
                  size="sm"
                >
                  Iptal
                </Button>
              </div>
            )}
          </div>
        </CardContent>
      )}
    </Card>
  );
}

// ── Main Page ────────────────────────────────────────────────────────────────

export default function AdvisorDashboardPage() {
  const { data: students, isLoading, error } = usePendingApprovals();

  return (
    <div className="space-y-6">
      <div>
        <h1 className="font-heading text-2xl font-extrabold text-foreground tracking-tight">
          Danisman Paneli
        </h1>
        <p className="text-muted-foreground mt-1">
          Ogrencilerin ders kayit planlarini inceleyin, onaylayin veya reddedin.
        </p>
      </div>

      {isLoading && (
        <div className="text-center py-12 text-muted-foreground">Yukleniyor...</div>
      )}

      {error && (
        <Alert variant="destructive">
          <AlertTriangle className="h-4 w-4" />
          <AlertTitle>Hata</AlertTitle>
          <AlertDescription>
            Onay bekleyen ogrenciler yuklenirken hata olustu. Lutfen sayfayi yenileyin.
          </AlertDescription>
        </Alert>
      )}

      {!isLoading && !error && students && students.length === 0 && (
        <Card>
          <CardContent className="flex flex-col items-center justify-center py-12 text-center">
            <Info className="h-10 w-10 text-muted-foreground mb-3" />
            <p className="text-lg font-medium text-foreground">Onay bekleyen ogrenci bulunmuyor</p>
            <p className="text-sm text-muted-foreground mt-1">
              Tum ogrenci planlari onaylandi veya henuz plan gonderilmedi.
            </p>
          </CardContent>
        </Card>
      )}

      {!isLoading && !error && students && students.length > 0 && (
        <div className="space-y-4">
          <p className="text-sm text-muted-foreground">
            {students.length} ogrenci onay bekliyor
          </p>
          {students.map(student => (
            <StudentApprovalCard key={student.studentId} student={student} />
          ))}
        </div>
      )}
    </div>
  );
}
