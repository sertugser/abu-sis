import { useEffect, useMemo, useRef, useState } from 'react';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Switch } from '@/components/ui/switch';
import { Button } from '@/components/ui/button';
import { X, Loader2 } from 'lucide-react';
import { useAdminCourses } from '../../../hooks/useAdminCourseCatalog';
import { useDebounce } from '../../../hooks/useDebounce';
import type { CourseIntibakState } from './types';

interface Props {
  value: CourseIntibakState;
  onChange: (next: CourseIntibakState) => void;
  /** Current course's id — excluded from dropdown options to prevent self-reference */
  currentCourseId: number;
  /** Department to scope search to when "Tüm bölümler" is unchecked */
  departmentId: number | null;
}

const MAX_RESULTS = 8;
const DEBOUNCE_MS = 200;

export default function IntibakSection({
  value, onChange, currentCourseId, departmentId,
}: Props) {
  const [search, setSearch] = useState('');
  const [allDepartments, setAllDepartments] = useState(false);
  const [open, setOpen] = useState(false);
  const containerRef = useRef<HTMLDivElement | null>(null);

  const debouncedSearch = useDebounce(search, DEBOUNCE_MS);

  // Only query when there's an active search and no current selection (or user is searching for change)
  const shouldQuery = open && debouncedSearch.trim().length >= 2;
  const queryFilter = useMemo(() => ({
    search: debouncedSearch.trim(),
    departmentId: !allDepartments && departmentId !== null ? departmentId : undefined,
  }), [debouncedSearch, allDepartments, departmentId]);

  const { data: results = [], isFetching } = useAdminCourses(shouldQuery ? queryFilter : undefined);

  // Filter own course out + cap at MAX_RESULTS
  const options = useMemo(
    () => results
      .filter(c => c.id !== currentCourseId)
      .slice(0, MAX_RESULTS),
    [results, currentCourseId],
  );

  // Close dropdown on outside click
  useEffect(() => {
    if (!open) return;
    function handler(e: MouseEvent) {
      if (containerRef.current && !containerRef.current.contains(e.target as Node)) {
        setOpen(false);
      }
    }
    document.addEventListener('mousedown', handler);
    return () => document.removeEventListener('mousedown', handler);
  }, [open]);

  function handleSelect(course: { id: number; code: string; name: string }) {
    onChange({
      previousCourseId: course.id,
      previousCourseCode: course.code,
      previousCourseName: course.name,
    });
    setSearch('');
    setOpen(false);
  }

  function handleClear() {
    onChange({
      previousCourseId: null,
      previousCourseCode: null,
      previousCourseName: null,
    });
  }

  const hasSelection = value.previousCourseId !== null;

  return (
    <fieldset className="space-y-3">
      <legend className="text-sm font-semibold mb-2">İntibak (Son Aktif Ders Kodu)</legend>

      {hasSelection ? (
        <div className="flex items-center justify-between gap-2 rounded-md border border-border bg-muted/30 px-3 py-2">
          <div className="flex flex-col min-w-0">
            <span className="font-mono text-sm font-semibold text-foreground">
              {value.previousCourseCode}
            </span>
            <span className="text-xs text-muted-foreground truncate">
              {value.previousCourseName}
            </span>
          </div>
          <Button
            type="button"
            variant="ghost"
            size="sm"
            className="h-8 w-8 p-0 shrink-0"
            onClick={handleClear}
            title="İntibakı iptal et"
          >
            <X className="h-4 w-4" />
          </Button>
        </div>
      ) : (
        <div ref={containerRef} className="relative">
          <Input
            value={search}
            onChange={e => { setSearch(e.target.value); setOpen(true); }}
            onFocus={() => setOpen(true)}
            placeholder="Ders kodu veya adı ile ara (en az 2 karakter)..."
            className="text-sm"
          />
          {open && debouncedSearch.trim().length >= 2 && (
            <div className="absolute z-10 mt-1 w-full rounded-md border border-border bg-popover shadow-md max-h-72 overflow-y-auto">
              {isFetching ? (
                <div className="px-3 py-3 text-xs text-muted-foreground flex items-center gap-2">
                  <Loader2 className="h-3 w-3 animate-spin" />
                  Aranıyor…
                </div>
              ) : options.length === 0 ? (
                <div className="px-3 py-3 text-xs text-muted-foreground">
                  Eşleşen ders bulunamadı.
                </div>
              ) : (
                options.map(c => (
                  <button
                    type="button"
                    key={c.id}
                    onClick={() => handleSelect(c)}
                    className="w-full text-left px-3 py-2 hover:bg-accent transition-colors flex flex-col"
                  >
                    <span className="font-mono text-xs font-semibold text-foreground">
                      {c.code}
                    </span>
                    <span className="text-xs text-muted-foreground truncate">
                      {c.name}
                    </span>
                  </button>
                ))
              )}
            </div>
          )}
        </div>
      )}

      <div className="flex items-center justify-between gap-2 pt-1">
        <div className="flex flex-col">
          <Label className="text-xs">Tüm bölümler arasından ara</Label>
          <span className="text-[11px] text-muted-foreground">
            Kapalıyken sadece dersin bölümünden öneri gelir.
          </span>
        </div>
        <Switch checked={allDepartments} onCheckedChange={setAllDepartments} />
      </div>
    </fieldset>
  );
}
