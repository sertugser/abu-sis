import { useRef } from 'react';
import { Skeleton } from '@/components/ui/skeleton';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Separator } from '@/components/ui/separator';
import { useCurriculumStatus } from '../hooks/useCurriculumStatus';
import { AlertCircle, Printer, BookX, AlertTriangle } from 'lucide-react';
import type { CurriculumCourseStatusDto, CurriculumSemesterDto } from '../types/curriculum';

const GRADE_COLORS: Record<string, string> = {
  AA: 'text-success', BA: 'text-success', BB: 'text-success',
  CB: 'text-primary', CC: 'text-primary',
  DC: 'text-warning', DD: 'text-warning',
  FF: 'text-destructive', NA: 'text-destructive', U: 'text-destructive',
  S: 'text-success',
  '--': 'text-muted-foreground',
};

function getGradeBadgeVariant(grade: string | null, status: string | null): 'success' | 'warning' | 'destructive' | 'secondary' | 'outline' {
  if (!grade || !status) return 'outline';
  if (status === 'current') return 'secondary';
  if (status === 'passed') return 'success';
  if (status === 'failed') return 'destructive';
  return 'outline';
}

export default function CurriculumStatusPage() {
  const { data, isLoading, error } = useCurriculumStatus();
  const printRef = useRef<HTMLDivElement>(null);

  if (isLoading) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-24 rounded-xl" />
        <Skeleton className="h-8 w-64" />
        {[1, 2, 3].map(i => <Skeleton key={i} className="h-40 rounded-xl" />)}
      </div>
    );
  }

  if (error || !data) {
    return (
      <Alert variant="destructive">
        <AlertCircle className="h-4 w-4" />
        <AlertDescription>Mufredat bilgisi yuklenirken hata olustu.</AlertDescription>
      </Alert>
    );
  }

  const handlePrint = () => {
    window.print();
  };

  return (
    <div className="space-y-6" ref={printRef}>
      {/* Header */}
      <div className="flex flex-col sm:flex-row sm:items-start sm:justify-between gap-4">
        <div>
          <h1 className="font-heading text-xl font-extrabold text-foreground tracking-tight">
            Mufredat Durumu
          </h1>
          <p className="text-sm text-muted-foreground mt-1">
            {data.studentName} — {data.programName} Mufredat {data.curriculumVersion}
          </p>
        </div>
        <Button variant="outline" size="sm" onClick={handlePrint} className="gap-2 print:hidden">
          <Printer className="h-4 w-4" />
          Mufredat Durum Yazdir
        </Button>
      </div>

      {/* Summary Cards */}
      <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
        <SummaryCard label="Genel Kredi" value={data.totalCredits} />
        <SummaryCard label="Genel AKTS" value={data.totalEcts} />
        <SummaryCard label="Genel Ort." value={data.cgpa.toFixed(2)} highlight={data.cgpa < 2.0 ? 'destructive' : undefined} />
        <SummaryCard label="Basarili AKTS" value={data.passedEcts} highlight="success" />
      </div>
      <div className="grid grid-cols-2 gap-3">
        <SummaryCard label="Basarili Olunan Kredi" value={data.passedCredits} highlight="success" />
        <SummaryCard label="Basarili Olunan AKTS" value={data.passedEcts} highlight="success" />
      </div>

      {/* Semester Tables */}
      {data.semesters.map(sem => (
        <SemesterTable key={sem.semesterNumber} semester={sem} />
      ))}

      {/* Empty semesters not in curriculum */}
      {data.semesters.length === 0 && (
        <div className="text-center py-16">
          <BookX className="h-12 w-12 text-muted-foreground/40 mx-auto mb-4" />
          <h3 className="font-heading text-lg font-semibold text-foreground">Mufredat bulunamadi</h3>
        </div>
      )}
    </div>
  );
}

function SummaryCard({ label, value, highlight }: {
  label: string;
  value: string | number;
  highlight?: 'success' | 'destructive';
}) {
  const valueColor = highlight === 'success' ? 'text-success' : highlight === 'destructive' ? 'text-destructive' : 'text-foreground';
  return (
    <Card>
      <CardContent className="p-4">
        <p className="text-xs font-medium text-muted-foreground uppercase tracking-wider">{label}</p>
        <p className={`text-xl font-bold ${valueColor} mt-1`}>{value}</p>
      </CardContent>
    </Card>
  );
}

