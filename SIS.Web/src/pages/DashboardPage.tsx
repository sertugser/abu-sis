import { Badge } from '@/components/ui/badge';
import { Card, CardContent } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import {
  Building2,
  CalendarRange,
  CreditCard,
  GraduationCap,
  IdCard,
  Landmark,
  ShieldCheck,
  Sparkles,
} from 'lucide-react';
import { useStudentInfo } from '../hooks/useStudentInfo';
import { useActiveTerm } from '../hooks/useActiveTerm';
import { STUDENT_STATUS_LABELS } from '../lib/constants';
import { formatDate, getEctsLimit } from '../lib/utils';

export default function DashboardPage() {
  const { data: student, isLoading: studentLoading } = useStudentInfo();
  const { data: term } = useActiveTerm();

  if (studentLoading) {
    return (
      <div className="space-y-6">
        <Skeleton className="h-52 rounded-3xl" />
        <div className="grid grid-cols-1 gap-4 md:grid-cols-2 xl:grid-cols-4">
          {[1, 2, 3, 4].map(i => (
            <Skeleton key={i} className="h-36 rounded-2xl" />
          ))}
        </div>
        <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
          <Skeleton className="h-44 rounded-2xl" />
          <Skeleton className="h-44 rounded-2xl" />
        </div>
      </div>
    );
  }

  if (!student) {
    return <div className="py-16 text-center text-muted-foreground">Öğrenci bilgisi bulunamadı.</div>;
  }

  const ectsLimit = getEctsLimit(student.cgpa);
  const currentSemester = student.completedSemesters + 1;
  const classYear = Math.min(Math.floor(student.completedSemesters / 2) + 1, 4);
  const statusLabel = STUDENT_STATUS_LABELS[student.status] || student.status;

  return (
    <div className="space-y-8">
      <Card className="overflow-hidden border-0 shadow-[0_24px_60px_rgba(15,23,42,0.12)]">
        <CardContent className="relative p-0">
          <div className="absolute inset-0 bg-[radial-gradient(circle_at_top_right,rgba(20,184,166,0.20),transparent_24%),linear-gradient(135deg,rgba(19,35,63,1)_0%,rgba(26,44,74,0.98)_55%,rgba(18,32,56,1)_100%)]" />
          <div className="relative grid gap-6 px-7 py-8 text-white lg:grid-cols-[1.55fr_1fr] lg:px-8">
            <div className="space-y-5">
              <div className="inline-flex items-center gap-2 rounded-full border border-white/15 bg-white/10 px-3 py-1 text-xs font-semibold uppercase tracking-[0.18em] text-slate-200">
                <Sparkles className="h-3.5 w-3.5" />
                Öğrenci Paneli
              </div>

              <div>
                <h1 className="font-heading text-3xl font-extrabold tracking-tight text-white">
                  Hoş Geldiniz, {student.firstName}
                </h1>
                <p className="mt-2 max-w-2xl text-[15px] leading-7 text-slate-200/90">
                  {student.programName} programında {classYear}. sınıf {currentSemester}. dönem öğrencisi olarak
                  akademik takviminizi, ders işlemlerinizi ve dönem performansınızı tek ekrandan takip edebilirsiniz.
                </p>
              </div>

              <div className="flex flex-wrap gap-2">
                <Badge variant="secondary" className="border border-white/10 bg-white/12 text-white">
                  {classYear}. Sınıf
                </Badge>
                <Badge variant="secondary" className="border border-white/10 bg-white/12 text-white">
                  {currentSemester}. Dönem
                </Badge>
                <Badge variant="secondary" className="border border-white/10 bg-white/12 text-white">
                  AKTS Limiti: {ectsLimit}
                </Badge>
                <Badge variant="secondary" className="border border-emerald-300/10 bg-emerald-400/15 text-emerald-100">
                  {statusLabel}
                </Badge>
              </div>
            </div>

            <div className="grid gap-3 sm:grid-cols-3 lg:grid-cols-1">
              <HeroInfoTile label="Öğrenci No" value={student.studentNumber} icon={IdCard} />
              <HeroInfoTile label="GANO" value={student.cgpa.toFixed(2)} icon={GraduationCap} />
              <HeroInfoTile label="Fakülte" value={student.facultyName} icon={Landmark} compact />
            </div>
          </div>
        </CardContent>
      </Card>

      <div className="grid grid-cols-1 gap-5 md:grid-cols-2 xl:grid-cols-4 stagger-children">
        <StatCard
          icon={IdCard}
          label="Öğrenci No"
          value={student.studentNumber}
          sub="Resmi öğrenci kaydı"
          accentColor="from-primary to-primary-light"
          mono
        />
        <StatCard
          icon={GraduationCap}
          label="GANO"
          value={student.cgpa.toFixed(2)}
          sub={`AKTS Limiti: ${ectsLimit}`}
          accentColor="from-accent to-success"
        />
        <StatCard
          icon={CalendarRange}
          label="Sınıf / Dönem"
          value={`${classYear}. Sınıf`}
          sub={`${currentSemester}. Dönem`}
          accentColor="from-info to-primary"
        />
        <StatCard
          icon={ShieldCheck}
          label="Öğrenim Durumu"
          value={statusLabel}
          sub={student.isScholarshipStudent ? 'Burslu öğrenci statüsü aktif' : 'Standart öğrenci statüsü'}
          accentColor="from-success to-accent"
        />
      </div>

      <div className="grid grid-cols-1 gap-5 lg:grid-cols-[1.15fr_0.85fr]">
        <Card className="overflow-hidden">
          <CardContent className="p-6">
            <div className="flex items-start gap-4">
              <div className="flex h-12 w-12 shrink-0 items-center justify-center rounded-2xl bg-primary/10 text-primary">
                <Building2 className="h-6 w-6" />
              </div>
              <div className="space-y-4">
                <div>
                  <p className="text-xs font-semibold uppercase tracking-[0.16em] text-muted-foreground">
                    Program Bilgisi
                  </p>
                  <h3 className="mt-2 font-heading text-2xl font-bold text-foreground">{student.programName}</h3>
                  <p className="mt-1 text-sm text-muted-foreground">{student.departmentName}</p>
                </div>
                <div className="grid gap-3 sm:grid-cols-2">
                  <MiniInfo label="Fakülte" value={student.facultyName} />
                  <MiniInfo label="Tamamlanan Dönem" value={String(student.completedSemesters)} />
                </div>
              </div>
            </div>
          </CardContent>
        </Card>

        <Card className="overflow-hidden">
          <CardContent className="p-6">
            <div className="flex items-start gap-4">
              <div className="flex h-12 w-12 shrink-0 items-center justify-center rounded-2xl bg-accent/12 text-accent">
                <CreditCard className="h-6 w-6" />
              </div>
              <div className="space-y-4">
                <div>
                  <p className="text-xs font-semibold uppercase tracking-[0.16em] text-muted-foreground">
                    Akademik Profil
                  </p>
                  <h3 className="mt-2 font-heading text-2xl font-bold text-foreground">{statusLabel}</h3>
                  <p className="mt-1 text-sm text-muted-foreground">
                    {student.isScholarshipStudent
                      ? 'Burs avantajı aktif durumda.'
                      : 'Standart öğrenci statüsü ile öğretime devam ediyor.'}
                  </p>
                </div>
                <div className="flex flex-wrap gap-2">
                  <Badge variant={student.status === 'Active' ? 'success' : 'secondary'}>{statusLabel}</Badge>
                  {student.isScholarshipStudent && <Badge variant="accent">Burslu</Badge>}
                  {student.hasFinancialBlock && <Badge variant="destructive">Mali Blok</Badge>}
                </div>
              </div>
            </div>
          </CardContent>
        </Card>
      </div>

      {term && (
        <Card className="overflow-hidden">
          <CardContent className="p-0">
            <div className="border-b border-border/70 bg-gradient-to-r from-primary/[0.07] via-transparent to-accent/[0.08] px-6 py-4">
              <div className="flex flex-col gap-2 lg:flex-row lg:items-center lg:justify-between">
                <div>
                  <h3 className="font-heading text-lg font-semibold text-foreground">Aktif Dönem Bilgisi</h3>
                  <p className="mt-1 text-sm text-muted-foreground">
                    Kayıt ve ekle/bırak aralıklarını bu dönem takvimine göre takip edebilirsiniz.
                  </p>
                </div>
                <div className="flex flex-wrap gap-2">
                  <Badge variant={term.isRegistrationOpen ? 'success' : 'secondary'}>
                    {term.isRegistrationOpen ? 'Kayıt Açık' : 'Kayıt Kapalı'}
                  </Badge>
                  <Badge variant={term.isAddDropOpen ? 'info' : 'secondary'}>
                    {term.isAddDropOpen ? 'Ek/Bırak Açık' : 'Ek/Bırak Kapalı'}
                  </Badge>
                </div>
              </div>
            </div>

            <div className="grid gap-4 px-6 py-6 text-sm md:grid-cols-3">
              <TermInfoItem label="Dönem" value={term.name} />
              <TermInfoItem
                label="Kayıt Tarihleri"
                value={`${formatDate(term.registrationStart)} — ${formatDate(term.registrationEnd)}`}
              />
              <TermInfoItem
                label="Ek/Bırak Tarihleri"
                value={`${formatDate(term.addDropStart)} — ${formatDate(term.addDropEnd)}`}
              />
            </div>
          </CardContent>
        </Card>
      )}
    </div>
  );
}

