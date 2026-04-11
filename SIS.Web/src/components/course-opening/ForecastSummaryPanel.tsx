import { AlertTriangle, TrendingUp } from 'lucide-react';
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/tooltip';
import type { CourseDemandForecastDto } from '../../types/forecast';

export function ForecastSummaryPanel({ forecast }: { forecast: CourseDemandForecastDto }) {
  return (
    <div className="bg-blue-50 border border-blue-200 rounded-lg p-4 mt-4 space-y-3">
      <div className="flex items-center gap-2">
        <TrendingUp className="h-4 w-4 text-blue-700" />
        <span className="font-semibold text-blue-800 text-sm">Talep Tahmini — {forecast.courseCode}</span>
      </div>
      <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
        <div className="bg-white rounded-md p-2.5 text-center">
          <p className="text-lg font-bold text-blue-700">{forecast.totalPredictedDemand}</p>
          <p className="text-[10px] text-blue-600/70">Toplam Talep</p>
        </div>
        <div className="bg-white rounded-md p-2.5 text-center">
          <p className="text-lg font-bold text-emerald-700">{forecast.demandBreakdown.totalEligible}</p>
          <p className="text-[10px] text-emerald-600/70">İlk Kez Alan</p>
        </div>
        <div className="bg-white rounded-md p-2.5 text-center">
          <p className="text-lg font-bold text-red-700">{forecast.demandBreakdown.totalRetake}</p>
          <p className="text-[10px] text-red-600/70">
            Tekrar (FF: {forecast.demandBreakdown.retakeFF}, NA: {forecast.demandBreakdown.retakeNA})
          </p>
        </div>
        <div className="bg-white rounded-md p-2.5 text-center">
          <p className="text-lg font-bold text-violet-700">{forecast.recommendedSectionCount}</p>
          <p className="text-[10px] text-violet-600/70">
            Önerilen Şube ({forecast.sectionDistribution.effectiveCapacityPerSection} kişi)
          </p>
        </div>
      </div>

      <div className="grid grid-cols-2 gap-x-6 gap-y-1 text-xs text-gray-700 pt-2 border-t border-blue-200">
        <div className="flex justify-between"><span>Sınıfta (zamanında):</span><span className="font-medium">{forecast.demandBreakdown.regularOnSchedule}</span></div>
        <div className="flex justify-between"><span>Geride kalan:</span><span className="font-medium">{forecast.demandBreakdown.behindSchedule}</span></div>
        <div className="flex justify-between"><span>Üst sınıftan:</span><span className="font-medium">{forecast.demandBreakdown.upperCourse}</span></div>
        <div className="flex justify-between"><span>AKTS kısıtlı:</span><span className="font-medium">{forecast.demandBreakdown.ectsLimited}</span></div>
      </div>

      {forecast.confidence && (
        <div className="flex items-center gap-3 text-xs pt-2 border-t border-blue-200">
          <span className="text-blue-700 font-medium">Güven Aralığı:</span>
          <div className="flex items-center gap-1">
            <span className="font-mono">{forecast.confidence.lowerBound}</span>
            <div className="w-24 h-1.5 bg-blue-200 rounded-full relative">
              <div
                className="absolute h-1.5 bg-blue-500 rounded-full"
                style={{
                  left: `${Math.max(0, (forecast.confidence.lowerBound / (forecast.confidence.upperBound * 1.2)) * 100)}%`,
                  width: `${Math.min(100, ((forecast.confidence.upperBound - forecast.confidence.lowerBound) / (forecast.confidence.upperBound * 1.2)) * 100)}%`,
                }}
              />
            </div>
            <span className="font-mono">{forecast.confidence.upperBound}</span>
          </div>
          <span className="text-muted-foreground">(±{forecast.confidence.margin})</span>
        </div>
      )}

      {forecast.historicalEnrollments.length > 0 && (
        <div className="pt-2 border-t border-blue-200">
          <p className="text-xs font-medium text-blue-700 mb-2">Geçmiş Dönemler</p>
          <div className="flex items-end gap-1.5 h-16">
            {forecast.historicalEnrollments.map((history, index) => {
              const maxVal = Math.max(...forecast.historicalEnrollments.map(item => item.actualEnrollment), 1);
              const pct = (history.actualEnrollment / maxVal) * 100;

              return (
                <TooltipProvider key={index} delayDuration={100}>
                  <Tooltip>
                    <TooltipTrigger asChild>
                      <div className="flex flex-col items-center flex-1 min-w-0">
                        <span className="text-[9px] font-mono text-blue-700 mb-0.5">{history.actualEnrollment}</span>
                        <div className="w-full bg-blue-300 rounded-t" style={{ height: `${Math.max(pct, 8)}%` }} />
                      </div>
                    </TooltipTrigger>
                    <TooltipContent side="top" className="text-xs">
                      <p className="font-medium">{history.termName}</p>
                      <p>Kayıt: {history.actualEnrollment} öğrenci</p>
                      <p>Şube: {history.sectionCount}</p>
                    </TooltipContent>
                  </Tooltip>
                </TooltipProvider>
              );
            })}
          </div>
          <div className="flex gap-1.5">
            {forecast.historicalEnrollments.map((history, index) => (
              <span key={index} className="flex-1 text-[8px] text-center text-muted-foreground truncate">
                {history.termName.replace(/\d{4}-\d{4}\s*/, '')}
              </span>
            ))}
          </div>
        </div>
      )}

      {forecast.sectionDistribution.warnings.length > 0 && (
        <div className="space-y-1 pt-2 border-t border-blue-200">
          {forecast.sectionDistribution.warnings.map((warning, index) => (
            <div key={index} className="flex items-center gap-1.5 text-xs text-amber-700">
              <AlertTriangle className="h-3 w-3 shrink-0" />
              <span>{warning}</span>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
