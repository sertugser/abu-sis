import { Badge } from '@/components/ui/badge';
import { semesterLabel } from '@/lib/calendarGenerator/semesters';
import { displayText, type CalendarView } from '@/lib/calendarGenerator/types';
import { warningRows } from '@/lib/calendarGenerator/warnings';

interface WarningsPanelProps {
  current: CalendarView;
}

export function WarningsPanel({ current }: WarningsPanelProps) {
  const rows = warningRows(current);
  const failedEvents = current.events.filter(event => event.isFailed);
  return (
    <div className="rounded-lg border border-slate-200 bg-white p-3 shadow-sm">
      <h2 className="text-sm font-semibold text-slate-900">Uyarılar</h2>
      <div className="mt-2 space-y-1.5">
        {failedEvents.length > 0 && (
          <div className="rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-800">
            <div className="font-semibold">Program seçtiğiniz kısıtlardan dolayı oluşturulamadı.</div>
            <div className="mt-1 text-xs">
              Yerleşemeyen etkinlikler: {failedEvents.map(event => `${semesterLabel(event.semester)} - ${displayText(event.title)}`).join(', ')}
            </div>
          </div>
        )}
        {rows.length === 0 && failedEvents.length === 0 ? (
          <p className="text-sm text-slate-500">Uyarı yok.</p>
        ) : rows.map(row => (
          <div key={row.key} className="rounded-lg border border-slate-200 px-3 py-2 text-sm">
            <Badge variant={row.badgeVariant}>
              {row.badgeLabel}
            </Badge>
            <span className="ml-2 text-slate-700">{row.message}</span>
            {row.detail && (
              <div className="mt-1 text-xs text-slate-500">{row.detail}</div>
            )}
          </div>
        ))}
      </div>
    </div>
  );
}