interface StatCardProps {
  icon: React.ComponentType<{ className?: string }>;
  label: string;
  value: string;
  sub?: string;
  accentColor: string;
  mono?: boolean;
  valueSize?: string;
}

function StatCard({
  icon: Icon,
  label,
  value,
  sub,
  accentColor,
  mono,
  valueSize = 'text-xl',
}: StatCardProps) {
  return (
    <Card className="overflow-hidden border-border/70 hover:-translate-y-0.5 hover:shadow-[0_16px_36px_rgba(15,23,42,0.08)]">
      <CardContent className="p-5">
        <div className={`mb-4 h-1.5 rounded-full bg-gradient-to-r ${accentColor}`} />
        <div className="flex items-start gap-4">
          <div className="flex h-11 w-11 shrink-0 items-center justify-center rounded-2xl bg-muted text-primary">
            <Icon className="h-5 w-5" />
          </div>
          <div className="space-y-1">
            <p className="text-xs font-medium uppercase tracking-wider text-muted-foreground">{label}</p>
            <p className={`${valueSize} font-bold text-foreground ${mono ? 'font-mono' : 'font-heading'}`}>{value}</p>
            {sub && <p className="text-xs text-muted-foreground">{sub}</p>}
          </div>
        </div>
      </CardContent>
    </Card>
  );
}

