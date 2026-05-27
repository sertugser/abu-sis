import { Dialog, DialogTitle } from '@/components/ui/dialog';
import ResizableDialogContent from '@/components/ui/resizable-dialog';
import { Skeleton } from '@/components/ui/skeleton';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { AlertCircle, GraduationCap, BookOpen, ScrollText, TrendingUp } from 'lucide-react';
import {
  useAdvisorStudentCourseHistory,
  useAdvisorStudentCurriculumStatus,
  useAdvisorStudentTranscript,
  useAdvisorStudentTermGpa,
} from '../../hooks/useAdvisor';

export type AdvisorInfoView = 'curriculum' | 'history' | 'transcript' | 'gpa' | null;

const VIEW_META: Record<Exclude<AdvisorInfoView, null>, { title: string; icon: React.ReactNode }> = {
  curriculum: { title: 'Müfredat Durumu', icon: <GraduationCap className="h-5 w-5 text-primary" /> },
  history: { title: 'Aldığı Dersler / Ders Kayıt Geçmişi', icon: <BookOpen className="h-5 w-5 text-primary" /> },
  transcript: { title: 'Transkript', icon: <ScrollText className="h-5 w-5 text-primary" /> },
  gpa: { title: 'Dönem Ortalamaları', icon: <TrendingUp className="h-5 w-5 text-primary" /> },
};

interface Props {
  studentId: number | null;
  view: AdvisorInfoView;
  onClose: () => void;
}

/**
 * Danışmanın, danışanının bilgi görünümleri (Proliz "Ek/Diğer İşlemler"): Müfredat Durumu,
 * Aldığı Dersler, Transkript, Dönem Ortalamaları. Tek dialog; açık görünüm `view` ile seçilir.
 * Veriler danışman-kapsamlı endpoint'lerden (mevcut servislerin sarmalı) gelir.
 */
export default function AdvisorStudentInfoDialogs({ studentId, view, onClose }: Props) {
  const open = view != null;
  const meta = view ? VIEW_META[view] : null;

  return (
    <Dialog open={open} onOpenChange={(o) => { if (!o) onClose(); }}>
      <ResizableDialogContent defaultWidth={860} defaultHeight={680} minWidth={560} minHeight={460} className="flex flex-col">
        <DialogTitle className="sr-only">{meta?.title ?? 'Bilgi'}</DialogTitle>
        <div className="shrink-0 border-b border-border pb-3">
          <div className="flex items-center gap-2">
            {meta?.icon}
            <h2 className="font-heading text-base font-bold text-foreground">{meta?.title}</h2>
          </div>
        </div>
        <div className="flex-1 overflow-y-auto pt-4 pr-1">
          {view === 'curriculum' && <CurriculumView studentId={studentId} active={open} />}
          {view === 'history' && <HistoryView studentId={studentId} active={open} />}
          {view === 'transcript' && <TranscriptView studentId={studentId} active={open} />}
          {view === 'gpa' && <GpaView studentId={studentId} active={open} />}
        </div>
      </ResizableDialogContent>
    </Dialog>
  );
}

function Loading() {
  return <div className="space-y-3"><Skeleton className="h-10 rounded" /><Skeleton className="h-40 rounded" /></div>;
}
function ErrorBox({ msg }: { msg: string }) {
  return <Alert variant="destructive"><AlertCircle className="h-4 w-4" /><AlertDescription>{msg}</AlertDescription></Alert>;
}

// Not durumu rengi (geçti/kaldı/almadı)
function gradeColor(grade: string | null, gradeStatus: string | null): string {
  if (!grade) return 'text-muted-foreground';
  const s = (gradeStatus ?? '').toLocaleLowerCase('tr');
  if (s.includes('kal') || s.includes('fail') || grade === 'FF' || grade === 'NA' || grade === 'U') return 'text-destructive';
  return 'text-emerald-600 dark:text-emerald-400';
}

