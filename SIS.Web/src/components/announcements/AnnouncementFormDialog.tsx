import { useEffect, useState } from 'react';
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Switch } from '@/components/ui/switch';
import { AlertCircle } from 'lucide-react';
import {
  useCreateAnnouncement,
  useUpdateAnnouncement,
} from '../../hooks/useAnnouncements';
import { useAdminDepartments } from '../../hooks/useAdminLookup';
import type {
  AnnouncementAudience,
  AnnouncementDetail,
} from '../../types/announcements';
import { ANNOUNCEMENT_AUDIENCE_LABELS } from '../../types/announcements';

interface AnnouncementFormDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  editing: AnnouncementDetail | null;
}

interface FormState {
  title: string;
  content: string;
  targetAudience: AnnouncementAudience;
  targetDepartmentId: number | null;
  isPinned: boolean;
  startsAt: string;
  expiresAt: string;
}

function emptyForm(): FormState {
  return {
    title: '',
    content: '',
    targetAudience: 'All',
    targetDepartmentId: null,
    isPinned: false,
    startsAt: '',
    expiresAt: '',
  };
}

function toInputDateTime(iso: string | null): string {
  if (!iso) return '';
  const d = new Date(iso);
  if (isNaN(d.getTime())) return '';
  const pad = (n: number) => n.toString().padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

export default function AnnouncementFormDialog({
  open,
  onOpenChange,
  editing,
}: AnnouncementFormDialogProps) {
  const createMutation = useCreateAnnouncement();
  const updateMutation = useUpdateAnnouncement();
  const { data: departments } = useAdminDepartments();

  const [form, setForm] = useState<FormState>(emptyForm);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!open) return;
    if (editing) {
      // eslint-disable-next-line react-hooks/set-state-in-effect -- initializing dialog form from props on open; intentional sync
      setForm({
        title: editing.title,
        content: editing.content,
        targetAudience: editing.targetAudience,
        targetDepartmentId: editing.targetDepartmentId,
        isPinned: editing.isPinned,
        startsAt: toInputDateTime(editing.startsAt),
        expiresAt: toInputDateTime(editing.expiresAt),
      });
    } else {
      setForm(emptyForm());
    }
    setError(null);
  }, [open, editing]);

  const isPending = createMutation.isPending || updateMutation.isPending;

  async function handleSubmit() {
    setError(null);
    if (!form.title.trim()) {
      setError('Başlık zorunludur.');
      return;
    }
    if (!form.content.trim()) {
      setError('İçerik zorunludur.');
      return;
    }
    if (form.targetAudience === 'Department' && !form.targetDepartmentId) {
      setError('Bölüm hedeflemesi için bölüm seçin.');
      return;
    }
    if (form.startsAt && form.expiresAt) {
      if (new Date(form.expiresAt) <= new Date(form.startsAt)) {
        setError('Bitiş tarihi başlangıçtan sonra olmalıdır.');
        return;
      }
    }

    const startsAt = form.startsAt ? new Date(form.startsAt).toISOString() : null;
    const expiresAt = form.expiresAt ? new Date(form.expiresAt).toISOString() : null;

    try {
      if (editing) {
        await updateMutation.mutateAsync({
          id: editing.id,
          payload: {
            title: form.title.trim(),
            content: form.content.trim(),
            targetAudience: form.targetAudience,
            targetDepartmentId: form.targetAudience === 'Department' ? form.targetDepartmentId : null,
            isPinned: form.isPinned,
            startsAt: startsAt ?? new Date().toISOString(),
            expiresAt,
          },
        });
      } else {
        await createMutation.mutateAsync({
          title: form.title.trim(),
          content: form.content.trim(),
          targetAudience: form.targetAudience,
          targetDepartmentId: form.targetAudience === 'Department' ? form.targetDepartmentId : null,
          isPinned: form.isPinned,
          startsAt,
          expiresAt,
        });
      }
      onOpenChange(false);
    } catch (err) {
      const message =
        (err as { response?: { data?: { error?: string } } })?.response?.data?.error ??
        'Duyuru kaydedilemedi.';
      setError(message);
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-2xl max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>{editing ? 'Duyuruyu Düzenle' : 'Yeni Duyuru'}</DialogTitle>
        </DialogHeader>

        <div className="space-y-4">
          <div>
            <Label htmlFor="title">Başlık</Label>
            <Input
              id="title"
              value={form.title}
              onChange={(e) => setForm({ ...form, title: e.target.value })}
              maxLength={200}
              placeholder="Duyuru başlığı"
            />
          </div>

          <div>
            <Label htmlFor="content">İçerik</Label>
            <Textarea
              id="content"
              value={form.content}
              onChange={(e) => setForm({ ...form, content: e.target.value })}
              rows={6}
              maxLength={5000}
              placeholder="Duyuru içeriği"
            />
            <p className="text-xs text-muted-foreground mt-1">{form.content.length} / 5000</p>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div>
              <Label htmlFor="audience">Hedef Kitle</Label>
              <select
                id="audience"
                value={form.targetAudience}
                onChange={(e) =>
                  setForm({
                    ...form,
                    targetAudience: e.target.value as AnnouncementAudience,
                    targetDepartmentId: e.target.value === 'Department' ? form.targetDepartmentId : null,
                  })
                }
                className="w-full bg-card border border-input rounded-md px-3 py-2 text-sm"
              >
                {Object.entries(ANNOUNCEMENT_AUDIENCE_LABELS).map(([key, label]) => (
                  <option key={key} value={key}>
                    {label}
                  </option>
                ))}
              </select>
            </div>

            {form.targetAudience === 'Department' && (
              <div>
                <Label htmlFor="department">Bölüm</Label>
                <select
                  id="department"
                  value={form.targetDepartmentId ?? ''}
                  onChange={(e) =>
                    setForm({
                      ...form,
                      targetDepartmentId: e.target.value ? Number(e.target.value) : null,
                    })
                  }
                  className="w-full bg-card border border-input rounded-md px-3 py-2 text-sm"
                >
                  <option value="">— Bölüm seçin —</option>
                  {departments?.map((d) => (
                    <option key={d.id} value={d.id}>
                      {d.name}
                    </option>
                  ))}
                </select>
              </div>
            )}
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div>
              <Label htmlFor="startsAt">Başlangıç</Label>
              <Input
                id="startsAt"
                type="datetime-local"
                value={form.startsAt}
                onChange={(e) => setForm({ ...form, startsAt: e.target.value })}
              />
              <p className="text-[11px] text-muted-foreground mt-1">
                Boş bırakılırsa şu andan itibaren yayınlanır.
              </p>
            </div>
            <div>
              <Label htmlFor="expiresAt">Bitiş (Opsiyonel)</Label>
              <Input
                id="expiresAt"
                type="datetime-local"
                value={form.expiresAt}
                onChange={(e) => setForm({ ...form, expiresAt: e.target.value })}
              />
            </div>
          </div>

          <div className="flex items-center gap-3 pt-2">
            <Switch
              id="pinned"
              checked={form.isPinned}
              onCheckedChange={(v) => setForm({ ...form, isPinned: v })}
            />
            <Label htmlFor="pinned" className="cursor-pointer">Üste Sabitle</Label>
          </div>

          {error && (
            <Alert variant="destructive">
              <AlertCircle className="h-4 w-4" />
              <AlertDescription>{error}</AlertDescription>
            </Alert>
          )}
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)} disabled={isPending}>
            İptal
          </Button>
          <Button onClick={handleSubmit} disabled={isPending}>
            {isPending ? 'Kaydediliyor...' : editing ? 'Güncelle' : 'Oluştur'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
