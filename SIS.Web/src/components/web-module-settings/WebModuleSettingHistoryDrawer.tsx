import { useState } from 'react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import {
  AlertDialog, AlertDialogAction, AlertDialogCancel, AlertDialogContent,
  AlertDialogDescription, AlertDialogFooter, AlertDialogHeader, AlertDialogTitle,
} from '@/components/ui/alert-dialog';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  Sheet, SheetContent, SheetDescription, SheetHeader, SheetTitle,
} from '@/components/ui/sheet';
import { Skeleton } from '@/components/ui/skeleton';
import { ArrowRight, CalendarRange, Clock3, History, RotateCcw } from 'lucide-react';
import { useAuditLogs } from '../../hooks/useAuditLog';
import { useUpdateWebModuleSetting } from '../../hooks/useWebModuleSettings';
import { getErrorMessage } from '../../lib/errorMessage';
import type { AuditLogDto } from '../../api/auditLog.api';

type WebModuleSettingSnapshot = {
  value: string | null;
  effectiveFrom: string | null;
  effectiveTo: string | null;
};

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

function parseSnapshot(serialized: string | null | undefined): WebModuleSettingSnapshot | null {
  if (!serialized) return null;

  try {
    const parsed = JSON.parse(serialized) as Record<string, unknown>;
    return {
      value: displayValue(parsed.value ?? parsed.Value),
      effectiveFrom: displayValue(parsed.effectiveFrom ?? parsed.EffectiveFrom),
      effectiveTo: displayValue(parsed.effectiveTo ?? parsed.EffectiveTo),
    };
  } catch {
    return {
      value: serialized,
      effectiveFrom: null,
      effectiveTo: null,
    };
  }
}

