import { useEffect, useState } from 'react';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Button } from '@/components/ui/button';
import { Switch } from '@/components/ui/switch';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { ICON_MAP } from '../layout/SidebarModules';
import type { MenuModuleAdminDto } from '@/api/menuAdmin.api';

const ICON_NAMES = Object.keys(ICON_MAP);

interface Props {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  /** null/undefined → create mode, dolu → edit mode. */
  module?: MenuModuleAdminDto | null;
  /** Create mode için parent (null = top-level). Edit mode'da görmezden gelinir. */
  parentId?: number | null;
  onSubmit: (values: {
    slug: string;
    label: string;
    icon: string | null;
    allowedRolesCsv: string | null;
    isActive: boolean;
  }) => Promise<void>;
  isPending: boolean;
  errorMessage?: string | null;
}

export default function MenuModuleFormDialog({
  open,
  onOpenChange,
  module,
  parentId,
  onSubmit,
  isPending,
  errorMessage,
}: Props) {
  const isEdit = Boolean(module);
  // Edit modunda parent bilgisi düzenlenen modülün kendisinden gelir (page edit'te
  // parentId prop'unu null geçer); create modunda prop'tan.
  const effectiveParentId = isEdit ? (module?.parentId ?? null) : (parentId ?? null);
  const [slug, setSlug] = useState('');
  const [label, setLabel] = useState('');
  const [icon, setIcon] = useState<string>('');
  const [allowedRolesCsv, setAllowedRolesCsv] = useState('');
  const [isActive, setIsActive] = useState(true);

  useEffect(() => {
    if (!open) return;
    if (module) {
      setSlug(module.slug);
      setLabel(module.label);
      setIcon(module.icon ?? '__none__');
      setAllowedRolesCsv(module.allowedRolesCsv ?? '');
      setIsActive(module.isActive);
    } else {
      setSlug('');
      setLabel('');
      setIcon('__none__');
      setAllowedRolesCsv('');
      setIsActive(true);
    }
  }, [open, module]);

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    await onSubmit({
      slug,
      label,
      icon: icon === '__none__' ? null : icon || null,
      allowedRolesCsv: allowedRolesCsv.trim() || null,
      isActive,
    });
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>{isEdit ? 'Modülü Düzenle' : 'Yeni Modül'}</DialogTitle>
          <DialogDescription>
            {effectiveParentId == null
              ? 'Üst seviye sidebar başlığı.'
              : 'Bir parent modülün altındaki nested grup.'}
          </DialogDescription>
        </DialogHeader>
        <form onSubmit={submit} className="space-y-4">
          <div className="space-y-2">
            <Label htmlFor="slug">Slug (kalıcı kimlik)</Label>
            <Input
              id="slug"
              value={slug}
              onChange={(e) => setSlug(e.target.value)}
              disabled={isEdit}
              placeholder="ornek-modul"
              required
            />
            <p className="text-xs text-muted-foreground">
              Sadece küçük harf, rakam ve tire. Oluşturulduktan sonra değiştirilemez.
            </p>
          </div>
          <div className="space-y-2">
            <Label htmlFor="label">Etiket (görünen ad)</Label>
            <Input
              id="label"
              value={label}
              onChange={(e) => setLabel(e.target.value)}
              placeholder="Örnek Modül"
              required
            />
          </div>
          <div className="space-y-2">
            <Label htmlFor="icon">İkon</Label>
            <Select value={icon} onValueChange={setIcon}>
              <SelectTrigger id="icon">
                <SelectValue placeholder="(varsayılan)" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="__none__">(yok)</SelectItem>
                {ICON_NAMES.map((n) => (
                  <SelectItem key={n} value={n}>
                    {n}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-2">
            <Label htmlFor="roles">İzinli roller (CSV — boş ise herkes)</Label>
            <Input
              id="roles"
              value={allowedRolesCsv}
              onChange={(e) => setAllowedRolesCsv(e.target.value)}
              placeholder="Student,Instructor"
            />
            <p className="text-xs text-muted-foreground">
              Yalnızca bu rollerden birine sahip kullanıcılar modülü görür. Boşsa rol filtresi
              uygulanmaz.
            </p>
          </div>
          {isEdit && (
            <div className="flex items-center justify-between rounded-md border px-3 py-2">
              <div>
                <Label htmlFor="isActive" className="text-sm">
                  Aktif
                </Label>
                <p className="text-xs text-muted-foreground">
                  Pasif modüller hiçbir kullanıcıya gösterilmez.
                </p>
              </div>
              <Switch id="isActive" checked={isActive} onCheckedChange={setIsActive} />
            </div>
          )}
          {errorMessage && (
            <div className="rounded-md border border-destructive/50 bg-destructive/10 px-3 py-2 text-sm text-destructive">
              {errorMessage}
            </div>
          )}
          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)} disabled={isPending}>
              İptal
            </Button>
            <Button type="submit" disabled={isPending}>
              {isPending ? 'Kaydediliyor...' : isEdit ? 'Güncelle' : 'Oluştur'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
