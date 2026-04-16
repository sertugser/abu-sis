import { useState, useMemo } from 'react';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Progress } from '@/components/ui/progress';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { cn } from '@/lib/utils';
import {
  Lock, Loader2, AlertTriangle, Search, CheckCircle2, Eye, EyeOff,
} from 'lucide-react';
import { StepAccordion, ConfirmButton, MutationFeedback, StatPill } from './shared';
import { useGradeReadinessDetail, useLockGrades, useUnlockGrades } from '@/hooks/useTermClosing';
import type { SectionGradeDetailDto } from '@/api/termClosing.api';

interface GradeLockStepProps {
  departmentId: number;
  isCompleted: boolean;
  isActive: boolean;
  completedAt?: string | null;
  completedBy?: string | null;
  completedNotes?: string | null;
  onStepDone: () => void;
}

export function GradeLockStep({
  departmentId, isCompleted, isActive,
  completedAt, completedBy, completedNotes,
  onStepDone,
}: GradeLockStepProps) {
  const detailQuery = useGradeReadinessDetail(departmentId);
  const detail = detailQuery.data;
  const lockMutation = useLockGrades(departmentId);
  const unlockMutation = useUnlockGrades(departmentId);
  const [search, setSearch] = useState('');
  const [showEmpty, setShowEmpty] = useState(false);

  const handleLock = async () => {
    try {
      const result = await lockMutation.mutateAsync();
      if (result.success) onStepDone();
    } catch { /* feedback below */ }
  };

  const filteredSections = useMemo(() => {
    if (!detail?.sections) return [];
    let result = detail.sections;

    // Varsayılan: kayıtlı öğrencisi olmayan şubeleri gizle
    if (!showEmpty) {
      result = result.filter(s => s.totalRegistrations > 0);
    }

    if (search) {
      const q = search.toLowerCase();
      result = result.filter(s =>
        s.courseCode.toLowerCase().includes(q)
        || s.courseName.toLowerCase().includes(q)
        || s.instructorName.toLowerCase().includes(q)
      );
    }
    return result;
  }, [detail?.sections, search, showEmpty]);

  const activeSectionCount = useMemo(
    () => detail?.sections.filter(s => s.totalRegistrations > 0).length ?? 0,
    [detail?.sections],
  );

  const emptySectionCount = useMemo(
    () => (detail?.sections.length ?? 0) - activeSectionCount,
    [detail?.sections.length, activeSectionCount],
  );

  const missingSections = useMemo(
    () => detail?.sections.filter(s => s.missingGradeCount > 0) ?? [],
    [detail?.sections],
  );

  return (
    <StepAccordion
      stepNum={1}
      title="Not Kilitleme"
      description="Tum ogretim uyeleri icin not girisini kilitleyin. Eksik notlar NA olarak atanir."
      icon={Lock}
      isCompleted={isCompleted}
      isActive={isActive}
      isLocked={false}
      completedSummary={completedNotes ?? undefined}
      completedAt={completedAt}
      completedBy={completedBy}
      onToggle={() => {}}
      onUndo={() => unlockMutation.mutate()}
      undoLoading={unlockMutation.isPending}
      undoWarning="Not kilidi acilacak, sonraki adimlar (GPA Hesaplama, Sinif Atlatma) da geri alinacaktir. Ogretim uyeleri notlari tekrar duzenleyebilecek."
    >
      {detailQuery.isLoading && (
        <div className="flex items-center gap-2 py-8 justify-center text-muted-foreground">
          <Loader2 className="h-4 w-4 animate-spin" />
          <span className="text-sm">Sube bilgileri yukleniyor...</span>
        </div>
      )}

      {detailQuery.isError && (
        <Alert variant="destructive">
          <AlertTriangle className="h-4 w-4" />
          <AlertDescription>
            Sube bilgileri yuklenemedi: {(detailQuery.error as Error)?.message ?? 'Bilinmeyen hata.'}
            {' '}Backend sunucusunun calistigini ve yeniden baslatildigini kontrol edin.
          </AlertDescription>
        </Alert>
      )}

      {detail && !isCompleted && (
        <>
          {/* Stats row */}
          <div className="flex flex-wrap gap-2">
            <StatPill label="Kayitli Sube" value={activeSectionCount} />
            <StatPill label="Kayitli Ogrenci" value={detail.totalActiveRegistrations} />
            {detail.totalActiveRegistrations > 0 && (
              <StatPill
                label="Not Girilen"
                value={`${detail.totalActiveRegistrations - detail.missingGradeCount} / ${detail.totalActiveRegistrations}`}
                variant={detail.allGradesEntered ? 'success' : 'warning'}
              />
            )}
            {detail.missingGradeCount > 0 && (
              <StatPill
                label="Eksik"
                value={detail.missingGradeCount}
                variant="danger"
              />
            )}
            {emptySectionCount > 0 && (
              <StatPill label="Kayitsiz Sube" value={emptySectionCount} />
            )}
          </div>

          {/* Progress bar */}
          {detail.totalActiveRegistrations > 0 && (
            <div className="space-y-1.5">
              <div className="flex items-center justify-between text-xs">
                <span className="text-muted-foreground">Not Giris Orani</span>
                <span className="font-semibold tabular-nums">{detail.overallEntryPercentage}%</span>
              </div>
              <Progress
                value={detail.overallEntryPercentage}
                indicatorClassName={detail.overallEntryPercentage === 100 ? 'bg-emerald-500' : detail.overallEntryPercentage > 80 ? 'bg-amber-500' : 'bg-red-500'}
                className="h-2"
              />
            </div>
          )}

          {/* Warning for missing grades */}
          {detail.missingGradeCount > 0 && (
            <Alert variant="warning">
              <AlertTriangle className="h-4 w-4" />
              <AlertDescription>
                <strong>{missingSections.length} sube</strong>'de toplam <strong>{detail.missingGradeCount}</strong> ogrencinin notu eksik.
                Kilitleme isleminde eksik notlar otomatik olarak <strong>NA (Devamsiz)</strong> olarak atanacaktir.
              </AlertDescription>
            </Alert>
          )}

          {detail.allGradesEntered && (
            <Alert variant="success">
              <CheckCircle2 className="h-4 w-4" />
              <AlertDescription>Tum notlar girilmis. Kilitleme islemine hazir.</AlertDescription>
            </Alert>
          )}

          {/* Section table */}
          {detail.sections.length > 0 && (
            <div className="space-y-2">
              <div className="flex items-center justify-between gap-2">
                <h4 className="text-xs font-semibold text-muted-foreground uppercase tracking-wider">
                  Sube Detayi
                  <span className="ml-1.5 font-normal">({filteredSections.length})</span>
                </h4>
                <div className="flex items-center gap-2">
                  {emptySectionCount > 0 && (
                    <Button
                      variant="ghost"
                      size="sm"
                      className="gap-1.5 h-8 text-xs text-muted-foreground"
                      onClick={() => setShowEmpty(!showEmpty)}
                    >
                      {showEmpty ? <EyeOff className="h-3.5 w-3.5" /> : <Eye className="h-3.5 w-3.5" />}
                      {showEmpty ? 'Bos subeleri gizle' : `Bos subeleri goster (${emptySectionCount})`}
                    </Button>
                  )}
                  <div className="relative w-56">
                    <Search className="absolute left-2.5 top-1/2 -translate-y-1/2 h-3.5 w-3.5 text-muted-foreground" />
                    <Input
                      placeholder="Ders kodu, ad veya hoca..."
                      value={search}
                      onChange={e => setSearch(e.target.value)}
                      className="h-8 pl-8 text-xs"
                    />
                  </div>
                </div>
              </div>

              <div className="rounded-lg border overflow-hidden">
                <div className="max-h-[360px] overflow-y-auto">
                  <table className="w-full text-xs">
                    <thead className="bg-muted/40 sticky top-0 z-10">
                      <tr>
                        <th className="px-3 py-2 text-left font-semibold">Ders</th>
                        <th className="px-3 py-2 text-left font-semibold">Ogretim Uyesi</th>
                        <th className="px-3 py-2 text-center font-semibold">Kayitli</th>
                        <th className="px-3 py-2 text-center font-semibold">Girilen</th>
                        <th className="px-3 py-2 text-center font-semibold">Eksik</th>
                        <th className="px-3 py-2 text-right font-semibold">Oran</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-border/50">
                      {filteredSections.map((s: SectionGradeDetailDto) => (
                        <SectionRow key={s.sectionId} section={s} />
                      ))}
                      {filteredSections.length === 0 && (
                        <tr>
                          <td colSpan={6} className="px-3 py-6 text-center text-muted-foreground">
                            Sonuc bulunamadi
                          </td>
                        </tr>
                      )}
                    </tbody>
                  </table>
                </div>
              </div>
            </div>
          )}

          {/* Lock button */}
          <div className="pt-1">
            <ConfirmButton
              label="Notlari Kilitle"
              icon={Lock}
              isLoading={lockMutation.isPending}
              disabled={!detail.hasActiveRegistrations}
              title="Notlari kilitlemek istiyor musunuz?"
              description={`${detail.totalSections} sube kilitlenecek${detail.missingGradeCount > 0 ? `, ${detail.missingGradeCount} eksik not NA olarak atanacak` : ''}. Bu islem geri alinabilir.`}
              onConfirm={handleLock}
            />
          </div>
        </>
      )}

      {/* Completed state */}
      {isCompleted && detail && (
        <div className="flex flex-wrap gap-2">
          <StatPill label="Kilitlenen Sube" value={detail.finalizedSectionCount} variant="success" />
          <StatPill label="Toplam Kayit" value={detail.totalActiveRegistrations} variant="success" />
        </div>
      )}

      <MutationFeedback mutation={lockMutation} />
      <MutationFeedback mutation={unlockMutation} successMsg="Kilit geri alindi. Ogretim uyeleri notlari tekrar duzenleyebilir." />
    </StepAccordion>
  );
}