function formatEffectiveDate(value: string | null | undefined) {
  if (!value) return '—';

  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) return value;

  return parsed.toLocaleString('tr-TR', {
    day: '2-digit',
    month: 'short',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
}

function formatEffectiveRangeSummary(effectiveFrom: string | null, effectiveTo: string | null) {
  if (!effectiveFrom && !effectiveTo) return 'Süresiz';
  return `${formatEffectiveDate(effectiveFrom)} - ${formatEffectiveDate(effectiveTo)}`;
}

function formatStatusLabel(value: string | null | undefined) {
  if (value == null) return '—';
  if (value === 'true' || value === '1') return 'Aktif';
  if (value === 'false' || value === '0') return 'Pasif';
  return value;
}

function hasRollbackSnapshot(snapshot: WebModuleSettingSnapshot | null) {
  return snapshot != null && (
    snapshot.value != null
    || snapshot.effectiveFrom != null
    || snapshot.effectiveTo != null
  );
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
    const previousSnapshot = parseSnapshot(log.oldValue);
    if (!hasRollbackSnapshot(previousSnapshot)) {
      setRollbackError('Bu kayıt için geri alınabilecek bir önceki değer bulunamadı.');
      return;
    }
    setRollbackTarget(log);
  }

  function executeRollback() {
    if (!rollbackTarget) return;

    const previousSnapshot = parseSnapshot(rollbackTarget.oldValue);
    if (previousSnapshot == null || !hasRollbackSnapshot(previousSnapshot)) return;
    const snapshot = previousSnapshot;

    setRollbackError(null);
    updateMutation.mutate(
      {
        id: settingId,
        payload: {
          value: snapshot.value ?? currentValue,
          effectiveFrom: snapshot.effectiveFrom ?? currentEffectiveFrom,
          effectiveTo: snapshot.effectiveTo ?? currentEffectiveTo,
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
                Son 5 değişiklik ve geri alma işlemleri.
              </SheetDescription>
            </SheetHeader>

            <div className="grid gap-3 rounded-2xl border bg-muted/30 p-4 sm:grid-cols-2">
              <div className="rounded-xl border bg-background px-4 py-3">
                <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-muted-foreground">Mevcut Durum</p>
                <div className="mt-2 flex items-center gap-2">
                  <Badge variant={currentValue === 'true' || currentValue === '1' ? 'default' : 'secondary'}>
                    {formatStatusLabel(currentValue)}
                  </Badge>
                </div>
              </div>
              <div className="rounded-xl border bg-background px-4 py-3">
                <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-muted-foreground">Mevcut Geçerlilik</p>
                <div className="mt-2 flex items-start gap-2 text-sm text-foreground">
                  <CalendarRange className="mt-0.5 h-4 w-4 text-muted-foreground" />
                  <span>{formatEffectiveRangeSummary(currentEffectiveFrom, currentEffectiveTo)}</span>
                </div>
              </div>
            </div>

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
                  const oldSnapshot = parseSnapshot(log.oldValue);
                  const newSnapshot = parseSnapshot(log.newValue);
                  const oldStatus = oldSnapshot?.value ?? newSnapshot?.value ?? currentValue;
                  const newStatus = newSnapshot?.value ?? oldSnapshot?.value ?? currentValue;
                  const canRollback = hasRollbackSnapshot(oldSnapshot);

                  return (
                    <div key={log.id} className="rounded-2xl border bg-card p-4 shadow-sm space-y-4">
                      <div className="flex items-start justify-between gap-3">
                        <div className="space-y-2">
                          <div className="inline-flex items-center gap-2 rounded-full border bg-muted/40 px-3 py-1 text-xs font-medium text-muted-foreground">
                            <Clock3 className="h-3.5 w-3.5" />
                            {formatHistoryDate(log.timestamp)}
                          </div>
                          <p className="text-sm font-medium">{getAuditUserLabel(log)}</p>
                        </div>
                        <Button
                          size="sm"
                          variant="outline"
                          onClick={() => confirmRollback(log)}
                          disabled={updateMutation.isPending || !canRollback}
                        >
                          <RotateCcw className="mr-1.5 h-3.5 w-3.5" />
                          Geri Al
                        </Button>
                      </div>

                      <div className="grid gap-3 sm:grid-cols-[1fr_auto_1fr] sm:items-stretch">
                        <div className="rounded-xl border border-red-200 bg-red-50/80 p-4">
                          <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-red-600">Eski Geçerlilik</p>
                          <div className="mt-3 space-y-2 text-sm text-red-950">
                            <p><span className="font-medium">Başlangıç:</span> {formatEffectiveDate(oldSnapshot?.effectiveFrom)}</p>
                            <p><span className="font-medium">Bitiş:</span> {formatEffectiveDate(oldSnapshot?.effectiveTo)}</p>
                          </div>
                          <p className="mt-4 text-xs text-red-700/90">
                            Durum: <span className="font-semibold">{formatStatusLabel(oldStatus)}</span>
                          </p>
                        </div>
                        <div className="hidden items-center justify-center sm:flex">
                          <div className="flex h-10 w-10 items-center justify-center rounded-full border bg-background text-muted-foreground shadow-sm">
                            <ArrowRight className="h-4 w-4" />
                          </div>
                        </div>
                        <div className="rounded-xl border border-emerald-200 bg-emerald-50/80 p-4">
                          <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-emerald-700">Yeni Geçerlilik</p>
                          <div className="mt-3 space-y-2 text-sm text-emerald-950">
                            <p><span className="font-medium">Başlangıç:</span> {formatEffectiveDate(newSnapshot?.effectiveFrom)}</p>
                            <p><span className="font-medium">Bitiş:</span> {formatEffectiveDate(newSnapshot?.effectiveTo)}</p>
                          </div>
                          <p className="mt-4 text-xs text-emerald-700/90">
                            Durum: <span className="font-semibold">{formatStatusLabel(newStatus)}</span>
                          </p>
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
                <span className="font-semibold">{rollbackTarget ? parseSnapshot(rollbackTarget.oldValue)?.value ?? currentValue : currentValue}</span>{' '}
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
