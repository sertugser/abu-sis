import { useState } from 'react';
import { AlertCircle, Inbox, Loader2, Plus, RefreshCw, ShieldOff } from 'lucide-react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Switch } from '@/components/ui/switch';
import { Textarea } from '@/components/ui/textarea';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog';
import { usePermissions } from '../context/PermissionContext';
import { useCreateDelegation, useDelegations, useRevokeDelegation } from '../hooks/useDelegations';
import { UserSearchCombobox } from '../components/audit-log/UserSearchCombobox';
import type { DelegationStatus, PermissionDelegationDto } from '@/api/delegation.api';

const STATUS_VARIANTS: Record<DelegationStatus, 'default' | 'secondary' | 'outline' | 'destructive'> = {
  Active: 'default',
  Pending: 'secondary',
  Expired: 'outline',
  Revoked: 'destructive',
};

const STATUS_LABELS: Record<DelegationStatus, string> = {
  Active: 'Aktif',
  Pending: 'Beklemede',
  Expired: 'Süresi Doldu',
  Revoked: 'İptal Edildi',
};

function extractError(err: unknown, fallback: string): string {
  if (err && typeof err === 'object') {
    const resp = (err as { response?: { data?: { message?: string } } }).response;
    if (resp?.data?.message) return resp.data.message;
  }
  return fallback;
}

function formatDate(iso: string): string {
  return new Date(iso).toLocaleString('tr-TR', { dateStyle: 'short', timeStyle: 'short' });
}

function toIsoUtc(local: string): string {
  // datetime-local → ISO UTC
  return new Date(local).toISOString();
}

