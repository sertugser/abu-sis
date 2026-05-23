import { Badge } from '@/components/ui/badge';
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
    <div className="rounded-lg border border-slate-200 bg-white p-3 shadow-sm">
      <h2 className="text-sm font-semibold text-slate-900">Tatil tanımları</h2>
      <p className="text-xs text-slate-600">Sabit resmi tatiller, dini bayramlar ve bloklayıcı önemli günler.</p>
      <div className="mt-2 grid gap-2 md:grid-cols-2 xl:grid-cols-3">
        {isLoading ? (
          <p className="text-sm text-slate-500">Yükleniyor...</p>
        ) : holidays.length === 0 ? (
          <p className="text-sm text-slate-500">Gösterilecek tatil tanımı yok.</p>
        ) : holidays.map(holiday => (
          <div key={holiday.id} className="rounded-lg border border-slate-200 px-3 py-3 text-sm">
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
