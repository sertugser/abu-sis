import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { cn } from '@/lib/utils';
import { dayNumberToTurkish } from '@/lib/scheduleUtils';

export interface ProlizRow {
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

interface ProlizDayTableProps {
  day: number;
  rows: ProlizRow[];
}

export function ProlizDayTable({ day, rows }: ProlizDayTableProps) {
  return (
    <Card className="overflow-hidden">
      <CardHeader className="bg-muted/40 py-2">
        <CardTitle className="text-sm font-semibold">{dayNumberToTurkish(day)}</CardTitle>
      </CardHeader>
      <CardContent className="p-0">
        {rows.length === 0 ? (
          <p className="px-3 py-4 text-xs italic text-muted-foreground">
            Tanımlı Ders Programı Bulunamadı!
          </p>
        ) : (
          <table className="w-full text-xs">
            <thead className="border-b">
              <tr className="text-muted-foreground">
                <th className="px-2 py-1 text-left font-medium">Saat</th>
                <th className="px-2 py-1 text-left font-medium">Ders Kodu</th>
                <th className="px-2 py-1 text-left font-medium">Ders Adı</th>
                <th className="px-2 py-1 text-left font-medium">Derslik</th>
                <th className="px-2 py-1 text-left font-medium">Öğretim Elemanı</th>
              </tr>
            </thead>
            <tbody>
              {rows.map((row, idx) => (
                <tr
                  key={`${row.sectionId}-${idx}`}
                  className={cn('border-b last:border-0', row.isLab && 'bg-emerald-50')}
                  data-testid={row.isLab ? 'lab-row' : 'theory-row'}
                >
                  <td className="px-2 py-1">{row.startTime}-{row.endTime}</td>
                  <td className="px-2 py-1">{row.courseCode}({row.sectionNumber})</td>
                  <td className="px-2 py-1">{row.courseName}</td>
                  <td className="px-2 py-1">{row.classroomLabel}</td>
                  <td className="px-2 py-1">{row.instructorDisplay}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </CardContent>
    </Card>
  );
}