function HeroInfoTile({
  label,
  value,
  icon: Icon,
  compact = false,
}: {
  label: string;
  value: string;
  icon: React.ComponentType<{ className?: string }>;
  compact?: boolean;
}) {
  return (
    <div className="rounded-2xl border border-white/10 bg-white/10 px-4 py-4 shadow-[inset_0_1px_0_rgba(255,255,255,0.04)]">
      <div className="mb-3 flex h-9 w-9 items-center justify-center rounded-xl bg-white/10 text-slate-100">
        <Icon className="h-[18px] w-[18px]" />
      </div>
      <p className="text-[11px] font-semibold uppercase tracking-[0.16em] text-slate-300">{label}</p>
      <p className={`mt-1 font-heading font-bold text-white ${compact ? 'text-base leading-snug' : 'text-2xl'}`}>{value}</p>
    </div>
  );
}

function MiniInfo({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-2xl border border-border/70 bg-muted/20 px-4 py-3">
      <p className="text-xs font-semibold uppercase tracking-[0.16em] text-muted-foreground">{label}</p>
      <p className="mt-1 text-sm font-medium text-foreground">{value}</p>
    </div>
  );
}

function TermInfoItem({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-2xl border border-border/70 bg-muted/20 px-4 py-4">
      <p className="text-xs font-semibold uppercase tracking-[0.16em] text-muted-foreground">{label}</p>
      <p className="mt-2 text-base font-semibold text-foreground">{value}</p>
    </div>
  );
}
