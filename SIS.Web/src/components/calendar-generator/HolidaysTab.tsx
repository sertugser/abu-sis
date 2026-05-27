import { CalendarDays } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { cn } from '@/lib/utils';
import { dateInput, fmtLong } from '@/lib/calendarGenerator/dates';
import { holidayDateText } from '@/lib/calendarGenerator/eventRules';
import { displayText } from '@/lib/calendarGenerator/types';
import type { HolidayDefinitionDto } from '@/hooks/useCalendarGenerator';

interface HolidaysTabProps {
  isLoading: boolean;
  holidays: HolidayDefinitionDto[];
}

export function HolidaysTab({ isLoading, holidays }: HolidaysTabProps) {
  return (
    <div className="flex min-h-0 flex-1 flex-col bg-white p-4">
      <h2 className="flex items-center gap-2 text-sm font-semibold text-slate-900">
        <CalendarDays className="h-4 w-4 text-[#003366]" />
        Tatil tanımları
      </h2>
      <p className="mt-0.5 text-xs text-slate-600">Sabit resmi tatiller, dini bayramlar ve bloklayıcı önemli günler.</p>
      <div className="mt-3 grid min-h-0 flex-1 auto-rows-min content-start gap-2.5 overflow-auto md:grid-cols-2 xl:grid-cols-3">
        {isLoading ? (
          <p className="text-sm text-slate-500">Yükleniyor...</p>
        ) : holidays.length === 0 ? (
          <p className="text-sm text-slate-500">Gösterilecek tatil tanımı yok.</p>
        ) : holidays.map(holiday => (
          <div
            key={holiday.id}
            className={cn(
              'rounded-lg border border-l-4 border-slate-200 px-3 py-3 text-sm transition-all duration-150 hover:-translate-y-0.5 hover:border-slate-300 hover:shadow-md',
              holiday.type === 'HijriCalculated' ? 'border-l-violet-400' : 'border-l-[#003366]',
            )}
          >
            <div className="flex items-center justify-between gap-2">
              <span className="font-semibold text-slate-800">{displayText(holiday.name)}</span>
              {holiday.type === 'HijriCalculated' && (
                <Badge variant="default">Hicri hesaplandı</Badge>
              )}
            </div>
            <div className="mt-2 font-medium text-slate-700">
              {holidayDateText(holiday)}
            </div>
            <div className="mt-1 text-xs text-slate-600">
              Süre: {holiday.durationDays} gün
            </div>
            {holiday.overrides.length > 0 && (
              <div className="mt-1 text-xs text-slate-500">
                Kaynak tarih: {holiday.overrides.map(o => `${o.year}: ${fmtLong(o.startDate)}${dateInput(o.startDate) === dateInput(o.endDate) ? '' : ` - ${fmtLong(o.endDate)}`}`).join(', ')}
              </div>
            )}
          </div>
        ))}
      </div>
    </div>
  );
}
