import { useEffect, useMemo, useState } from 'react';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Search, Star } from 'lucide-react';
import { cn } from '@/lib/utils';
import type { MenuGroup } from './SidebarMenuGroups';

interface FavoritesDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  menuGroups: MenuGroup[];
  currentFavorites: string[];
  maxFavorites: number;
  onSave: (favorites: string[]) => void;
}

interface FlatItem {
  route: string;
  label: string;
  groupLabel: string;
}

function flattenMenu(groups: MenuGroup[]): FlatItem[] {
  const items: FlatItem[] = [];
  const seen = new Set<string>();
  for (const group of groups) {
    if (group.id === 'sik-kullanilanlar') continue;
    for (const child of group.children) {
      if (child.openInNewTab) continue;     // external/new-tab links aren't pinnable shortcuts
      if (seen.has(child.to)) continue;     // skip duplicates already in another group
      seen.add(child.to);
      items.push({ route: child.to, label: child.label, groupLabel: group.label });
    }
  }
  return items;
}

export function FavoritesDialog({
  open,
  onOpenChange,
  menuGroups,
  currentFavorites,
  maxFavorites,
  onSave,
}: FavoritesDialogProps) {
  const [selected, setSelected] = useState<string[]>(currentFavorites);
  const [query, setQuery] = useState('');

  // Reset state when opened
  /* eslint-disable react-hooks/set-state-in-effect -- syncing dialog state with props on open; intentional */
  useEffect(() => {
    if (open) {
      setSelected(currentFavorites);
      setQuery('');
    }
  }, [open, currentFavorites]);
  /* eslint-enable react-hooks/set-state-in-effect */

  const allItems = useMemo(() => flattenMenu(menuGroups), [menuGroups]);

  const filteredItems = useMemo(() => {
    const q = query.trim().toLocaleLowerCase('tr-TR');
    if (!q) return allItems;
    return allItems.filter(item =>
      item.label.toLocaleLowerCase('tr-TR').includes(q)
      || item.groupLabel.toLocaleLowerCase('tr-TR').includes(q)
    );
  }, [allItems, query]);

  const grouped = useMemo(() => {
    const map = new Map<string, FlatItem[]>();
    for (const item of filteredItems) {
      const arr = map.get(item.groupLabel) ?? [];
      arr.push(item);
      map.set(item.groupLabel, arr);
    }
    return Array.from(map.entries());
  }, [filteredItems]);

  const atMax = selected.length >= maxFavorites;

  function togglePin(route: string) {
    setSelected(prev => {
      if (prev.includes(route)) return prev.filter(r => r !== route);
      if (prev.length >= maxFavorites) return prev;
      return [...prev, route];
    });
  }

  function handleSave() {
    onSave(selected);
    onOpenChange(false);
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-2xl max-h-[85vh] flex flex-col gap-3 overflow-hidden">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <Star className="h-5 w-5 text-amber-500" />
            Sık Kullanılanları Düzenle
          </DialogTitle>
          <DialogDescription>
            Sidebar'da kısayol olarak görünecek sayfaları seçin. Ana Sayfa her zaman sabit kalır.
          </DialogDescription>
        </DialogHeader>

        <div className="relative shrink-0">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
          <Input
            value={query}
            onChange={e => setQuery(e.target.value)}
            placeholder="Sayfa ara..."
            className="pl-9"
          />
        </div>

        <div className="flex items-center justify-between text-xs text-muted-foreground px-1">
          <span>
            Seçili: <strong className={cn(atMax && 'text-amber-600')}>{selected.length}</strong> / {maxFavorites}
          </span>
          {atMax && <span className="text-amber-600">Maksimuma ulaşıldı</span>}
        </div>

        <div className="flex-1 min-h-0 overflow-y-auto border rounded-md">
          <div className="p-2 space-y-3">
            {grouped.length === 0 && (
              <p className="text-sm text-muted-foreground text-center py-8">
                Arama sonucu bulunamadı.
              </p>
            )}
            {grouped.map(([groupLabel, items]) => (
              <div key={groupLabel}>
                <div className="text-[11px] font-semibold uppercase tracking-wide text-muted-foreground px-2 py-1 sticky top-0 bg-background">
                  {groupLabel}
                </div>
                <div>
                  {items.map(item => {
                    const isPinned = selected.includes(item.route);
                    const disabled = !isPinned && atMax;
                    return (
                      <button
                        key={item.route}
                        type="button"
                        onClick={() => togglePin(item.route)}
                        disabled={disabled}
                        className={cn(
                          'w-full flex items-center gap-2 px-2 py-1.5 text-sm rounded text-left transition-colors',
                          'hover:bg-accent disabled:opacity-40 disabled:cursor-not-allowed',
                          isPinned && 'bg-amber-50 dark:bg-amber-950/20'
                        )}
                      >
                        <Star
                          className={cn(
                            'h-4 w-4 shrink-0',
                            isPinned ? 'fill-amber-500 text-amber-500' : 'text-muted-foreground'
                          )}
                        />
                        <span className="truncate">{item.label}</span>
                      </button>
                    );
                  })}
                </div>
              </div>
            ))}
          </div>
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>
            İptal
          </Button>
          <Button onClick={handleSave}>Kaydet</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
