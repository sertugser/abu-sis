import { useState } from 'react';
import { CalendarDays, ClipboardList, Info, ShieldAlert, XCircle } from 'lucide-react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Skeleton } from '@/components/ui/skeleton';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { cn } from '@/lib/utils';
import { useStudentAttendanceStatus } from '../hooks/useStudentAttendanceStatus';
import type { StudentAttendanceCourseDto, StudentAttendanceStatusDto } from '../types/registration';

const METHOD_LABELS: Record<string, string> = {
  Separate: 'Teori/Uygulama ayrı',
  TotalHours: 'Toplam saat',
  Passive: 'Devamsızlık takibi kapalı',
  MarkOnly: 'Sadece işaretleme (kalma yok)',
};

function isTracked(method: string) {
  return method !== 'Passive' && method !== 'MarkOnly';
}

const TH_CLS = 'h-9 px-3 text-[11px] font-semibold uppercase tracking-wide text-slate-700';

export default function AttendanceStatusPage() {
  const [selectedTermId, setSelectedTermId] = useState<number | null>(null);
  const { data, isLoading, isError } = useStudentAttendanceStatus(selectedTermId);
  const [selectedCourseRegistrationId, setSelectedCourseRegistrationId] = useState<number | null>(null);
  const selectedCourse = data?.courses.find(course => course.registrationId === selectedCourseRegistrationId) ?? null;

  if (isLoading) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-11 rounded-lg" />
        <Skeleton className="h-96 rounded-lg" />
      </div>
    );
  }

  // Backend, kayıt/aktif dönem yoksa 404 döner — bu bir hata değil, dostça boş durum göster.
  if (isError || !data) {
    return (
      <div className="flex h-full min-w-0 flex-1 flex-col">
        <div className="flex flex-1 flex-col items-center justify-center rounded-lg border border-border bg-card py-16 text-center shadow-sm">
          <ClipboardList className="mb-3 h-10 w-10 text-muted-foreground/40" />
          <h3 className="font-heading text-lg font-semibold text-foreground">Devamsızlık durumu görüntülenemiyor</h3>
          <p className="mt-1 max-w-md text-sm text-muted-foreground">
            Aktif bir döneme ait ders kaydınız bulunmuyor. Ders kaydı yaptığınızda devamsızlık bilgileriniz burada görünür.
          </p>
        </div>
      </div>
    );
  }

  const tracked = isTracked(data.policy.attendanceMethod);

  return (
    <div className="flex h-full min-w-0 flex-1 flex-col">
      {/* Proliz-tarzı tek entegre konteyner — sayfa-boyu, kompakt toolbar + temiz tablo */}
      <div className="flex flex-1 flex-col overflow-hidden rounded-lg border border-border bg-card shadow-sm">

        {/* Tek toolbar — dönem seçimi + politika rozetleri */}
        <div className="flex flex-wrap items-center gap-x-4 gap-y-2 border-b border-border bg-accent/30 px-5 py-2.5">
          <div className="flex items-center gap-2">
            <span className="text-[11px] font-semibold uppercase tracking-wide text-muted-foreground">
              Dönem
            </span>
            <div className="relative">
              <select
                value={selectedTermId ?? data.termId}
                onChange={event => {
                  setSelectedCourseRegistrationId(null);
                  setSelectedTermId(Number(event.target.value));
                }}
                className="h-8 w-[220px] appearance-none rounded-md border border-border bg-card pl-3 pr-9 text-sm font-medium text-foreground shadow-sm outline-none transition-colors hover:border-[#003366]/40 focus:border-[#003366] focus:ring-2 focus:ring-[#003366]/10"
              >
                {data.availableTerms.map(term => (
                  <option key={term.termId} value={term.termId}>{term.termName}</option>
                ))}
              </select>
              <CalendarDays className="pointer-events-none absolute right-2.5 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-muted-foreground" />
            </div>
          </div>

          <div className="ml-auto flex flex-wrap items-center gap-1.5 text-[11px]">
            <PolicyChip>{data.policy.termWeekCount} hafta</PolicyChip>
            {!tracked ? (
              <PolicyChip tone="muted">{METHOD_LABELS[data.policy.attendanceMethod] ?? data.policy.attendanceMethod}</PolicyChip>
            ) : data.policy.attendanceMethod === 'TotalHours' ? (
              <PolicyChip>Toplam limit %{formatPercent(data.policy.theoryAbsenceLimitPercent)}</PolicyChip>
            ) : (
              <>
                <PolicyChip>Teori %{formatPercent(data.policy.theoryAbsenceLimitPercent)}</PolicyChip>
                <PolicyChip>Uygulama %{formatPercent(data.policy.labAbsenceLimitPercent)}</PolicyChip>
              </>
            )}
          </div>
        </div>

        {/* Tablo — flex-1 ile kalan sayfayı doldur */}
        <div className="flex flex-1 flex-col overflow-auto">
          <div className="overflow-x-auto">
            <Table className="min-w-[1000px] text-[13px]">
              <TableHeader>
                <TableRow className="border-b border-slate-300 bg-slate-100 hover:bg-slate-100">
                  <TableHead className={cn(TH_CLS, 'w-12 text-center')}>Şb</TableHead>
                  <TableHead className={cn(TH_CLS, 'w-24')}>Ders Kodu</TableHead>
                  <TableHead className={TH_CLS}>Ders Adı</TableHead>
                  <TableHead className={cn(TH_CLS, 'w-16 text-center')}>T+U</TableHead>
                  <TableHead className={cn(TH_CLS, 'w-14 text-center')}>Krd</TableHead>
                  <TableHead className={cn(TH_CLS, 'w-14 text-center')}>Snf</TableHead>
                  <TableHead className={cn(TH_CLS, 'w-48')}>Program</TableHead>
                  <TableHead className={cn(TH_CLS, 'w-44 text-center')}>Devamsızlık Durumu</TableHead>
                  <TableHead className={cn(TH_CLS, 'w-14 text-right')}>İşlem</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {data.courses.map((course, index) => (
                  <TableRow
                    key={course.registrationId}
                    className={cn(
                      'border-b border-slate-200 transition-colors',
                      course.isFailedByAbsence
                        ? 'bg-red-50/70 hover:bg-red-50'
                        : cn('hover:bg-[#003366]/5', index % 2 === 1 && 'bg-slate-50/50')
                    )}
                  >
                    <TableCell className="px-3 py-2 text-center font-mono text-[12px] font-semibold">{course.sectionNumber}</TableCell>
                    <TableCell className="px-3 py-2 font-mono text-[13px] font-bold text-[#003366]">{course.courseCode}</TableCell>
                    <TableCell className="px-3 py-2 font-medium leading-snug text-foreground">
                      {course.courseName}
                      {course.mandatoryAttendance && (
                        <span className="ml-1.5 inline-flex items-center rounded border border-amber-400/60 px-1 py-0 text-[10px] font-semibold uppercase tracking-wide text-amber-700">
                          Devam zorunlu
                        </span>
                      )}
                    </TableCell>
                    <TableCell className="px-3 py-2 text-center font-mono text-[12px] text-muted-foreground">{course.theoryHours}+{course.labHours}</TableCell>
                    <TableCell className="px-3 py-2 text-center font-mono text-[12px] text-muted-foreground">{course.nationalCredit}</TableCell>
                    <TableCell className="px-3 py-2 text-center font-mono text-[12px] text-muted-foreground">{formatClass(course.suggestedSemester)}</TableCell>
                    <TableCell className="px-3 py-2 text-[12px] leading-snug text-muted-foreground">{course.programName || '-'}</TableCell>
                    <TableCell className="px-3 py-2 text-center">
                      <AbsenceStatusText course={course} />
                    </TableCell>
                    <TableCell className="px-3 py-2 text-right">
                      <Button
                        type="button"
                        variant="ghost"
                        size="sm"
                        onClick={() => setSelectedCourseRegistrationId(course.registrationId)}
                        aria-label={`${course.courseCode} devamsızlık bilgisini aç`}
                        title="Devamsızlık bilgisi"
                        className="h-7 w-7 px-0 text-muted-foreground hover:bg-[#003366]/10 hover:text-[#003366]"
                      >
                        <Info className="h-3.5 w-3.5" />
                        <span className="sr-only">Bilgi</span>
                      </Button>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>

          {data.courses.length === 0 && (
            <div className="py-16 text-center">
              <ClipboardList className="mx-auto mb-3 h-10 w-10 text-muted-foreground/40" />
              <h3 className="font-heading text-lg font-semibold text-foreground">Seçilen döneme ait ders kaydı bulunamadı</h3>
              <p className="mt-1 text-sm text-muted-foreground">Bu dönem için öğrencinin ders kayıt işleminden gelen ders kaydı yok.</p>
            </div>
          )}
        </div>
      </div>

      <Dialog open={selectedCourse !== null} onOpenChange={open => { if (!open) setSelectedCourseRegistrationId(null); }}>
        <DialogContent className="overflow-hidden p-0 [&>button.absolute]:hidden sm:max-w-[640px]">
          <DialogHeader className="border-b border-border bg-card px-5 py-3">
            <DialogTitle className="flex items-center gap-2 text-base font-bold text-[#003366]">
              <Info className="h-4 w-4" />
              Devamsızlık Bilgisi
            </DialogTitle>
          </DialogHeader>
          {selectedCourse && <AttendanceDetail data={data} course={selectedCourse} />}
        </DialogContent>
      </Dialog>
    </div>
  );
}

function AbsenceStatusText({ course }: { course: StudentAttendanceCourseDto }) {
  if (!course.attendanceEntered) {
    return <span className="text-[12px] font-medium text-amber-600">Yoklama girişi yapılmamış</span>;
  }
  if (course.isFailedByAbsence) {
    return <span className="text-[12px] font-semibold text-red-600">Devamsızlıktan Kaldı · {course.totalAbsenceHours} saat</span>;
  }
  if (!course.attendanceTracked) {
    return <span className="text-[12px] text-muted-foreground">{course.totalAbsenceHours} saat (takip dışı)</span>;
  }
  return <span className="text-[12px] font-medium text-emerald-600">{course.totalAbsenceHours} saat</span>;
}

function AttendanceDetail({
  data,
  course,
}: {
  data: StudentAttendanceStatusDto;
  course: StudentAttendanceCourseDto;
}) {
  const method = data.policy.attendanceMethod;
  const tracked = course.attendanceTracked;
  const totalHours = course.totalTheoryHours + course.totalLabHours;

  return (
    <div className="max-h-[80vh] overflow-y-auto">
      {/* Ders kimlik bilgileri — InfoRow stili (GradeList ile tutarlı) */}
      <div>
        <DetailRow label="Dönem" value={data.termName} />
        <DetailRow label="Ders" value={`${course.courseCode} - ${course.courseName}`} />
        <DetailRow label="Şube" value={String(course.sectionNumber)} mono />
        <DetailRow label="T+U" value={`${course.theoryHours}+${course.labHours}`} mono />
        <DetailRow label="Yöntem" value={METHOD_LABELS[method] ?? method} />
      </div>

      <div className="border-t border-border bg-muted/30 px-5 py-4 text-sm">
        <p className="font-semibold text-foreground">
          Toplam {data.policy.termWeekCount} Hafta ({totalHours} Saat).
        </p>
        {!tracked ? (
          <p className="mt-2 text-muted-foreground">
            Bu dönemde devamsızlık takibi kapalı; devamsızlık devamsızlıktan kalmaya yol açmaz.
          </p>
        ) : method === 'TotalHours' ? (
          <p className="mt-2 text-muted-foreground">
            Toplam (Teori+Uygulama) {totalHours} Saat, Devamsızlık Sınırı (%{formatPercent(data.policy.theoryAbsenceLimitPercent)})/{course.totalAbsenceLimitHours} Saat.
          </p>
        ) : (
          <div className="mt-2 space-y-1 text-muted-foreground">
            <p>Teori Toplamı {course.totalTheoryHours} Saat, Devamsızlık Oranı (%{formatPercent(data.policy.theoryAbsenceLimitPercent)})/{course.theoryAbsenceLimitHours} Saat.</p>
            <p>Uygulama Toplamı {course.totalLabHours} Saat, Devamsızlık Oranı (%{formatPercent(data.policy.labAbsenceLimitPercent)})/{course.labAbsenceLimitHours} Saat.</p>
          </div>
        )}
      </div>

      <div className="border-t border-border p-5">
        {course.isFailedByAbsence ? (
          <Alert variant="destructive">
            <XCircle className="h-4 w-4" />
            <AlertDescription>
              Devamsızlık sınırı aşıldı — bu dersten devamsızlıktan kaldınız (toplam {course.totalAbsenceHours ?? 0} saat).
            </AlertDescription>
          </Alert>
        ) : (
          <Alert>
            <ShieldAlert className="h-4 w-4" />
            <AlertDescription className="text-muted-foreground">
              {course.attendanceEntered
                ? `Toplam devamsızlık: ${course.totalAbsenceHours ?? 0} saat.${tracked ? ' Sınır aşılmadı.' : ''}`
                : course.attendanceStatusLabel}
            </AlertDescription>
          </Alert>
        )}
      </div>
    </div>
  );
}

function DetailRow({ label, value, mono = false }: { label: string; value: string; mono?: boolean }) {
  return (
    <div className="grid grid-cols-1 border-b border-border/60 transition-colors last:border-b-0 hover:bg-muted/30 sm:grid-cols-[minmax(140px,200px)_1fr]">
      <div className="px-5 py-2 text-xs text-muted-foreground sm:text-sm">{label}</div>
      <div className={cn('px-5 py-2 text-sm text-foreground sm:text-right', mono && 'font-mono')}>{value}</div>
    </div>
  );
}

function PolicyChip({ children, tone }: { children: React.ReactNode; tone?: 'muted' }) {
  return (
    <span
      className={cn(
        'rounded-md border px-2 py-0.5 font-medium',
        tone === 'muted'
          ? 'border-border bg-card text-muted-foreground'
          : 'border-[#003366]/20 bg-[#003366]/5 text-[#003366]'
      )}
    >
      {children}
    </span>
  );
}

function formatClass(suggestedSemester: number) {
  if (!suggestedSemester || suggestedSemester < 1)
    return '-';

  return String(Math.ceil(suggestedSemester / 2));
}

function formatPercent(value: number) {
  return Number.isInteger(value)
    ? String(value)
    : value.toFixed(2).replace('.', ',');
}
