import { useState, useMemo, useCallback, useEffect } from 'react';
import { AlertTriangle, ChevronLeft, ChevronRight, CalendarDays, UserPlus, Repeat2, LogOut, Grid3x3 } from 'lucide-react';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { cn } from '@/lib/utils';
import {
  useAcademicCalendarTerms,
  useStudentCalendar,
  type StudentAcademicCalendarDto,
  type TermCalendarEventDto,
} from '@/hooks/useAcademicCalendar';

type Cat = 'registration' | 'classes' | 'exam' | 'holiday' | 'other';

const CAT_STYLE: Record<Cat, { label: string; bg: string; border: string; text: string }> = {
  registration: { label: 'Kayıt', bg: 'bg-blue-50', border: 'border-blue-400', text: 'text-blue-900' },
  classes: { label: 'Dersler', bg: 'bg-emerald-50', border: 'border-emerald-400', text: 'text-emerald-900' },
  exam: { label: 'Sınavlar', bg: 'bg-amber-50', border: 'border-amber-400', text: 'text-amber-900' },
  holiday: { label: 'Tatiller', bg: 'bg-rose-50', border: 'border-rose-400', text: 'text-rose-900' },
  other: { label: 'Diğer', bg: 'bg-slate-50', border: 'border-slate-400', text: 'text-slate-900' },
};

function categorize(eventType: string): Cat {
  if (eventType.includes('Registration') || eventType.includes('AddDrop') || eventType.includes('Tuition')) return 'registration';
  if (eventType === 'ClassesStart' || eventType === 'ClassesEnd' || eventType.includes('Classes')) return 'classes';
  if (eventType.includes('Exam') || eventType.includes('Midterm') || eventType.includes('Final') || eventType.includes('Makeup')) return 'exam';
  if (eventType.includes('Holiday') || eventType === 'SemesterBreak' || eventType === 'ImportantDay') return 'holiday';
  return 'other';
}

function fmtDate(iso: string | null | undefined) {
  if (!iso) return '—';
  return new Date(iso).toLocaleDateString('tr-TR', { day: '2-digit', month: 'short', year: 'numeric' });
}

function PhaseCard({
  icon,
  accent,
  label,
  value,
  topBorder = false,
}: {
  icon: React.ReactNode;
  accent: string;
  label: string;
  value: string;
  topBorder?: boolean;
}) {
  return (
    <div className={cn('relative px-4 py-3 min-h-[64px] flex flex-col justify-center', topBorder && 'border-t lg:border-t-0 border-slate-100')}>
      <span className={cn('absolute left-0 top-1/2 -translate-y-1/2 w-1 h-8 rounded-r-full', accent)} aria-hidden="true" />
      <div className="flex items-center gap-1.5 text-[10px] font-semibold uppercase tracking-wider text-slate-500">
        {icon}
        <span>{label}</span>
      </div>
      <div className="mt-1 text-sm font-medium text-slate-800 truncate">{value}</div>
    </div>
  );
}

