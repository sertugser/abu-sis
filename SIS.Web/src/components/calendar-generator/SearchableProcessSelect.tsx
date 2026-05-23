import { useMemo, useState } from 'react';
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
        className="flex h-10 w-full items-center justify-between gap-2 rounded-lg border border-slate-200 bg-white px-3 text-left text-sm text-slate-800 disabled:cursor-not-allowed disabled:opacity-60"
      >
        <span className="min-w-0 truncate">{selected ? processOptionLabel(selected) : placeholder}</span>
        <span className="shrink-0 text-slate-500">v</span>
      </button>

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
