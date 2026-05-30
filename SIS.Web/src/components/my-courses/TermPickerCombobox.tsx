import { useMemo, useState } from 'react';
import { ChevronDown, Search } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { cn } from '@/lib/utils';

interface TermOption {
  termId: number;
  termName: string;
}

interface TermPickerComboboxProps {
  terms: TermOption[];
  selectedTermId: number | null;
  onSelect: (termId: number) => void;
  placeholder?: string;
  emptyMessage?: string;
  className?: string;
}

/**
 * Aramalı dönem seçici. Proliz davranışına paralel: Popover + canlı filtre.
 *
 * Ortak component — Alınan Dersler (MyCoursesPage) ve Not Listesi (GradeListPage)
 * ikisi de aynı veri kaynağını (CourseHistoryTermDto) kullanır, dolayısıyla aynı seçiciyi.
 * İkibaşlılığı önlemek için tek yerde tanımlıdır.
 */
export function TermPickerCombobox({
  terms,
  selectedTermId,
  onSelect,
  placeholder = 'Dönem seçin',
  emptyMessage = 'Eşleşen dönem bulunamadı',
  className,
}: TermPickerComboboxProps) {
  const [open, setOpen] = useState(false);
  const [filter, setFilter] = useState('');

  const selectedTerm = useMemo(
    () => terms.find(t => t.termId === selectedTermId) ?? null,
    [terms, selectedTermId],
  );

  const filtered = useMemo(() => {
    const q = filter.trim().toLocaleLowerCase('tr-TR');
    if (!q) return terms;
    return terms.filter(t => t.termName.toLocaleLowerCase('tr-TR').includes(q));
  }, [terms, filter]);

  return (
    <Popover
      open={open}
      onOpenChange={(value) => {
        setOpen(value);
        if (!value) setFilter('');
      }}
    >
      <PopoverTrigger asChild>
        <Button
          variant="outline"
          role="combobox"
          aria-expanded={open}
          className={cn('h-8 w-full max-w-xs justify-between bg-card text-sm font-normal', className)}
        >
          <span className="truncate">{selectedTerm?.termName ?? placeholder}</span>
          <ChevronDown className="ml-2 h-4 w-4 shrink-0 text-muted-foreground" />
        </Button>
      </PopoverTrigger>
      <PopoverContent className="w-[var(--radix-popover-trigger-width)] p-0" align="start">
        <div className="border-b p-2">
          <div className="relative">
            <Search className="absolute left-2 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-muted-foreground" />
            <Input
              placeholder="Dönem ara…"
              value={filter}
              onChange={(e) => setFilter(e.target.value)}
              className="h-8 pl-7 text-sm"
              autoFocus
            />
          </div>
        </div>
        <div className="max-h-64 overflow-y-auto py-1">
          {filtered.length === 0 ? (
            <p className="px-3 py-4 text-center text-xs text-muted-foreground">{emptyMessage}</p>
          ) : (
            filtered.map((t) => (
              <button
                key={t.termId}
                type="button"
                onClick={() => {
                  onSelect(t.termId);
                  setOpen(false);
                  setFilter('');
                }}
                className={cn(
                  'flex w-full items-center justify-between px-3 py-1.5 text-left text-sm hover:bg-accent',
                  t.termId === selectedTermId && 'bg-accent/60 font-semibold',
                )}
              >
                <span>{t.termName}</span>
                {t.termId === selectedTermId && (
                  <span className="text-[10px] text-muted-foreground">Seçili</span>
                )}
              </button>
            ))
          )}
        </div>
      </PopoverContent>
    </Popover>
  );
}
