import { useState } from 'react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
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
import { Plus, Pencil, Trash2, Pin, AlertCircle, EyeOff, Eye } from 'lucide-react';
import AnnouncementFormDialog from '../components/announcements/AnnouncementFormDialog';
import {
  useAllAnnouncements,
  useAnnouncementDetail,
  useDeleteAnnouncement,
  usePermanentDeleteAnnouncement,
  useReactivateAnnouncement,
} from '../hooks/useAnnouncements';
import { getAudienceBadgeLabel } from '../types/announcements';
import { formatDate } from '../lib/utils';

export default function AnnouncementManagementPage() {
  const { data, isLoading, isError } = useAllAnnouncements();
  const deleteMutation        = useDeleteAnnouncement();
  const permDeleteMutation    = usePermanentDeleteAnnouncement();
  const reactivateMutation    = useReactivateAnnouncement();

  const [formOpen,         setFormOpen]         = useState(false);
  const [editingId,        setEditingId]        = useState<number | null>(null);
  const [deletingId,       setDeletingId]       = useState<number | null>(null);
  const [permDeletingId,   setPermDeletingId]   = useState<number | null>(null);

  const { data: editingDetail } = useAnnouncementDetail(editingId, editingId != null);

  function handleCreateClick() {
    setEditingId(null);
    setFormOpen(true);
  }

  function handleEditClick(id: number) {
    setEditingId(id);
    setFormOpen(true);
  }

  function handleFormClose(open: boolean) {
    setFormOpen(open);
    if (!open) setEditingId(null);
  }

  async function confirmDelete() {
    if (deletingId == null) return;
    try {
      await deleteMutation.mutateAsync(deletingId);
    } finally {
      setDeletingId(null);
    }
  }

  async function confirmPermDelete() {
    if (permDeletingId == null) return;
    try {
      await permDeleteMutation.mutateAsync(permDeletingId);
    } catch {
      // error exposed via permDeleteMutation.error if needed
    } finally {
      setPermDeletingId(null);
    }
  }

  async function handleReactivate(id: number) {
    await reactivateMutation.mutateAsync(id);
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between flex-wrap gap-3">
        <div>
          <h1 className="font-heading text-xl font-bold">Duyuru Yönetimi</h1>
          <p className="text-sm text-muted-foreground">
            Duyurularınızı oluşturun, düzenleyin veya kaldırın.
          </p>
        </div>
        <Button onClick={handleCreateClick}>
          <Plus className="h-4 w-4 mr-1.5" />
          Yeni Duyuru
        </Button>
      </div>

      {isError && (
        <Alert variant="destructive">
          <AlertCircle className="h-4 w-4" />
          <AlertDescription>Duyurular yüklenemedi.</AlertDescription>
        </Alert>
      )}

      {isLoading ? (
        <Skeleton className="h-64 rounded-xl" />
      ) : (
        <Card>
          <CardContent className="p-0">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Başlık</TableHead>
                  <TableHead>Hedef Kitle</TableHead>
                  <TableHead>Başlangıç</TableHead>
                  <TableHead>Bitiş</TableHead>
                  <TableHead>Durum</TableHead>
                  <TableHead className="text-right">İşlem</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {(data ?? []).length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={6} className="text-center text-muted-foreground py-8">
                      Duyuru bulunmuyor.
                    </TableCell>
                  </TableRow>
                ) : (
                  data?.map((a) => (
                    <TableRow key={a.id}>
                      <TableCell>
                        <div className="flex items-center gap-2 min-w-0">
                          {a.isPinned && <Pin className="h-3.5 w-3.5 text-amber-500 shrink-0" />}
                          <span className="font-medium truncate">{a.title}</span>
                        </div>
                        <p className="text-xs text-muted-foreground truncate mt-0.5">
                          {a.createdByName}
                        </p>
                      </TableCell>
                      <TableCell>
                        <Badge variant="outline" className="text-[10px] max-w-[160px] truncate">
                          {getAudienceBadgeLabel(a)}
                        </Badge>
                      </TableCell>
                      <TableCell className="text-sm text-muted-foreground">
                        {formatDate(a.startsAt)}
                      </TableCell>
                      <TableCell className="text-sm text-muted-foreground">
                        {a.expiresAt ? formatDate(a.expiresAt) : '—'}
                      </TableCell>
                      <TableCell>
                        <StatusBadge announcement={a} />
                      </TableCell>
                      <TableCell className="text-right">
                        <div className="flex justify-end gap-1">
                          <Button
                            size="icon"
                            variant="ghost"
                            className="h-8 w-8"
                            onClick={() => handleEditClick(a.id)}
                          >
                            <Pencil className="h-4 w-4" />
                          </Button>
                          {a.isActive ? (
                            <Button
                              size="icon"
                              variant="ghost"
                              className="h-8 w-8"
                              onClick={() => setDeletingId(a.id)}
                              title="Geri Çek"
                            >
                              <EyeOff className="h-4 w-4 text-muted-foreground" />
                            </Button>
                          ) : (
                            <Button
                              size="icon"
                              variant="ghost"
                              className="h-8 w-8"
                              onClick={() => handleReactivate(a.id)}
                              title="Yeniden Yayınla"
                            >
                              <Eye className="h-4 w-4 text-green-600" />
                            </Button>
                          )}
                          <Button
                            size="icon"
                            variant="ghost"
                            className="h-8 w-8"
                            onClick={() => setPermDeletingId(a.id)}
                            title="Kalıcı Sil"
                          >
                            <Trash2 className="h-4 w-4 text-destructive" />
                          </Button>
                        </div>
                      </TableCell>
                    </TableRow>
                  ))
                )}
              </TableBody>
            </Table>
          </CardContent>
        </Card>
      )}

      <AnnouncementFormDialog
        open={formOpen}
        onOpenChange={handleFormClose}
        editing={editingId ? editingDetail ?? null : null}
      />

      <AlertDialog open={deletingId != null} onOpenChange={(open) => !open && setDeletingId(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Duyuruyu geri çek?</AlertDialogTitle>
            <AlertDialogDescription>
              Duyuru geri çekilecek ve artık kimseye görünmeyecek. Gerekirse tekrar yayına alabilirsiniz.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>İptal</AlertDialogCancel>
            <AlertDialogAction onClick={confirmDelete}>Geri Çek</AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      <AlertDialog open={permDeletingId != null} onOpenChange={(open) => !open && setPermDeletingId(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Duyuruyu kalıcı olarak sil?</AlertDialogTitle>
            <AlertDialogDescription>
              Bu duyuru veritabanından <strong>kalıcı olarak silinecektir</strong>. Bu işlem kesinlikle geri alınamaz.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>İptal</AlertDialogCancel>
            <AlertDialogAction onClick={confirmPermDelete}>
              Kalıcı Sil
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}

function StatusBadge({
  announcement,
}: {
  announcement: { isActive: boolean; startsAt: string; expiresAt: string | null };
}) {
  const now     = new Date();
  const starts  = new Date(announcement.startsAt);
  const expires = announcement.expiresAt ? new Date(announcement.expiresAt) : null;

  if (!announcement.isActive)          return <Badge variant="secondary">Pasif</Badge>;
  if (now < starts)                    return <Badge variant="outline">Planlandı</Badge>;
  if (expires && now > expires)        return <Badge variant="secondary">Süresi Doldu</Badge>;
  return <Badge variant="success">Aktif</Badge>;
}
