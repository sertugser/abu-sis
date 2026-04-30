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
import { useUpdateWebModuleSetting } from '../../hooks/useWebModuleSettings';
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
  if (value == null) return null;
  if (typeof value === 'string') return value;
  return JSON.stringify(value);
}

function extractField(serialized: string | null | undefined, field: string) {
  if (!serialized) return null;

  try {
    const parsed = JSON.parse(serialized) as Record<string, unknown>;
    return displayValue(parsed[field] ?? parsed[field.charAt(0).toUpperCase() + field.slice(1)]);
  } catch {
    return null;
  }
}

function getAuditUserLabel(log: AuditLogDto) {
  if (log.userName) return log.userName;
  if (log.userId != null) return `Kullanıcı #${log.userId}`;
  return 'Sistem';
}

export default function WebModuleSettingHistoryDrawer({
  settingId,
  label,
  currentValue,
  currentEffectiveFrom,
  currentEffectiveTo,
}: {
  settingId: number;
  label: string;
  currentValue: string;
  currentEffectiveFrom: string | null;
  currentEffectiveTo: string | null;
}) {
  const [open, setOpen] = useState(false);
  const [rollbackError, setRollbackError] = useState<string | null>(null);
  const [rollbackTarget, setRollbackTarget] = useState<AuditLogDto | null>(null);
  const updateMutation = useUpdateWebModuleSetting();
  const historyQuery = useAuditLogs({
    entityType: 'WebModuleSetting',
    entityId: String(settingId),
    action: 'Modified',
    page: 1,
    pageSize: 5,
  }, { enabled: open });

  const historyItems = historyQuery.data?.items ?? [];

  function confirmRollback(log: AuditLogDto) {
    const previousValue = extractField(log.oldValue, 'value');
    if (previousValue == null) {
      setRollbackError('Bu kayıt için geri alınabilecek bir önceki değer bulunamadı.');
      return;
    }
    setRollbackTarget(log);
  }

  function executeRollback() {
    if (!rollbackTarget) return;

    const previousValue = extractField(rollbackTarget.oldValue, 'value');
    if (previousValue == null) return;

    setRollbackError(null);
    updateMutation.mutate(
      {
        id: settingId,
        payload: {
          value: previousValue,
          effectiveFrom: extractField(rollbackTarget.oldValue, 'effectiveFrom') ?? currentEffectiveFrom,
          effectiveTo: extractField(rollbackTarget.oldValue, 'effectiveTo') ?? currentEffectiveTo,
        },
      },
      {
        onSuccess: () => {
          setRollbackTarget(null);
          historyQuery.refetch();
        },
        onError: error => {
          setRollbackTarget(null);
          setRollbackError(getErrorMessage(error, 'Ayar geri alınamadı.'));
        },
      },
    );
  }

  return (
    <>
      <Button size="icon" variant="ghost" className="h-7 w-7" onClick={() => setOpen(true)} title="Geçmiş">
        <History className="h-4 w-4" />
      </Button>

      <Sheet open={open} onOpenChange={setOpen}>
        <SheetContent side="right" className="w-full sm:max-w-xl overflow-y-auto">
          <div className="space-y-5">
            <SheetHeader>
              <SheetTitle>{label}</SheetTitle>
              <SheetDescription>
                Son 5 değişiklik. Mevcut değer: <span className="font-mono">{currentValue}</span>
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
                  const oldValue = extractField(log.oldValue, 'value');
                  const newValue = extractField(log.newValue, 'value');

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
                <span className="font-mono text-xs">{label}</span> değerini{' '}
                <span className="font-semibold">{currentValue}</span> &rarr;{' '}
                <span className="font-semibold">{rollbackTarget ? extractField(rollbackTarget.oldValue, 'value') ?? '—' : '—'}</span>{' '}
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
