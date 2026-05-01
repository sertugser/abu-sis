import { Badge } from '@/components/ui/badge';
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/tooltip';
import { cn } from '@/lib/utils';
import { Layers } from 'lucide-react';
import type { OpenedCourseSectionDto } from '../../types/courseOpening';
import type { CourseDemandForecastDto } from '../../types/forecast';

interface Props {
  courseCode: string;
  courseName: string;
  courseId: number;
  sections: OpenedCourseSectionDto[];
  forecast: CourseDemandForecastDto | undefined;
  selected?: boolean;
  onSelect?: (courseId: number) => void;
  onOpenManagement?: (courseId: number) => void;
}

/**
 * Açılan dersin tablo satırı — Proliz tarzı.
 * - Tek tıkla = sarı highlight (selected)
 * - Çift tıkla = ders yönetim dialog'u açılır (CourseManagementDialog)
 *
 * Şube ekleme/düzenleme/kapatma vb. tüm aksiyonlar dialog içine taşındı.
 * Bu satır sadece özet bilgi gösterir.
 */
export function OpenedCourseRow({
  courseCode,
  courseName,
  courseId,
  sections,
  forecast,
  selected = false,
  onSelect,
  onOpenManagement,
}: Props) {
  const totCap = sections.reduce((s, sec) => s + sec.capacity, 0);
  const totEnr = sections.reduce((s, sec) => s + sec.enrolledCount, 0);
  const rec = forecast?.recommendedSectionCount ?? 0;
  const fillRatio = totCap > 0 ? totEnr / totCap : 0;
  const demandWarning = forecast && forecast.totalPredictedDemand > totCap;

  return (
    <div
      className={cn(
        'grid grid-cols-[20px_100px_1fr_60px_72px_60px_60px_100px_60px] items-center gap-2 px-3 py-2 text-sm cursor-pointer transition-colors border-b border-gray-100 last:border-b-0',
        selected ? 'bg-amber-50 hover:bg-amber-100/80' : 'hover:bg-gray-50',
      )}
      onClick={() => onSelect?.(courseId)}
      onDoubleClick={(e) => {
        e.preventDefault();
        onOpenManagement?.(courseId);
      }}
      title="Çift tıkla: ders yönetim ekranı"
    >
      {/* Boş — sol panel chevron yerine */}
      <span />

      <span className="font-mono text-slate-800 font-semibold truncate" title={courseCode}>
        {courseCode}
      </span>

      <span className="text-gray-800 truncate" title={courseName}>
        {courseName}
      </span>

      {/* Şube sayısı */}
      <span className="text-center tabular-nums text-gray-700 inline-flex items-center justify-center gap-1">
        <Layers className="h-3 w-3 text-gray-400" />
        {sections.length}
      </span>

      {/* Kontenjan */}
      <span className="text-center tabular-nums text-gray-700">{totCap}</span>

      {/* Kayıt */}
      <span
        className={cn(
          'text-center tabular-nums',
          totEnr > 0 ? 'text-gray-700 font-medium' : 'text-gray-400',
        )}
      >
        {totEnr}
      </span>

      {/* Talep */}
      <span
        className={cn(
          'text-center tabular-nums',
          demandWarning ? 'text-red-600 font-medium' : 'text-gray-500',
        )}
      >
        {forecast ? forecast.totalPredictedDemand : '—'}
      </span>

      {/* Doluluk mini progress + % */}
      <div className="flex items-center gap-1.5">
        {totCap > 0 ? (
          <>
            <div className="flex-1 h-1.5 bg-gray-200 rounded-full overflow-hidden min-w-[40px]">
              <div
                className={cn(
                  'h-full rounded-full transition-all',
                  fillRatio > 0.9 ? 'bg-red-500' : fillRatio > 0.7 ? 'bg-amber-500' : 'bg-blue-500',
                )}
                style={{ width: `${Math.min(fillRatio * 100, 100)}%` }}
              />
            </div>
            <span className="text-[10px] text-gray-400 tabular-nums w-7 text-right">
              %{Math.round(fillRatio * 100)}
            </span>
          </>
        ) : (
          <span className="text-xs text-gray-300 mx-auto">—</span>
        )}
      </div>

      {/* Önerilen şube oranı badge */}
      <div className="flex items-center justify-center" onClick={(e) => e.stopPropagation()}>
        {rec > 0 ? (
          <TooltipProvider delayDuration={200}>
            <Tooltip>
              <TooltipTrigger asChild>
                <Badge
                  variant={
                    sections.length < rec
                      ? 'destructive'
                      : sections.length === rec
                      ? 'success'
                      : 'warning'
                  }
                  className="text-[10px] tabular-nums cursor-default px-1.5"
                >
                  {sections.length}/{rec}
                </Badge>
              </TooltipTrigger>
              <TooltipContent side="top" className="text-xs">
                {sections.length < rec
                  ? 'Önerilen şubeden az'
                  : sections.length === rec
                  ? 'Önerilen ile eşleşti'
                  : 'Önerilenden fazla'}
              </TooltipContent>
            </Tooltip>
          </TooltipProvider>
        ) : (
          <span className="text-gray-300 text-xs">—</span>
        )}
      </div>
    </div>
  );
}
