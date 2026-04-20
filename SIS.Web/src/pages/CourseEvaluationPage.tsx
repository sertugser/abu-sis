import { useState } from 'react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { CheckCircle2, ClipboardCheck, Clock } from 'lucide-react';
import {
  useActiveEvaluationPeriod,
  useMyEvaluableCourses,
} from '../hooks/useCourseEvaluation';
import EvaluationDialog from '../components/course-evaluation/EvaluationDialog';
import type { EvaluableCourse } from '../types/courseEvaluation';
import { formatDate } from '../lib/utils';

export default function CourseEvaluationPage() {
  const { data: periodData, isLoading: periodLoading } = useActiveEvaluationPeriod();
  const isOpen = periodData?.isOpen === true;
  const { data: courses, isLoading: coursesLoading } = useMyEvaluableCourses(isOpen);

  const [selectedCourse, setSelectedCourse] = useState<EvaluableCourse | null>(null);
  const [dialogOpen, setDialogOpen] = useState(false);

  function openDialog(course: EvaluableCourse) {
    setSelectedCourse(course);
    setDialogOpen(true);
  }

  if (periodLoading) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-24 rounded-xl" />
        <Skeleton className="h-40 rounded-xl" />
      </div>
    );
  }

  if (!isOpen || !periodData?.period) {
    return (
      <div className="space-y-6">
        <div>
          <h1 className="font-heading text-xl font-bold">Ders Değerlendirme</h1>
          <p className="text-sm text-muted-foreground">Dönem sonu ders ve öğretim üyesi değerlendirmesi.</p>
        </div>
        <Alert>
          <Clock className="h-4 w-4" />
          <AlertDescription>
            Değerlendirme dönemi şu anda kapalıdır. Dönem sonlarında açılan bu ankette aldığınız dersleri puanlayabilirsiniz.
          </AlertDescription>
        </Alert>
      </div>
    );
  }

  const period = periodData.period;
  const completed = courses?.filter((c) => c.isCompleted).length ?? 0;
  const total = courses?.length ?? 0;

  return (
    <div className="space-y-6">
      <div>
        <h1 className="font-heading text-xl font-bold">Ders Değerlendirme</h1>
        <p className="text-sm text-muted-foreground">
          {period.termName} · Son tarih: <span className="font-medium">{formatDate(period.closesAt)}</span>
        </p>
      </div>

      <Card className="border-l-4 border-l-blue-500">
        <CardContent className="p-5 flex items-center justify-between flex-wrap gap-4">
          <div className="flex items-center gap-3">
            <ClipboardCheck className="h-8 w-8 text-blue-600" />
            <div>
              <p className="text-sm font-semibold">Değerlendirme Durumu</p>
              <p className="text-xs text-muted-foreground">
                {completed}/{total} ders değerlendirildi
              </p>
            </div>
          </div>
          <Badge variant={completed === total && total > 0 ? 'success' : 'secondary'}>
            {completed === total && total > 0 ? 'Tamamlandı' : 'Devam Ediyor'}
          </Badge>
        </CardContent>
      </Card>

      <Alert className="bg-blue-50 border-blue-200">
        <CheckCircle2 className="h-4 w-4 text-blue-600" />
        <AlertDescription className="text-blue-900 text-xs">
          Yanıtlarınız anonim olarak saklanır. Bir dersi birden fazla değerlendiremezsiniz.
        </AlertDescription>
      </Alert>

      {coursesLoading ? (
        <div className="space-y-3">
          {[1, 2, 3].map((i) => (
            <Skeleton key={i} className="h-20 rounded-xl" />
          ))}
        </div>
      ) : !courses || courses.length === 0 ? (
        <div className="text-center py-16">
          <ClipboardCheck className="h-12 w-12 text-muted-foreground/40 mx-auto mb-4" />
          <p className="text-muted-foreground">Değerlendirilebilecek ders bulunamadı.</p>
        </div>
      ) : (
        <div className="space-y-3">
          {courses.map((course) => (
            <CourseRow key={course.courseSectionId} course={course} onEvaluate={openDialog} />
          ))}
        </div>
      )}

      <EvaluationDialog open={dialogOpen} onOpenChange={setDialogOpen} course={selectedCourse} />
    </div>
  );
}

function CourseRow({
  course,
  onEvaluate,
}: {
  course: EvaluableCourse;
  onEvaluate: (course: EvaluableCourse) => void;
}) {
  return (
    <Card>
      <CardContent className="p-4 flex items-center justify-between gap-4 flex-wrap">
        <div className="flex-1 min-w-0 space-y-1">
          <div className="flex items-center gap-2">
            <span className="font-mono text-sm font-semibold text-primary">{course.courseCode}</span>
            <span className="text-sm font-medium">{course.courseName}</span>
          </div>
          <p className="text-xs text-muted-foreground">{course.instructorName}</p>
        </div>

        {course.isCompleted ? (
          <div className="flex items-center gap-2">
            <Badge variant="success" className="gap-1">
              <CheckCircle2 className="h-3 w-3" />
              Tamamlandı
            </Badge>
            {course.submittedAt && (
              <span className="text-xs text-muted-foreground">{formatDate(course.submittedAt)}</span>
            )}
          </div>
        ) : (
          <Button size="sm" onClick={() => onEvaluate(course)}>
            Değerlendir
          </Button>
        )}
      </CardContent>
    </Card>
  );
}