function SemesterTable({ semester }: { semester: CurriculumSemesterDto }) {
  return (
    <Card className="overflow-hidden">
      <CardContent className="p-0">
        {/* Semester header */}
        <div className="bg-primary px-4 py-2">
          <h3 className="text-sm font-bold text-primary-foreground">{semester.label}</h3>
        </div>

        {/* Table */}
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-border bg-muted/30">
                {/* Left side — curriculum */}
                <th className="text-left px-3 py-2 font-medium text-muted-foreground w-24">Ders Kodu</th>
                <th className="text-left px-3 py-2 font-medium text-muted-foreground">Mufredat Ders Adi</th>
                <th className="text-center px-2 py-2 font-medium text-muted-foreground w-10">Z</th>
                <th className="text-center px-2 py-2 font-medium text-muted-foreground w-10">Krd</th>
                <th className="text-center px-2 py-2 font-medium text-muted-foreground w-14">AKTS</th>
                {/* Divider */}
                <th className="w-px bg-border"></th>
                {/* Right side — taken */}
                <th className="text-left px-3 py-2 font-medium text-muted-foreground w-20">Donemi</th>
                <th className="text-left px-3 py-2 font-medium text-muted-foreground w-24">Ders Kodu</th>
                <th className="text-left px-3 py-2 font-medium text-muted-foreground">Ders Adi</th>
                <th className="text-center px-2 py-2 font-medium text-muted-foreground w-10">Z/S</th>
                <th className="text-center px-2 py-2 font-medium text-muted-foreground w-10">Krd</th>
                <th className="text-center px-2 py-2 font-medium text-muted-foreground w-14">AKTS</th>
                <th className="text-center px-2 py-2 font-medium text-muted-foreground w-14">Harf</th>
              </tr>
            </thead>
            <tbody>
              {semester.courses.map(course => (
                <CourseRow key={course.courseId} course={course} />
              ))}
            </tbody>
          </table>
        </div>

        {/* Semester summary */}
        <div className="px-4 py-2 border-t border-border bg-muted/20 text-xs text-muted-foreground">
          Mufredat Toplam Kredi:{semester.totalCredits} AKTS:{semester.totalEcts} Ders
          Say:{semester.courseCount} Zorunlu:{semester.compulsoryCount} Secmeli:{semester.electiveCount}
        </div>
      </CardContent>
    </Card>
  );
}

function CourseRow({ course }: { course: CurriculumCourseStatusDto }) {
  const hasPrereqWarning = course.prerequisites.length > 0 && !course.prerequisitesMet;
  const gradeColor = course.grade ? (GRADE_COLORS[course.grade] || 'text-foreground') : '';

  return (
    <tr className="border-b border-border/50 hover:bg-muted/20 transition-colors">
      {/* Left side — curriculum */}
      <td className="px-3 py-2 font-mono text-sm font-semibold text-primary whitespace-nowrap">
        <span className="flex items-center gap-1.5">
          {course.code}
          {hasPrereqWarning && (
            <span title={`On Kosul: ${course.prerequisites.join(', ')}`}>
              <AlertTriangle className="h-3.5 w-3.5 text-destructive" />
            </span>
          )}
        </span>
      </td>
      <td className="px-3 py-2">
        <span className={course.gradeStatus === 'failed' ? 'text-destructive' : 'text-foreground'}>
          {course.name}
        </span>
        {hasPrereqWarning && (
          <span className="text-xs text-destructive ml-1">
            ({course.prerequisites.join(', ')})
          </span>
        )}
      </td>
      <td className="text-center px-2 py-2">
        <span className={`text-xs font-bold ${course.isCompulsory ? 'text-primary' : 'text-accent'}`}>
          {course.isCompulsory ? 'Z' : 'S'}
        </span>
      </td>
      <td className="text-center px-2 py-2 font-mono text-sm">{course.credits}</td>
      <td className="text-center px-2 py-2 font-mono text-sm">{course.ects}</td>

      {/* Divider */}
      <td className="w-px bg-border"></td>

      {/* Right side — taken */}
      {course.takenTermLabel ? (
        <>
          <td className="px-3 py-2 text-xs text-muted-foreground whitespace-nowrap">[{course.takenTermLabel}]</td>
          <td className="px-3 py-2 font-mono text-sm font-medium">{course.takenCourseCode}</td>
          <td className="px-3 py-2 text-sm">{course.takenCourseName}</td>
          <td className="text-center px-2 py-2">
            <span className={`text-xs font-bold ${course.takenIsCompulsory ? 'text-primary' : 'text-accent'}`}>
              {course.takenIsCompulsory ? 'Z' : 'S'}
            </span>
          </td>
          <td className="text-center px-2 py-2 font-mono text-sm">{course.takenCredits}</td>
          <td className="text-center px-2 py-2 font-mono text-sm">{course.takenEcts}</td>
          <td className="text-center px-2 py-2">
            {course.grade && (
              <Badge variant={getGradeBadgeVariant(course.grade, course.gradeStatus)} className="text-xs font-bold">
                {course.grade}
              </Badge>
            )}
          </td>
        </>
      ) : (
        <>
          <td className="px-3 py-2" colSpan={7}>
            <span className="text-xs text-muted-foreground/50 italic">Henuz alinmadi</span>
          </td>
        </>
      )}
    </tr>
  );
}
