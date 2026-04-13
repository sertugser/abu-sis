import { useEffect, useMemo, useState } from 'react';
import { AlertCircle, CalendarDays, ClipboardList, Info, ShieldAlert } from 'lucide-react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
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
import { useStudentAttendanceStatus } from '../hooks/useStudentAttendanceStatus';
import type { StudentAttendanceCourseDto, StudentAttendanceStatusDto } from '../types/registration';

export default function AttendanceStatusPage() {
  const [selectedTermId, setSelectedTermId] = useState<number | null>(null);
  const { data, isLoading, isError } = useStudentAttendanceStatus(selectedTermId);
  const [selectedCourse, setSelectedCourse] = useState<StudentAttendanceCourseDto | null>(null);

  useEffect(() => {
    if (data && selectedTermId == null) {
      setSelectedTermId(data.termId);
    }
  }, [data, selectedTermId]);

  useEffect(() => {
    if (!data || !selectedCourse) return;
    if (!data.courses.some(course => course.registrationId === selectedCourse.registrationId)) {
      setSelectedCourse(null);
    }
  }, [data, selectedCourse]);

  const totals = useMemo(() => {
    const courses = data?.courses ?? [];
    return {
      courseCount: courses.length,
      weeklyHours: courses.reduce((sum, course) => sum + course.theoryHours + course.labHours, 0),
      enteredCount: courses.filter(course => course.attendanceEntered).length,
    };
  }, [data]);

  if (isLoading) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-24 rounded-xl" />
        <Skeleton className="h-12 rounded-xl" />
        <Skeleton className="h-96 rounded-xl" />
      </div>
    );
  }

  if (isError || !data) {
    return (
      <Alert variant="destructive">
        <AlertCircle className="h-4 w-4" />
        <AlertDescription>Devamsızlık durumu yüklenirken hata oluştu.</AlertDescription>
      </Alert>
    );
  }

  return (
    <div className="space-y-5">
      <Card className="overflow-hidden border border-[#003366]/20 shadow-sm">
        <CardContent className="p-0">
          <div className="bg-[#003366] px-5 py-4 text-white">
            <p className="text-xs font-semibold uppercase tracking-wider text-white/60">Ders ve Dönem İşlemleri</p>
            <h1 className="font-heading text-xl font-extrabold">Devamsızlık Durumu</h1>
          </div>
          <div className="grid grid-cols-2 gap-px bg-border md:grid-cols-4">
            <SummaryCell label="Dönem" value={data.termName} />
            <SummaryCell label="Ders" value={String(totals.courseCount)} />
            <SummaryCell label="Haftalık Saat" value={String(totals.weeklyHours)} />
            <SummaryCell label="Yoklama Girilen" value={String(totals.enteredCount)} />
          </div>
        </CardContent>
      </Card>

      <Card className="border-border shadow-sm">
        <CardContent className="flex flex-col gap-3 p-4 md:flex-row md:items-center md:justify-between">
          <div className="relative w-full md:w-72">
            <select
              value={selectedTermId ?? data.termId}
              onChange={event => {
                setSelectedCourse(null);
                setSelectedTermId(Number(event.target.value));
              }}
              className="h-10 w-full appearance-none rounded-md border border-border bg-card px-3 pr-9 text-sm font-semibold text-[#003366] shadow-sm outline-none transition-colors hover:border-[#003366]/40 focus:border-[#003366] focus:ring-2 focus:ring-[#003366]/10"
            >
              {data.availableTerms.map(term => (
                <option key={term.termId} value={term.termId}>{term.termName}</option>
              ))}
            </select>
            <CalendarDays className="pointer-events-none absolute right-3 top-1/2 h-4 w-4 -translate-y-1/2 text-[#003366]" />
          </div>

          <div className="flex flex-wrap items-center gap-2 text-xs">
            <Badge variant="secondary" className="rounded-md px-2 py-1">
              {data.policy.termWeekCount} hafta
            </Badge>
            <Badge variant="outline" className="rounded-md px-2 py-1">
              Teori %{formatPercent(data.policy.theoryAbsenceLimitPercent)}
            </Badge>
            <Badge variant="outline" className="rounded-md px-2 py-1">
              Uygulama %{formatPercent(data.policy.labAbsenceLimitPercent)}
            </Badge>
          </div>
        </CardContent>
      </Card>

      <Card className="overflow-hidden border border-[#003366]/20 shadow-md">
        <CardContent className="p-0">
          <div className="overflow-x-auto">
            <Table>
              <TableHeader>
                <TableRow className="border-b-0 bg-[#003366] hover:bg-[#003366]">
                  <TableHead className="w-16 pl-5 text-center text-white">Şb</TableHead>
                  <TableHead className="w-32 text-white">Ders Kodu</TableHead>
                  <TableHead className="min-w-[280px] text-white">Ders Adı</TableHead>
                  <TableHead className="w-20 text-center text-white">T+U</TableHead>
                  <TableHead className="w-20 text-center text-white">Krd</TableHead>
                  <TableHead className="w-20 text-center text-white">Snf</TableHead>
                  <TableHead className="min-w-[220px] text-white">Program</TableHead>
                  <TableHead className="min-w-[230px] pr-5 text-right text-white">Toplam Devamsızlık Saati</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {data.courses.map((course, index) => (
                  <TableRow key={course.registrationId} className={index % 2 === 1 ? 'bg-muted/10' : undefined}>
                    <TableCell className="pl-5 text-center font-semibold">{course.sectionNumber}</TableCell>
                    <TableCell className="font-mono text-sm font-bold text-[#003366]">{course.courseCode}</TableCell>
                    <TableCell>
                      <div className="font-medium text-foreground">{course.courseName}</div>
                      {course.mandatoryAttendance && (
                        <div className="mt-0.5 text-xs text-amber-700">Devam zorunlu</div>
                      )}
                    </TableCell>
                    <TableCell className="text-center font-mono text-sm">{course.theoryHours}+{course.labHours}</TableCell>
                    <TableCell className="text-center">{course.nationalCredit}</TableCell>
                    <TableCell className="text-center">{formatClass(course.suggestedSemester)}</TableCell>
                    <TableCell className="text-sm text-muted-foreground">{course.programName || '-'}</TableCell>
                    <TableCell className="pr-5 text-right">
                      <Button
                        type="button"
                        variant="ghost"
                        size="sm"
                        onClick={() => setSelectedCourse(course)}
                        className="h-auto min-h-8 max-w-full justify-end gap-2 whitespace-normal rounded-md px-2 text-right text-[#006699] hover:bg-[#006699]/10 hover:text-[#004f73]"
                      >
                        <Info className="h-4 w-4 shrink-0" />
                        <span>{formatAbsenceStatus(course)}</span>
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
        </CardContent>
      </Card>

      <Dialog open={selectedCourse !== null} onOpenChange={open => { if (!open) setSelectedCourse(null); }}>
        <DialogContent className="max-w-[min(760px,calc(100vw-2rem))] overflow-hidden p-0">
          <DialogHeader className="border-b bg-[#003366] px-5 py-3 text-white">
            <DialogTitle className="text-base">Devamsızlık Bilgisi</DialogTitle>
          </DialogHeader>
          {selectedCourse && <AttendanceDetail data={data} course={selectedCourse} />}
        </DialogContent>
      </Dialog>
    </div>
  );
}

function AttendanceDetail({
  data,
  course,
}: {
  data: StudentAttendanceStatusDto;
  course: StudentAttendanceCourseDto;
}) {
  const totalHours = course.totalTheoryHours + course.totalLabHours;

  return (
    <div className="space-y-4 bg-[#f7f7f7] p-5 text-sm">
      <div className="rounded-md border border-slate-300 bg-white">
        <InfoRow label="Dönem" value={data.termName} />
        <InfoRow label="Ders" value={`${course.courseCode} - ${course.courseName}`} />
        <InfoRow label="Şube" value={String(course.sectionNumber)} />
        <InfoRow label="T+U" value={`${course.theoryHours}+${course.labHours}`} />
      </div>

      <div className="rounded-md border border-slate-300 bg-white p-4">
        <p className="font-semibold text-slate-900">
          Toplam {data.policy.termWeekCount} Hafta ({totalHours} Saat).
        </p>
        <p className="mt-2">
          Teori Toplamı {course.totalTheoryHours} Saat, Devamsızlık Oranı (%{formatPercent(data.policy.theoryAbsenceLimitPercent)})/{course.theoryAbsenceLimitHours} Saat.
        </p>
        <p className="mt-1">
          Uygulama Toplamı {course.totalLabHours} Saat, Devamsızlık Oranı (%{formatPercent(data.policy.labAbsenceLimitPercent)})/{course.labAbsenceLimitHours} Saat.
        </p>
      </div>

      <Alert className="border-[#006699]/20 bg-[#006699]/5">
        <ShieldAlert className="h-4 w-4 text-[#006699]" />
        <AlertDescription className="text-slate-700">
          {course.attendanceEntered
            ? `Toplam devamsızlık: ${course.totalAbsenceHours ?? 0} saat.`
            : course.attendanceStatusLabel}
        </AlertDescription>
      </Alert>
    </div>
  );
}

function SummaryCell({ label, value }: { label: string; value: string }) {
  return (
    <div className="min-w-0 bg-card px-5 py-4">
      <p className="truncate text-xs font-semibold uppercase tracking-wider text-muted-foreground">{label}</p>
      <p className="mt-1 truncate font-heading text-lg font-extrabold text-foreground">{value}</p>
    </div>
  );
}

function InfoRow({ label, value }: { label: string; value: string }) {
  return (
    <div className="grid grid-cols-[140px_1fr] border-b border-slate-200 last:border-b-0">
      <div className="bg-slate-50 px-3 py-2 font-medium text-slate-700">{label}</div>
      <div className="px-3 py-2 text-slate-900">{value}</div>
    </div>
  );
}

function formatAbsenceStatus(course: StudentAttendanceCourseDto) {
  if (course.totalAbsenceHours == null)
    return course.attendanceStatusLabel;

  return `${course.totalAbsenceHours} Saat`;
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
