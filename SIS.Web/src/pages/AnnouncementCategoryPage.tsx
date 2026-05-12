import { useState } from 'react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Skeleton } from '@/components/ui/skeleton';
import {
  Dialog,
  DialogContent,
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
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { AlertCircle, Lock, Pencil, Plus, Trash2 } from 'lucide-react';
import {
  useAnnouncementCategories,
  useCreateAnnouncementCategory,
  useDeleteAnnouncementCategory,
  useUpdateAnnouncementCategory,
} from '../hooks/useAnnouncementCategories';
import type { AnnouncementCategory } from '../types/announcementCategories';

interface FormState {
  name: string;
  colorHex: string;
  iconName: string;
  sortOrder: number;
}

const HEX_PATTERN = /^#[0-9a-fA-F]{6}$/;

function emptyForm(): FormState {
  return { name: '', colorHex: '#3b82f6', iconName: '', sortOrder: 0 };
}

export default function AnnouncementCategoryPage() {
  const { data, isLoading } = useAnnouncementCategories();
  const createMutation = useCreateAnnouncementCategory();
  const updateMutation = useUpdateAnnouncementCategory();
  const deleteMutation = useDeleteAnnouncementCategory();

  const [formOpen, setFormOpen] = useState(false);
  const [editing, setEditing] = useState<AnnouncementCategory | null>(null);
  const [form, setForm] = useState<FormState>(emptyForm());
  const [error, setError] = useState<string | null>(null);
  const [deletingId, setDeletingId] = useState<number | null>(null);

  const isPending = createMutation.isPending || updateMutation.isPending;

  function openCreate() {
    setEditing(null);
    setForm(emptyForm());
    setError(null);
    setFormOpen(true);
  }

  function openEdit(category: AnnouncementCategory) {
    setEditing(category);
    setForm({
      name: category.name,
      colorHex: category.colorHex,
      iconName: category.iconName,
      sortOrder: category.sortOrder,
    });
    setError(null);
    setFormOpen(true);
  }

  async function handleSubmit() {
    setError(null);
    const name = form.name.trim();
    if (!name) {
      setError('Kategori adı zorunludur.');
      return;
    }
    if (!HEX_PATTERN.test(form.colorHex)) {
      setError('Renk kodu HEX formatında olmalıdır (örn. #3b82f6).');
      return;
    }

    try {
      if (editing) {
        await updateMutation.mutateAsync({
          id: editing.id,
          payload: {
            name,
            colorHex: form.colorHex,
            iconName: form.iconName.trim(),
            sortOrder: form.sortOrder,
          },
        });
      } else {
        await createMutation.mutateAsync({
          name,
          colorHex: form.colorHex,
          iconName: form.iconName.trim(),
          sortOrder: form.sortOrder,
        });
      }
      setFormOpen(false);
    } catch (err) {
      const message =
        (err as { response?: { data?: { error?: string } } })?.response?.data?.error ??
        'Kategori kaydedilemedi.';
      setError(message);
    }
  }

  async function confirmDelete() {
    if (deletingId == null) return;
    try {
      await deleteMutation.mutateAsync(deletingId);
    } finally {
      setDeletingId(null);
    }
  }

  const categories = data ?? [];

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between flex-wrap gap-3">
        <div>
          <h1 className="font-heading text-xl font-bold">Duyuru Kategorileri</h1>
          <p className="text-sm text-muted-foreground">
            Duyuruları sınıflandırmak için kategori oluşturun. Sistem kategorileri silinemez,
            ancak görsel özellikleri (ad / renk / ikon) düzenlenebilir.
          </p>
        </div>
        <Button onClick={openCreate}>
          <Plus className="h-4 w-4 mr-1.5" />
          Yeni Kategori
        </Button>
      </div>

      {isLoading ? (
        <Skeleton className="h-64 rounded-xl" />
      ) : (
        <Card>
          <CardContent className="p-0">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead className="w-12">Renk</TableHead>
                  <TableHead>Ad</TableHead>
                  <TableHead>Icon</TableHead>
                  <TableHead className="w-24 text-right">Sıra</TableHead>
                  <TableHead className="w-24">Tip</TableHead>
                  <TableHead className="w-32 text-right">İşlem</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {categories.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={6} className="text-center text-muted-foreground py-8">
                      Henüz kategori eklenmemiş.
                    </TableCell>
                  </TableRow>
                ) : (
                  categories.map((cat) => (
                    <TableRow key={cat.id}>
                      <TableCell>
                        <span
                          className="inline-block h-6 w-6 rounded-md border border-border/60"
                          style={{ backgroundColor: cat.colorHex }}
                          title={cat.colorHex}
                        />
                      </TableCell>
                      <TableCell className="font-medium">{cat.name}</TableCell>
                      <TableCell className="text-sm text-muted-foreground">
                        {cat.iconName || '—'}
                      </TableCell>
                      <TableCell className="text-right text-sm text-muted-foreground">
                        {cat.sortOrder}
                      </TableCell>
                      <TableCell>
                        {cat.isSystem ? (
                          <Badge variant="outline" className="gap-1 text-[10px]">
                            <Lock className="h-3 w-3" />
                            Sistem
                          </Badge>
                        ) : (
                          <Badge variant="secondary" className="text-[10px]">
                            Özel
                          </Badge>
                        )}
                      </TableCell>
                      <TableCell className="text-right">
                        <div className="flex justify-end gap-1">
                          <Button
                            size="icon"
                            variant="ghost"
                            className="h-8 w-8"
                            onClick={() => openEdit(cat)}
                            title="Düzenle"
                          >
                            <Pencil className="h-4 w-4" />
                          </Button>
                          {!cat.isSystem && (
                            <Button
                              size="icon"
                              variant="ghost"
                              className="h-8 w-8"
                              onClick={() => setDeletingId(cat.id)}
                              title="Sil"
                            >
                              <Trash2 className="h-4 w-4 text-destructive" />
                            </Button>
                          )}
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

      {/* Create / Edit Form Dialog */}
      <Dialog open={formOpen} onOpenChange={setFormOpen}>
        <DialogContent className="max-w-md">
          <DialogHeader>
            <DialogTitle>{editing ? 'Kategoriyi Düzenle' : 'Yeni Kategori'}</DialogTitle>
          </DialogHeader>
          <div className="space-y-4">
            <div>
              <Label htmlFor="name">Ad</Label>
              <Input
                id="name"
                value={form.name}
                onChange={(e) => setForm({ ...form, name: e.target.value })}
                maxLength={100}
                placeholder="Örn. Akademik"
              />
            </div>

            <div className="grid grid-cols-2 gap-3">
              <div>
                <Label htmlFor="color">Renk (HEX)</Label>
                <div className="flex items-center gap-2">
                  <Input
                    id="color"
                    type="color"
                    value={form.colorHex}
                    onChange={(e) => setForm({ ...form, colorHex: e.target.value })}
                    className="w-14 h-9 p-1"
                  />
                  <Input
                    value={form.colorHex}
                    onChange={(e) => setForm({ ...form, colorHex: e.target.value })}
                    placeholder="#3b82f6"
                    maxLength={7}
                  />
                </div>
              </div>
              <div>
                <Label htmlFor="sortOrder">Sıra</Label>
                <Input
                  id="sortOrder"
                  type="number"
                  value={form.sortOrder}
                  onChange={(e) => setForm({ ...form, sortOrder: Number(e.target.value) || 0 })}
                />
              </div>
            </div>

            <div>
              <Label htmlFor="icon">Icon Adı (Opsiyonel)</Label>
              <Input
                id="icon"
                value={form.iconName}
                onChange={(e) => setForm({ ...form, iconName: e.target.value })}
                placeholder="Örn. GraduationCap (Lucide icon adı)"
                maxLength={50}
              />
              <p className="text-[11px] text-muted-foreground mt-1">
                <a
                  href="https://lucide.dev/icons/"
                  target="_blank"
                  rel="noopener noreferrer"
                  className="text-primary hover:underline"
                >
                  Lucide
                </a>{' '}
                kütüphanesinden bir icon adı.
              </p>
            </div>

            {error && (
              <Alert variant="destructive">
                <AlertCircle className="h-4 w-4" />
                <AlertDescription>{error}</AlertDescription>
              </Alert>
            )}
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setFormOpen(false)} disabled={isPending}>
              İptal
            </Button>
            <Button onClick={handleSubmit} disabled={isPending}>
              {isPending ? 'Kaydediliyor...' : editing ? 'Güncelle' : 'Oluştur'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Delete Confirm */}
      <AlertDialog open={deletingId != null} onOpenChange={(open) => !open && setDeletingId(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Kategoriyi sil?</AlertDialogTitle>
            <AlertDialogDescription>
              Bu kategoriyi kullanan duyurular kategorisiz duruma geçer. Bu işlem geri alınamaz.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>İptal</AlertDialogCancel>
            <AlertDialogAction onClick={confirmDelete}>Sil</AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}
