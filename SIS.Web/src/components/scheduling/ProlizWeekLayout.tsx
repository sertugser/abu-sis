import { Card, CardContent } from '@/components/ui/card';
import { cn } from '@/lib/utils';
import { dayNumberToTurkish } from '@/lib/scheduleUtils';
import type { ProlizRow } from './ProlizDayTable';

interface ProlizWeekLayoutProps {
  rowsByDay: Record<number, ProlizRow[]>;
}

const DAY_ORDER = [1, 2, 3, 4, 5];

export function ProlizWeekLayout({ rowsByDay }: ProlizWeekLayoutProps) {
  return (
    <div className="space-y-3">
      <Card className="overflow-hidden">
        <CardContent className="p-0">
          <table className="w-full border-collapse text-xs">
            <colgroup>
              <col className="w-[10%]" />
              <col className="w-[12%]" />
              <col className="w-[34%]" />
              <col className="w-[14%]" />
              <col className="w-[30%]" />
            </colgroup>
            {DAY_ORDER.map((day) => {
              const rows = rowsByDay[day] ?? [];
              return (
                <tbody key={day} className="border-t border-border first:border-t-0">
                  <tr className="bg-muted/60">
                    <th
                      colSpan={5}
                      className="px-3 py-2 text-left text-sm font-semibold text-foreground"
                    >
                      {dayNumberToTurkish(day)}
                    </th>
                  </tr>
                  <tr className="border-y bg-muted/20 text-muted-foreground">
                    <th className="px-3 py-1 text-left font-medium">Saat</th>
                    <th className="px-3 py-1 text-left font-medium">Ders Kodu</th>
                    <th className="px-3 py-1 text-left font-medium">Ders Adı</th>
                    <th className="px-3 py-1 text-left font-medium">Derslik</th>
                    <th className="px-3 py-1 text-left font-medium">Öğretim Elemanı</th>
                  </tr>
                  {rows.length === 0 ? (
                    <tr>
                      <td
                        colSpan={5}
                        className="px-3 py-2 text-xs italic text-muted-foreground"
                      >
                        Tanımlı Ders Programı Bulunamadı!
                      </td>
                    </tr>
                  ) : (
                    rows.map((row, idx) => (
                      <tr
                        key={`${day}-${row.sectionId}-${idx}`}
                        className={cn(row.isLab && 'bg-emerald-50')}
                        data-testid={row.isLab ? 'lab-row' : 'theory-row'}
                      >
                        <td className="px-3 py-1">
                          {row.startTime}-{row.endTime}
                        </td>
                        <td className="px-3 py-1">
                          {row.courseCode}({row.sectionNumber})
                        </td>
                        <td className="px-3 py-1">{row.courseName}</td>
                        <td className="px-3 py-1">{row.classroomLabel}</td>
                        <td className="px-3 py-1">{row.instructorDisplay}</td>
                      </tr>
                    ))
                  )}
                </tbody>
              );
            })}
          </table>
        </CardContent>
      </Card>
      <p className="text-xs italic text-muted-foreground">
        Not: Uygulama saatleri yeşil renkle gösterilir
      </p>
    </div>
  );
}
