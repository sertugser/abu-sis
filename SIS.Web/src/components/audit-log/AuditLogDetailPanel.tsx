import { ArrowRight } from 'lucide-react';
import { Skeleton } from '@/components/ui/skeleton';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import {
  SheetHeader, SheetTitle, SheetDescription,
} from '@/components/ui/sheet';
import { AlertCircle, RotateCcw, History } from 'lucide-react';
import { useAuditLogDetail } from '../../hooks/useAuditLog';
import { ACTION_CONFIG, ENTITY_TYPE_LABELS } from './audit-log.constants';
import { buildSummary, formatFullDate, formatJson, getUserInitials } from './audit-log.utils';
import type { AuditLogSummaryDto } from '../../api/auditLog.api';

interface AuditLogDetailPanelProps {
  logId: number;
  /** Minimal summary data from list — used while detail loads */
  summary: AuditLogSummaryDto;
  onViewEntityHistory?: (entityType: string, entityId: string) => void;
}

export function AuditLogDetailPanel({ logId, summary, onViewEntityHistory }: AuditLogDetailPanelProps) {
  const { data: log, isLoading, isError, refetch } = useAuditLogDetail(logId);

  const actionCfg = ACTION_CONFIG[summary.action] ?? { label: summary.action, color: 'text-gray-700', bg: 'bg-gray-50 border-gray-200', icon: '•' };
  const entityLabel = ENTITY_TYPE_LABELS[summary.entityType] ?? summary.entityType;
  const userName = summary.userName ?? (summary.userId != null ? `Kullanıcı #${summary.userId}` : 'Sistem');

  // Parse JSON values from detail response
  let oldObj: Record<string, unknown> | null = null;
  let newObj: Record<string, unknown> | null = null;
  if (log) {
    try { if (log.oldValue) oldObj = JSON.parse(log.oldValue); } catch { /* skip */ }
    try { if (log.newValue) newObj = JSON.parse(log.newValue); } catch { /* skip */ }
  }

  const allKeys = oldObj && newObj
    ? [...new Set([...Object.keys(oldObj), ...Object.keys(newObj)])]
    : [];
  const changedKeys = allKeys.filter(k => JSON.stringify(oldObj![k]) !== JSON.stringify(newObj![k]));
  const unchangedKeys = allKeys.filter(k => JSON.stringify(oldObj![k]) === JSON.stringify(newObj![k]));

  const singleObj = newObj ?? oldObj;
  const singleKeys = singleObj ? Object.keys(singleObj) : [];

  return (
    <div className="space-y-5">
      {/* Header */}
      <SheetHeader className="space-y-3">
        <div className="flex items-center gap-2.5">
          <span className={`inline-flex items-center gap-1.5 rounded-md border px-2.5 py-1 text-xs font-semibold ${actionCfg.bg} ${actionCfg.color}`}>
            {actionCfg.icon} {actionCfg.label}
          </span>
        </div>
        <SheetTitle className="text-xl">{entityLabel}</SheetTitle>
        <SheetDescription className="text-sm">{buildSummary(summary)}</SheetDescription>
      </SheetHeader>

      {/* Genel Bilgiler */}
      <div className="rounded-lg border bg-muted/20 p-4">
        <h3 className="text-xs font-semibold text-muted-foreground uppercase tracking-wider mb-3">Genel Bilgiler</h3>
        <div className="grid grid-cols-2 gap-y-3 gap-x-4">
          <div>
            <p className="text-[11px] text-muted-foreground">Log ID</p>
            <p className="text-sm font-mono font-medium">{summary.id}</p>
          </div>
          <div>
            <p className="text-[11px] text-muted-foreground">Kayıt ID (Entity)</p>
            <p className="text-sm font-mono font-medium">{summary.entityId ?? '—'}</p>
          </div>
          <div>
            <p className="text-[11px] text-muted-foreground">Varlık Tipi</p>
            <p className="text-sm font-medium">{entityLabel} <span className="text-muted-foreground font-mono text-xs">({summary.entityType})</span></p>
          </div>
          <div>
            <p className="text-[11px] text-muted-foreground">İşlem</p>
            <p className="text-sm font-medium">{actionCfg.label} <span className="text-muted-foreground font-mono text-xs">({summary.action})</span></p>
          </div>
          <div>
            <p className="text-[11px] text-muted-foreground">Tarih / Saat</p>
            <p className="text-sm font-medium">{formatFullDate(summary.timestamp)}</p>
          </div>
          <div>
            <p className="text-[11px] text-muted-foreground">Kullanıcı</p>
            <p className="text-sm font-medium">{userName} {summary.userId && <span className="text-muted-foreground font-mono text-xs">(ID: {summary.userId})</span>}</p>
          </div>
          <div className="col-span-2">
            <p className="text-[11px] text-muted-foreground">IP Adresi</p>
            <p className="text-sm font-mono font-medium">{summary.ipAddress ?? '—'}</p>
          </div>
        </div>

        {/* Entity history button */}
        {summary.entityId && onViewEntityHistory && (
          <Button
            variant="outline"
            size="sm"
            className="mt-3 w-full"
            onClick={() => onViewEntityHistory(summary.entityType, summary.entityId!)}
          >
            <History className="h-3.5 w-3.5 mr-1.5" />
            Bu kaydın tüm geçmişini gör
          </Button>
        )}
      </div>

      {/* Loading / Error for detail data */}
      {isLoading && (
        <div className="space-y-3">
          <Skeleton className="h-4 w-32" />
          <Skeleton className="h-20 rounded-lg" />
          <Skeleton className="h-20 rounded-lg" />
        </div>
      )}

      {isError && (
        <Alert variant="destructive">
          <AlertCircle className="h-4 w-4" />
          <AlertDescription className="flex items-center gap-2">
            Detay verisi yüklenemedi.
            <Button variant="outline" size="sm" onClick={() => refetch()}>
              <RotateCcw className="h-3 w-3 mr-1" /> Tekrar Dene
            </Button>
          </AlertDescription>
        </Alert>
      )}

      {/* Modified: Diff View */}
      {log && log.action === 'Modified' && oldObj && newObj && changedKeys.length > 0 && (
        <div className="space-y-3">
          <h3 className="text-xs font-semibold text-muted-foreground uppercase tracking-wider">
            Değişen Alanlar <span className="text-foreground">({changedKeys.length})</span>
          </h3>
          <div className="space-y-2">
            {changedKeys.map(key => (
              <div key={key} className="rounded-lg border p-3 space-y-2">
                <p className="text-xs font-bold font-mono text-foreground">{key}</p>
                <div className="flex items-start gap-2">
                  <div className="flex-1 min-w-0">
                    <div className="rounded bg-red-50 border border-red-200 px-2.5 py-1.5">
                      <p className="text-[10px] text-red-400 uppercase tracking-wider mb-0.5">Eski Değer</p>
                      <code className="text-xs text-red-800 break-all font-mono">{oldObj![key] !== undefined ? String(oldObj![key]) : '—'}</code>
                    </div>
                  </div>
                  <ArrowRight className="h-3.5 w-3.5 text-muted-foreground mt-4 shrink-0" />
                  <div className="flex-1 min-w-0">
                    <div className="rounded bg-emerald-50 border border-emerald-200 px-2.5 py-1.5">
                      <p className="text-[10px] text-emerald-400 uppercase tracking-wider mb-0.5">Yeni Değer</p>
                      <code className="text-xs text-emerald-800 break-all font-mono">{newObj![key] !== undefined ? String(newObj![key]) : '—'}</code>
                    </div>
                  </div>
                </div>
              </div>
            ))}
          </div>

          {unchangedKeys.length > 0 && (
            <details className="group">
              <summary className="text-xs text-muted-foreground cursor-pointer hover:text-foreground transition-colors">
                Değişmeyen alanlar ({unchangedKeys.length})
              </summary>
              <div className="mt-2 rounded-lg border bg-muted/20 p-3">
                <div className="grid grid-cols-1 gap-1.5">
                  {unchangedKeys.map(key => (
                    <div key={key} className="flex items-baseline gap-2 text-xs">
                      <span className="font-mono font-medium text-muted-foreground w-32 shrink-0 truncate">{key}</span>
                      <span className="text-foreground truncate">{String(newObj![key] ?? '—')}</span>
                    </div>
                  ))}
                </div>
              </div>
            </details>
          )}
        </div>
      )}

      {/* Created / Deleted: Full Data */}
      {log && (log.action === 'Created' || log.action === 'Deleted') && singleObj && singleKeys.length > 0 && (
        <div className="space-y-3">
          <h3 className="text-xs font-semibold text-muted-foreground uppercase tracking-wider">
            {log.action === 'Created' ? 'Oluşturulan Veri' : 'Silinen Veri'}
            <span className="text-foreground ml-1">({singleKeys.length} alan)</span>
          </h3>
          <div className="rounded-lg border overflow-hidden">
            <table className="w-full text-xs">
              <thead>
                <tr className="bg-muted/40">
                  <th className="text-left px-3 py-2 font-semibold text-muted-foreground">Alan</th>
                  <th className="text-left px-3 py-2 font-semibold text-muted-foreground">Değer</th>
                </tr>
              </thead>
              <tbody>
                {singleKeys.map((key, i) => (
                  <tr key={key} className={i % 2 === 0 ? 'bg-background' : 'bg-muted/10'}>
                    <td className="px-3 py-1.5 font-mono font-medium text-foreground whitespace-nowrap">{key}</td>
                    <td className="px-3 py-1.5 text-muted-foreground break-all">{String(singleObj![key] ?? '—')}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* Other actions with newValue */}
      {log && !['Created', 'Modified', 'Deleted'].includes(log.action) && newObj && (
        <div className="space-y-3">
          <h3 className="text-xs font-semibold text-muted-foreground uppercase tracking-wider">Detay Verisi</h3>
          <div className="rounded-lg border overflow-hidden">
            <table className="w-full text-xs">
              <thead>
                <tr className="bg-muted/40">
                  <th className="text-left px-3 py-2 font-semibold text-muted-foreground">Alan</th>
                  <th className="text-left px-3 py-2 font-semibold text-muted-foreground">Değer</th>
                </tr>
              </thead>
              <tbody>
                {Object.entries(newObj).map(([key, val], i) => (
                  <tr key={key} className={i % 2 === 0 ? 'bg-background' : 'bg-muted/10'}>
                    <td className="px-3 py-1.5 font-mono font-medium text-foreground whitespace-nowrap">{key}</td>
                    <td className="px-3 py-1.5 text-muted-foreground break-all">
                      {typeof val === 'string' && val.length > 200
                        ? <pre className="whitespace-pre-wrap font-mono">{val}</pre>
                        : String(val ?? '—')
                      }
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* Raw JSON fallback */}
      {log && (log.oldValue || log.newValue) && !oldObj && !newObj && (
        <div className="space-y-3">
          <h3 className="text-xs font-semibold text-muted-foreground uppercase tracking-wider">Ham Veri</h3>
          {log.oldValue && (
            <div>
              <p className="text-[11px] text-muted-foreground mb-1">Eski Değer</p>
              <pre className="text-xs bg-muted/40 border rounded-lg p-3 overflow-auto max-h-60 whitespace-pre-wrap font-mono">
                {formatJson(log.oldValue)}
              </pre>
            </div>
          )}
          {log.newValue && (
            <div>
              <p className="text-[11px] text-muted-foreground mb-1">Yeni Değer</p>
              <pre className="text-xs bg-muted/40 border rounded-lg p-3 overflow-auto max-h-60 whitespace-pre-wrap font-mono">
                {formatJson(log.newValue)}
              </pre>
            </div>
          )}
        </div>
      )}
    </div>
  );
}
