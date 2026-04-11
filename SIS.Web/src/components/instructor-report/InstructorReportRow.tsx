import { useState } from 'react';
import { ChevronDown, ChevronRight, Loader2 } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { useInstructorWorkload } from '../../hooks/useCourseOpening';
import type { InstructorOptionDto } from '../../types/courseOpening';

interface Props {
  instructor: InstructorOptionDto & { departmentName: string };
  termId: number;
}

export function InstructorReportRow({ instructor, termId }: Props) {
  const [expanded, setExpanded] = useState(false);

  const workloadQuery = useInstructorWorkload(
    expanded ? instructor.instructorId : null,
    termId,
  );

  const workload = workloadQuery.data;
  const sections = workload?.assignedSections ?? [];
  const assignedHours = workload?.assignedWeeklyHours ?? 0;
  const maxHours = workload?.maxWeeklyHours ?? 0;
  const pct = maxHours > 0 ? Math.round((assignedHours / maxHours) * 100) : 0;
  const barColor = pct >= 100 ? 'bg-red-500' : pct >= 80 ? 'bg-amber-500' : 'bg-emerald-500';

  return (
    <>
      <tr
        className="hover:bg-muted/20 transition-colors cursor-pointer select-none"
        onClick={() => setExpanded(v => !v)}
      >
        <td className="px-4 py-3">
          {expanded
            ? <ChevronDown className="h-4 w-4 text-muted-foreground" />
            : <ChevronRight className="h-4 w-4 text-muted-foreground" />}
        </td>
        <td className="px-4 py-3">
          <Badge variant="outline" className="text-xs">{workload?.title ?? '—'}</Badge>
        </td>
        <td className="px-4 py-3 font-medium text-foreground">{instructor.fullName}</td>
        <td className="px-4 py-3 text-sm text-muted-foreground">{instructor.departmentName}</td>
        <td className="px-4 py-3 min-w-[140px]">
          {expanded && workloadQuery.isLoading ? (
            <Loader2 className="h-3.5 w-3.5 animate-spin text-muted-foreground" />
          ) : workload ? (
            <div className="space-y-1">
              <span className="text-xs font-medium">{assignedHours}/{maxHours} saat</span>
              <div className="h-1.5 w-24 bg-gray-100 rounded-full overflow-hidden">
                <div className={`h-full rounded-full ${barColor}`} style={{ width: `${Math.min(pct, 100)}%` }} />
              </div>
            </div>
          ) : (
            <span className="text-xs text-muted-foreground">—</span>
          )}
        </td>
        <td className="px-4 py-3 text-center text-sm">
          {workload
            ? new Set(sections.map(s => s.courseCode)).size
            : <span className="text-muted-foreground">—</span>}
        </td>
        <td className="px-4 py-3 text-center text-sm">
          {workload
            ? sections.length
            : <span className="text-muted-foreground">—</span>}
        </td>
      </tr>

      {expanded && (
        <tr>
          <td colSpan={7} className="bg-muted/10 px-0 py-0">
            <div className="px-12 py-3">
              {workloadQuery.isLoading ? (
                <p className="text-sm text-muted-foreground py-2">Yükleniyor...</p>
              ) : workloadQuery.isError ? (
                <p className="text-sm text-destructive py-2">Yük bilgisi alınamadı.</p>
              ) : sections.length === 0 ? (
                <p className="text-sm text-muted-foreground py-2">Bu dönem atanmış ders yok.</p>
              ) : (
                <table className="w-full text-xs">
                  <thead>
                    <tr className="text-muted-foreground border-b border-border">
                      <th className="text-left pb-2 font-medium">Ders Kodu</th>
                      <th className="text-left pb-2 font-medium">Ders Adı</th>
                      <th className="text-center pb-2 font-medium">Şube</th>
                      <th className="text-center pb-2 font-medium">Hft. Saat</th>
                      <th className="text-center pb-2 font-medium">Kontenjan</th>
                      <th className="text-center pb-2 font-medium">Kayıtlı</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-border/50">
                    {sections.map(sec => (
                      <tr key={sec.sectionId} className="hover:bg-muted/20">
                        <td className="py-1.5 pr-3 font-mono font-medium">{sec.courseCode}</td>
                        <td className="py-1.5 pr-3">{sec.courseName}</td>
                        <td className="py-1.5 text-center">{sec.sectionNumber}</td>
                        <td className="py-1.5 text-center">{sec.weeklyHours}</td>
                        <td className="py-1.5 text-center">{sec.capacity}</td>
                        <td className="py-1.5 text-center">{sec.enrolledCount}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}
            </div>
          </td>
        </tr>
      )}
    </>
  );
}
