import { Tag } from 'lucide-react';
import { cn } from '@/lib/utils';
import type { AnnouncementCategory } from '../../types/announcementCategories';
import type { AnnouncementList } from '../../types/announcements';

export type CategoryFilterValue = 'all' | 'none' | number;

interface CategoryPillBarProps {
  categories: AnnouncementCategory[];
  items: AnnouncementList[];
  value: CategoryFilterValue;
  onChange: (next: CategoryFilterValue) => void;
}

/**
 * Duyurular sayfasında arama bloğunun üstünde yer alan renkli kategori sekmeleri.
 * Tıklanabilir pill'ler — kategori rengini accent olarak kullanır, seçili olan dolu görünür.
 */
export default function CategoryPillBar({
  categories,
  items,
  value,
  onChange,
}: CategoryPillBarProps) {
  const totalCount   = items.length;
  const noneCount    = items.filter((a) => a.category === null).length;
  const visibleCats  = categories
    .map((cat) => ({ cat, count: items.filter((a) => a.category?.id === cat.id).length }))
    .filter((entry) => entry.count > 0);

  if (totalCount === 0) return null;

  return (
    <div className="flex flex-wrap items-center gap-2">
      <PillButton
        active={value === 'all'}
        onClick={() => onChange('all')}
        label="Tümü"
        count={totalCount}
      />
      {visibleCats.map(({ cat, count }) => (
        <PillButton
          key={cat.id}
          active={value === cat.id}
          onClick={() => onChange(cat.id)}
          label={cat.name}
          count={count}
          color={cat.colorHex}
        />
      ))}
      {noneCount > 0 && (
        <PillButton
          active={value === 'none'}
          onClick={() => onChange('none')}
          label="Kategorisiz"
          count={noneCount}
          icon={<Tag className="h-3 w-3" />}
        />
      )}
    </div>
  );
}

interface PillButtonProps {
  active: boolean;
  onClick: () => void;
  label: string;
  count: number;
  color?: string;
  icon?: React.ReactNode;
}

function PillButton({ active, onClick, label, count, color, icon }: PillButtonProps) {
  // Aktif değilse soluk kategori rengi (accent), aktifse dolgun
  const baseStyle = color
    ? active
      ? { backgroundColor: color, color: '#fff', borderColor: color }
      : { backgroundColor: `${color}14`, color, borderColor: `${color}33` }
    : undefined;

  return (
    <button
      type="button"
      onClick={onClick}
      style={baseStyle}
      className={cn(
        'group inline-flex items-center gap-1.5 rounded-full border px-3.5 py-1.5 text-sm font-medium transition-all',
        'hover:-translate-y-0.5 hover:shadow-sm',
        !color && (active
          ? 'border-primary bg-primary text-primary-foreground shadow-sm'
          : 'border-border bg-card text-foreground hover:border-primary/40 hover:bg-accent/40')
      )}
    >
      {!color && icon && <span className="opacity-70">{icon}</span>}
      {color && (
        <span
          className="h-1.5 w-1.5 rounded-full shrink-0"
          style={{ backgroundColor: active ? '#fff' : color }}
        />
      )}
      <span>{label}</span>
      <span
        className={cn(
          'tabular-nums rounded-full px-1.5 py-0.5 text-[10px] font-semibold',
          active
            ? 'bg-white/25 text-current'
            : 'bg-foreground/8 text-foreground/70'
        )}
      >
        {count}
      </span>
    </button>
  );
}
