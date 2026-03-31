import { useState } from 'react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table';
import { AlertCircle, ChevronDown, ChevronRight, Search } from 'lucide-react';
import { useAuditLogs } from '../hooks/useAuditLog';
import type { AuditLogDto } from '../api/auditLog.api';

export default function AuditLogPage() {
  const [entityType, setEntityType] = useState('');
  const [userId, setUserId] = useState('');
  const [action, setAction] = useState('');
  const [from, setFrom] = useState('');
  const [to, setTo] = useState('');
  const [page, setPage] = useState(1);
  const pageSize = 50;

  const { data, isLoading, isError } = useAuditLogs({
    entityType: entityType || undefined,
    userId: userId || undefined,
    action: action || undefined,
    from: from || undefined,
    to: to || undefined,
    page,
    pageSize,
  });

  const [expandedId, setExpandedId] = useState<number | null>(null);

  return (
    <div className="space-y-6">
      <h1 className="text-xl font-bold">Denetim Kayitlari</h1>

      {/* Filters */}
      <Card>
        <CardContent className="pt-4">
          <div className="flex flex-wrap items-end gap-3">
            <div className="space-y-1">
              <label className="text-xs font-medium">Varlik Tipi</label>
              <Input
                className="h-8 w-36"
                placeholder="orn: Course"
                value={entityType}
                onChange={e => { setEntityType(e.target.value); setPage(1); }}
              />
            </div>
            <div className="space-y-1">
              <label className="text-xs font-medium">Kullanici ID</label>
              <Input
                className="h-8 w-36"
                placeholder="Kullanici ID"
                value={userId}
                onChange={e => { setUserId(e.target.value); setPage(1); }}
              />
            </div>
            <div className="space-y-1">
              <label className="text-xs font-medium">Islem</label>
              <Input
                className="h-8 w-36"
                placeholder="orn: Create"
                value={action}
                onChange={e => { setAction(e.target.value); setPage(1); }}
              />
            </div>
            <div className="space-y-1">
              <label className="text-xs font-medium">Baslangic</label>
              <Input
                type="date"
                className="h-8 w-36"
                value={from}
                onChange={e => { setFrom(e.target.value); setPage(1); }}
              />
            </div>
            <div className="space-y-1">
              <label className="text-xs font-medium">Bitis</label>
              <Input
                type="date"
                className="h-8 w-36"
                value={to}
                onChange={e => { setTo(e.target.value); setPage(1); }}
              />
            </div>
            <Button size="sm" variant="outline" className="h-8" onClick={() => setPage(1)}>
              <Search className="h-4 w-4 mr-1" /> Ara
            </Button>
          </div>
        </CardContent>
      </Card>

      {/* Results */}
      {isLoading ? (
        <Skeleton className="h-64 rounded-xl" />
      ) : isError ? (
        <Alert variant="destructive">
          <AlertCircle className="h-4 w-4" />
          <AlertDescription>Denetim kayitlari yuklenemedi.</AlertDescription>
        </Alert>
      ) : (
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm">
              {data?.totalCount ?? 0} kayit bulundu
            </CardTitle>
          </CardHeader>
          <CardContent>
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead className="w-8" />
                  <TableHead>Zaman</TableHead>
                  <TableHead>Islem</TableHead>
                  <TableHead>Varlik Tipi</TableHead>
                  <TableHead>Varlik ID</TableHead>
                  <TableHead>Kullanici</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {(data?.items ?? []).length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={6} className="text-center py-8 text-muted-foreground">
                      Kayit bulunamadi.
                    </TableCell>
                  </TableRow>
                ) : (
                  (data?.items ?? []).map(log => (
                    <AuditLogRow
                      key={log.id}
                      log={log}
                      expanded={expandedId === log.id}
                      onToggle={() => setExpandedId(prev => prev === log.id ? null : log.id)}
                    />
                  ))
                )}
              </TableBody>
            </Table>

            {/* Pagination */}
            {data && data.totalCount > pageSize && (
              <div className="flex justify-center gap-2 mt-4">
                <Button size="sm" variant="outline" disabled={page <= 1} onClick={() => setPage(p => p - 1)}>
                  Onceki
                </Button>
                <span className="text-sm py-1">
                  Sayfa {page} / {Math.ceil(data.totalCount / pageSize)}
                </span>
                <Button size="sm" variant="outline" disabled={page * pageSize >= data.totalCount} onClick={() => setPage(p => p + 1)}>
                  Sonraki
                </Button>
              </div>
            )}
          </CardContent>
        </Card>
      )}
    </div>
  );
}

function AuditLogRow({ log, expanded, onToggle }: { log: AuditLogDto; expanded: boolean; onToggle: () => void }) {
  const hasDetail = log.oldValue || log.newValue;

  return (
    <>
      <TableRow
        className={hasDetail ? 'cursor-pointer hover:bg-muted/30' : ''}
        onClick={hasDetail ? onToggle : undefined}
      >
        <TableCell>
          {hasDetail && (expanded ? <ChevronDown className="h-4 w-4" /> : <ChevronRight className="h-4 w-4" />)}
        </TableCell>
        <TableCell className="text-xs text-muted-foreground whitespace-nowrap">
          {new Date(log.timestamp).toLocaleString('tr-TR')}
        </TableCell>
        <TableCell className="text-sm font-medium">{log.action}</TableCell>
        <TableCell className="text-sm">{log.entityType}</TableCell>
        <TableCell className="font-mono text-xs">{log.entityId}</TableCell>
        <TableCell className="text-sm">{log.userName || log.userId}</TableCell>
      </TableRow>
      {expanded && hasDetail && (
        <TableRow className="bg-muted/20">
          <TableCell colSpan={6} className="p-4">
            <div className="grid grid-cols-2 gap-4">
              {log.oldValue && (
                <div>
                  <p className="text-xs font-semibold text-muted-foreground mb-1">Eski Deger</p>
                  <pre className="text-xs bg-muted p-2 rounded overflow-auto max-h-40 whitespace-pre-wrap">
                    {formatJson(log.oldValue)}
                  </pre>
                </div>
              )}
              {log.newValue && (
                <div>
                  <p className="text-xs font-semibold text-muted-foreground mb-1">Yeni Deger</p>
                  <pre className="text-xs bg-muted p-2 rounded overflow-auto max-h-40 whitespace-pre-wrap">
                    {formatJson(log.newValue)}
                  </pre>
                </div>
              )}
            </div>
            {log.ipAddress && (
              <p className="text-xs text-muted-foreground mt-2">IP: {log.ipAddress}</p>
            )}
          </TableCell>
        </TableRow>
      )}
    </>
  );
}

function formatJson(value: string): string {
  try {
    return JSON.stringify(JSON.parse(value), null, 2);
  } catch {
    return value;
  }
}
