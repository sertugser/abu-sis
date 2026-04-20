import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import {
  Popover, PopoverContent, PopoverTrigger,
} from '@/components/ui/popover';
import { Bookmark, Plus, Trash2 } from 'lucide-react';

export interface FilterPreset {
  name: string;
  filters: Record<string, string>;
}

const STORAGE_KEY = 'audit-log-filter-presets';

function loadPresets(): FilterPreset[] {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    return raw ? JSON.parse(raw) : [];
  } catch {
    return [];
  }
}

function savePresets(presets: FilterPreset[]) {
  localStorage.setItem(STORAGE_KEY, JSON.stringify(presets));
}

interface FilterPresetsProps {
  currentFilters: Record<string, string>;
  onApplyPreset: (filters: Record<string, string>) => void;
}

export function FilterPresets({ currentFilters, onApplyPreset }: FilterPresetsProps) {
  const [presets, setPresets] = useState<FilterPreset[]>(loadPresets);
  const [newName, setNewName] = useState('');
  const [open, setOpen] = useState(false);

  const hasFilters = Object.values(currentFilters).some(Boolean);

  function handleSave() {
    if (!newName.trim() || !hasFilters) return;
    const updated = [...presets, { name: newName.trim(), filters: { ...currentFilters } }];
    setPresets(updated);
    savePresets(updated);
    setNewName('');
  }

  function handleDelete(index: number) {
    const updated = presets.filter((_, i) => i !== index);
    setPresets(updated);
    savePresets(updated);
  }

  function handleApply(preset: FilterPreset) {
    onApplyPreset(preset.filters);
    setOpen(false);
  }

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <Button variant="outline" size="sm">
          <Bookmark className="h-3.5 w-3.5 mr-1.5" />
          Kayıtlı Filtreler
          {presets.length > 0 && (
            <span className="ml-1 text-[10px] text-muted-foreground">({presets.length})</span>
          )}
        </Button>
      </PopoverTrigger>
      <PopoverContent className="w-64 p-3" align="end">
        <div className="space-y-3">
          <p className="text-xs font-semibold text-muted-foreground uppercase tracking-wider">Kayıtlı Filtreler</p>

          {presets.length === 0 && (
            <p className="text-xs text-muted-foreground">Henüz kayıtlı filtre yok.</p>
          )}

          {presets.map((preset, i) => (
            <div key={i} className="flex items-center gap-1.5">
              <Button
                variant="ghost"
                size="sm"
                className="flex-1 justify-start text-xs h-7"
                onClick={() => handleApply(preset)}
              >
                {preset.name}
              </Button>
              <Button
                variant="ghost"
                size="icon"
                className="h-6 w-6 text-muted-foreground hover:text-destructive"
                onClick={() => handleDelete(i)}
              >
                <Trash2 className="h-3 w-3" />
              </Button>
            </div>
          ))}

          {hasFilters && (
            <div className="flex gap-1.5 pt-2 border-t">
              <Input
                className="h-7 text-xs"
                placeholder="Preset adı..."
                value={newName}
                onChange={e => setNewName(e.target.value)}
                onKeyDown={e => e.key === 'Enter' && handleSave()}
              />
              <Button size="sm" className="h-7 px-2" onClick={handleSave} disabled={!newName.trim()}>
                <Plus className="h-3 w-3" />
              </Button>
            </div>
          )}
        </div>
      </PopoverContent>
    </Popover>
  );
}
