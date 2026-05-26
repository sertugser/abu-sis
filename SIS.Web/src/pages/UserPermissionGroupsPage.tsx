import { useState } from 'react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
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
import { cn } from '@/lib/utils';
import { AlertCircle, FolderOpen, Loader2, Plus, Trash2, ShieldCheck, Package, Users } from 'lucide-react';
import { useQueryClient } from '@tanstack/react-query';
import { usePermissionBundles, useDeleteBundle } from '../hooks/usePermissionBundles';
import { usePermissions } from '../context/PermissionContext';
import PermissionBundleFormDialog from '../components/admin/PermissionBundleFormDialog';
import RolePermissionMatrix from '../components/admin/RolePermissionMatrix';
import type { PermissionBundleDto } from '../api/permissionBundle.api';

/**
 * RBAC Phase 2 — Yetki paketleri + rollere yetki atama.
 *
 * Tek sayfa, iki bölüm (segment): "Yetki Paketleri" (bundle CRUD) ve
 * "Rollere Yetki Ata" (RolePermissionMatrix). Segment seçici sayfayla bütünleşik
 * tek bir başlık şeridindedir.
 */
type TabKey = 'bundles' | 'roles';

export default function UserPermissionGroupsPage() {
  const { hasPermission } = usePermissions();
  const canManage = hasPermission('system.permission-bundle.manage');
  const canAssignRoles = hasPermission('system.permission.view');
  const qc = useQueryClient();

  const { data: bundles, isLoading, error } = usePermissionBundles();
  const deleteMut = useDeleteBundle();

  const [tab, setTab] = useState<TabKey>('bundles');
  const [createOpen, setCreateOpen] = useState(false);
  const [editId, setEditId] = useState<number | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<PermissionBundleDto | null>(null);

  async function handleDelete() {
    if (deleteTarget === null) return;
    await deleteMut.mutateAsync(deleteTarget.id);
    setDeleteTarget(null);
    qc.invalidateQueries({ queryKey: ['admin', 'permission-bundles'] });
  }

  const segments: { key: TabKey; label: string; icon: typeof Package }[] = [
    { key: 'bundles', label: 'Yetki Paketleri', icon: Package },
    { key: 'roles', label: 'Rollere Yetki Ata', icon: Users },
  ];

  const bundlesPanel = (
    <div className="flex min-h-0 flex-1 flex-col">
      {error && (
        <Alert variant="destructive" className="m-4 mb-0">
          <AlertCircle className="h-4 w-4" />
          <AlertDescription>Paketler yüklenirken hata oluştu.</AlertDescription>
        </Alert>
      )}

      <div className="flex min-h-0 flex-1 flex-col overflow-hidden">
        <div className="min-h-0 flex-1 overflow-auto">
          <table className="w-full border-separate border-spacing-0">
            <thead className="sticky top-0 z-10 bg-card">
              <tr className="border-b border-border">
                <th className="border-b border-border px-5 py-3 text-left text-[11px] font-semibold uppercase tracking-wide text-muted-foreground">Ad</th>
                <th className="w-48 border-b border-border px-5 py-3 text-left text-[11px] font-semibold uppercase tracking-wide text-muted-foreground">Kategori</th>
                <th className="w-28 border-b border-border px-5 py-3 text-center text-[11px] font-semibold uppercase tracking-wide text-muted-foreground">Yetki</th>
                <th className="w-24 border-b border-border px-5 py-3 text-center text-[11px] font-semibold uppercase tracking-wide text-muted-foreground">Tür</th>
                <th className="w-12 border-b border-border px-5 py-3"></th>
              </tr>
            </thead>
            <tbody>
              {isLoading && (
                <tr>
                  <td colSpan={5} className="px-5 py-16 text-center text-sm text-muted-foreground">
                    <Loader2 className="mx-auto mb-2 h-5 w-5 animate-spin text-muted-foreground" />
                    Yükleniyor...
                  </td>
                </tr>
              )}

              {!isLoading && bundles && bundles.length === 0 && (
                <tr>
                  <td colSpan={5} className="px-5 py-20 text-center">
                    <FolderOpen className="mx-auto mb-3 h-10 w-10 text-muted-foreground/30" />
                    <p className="text-sm font-medium text-foreground">Henüz paket tanımlanmamış</p>
                    {canManage && (
                      <p className="mt-1 text-xs text-muted-foreground">
                        Sağ üstteki “Yeni Paket” ile ilk paketi oluşturun.
                      </p>
                    )}
                  </td>
                </tr>
              )}

              {!isLoading && bundles && bundles.map(b => (
                <tr
                  key={b.id}
                  className="group cursor-pointer border-b border-border/60 transition-colors last:border-0 hover:bg-primary/[0.04]"
                  onClick={() => setEditId(b.id)}
                >
                  <td className="px-5 py-3.5">
                    <span className="text-sm font-medium text-foreground">{b.name}</span>
                    {b.description && (
                      <p className="mt-0.5 line-clamp-1 text-xs text-muted-foreground">{b.description}</p>
                    )}
                  </td>
                  <td className="px-5 py-3.5 text-sm text-muted-foreground">
                    {b.category ?? <span className="italic">—</span>}
                  </td>
                  <td className="px-5 py-3.5 text-center">
                    <Badge variant="outline" className="tabular-nums text-xs">
                      {b.permissionCount}
                    </Badge>
                  </td>
                  <td className="px-5 py-3.5 text-center">
                    {b.isSystemDefault ? (
                      <Badge variant="secondary" className="gap-1 text-[10px]">
                        <ShieldCheck className="h-3 w-3" />
                        Sistem
                      </Badge>
                    ) : (
                      <Badge variant="outline" className="text-[10px] text-muted-foreground">Özel</Badge>
                    )}
                  </td>
                  <td className="px-5 py-3.5" onClick={e => e.stopPropagation()}>
                    {canManage && !b.isSystemDefault && (
                      <Button
                        variant="ghost"
                        size="icon"
                        className="h-8 w-8 text-muted-foreground opacity-0 transition-opacity hover:text-destructive group-hover:opacity-100"
                        onClick={() => setDeleteTarget(b)}
                        title="Sil"
                      >
                        <Trash2 className="h-4 w-4" />
                      </Button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        {!isLoading && bundles && bundles.length > 0 && (
          <div className="border-t border-border px-5 py-2.5">
            <p className="text-xs text-muted-foreground">
              Toplam <span className="font-medium text-foreground">{bundles.length}</span> paket
            </p>
          </div>
        )}
      </div>
    </div>
  );

  return (
    <div className="flex min-h-[calc(100vh-7rem)] flex-col">
      {/* Sekmeler + içerik TEK kutuda */}
      <div className="flex min-h-0 flex-1 flex-col overflow-hidden rounded-xl border border-border bg-card shadow-sm">
        {/* Kutunun başlık şeridi: segment seçici + bağlamsal aksiyon */}
        <div className="flex flex-wrap items-center justify-between gap-3 border-b border-border px-3 py-2.5">
          {canAssignRoles ? (
            <div className="inline-flex items-center gap-1 rounded-lg bg-muted/60 p-1">
              {segments.map(s => {
                const Icon = s.icon;
                const active = tab === s.key;
                return (
                  <button
                    key={s.key}
                    type="button"
                    onClick={() => setTab(s.key)}
                    className={cn(
                      'inline-flex items-center gap-2 rounded-md px-4 py-1.5 text-sm font-medium transition-colors',
                      active
                        ? 'bg-card text-foreground shadow-sm'
                        : 'text-muted-foreground hover:text-foreground',
                    )}
                  >
                    <Icon className="h-4 w-4" />
                    {s.label}
                  </button>
                );
              })}
            </div>
          ) : (
            <div className="inline-flex items-center gap-2 px-1 text-sm font-semibold text-foreground">
              <Package className="h-4 w-4 text-primary" />
              Yetki Paketleri
            </div>
          )}

          {tab === 'bundles' && canManage && (
            <Button onClick={() => setCreateOpen(true)} className="h-9 gap-1.5 shadow-sm">
              <Plus className="h-4 w-4" />
              Yeni Paket
            </Button>
          )}
        </div>

        {/* Kutunun gövdesi — kalan yüksekliği doldurur */}
        <div className="flex min-h-0 flex-1 flex-col">
          {tab === 'bundles' ? bundlesPanel : <RolePermissionMatrix />}
        </div>
      </div>

      <PermissionBundleFormDialog
        open={createOpen}
        onOpenChange={setCreateOpen}
        bundleId={null}
      />
      <PermissionBundleFormDialog
        key={editId ?? 'edit-closed'}
        open={editId !== null}
        onOpenChange={open => { if (!open) setEditId(null); }}
        bundleId={editId}
      />

      <AlertDialog open={deleteTarget !== null} onOpenChange={open => { if (!open) setDeleteTarget(null); }}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Paketi sil?</AlertDialogTitle>
            <AlertDialogDescription>
              <span className="font-medium text-foreground">{deleteTarget?.name}</span> paketi silinecek.
              Bu paketten role atanmış yetkiler de otomatik kaldırılır. Manuel atanmış yetkiler etkilenmez.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel disabled={deleteMut.isPending}>Vazgeç</AlertDialogCancel>
            <AlertDialogAction
              onClick={handleDelete}
              disabled={deleteMut.isPending}
              className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
            >
              {deleteMut.isPending ? 'Siliniyor…' : 'Sil'}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}
