import { ProlizDayTable, type ProlizRow } from './ProlizDayTable';

interface ProlizWeekLayoutProps {
  rowsByDay: Record<number, ProlizRow[]>;
}

export function ProlizWeekLayout({ rowsByDay }: ProlizWeekLayoutProps) {
  return (
    <div className="space-y-4">
      <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
        <ProlizDayTable day={1} rows={rowsByDay[1] ?? []} />
        <ProlizDayTable day={2} rows={rowsByDay[2] ?? []} />
        <ProlizDayTable day={3} rows={rowsByDay[3] ?? []} />
        <ProlizDayTable day={4} rows={rowsByDay[4] ?? []} />
      </div>
      <ProlizDayTable day={5} rows={rowsByDay[5] ?? []} />
      <p className="text-xs italic text-muted-foreground">
        Not: Uygulama saatleri yeşil renkle gösterilir
      </p>
    </div>
  );
}