function toLocalInput(iso: string): string {
  const d = new Date(iso);
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

export default function UserDelegationPage() {
  const { hasPermission } = usePermissions();
  const canManage = hasPermission('menu.kullanici-vekalet-islemleri.manage');

  const [includeInactive, setIncludeInactive] = useState(false);
  const { data: rows, isLoading, error, refetch, isFetching } = useDelegations({ includeInactive });

  const createMutation = useCreateDelegation();
  const revokeMutation = useRevokeDelegation();

  const [createOpen, setCreateOpen] = useState(false);
  const [revokeTarget, setRevokeTarget] = useState<PermissionDelegationDto | null>(null);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  return (
    <div className="flex min-h-[calc(100vh-7rem)] flex-col">
      <Card className="flex flex-1 flex-col">
        <CardHeader className="flex flex-row items-center justify-end gap-3">
          <div className="flex items-center gap-2">
            <Label htmlFor="include-inactive" className="text-sm">Geçmişi göster</Label>
            <Switch
              id="include-inactive"
              checked={includeInactive}
              onCheckedChange={setIncludeInactive}
            />
          </div>
          {canManage && (
            <Button size="sm" onClick={() => { setErrorMsg(null); setCreateOpen(true); }}>
              <Plus className="h-4 w-4 mr-2" />
              Yeni Vekâlet
            </Button>
          )}
        </CardHeader>
        <CardContent className="flex flex-1 flex-col">
          {isLoading ? (
            <div className="flex flex-1 items-center justify-center gap-2 text-muted-foreground">
              <Loader2 className="h-4 w-4 animate-spin" />
              Yükleniyor...
            </div>
          ) : error ? (
            <div className="flex flex-1 flex-col items-center justify-center gap-3 text-center">
              <AlertCircle className="h-8 w-8 text-destructive" />
              <div className="space-y-1">
                <p className="font-medium">Vekâlet kayıtları yüklenemedi</p>
                <p className="text-sm text-muted-foreground">
                  Sunucuya ulaşılamadı. Bağlantını kontrol edip tekrar dene.
                </p>
              </div>
              <Button variant="outline" size="sm" onClick={() => refetch()} disabled={isFetching}>
                <RefreshCw className={`h-4 w-4 mr-2 ${isFetching ? 'animate-spin' : ''}`} />
                Tekrar dene
              </Button>
            </div>
          ) : !rows || rows.length === 0 ? (
            <div className="flex flex-1 flex-col items-center justify-center gap-2 text-center text-muted-foreground">
              <Inbox className="h-8 w-8 opacity-40" />
              <p className="text-sm">
                {includeInactive ? 'Hiç vekâlet kaydı yok.' : 'Aktif veya beklemede vekâlet yok.'}
              </p>
              {canManage && (
                <p className="text-xs">
                  Yeni bir vekâlet oluşturmak için sağ üstteki “Yeni Vekâlet” butonunu kullan.
                </p>
              )}
            </div>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead className="bg-muted/40 text-left text-xs uppercase text-muted-foreground">
                  <tr>
                    <th className="px-3 py-2 font-medium">Yetkiyi Veren</th>
                    <th className="px-3 py-2 font-medium">Vekil</th>
                    <th className="px-3 py-2 font-medium">Başlangıç</th>
                    <th className="px-3 py-2 font-medium">Bitiş</th>
                    <th className="px-3 py-2 font-medium">Sebep</th>
                    <th className="px-3 py-2 font-medium">Durum</th>
                    <th className="px-3 py-2 font-medium text-right">İşlem</th>
                  </tr>
                </thead>
                <tbody className="divide-y">
                  {rows.map((d) => (
                    <tr key={d.id} className="hover:bg-muted/30">
                      <td className="px-3 py-2">
                        <div className="font-medium">{d.delegatorFullName || d.delegatorUserName}</div>
                        <div className="text-xs text-muted-foreground">{d.delegatorUserName}</div>
                      </td>
                      <td className="px-3 py-2">
                        <div className="font-medium">{d.delegateeFullName || d.delegateeUserName}</div>
                        <div className="text-xs text-muted-foreground">{d.delegateeUserName}</div>
                      </td>
                      <td className="px-3 py-2 whitespace-nowrap">{formatDate(d.startsAtUtc)}</td>
                      <td className="px-3 py-2 whitespace-nowrap">{formatDate(d.endsAtUtc)}</td>
                      <td className="px-3 py-2 max-w-xs">
                        <span className="line-clamp-2" title={d.reason}>{d.reason}</span>
                        {d.revokeReason && (
                          <span
                            className="block text-xs text-destructive line-clamp-1"
                            title={d.revokeReason}
                          >
                            İptal: {d.revokeReason}
                          </span>
                        )}
                      </td>
                      <td className="px-3 py-2">
                        <Badge variant={STATUS_VARIANTS[d.status]}>
                          {STATUS_LABELS[d.status]}
                        </Badge>
                      </td>
                      <td className="px-3 py-2 text-right">
                        {canManage && (d.status === 'Active' || d.status === 'Pending') && (
                          <Button
                            variant="ghost"
                            size="sm"
                            onClick={() => { setErrorMsg(null); setRevokeTarget(d); }}
                            className="text-destructive hover:text-destructive"
                          >
                            <ShieldOff className="h-4 w-4 mr-1" />
                            İptal Et
                          </Button>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </CardContent>
      </Card>

      <CreateDelegationDialog
        open={createOpen}
        onOpenChange={(o) => { if (!o) setErrorMsg(null); setCreateOpen(o); }}
        errorMsg={errorMsg}
        isPending={createMutation.isPending}
        onSubmit={async (values) => {
          setErrorMsg(null);
          try {
            await createMutation.mutateAsync({
              delegatorUserId: values.delegatorUserId,
              delegateeUserId: values.delegateeUserId,
              startsAtUtc: toIsoUtc(values.startsAtLocal),
              endsAtUtc: toIsoUtc(values.endsAtLocal),
              reason: values.reason,
            });
            setCreateOpen(false);
          } catch (err) {
            setErrorMsg(extractError(err, 'Vekâlet oluşturulamadı.'));
          }
        }}
      />

      <RevokeDialog
        target={revokeTarget}
        onCancel={() => { setRevokeTarget(null); setErrorMsg(null); }}
        errorMsg={errorMsg}
        isPending={revokeMutation.isPending}
        onConfirm={async (reason) => {
          if (!revokeTarget) return;
          setErrorMsg(null);
          try {
            await revokeMutation.mutateAsync({
              id: revokeTarget.id,
              dto: { reason: reason || null },
            });
            setRevokeTarget(null);
          } catch (err) {
            setErrorMsg(extractError(err, 'İptal işlemi başarısız.'));
          }
        }}
      />
    </div>
  );
}

interface CreateValues {
  delegatorUserId: number;
  delegateeUserId: number;
  startsAtLocal: string;
  endsAtLocal: string;
  reason: string;
}

function CreateDelegationDialog({
  open,
  onOpenChange,
  onSubmit,
  isPending,
  errorMsg,
}: {
  open: boolean;
  onOpenChange: (o: boolean) => void;
  onSubmit: (values: CreateValues) => Promise<void>;
  isPending: boolean;
  errorMsg: string | null;
}) {
  const [delegatorId, setDelegatorId] = useState('');
  const [delegateeId, setDelegateeId] = useState('');
  const now = new Date();
  const tomorrow = new Date(now.getTime() + 24 * 3600 * 1000);
  const [startsAt, setStartsAt] = useState(toLocalInput(now.toISOString()));
  const [endsAt, setEndsAt] = useState(toLocalInput(tomorrow.toISOString()));
  const [reason, setReason] = useState('');

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    const dId = Number.parseInt(delegatorId, 10);
    const eId = Number.parseInt(delegateeId, 10);
    if (!dId || !eId) return;
    await onSubmit({
      delegatorUserId: dId,
      delegateeUserId: eId,
      startsAtLocal: startsAt,
      endsAtLocal: endsAt,
      reason: reason.trim(),
    });
  };

  // Reset form when dialog opens
  const handleOpenChange = (o: boolean) => {
    if (o) {
      setDelegatorId('');
      setDelegateeId('');
      setStartsAt(toLocalInput(now.toISOString()));
      setEndsAt(toLocalInput(tomorrow.toISOString()));
      setReason('');
    }
    onOpenChange(o);
  };

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>Yeni Vekâlet</DialogTitle>
          <DialogDescription>
            Bir kullanıcının (Yetkiyi Veren) rollerini ve scope'unu, başka bir kullanıcıya (Vekil) belirli bir
            zaman aralığında devredin. Vekil, kendi yetkilerinin yanı sıra Yetkiyi Verenin tüm yetkilerini de
            kullanabilir.
          </DialogDescription>
        </DialogHeader>
        <form onSubmit={submit} className="space-y-4">
          <div className="space-y-2">
            <Label>Yetkiyi Veren</Label>
            <UserSearchCombobox value={delegatorId} onChange={setDelegatorId} endpoint="/admin/delegations/users" />
          </div>
          <div className="space-y-2">
            <Label>Vekil</Label>
            <UserSearchCombobox value={delegateeId} onChange={setDelegateeId} endpoint="/admin/delegations/users" />
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-2">
              <Label htmlFor="starts-at">Başlangıç</Label>
              <Input
                id="starts-at"
                type="datetime-local"
                value={startsAt}
                onChange={(e) => setStartsAt(e.target.value)}
                required
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="ends-at">Bitiş</Label>
              <Input
                id="ends-at"
                type="datetime-local"
                value={endsAt}
                onChange={(e) => setEndsAt(e.target.value)}
                required
              />
            </div>
          </div>
          <div className="space-y-2">
            <Label htmlFor="reason">Sebep</Label>
            <Textarea
              id="reason"
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              placeholder="Örn: Yıllık izin (12-19 Mayıs) süresince tüm dekanlık işleri için Prof. Y vekâlet edecek."
              required
              rows={3}
              maxLength={500}
            />
            <p className="text-xs text-muted-foreground text-right">{reason.length}/500</p>
          </div>
          {errorMsg && (
            <Alert variant="destructive">
              <AlertDescription>{errorMsg}</AlertDescription>
            </Alert>
          )}
          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)} disabled={isPending}>
              İptal
            </Button>
            <Button type="submit" disabled={isPending || !delegatorId || !delegateeId || !reason.trim()}>
              {isPending ? <Loader2 className="h-4 w-4 mr-2 animate-spin" /> : null}
              Oluştur
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}

function RevokeDialog({
  target,
  onCancel,
  onConfirm,
  isPending,
  errorMsg,
}: {
  target: PermissionDelegationDto | null;
  onCancel: () => void;
  onConfirm: (reason: string) => Promise<void>;
  isPending: boolean;
  errorMsg: string | null;
}) {
  const [reason, setReason] = useState('');

  const open = target !== null;
  const handleOpenChange = (o: boolean) => {
    if (!o) {
      setReason('');
      onCancel();
    }
  };

  return (
    <AlertDialog open={open} onOpenChange={handleOpenChange}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>Vekâleti iptal et</AlertDialogTitle>
          <AlertDialogDescription>
            {target ? (
              <>
                <strong>{target.delegatorFullName || target.delegatorUserName}</strong> →{' '}
                <strong>{target.delegateeFullName || target.delegateeUserName}</strong> vekâleti iptal
                edilecek. Vekilin devraldığı yetkiler hemen geçersiz olur. Bu işlem audit'e işlenir, geri
                alınamaz.
              </>
            ) : null}
          </AlertDialogDescription>
        </AlertDialogHeader>
        <div className="space-y-2">
          <Label htmlFor="revoke-reason">İptal sebebi (opsiyonel)</Label>
          <Textarea
            id="revoke-reason"
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            rows={2}
            placeholder="Örn: İzin erken sonlandı."
            maxLength={500}
          />
        </div>
        {errorMsg && (
          <Alert variant="destructive">
            <AlertDescription>{errorMsg}</AlertDescription>
          </Alert>
        )}
        <AlertDialogFooter>
          <AlertDialogCancel disabled={isPending}>Vazgeç</AlertDialogCancel>
          <AlertDialogAction
            onClick={(e) => {
              e.preventDefault();
              void onConfirm(reason.trim());
            }}
            disabled={isPending}
            className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
          >
            {isPending ? <Loader2 className="h-4 w-4 mr-2 animate-spin" /> : null}
            İptal Et
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}