export default function AcademicCalendarPage() {
  const terms = useAcademicCalendarTerms();
  const [explicitTermId, setExplicitTermId] = useState<number | null>(null);
  const activeTerm = terms.data?.find(t => t.isActive && !t.isCancelled);
  const termId = explicitTermId ?? activeTerm?.termId ?? null;
  const calendar = useStudentCalendar(termId);
  const data: StudentAcademicCalendarDto | undefined = calendar.data;
  const selectedTermSummary = terms.data?.find(t => t.termId === termId);

  const [cursorDate, setCursorDate] = useState<Date>(() => new Date());
  const [viewMode, setViewMode] = useState<'year' | 'month'>('year');

  // Seçilen dönemin yılına otomatik navigasyon
  useEffect(() => {
    if (!data?.termStart) return;
    const termStart = new Date(data.termStart);
    setCursorDate(new Date(termStart.getFullYear(), termStart.getMonth(), 1));
  }, [data?.termStart]);

  const eventsByDay = useMemo(() => {
    const map = new Map<string, TermCalendarEventDto[]>();
    if (!data?.events) return map;
    for (const ev of data.events) {
      const start = new Date(ev.startDate);
      const end = ev.endDate ? new Date(ev.endDate) : start;
      const iter = new Date(start);
      while (iter <= end) {
        const key = iter.toISOString().slice(0, 10);
        if (!map.has(key)) map.set(key, []);
        map.get(key)!.push(ev);
        iter.setDate(iter.getDate() + 1);
      }
    }
    return map;
  }, [data?.events]);

  const prevMonth = useCallback(() => setCursorDate(d => new Date(d.getFullYear(), d.getMonth() - 1, 1)), []);
  const nextMonth = useCallback(() => setCursorDate(d => new Date(d.getFullYear(), d.getMonth() + 1, 1)), []);
  const prevYear = useCallback(() => setCursorDate(d => new Date(d.getFullYear() - 1, d.getMonth(), 1)), []);
  const nextYear = useCallback(() => setCursorDate(d => new Date(d.getFullYear() + 1, d.getMonth(), 1)), []);
  const openMonth = useCallback((monthIndex: number) => {
    setCursorDate(d => new Date(d.getFullYear(), monthIndex, 1));
    setViewMode('month');
  }, []);

  const monthLabel = cursorDate.toLocaleDateString('tr-TR', { month: 'long', year: 'numeric' });
  const yearLabel = cursorDate.getFullYear().toString();
  const firstOfMonth = new Date(cursorDate.getFullYear(), cursorDate.getMonth(), 1);
  const lastOfMonth = new Date(cursorDate.getFullYear(), cursorDate.getMonth() + 1, 0);
  const startDayOfWeek = (firstOfMonth.getDay() + 6) % 7;
  const daysInMonth = lastOfMonth.getDate();

  const hasCalendarData = Boolean(data && (data.registrationStart || data.addDropStart || data.events.length > 0));

  return (
    <div className="p-3 sm:p-4 space-y-3 w-full">
      <div className="flex flex-col lg:flex-row lg:items-stretch border border-slate-200 rounded-xl bg-white shadow-sm overflow-hidden">
        <div className="lg:w-72 lg:flex-shrink-0 bg-slate-50 lg:border-r border-slate-200">
          <label htmlFor="term-select" className="sr-only">Dönem</label>
          <Select value={termId?.toString() ?? ''} onValueChange={(v) => setExplicitTermId(Number(v))}>
            <SelectTrigger id="term-select" aria-label="Dönem seçimi" className="border-0 focus:ring-0 shadow-none h-full min-h-[64px] bg-transparent rounded-none px-4 font-semibold text-slate-800">
              <SelectValue placeholder="Dönem seçin..." />
            </SelectTrigger>
            <SelectContent>
              {terms.data?.map(t => (
                <SelectItem key={t.termId} value={t.termId.toString()}>
                  {t.termName} {t.isCancelled && '(İptal)'}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>

        <div className="flex-1 grid grid-cols-2 lg:grid-cols-4 divide-x divide-slate-100 border-t lg:border-t-0 border-slate-200">
          <PhaseCard
            icon={<CalendarDays className="w-4 h-4 text-indigo-500" />}
            accent="bg-indigo-500"
            label="Dönem"
            value={hasCalendarData && data ? `${fmtDate(data.termStart)} — ${fmtDate(data.termEnd)}` : '—'}
          />
          <PhaseCard
            icon={<UserPlus className="w-4 h-4 text-blue-500" />}
            accent="bg-blue-500"
            label="Kayıt"
            value={hasCalendarData && data ? `${fmtDate(data.registrationStart)} — ${fmtDate(data.registrationEnd)}` : '—'}
          />
          <PhaseCard
            icon={<Repeat2 className="w-4 h-4 text-emerald-500" />}
            accent="bg-emerald-500"
            label="Ekle / Bırak"
            value={hasCalendarData && data ? `${fmtDate(data.addDropStart)} — ${fmtDate(data.addDropEnd)}` : '—'}
            topBorder
          />
          <PhaseCard
            icon={<LogOut className="w-4 h-4 text-rose-500" />}
            accent="bg-rose-500"
            label="Ders Çekilme"
            value={hasCalendarData && data ? fmtDate(data.withdrawalDeadline) : '—'}
            topBorder
          />
        </div>
      </div>

      {selectedTermSummary && selectedTermSummary.hasCalendar === false && (
        <div className="flex items-center gap-2 p-4 bg-amber-50 border border-amber-200 rounded-lg text-amber-900">
          <AlertTriangle className="w-5 h-5 flex-shrink-0" />
          <span>Bu dönem için akademik takvim henüz tanımlı değil.</span>
        </div>
      )}

      {calendar.isLoading ? (
        <div className="border border-gray-200 rounded-xl bg-white shadow-sm p-10 text-center text-gray-400">
          Takvim yükleniyor...
        </div>
      ) : viewMode === 'year' ? (
        <div className="border border-gray-200 rounded-xl bg-white shadow-sm overflow-hidden">
          <div className="flex items-center justify-between px-4 py-2 border-b border-gray-100 bg-gray-50">
            <button onClick={prevYear} className="p-1.5 hover:bg-white rounded-md transition" aria-label="Önceki yıl">
              <ChevronLeft className="w-5 h-5" />
            </button>
            <div className="font-semibold text-lg text-slate-800">{yearLabel}</div>
            <button onClick={nextYear} className="p-1.5 hover:bg-white rounded-md transition" aria-label="Sonraki yıl">
              <ChevronRight className="w-5 h-5" />
            </button>
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-3 p-3">
            {Array.from({ length: 12 }).map((_, monthIndex) => (
              <MiniMonth
                key={monthIndex}
                year={cursorDate.getFullYear()}
                month={monthIndex}
                eventsByDay={eventsByDay}
                onClick={() => openMonth(monthIndex)}
              />
            ))}
          </div>

          {data && data.events.length > 0 && (
            <div className="flex flex-wrap gap-4 px-4 py-3 border-t border-gray-100 bg-gray-50">
              {(Object.entries(CAT_STYLE) as [Cat, typeof CAT_STYLE[Cat]][]).map(([, s]) => (
                <div key={s.label} className="flex items-center gap-1.5">
                  <div className={cn('w-4 h-3 rounded-sm border-l-[3px]', s.bg, s.border)} />
                  <span className="text-[11px] text-gray-500 font-medium">{s.label}</span>
                </div>
              ))}
            </div>
          )}

          {(!data || data.events.length === 0) && (
            <div className="px-4 py-3 text-center text-xs text-gray-400 border-t border-gray-100 bg-gray-50">
              Bu yıl için henüz etkinlik yok.
            </div>
          )}
        </div>
      ) : (
        <div className="border border-gray-200 rounded-xl bg-white shadow-sm overflow-hidden">
          <div className="flex items-center justify-between px-4 py-2 border-b border-gray-100 bg-gray-50">
            <div className="flex items-center gap-1">
              <button onClick={() => setViewMode('year')} className="flex items-center gap-1 px-2 py-1.5 text-xs font-medium text-slate-600 hover:bg-white rounded-md transition" aria-label="Yıllık görünüme dön">
                <Grid3x3 className="w-3.5 h-3.5" />
                Yıllık
              </button>
              <button onClick={prevMonth} className="p-1.5 hover:bg-white rounded-md transition" aria-label="Önceki ay">
                <ChevronLeft className="w-5 h-5" />
              </button>
            </div>
            <div className="font-semibold text-base sm:text-lg capitalize text-slate-800">{monthLabel}</div>
            <button onClick={nextMonth} className="p-1.5 hover:bg-white rounded-md transition" aria-label="Sonraki ay">
              <ChevronRight className="w-5 h-5" />
            </button>
          </div>

          <div className="grid grid-cols-7 text-center text-[11px] font-medium text-gray-500 uppercase border-b border-gray-100">
            {['Pzt', 'Sal', 'Çar', 'Per', 'Cum', 'Cts', 'Paz'].map(d => (
              <div key={d} className="py-2 border-r border-gray-100 last:border-r-0">{d}</div>
            ))}
          </div>

          <div className="grid grid-cols-7 auto-rows-fr">
            {Array.from({ length: startDayOfWeek }).map((_, i) => (
              <div key={`empty-${i}`} className="border-r border-b border-gray-100 last:border-r-0 min-h-[110px] sm:min-h-[130px] bg-gray-50/40" />
            ))}
            {Array.from({ length: daysInMonth }).map((_, i) => {
              const day = i + 1;
              const date = new Date(cursorDate.getFullYear(), cursorDate.getMonth(), day);
              const key = date.toISOString().slice(0, 10);
              const dayEvents = eventsByDay.get(key) ?? [];
              const isToday = new Date().toDateString() === date.toDateString();
              return (
                <div key={day} className="border-r border-b border-gray-100 last:border-r-0 min-h-[110px] sm:min-h-[130px] p-1.5 bg-white hover:bg-gray-50/40 transition">
                  <div className={cn('text-xs font-medium mb-1', isToday ? 'text-blue-600' : 'text-gray-600')}>
                    {isToday ? <span className="bg-blue-600 text-white rounded-full px-1.5 py-0.5 text-[10px]">{day}</span> : day}
                  </div>
                  <div className="space-y-0.5">
                    {dayEvents.slice(0, 3).map((ev, idx) => {
                      const cat = categorize(ev.eventType);
                      const style = CAT_STYLE[cat];
                      return (
                        <div key={idx} title={ev.title} className={cn('text-[10px] truncate px-1 py-0.5 border-l-2 rounded-sm', style.bg, style.border, style.text)}>
                          {ev.title}
                        </div>
                      );
                    })}
                    {dayEvents.length > 3 && (
                      <div className="text-[9px] text-gray-500 px-1">+{dayEvents.length - 3} daha</div>
                    )}
                  </div>
                </div>
              );
            })}
          </div>

          {data && data.events.length > 0 && (
            <div className="flex flex-wrap gap-4 px-4 py-3 border-t border-gray-100 bg-gray-50">
              {(Object.entries(CAT_STYLE) as [Cat, typeof CAT_STYLE[Cat]][]).map(([, s]) => (
                <div key={s.label} className="flex items-center gap-1.5">
                  <div className={cn('w-4 h-3 rounded-sm border-l-[3px]', s.bg, s.border)} />
                  <span className="text-[11px] text-gray-500 font-medium">{s.label}</span>
                </div>
              ))}
            </div>
          )}
        </div>
      )}
    </div>
  );
}

function MiniMonth({
  year,
  month,
  eventsByDay,
  onClick,
}: {
  year: number;
  month: number;
  eventsByDay: Map<string, TermCalendarEventDto[]>;
  onClick: () => void;
}) {
  const firstOfMonth = new Date(year, month, 1);
  const lastOfMonth = new Date(year, month + 1, 0);
  const startDayOfWeek = (firstOfMonth.getDay() + 6) % 7;
  const daysInMonth = lastOfMonth.getDate();
  const monthName = firstOfMonth.toLocaleDateString('tr-TR', { month: 'long' });

  const today = new Date();
  const isCurrentMonth = today.getFullYear() === year && today.getMonth() === month;

  return (
    <button
      onClick={onClick}
      className="group text-left border border-slate-200 rounded-lg bg-white hover:border-slate-400 hover:shadow-md transition overflow-hidden"
    >
      <div className={cn('px-3 py-2 border-b border-slate-100 flex items-center justify-between', isCurrentMonth ? 'bg-blue-50' : 'bg-slate-50')}>
        <span className={cn('text-sm font-semibold capitalize', isCurrentMonth ? 'text-blue-700' : 'text-slate-700')}>{monthName}</span>
        <span className="text-[10px] text-slate-400 group-hover:text-slate-600 transition">Detay →</span>
      </div>

      <div className="grid grid-cols-7 text-center text-[9px] font-medium text-slate-400 uppercase bg-slate-50/50">
        {['Pt', 'Sl', 'Çr', 'Pr', 'Cu', 'Ct', 'Pz'].map(d => (
          <div key={d} className="py-1">{d}</div>
        ))}
      </div>

      <div className="grid grid-cols-7 gap-0 p-1">
        {Array.from({ length: startDayOfWeek }).map((_, i) => (
          <div key={`empty-${i}`} className="aspect-square" />
        ))}
        {Array.from({ length: daysInMonth }).map((_, i) => {
          const day = i + 1;
          const date = new Date(year, month, day);
          const key = date.toISOString().slice(0, 10);
          const dayEvents = eventsByDay.get(key) ?? [];
          const isToday = today.toDateString() === date.toDateString();
          const cat = dayEvents.length > 0 ? categorize(dayEvents[0].eventType) : null;
          const style = cat ? CAT_STYLE[cat] : null;
          return (
            <div key={day} className={cn('aspect-square flex items-center justify-center relative text-[10px] font-medium rounded', style?.bg, style?.text)}>
              <span className={cn(isToday && 'bg-blue-600 text-white rounded-full w-5 h-5 flex items-center justify-center')}>{day}</span>
              {dayEvents.length > 1 && (
                <span className="absolute bottom-0.5 right-0.5 text-[7px] text-slate-500">+{dayEvents.length - 1}</span>
              )}
            </div>
          );
        })}
      </div>
    </button>
  );
}
