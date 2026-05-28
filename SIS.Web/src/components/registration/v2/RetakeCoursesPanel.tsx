import { Loader2, RotateCcw, ShieldCheck } from 'lucide-react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import type { CatalogCourseRow } from '../../../types/registrationCatalog';

interface Props {
  retakeCourses: CatalogCourseRow[];
  onCheck: () => void;
  checkLoading: boolean;
  validationSummary?: { errorCount: number; warningCount: number } | null;
}

export default function RetakeCoursesPanel({
  retakeCourses,
  onCheck,
  checkLoading,
  validationSummary,
}: Props) {
  const alertVariant =
    validationSummary == null
      ? null
      : validationSummary.errorCount > 0
        ? 'destructive'
        : validationSummary.warningCount > 0
          ? 'warning'
          : 'success';

  return (
    <Card className="border-destructive/40 bg-destructive/5">
      <CardHeader className="pb-2">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            <RotateCcw className="h-4 w-4 text-destructive" />
            <CardTitle className="text-sm text-destructive">
              Tekrar Edilmesi Gereken Dersler
            </CardTitle>
          </div>
          <Button
            size="sm"
            variant="outline"
            onClick={onCheck}
            disabled={checkLoading}
          >
            {checkLoading ? (
              <Loader2 className="h-4 w-4 animate-spin" />
            ) : (
              <ShieldCheck className="h-4 w-4" />
            )}
            Kontrol Et
          </Button>
        </div>
      </CardHeader>

      <CardContent className="space-y-3">
        {retakeCourses.length === 0 ? (
          <p className="text-sm text-muted-foreground">
            Tekrar alınması gereken ders yok.
          </p>
        ) : (
          <ul className="space-y-1">
            {retakeCourses.map(course => (
              <li key={course.courseId} className="font-mono text-xs text-foreground">
                {course.code} {course.name}
                {' '}
                <span className="text-muted-foreground">
                  ({course.previousAttemptInfo ?? '—'})
                </span>
              </li>
            ))}
          </ul>
        )}

        {validationSummary != null && alertVariant != null && (
          <Alert variant={alertVariant}>
            <AlertDescription>
              {validationSummary.errorCount} hata · {validationSummary.warningCount} uyarı
            </AlertDescription>
          </Alert>
        )}
      </CardContent>
    </Card>
  );
}
