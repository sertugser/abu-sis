import { Calculator, Loader2 } from 'lucide-react';
import { StepAccordion, ConfirmButton, MutationFeedback, StatPill, PrerequisiteAlert } from './shared';
import { useCalculateGpa, useGradeDistribution } from '@/hooks/useTermClosing';
import { cn } from '@/lib/utils';

// Grade → color mapping for bar chart
const GRADE_COLORS: Record<string, string> = {
  AA: 'bg-emerald-500', BA: 'bg-emerald-400', BB: 'bg-teal-400',
  CB: 'bg-amber-400', CC: 'bg-amber-500',
  DC: 'bg-orange-400', DD: 'bg-orange-500',
  FF: 'bg-red-500', NA: 'bg-red-400',
  W: 'bg-gray-400', I: 'bg-gray-300', NI: 'bg-gray-300',
  S: 'bg-emerald-300', U: 'bg-red-300',
};

interface GpaStepProps {
  departmentId: number;
  isCompleted: boolean;
  isActive: boolean;
  step1Completed: boolean;
  completedAt?: string | null;
  completedBy?: string | null;
  completedNotes?: string | null;
  onStepDone: () => void;
}

export function GpaStep({
  departmentId, isCompleted, isActive, step1Completed,
  completedAt, completedBy, completedNotes,
  onStepDone,
}: GpaStepProps) {
  const gpaMutation = useCalculateGpa(departmentId);
  const distQuery = useGradeDistribution(departmentId, isCompleted || gpaMutation.isSuccess);
  const dist = distQuery.data;
  const result = gpaMutation.data;

  const handleCalc = async () => {
    try {
      await gpaMutation.mutateAsync();
      onStepDone();
    } catch { /* feedback below */ }
  };

  const isLocked = !step1Completed && !isCompleted;

  return (
    <StepAccordion
      stepNum={2}
      title="GPA Hesaplama"
      description="Tum ogrencilerin donem (ANO) ve genel (AGNO) ortalamasini yeniden hesaplayin."
      icon={Calculator}
      isCompleted={isCompleted}
      isActive={isActive}
      isLocked={isLocked}
      lockReason="Once Not Kilitleme tamamlanmalidir"
      completedSummary={completedNotes ?? undefined}
      completedAt={completedAt}
      completedBy={completedBy}
      onToggle={() => {}}
    >
      {isLocked && <PrerequisiteAlert stepName="Not Kilitleme (Adim 1)" />}

      {/* Pre-calculation state */}
      {!isCompleted && step1Completed && !gpaMutation.isSuccess && (
        <>
          {gpaMutation.isPending ? (
            <div className="flex items-center gap-3 py-6 justify-center">
              <Loader2 className="h-5 w-5 animate-spin text-primary" />
              <div>
                <p className="text-sm font-medium">GPA hesaplaniyor...</p>
                <p className="text-xs text-muted-foreground">Ogrenci sayisina gore birkac dakika surebilir.</p>
              </div>
            </div>
          ) : (
            <ConfirmButton
              label="GPA Hesapla"
              icon={Calculator}
              isLoading={false}
              title="GPA hesaplamasini baslatmak istiyor musunuz?"
              description="Tum ogrencilerin ANO ve AGNO degerleri yeniden hesaplanacaktir."
              onConfirm={handleCalc}
            />
          )}
        </>
      )}

      {/* Result stats */}
      {result && (
        <div className="flex flex-wrap gap-2">
          <StatPill label="Toplam Ogrenci" value={result.totalStudents} />
          <StatPill label="Hesaplanan" value={result.recalculatedCount} variant="success" />
          <StatPill label="GPA Degisen" value={result.changedCount} variant={result.changedCount > 0 ? 'warning' : 'success'} />
        </div>
      )}

      {/* Completed without fresh result */}
      {isCompleted && !result && (
        <div className="flex flex-wrap gap-2">
          <StatPill label="Durum" value="Tamamlandi" variant="success" />
        </div>
      )}

      {/* Grade distribution chart */}
      {dist && dist.grades.length > 0 && (
        <div className="space-y-3">
          <div className="flex items-center justify-between">
            <h4 className="text-xs font-semibold text-muted-foreground uppercase tracking-wider">Not Dagilimi</h4>
            <div className="flex items-center gap-4 text-xs">
              <span className="text-muted-foreground">
                Toplam: <strong className="text-foreground">{dist.totalGraded}</strong>
              </span>
              <span className="text-muted-foreground">
                Gecme Orani: <strong className={cn(
                  dist.passRate >= 70 ? 'text-emerald-600' : dist.passRate >= 50 ? 'text-amber-600' : 'text-red-600',
                )}>%{dist.passRate}</strong>
              </span>
              <span className="text-muted-foreground">
                Ort. Puan: <strong className="text-foreground">{dist.averageScore.toFixed(2)}</strong>
              </span>
            </div>
          </div>

          {/* Horizontal bar chart — pure CSS */}
          <div className="space-y-1.5">
            {dist.grades.map(g => {
              const maxPct = Math.max(...dist.grades.map(x => x.percentage));
              const barWidth = maxPct > 0 ? (g.percentage / maxPct) * 100 : 0;

              return (
                <div key={g.gradeCode} className="flex items-center gap-2 group">
                  <span className="w-7 text-right text-xs font-mono font-semibold text-muted-foreground flex-shrink-0">
                    {g.gradeCode}
                  </span>
                  <div className="flex-1 h-6 rounded bg-muted/30 relative overflow-hidden">
                    <div
                      className={cn(
                        'h-full rounded transition-all duration-500 ease-out',
                        GRADE_COLORS[g.gradeCode] ?? 'bg-gray-400',
                      )}
                      style={{ width: `${barWidth}%` }}
                    />
                  </div>
                  <span className="w-16 text-right text-xs tabular-nums text-muted-foreground flex-shrink-0">
                    <strong className="text-foreground">{g.count}</strong>
                    <span className="ml-1">({g.percentage}%)</span>
                  </span>
                </div>
              );
            })}
          </div>
        </div>
      )}

      <MutationFeedback mutation={gpaMutation} />
    </StepAccordion>
  );
}
