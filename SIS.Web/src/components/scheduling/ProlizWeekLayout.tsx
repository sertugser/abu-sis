import { Badge } from '@/components/ui/badge';
import { cn } from '@/lib/utils';
import { dayNumberToTurkish } from '@/lib/scheduleUtils';
import type { ProlizRow } from './ProlizDayTable';

interface ProlizWeekLayoutProps {
  rowsByDay: Record<number, ProlizRow[]>;
}

const DAY_ORDER = [1, 2, 3, 4, 5];

const COLUMN_GRID =
  'grid grid-cols-[140px_minmax(120px,150px)_minmax(0,1fr)_minmax(110px,160px)_minmax(180px,260px)] gap-4';

export function ProlizWeekLayout({ rowsByDay }: ProlizWeekLayoutProps) {
  return (
    <div className="flex flex-1 flex-col">
      {/* Sütun başlıkları — bir kez */}
      <div
        className={cn(
          COLUMN_GRID,
          'border-y border-border bg-muted/40 px-6 py-2 text-[11px] font-semibold uppercase tracking-wider text-muted-foreground',
        )}
      >
        <span>Saat</span>
        <span>Ders Kodu</span>
        <span>Ders Adı</span>
        <span>Derslik</span>
        <span>Öğretim Elemanı</span>
      </div>

      {/* Günler eşit dağılım — az ders varsa satırlar büyür, çok ders varsa sıkışır */}
      <div className="flex flex-1 flex-col divide-y divide-border">
        {DAY_ORDER.map((day) => {
          const rows = rowsByDay[day] ?? [];
          return (
            <section key={day} className="flex flex-1 flex-col">
              {/* Gün başlığı */}
              <header className="flex flex-none items-center gap-3 px-6 py-2">
                <span className="h-4 w-1 rounded-full bg-primary" aria-hidden />
                <h2 className="text-sm font-semibold text-foreground">
                  {dayNumberToTurkish(day)}
                </h2>
                <span className="text-xs text-muted-foreground">
                  {rows.length === 0 ? 'ders yok' : `${rows.length} ders`}
                </span>
              </header>

              {/* Satırlar — flex-1 ile eşit pay alır */}
              {rows.length === 0 ? (
                <div className="flex flex-1 items-center px-6 pb-2 pl-10 text-xs italic text-muted-foreground">
                  Bu güne tanımlı ders programı bulunamadı.
                </div>
              ) : (
                <ul className="flex flex-1 flex-col divide-y divide-border/50">
                  {rows.map((row, idx) => (
                    <li
                      key={`${day}-${row.sectionId}-${idx}`}
                      className={cn(
                        COLUMN_GRID,
                        'group flex-1 items-center px-6 py-1.5 text-sm transition-colors hover:bg-muted/40',
                        row.isLab && 'bg-emerald-50/60 hover:bg-emerald-100/70',
                      )}
                      data-testid={row.isLab ? 'lab-row' : 'theory-row'}
                    >
                      <span className="font-mono text-xs tabular-nums text-foreground">
                        {row.startTime} – {row.endTime}
                      </span>
                      <span className="flex items-baseline gap-1">
                        <span className="font-medium text-foreground">
                          {row.courseCode}
                        </span>
                        <span className="text-xs text-muted-foreground">
                          · {row.sectionNumber}
                        </span>
                      </span>
                      <span className="truncate text-foreground" title={row.courseName}>
                        {row.courseName}
                      </span>
                      <span>
                        <Badge
                          variant="outline"
                          className="font-normal text-foreground"
                        >
                          {row.classroomLabel}
                        </Badge>
                      </span>
                      <span
                        className="truncate text-xs text-muted-foreground"
                        title={row.instructorDisplay}
                      >
                        {row.instructorDisplay || '—'}
                      </span>
                    </li>
                  ))}
                </ul>
              )}
            </section>
          );
        })}
      </div>

      {/* Lab açıklama şeridi — alta yapışır */}
      <div className="flex flex-none items-center gap-2 border-t border-border bg-muted/30 px-6 py-2 text-xs text-muted-foreground">
        <span
          className="inline-block h-2.5 w-2.5 rounded-sm bg-emerald-200"
          aria-hidden
        />
        Uygulama saatleri yeşil renkle gösterilir
      </div>
    </div>
  );
}
