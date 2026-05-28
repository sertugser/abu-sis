import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs';
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
      <div className="border-b border-border/40 bg-muted/20 px-3 py-1.5">
        <p className="text-xs text-muted-foreground italic">Müfredat dersi yok.</p>
      </div>
    );
  }

  return (
    <div className="border-b border-border/40 bg-muted/20 px-3 py-1.5 flex items-center justify-between flex-wrap gap-2">
      {/* Parity pills — LEFT */}
      <div className="flex items-center gap-1">
        {PARITY_OPTIONS.map((opt) => (
          <button
            key={opt.value}
            type="button"
            onClick={() => onParityChange(opt.value)}
            className={cn(
              'inline-flex items-center rounded px-2.5 py-0.5 text-xs font-medium transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-1',
              parity === opt.value
                ? 'bg-primary text-primary-foreground'
                : 'bg-muted text-muted-foreground hover:bg-muted/70 hover:text-foreground',
            )}
            aria-pressed={parity === opt.value}
          >
            {opt.label}
          </button>
        ))}
      </div>

      {/* Class year tabs — RIGHT */}
      <Tabs
        value={String(selectedClassYear)}
        onValueChange={(v) => onClassYearChange(Number(v))}
      >
        <TabsList className="h-7 gap-0.5 bg-transparent p-0 rounded-none">
          {classYears.map((year) => (
            <TabsTrigger
              key={year}
              value={String(year)}
              className="rounded px-2.5 py-0.5 text-xs h-6 data-[state=active]:bg-background data-[state=active]:shadow-sm"
            >
              {year}. Sınıf
            </TabsTrigger>
          ))}
        </TabsList>
      </Tabs>
    </div>
  );
}
