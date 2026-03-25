import { useState } from 'react';
import { CalendarDays, ExternalLink } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent } from '@/components/ui/card';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { cn } from '@/lib/utils';
import {
  ACADEMIC_CALENDAR_SOURCE_URL,
  SPRING_2025_2026_CALENDAR,
  type AcademicCalendarEntry,
} from '../lib/academicCalendar';

const TODAY = new Date();
const ACTIVE_TERM_LABEL = '2025-2026 Bahar';
const HIGHLIGHT_COUNT = 4;

type EventStatus = 'active' | 'upcoming' | 'completed' | 'pending';

function getEventStatus(event: AcademicCalendarEntry): EventStatus {
  if (!event.startDate || !event.endDate) {
    return 'pending';
  }

  const start = new Date(`${event.startDate}T00:00:00+03:00`);
  const end = new Date(`${event.endDate}T23:59:59+03:00`);

  if (TODAY >= start && TODAY <= end) {
    return 'active';
  }

  if (TODAY < start) {
    return 'upcoming';
  }

  return 'completed';
}

function getDaysUntil(event: AcademicCalendarEntry): number | null {
  if (!event.startDate) return null;
  const start = new Date(`${event.startDate}T00:00:00+03:00`);
  const diff = start.getTime() - TODAY.getTime();
  return Math.ceil(diff / (1000 * 60 * 60 * 24));
}