function CurriculumView({ studentId, active }: { studentId: number | null; active: boolean }) {
  const { data, isLoading, error } = useAdvisorStudentCurriculumStatus(studentId, active);
  if (isLoading) return <Loading />;
  if (error) return <ErrorBox msg="Müfredat durumu yüklenemedi." />;
  if (!data) return <ErrorBox msg="Müfredat durumu bulunamadı." />;
  const ectsPct = data.totalEcts > 0 ? Math.round((data.passedEcts / data.totalEcts) * 100) : 0;
  return (
    <div className="space-y-4">
      <section className="rounded-lg border border-border bg-card p-4">
        <h3 className="text-sm font-semibold text-foreground">{data.programName} <span className="text-xs text-muted-foreground">({data.curriculumVersion})</span></h3>
        <div className="mt-2 grid grid-cols-2 gap-2 text-xs sm:grid-cols-4">
          <Stat label="GANO" value={data.cgpa.toFixed(2)} />
          <Stat label="Geçilen AKTS" value={`${data.passedEcts} / ${data.totalEcts}`} />
          <Stat label="Geçilen Kredi" value={`${data.passedCredits} / ${data.totalCredits}`} />
          <Stat label="İlerleme" value={`%${ectsPct}`} />
        </div>
      </section>
      {data.semesters.map(sem => (
        <section key={sem.semesterNumber}>
          <p className="mb-1 text-xs font-semibold uppercase tracking-wide text-muted-foreground">{sem.label}</p>
          <div className="overflow-hidden rounded-lg border border-border">
            <table className="w-full text-xs">
              <tbody>
                {sem.courses.map(c => (
                  <tr key={c.courseId} className="border-b border-border last:border-0">
                    <td className="px-2 py-1.5 font-mono font-semibold text-primary">{c.code}</td>
                    <td className="px-2 py-1.5 text-foreground">{c.name}</td>
                    <td className="px-2 py-1.5 text-center text-muted-foreground">{c.ects} AKTS</td>
                    <td className="px-2 py-1.5 text-center">
                      {c.isCompulsory ? <span className="text-muted-foreground">Z</span> : <span className="text-muted-foreground">S</span>}
                    </td>
                    <td className={`px-2 py-1.5 text-right font-semibold ${gradeColor(c.grade, c.gradeStatus)}`}>
                      {c.grade ?? (c.prerequisitesMet ? 'Alınmadı' : 'Önkoşul eksik')}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>
      ))}
    </div>
  );
}

function HistoryView({ studentId, active }: { studentId: number | null; active: boolean }) {
  const { data, isLoading, error } = useAdvisorStudentCourseHistory(studentId, active);
  if (isLoading) return <Loading />;
  if (error) return <ErrorBox msg="Ders geçmişi yüklenemedi." />;
  if (!data || data.length === 0) return <p className="text-sm text-muted-foreground">Kayıt bulunmuyor.</p>;
  return (
    <div className="space-y-4">
      {data.map(term => (
        <section key={term.termId}>
          <p className="mb-1 text-xs font-semibold uppercase tracking-wide text-muted-foreground">{term.termName}</p>
          <div className="overflow-x-auto rounded-lg border border-border">
            <table className="w-full min-w-[560px] text-xs">
              <thead>
                <tr className="border-b border-border bg-muted/40 text-left text-[11px] uppercase text-muted-foreground">
                  <th className="px-2 py-1.5">Kod</th><th className="px-2 py-1.5">Ad</th>
                  <th className="px-2 py-1.5 text-center">Krd</th><th className="px-2 py-1.5 text-center">AKTS</th>
                  <th className="px-2 py-1.5">Alış</th><th className="px-2 py-1.5 text-right">Not</th>
                </tr>
              </thead>
              <tbody>
                {term.courses.map(c => (
                  <tr key={c.registrationId} className="border-b border-border last:border-0">
                    <td className="px-2 py-1.5 font-mono font-semibold text-primary">{c.courseCode}</td>
                    <td className="px-2 py-1.5 text-foreground">{c.courseName}</td>
                    <td className="px-2 py-1.5 text-center font-mono">{c.nationalCredit}</td>
                    <td className="px-2 py-1.5 text-center font-mono">{c.ects}</td>
                    <td className="px-2 py-1.5 text-muted-foreground">{c.acquisitionType}</td>
                    <td className={`px-2 py-1.5 text-right font-semibold ${gradeColor(c.grade, c.status)}`}>{c.grade ?? '—'}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>
      ))}
    </div>
  );
}

function TranscriptView({ studentId, active }: { studentId: number | null; active: boolean }) {
  const { data, isLoading, error } = useAdvisorStudentTranscript(studentId, active);
  if (isLoading) return <Loading />;
  if (error) return <ErrorBox msg="Transkript yüklenemedi." />;
  if (!data) return <ErrorBox msg="Transkript bulunamadı." />;
  return (
    <div className="space-y-4">
      <section className="rounded-lg border border-border bg-card p-4">
        <h3 className="text-sm font-semibold text-foreground">{data.fullName} <span className="font-mono text-xs text-muted-foreground">({data.studentNumber})</span></h3>
        <p className="text-xs text-muted-foreground">{data.programName}</p>
        <div className="mt-2 grid grid-cols-2 gap-2 text-xs sm:grid-cols-4">
          <Stat label="GANO" value={data.currentCgpa.toFixed(2)} />
          <Stat label="Kazanılan AKTS" value={`${data.totalEarnedEcts}`} />
          <Stat label="Denenen AKTS" value={`${data.totalAttemptedEcts}`} />
          <Stat label="Kazanılan Kredi" value={`${data.totalEarnedCredits}`} />
        </div>
      </section>
      {data.terms.map(term => (
        <section key={term.termId}>
          <div className="mb-1 flex items-center justify-between text-xs">
            <span className="font-semibold uppercase tracking-wide text-muted-foreground">{term.termName}</span>
            <span className="text-muted-foreground">DNO <strong className="text-foreground">{term.sgpa.toFixed(2)}</strong> · GNO <strong className="text-foreground">{term.cgpa.toFixed(2)}</strong></span>
          </div>
          <div className="overflow-x-auto rounded-lg border border-border">
            <table className="w-full min-w-[560px] text-xs">
              <thead>
                <tr className="border-b border-border bg-muted/40 text-left text-[11px] uppercase text-muted-foreground">
                  <th className="px-2 py-1.5">Kod</th><th className="px-2 py-1.5">Ad</th>
                  <th className="px-2 py-1.5 text-center">Krd</th><th className="px-2 py-1.5 text-center">AKTS</th>
                  <th className="px-2 py-1.5 text-right">Not</th>
                </tr>
              </thead>
              <tbody>
                {term.courses.map(c => (
                  <tr key={c.registrationId} className="border-b border-border last:border-0">
                    <td className="px-2 py-1.5 font-mono font-semibold text-primary">{c.courseCode}</td>
                    <td className="px-2 py-1.5 text-foreground">{c.courseName}</td>
                    <td className="px-2 py-1.5 text-center font-mono">{c.credits}</td>
                    <td className="px-2 py-1.5 text-center font-mono">{c.ects}</td>
                    <td className={`px-2 py-1.5 text-right font-semibold ${c.isPassed ? 'text-emerald-600 dark:text-emerald-400' : 'text-destructive'}`}>{c.grade}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>
      ))}
    </div>
  );
}

function GpaView({ studentId, active }: { studentId: number | null; active: boolean }) {
  const { data, isLoading, error } = useAdvisorStudentTermGpa(studentId, active);
  if (isLoading) return <Loading />;
  if (error) return <ErrorBox msg="Dönem ortalamaları yüklenemedi." />;
  if (!data || data.terms.length === 0) return <p className="text-sm text-muted-foreground">Kayıt bulunmuyor.</p>;
  return (
    <div className="overflow-x-auto rounded-lg border border-border">
      <table className="w-full min-w-[560px] text-xs">
        <thead>
          <tr className="border-b border-border bg-muted/40 text-left text-[11px] uppercase text-muted-foreground">
            <th className="px-2 py-2">Dönem</th><th className="px-2 py-2 text-center">Ders</th>
            <th className="px-2 py-2 text-center">Kredi</th><th className="px-2 py-2 text-center">AKTS</th>
            <th className="px-2 py-2 text-center">DNO</th><th className="px-2 py-2 text-center">GNO</th>
          </tr>
        </thead>
        <tbody>
          {data.terms.map(t => (
            <tr key={t.termId} className="border-b border-border last:border-0 hover:bg-muted/20">
              <td className="px-2 py-2 text-foreground">{t.termName}</td>
              <td className="px-2 py-2 text-center">{t.courseCount}</td>
              <td className="px-2 py-2 text-center font-mono">{t.totalCredits}</td>
              <td className="px-2 py-2 text-center font-mono">{t.totalEcts}</td>
              <td className="px-2 py-2 text-center font-mono font-semibold">{t.sgpa != null ? t.sgpa.toFixed(2) : '—'}</td>
              <td className="px-2 py-2 text-center font-mono font-semibold">{t.cgpa != null ? t.cgpa.toFixed(2) : '—'}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function Stat({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-md border border-border bg-background px-2 py-1.5">
      <p className="text-[10px] font-semibold uppercase tracking-wide text-muted-foreground">{label}</p>
      <p className="mt-0.5 font-mono text-sm font-bold text-foreground">{value}</p>
    </div>
  );
}
