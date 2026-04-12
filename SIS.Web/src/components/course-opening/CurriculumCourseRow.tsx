import { Badge } from '@/components/ui/badge';
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/tooltip';
import type { CurriculumCourseForOpeningDto } from '../../types/courseOpening';
import type { CourseDemandForecastDto } from '../../types/forecast';

interface Props {
  course: CurriculumCourseForOpeningDto;
  forecast: CourseDemandForecastDto | undefined;
  selected: boolean;
  onToggle: (courseId: number) => void;
}

function getStatusBadge(course: CurriculumCourseForOpeningDto) {
  if (course.alreadyOpened) {
    return (
      <Badge variant="success" className="text-[10px]">
        Açıldı ({course.existingSectionCount})
      </Badge>
    );
  }
  if (course.canOpen) {
    return <Badge variant="warning" className="text-[10px]">Bekliyor</Badge>;
  }
  return <Badge variant="outline" className="text-[10px]">Açılamaz</Badge>;
}

export function CurriculumCourseRow({ course, forecast, selected, onToggle }: Props) {
  // Capacity ratio: how much of the predicted demand is covered by existing sections
  const capacityRatio = course.alreadyOpened && course.existingSectionCount > 0 && forecast && forecast.totalPredictedDemand > 0
    ? Math.min(100, Math.round(((course.existingSectionCount * (course.recommendedCapacity ?? 40)) / forecast.totalPredictedDemand) * 100))
    : 0;

  const canSelect = course.canOpen && !course.alreadyOpened;

  return (
    <div
      className={`flex items-center gap-3 px-3 py-2 border-b last:border-b-0 hover:bg-accent/30 transition-colors cursor-pointer ${
        selected ? 'bg-primary/5' : ''
      } ${!canSelect ? 'opacity-60' : ''}`}
      onClick={() => canSelect && onToggle(course.courseId)}
    >
      <input
        type="checkbox"
        checked={selected}
        disabled={!canSelect}
        onChange={() => onToggle(course.courseId)}
        onClick={e => e.stopPropagation()}
        className="rounded border-gray-300 shrink-0"
      />

      <div className="flex-1 min-w-0">
        <div className="flex items-center gap-2">
          <span className="font-mono text-xs font-bold text-[#003366]">{course.courseCode}</span>
          <span className="text-sm truncate">{course.courseName}</span>
          <Badge variant={course.isCompulsory ? 'default' : 'outline'} className="text-[9px] shrink-0">
            {course.isCompulsory ? 'Z' : 'S'}
          </Badge>
        </div>

        <div className="flex items-center gap-3 mt-0.5">
          <span className="text-[11px] text-muted-foreground">{course.ects} AKTS</span>
          <span className="text-[11px] text-muted-foreground">{course.theoryHours}+{course.labHours}</span>
          {forecast && (
            <TooltipProvider delayDuration={150}>
              <Tooltip>
                <TooltipTrigger asChild>
                  <span className="text-[11px] font-medium">
                    Talep: {forecast.totalPredictedDemand}
                    {course.retakeCount != null && course.retakeCount > 0 && (
                      <span className="text-red-600 ml-1">({course.retakeCount} tekrar)</span>
                    )}
                  </span>
                </TooltipTrigger>
                <TooltipContent side="right" className="text-xs space-y-0.5">
                  <p>Sınıfta: {forecast.demandBreakdown.regularOnSchedule}</p>
                  <p>Tekrar: {forecast.demandBreakdown.totalRetake}</p>
                  <p>Geride: {forecast.demandBreakdown.behindSchedule}</p>
                  <p>Üst sınıf: {forecast.demandBreakdown.upperCourse}</p>
                  <p className="pt-1 border-t font-medium">Önerilen: {forecast.recommendedSectionCount} şube</p>
                </TooltipContent>
              </Tooltip>
            </TooltipProvider>
          )}
        </div>

        {/* Capacity coverage bar */}
        {forecast && forecast.totalPredictedDemand > 0 && course.alreadyOpened && (
          <div className="h-1 bg-gray-100 rounded-full mt-1 overflow-hidden">
            <div
              className={`h-full rounded-full ${capacityRatio >= 90 ? 'bg-emerald-400' : capacityRatio >= 60 ? 'bg-amber-400' : 'bg-red-400'}`}
              style={{ width: `${capacityRatio}%` }}
            />
          </div>
        )}

        {course.crossDepartmentDemand && course.crossDepartmentDemand.length > 0 && (
          <div className="flex gap-1 mt-1 flex-wrap">
            {course.crossDepartmentDemand.map((cd, i) => (
              <span key={i} className="text-[9px] px-1.5 rounded-full bg-sky-100 text-sky-700">
                {cd.departmentName}({cd.estimatedStudentCount})
              </span>
            ))}
          </div>
        )}
      </div>

      <div className="shrink-0">
        {getStatusBadge(course)}
      </div>
    </div>
  );
}