/* ── Section Table Row ── */

function SectionRow({ section: s }: { section: SectionGradeDetailDto }) {
  const hasMissing = s.missingGradeCount > 0;
  return (
    <tr className={cn(
      'hover:bg-muted/20 transition-colors',
      hasMissing && 'bg-red-50/40',
    )}>
      <td className="px-3 py-2">
        <span className="font-mono font-medium text-primary">{s.sectionCode}</span>
        <span className="ml-1.5 text-muted-foreground">{s.courseName}</span>
      </td>
      <td className="px-3 py-2 text-muted-foreground">{s.instructorName}</td>
      <td className="px-3 py-2 text-center tabular-nums">{s.totalRegistrations}</td>
      <td className="px-3 py-2 text-center tabular-nums">{s.enteredGradeCount}</td>
      <td className="px-3 py-2 text-center tabular-nums">
        {hasMissing ? (
          <Badge variant="destructive" className="text-[10px] px-1.5">{s.missingGradeCount}</Badge>
        ) : (
          <span className="text-emerald-600">0</span>
        )}
      </td>
      <td className="px-3 py-2 text-right">
        <span className={cn(
          'tabular-nums font-medium',
          s.entryPercentage === 100 ? 'text-emerald-600' : s.entryPercentage >= 80 ? 'text-amber-600' : 'text-red-600',
        )}>
          {s.entryPercentage}%
        </span>
      </td>
    </tr>
  );
}
