import type { TermCalendarEventDto } from '@/hooks/useAcademicCalendar';
import { CALENDAR_EVENT_LABELS } from '@/lib/calendarEventTypes';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';

interface Props {
  events: TermCalendarEventDto[];
}

function fmtDate(iso: string | null) {
  return iso ? new Date(iso).toLocaleDateString('tr-TR') : '—';
}

export function CalendarEventsPanel({ events }: Props) {
  if (events.length === 0) {
    return <p className="text-sm text-slate-500 italic">Bu dönem için takvim etkinliği tanımlı değil.</p>;
  }
  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>Tip</TableHead>
          <TableHead>Başlık</TableHead>
          <TableHead>Başlangıç</TableHead>
          <TableHead>Bitiş</TableHead>
          <TableHead>Not</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {events.map(e => (
          <TableRow key={e.id}>
            <TableCell>{CALENDAR_EVENT_LABELS[e.eventType] ?? e.eventType}</TableCell>
            <TableCell className="font-medium">{e.title}</TableCell>
            <TableCell>{fmtDate(e.startDate)}</TableCell>
            <TableCell>{fmtDate(e.endDate)}</TableCell>
            <TableCell className="text-slate-600 text-sm">{e.note ?? '—'}</TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );
}
