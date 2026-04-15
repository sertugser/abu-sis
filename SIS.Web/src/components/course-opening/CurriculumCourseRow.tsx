import { Badge } from '@/components/ui/badge';
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/tooltip';
import { cn } from '@/lib/utils';
import { CheckCircle2, TrendingUp } from 'lucide-react';
import type { CurriculumCourseForOpeningDto } from '../../types/courseOpening';
import type { CourseDemandForecastDto } from '../../types/forecast';

interface Props {
  course: CurriculumCourseForOpeningDto;
  forecast: CourseDemandForecastDto | undefined;
  selected: boolean;
  onToggle: (courseId: number) => void;
  onOpenDetail: (course: CurriculumCourseForOpeningDto) => void;
}

export function CurriculumCourseRow({ course, forecast, selected, onToggle, onOpenDetail }: Props) {
  const canSelect = course.canOpen && !course.alreadyOpened;

  return (
    <div
      className={cn(
        'flex items-center gap-3 px-3 py-2.5 border-b last:border-b-0 transition-colors cursor-pointer',
        selected ? 'bg-blue-50/60' : 'hover:bg-gray-50/80',
        !canSelect && 'opacity-50',
      )}
      onClick={() => canSelect && onToggle(course.courseId)}
      onDoubleClick={e => { e.preventDefault(); onOpenDetail(course); }}
    >
      {/* Checkbox */}
      <input
        type="checkbox"
        checked={selected}
        disabled={!canSelect}
        onChange={() => onToggle(course.courseId)}
        onClick={e => e.stopPropagation()}
        className="rounded border-gray-300 shrink-0 mt-0.5"
      />

      {/* Content */}
      <div className="flex-1 min-w-0">
        {/* Line 1: Code + Name */}
        <div className="flex items-center gap-2">
          <span className="font-mono text-xs font-bold text-blue-700 shrink-0">{course.courseCode}</span>
          <span className="text-sm text-gray-800 truncate">{course.courseName}</span>
          <Badge
            variant={course.isCompulsory ? 'default' : 'outline'}
            className="text-[9px] px-1 py-0 shrink-0 rounded"
          >
            {course.isCompulsory ? 'Z' : 'S'}
          </Badge>
        </div>

        {/* Line 2: Metadata */}
        <div className="flex items-center gap-2 mt-1">
          <span className="text-[10px] text-gray-400 tabular-nums">{course.ects} AKTS</span>
          <span className="text-[10px] text-gray-400 tabular-nums">{course.theoryHours}+{course.labHours}</span>
          {forecast && (
            <TooltipProvider delayDuration={150}>
              <Tooltip>
                <TooltipTrigger asChild>
                  <span className="inline-flex items-center gap-0.5 text-[10px] font-medium text-gray-600">
                    <TrendingUp className="h-2.5 w-2.5" />
                    {forecast.totalPredictedDemand}
                    {course.retakeCount != null && course.retakeCount > 0 && (
                      <span className="text-red-500">+{course.retakeCount}</span>
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
          {course.crossDepartmentDemand && course.crossDepartmentDemand.length > 0 && (
            <TooltipProvider delayDuration={150}>
              <Tooltip>
                <TooltipTrigger asChild>
                  <Badge variant="outline" className="text-[9px] px-1 py-0 text-sky-600 border-sky-200">
                    +{course.crossDepartmentDemand.length} bölüm
                  </Badge>
                </TooltipTrigger>
                <TooltipContent side="right" className="text-xs space-y-0.5">
                  {course.crossDepartmentDemand.map((cd, i) => (
                    <p key={i}>{cd.departmentName}: {cd.estimatedStudentCount}</p>
                  ))}
                </TooltipContent>
              </Tooltip>
            </TooltipProvider>
          )}
        </div>
      </div>

      {/* Status */}
      <div className="shrink-0">
        {course.alreadyOpened ? (
          <span className="inline-flex items-center gap-1 text-[10px] font-medium text-emerald-600">
            <CheckCircle2 className="h-3 w-3" />
            {course.existingSectionCount} şube
          </span>
        ) : (
          <Badge variant="outline" className="text-[10px]">Açılabilir</Badge>
        )}
      </div>
    </div>
  );
}
