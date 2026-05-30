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
      <div className="border-b bg-card px-3 pt-2 flex items-center">
        <p className="text-xs text-muted-foreground italic">Müfredat dersi yok.</p>
      </div>
    );
  }

  return (
    <div className="border-b bg-card px-3 pt-2 flex items-center justify-between gap-4 flex-wrap">
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
                'pb-1.5 text-xs transition-colors',
                active
                  ? 'border-b-2 border-primary text-primary font-semibold'
                  : 'border-b-2 border-transparent text-muted-foreground hover:text-foreground hover:border-border',
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
                'pb-1.5 text-xs transition-colors',
                active
                  ? 'border-b-2 border-primary text-primary font-semibold'
                  : 'border-b-2 border-transparent text-muted-foreground hover:text-foreground hover:border-border',
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
