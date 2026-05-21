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
import { AlertCircle, FolderOpen, Loader2, Plus, Trash2, ShieldCheck } from 'lucide-react';
import { useQueryClient } from '@tanstack/react-query';
import { usePermissionBundles, useDeleteBundle } from '../hooks/usePermissionBundles';
import { usePermissions } from '../context/PermissionContext';
import PermissionBundleFormDialog from '../components/admin/PermissionBundleFormDialog';
import type { PermissionBundleDto } from '../api/permissionBundle.api';

/**
 * RBAC Phase 2 — Yetki paketleri yönetimi.
 *
 * Karar 1: Snapshot — paket güncellenince bağlı role'ler etkilenmez.
 * Karar 3: Sadece SystemAdmin yönetir; system.permission-bundle.manage permission'ı.
 * Karar 4: Sidebar grubu bazlı default seed bundle'lar IsSystemDefault=true ile gelir,
 *          silinemez ama içerik düzenlenebilir.
 */
export default function UserPermissionGroupsPage() {
  const { hasPermission } = usePermissions();
  const canManage = hasPermission('system.permission-bundle.manage');
  const qc = useQueryClient();

  const { data: bundles, isLoading, error } = usePermissionBundles();
  const deleteMut = useDeleteBundle();

  const [createOpen, setCreateOpen] = useState(false);
  const [editId, setEditId] = useState<number | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<PermissionBundleDto | null>(null);

  async function handleDelete() {
    if (deleteTarget === null) return;
    await deleteMut.mutateAsync(deleteTarget.id);
    setDeleteTarget(null);
    qc.invalidateQueries({ queryKey: ['admin', 'permission-bundles'] });
  }

  return (
    <div className="space-y-6">
      {error && (
        <Alert variant="destructive">
          <AlertCircle className="h-4 w-4" />
          <AlertDescription>Paketler yüklenirken hata oluştu.</AlertDescription>
        </Alert>
      )}

      <div className="bg-background border border-border rounded-xl overflow-hidden">
        {/* Toolbar */}
        <div className="flex items-center px-4 py-3 border-b border-border bg-muted/40">
          <p className="text-xs text-muted-foreground">
            Paketler, sistem yöneticisi tarafından oluşturulur ve rol yetkilendirme matrisinde
            "Paket Uygula" ile role atanır.
          </p>
          {canManage && (
            <Button onClick={() => setCreateOpen(true)} className="h-9 ml-auto">
              <Plus className="h-4 w-4 mr-1.5" />
              Yeni Paket
            </Button>
          )}
        </div>

        {/* Table */}
        <table className="w-full">
          <thead className="bg-muted/20 border-b border-border">
            <tr>
              <th className="text-left px-4 py-3 text-xs font-semibold text-muted-foreground">Ad</th>
              <th className="text-left px-4 py-3 text-xs font-semibold text-muted-foreground w-48">Kategori</th>
              <th className="text-center px-4 py-3 text-xs font-semibold text-muted-foreground w-28">Yetki Sayısı</th>
              <th className="text-center px-4 py-3 text-xs font-semibold text-muted-foreground w-24">Sistem</th>
              <th className="px-4 py-3 w-12"></th>
            </tr>
          </thead>
          <tbody className="divide-y divide-border">
            {isLoading && (
              <tr>
                <td colSpan={5} className="px-4 py-10 text-center text-sm text-muted-foreground">
                  <Loader2 className="h-5 w-5 animate-spin mx-auto mb-2 text-muted-foreground" />
                  Yükleniyor...
                </td>
              </tr>
            )}

            {!isLoading && bundles && bundles.length === 0 && (
              <tr>
                <td colSpan={5} className="px-4 py-16 text-center">
                  <FolderOpen className="h-10 w-10 text-muted-foreground/30 mx-auto mb-3" />
                  <p className="text-sm font-medium text-foreground">Henüz paket tanımlanmamış</p>
                  {canManage && (
                    <p className="text-xs text-muted-foreground mt-1">
                      "Yeni Paket" butonuyla ilk paketi oluşturun.
                    </p>
                  )}
                </td>
              </tr>
            )}

            {!isLoading && bundles && bundles.map(b => (
              <tr
                key={b.id}
                className="cursor-pointer hover:bg-muted/20 transition-colors"
                onClick={() => setEditId(b.id)}
              >
                <td className="px-4 py-3">
                  <div className="flex items-center gap-2">
                    <span className="text-sm font-medium">{b.name}</span>
                  </div>
                  {b.description && (
                    <p className="text-xs text-muted-foreground mt-0.5 line-clamp-1">{b.description}</p>
                  )}
                </td>
                <td className="px-4 py-3 text-sm text-muted-foreground">
                  {b.category ?? <span className="italic">—</span>}
                </td>
                <td className="px-4 py-3 text-center">
                  <Badge variant="outline" className="tabular-nums text-xs">
                    {b.permissionCount}
                  </Badge>
                </td>
                <td className="px-4 py-3 text-center">
                  {b.isSystemDefault ? (
                    <Badge variant="secondary" className="text-[10px] gap-1">
                      <ShieldCheck className="h-3 w-3" />
                      Sistem
                    </Badge>
                  ) : (
                    <span className="text-[10px] text-muted-foreground">Özel</span>
                  )}
                </td>
                <td className="px-4 py-3" onClick={e => e.stopPropagation()}>
                  {canManage && !b.isSystemDefault && (
                    <Button
                      variant="ghost"
                      size="icon"
                      className="h-8 w-8 text-muted-foreground hover:text-destructive"
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

        {!isLoading && bundles && bundles.length > 0 && (
          <div className="px-4 py-2.5 border-t border-border bg-muted/30">
            <p className="text-xs text-muted-foreground">
              Toplam <span className="font-medium text-foreground">{bundles.length}</span> paket
            </p>
          </div>
        )}
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
