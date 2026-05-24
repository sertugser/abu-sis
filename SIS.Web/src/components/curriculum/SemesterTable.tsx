import { Badge } from '@/components/ui/badge';
import { Card, CardContent } from '@/components/ui/card';
import { AlertTriangle } from 'lucide-react';
import type { CurriculumCourseStatusDto, CurriculumSemesterDto } from '@/types/curriculum';

function getGradeBadgeVariant(grade: string | null, status: string | null): 'success' | 'destructive' | 'secondary' | 'outline' {
  if (!grade || !status) return 'outline';
  if (status === 'current') return 'secondary';
  if (status === 'passed') return 'success';
  if (status === 'failed') return 'destructive';
  return 'outline';
}

export function SemesterTable({ semester }: { semester: CurriculumSemesterDto }) {
  return (
    <Card className="overflow-hidden print-no-break">
      <CardContent className="p-0">
        {/* Semester header */}
        <div className="bg-primary px-4 py-2">
          <h3 className="text-sm font-bold text-primary-foreground">{semester.label}</h3>
        </div>

        {/* Table */}
        <div className="overflow-x-auto">
          <table className="w-full text-sm table-fixed print-table-auto">
            <colgroup>
              {/* Left side — 49.8% */}
              <col style={{ width: '8%' }} />
              <col style={{ width: '28.8%' }} />
              <col style={{ width: '4%' }} />
              <col style={{ width: '4%' }} />
              <col style={{ width: '5%' }} />
              {/* Divider — 0.4% */}
              <col style={{ width: '0.4%' }} />
              {/* Right side — 49.8% */}
              <col style={{ width: '5%' }} />
              <col style={{ width: '8%' }} />
              <col style={{ width: '21.8%' }} />
              <col style={{ width: '3.5%' }} />
              <col style={{ width: '3.5%' }} />
              <col style={{ width: '4%' }} />
              <col style={{ width: '4%' }} />
            </colgroup>
            <thead>
              <tr className="border-b border-border bg-muted/30">
                {/* Left side — curriculum */}
                <th className="text-left px-3 py-2 font-medium text-muted-foreground">Ders Kodu</th>
                <th className="text-left px-3 py-2 font-medium text-muted-foreground">Müfredat Ders Adı</th>
                <th className="text-center px-1 py-2 font-medium text-muted-foreground">Z/S</th>
                <th className="text-center px-1 py-2 font-medium text-muted-foreground">Krd</th>
                <th className="text-center px-1 py-2 font-medium text-muted-foreground">AKTS</th>
                {/* Divider */}
                <th className="bg-primary/20 p-0"></th>
                {/* Right side — taken */}
                <th className="text-left px-2 py-2 font-medium text-muted-foreground">Dönem</th>
                <th className="text-left px-2 py-2 font-medium text-muted-foreground">Ders Kodu</th>
                <th className="text-left px-3 py-2 font-medium text-muted-foreground">Ders Adı</th>
                <th className="text-center px-1 py-2 font-medium text-muted-foreground">Z/S</th>
                <th className="text-center px-1 py-2 font-medium text-muted-foreground">Krd</th>
                <th className="text-center px-1 py-2 font-medium text-muted-foreground">AKTS</th>
                <th className="text-center px-1 py-2 font-medium text-muted-foreground">Harf</th>
              </tr>
            </thead>
            <tbody>
              {semester.courses.map((course, index) => (
                <CourseRow key={course.courseId} course={course} isEven={index % 2 === 0} />
              ))}
            </tbody>
          </table>
        </div>

        {/* Semester summary */}
        <div className="flex flex-wrap gap-x-4 gap-y-1 px-4 py-2 border-t border-border bg-muted/20 text-xs text-muted-foreground">
          <span>Kredi: {semester.totalCredits}</span>
          <span>AKTS: {semester.totalEcts}</span>
          <span>Ders: {semester.courseCount}</span>
          <span>Zorunlu: {semester.compulsoryCount}</span>
          <span>Seçmeli: {semester.electiveCount}</span>
        </div>
      </CardContent>
    </Card>
  );
}

function CourseRow({ course, isEven }: { course: CurriculumCourseStatusDto; isEven: boolean }) {
  const hasPrereqWarning = course.prerequisites.length > 0 && !course.prerequisitesMet;

  return (
    <tr className={`border-b border-border/50 hover:bg-muted/30 transition-colors ${isEven ? 'bg-muted/10' : ''}`}>
      {/* Left side — curriculum */}
      <td className="px-3 py-2 font-mono text-sm font-bold text-foreground whitespace-nowrap">
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
        <span className={`text-xs font-bold uppercase ${course.isCompulsory ? 'text-primary' : 'text-accent'}`}>
          {course.isCompulsory ? 'Z' : 'S'}
        </span>
      </td>
      <td className="text-center px-2 py-2 font-mono text-sm">{course.credits}</td>
      <td className="text-center px-2 py-2 font-mono text-sm">{course.ects}</td>

      {/* Divider */}
      <td className="bg-primary/20 p-0"></td>

      {/* Right side — taken */}
      {course.takenTermLabel ? (
        <>
          <td className="px-3 py-2 text-xs text-muted-foreground whitespace-nowrap">[{course.takenTermLabel}]</td>
          <td className="px-3 py-2 font-mono text-sm font-medium">{course.takenCourseCode}</td>
          <td className="px-3 py-2 text-sm">{course.takenCourseName}</td>
          <td className="text-center px-2 py-2">
            <span className={`text-xs font-bold uppercase ${course.takenIsCompulsory ? 'text-primary' : 'text-accent'}`}>
              {course.takenIsCompulsory ? 'Z' : 'S'}
            </span>
          </td>
          <td className="text-center px-2 py-2 font-mono text-sm">{course.takenCredits}</td>
          <td className="text-center px-2 py-2 font-mono text-sm">{course.takenEcts}</td>
          <td className="text-center px-2 py-2">
            {course.grade && (
              <Badge
                variant={getGradeBadgeVariant(course.grade, course.gradeStatus)}
                className={`w-10 justify-center text-xs font-bold ${
                  course.gradeStatus === 'passed'
                    ? 'bg-green-600/15 text-foreground border-green-600/30'
                    : course.gradeStatus === 'failed'
                      ? 'bg-red-600/15 text-foreground border-red-600/30'
                      : ''
                }`}
              >
                {course.grade}
              </Badge>
            )}
          </td>
        </>
      ) : (
        <td className="px-3 py-2" colSpan={7}>
          <span className="text-sm text-muted-foreground italic">Henüz alınmadı</span>
        </td>
      )}
    </tr>
  );
}
