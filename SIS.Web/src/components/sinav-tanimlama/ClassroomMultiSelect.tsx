import { useState } from 'react';
import { ChevronDown, X } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import type { ExamConfigClassroom } from '../../types/sectionExam';

interface ClassroomMultiSelectProps {
  classrooms: ExamConfigClassroom[];
  value: number[];
  onChange: (ids: number[]) => void;
}

export function ClassroomMultiSelect({ classrooms, value, onChange }: ClassroomMultiSelectProps) {
  const [open, setOpen] = useState(false);

  const selectedNames = classrooms
    .filter((c) => value.includes(c.id))
    .map((c) => c.name)
    .join(', ');

  function toggle(id: number) {
    if (value.includes(id)) {
      onChange(value.filter((v) => v !== id));
    } else {
      onChange([...value, id]);
    }
  }

  function clearAll() {
    onChange([]);
  }

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <Button
          type="button"
          variant="outline"
          className="h-9 w-full justify-between text-sm font-normal"
        >
          <span className="truncate">
            {selectedNames || <span className="text-muted-foreground">Derslik seç…</span>}
          </span>
          <div className="flex items-center gap-1 shrink-0 ml-2">
            {value.length > 0 && (
              <span
                role="button"
                tabIndex={0}
                className="inline-flex items-center"
                onKeyDown={(e) => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); clearAll(); } }}
                onClick={(e) => { e.stopPropagation(); clearAll(); }}
                aria-label="Seçimi temizle"
              >
                <X className="h-3.5 w-3.5 text-muted-foreground hover:text-foreground" />
              </span>
            )}
            <ChevronDown className="h-4 w-4 text-muted-foreground" />
          </div>
        </Button>
      </PopoverTrigger>
      <PopoverContent className="w-72 p-0" align="start">
        <div className="max-h-60 overflow-y-auto p-1">
          {classrooms.length === 0 ? (
            <p className="px-3 py-4 text-center text-sm text-muted-foreground">Derslik yok</p>
          ) : (
            classrooms.map((classroom) => (
              <label
                key={classroom.id}
                className="flex cursor-pointer items-center gap-2.5 rounded px-3 py-2 text-sm hover:bg-muted/60"
              >
                <input
                  type="checkbox"
                  checked={value.includes(classroom.id)}
                  onChange={() => toggle(classroom.id)}
                  className="h-4 w-4 rounded border-border accent-primary"
                />
                <div className="min-w-0">
                  <span className="font-medium">{classroom.name}</span>
                  {classroom.buildingName && (
                    <span className="ml-1.5 text-xs text-muted-foreground">{classroom.buildingName}</span>
                  )}
                  <span className="ml-1.5 text-xs text-muted-foreground">· {classroom.capacity} kişi</span>
                </div>
              </label>
            ))
          )}
        </div>
        {value.length > 0 && (
          <div className="border-t px-3 py-2">
            <button
              type="button"
              className="text-xs text-muted-foreground hover:text-foreground"
              onClick={clearAll}
            >
              Seçimi temizle ({value.length})
            </button>
          </div>
        )}
      </PopoverContent>
    </Popover>
  );
}
