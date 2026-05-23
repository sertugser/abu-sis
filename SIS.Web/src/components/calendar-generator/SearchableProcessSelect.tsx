import { useMemo, useState } from 'react';
import { ChevronDown, X } from 'lucide-react';
import { Input } from '@/components/ui/input';
import { cn } from '@/lib/utils';
import { filterProcessOptions, processOptionLabel } from '@/lib/calendarGenerator/processOptions';
import type { CalendarProcessOption } from '@/lib/calendarGenerator/types';

export function SearchableProcessSelect({
  options,
  selectedKey,
  onChange,
  disabled,
  placeholder = 'Etkinlik seçin',
}: {
  options: CalendarProcessOption[];
  selectedKey: string;
  onChange: (key: string) => void;
  disabled?: boolean;
  placeholder?: string;
}) {
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState('');
  const selected = options.find(option => option.key === selectedKey) ?? null;
  const filtered = useMemo(() => filterProcessOptions(options, query), [options, query]);

  function selectOption(key: string) {
    onChange(key);
    setOpen(false);
    setQuery('');
  }

  return (
    <div
      className="relative"
      onBlur={event => {
        if (!event.currentTarget.contains(event.relatedTarget as Node | null)) {
          setOpen(false);
        }
      }}
    >
      <button
        type="button"
        disabled={disabled}
        onClick={() => setOpen(prev => !prev)}
        className={cn(
          'flex h-10 w-full items-center justify-between gap-2 rounded-lg border bg-white px-3 text-left text-sm text-slate-800 transition-colors hover:border-slate-300 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[#003366]/30 disabled:cursor-not-allowed disabled:opacity-60',
          open ? 'border-[#003366]/40' : 'border-slate-200',
          selected ? 'pr-9' : '',
        )}
      >
        <span className={cn('min-w-0 truncate', !selected && 'text-slate-400')}>
          {selected ? processOptionLabel(selected) : placeholder}
        </span>
        {!selected && <ChevronDown className="h-4 w-4 shrink-0 text-slate-400" />}
      </button>

      {selected && !disabled && (
        <button
          type="button"
          onClick={event => {
            event.stopPropagation();
            onChange('');
            setQuery('');
          }}
          className="absolute right-2 top-1/2 flex h-6 w-6 -translate-y-1/2 items-center justify-center rounded-md text-slate-400 transition-colors hover:bg-slate-100 hover:text-slate-700"
          aria-label="Seçimi temizle"
          title="Seçimi temizle"
        >
          <X className="h-4 w-4" />
        </button>
      )}

      {open && (
        <div className="absolute left-0 right-0 top-11 z-30 rounded-lg border border-slate-200 bg-white p-2 shadow-lg">
          <Input
            autoFocus
            value={query}
            onChange={event => setQuery(event.target.value)}
            placeholder="Ara..."
            className="h-9"
          />
          <div className="mt-2 max-h-64 overflow-auto">
            {filtered.length === 0 ? (
              <div className="px-3 py-2 text-sm text-slate-500">Sonuç bulunamadı.</div>
            ) : filtered.map(option => (
              <button
                key={option.key}
                type="button"
                onMouseDown={event => event.preventDefault()}
                onClick={() => selectOption(option.key)}
                className={cn(
                  'block w-full rounded-md px-3 py-2 text-left text-sm hover:bg-cyan-50',
                  selectedKey === option.key ? 'bg-cyan-50 font-semibold text-cyan-900' : 'text-slate-700',
                )}
              >
                <span className="block truncate">{processOptionLabel(option)}</span>
              </button>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}
