import { Search, CalendarClock } from 'lucide-react';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Label } from '@/components/ui/label';
import type { FilterType } from '../types';

interface TransferActionToolbarProps {
  searchQuery: string;
  onSearchChange: (query: string) => void;

  filterType: FilterType;
  onFilterChange: (filter: FilterType) => void;

  selectedCount: number;
  onSelectAll: () => void;
  onSelectCompulsory: () => void;
  onClearSelection: () => void;

  includeSchedule: boolean;
  onIncludeScheduleChange: (value: boolean) => void;

  onTransferClick: () => void;
  isPending: boolean;
  canTransfer: boolean;
}

const FILTERS: ReadonlyArray<readonly [FilterType, string]> = [
  ['all', 'Tümü'],
  ['compulsory', 'Zorunlu'],
  ['elective', 'Seçmeli'],
];

export function TransferActionToolbar({
  searchQuery,
  onSearchChange,
  filterType,
  onFilterChange,
  selectedCount,
  onSelectAll,
  onSelectCompulsory,
  onClearSelection,
  includeSchedule,
  onIncludeScheduleChange,
  onTransferClick,
  isPending,
  canTransfer,
}: TransferActionToolbarProps) {
  return (
    <Card>
      <CardContent className="pt-3 pb-3">
        <div className="flex items-center gap-2.5 flex-wrap">
          <div className="relative w-[220px]">
            <Search className="absolute left-2.5 top-1/2 -translate-y-1/2 h-3.5 w-3.5 text-muted-foreground" />
            <Input
              placeholder="Kod, isim veya hoca..."
              value={searchQuery}
              onChange={(e) => onSearchChange(e.target.value)}
              className="pl-8 h-8 text-sm"
            />
          </div>

          <div className="h-5 w-px bg-border" />

          <div className="flex items-center gap-0.5 bg-muted rounded-md p-0.5">
            {FILTERS.map(([key, label]) => (
              <button
                key={key}
                onClick={() => onFilterChange(key)}
                className={`px-2.5 py-1 text-xs rounded transition-all ${
                  filterType === key
                    ? 'bg-background text-foreground shadow-sm font-medium'
                    : 'text-muted-foreground hover:text-foreground'
                }`}
              >
                {label}
              </button>
            ))}
          </div>

          <div className="h-5 w-px bg-border" />

          <Button variant="outline" size="sm" className="h-8 text-xs" onClick={onSelectAll}>
            Tümünü Seç
          </Button>
          <Button variant="outline" size="sm" className="h-8 text-xs" onClick={onSelectCompulsory}>
            Zorunluları Seç
          </Button>
          {selectedCount > 0 && (
            <Button variant="ghost" size="sm" className="h-8 text-xs" onClick={onClearSelection}>
              Temizle
            </Button>
          )}

          <div className="ml-auto flex items-center gap-3">
            <label
              htmlFor="include-schedule"
              className="flex items-center gap-1.5 text-xs cursor-pointer select-none"
              title="Geçen dönemin gün/saat/derslik bilgisini de aktarır. Çakışma varsa o şube schedule'sız kalır."
            >
              <input
                id="include-schedule"
                type="checkbox"
                checked={includeSchedule}
                onChange={(e) => onIncludeScheduleChange(e.target.checked)}
                className="h-3.5 w-3.5 cursor-pointer"
              />
              <CalendarClock className="h-3.5 w-3.5" />
              <Label htmlFor="include-schedule" className="text-xs cursor-pointer">
                Programı da kopyala
              </Label>
            </label>
            <div className="h-5 w-px bg-border" />
            <span className="text-xs text-muted-foreground whitespace-nowrap">{selectedCount} seçili</span>
            <Button
              size="sm"
              className="h-8 text-xs"
              onClick={onTransferClick}
              disabled={selectedCount === 0 || isPending || !canTransfer}
            >
              {isPending ? 'Aktarılıyor...' : `Aktar (${selectedCount})`}
            </Button>
          </div>
        </div>
      </CardContent>
    </Card>
  );
}
