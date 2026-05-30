import { Fragment, useMemo } from 'react';
import { cn } from '@/lib/utils';
import type { DepartmentSchedule, DepartmentScheduleBlock } from '@/types/departmentSchedule';

interface DepartmentScheduleWeekGridProps {
  data: DepartmentSchedule;
}

interface DayRow {
  rowKey: string;
  sectionId: number;
  sectionNumber: string;
  courseCode: string;
  courseName: string;
  classroomLabel: string;
  instructorDisplay: string;
  startTime: string;
  endTime: string;
  isLab: boolean;
}

const DAY_PAIRS: [number, number | null][] = [
  [1, 2],
  [3, 4],
  [5, null],
];

const DAY_LABELS: Record<number, string> = {
  1: 'Pazartesi',
  2: 'Salı',
  3: 'Çarşamba',
  4: 'Perşembe',
  5: 'Cuma',
  6: 'Cumartesi',
  7: 'Pazar',
};

function formatClassroom(block: DepartmentScheduleBlock): string {
  if (!block.classroomName) return 'Çevrimiçi';
  return block.classroomCapacity != null
    ? `${block.classroomName}[${block.classroomCapacity}]`
    : block.classroomName;
}

function formatTime(t: string): string {
  return t.length >= 5 ? t.slice(0, 5) : t;
}

function extractDayRows(data: DepartmentSchedule, day: number): DayRow[] {
  const rows: DayRow[] = [];
  for (const section of data.sections) {
    for (const block of section.blocks) {
      if (block.day !== day) continue;
      rows.push({
        rowKey: `${section.sectionId}-${block.day}-${block.startTime}`,
        sectionId: section.sectionId,
        sectionNumber: section.sectionNumber,
        courseCode: section.courseCode,
        courseName: section.courseName,
        classroomLabel: formatClassroom(block),
        instructorDisplay: section.instructorDisplay,
        startTime: formatTime(block.startTime),
        endTime: formatTime(block.endTime),
        isLab: block.blockType === 'Lab',
      });
    }
  }
  return rows.sort((a, b) => a.startTime.localeCompare(b.startTime));
}

function DayPanel({ day, rows }: { day: number; rows: DayRow[] }) {
  return (
    <div className="overflow-hidden rounded-md border border-border bg-card">
      <div className="border-b border-border bg-muted/50 px-3 py-2 text-sm font-semibold text-foreground">
        {DAY_LABELS[day]}
      </div>
      {rows.length === 0 ? (
        <p className="px-3 py-3 text-xs italic text-muted-foreground">
          Tanımlı Ders Programı Bulunamadı!
        </p>
      ) : (
        <table className="w-full border-collapse text-xs">
          <thead>
            <tr className="border-b border-border bg-muted/20 text-muted-foreground">
              <th className="px-2 py-1 text-left font-medium">Saat</th>
              <th className="px-2 py-1 text-left font-medium">Ders Kodu</th>
              <th className="px-2 py-1 text-left font-medium">Ders Adı</th>
              <th className="px-2 py-1 text-left font-medium">Derslik</th>
              <th className="px-2 py-1 text-left font-medium">Öğretim Elemanı</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((row) => (
              <tr
                key={row.rowKey}
                className={cn(
                  'border-b border-border/40 last:border-0',
                  row.isLab && 'bg-emerald-50',
                )}
                data-testid={row.isLab ? 'lab-row' : 'theory-row'}
              >
                <td className="px-2 py-1 whitespace-nowrap">
                  {row.startTime}-{row.endTime}
                </td>
                <td className="px-2 py-1 whitespace-nowrap font-mono">
                  {row.courseCode}({row.sectionNumber})
                </td>
                <td className="px-2 py-1">{row.courseName}</td>
                <td className="px-2 py-1 whitespace-nowrap font-mono">{row.classroomLabel}</td>
                <td className="px-2 py-1">{row.instructorDisplay}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}

export function DepartmentScheduleWeekGrid({ data }: DepartmentScheduleWeekGridProps) {
  const rowsByDay = useMemo(() => {
    const grouped: Record<number, DayRow[]> = {};
    for (const day of [1, 2, 3, 4, 5, 6, 7]) {
      grouped[day] = extractDayRows(data, day);
    }
    return grouped;
  }, [data]);

  return (
    <div className="grid grid-cols-1 gap-3 lg:grid-cols-2">
      {DAY_PAIRS.map(([leftDay, rightDay]) => (
        <Fragment key={`pair-${leftDay}`}>
          <DayPanel day={leftDay} rows={rowsByDay[leftDay] ?? []} />
          {rightDay !== null ? (
            <DayPanel day={rightDay} rows={rowsByDay[rightDay] ?? []} />
          ) : (
            <div className="hidden lg:block" />
          )}
        </Fragment>
      ))}
      <p className="col-span-full text-xs italic text-muted-foreground">
        Not: Uygulama saatleri yeşil renkle gösterilir
      </p>
    </div>
  );
}