export default function AcademicCalendarPage() {
  const [activeFilter, setActiveFilter] = useState<EventStatus | null>(null);

  const eventsWithStatus = SPRING_2025_2026_CALENDAR.map(event => ({
    event,
    status: getEventStatus(event),
  }));

  // Active + first upcoming events for the highlight block
  const highlightEvents = eventsWithStatus
    .filter(e => e.status === 'active' || e.status === 'upcoming')
    .slice(0, HIGHLIGHT_COUNT);

  const filteredEvents = activeFilter
    ? eventsWithStatus.filter(e => e.status === activeFilter)
    : eventsWithStatus;

  const toggleFilter = (status: EventStatus) => {
    setActiveFilter(prev => prev === status ? null : status);
  };

  return (
    <div className="space-y-6">

      <Card className="border border-[#003366] shadow-md overflow-hidden bg-card">
        <CardContent className="p-0">
          <div className="flex items-center justify-between px-5 py-3 bg-[#003366]">
            <div className="flex items-center gap-2.5">
              <CalendarDays className="h-5 w-5 text-white/90" />
              <h2 className="font-heading text-base font-extrabold tracking-tight text-white uppercase leading-none">
                Yaklaşan Etkinlikler
              </h2>
            </div>
            <a
              href={ACADEMIC_CALENDAR_SOURCE_URL}
              target="_blank"
              rel="noreferrer"
              className="inline-flex items-center gap-1.5 rounded-lg border border-white/20 px-3 py-1.5 text-xs font-semibold text-white/90 transition-colors hover:bg-white/10 hover:border-white/30 shrink-0"
            >
              Resmi kaynak
              <ExternalLink className="h-3.5 w-3.5" />
            </a>
          </div> 

          <div className="p-5">
            {highlightEvents.length === 0 ? (
              <p className="text-sm text-muted-foreground py-4 text-center">
                Yaklaşan etkinlik bulunmuyor.
              </p>
            ) : (
              <div className="grid gap-3 md:grid-cols-2">
                {highlightEvents.map(({ event, status }) => {
                  const days = getDaysUntil(event);
                  const isActive = status === 'active';

                  return (
                    <div
                      key={event.id}
                      className={cn(
                        'flex items-start gap-4 rounded-xl border p-4 transition-all duration-200 hover:shadow-sm',
                        isActive
                          ? 'border-success/30 bg-success/5'
                          : 'border-[#003366]/30 bg-card'
                      )}
                    >
                      <div className={cn(
                        'flex flex-col items-center justify-center rounded-lg px-3 py-2 min-w-[56px] text-center shrink-0 border shadow-inner',
                        isActive 
                          ? 'bg-success text-white border-success' 
                          : 'bg-sky-50 text-[#003366] border-[#003366]/40' // ANKARA BİLİM AÇIK MAVİSİ VE LACİVERT YAZI
                      )}>
                        {isActive ? (
                          <>
                            <span className="text-[10px] font-semibold uppercase leading-none">Şu an</span>
                            <span className="text-lg font-bold leading-tight mt-0.5">aktif</span>
                          </>
                        ) : days !== null ? (
                          <>
                            <span className="text-lg font-bold leading-tight">{days}</span>
                            <span className="text-[10px] font-semibold uppercase leading-none mt-0.5">gün</span>
                          </>
                        ) : (
                          <span className="text-xs font-semibold">—</span>
                        )}
                      </div>
                      <div className="min-w-0 flex-1">
                        <p className="font-semibold text-sm text-foreground leading-snug">{event.title}</p>
                        <p className="text-xs text-muted-foreground mt-1">
                          {event.startLabel} — {event.endLabel}
                        </p>
                      </div>
                    </div>
                  );
                })}
              </div>
            )}
          </div>
        </CardContent>
      </Card>

     
      <Card className="border border-[#003366] shadow-md overflow-hidden bg-card">
        
        <div className="px-5 py-3 bg-[#003366] flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between border-b border-white/10">
          <div>
            <h3 className="font-heading text-base font-extrabold text-white uppercase tracking-tight">
              {ACTIVE_TERM_LABEL} Dönemi
            </h3>
            <p className="text-[11px] text-white/70 font-medium">
              Tüm takvim kalemleri resmi ilan sırasına göre listelenmiştir.
            </p>
          </div>
          
         
          <div className="flex flex-wrap items-center gap-2">
            <FilterBadge
              label="Devam eden"
              variant="success"
              isActive={activeFilter === 'active'}
              count={eventsWithStatus.filter(e => e.status === 'active').length}
              onClick={() => toggleFilter('active')}
            />
            <FilterBadge
              label="Yaklaşan"
              variant="info"
              isActive={activeFilter === 'upcoming'}
              count={eventsWithStatus.filter(e => e.status === 'upcoming').length}
              onClick={() => toggleFilter('upcoming')}
            />
            <FilterBadge
              label="Tamamlandı"
              variant="secondary"
              isActive={activeFilter === 'completed'}
              count={eventsWithStatus.filter(e => e.status === 'completed').length}
              onClick={() => toggleFilter('completed')}
            />
            <FilterBadge
              label="Tarih bekleniyor"
              variant="outline"
              isActive={activeFilter === 'pending'}
              count={eventsWithStatus.filter(e => e.status === 'pending').length}
              onClick={() => toggleFilter('pending')}
            />
          </div>
        </div>

        <CardContent className="p-0">
          <div className="overflow-hidden">
            <Table>
              <TableHeader>
                <TableRow className="bg-muted/60 hover:bg-muted/60">
                  <TableHead className="w-[48%] font-bold">Takvim Adı</TableHead>
                  <TableHead className="font-bold">Başlangıç Tarihi</TableHead>
                  <TableHead className="font-bold">Bitiş Tarihi</TableHead>
                  <TableHead className="w-[140px] font-bold">Durum</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {filteredEvents.map(({ event, status }) => (
                  <TableRow
                    key={event.id}
                    className={cn(
                      'align-top',
                      status === 'active' && 'bg-success/5',
                      status === 'upcoming' && 'bg-info/5',
                      status === 'completed' && 'opacity-60',
                      status === 'pending' && 'bg-warning/5',
                    )}
                  >
                    <TableCell>
                      <div className="space-y-1">
                        <p className="font-medium text-foreground">{event.title}</p>
                        {event.note && (
                          <p className="text-xs text-muted-foreground">{event.note}</p>
                        )}
                      </div>
                    </TableCell>
                    <TableCell>{event.startLabel}</TableCell>
                    <TableCell>{event.endLabel}</TableCell>
                    <TableCell>
                      <StatusBadge status={status} />
                    </TableCell>
                  </TableRow>
                ))}
                {filteredEvents.length === 0 && (
                  <TableRow>
                    <TableCell colSpan={4} className="text-center py-8 text-muted-foreground">
                      Bu filtreye uygun etkinlik bulunamadı.
                    </TableCell>
                  </TableRow>
                )}
              </TableBody>
            </Table>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}

const FILTER_COLORS: Record<string, { active: string; inactive: string }> = {
  success: {
    active: 'bg-emerald-500 text-white shadow-md ring-2 ring-emerald-300',
    inactive: 'bg-white/15 text-white border border-white/30 hover:bg-white/25',
  },
  info: {
    active: 'bg-sky-500 text-white shadow-md ring-2 ring-sky-300',
    inactive: 'bg-white/15 text-white border border-white/30 hover:bg-white/25',
  },
  secondary: {
    active: 'bg-slate-400 text-white shadow-md ring-2 ring-slate-300',
    inactive: 'bg-white/15 text-white border border-white/30 hover:bg-white/25',
  },
  outline: {
    active: 'bg-amber-500 text-white shadow-md ring-2 ring-amber-300',
    inactive: 'bg-white/15 text-white border border-white/30 hover:bg-white/25',
  },
};

function FilterBadge({
  label,
  variant,
  isActive,
  count,
  onClick,
}: {
  label: string;
  variant: 'success' | 'info' | 'secondary' | 'outline';
  isActive: boolean;
  count: number;
  onClick: () => void;
}) {
  const colors = FILTER_COLORS[variant];

  return (
    <button
      type="button"
      onClick={onClick}
      className={cn(
        'rounded-lg px-3 py-1.5 text-sm font-semibold transition-colors duration-150',
        isActive ? colors.active : colors.inactive,
      )}
    >
      {label} ({count})
    </button>
  );
}

function StatusBadge({ status }: { status: EventStatus }) {
  if (status === 'active') {
    return <Badge variant="success">Devam ediyor</Badge>;
  }

  if (status === 'upcoming') {
    return <Badge variant="info">Yaklaşan</Badge>;
  }

  if (status === 'pending') {
    return <Badge variant="outline">Tarih bekleniyor</Badge>;
  }

  return <Badge variant="secondary">Tamamlandı</Badge>;
}
