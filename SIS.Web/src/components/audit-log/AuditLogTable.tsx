import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table';
import { ArrowDown, ArrowUp, ArrowUpDown, Eye, Search } from 'lucide-react';
import type { AuditLogSummaryDto } from '../../api/auditLog.api';
import { ACTION_CONFIG, ENTITY_TYPE_LABELS } from './audit-log.constants';
import { buildSummary, formatFullDate, formatRelativeTime, getUserInitials } from './audit-log.utils';

interface AuditLogTableProps {
  items: AuditLogSummaryDto[];
  onViewDetail: (logId: number) => void;
  sortBy: string;
  sortDir: 'asc' | 'desc';
  onSortChange: (column: string) => void;
  footer?: React.ReactNode;
}

function SortIcon({ column, sortBy, sortDir }: { column: string; sortBy: string; sortDir: 'asc' | 'desc' }) {
  if (sortBy !== column) return <ArrowUpDown className="h-3 w-3 ml-1 opacity-40" />;
  return sortDir === 'asc'
    ? <ArrowUp className="h-3 w-3 ml-1" />
    : <ArrowDown className="h-3 w-3 ml-1" />;
}

function SortableHead({
  column, label, sortBy, sortDir, onSort, className,
}: {
  column: string; label: string; sortBy: string; sortDir: 'asc' | 'desc';
  onSort: (col: string) => void; className?: string;
}) {
  return (
    <TableHead className={className}>
      <button
        type="button"
        className="flex items-center font-semibold hover:text-foreground transition-colors"
        onClick={() => onSort(column)}
      >
        {label}
        <SortIcon column={column} sortBy={sortBy} sortDir={sortDir} />
      </button>
    </TableHead>
  );
}

export function AuditLogTable({ items, onViewDetail, sortBy, sortDir, onSortChange, footer }: AuditLogTableProps) {
  return (
    <Card className="overflow-hidden">
      <Table>
        <TableHeader>
          <TableRow className="bg-muted/40 hover:bg-muted/40">
            <SortableHead column="Timestamp" label="Zaman" sortBy={sortBy} sortDir={sortDir} onSort={onSortChange} className="w-[180px]" />
            <SortableHead column="Action" label="İşlem" sortBy={sortBy} sortDir={sortDir} onSort={onSortChange} className="w-[140px]" />
            <SortableHead column="EntityType" label="Açıklama" sortBy={sortBy} sortDir={sortDir} onSort={onSortChange} />
            <SortableHead column="UserId" label="Kullanıcı" sortBy={sortBy} sortDir={sortDir} onSort={onSortChange} className="w-[180px]" />
            <TableHead className="w-[60px]" />
          </TableRow>
        </TableHeader>
        <TableBody>
          {items.length === 0 ? (
            <TableRow>
              <TableCell colSpan={5} className="text-center py-16">
                <div className="flex flex-col items-center gap-2 text-muted-foreground">
                  <Search className="h-8 w-8 opacity-30" />
                  <p className="text-sm font-medium">Kayıt bulunamadı</p>
                  <p className="text-xs">Filtre kriterlerinizi değiştirmeyi deneyin</p>
                </div>
              </TableCell>
            </TableRow>
          ) : (
            items.map(log => (
              <AuditLogRow key={log.id} log={log} onViewDetail={() => onViewDetail(log.id)} />
            ))
          )}
        </TableBody>
      </Table>
      {footer && (
        <div className="border-t px-4 py-3">
          {footer}
        </div>
      )}
    </Card>
  );
}

function AuditLogRow({ log, onViewDetail }: { log: AuditLogSummaryDto; onViewDetail: () => void }) {
  const actionCfg = ACTION_CONFIG[log.action] ?? { label: log.action, color: 'text-gray-700', bg: 'bg-gray-50 border-gray-200', icon: '•' };
  const summary = buildSummary(log);
  const entityLabel = ENTITY_TYPE_LABELS[log.entityType] ?? log.entityType;
  const userName = log.userName ?? (log.userId != null ? `Kullanıcı #${log.userId}` : 'Sistem');
  const initials = getUserInitials(userName);

  return (
    <TableRow className="group hover:bg-muted/30 transition-colors">
      <TableCell className="py-3">
        <div className="flex flex-col">
          <span className="text-sm text-foreground">{formatRelativeTime(log.timestamp)}</span>
          <span className="text-[11px] text-muted-foreground">{formatFullDate(log.timestamp)}</span>
        </div>
      </TableCell>

      <TableCell className="py-3">
        <span className={`inline-flex items-center gap-1.5 rounded-md border px-2 py-0.5 text-xs font-medium ${actionCfg.bg} ${actionCfg.color}`}>
          <span className="text-[10px] leading-none">{actionCfg.icon}</span>
          {actionCfg.label}
        </span>
      </TableCell>

      <TableCell className="py-3">
        <p className="text-sm text-foreground leading-snug">{summary}</p>
        <div className="flex items-center gap-2 mt-0.5">
          <span className="text-[11px] text-muted-foreground">{entityLabel}</span>
          {log.entityId && (
            <>
              <span className="text-muted-foreground/40">·</span>
              <span className="text-[11px] font-mono text-muted-foreground">#{log.entityId}</span>
            </>
          )}
        </div>
      </TableCell>

      <TableCell className="py-3">
        <div className="flex items-center gap-2">
          <div className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-primary/10 text-primary text-[10px] font-bold">
            {initials}
          </div>
          <span className="text-sm text-foreground truncate max-w-[120px]">{userName}</span>
        </div>
      </TableCell>

      <TableCell className="py-3">
        <Button
          variant="ghost"
          size="icon"
          className="h-7 w-7 opacity-0 group-hover:opacity-100 transition-opacity"
          onClick={onViewDetail}
        >
          <Eye className="h-3.5 w-3.5" />
        </Button>
      </TableCell>
    </TableRow>
  );
}
