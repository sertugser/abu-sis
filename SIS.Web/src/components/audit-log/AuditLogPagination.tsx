import { Button } from '@/components/ui/button';
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/components/ui/select';
import { ChevronLeft, ChevronRight } from 'lucide-react';
import { PAGE_SIZE_OPTIONS } from './audit-log.constants';
import { generatePageNumbers } from './audit-log.utils';

interface AuditLogPaginationProps {
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasActiveFilter: boolean;
  onPageChange: (page: number) => void;
  onPageSizeChange: (size: number) => void;
}

export function AuditLogPagination({
  page, pageSize, totalCount, totalPages, hasActiveFilter,
  onPageChange, onPageSizeChange,
}: AuditLogPaginationProps) {
  return (
    <div className="flex items-center justify-between">
      {/* Sol: kayıt sayısı */}
      <p className="text-sm text-muted-foreground">
        <span className="font-semibold text-foreground">{totalCount.toLocaleString('tr-TR')}</span> kayıt
        {hasActiveFilter && <span className="ml-1">(filtrelenmiş)</span>}
      </p>

      {/* Orta: sayfalama */}
      {totalPages > 1 && (
        <div className="flex items-center gap-1">
          <Button
            variant="outline" size="icon"
            className="h-8 w-8"
            disabled={page <= 1}
            onClick={() => onPageChange(page - 1)}
          >
            <ChevronLeft className="h-4 w-4" />
          </Button>

          {generatePageNumbers(page, totalPages).map((p, i) =>
            p === '...' ? (
              <span key={`ellipsis-${i}`} className="px-1 text-xs text-muted-foreground">...</span>
            ) : (
              <Button
                key={p}
                variant={p === page ? 'default' : 'outline'}
                size="icon"
                className="h-8 w-8 text-xs"
                onClick={() => onPageChange(p as number)}
              >
                {p}
              </Button>
            )
          )}

          <Button
            variant="outline" size="icon"
            className="h-8 w-8"
            disabled={page >= totalPages}
            onClick={() => onPageChange(page + 1)}
          >
            <ChevronRight className="h-4 w-4" />
          </Button>
        </div>
      )}

      {/* Sağ: sayfa boyutu */}
      <Select value={String(pageSize)} onValueChange={v => onPageSizeChange(Number(v))}>
        <SelectTrigger className="h-8 w-20">
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          {PAGE_SIZE_OPTIONS.map(n => (
            <SelectItem key={n} value={String(n)}>{n}</SelectItem>
          ))}
        </SelectContent>
      </Select>
    </div>
  );
}
