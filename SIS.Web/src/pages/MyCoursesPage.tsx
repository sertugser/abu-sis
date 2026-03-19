import { useState } from 'react';
import { Skeleton } from '@/components/ui/skeleton';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent } from '@/components/ui/card';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table';
import { useCourseHistory } from '../hooks/useCourseHistory';
import { AlertCircle, GraduationCap, ChevronDown } from 'lucide-react';
import { cn } from '@/lib/utils';

const GRADE_COLORS: Record<string, string> = {
  AA: 'bg-success text-success-foreground',
  BA: 'bg-success text-success-foreground',
  BB: 'bg-success/80 text-success-foreground',
  CB: 'bg-accent/20 text-accent-foreground',
  CC: 'bg-accent/20 text-accent-foreground',
  DC: 'bg-warning/20 text-warning-foreground',
  DD: 'bg-warning/20 text-warning-foreground',
  FF: 'bg-destructive text-destructive-foreground',
  NA: 'bg-destructive text-destructive-foreground',
  W: 'bg-muted text-muted-foreground',
  I: 'bg-muted text-muted-foreground',
  S: 'bg-success text-success-foreground',
  U: 'bg-destructive text-destructive-foreground',
  NI: 'bg-muted text-muted-foreground',
};

export default function MyCoursesPage() {
  const { data: history, isLoading, error } = useCourseHistory();
  const [selectedTermId, setSelectedTermId] = useState<number | 'all'>('all');

  if (isLoading) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-10 w-64" />
        <Skeleton className="h-64 rounded-xl" />
      </div>
    );
  }

  if (error) {
    return (
      <Alert variant="destructive">
        <AlertCircle className="h-4 w-4" />
        <AlertDescription>Ders gecmisi yuklenirken hata olustu.</AlertDescription>
      </Alert>
    );
  }

  if (!history || history.length === 0) {
    return (
      <div className="text-center py-16">
        <GraduationCap className="h-12 w-12 text-muted-foreground/40 mx-auto mb-4" />
        <h3 className="font-heading text-lg font-semibold text-foreground">Ders gecmisi bulunamadi</h3>
        <p className="text-muted-foreground mt-1">Henuz tamamlanmis bir doneminiz bulunmamaktadir.</p>
      </div>
    );
  }

  const filteredTerms = selectedTermId === 'all'
    ? history
    : history.filter(t => t.termId === selectedTermId);

  const totalEcts = filteredTerms.reduce((sum, t) => sum + t.courses.reduce((s, c) => s + c.ects, 0), 0);
  const totalCourses = filteredTerms.reduce((sum, t) => sum + t.courses.length, 0);

  return (
    <div className="space-y-6">
      {/* Term filter */}
      <div className="flex items-center gap-4 flex-wrap">
        <div className="relative">
          <select
            value={selectedTermId}
            onChange={e => setSelectedTermId(e.target.value === 'all' ? 'all' : Number(e.target.value))}
            className="appearance-none bg-card border border-border rounded-lg px-4 py-2 pr-10 text-sm font-medium text-foreground focus:outline-none focus:ring-2 focus:ring-ring cursor-pointer"
          >
            <option value="all">Tum Donemler</option>
            {history.map(t => (
              <option key={t.termId} value={t.termId}>{t.termName}</option>
            ))}
          </select>
          <ChevronDown className="absolute right-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground pointer-events-none" />
        </div>
        <div className="flex gap-3 text-sm text-muted-foreground">
          <span>{totalCourses} ders</span>
          <span>|</span>
          <span>{totalEcts} ECTS</span>
        </div>
      </div>

      {/* Term blocks */}
      {filteredTerms.map(term => {
        const termEcts = term.courses.reduce((s, c) => s + c.ects, 0);
        return (
          <Card key={term.termId}>
            <CardContent className="p-0">
              {/* Term header */}
              <div className="flex items-center justify-between px-5 py-3 bg-muted/50 border-b border-border">
                <h3 className="font-heading text-sm font-semibold text-foreground">{term.termName}</h3>
                <div className="flex gap-3">
                  <Badge variant="secondary">{term.courses.length} ders</Badge>
                  <Badge variant="default">{termEcts} ECTS</Badge>
                </div>
              </div>

              {/* Courses table */}
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead className="w-28">Ders Kodu</TableHead>
                    <TableHead>Ders Adi</TableHead>
                    <TableHead className="text-center w-20">ECTS</TableHead>
                    <TableHead className="hidden md:table-cell">Ogretim Uyesi</TableHead>
                    <TableHead className="text-center w-20">Not</TableHead>
                    <TableHead className="text-center w-24 hidden sm:table-cell">Durum</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {term.courses.map(course => (
                    <TableRow key={course.registrationId}>
                      <TableCell className="font-mono text-sm font-semibold text-primary">
                        {course.courseCode}
                        {course.isRepeated && (
                          <span className="ml-1 text-[10px] text-warning font-normal">(T)</span>
                        )}
                      </TableCell>
                      <TableCell className="font-medium">{course.courseName}</TableCell>
                      <TableCell className="text-center">
                        <Badge variant="secondary">{course.ects}</Badge>
                      </TableCell>
                      <TableCell className="hidden md:table-cell text-muted-foreground text-sm">
                        {course.instructorName || '—'}
                      </TableCell>
                      <TableCell className="text-center">
                        {course.grade ? (
                          <Badge className={cn('font-mono font-bold', GRADE_COLORS[course.grade] || 'bg-muted text-muted-foreground')}>
                            {course.grade}
                          </Badge>
                        ) : (
                          <span className="text-xs text-muted-foreground">—</span>
                        )}
                      </TableCell>
                      <TableCell className="text-center hidden sm:table-cell">
                        <StatusBadge status={course.status} />
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </CardContent>
          </Card>
        );
      })}
    </div>
  );
}

function StatusBadge({ status }: { status: string }) {
  const map: Record<string, { label: string; variant: 'success' | 'warning' | 'destructive' | 'secondary' | 'outline' }> = {
    Active: { label: 'Aktif', variant: 'success' },
    PendingAdvisorApproval: { label: 'Onay Bekliyor', variant: 'warning' },
    Withdrawn: { label: 'Cekildi', variant: 'outline' },
  };
  const entry = map[status] || { label: status, variant: 'secondary' as const };
  return <Badge variant={entry.variant} className="text-[11px]">{entry.label}</Badge>;
}
