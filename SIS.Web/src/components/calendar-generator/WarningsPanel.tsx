import { AlertTriangle, CheckCircle2 } from 'lucide-react';
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
  const isClear = rows.length === 0 && failedEvents.length === 0;
  return (
    <div className="rounded-xl border border-slate-200 bg-white p-3 shadow-sm">
      <h2 className="flex items-center gap-2 text-sm font-semibold text-slate-900">
        <AlertTriangle className="h-4 w-4 text-[#003366]" />
        Uyarılar
      </h2>
      <div className="mt-2 space-y-1.5">
        {failedEvents.length > 0 && (
          <div className="rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-800">
            <div className="font-semibold">Program seçtiğiniz kısıtlardan dolayı oluşturulamadı.</div>
            <div className="mt-1 text-xs">
              Yerleşemeyen etkinlikler: {failedEvents.map(event => `${semesterLabel(event.semester)} - ${displayText(event.title)}`).join(', ')}
            </div>
          </div>
        )}
        {isClear ? (
          <p className="flex items-center gap-2 text-sm text-emerald-700">
            <CheckCircle2 className="h-4 w-4" />
            Uyarı yok.
          </p>
        ) : rows.map(row => (
          <div key={row.key} className="rounded-lg border border-slate-200 px-3 py-2 text-sm transition-colors hover:border-slate-300 hover:bg-slate-50/60">
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
