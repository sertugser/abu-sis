import { useState } from 'react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import {
  AlertDialog, AlertDialogAction, AlertDialogCancel, AlertDialogContent,
  AlertDialogDescription, AlertDialogFooter, AlertDialogHeader, AlertDialogTitle,
} from '@/components/ui/alert-dialog';
import { Button } from '@/components/ui/button';
import {
  Sheet, SheetContent, SheetDescription, SheetHeader, SheetTitle,
} from '@/components/ui/sheet';
import { Skeleton } from '@/components/ui/skeleton';
import { History, RotateCcw } from 'lucide-react';
import { useAuditLogs } from '../../hooks/useAuditLog';
import { useUpdateSystemConfig } from '../../hooks/useSystemConfig';
import { getErrorMessage } from '../../lib/errorMessage';
import type { AuditLogDto } from '../../api/auditLog.api';

function formatHistoryDate(timestamp: string) {
  return new Date(timestamp).toLocaleString('tr-TR', {
    day: '2-digit',
    month: 'short',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
}

function displayValue(value: unknown) {
  if (value == null) {
    return '—';
  }

  if (typeof value === 'string') {
    return value;
  }

  return JSON.stringify(value);
}

function extractConfigValue(serialized?: string) {
  if (!serialized) {
    return null;
  }

  try {
    const parsed = JSON.parse(serialized) as Record<string, unknown>;
    if ('Value' in parsed) {
      return displayValue(parsed.Value);
    }

    if ('value' in parsed) {
      return displayValue(parsed.value);
    }

    return displayValue(parsed);
  } catch {
    return serialized;
  }
}

function getAuditUserLabel(log: AuditLogDto) {
  if (log.userName) {
    return log.userName;
  }

  if (log.userId != null) {
    return `Kullanıcı #${log.userId}`;
  }

  return 'Sistem';
}

export default function ConfigHistoryDrawer({
  configId,
  configKey,
  currentValue,
}: {
  configId: number;
  configKey: string;
  currentValue: string;
}) {
  const [open, setOpen] = useState(false);
  const [rollbackError, setRollbackError] = useState<string | null>(null);
  const historyQuery = useAuditLogs({
    entityType: 'SystemConfig',
    entityId: String(configId),
    action: 'Modified',
    page: 1,
    pageSize: 5,
  }, { enabled: open });
  const updateMutation = useUpdateSystemConfig();
  const [rollbackTarget, setRollbackTarget] = useState<AuditLogDto | null>(null);

  const historyItems = historyQuery.data?.items ?? [];

  function confirmRollback(log: AuditLogDto) {
    const previousValue = extractConfigValue(log.oldValue);
    if (previousValue == null) {
      setRollbackError('Bu kayıt için geri alınabilecek bir önceki değer bulunamadı.');
      return;
    }
    setRollbackTarget(log);
  }

  function executeRollback() {
    if (!rollbackTarget) return;
    const previousValue = extractConfigValue(rollbackTarget.oldValue);
    if (previousValue == null) return;

    setRollbackError(null);
    updateMutation.mutate(
      { id: configId, value: previousValue },
      {
        onSuccess: () => {
          setRollbackTarget(null);
          historyQuery.refetch();
        },
        onError: (error) => {
          setRollbackTarget(null);
          setRollbackError(getErrorMessage(error, 'Ayar geri alınamadı.'));
        },
      },
    );
  }

  return (
    <>
      <Button
        size="icon"
        variant="ghost"
        className="h-7 w-7"
        onClick={() => setOpen(true)}
        title="Geçmiş"
      >
        <History className="h-4 w-4" />
      </Button>

      <Sheet open={open} onOpenChange={setOpen}>
        <SheetContent side="right" className="w-full sm:max-w-xl overflow-y-auto">
          <div className="space-y-5">
            <SheetHeader>
              <SheetTitle>{configKey}</SheetTitle>
              <SheetDescription>
                Son 5 değişiklik ve geri alma işlemleri. Mevcut değer: <span className="font-mono">{currentValue}</span>
              </SheetDescription>
            </SheetHeader>

            {rollbackError && (
              <Alert variant="destructive">
                <AlertDescription>{rollbackError}</AlertDescription>
              </Alert>
            )}

            {historyQuery.isLoading ? (
              <div className="space-y-3">
                {Array.from({ length: 3 }).map((_, index) => (
                  <Skeleton key={index} className="h-28 rounded-lg" />
                ))}
              </div>
            ) : historyQuery.isError ? (
              <Alert variant="destructive">
                <AlertDescription>Geçmiş kayıtları yüklenemedi.</AlertDescription>
              </Alert>
            ) : historyItems.length === 0 ? (
              <div className="rounded-lg border border-dashed p-6 text-sm text-muted-foreground">
                Bu ayar için henüz değişiklik geçmişi bulunmuyor.
              </div>
            ) : (
              <div className="space-y-3">
                {historyItems.map(log => {
                  const oldValue = extractConfigValue(log.oldValue);
                  const newValue = extractConfigValue(log.newValue);

                  return (
                    <div key={log.id} className="rounded-lg border p-4 space-y-3">
                      <div className="flex items-start justify-between gap-3">
                        <div>
                          <p className="text-sm font-medium">{formatHistoryDate(log.timestamp)}</p>
                          <p className="text-xs text-muted-foreground">{getAuditUserLabel(log)}</p>
                        </div>
                        <Button
                          size="sm"
                          variant="outline"
                          onClick={() => confirmRollback(log)}
                          disabled={updateMutation.isPending || oldValue == null}
                        >
                          <RotateCcw className="mr-1.5 h-3.5 w-3.5" />
                          Geri Al
                        </Button>
                      </div>

                      <div className="grid gap-3">
                        <div className="rounded-md border border-red-200 bg-red-50 p-3">
                          <p className="text-[11px] font-semibold uppercase tracking-wide text-red-600">Eski Değer</p>
                          <p className="mt-1 break-all font-mono text-sm text-red-900">{oldValue ?? '—'}</p>
                        </div>
                        <div className="rounded-md border border-emerald-200 bg-emerald-50 p-3">
                          <p className="text-[11px] font-semibold uppercase tracking-wide text-emerald-600">Yeni Değer</p>
                          <p className="mt-1 break-all font-mono text-sm text-emerald-900">{newValue ?? '—'}</p>
                        </div>
                      </div>
                    </div>
                  );
                })}
              </div>
            )}
          </div>
        </SheetContent>
      </Sheet>

      <AlertDialog open={rollbackTarget != null} onOpenChange={() => setRollbackTarget(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Geri almayı onayla</AlertDialogTitle>
            <AlertDialogDescription asChild>
              <div>
                <span className="font-mono text-xs">{configKey}</span> değerini{' '}
                <span className="font-semibold">{currentValue}</span> &rarr;{' '}
                <span className="font-semibold">{rollbackTarget ? extractConfigValue(rollbackTarget.oldValue) ?? '—' : '—'}</span>{' '}
                olarak geri almak istediğinize emin misiniz?
              </div>
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>İptal</AlertDialogCancel>
            <AlertDialogAction onClick={executeRollback}>Geri Al</AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </>
  );
}
