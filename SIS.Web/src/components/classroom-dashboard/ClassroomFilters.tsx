import { X } from 'lucide-react';
import { classroomTypeLabel } from '@/lib/classroomLabels';
import type { ClassroomFilterOptions } from '@/types/scheduling';

export interface ClassroomFilterState {
  facultyId: number | null;
  buildingName: string | null;
  classroomType: string | null;
  minCapacity: number | null;
  maxCapacity: number | null;
}

// eslint-disable-next-line react-refresh/only-export-components -- co-located filter default; HMR impact acceptable
export const EMPTY_FILTERS: ClassroomFilterState = {
  facultyId: null,
  buildingName: null,
  classroomType: null,
  minCapacity: null,
  maxCapacity: null,
};

const CAPACITY_RANGES = [
  { label: 'Tümünü Göster', min: null, max: null },
  { label: '0-30', min: 0, max: 30 },
  { label: '31-60', min: 31, max: 60 },
  { label: '61-100', min: 61, max: 100 },
  { label: '100+', min: 101, max: null },
];

interface ClassroomFiltersProps {
  filters: ClassroomFilterState;
  onChange: (filters: ClassroomFilterState) => void;
  options: ClassroomFilterOptions | undefined;
}

export default function ClassroomFilters({ filters, onChange, options }: ClassroomFiltersProps) {
  const activeChips: { label: string; onRemove: () => void }[] = [];

  if (filters.facultyId && options) {
    const fac = options.faculties.find(f => f.id === filters.facultyId);
    if (fac) activeChips.push({
      label: fac.name,
      onRemove: () => onChange({ ...filters, facultyId: null, buildingName: null }),
    });
  }
  if (filters.buildingName) {
    activeChips.push({
      label: filters.buildingName,
      onRemove: () => onChange({ ...filters, buildingName: null }),
    });
  }
  if (filters.classroomType) {
    activeChips.push({
      label: classroomTypeLabel(filters.classroomType),
      onRemove: () => onChange({ ...filters, classroomType: null }),
    });
  }
  if (filters.minCapacity !== null || filters.maxCapacity !== null) {
    const range = CAPACITY_RANGES.find(r => r.min === filters.minCapacity && r.max === filters.maxCapacity);
    activeChips.push({
      label: `Kapasite: ${range?.label ?? `${filters.minCapacity ?? 0}+`}`,
      onRemove: () => onChange({ ...filters, minCapacity: null, maxCapacity: null }),
    });
  }

  return (
    <div className="space-y-2">
      <div className="flex flex-wrap gap-2">
        <select
          value={filters.facultyId ?? ''}
          onChange={e => onChange({
            ...filters,
            facultyId: e.target.value ? Number(e.target.value) : null,
            buildingName: null,
          })}
          className="h-9 rounded-lg border bg-white px-3 text-sm"
        >
          <option value="">Tüm Fakülteler</option>
          {options?.faculties.map(f => (
            <option key={f.id} value={f.id}>{f.name}</option>
          ))}
        </select>

        <select
          value={filters.buildingName ?? ''}
          onChange={e => onChange({ ...filters, buildingName: e.target.value || null })}
          className="h-9 rounded-lg border bg-white px-3 text-sm"
        >
          <option value="">Tüm Binalar</option>
          {(options?.buildings ?? []).map(b => (
            <option key={b} value={b}>{b}</option>
          ))}
        </select>

        <select
          value={filters.classroomType ?? ''}
          onChange={e => onChange({ ...filters, classroomType: e.target.value || null })}
          className="h-9 rounded-lg border bg-white px-3 text-sm"
        >
          <option value="">Tüm Tipler</option>
          {(options?.classroomTypes ?? []).map(t => (
            <option key={t} value={t}>{classroomTypeLabel(t)}</option>
          ))}
        </select>

        <select
          value={filters.minCapacity !== null ? `${filters.minCapacity}-${filters.maxCapacity ?? ''}` : ''}
          onChange={e => {
            if (!e.target.value) {
              onChange({ ...filters, minCapacity: null, maxCapacity: null });
              return;
            }
            const range = CAPACITY_RANGES.find(r => `${r.min}-${r.max ?? ''}` === e.target.value);
            onChange({
              ...filters,
              minCapacity: range?.min ?? null,
              maxCapacity: range?.max ?? null,
            });
          }}
          className="h-9 rounded-lg border bg-white px-3 text-sm"
        >
          {CAPACITY_RANGES.map(r => (
            <option key={r.label} value={r.min !== null ? `${r.min}-${r.max ?? ''}` : ''}>
              {r.label}
            </option>
          ))}
        </select>
      </div>

      {activeChips.length > 0 && (
        <div className="flex flex-wrap gap-1.5">
          {activeChips.map(chip => (
            <span key={chip.label} className="inline-flex items-center gap-1 px-2.5 py-1 rounded-full bg-[#003366]/10 text-[#003366] text-xs font-medium">
              {chip.label}
              <button onClick={chip.onRemove} className="hover:bg-[#003366]/20 rounded-full p-0.5">
                <X className="h-3 w-3" />
              </button>
            </span>
          ))}
          <button
            onClick={() => onChange(EMPTY_FILTERS)}
            className="text-xs text-muted-foreground hover:text-foreground underline"
          >
            Tümü Temizle
          </button>
        </div>
      )}
    </div>
  );
}
