import { useRef, useState } from 'react';
import { Skeleton } from '@/components/ui/skeleton';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { useCurriculumStatus } from '../hooks/useCurriculumStatus';
import { AlertCircle, Printer, BookX, AlertTriangle, Search } from 'lucide-react';
import type { CurriculumCourseStatusDto, CurriculumSemesterDto } from '../types/curriculum';


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
  const [searchTerm, setSearchTerm] = useState('');

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
        <AlertDescription>Müfredat bilgisi yüklenirken hata oluştu.</AlertDescription>
      </Alert>
    );
  }

  const handlePrint = () => {
    window.print();
  };

  const allCourses = data.semesters.flatMap(semester => semester.courses);
  const failedCourseCount = allCourses.filter(course => course.gradeStatus === 'failed').length;
  const currentCourseCount = allCourses.filter(course => course.gradeStatus === 'current').length;
  const filteredSemesters = data.semesters
    .map(semester => ({
      ...semester,
      courses: semester.courses.filter(course => {
        if (!searchTerm.trim()) return true;
        const normalizedSearch = searchTerm.toLowerCase();
        return `${course.code} ${course.name} ${course.takenCourseCode ?? ''} ${course.takenCourseName ?? ''}`.toLowerCase().includes(normalizedSearch);
      }),
    }))
    .filter(semester => semester.courses.length > 0);

  return (
    <div className="space-y-6" ref={printRef}>
      {/* Header */}
      <div className="flex flex-col sm:flex-row sm:items-start sm:justify-between gap-4">
        <div>
          <h1 className="font-heading text-xl font-extrabold text-foreground tracking-tight">
            Müfredat Durumu
          </h1>
        </div>
        <Button variant="outline" size="sm" onClick={handlePrint} className="gap-2 print:hidden">
          <Printer className="h-4 w-4" />
          Müfredat Durum Yazdır
        </Button>
      </div>

      {/* Summary Cards */}
      <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-6 gap-3">
        <SummaryCard label="Genel Ort." value={data.cgpa.toFixed(2)} highlight={data.cgpa < 2.0 ? 'destructive' : undefined} />
        <SummaryCard label="Toplam Kredi" value={data.totalCredits} />
        <SummaryCard label="Toplam AKTS" value={data.totalEcts} />
        <SummaryCard label="Başarılı Kredi" value={data.passedCredits} highlight="success" />
        <SummaryCard label="Başarısız Ders" value={failedCourseCount} highlight={failedCourseCount > 0 ? 'destructive' : undefined} />
        <SummaryCard label="Devam Eden" value={currentCourseCount} />
      </div>

      <Card className="print:hidden">
        <CardContent className="p-4">
          <div className="relative">
            <Search className="h-4 w-4 text-muted-foreground absolute left-3 top-1/2 -translate-y-1/2" />
            <Input
              value={searchTerm}
              onChange={event => setSearchTerm(event.target.value)}
              placeholder="Ders kodu veya ders adı ile filtrele"
              className="pl-9"
            />
          </div>
        </CardContent>
      </Card>

      {/* Semester Tables */}
      {filteredSemesters.map(sem => (
        <SemesterTable key={sem.semesterNumber} semester={sem} />
      ))}

      {/* Empty semesters not in curriculum */}
      {filteredSemesters.length === 0 && (
        <div className="text-center py-16">
          <BookX className="h-12 w-12 text-muted-foreground/40 mx-auto mb-4" />
          <h3 className="font-heading text-lg font-semibold text-foreground">
            {data.semesters.length === 0 ? 'Müfredat bulunamadı' : 'Filtreye uyan ders bulunamadı'}
          </h3>
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
                <th className="text-left px-3 py-2 font-medium text-muted-foreground">Müfredat Ders Adı</th>
                <th className="text-center px-2 py-2 font-medium text-muted-foreground w-10">Z</th>
                <th className="text-center px-2 py-2 font-medium text-muted-foreground w-10">Krd</th>
                <th className="text-center px-2 py-2 font-medium text-muted-foreground w-14">AKTS</th>
                {/* Divider */}
                <th className="w-px bg-border"></th>
                {/* Right side — taken */}
                <th className="text-left px-3 py-2 font-medium text-muted-foreground w-20">Dönemi</th>
                <th className="text-left px-3 py-2 font-medium text-muted-foreground w-24">Ders Kodu</th>
                <th className="text-left px-3 py-2 font-medium text-muted-foreground">Ders Adı</th>
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
          Müfredat Toplam Kredi:{semester.totalCredits} AKTS:{semester.totalEcts} Ders
          Say:{semester.courseCount} Zorunlu:{semester.compulsoryCount} Seçmeli:{semester.electiveCount}
        </div>
      </CardContent>
    </Card>
  );
}

function CourseRow({ course }: { course: CurriculumCourseStatusDto }) {
  const hasPrereqWarning = course.prerequisites.length > 0 && !course.prerequisitesMet;

  return (
    <tr className="border-b border-border/50 hover:bg-muted/20 transition-colors">
      {/* Left side — curriculum */}
      <td className="px-3 py-2 font-mono text-sm font-semibold text-primary whitespace-nowrap">
        <span className="flex items-center gap-1.5">
          {course.code}
          {hasPrereqWarning && (
            <span title={`Ön Koşul: ${course.prerequisites.join(', ')}`}>
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
            <span className="text-xs text-muted-foreground/50 italic">Henüz alınmadı</span>
          </td>
        </>
      )}
    </tr>
  );
}
