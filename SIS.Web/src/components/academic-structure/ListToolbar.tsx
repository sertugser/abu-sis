import type { ReactNode } from 'react';
import { Search } from 'lucide-react';
import { Input } from '@/components/ui/input';

interface ListToolbarProps {
  searchPlaceholder: string;
  search: string;
  onSearchChange: (value: string) => void;
  hasAnyConstraint: boolean;
  onClear: () => void;
  successMessage?: string;
  /** Sağ taraftaki aksiyon butonları (Toplu Ekle / Ekle vb.). */
  actions: ReactNode;
}

/**
 * Liste sayfalarının ortak araç çubuğu: arama + Temizle + başarı mesajı
 * + sayfaya özel aksiyon butonları. Fakülte/Bölüm/Program paylaşır.
 */
export function ListToolbar({
  searchPlaceholder,
  search,
  onSearchChange,
  hasAnyConstraint,
  onClear,
  successMessage,
  actions,
}: ListToolbarProps) {
  return (
    <div className="flex items-center gap-3 px-4 py-2 border-b border-slate-200 bg-slate-50/40 flex-wrap">
      <div className="relative shrink-0 group/search">
        <Search className="absolute left-2.5 top-1/2 -translate-y-1/2 h-3.5 w-3.5 text-muted-foreground pointer-events-none transition-colors group-focus-within/search:text-primary" />
        <Input
          placeholder={searchPlaceholder}
          value={search}
          onChange={e => onSearchChange(e.target.value)}
          className="pl-8 h-8 w-72 text-sm transition-shadow focus-visible:ring-2 focus-visible:ring-primary/30"
        />
      </div>

      {hasAnyConstraint && (
        <button
          type="button"
          className="text-xs text-muted-foreground hover:text-foreground underline underline-offset-2 shrink-0"
          onClick={onClear}
        >
          Temizle
        </button>
      )}

      <div className="flex-1" />

      {successMessage && (
        <span className="text-xs font-medium text-emerald-600 shrink-0">{successMessage}</span>
      )}

      {actions}
    </div>
  );
}
