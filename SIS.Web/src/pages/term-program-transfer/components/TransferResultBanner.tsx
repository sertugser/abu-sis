import { AlertCircle, CheckCircle2, XCircle, AlertTriangle } from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import type { TransferFromTermResultDto } from '../../../types/courseOpening';

interface TransferResultBannerProps {
  result: TransferFromTermResultDto;
}

export function TransferResultBanner({ result }: TransferResultBannerProps) {
  const hasFailure = result.failedCount > 0;
  const warningEntries = result.results.filter((r) => r.success && r.warnings && r.warnings.length > 0);
  return (
    <Alert variant={hasFailure ? 'destructive' : 'default'}>
      {hasFailure ? <AlertCircle className="h-4 w-4" /> : <CheckCircle2 className="h-4 w-4" />}
      <AlertTitle>{hasFailure ? 'Aktarım kısmen tamamlandı' : 'Aktarım tamamlandı'}</AlertTitle>
      <AlertDescription className="space-y-1">
        <p>
          <strong>{result.successCount}</strong> ders başarıyla aktarıldı ({result.totalSectionsCreated} şube oluşturuldu).
          {hasFailure && (
            <span className="text-destructive">
              {' '}
              <strong>{result.failedCount}</strong> ders aktarılamadı.
            </span>
          )}
        </p>
        {result.results
          .filter((r) => !r.success)
          .map((r) => (
            <div key={`fail-${r.courseId}`} className="flex items-start gap-1.5 text-sm mt-1 text-destructive">
              <XCircle className="h-3.5 w-3.5 mt-0.5 shrink-0" />
              <span className="flex flex-wrap items-center gap-1.5">
                <strong>
                  {r.courseCode} — {r.courseName}:
                </strong>
                <span>{r.error}</span>
                {r.incidentId && (
                  <code
                    className="text-[10px] font-mono bg-destructive/10 border border-destructive/30 rounded px-1.5 py-0.5"
                    title="Sistem yöneticisine bildirirken bu olay numarasını paylaşın. Sunucu loglarında aynı numara ile bulunur."
                  >
                    Olay: {r.incidentId}
                  </code>
                )}
              </span>
            </div>
          ))}
        {warningEntries.length > 0 && (
          <div className="mt-2 pt-2 border-t border-border/40 space-y-1">
            <div className="flex items-center gap-1.5 text-xs font-medium text-amber-600">
              <AlertTriangle className="h-3.5 w-3.5" />
              Schedule uyarıları ({warningEntries.reduce((sum, e) => sum + e.warnings.length, 0)}):
            </div>
            {warningEntries.map((r) => (
              <div key={`warn-${r.courseId}`} className="text-xs space-y-0.5 pl-5">
                <div className="font-mono font-medium">
                  {r.courseCode} — {r.courseName}:
                </div>
                {r.warnings.map((w, idx) => (
                  <div key={idx} className="text-muted-foreground">
                    • {w}
                  </div>
                ))}
              </div>
            ))}
          </div>
        )}
      </AlertDescription>
    </Alert>
  );
}
