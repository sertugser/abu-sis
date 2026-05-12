import { useState } from 'react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
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
import {
  AlertTriangle,
  BarChart3,
  Plus,
  Pencil,
  Trash2,
  Pin,
  AlertCircle,
  EyeOff,
  Eye,
} from 'lucide-react';
import AnnouncementFormDialog from '../components/announcements/AnnouncementFormDialog';
import AnnouncementStatsSheet from '../components/announcements/AnnouncementStatsSheet';
import AnnouncementStatsBar from '../components/announcements/AnnouncementStatsBar';
import AnnouncementsEmptyState from '../components/announcements/AnnouncementsEmptyState';
import {
  useAllAnnouncements,
  useAnnouncementDetail,
  useDeleteAnnouncement,
  usePermanentDeleteAnnouncement,
  useReactivateAnnouncement,
} from '../hooks/useAnnouncements';
import { getAudienceBadgeLabel, type AnnouncementList } from '../types/announcements';
import { formatDate } from '../lib/utils';

export default function AnnouncementManagementPage() {
  const { data, isLoading, isError } = useAllAnnouncements();
  const deleteMutation     = useDeleteAnnouncement();
  const permDeleteMutation = usePermanentDeleteAnnouncement();
  const reactivateMutation = useReactivateAnnouncement();

  const [formOpen,       setFormOpen]       = useState(false);
  const [editingId,      setEditingId]      = useState<number | null>(null);
  const [deletingId,     setDeletingId]     = useState<number | null>(null);
  const [permDeletingId, setPermDeletingId] = useState<number | null>(null);
  const [statsFor,       setStatsFor]       = useState<{ id: number; title: string } | null>(null);

  const { data: editingDetail } = useAnnouncementDetail(editingId, editingId != null);

  const items = data ?? [];

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
    <>
      {isError && (
        <Alert variant="destructive" className="mb-4">
          <AlertCircle className="h-4 w-4" />
          <AlertDescription>Duyurular yüklenemedi.</AlertDescription>
        </Alert>
      )}

      {/* ─── Tek bütün kart: sayaçlar + aksiyon + tablo / empty state ─── */}
      <Card className="overflow-hidden border-border/70 shadow-sm">
        {/* Üst şerit: sayaçlar solda, "Yeni Duyuru" sağda */}
        <div className="flex flex-wrap items-center justify-between gap-3 px-5 py-4 border-b border-border/60 bg-gradient-to-r from-card via-card to-accent/20">
          {isLoading ? (
            <Skeleton className="h-9 w-full max-w-md" />
          ) : (
            <AnnouncementStatsBar items={items} />
          )}
          <Button
            onClick={handleCreateClick}
            className="bg-gradient-to-br from-primary to-primary-light shadow-md hover:shadow-lg transition-shadow"
          >
            <Plus className="h-4 w-4 mr-1.5" />
            Yeni Duyuru
          </Button>
        </div>

        {/* İçerik bölümü */}
        {isLoading ? (
          <div className="p-5">
            <Skeleton className="h-48 rounded-xl" />
          </div>
        ) : items.length === 0 ? (
          <div className="p-5">
            <AnnouncementsEmptyState
              title="Henüz duyuru yok"
              description="İlk duyurunuzu oluşturarak öğrencileri, akademisyenleri ve personeli bilgilendirmeye başlayın."
              action={
                <Button
                  onClick={handleCreateClick}
                  className="bg-gradient-to-br from-primary to-primary-light shadow-md hover:shadow-lg transition-shadow"
                >
                  <Plus className="h-4 w-4 mr-1.5" />
                  İlk Duyuruyu Oluştur
                </Button>
              }
            />
          </div>
        ) : (
          <Table>
            <TableHeader>
              <TableRow className="bg-muted/30 hover:bg-muted/30">
                <TableHead>Başlık</TableHead>
                <TableHead>Hedef Kitle</TableHead>
                <TableHead>Başlangıç</TableHead>
                <TableHead>Bitiş</TableHead>
                <TableHead>Durum</TableHead>
                <TableHead className="text-right">İşlem</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {items.map((a) => (
                <TableRow
                  key={a.id}
                  className="transition-colors hover:bg-accent/20 cursor-default"
                >
                  <TableCell>
                    <div className="flex items-center gap-2 min-w-0">
                      {a.isPinned && (
                        <Pin
                          className="h-3.5 w-3.5 text-amber-500 shrink-0"
                          aria-label="Sabitlenmiş"
                        />
                      )}
                      {a.isPopup && (
                        <AlertTriangle
                          className={`h-3.5 w-3.5 shrink-0 ${
                            a.requiresAcknowledgement ? 'text-destructive' : 'text-blue-500'
                          }`}
                          aria-label={
                            a.requiresAcknowledgement ? 'Pop-up (onay zorunlu)' : 'Pop-up'
                          }
                        />
                      )}
                      {a.category && (
                        <span
                          className="h-2 w-2 rounded-full shrink-0"
                          style={{ backgroundColor: a.category.colorHex }}
                          title={a.category.name}
                        />
                      )}
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
                  <TableCell className="text-sm text-muted-foreground tabular-nums">
                    {formatDate(a.startsAt)}
                  </TableCell>
                  <TableCell className="text-sm text-muted-foreground tabular-nums">
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
                        className="h-8 w-8 hover:bg-primary/10 hover:text-primary"
                        onClick={() => setStatsFor({ id: a.id, title: a.title })}
                        title="İstatistik"
                      >
                        <BarChart3 className="h-4 w-4" />
                      </Button>
                      <Button
                        size="icon"
                        variant="ghost"
                        className="h-8 w-8 hover:bg-primary/10 hover:text-primary"
                        onClick={() => handleEditClick(a.id)}
                        title="Düzenle"
                      >
                        <Pencil className="h-4 w-4" />
                      </Button>
                      {a.isActive ? (
                        <Button
                          size="icon"
                          variant="ghost"
                          className="h-8 w-8 hover:bg-amber-500/10 hover:text-amber-600"
                          onClick={() => setDeletingId(a.id)}
                          title="Geri Çek"
                        >
                          <EyeOff className="h-4 w-4" />
                        </Button>
                      ) : (
                        <Button
                          size="icon"
                          variant="ghost"
                          className="h-8 w-8 hover:bg-emerald-500/10 hover:text-emerald-600"
                          onClick={() => handleReactivate(a.id)}
                          title="Yeniden Yayınla"
                        >
                          <Eye className="h-4 w-4 text-emerald-600" />
                        </Button>
                      )}
                      <Button
                        size="icon"
                        variant="ghost"
                        className="h-8 w-8 hover:bg-destructive/10"
                        onClick={() => setPermDeletingId(a.id)}
                        title="Kalıcı Sil"
                      >
                        <Trash2 className="h-4 w-4 text-destructive" />
                      </Button>
                    </div>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </Card>

      <AnnouncementFormDialog
        open={formOpen}
        onOpenChange={handleFormClose}
        editing={editingId ? editingDetail ?? null : null}
      />

      <AnnouncementStatsSheet
        announcementId={statsFor?.id ?? null}
        announcementTitle={statsFor?.title ?? ''}
        onClose={() => setStatsFor(null)}
      />

      <AlertDialog
        open={deletingId != null}
        onOpenChange={(open) => !open && setDeletingId(null)}
      >
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

      <AlertDialog
        open={permDeletingId != null}
        onOpenChange={(open) => !open && setPermDeletingId(null)}
      >
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Duyuruyu kalıcı olarak sil?</AlertDialogTitle>
            <AlertDialogDescription>
              Bu duyuru veritabanından <strong>kalıcı olarak silinecektir</strong>. Bu işlem kesinlikle geri alınamaz.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>İptal</AlertDialogCancel>
            <AlertDialogAction onClick={confirmPermDelete}>Kalıcı Sil</AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </>
  );
}

function StatusBadge({ announcement }: { announcement: AnnouncementList }) {
  const now     = new Date();
  const starts  = new Date(announcement.startsAt);
  const expires = announcement.expiresAt ? new Date(announcement.expiresAt) : null;

  if (!announcement.isActive)
    return (
      <Badge variant="secondary" className="bg-slate-500/10 text-slate-700 border-0">
        Pasif
      </Badge>
    );
  if (now < starts)
    return (
      <Badge variant="outline" className="bg-blue-500/10 text-blue-700 border-blue-500/30">
        Planlandı
      </Badge>
    );
  if (expires && now > expires)
    return (
      <Badge variant="secondary" className="bg-amber-500/10 text-amber-700 border-0">
        Süresi Doldu
      </Badge>
    );
  return (
    <Badge variant="success" className="gap-1">
      <span className="h-1.5 w-1.5 rounded-full bg-white/90" />
      Aktif
    </Badge>
  );
}
