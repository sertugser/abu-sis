import { Sheet, SheetContent, SheetHeader, SheetTitle, SheetDescription } from '@/components/ui/sheet';
import { Badge } from '@/components/ui/badge';
import { Skeleton } from '@/components/ui/skeleton';
import { useAdvisorSchedule } from '@/hooks/useAdvisorInfo';
import type { AdvisorScheduleItem } from '@/api/advisorInfo.api';

interface AdvisorScheduleSheetProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  advisorName: string;
}

const DAY_LABELS: Record<number, string> = {
  1: 'Pazartesi',
  2: 'Salı',
  3: 'Çarşamba',
  4: 'Perşembe',
  5: 'Cuma',
  6: 'Cumartesi',
  7: 'Pazar',
};

function formatTime(t: string): string {
  return t.length >= 5 ? t.slice(0, 5) : t;
}

function groupByDay(items: AdvisorScheduleItem[]): Map<number, AdvisorScheduleItem[]> {
  const map = new Map<number, AdvisorScheduleItem[]>();
  for (const item of items) {
    const list = map.get(item.dayOfWeek) ?? [];
    list.push(item);
    map.set(item.dayOfWeek, list);
  }
  return map;
}

export function AdvisorScheduleSheet({ open, onOpenChange, advisorName }: AdvisorScheduleSheetProps) {
  const { data, isLoading, isError, error } = useAdvisorSchedule(open);

  const grouped = data ? groupByDay(data.items) : new Map<number, AdvisorScheduleItem[]>();
  const days = Array.from(grouped.keys()).sort((a, b) => a - b);

  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent side="right" className="w-full sm:max-w-2xl overflow-y-auto">
        <SheetHeader>
          <SheetTitle>Danışman Ders Programı</SheetTitle>
          <SheetDescription>
            {advisorName} — {data?.termLabel ?? 'Aktif dönem yok'}
          </SheetDescription>
        </SheetHeader>

        <div className="mt-6 space-y-4">
          {isLoading && (
            <div className="space-y-2">
              <Skeleton className="h-20 w-full" />
              <Skeleton className="h-20 w-full" />
              <Skeleton className="h-20 w-full" />
            </div>
          )}

          {isError && (
            <p className="text-sm text-destructive">
              Ders programı yüklenemedi: {error instanceof Error ? error.message : 'Bilinmeyen hata'}
            </p>
          )}

          {!isLoading && !isError && data && data.items.length === 0 && (
            <div className="rounded-md border border-dashed p-8 text-center text-sm text-muted-foreground">
              Aktif dönemde atanmış ders bulunmuyor.
            </div>
          )}

          {!isLoading && !isError && days.length > 0 && (
            <div className="space-y-4">
              {days.map(day => (
                <section key={day} className="space-y-2">
                  <h3 className="text-sm font-semibold text-muted-foreground">
                    {DAY_LABELS[day] ?? `Gün ${day}`}
                  </h3>
                  <ul className="space-y-2">
                    {grouped.get(day)!.map((item, idx) => (
                      <li
                        key={`${item.sectionId}-${day}-${idx}`}
                        className="rounded-md border p-3 text-sm"
                      >
                        <div className="flex items-start justify-between gap-2">
                          <div>
                            <div className="font-medium">
                              {item.courseCode} — {item.courseName}
                            </div>
                            <div className="text-xs text-muted-foreground">
                              Şube {item.sectionNumber}
                              {item.classroomCode && ` · Derslik ${item.classroomCode}`}
                            </div>
                          </div>
                          <div className="text-right">
                            <div className="font-mono text-xs">
                              {formatTime(item.startTime)}–{formatTime(item.endTime)}
                            </div>
                            <Badge variant="secondary" className="mt-1 text-[10px]">
                              {item.blockType === 'Lab' ? 'Uygulama' : 'Teorik'}
                            </Badge>
                          </div>
                        </div>
                      </li>
                    ))}
                  </ul>
                </section>
              ))}
            </div>
          )}
        </div>
      </SheetContent>
    </Sheet>
  );
}
