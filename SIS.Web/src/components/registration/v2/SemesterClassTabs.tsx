import { cn } from '@/lib/utils';

export type ParityFilter = 'Tumu' | 'Guz' | 'Bahar';

interface SemesterClassTabsProps {
  classYears: number[];
  selectedClassYear: number;
  onClassYearChange: (year: number) => void;
  parity: ParityFilter;
  onParityChange: (p: ParityFilter) => void;
}

const PARITY_OPTIONS: { value: ParityFilter; label: string }[] = [
  { value: 'Tumu', label: 'Tümü' },
  { value: 'Guz', label: 'Güz' },
  { value: 'Bahar', label: 'Bahar' },
];

export function SemesterClassTabs({
  classYears,
  selectedClassYear,
  onClassYearChange,
  parity,
  onParityChange,
}: SemesterClassTabsProps) {
  if (classYears.length === 0) {
    return (
      <div className="border-b border-slate-200 bg-white px-3 pt-2 flex items-center">
        <p className="text-[12px] text-slate-500 italic">Müfredat dersi yok.</p>
      </div>
    );
  }

  return (
    <div className="border-b border-slate-200 bg-white px-3 pt-2 flex items-center justify-between gap-4 flex-wrap">
      {/* Parity underline tabs — LEFT */}
      <div className="flex items-center gap-4">
        {PARITY_OPTIONS.map((opt) => {
          const active = parity === opt.value;
          return (
            <button
              key={opt.value}
              type="button"
              onClick={() => onParityChange(opt.value)}
              className={cn(
                'pb-1.5 text-[12px] transition-colors',
                active
                  ? 'border-b-2 border-blue-600 text-blue-700 font-semibold'
                  : 'border-b-2 border-transparent text-slate-600 hover:text-slate-900 hover:border-slate-300',
              )}
            >
              {opt.label}
            </button>
          );
        })}
      </div>

      {/* Class year underline tabs — RIGHT */}
      <div className="flex items-center gap-4">
        {classYears.map((year) => {
          const active = year === selectedClassYear;
          return (
            <button
              key={year}
              type="button"
              onClick={() => onClassYearChange(year)}
              className={cn(
                'pb-1.5 text-[12px] transition-colors',
                active
                  ? 'border-b-2 border-blue-600 text-blue-700 font-semibold'
                  : 'border-b-2 border-transparent text-slate-600 hover:text-slate-900 hover:border-slate-300',
              )}
            >
              {year}. Sınıf
            </button>
          );
        })}
      </div>
    </div>
  );
}
