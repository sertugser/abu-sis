import { Loader2, ShieldCheck } from 'lucide-react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
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

  const courseList = retakeCourses
    .map(c => c.code)
    .join(' · ');

  return (
    <div>
      <div className="bg-destructive/5 border-y border-destructive/30 px-3 py-2 flex items-center justify-between gap-4">
        <div className="text-xs min-w-0">
          <span className="font-semibold text-destructive">Tekrar Edilmesi Gereken Dersler: </span>
          {retakeCourses.length === 0 ? (
            <span className="text-muted-foreground">Yok</span>
          ) : (
            <span className="font-mono text-foreground">{courseList}</span>
          )}
        </div>
        <Button
          size="sm"
          variant="outline"
          onClick={onCheck}
          disabled={checkLoading}
          className="flex-shrink-0 gap-1"
        >
          {checkLoading ? (
            <Loader2 className="h-3.5 w-3.5 animate-spin" />
          ) : (
            <ShieldCheck className="h-3.5 w-3.5" />
          )}
          Kontrol Et
        </Button>
      </div>

      {validationSummary != null && alertVariant != null && (
        <Alert variant={alertVariant} className="rounded-none border-x-0 border-t-0">
          <AlertDescription>
            {validationSummary.errorCount} hata · {validationSummary.warningCount} uyarı
          </AlertDescription>
        </Alert>
      )}
    </div>
  );
}
