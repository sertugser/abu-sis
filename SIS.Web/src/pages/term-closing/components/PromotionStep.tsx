import { useState, useMemo } from 'react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import { Progress } from '@/components/ui/progress';
import { cn } from '@/lib/utils';
import {
  GraduationCap, Loader2, Search, Download, ChevronDown, ChevronUp,
} from 'lucide-react';
import { StepAccordion, ConfirmButton, MutationFeedback, StatPill, PrerequisiteAlert } from './shared';
import {
  usePromotionPreview, usePromote, useRollbackPromotion, useExportPromotionReport,
} from '@/hooks/useTermClosing';
import type { StudentProgressionDto } from '@/api/termClosing.api';

type FilterTab = 'all' | 'promoted' | 'stayed' | 'warning';

interface PromotionStepProps {
  departmentId: number;
  isCompleted: boolean;
  isActive: boolean;
  step2Completed: boolean;
  completedAt?: string | null;
  completedBy?: string | null;
  completedNotes?: string | null;
  onStepDone: () => void;
}

export function PromotionStep({
  departmentId, isCompleted, isActive, step2Completed,
  completedAt, completedBy, completedNotes,
  onStepDone,
}: PromotionStepProps) {
  const previewQuery = usePromotionPreview(departmentId, !isCompleted && step2Completed);
  const promoteMutation = usePromote(departmentId);
  const rollbackMutation = useRollbackPromotion(departmentId);
  const exportMutation = useExportPromotionReport(departmentId);

  const preview = previewQuery.data;
  const [filter, setFilter] = useState<FilterTab>('all');
  const [search, setSearch] = useState('');
  const [expandedId, setExpandedId] = useState<number | null>(null);

  const isLocked = !step2Completed && !isCompleted;

  const filteredStudents = useMemo(() => {
    let results: StudentProgressionDto[] = preview?.results ?? [];
    if (filter === 'promoted') results = results.filter(s => s.wasPromoted);
    else if (filter === 'stayed') results = results.filter(s => !s.wasPromoted && !s.status.startsWith('Warning'));
    else if (filter === 'warning') results = results.filter(s => s.status.startsWith('Warning'));

    if (search) {
      const q = search.toLowerCase();
      results = results.filter(s =>
        s.fullName.toLowerCase().includes(q) || s.studentNumber.includes(q)
      );
    }
    return results;
  }, [preview?.results, filter, search]);

  const handlePromote = async () => {
    try {
      await promoteMutation.mutateAsync();
      onStepDone();
    } catch { /* feedback below */ }
  };

  const tabs: { key: FilterTab; label: string; count?: number }[] = [
    { key: 'all', label: 'Tumu', count: preview?.totalStudentsAnalyzed },
    { key: 'promoted', label: 'Terfi', count: preview?.promotedCount },
    { key: 'stayed', label: 'Sabit', count: preview?.stayedCount },
    { key: 'warning', label: 'Uyari', count: preview?.warningCount },
  ];

  return (
    <StepAccordion
      stepNum={3}
      title="Sinif Atlatma"
      description="Ogrenci sinif seviyelerini akademik ilerleme kurallarina gore guncelleyin."
      icon={GraduationCap}
      isCompleted={isCompleted}
      isActive={isActive}
      isLocked={isLocked}
      lockReason="Once GPA Hesaplama tamamlanmalidir"
      completedSummary={completedNotes ?? undefined}
      completedAt={completedAt}
      completedBy={completedBy}
      onToggle={() => {}}
      onUndo={isCompleted ? () => rollbackMutation.mutate() : undefined}
      undoLoading={rollbackMutation.isPending}
      undoWarning="Sinif atlatma geri alinacak. Tum ogrencilerin sinif seviyeleri onceki haline donecektir."
    >
      {isLocked && <PrerequisiteAlert stepName="GPA Hesaplama (Adim 2)" />}

      {previewQuery.isLoading && (
        <div className="flex items-center gap-2 py-6 justify-center text-muted-foreground">
          <Loader2 className="h-4 w-4 animate-spin" />
          <span className="text-sm">Onizleme hazirlaniyor...</span>
        </div>
      )}

      {preview && (
        <>
          {/* Stats */}
          <div className="flex flex-wrap gap-2">
            <StatPill label="Toplam Ogrenci" value={preview.totalStudentsAnalyzed} />
            <StatPill label="Yukselecek" value={preview.promotedCount} variant="success" />
            <StatPill label="Kalacak" value={preview.stayedCount} />
            <StatPill label="Uyari" value={preview.warningCount} variant={preview.warningCount > 0 ? 'warning' : 'success'} />
          </div>

          {/* Promotion rate */}
          {preview.totalStudentsAnalyzed > 0 && (
            <div className="space-y-1.5">
              <div className="flex items-center justify-between text-xs">
                <span className="text-muted-foreground">Terfi Orani</span>
                <span className="font-semibold tabular-nums">
                  {Math.round((preview.promotedCount / preview.totalStudentsAnalyzed) * 100)}%
                </span>
              </div>
              <Progress
                value={(preview.promotedCount / preview.totalStudentsAnalyzed) * 100}
                indicatorClassName="bg-emerald-500"
                className="h-2"
              />
            </div>
          )}

          {/* Filter tabs + search */}
          <div className="flex items-center justify-between gap-3 flex-wrap">
            <div className="flex gap-1">
              {tabs.map(t => (
                <button
                  key={t.key}
                  onClick={() => setFilter(t.key)}
                  className={cn(
                    'px-3 py-1.5 rounded-md text-xs font-medium transition-colors',
                    filter === t.key
                      ? 'bg-primary text-white shadow-sm'
                      : 'bg-muted/50 text-muted-foreground hover:bg-muted',
                  )}
                >
                  {t.label}
                  {t.count !== undefined && (
                    <span className={cn(
                      'ml-1.5 tabular-nums',
                      filter === t.key ? 'text-white/80' : 'text-muted-foreground/60',
                    )}>
                      {t.count}
                    </span>
                  )}
                </button>
              ))}
            </div>

            <div className="flex items-center gap-2">
              <div className="relative w-48">
                <Search className="absolute left-2.5 top-1/2 -translate-y-1/2 h-3.5 w-3.5 text-muted-foreground" />
                <Input
                  placeholder="Ogrenci ara..."
                  value={search}
                  onChange={e => setSearch(e.target.value)}
                  className="h-8 pl-8 text-xs"
                />
              </div>
              <Button
                variant="outline"
                size="sm"
                className="gap-1.5 h-8"
                onClick={() => exportMutation.mutate()}
                disabled={exportMutation.isPending}
              >
                <Download className="h-3.5 w-3.5" />
                <span className="text-xs">Excel</span>
              </Button>
            </div>
          </div>

          {/* Student table */}
          <div className="rounded-lg border overflow-hidden">
            <div className="max-h-[400px] overflow-y-auto">
              <table className="w-full text-xs">
                <thead className="bg-muted/40 sticky top-0 z-10">
                  <tr>
                    <th className="px-3 py-2 text-left font-semibold">Ogrenci</th>
                    <th className="px-3 py-2 text-center font-semibold">Eski Sinif</th>
                    <th className="px-3 py-2 text-center font-semibold">Yeni Sinif</th>
                    <th className="px-3 py-2 text-right font-semibold">AKTS</th>
                    <th className="px-3 py-2 text-right font-semibold">ANO</th>
                    <th className="px-3 py-2 text-right font-semibold">AGNO</th>
                    <th className="px-3 py-2 text-center font-semibold">Durum</th>
                    <th className="px-3 py-2 w-8" />
                  </tr>
                </thead>
                <tbody className="divide-y divide-border/50">
                  {filteredStudents.map(row => (
                    <StudentRow
                      key={row.studentId}
                      student={row}
                      isExpanded={expandedId === row.studentId}
                      onToggle={() => setExpandedId(expandedId === row.studentId ? null : row.studentId)}
                    />
                  ))}
                  {filteredStudents.length === 0 && (
                    <tr>
                      <td colSpan={8} className="px-3 py-6 text-center text-muted-foreground">
                        Sonuc bulunamadi
                      </td>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>
          </div>

          {/* Action */}
          {!isCompleted && step2Completed && (
            <ConfirmButton
              label="Sinif Atlatmayi Uygula"
              icon={GraduationCap}
              isLoading={promoteMutation.isPending}
              title="Sinif atlatma islemini uygulamak istiyor musunuz?"
              description={`${preview.promotedCount} ogrenci sinif atlayacak, ${preview.stayedCount} ogrenci kalacak. Bu islem geri alinabilir.`}
              onConfirm={handlePromote}
            />
          )}
        </>
      )}

      <MutationFeedback mutation={promoteMutation} />
      <MutationFeedback mutation={rollbackMutation} successMsg="Sinif atlatma geri alindi. Ogrenci seviyeleri eski haline donduruldu." />
      <MutationFeedback mutation={exportMutation} />
    </StepAccordion>
  );
}

/* ── Student Row (expandable) ── */

function StudentRow({ student: s, isExpanded, onToggle }: {
  student: StudentProgressionDto; isExpanded: boolean; onToggle: () => void;
}) {
  const hasWarnings = s.warnings.length > 0;
  const isWarning = s.status.startsWith('Warning');
  const isRetention = s.cgpa < 1.70;

  return (
    <>
      <tr
        className={cn(
          'hover:bg-muted/20 transition-colors cursor-pointer',
          isRetention && 'bg-red-50/50',
          isWarning && !isRetention && 'bg-amber-50/40',
        )}
        onClick={onToggle}
      >
        <td className="px-3 py-2">
          <p className="font-medium">{s.fullName}</p>
          <p className="text-[10px] text-muted-foreground font-mono">{s.studentNumber}</p>
        </td>
        <td className="px-3 py-2 text-center">{s.oldLevelLabel}</td>
        <td className="px-3 py-2 text-center">
          <span className={cn('font-semibold', s.wasPromoted ? 'text-emerald-600' : 'text-muted-foreground')}>
            {s.newLevelLabel}
          </span>
        </td>
        <td className="px-3 py-2 text-right tabular-nums">{s.completedEcts}</td>
        <td className="px-3 py-2 text-right tabular-nums">{s.sgpa.toFixed(2)}</td>
        <td className="px-3 py-2 text-right">
          <span className={cn(
            'tabular-nums font-medium',
            isRetention ? 'text-red-600' : s.cgpa >= 2.5 ? 'text-emerald-600' : s.cgpa >= 2.0 ? 'text-foreground' : 'text-amber-600',
          )}>
            {s.cgpa.toFixed(2)}
          </span>
        </td>
        <td className="px-3 py-2 text-center">
          {isRetention ? (
            <Badge variant="destructive" className="text-[10px]">Tutulan</Badge>
          ) : isWarning ? (
            <Badge variant="warning" className="text-[10px]">Uyari</Badge>
          ) : s.wasPromoted ? (
            <Badge variant="success" className="text-[10px]">Terfi</Badge>
          ) : (
            <Badge variant="secondary" className="text-[10px]">Sabit</Badge>
          )}
        </td>
        <td className="px-3 py-2 text-center">
          {hasWarnings && (
            isExpanded ? <ChevronUp className="h-3 w-3 text-muted-foreground" /> : <ChevronDown className="h-3 w-3 text-muted-foreground" />
          )}
        </td>
      </tr>
      {isExpanded && hasWarnings && (
        <tr>
          <td colSpan={8} className="px-6 py-2 bg-muted/10">
            <div className="space-y-1">
              {s.warnings.map((w, i) => (
                <p key={i} className="text-[11px] text-amber-700">• {w}</p>
              ))}
              {s.promotionRuleDetails?.length > 0 && (
                <div className="pt-1 border-t border-border/30 mt-1">
                  {s.promotionRuleDetails.map((d, i) => (
                    <p key={i} className="text-[10px] text-muted-foreground">📋 {d}</p>
                  ))}
                </div>
              )}
            </div>
          </td>
        </tr>
      )}
    </>
  );
}
