import { useMemo } from 'react';
import { Printer } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Skeleton } from '@/components/ui/skeleton';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { ProlizWeekLayout } from '@/components/scheduling/ProlizWeekLayout';
import type { ProlizRow } from '@/components/scheduling/ProlizDayTable';
import {
  WeekTimetableGrid,
  type WeekTimetableBlock,
  type WeekTimetableSection,
  type WeekTimetableSlot,
} from '@/components/scheduling/WeekTimetableGrid';

export interface TimetableTermOption {
  termId: number;
  label: string;
}

interface TimetableViewProps {
  sections: WeekTimetableSection[];
  slots?: WeekTimetableSlot[];
  lunchBreakStart?: string;
  lunchBreakEnd?: string;
  /** Dönem seçici — verilirse header'da dropdown çıkar. */
  terms?: TimetableTermOption[];
  selectedTermId?: number;
  onTermChange?: (termId: number) => void;
  /** Verilirse "Yazdır" butonu çıkar. */
  onPrint?: () => void;
  isPrinting?: boolean;
  isLoading?: boolean;
  isError?: boolean;
  errorTitle?: string;
  errorDescription?: string;
}

function classroomLabel(block: WeekTimetableBlock): string {
  return block.classroomName ?? 'Çevrimiçi';
}

export function TimetableView({
  sections,
  slots,
  lunchBreakStart,
  lunchBreakEnd,
  terms,
  selectedTermId,
  onTermChange,
  onPrint,
  isPrinting,
  isLoading,
  isError,
  errorTitle = 'Program yüklenemedi',
  errorDescription = 'Dönemi değiştirip tekrar deneyin.',
}: TimetableViewProps) {
  // Dar ekran ajanda listesi için güne göre satırlar.
  const rowsByDay = useMemo(() => {
    const grouped: Record<number, ProlizRow[]> = {};
    for (const section of sections) {
      for (const block of section.blocks) {
        (grouped[block.day] ??= []).push({
          sectionId: section.sectionId,
          sectionNumber: section.sectionNumber,
          courseCode: section.courseCode,
          courseName: section.courseName,
          classroomLabel: classroomLabel(block),
          instructorDisplay: section.instructorDisplay,
          startTime: block.startTime.slice(0, 5),
          endTime: block.endTime.slice(0, 5),
          isLab: block.blockType === 'Lab',
        });
      }
    }
    for (const day of Object.keys(grouped)) {
      grouped[Number(day)].sort((a, b) => a.startTime.localeCompare(b.startTime));
    }
    return grouped;
  }, [sections]);

  return (
    <Card className="flex min-h-[calc(100vh-8rem)] flex-col overflow-hidden">
      <CardContent className="flex flex-1 flex-col p-0">
        <header className="flex items-center justify-between gap-3 border-b border-border bg-muted/20 px-4 py-2">
          <div className="flex items-center gap-2">
            {terms && terms.length > 0 && (
              <>
                <label className="text-xs font-medium uppercase tracking-wider text-muted-foreground">
                  Dönem
                </label>
                <Select
                  value={selectedTermId?.toString()}
                  onValueChange={(v) => onTermChange?.(Number(v))}
                >
                  <SelectTrigger className="h-8 w-52 text-xs">
                    <SelectValue placeholder="Dönem seçin" />
                  </SelectTrigger>
                  <SelectContent>
                    {terms.map((t) => (
                      <SelectItem key={t.termId} value={t.termId.toString()}>
                        {t.label}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </>
            )}
          </div>
          {onPrint && (
            <Button
              onClick={onPrint}
              disabled={isPrinting || selectedTermId === undefined}
              variant="ghost"
              size="sm"
              className="h-8 text-xs"
            >
              <Printer className="mr-1.5 h-3.5 w-3.5" />
              Yazdır
            </Button>
          )}
        </header>

        {isLoading ? (
          <div className="flex-1 p-6">
            <Skeleton className="h-full min-h-[400px] rounded-xl" />
          </div>
        ) : isError ? (
          <div className="p-6">
            <Alert variant="destructive">
              <AlertTitle>{errorTitle}</AlertTitle>
              <AlertDescription>{errorDescription}</AlertDescription>
            </Alert>
          </div>
        ) : (
          <div className="flex flex-1 flex-col">
            {/* Geniş ekran: haftalık takvim ızgarası */}
            <div className="hidden flex-1 md:flex md:flex-col">
              <WeekTimetableGrid
                sections={sections}
                slots={slots}
                lunchBreakStart={lunchBreakStart}
                lunchBreakEnd={lunchBreakEnd}
              />
            </div>
            {/* Dar ekran: güne göre ajanda listesi */}
            <div className="flex flex-1 flex-col md:hidden">
              <ProlizWeekLayout rowsByDay={rowsByDay} />
            </div>
          </div>
        )}
      </CardContent>
    </Card>
  );
}
