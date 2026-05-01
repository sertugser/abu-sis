import { Sheet, SheetContent, SheetDescription, SheetHeader, SheetTitle } from '@/components/ui/sheet';
import { Badge } from '@/components/ui/badge';
import { cn } from '@/lib/utils';
import { AlertTriangle, ExternalLink } from 'lucide-react';
import type { ExternalCourseDto } from '../../api/courseOpening.api';

interface Props {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  externalCourses: ExternalCourseDto[] | undefined;
  departmentName?: string;
}

export function ExternalCoursesDrawer({ open, onOpenChange, externalCourses, departmentName }: Props) {
  const list = externalCourses ?? [];

  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent side="right" className="w-[480px] sm:max-w-[480px] overflow-y-auto">
        <SheetHeader>
          <SheetTitle className="flex items-center gap-2">
            <ExternalLink className="h-4 w-4 text-violet-600" />
            Dış Dersler
            <Badge variant="secondary" className="ml-1 text-xs">{list.length}</Badge>
          </SheetTitle>
          <SheetDescription className="text-xs">
            {departmentName ? `${departmentName} müfredatında olan` : 'Müfredatınızda olan'}, başka bölüme ait dersler.
            Bu dersleri sahip bölüm açar — siz sadece görüntülersiniz.
          </SheetDescription>
        </SheetHeader>

        <div className="mt-4 space-y-2">
          {list.length === 0 ? (
            <div className="flex flex-col items-center py-12 text-gray-400">
              <ExternalLink className="h-8 w-8 mb-2" />
              <p className="text-sm">Dış ders bulunamadı.</p>
            </div>
          ) : (
            list.map((ec) => (
              <div
                key={ec.courseId}
                className="rounded-lg border border-gray-200 px-3 py-2.5 hover:bg-gray-50/80"
              >
                <div className="flex items-center gap-2">
                  <span className="font-mono text-xs font-bold text-blue-700 shrink-0">{ec.courseCode}</span>
                  <span className="text-sm text-gray-800 truncate flex-1">{ec.courseName}</span>
                  <Badge variant="outline" className="text-[10px] shrink-0 text-violet-600 border-violet-200">
                    {ec.ownerDepartmentName}
                  </Badge>
                </div>
                <div className="flex items-center gap-1.5 mt-1.5">
                  <span className="text-xs text-gray-500">
                    <strong>{ec.sectionCount}</strong> şube
                  </span>
                  <span className="text-gray-300">·</span>
                  <span className="text-xs text-gray-500 tabular-nums">
                    Kont: <strong className="text-gray-700">{ec.totalCapacity}</strong>
                  </span>
                  <span className="text-gray-300">·</span>
                  <span className="text-xs text-gray-500 tabular-nums">
                    Kayıt: <strong>{ec.totalEnrolled}</strong>
                  </span>
                  {ec.predictedDemand > 0 && (
                    <>
                      <span className="text-gray-300">·</span>
                      <span
                        className={cn(
                          'text-xs tabular-nums font-medium',
                          ec.predictedDemand > ec.totalCapacity ? 'text-red-600' : 'text-gray-500',
                        )}
                      >
                        Talep: {ec.predictedDemand}
                      </span>
                    </>
                  )}
                </div>
                {ec.predictedDemand > ec.totalCapacity && (
                  <div className="flex items-center gap-1 mt-1.5 text-[11px] text-amber-600">
                    <AlertTriangle className="h-3 w-3 shrink-0" />
                    Talep kontenjanı aşıyor
                  </div>
                )}
              </div>
            ))
          )}
        </div>
      </SheetContent>
    </Sheet>
  );
}
