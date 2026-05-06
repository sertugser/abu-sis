import type { ReactNode } from 'react';
import { ChevronLeft, ChevronRight, ChevronsLeft, ChevronsRight } from 'lucide-react';
import { Button } from '@/components/ui/button';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';

interface TablePaginationProps {
  totalCount: number;
  page: number;
  pageSize: number;
  onPageChange: (page: number) => void;
  onPageSizeChange: (size: number) => void;
  /** "ders", "şube" vb. — tekil etiket. */
  itemLabel?: string;
  pageSizeOptions?: number[];
  /**
   * Sayaç ile sayfa kontrolleri arasında render edilecek ek bilgi
   * (toplam kredi, ders sayısı vb.). Tekrar eden bilgileri tek satırda toplamak için.
   */
  extraInfo?: ReactNode;
  /**
   * Sayfa boyutu seçici'nin sağında render edilecek ana aksiyon (örn. "Aktar" butonu).
   * Sol/sağ panellerin simetrik kalması için pagination satırı içinde tutulur.
   */
  rightAction?: ReactNode;
}

/**
 * Tablo altına yerleşen sayfalama barı. Hareket + Müfredat sayfaları paylaşır.
 * Filtreler daraldığında safePage clamp'lenir.
 */
export function TablePagination({
  totalCount,
  page,
  pageSize,
  onPageChange,
  onPageSizeChange,
  itemLabel = 'kayıt',
  pageSizeOptions = [25, 50, 100],
  extraInfo,
  rightAction,
}: TablePaginationProps) {
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));
  const safePage = Math.min(Math.max(1, page), totalPages);
  const startIdx = totalCount === 0 ? 0 : (safePage - 1) * pageSize + 1;
  const endIdx = Math.min(safePage * pageSize, totalCount);
  const isFirst = safePage <= 1 || totalCount === 0;
  const isLast = safePage >= totalPages || totalCount === 0;

  return (
    <div className="px-3 py-1.5 flex items-center gap-x-3 gap-y-1 flex-wrap text-xs text-muted-foreground border-t bg-muted/20">
      <span className="tabular-nums whitespace-nowrap">
        {totalCount === 0
          ? `0 ${itemLabel}`
          : `${startIdx}–${endIdx} / ${totalCount} ${itemLabel}`}
      </span>
      {extraInfo && <span className="whitespace-nowrap">{extraInfo}</span>}
      <div className="ml-auto flex items-center gap-0.5">
        <Button
          type="button"
          variant="ghost"
          size="sm"
          className="h-6 w-6 p-0"
          disabled={isFirst}
          onClick={() => onPageChange(1)}
          title="İlk sayfa"
        >
          <ChevronsLeft className="h-3.5 w-3.5" />
        </Button>
        <Button
          type="button"
          variant="ghost"
          size="sm"
          className="h-6 w-6 p-0"
          disabled={isFirst}
          onClick={() => onPageChange(safePage - 1)}
          title="Önceki sayfa"
        >
          <ChevronLeft className="h-3.5 w-3.5" />
        </Button>
        <span className="px-1.5 tabular-nums">
          {safePage} / {totalPages}
        </span>
        <Button
          type="button"
          variant="ghost"
          size="sm"
          className="h-6 w-6 p-0"
          disabled={isLast}
          onClick={() => onPageChange(safePage + 1)}
          title="Sonraki sayfa"
        >
          <ChevronRight className="h-3.5 w-3.5" />
        </Button>
        <Button
          type="button"
          variant="ghost"
          size="sm"
          className="h-6 w-6 p-0"
          disabled={isLast}
          onClick={() => onPageChange(totalPages)}
          title="Son sayfa"
        >
          <ChevronsRight className="h-3.5 w-3.5" />
        </Button>
        <Select
          value={String(pageSize)}
          onValueChange={(v) => onPageSizeChange(Number(v))}
        >
          <SelectTrigger className="h-6 w-[88px] text-xs ml-1">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            {pageSizeOptions.map((opt) => (
              <SelectItem key={opt} value={String(opt)} className="text-xs">
                {opt}/sayfa
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
        {rightAction && <div className="ml-2">{rightAction}</div>}
      </div>
    </div>
  );
}
