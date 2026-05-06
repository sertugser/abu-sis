import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/tooltip';
import { cn } from '@/lib/utils';
import { CheckCircle2, Lock, AlertCircle } from 'lucide-react';
import type { CurriculumCourseForOpeningDto } from '../../types/courseOpening';

interface Props {
  course: CurriculumCourseForOpeningDto;
  selected: boolean;
  onToggle: (courseId: number) => void;
  onOpenDetail: (course: CurriculumCourseForOpeningDto) => void;
}

/** Proliz tarzı tek satır tablo görünümü: ☐ | Kod | Ad | Z/S | KRD | AKTS | Snf | Grup | Durum */
export function CurriculumCourseRow({ course, selected, onToggle, onOpenDetail }: Props) {
  const canSelect = course.canOpen && !course.alreadyOpened;
  const classYear = course.suggestedSemester > 0 ? Math.ceil(course.suggestedSemester / 2) : null;
  const credit = course.nationalCredit;
  const groupLabel = course.source === 'Pool' ? course.electiveGroupCode ?? '—' : '—';

  return (
    <div
      className={cn(
        'grid grid-cols-[28px_100px_1fr_32px_48px_52px_40px_80px_32px] items-center gap-2 px-3 py-2 border-b border-gray-100 text-sm cursor-pointer transition-colors',
        selected ? 'bg-amber-50 hover:bg-amber-100/80' : 'hover:bg-gray-50',
        !canSelect && 'opacity-60',
      )}
      onClick={() => canSelect && onToggle(course.courseId)}
      onDoubleClick={(e) => {
        e.preventDefault();
        onOpenDetail(course);
      }}
    >
      <input
        type="checkbox"
        checked={selected}
        disabled={!canSelect}
        onChange={() => onToggle(course.courseId)}
        onClick={(e) => e.stopPropagation()}
        className="h-4 w-4 rounded border-gray-300 text-blue-600 focus:ring-1 focus:ring-blue-300"
      />

      <button
        className="text-left font-mono text-slate-800 font-semibold hover:text-primary hover:underline truncate"
        onClick={(e) => {
          e.stopPropagation();
          onOpenDetail(course);
        }}
        title="Detaya bak"
      >
        {course.courseCode}
      </button>

      <span className="text-gray-800 truncate" title={course.courseName}>
        {course.courseName}
      </span>

      <span
        className={cn(
          'text-center font-semibold',
          course.isCompulsory ? 'text-red-600' : 'text-emerald-600',
        )}
        title={course.isCompulsory ? 'Zorunlu' : 'Seçmeli'}
      >
        {course.isCompulsory ? 'Z' : 'S'}
      </span>

      <span className="text-center tabular-nums text-gray-700">{credit}</span>

      <span className="text-center tabular-nums text-gray-700 font-medium">{course.ects}</span>

      <span className="text-center tabular-nums text-gray-600">{classYear ?? '—'}</span>

      <span
        className={cn(
          'truncate text-center text-xs',
          course.source === 'Pool' ? 'text-violet-700 font-medium' : 'text-gray-400',
        )}
        title={course.electiveGroupName ?? ''}
      >
        {groupLabel}
      </span>

      {/* Durum mini ikon */}
      <div className="flex justify-center">
        {course.alreadyOpened ? (
          <TooltipProvider delayDuration={150}>
            <Tooltip>
              <TooltipTrigger asChild>
                <CheckCircle2 className="h-4 w-4 text-emerald-600" />
              </TooltipTrigger>
              <TooltipContent side="left" className="text-xs">
                Açılmış — {course.existingSectionCount} şube
              </TooltipContent>
            </Tooltip>
          </TooltipProvider>
        ) : !course.canOpen ? (
          <TooltipProvider delayDuration={150}>
            <Tooltip>
              <TooltipTrigger asChild>
                {course.blockReason === 'TermNotInPlanning' ? (
                  <Lock className="h-4 w-4 text-amber-600" />
                ) : (
                  <AlertCircle className="h-4 w-4 text-gray-400" />
                )}
              </TooltipTrigger>
              <TooltipContent side="left" className="text-xs max-w-[220px]">
                {course.blockReason === 'TermNotInPlanning' && 'Dönem aktif değil — açılamaz.'}
                {course.blockReason === 'CourseUnassigned' && 'Sahip bölüm atanmamış.'}
                {course.blockReason === 'NotOwner' && 'Bu ders başka bölüme ait, sahibi açar.'}
              </TooltipContent>
            </Tooltip>
          </TooltipProvider>
        ) : null}
      </div>
    </div>
  );
}
