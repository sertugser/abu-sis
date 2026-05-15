import { useEffect, useMemo, useState } from 'react';
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
import { Badge } from '@/components/ui/badge';
import { ScrollArea } from '@/components/ui/scroll-area';
import { Check, X } from 'lucide-react';
import type { PermissionDto } from '@/api/permissionAdmin.api';
import type { MenuItemAdminDto } from '@/api/menuAdmin.api';

interface Props {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  /** null/undefined → create mode. */
  item?: MenuItemAdminDto | null;
  permissions: PermissionDto[];
  onSubmit: (values: {
    slug: string;
    label: string;
    route: string;
    openInNewTab: boolean;
    isActive: boolean;
    permissionKeys: string[];
  }) => Promise<void>;
  isPending: boolean;
  errorMessage?: string | null;
}

export default function MenuItemFormDialog({
  open,
  onOpenChange,
  item,
  permissions,
  onSubmit,
  isPending,
  errorMessage,
}: Props) {
  const isEdit = Boolean(item);
  const [slug, setSlug] = useState('');
  const [label, setLabel] = useState('');
  const [route, setRoute] = useState('');
  const [openInNewTab, setOpenInNewTab] = useState(false);
  const [isActive, setIsActive] = useState(true);
  const [selectedKeys, setSelectedKeys] = useState<string[]>([]);
  const [permFilter, setPermFilter] = useState('');

  useEffect(() => {
    if (!open) return;
    if (item) {
      setSlug(item.slug);
      setLabel(item.label);
      setRoute(item.route);
      setOpenInNewTab(item.openInNewTab);
      setIsActive(item.isActive);
      setSelectedKeys(item.permissionKeys);
    } else {
      setSlug('');
      setLabel('');
      setRoute('');
      setOpenInNewTab(false);
      setIsActive(true);
      setSelectedKeys([]);
    }
    setPermFilter('');
  }, [open, item]);

  const filteredPerms = useMemo(() => {
    const q = permFilter.trim().toLowerCase();
    if (!q) return permissions;
    return permissions.filter(
      (p) =>
        p.key.toLowerCase().includes(q) ||
        (p.description ?? '').toLowerCase().includes(q),
    );
  }, [permissions, permFilter]);

  const togglePerm = (key: string) =>
    setSelectedKeys((prev) =>
      prev.includes(key) ? prev.filter((k) => k !== key) : [...prev, key],
    );

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    await onSubmit({ slug, label, route, openInNewTab, isActive, permissionKeys: selectedKeys });
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-2xl">
        <DialogHeader>
          <DialogTitle>{isEdit ? 'Sayfayı Düzenle' : 'Yeni Sayfa (Item)'}</DialogTitle>
          <DialogDescription>
            Bu modülün altındaki tıklanabilir bir sidebar item'ı.
          </DialogDescription>
        </DialogHeader>
        <form onSubmit={submit} className="space-y-4">
          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-2">
              <Label htmlFor="item-slug">Slug</Label>
              <Input
                id="item-slug"
                value={slug}
                onChange={(e) => setSlug(e.target.value)}
                disabled={isEdit}
                placeholder="ornek-sayfa"
                required
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="item-label">Etiket</Label>
              <Input
                id="item-label"
                value={label}
                onChange={(e) => setLabel(e.target.value)}
                placeholder="Örnek Sayfa"
                required
              />
            </div>
          </div>
          <div className="space-y-2">
            <Label htmlFor="item-route">Route</Label>
            <Input
              id="item-route"
              value={route}
              onChange={(e) => setRoute(e.target.value)}
              placeholder="/ornek-sayfa"
              required
            />
            <p className="text-xs text-muted-foreground">
              Frontend rotasıyla aynı olmalı. Aynı modülde aynı route iki kez kullanılamaz.
            </p>
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div className="flex items-center justify-between rounded-md border px-3 py-2">
              <Label htmlFor="newtab" className="text-sm">
                Yeni sekmede aç
              </Label>
              <Switch id="newtab" checked={openInNewTab} onCheckedChange={setOpenInNewTab} />
            </div>
            {isEdit && (
              <div className="flex items-center justify-between rounded-md border px-3 py-2">
                <Label htmlFor="item-active" className="text-sm">
                  Aktif
                </Label>
                <Switch id="item-active" checked={isActive} onCheckedChange={setIsActive} />
              </div>
            )}
          </div>

          <div className="space-y-2">
            <Label>Yetkiler (item için OR mantığı — biri yeterli)</Label>
            <Input
              placeholder="Yetki ara..."
              value={permFilter}
              onChange={(e) => setPermFilter(e.target.value)}
            />
            {selectedKeys.length > 0 && (
              <div className="flex flex-wrap gap-1 rounded-md border bg-muted/30 p-2">
                {selectedKeys.map((k) => (
                  <Badge key={k} variant="secondary" className="gap-1">
                    {k}
                    <button
                      type="button"
                      onClick={() => togglePerm(k)}
                      className="ml-1 inline-flex h-3 w-3 items-center justify-center rounded-full hover:bg-destructive/20"
                      aria-label={`${k} kaldır`}
                    >
                      <X className="h-3 w-3" />
                    </button>
                  </Badge>
                ))}
              </div>
            )}
            <ScrollArea className="h-48 rounded-md border">
              <ul className="divide-y">
                {filteredPerms.map((p) => {
                  const checked = selectedKeys.includes(p.key);
                  return (
                    <li
                      key={p.id}
                      className="flex cursor-pointer items-start gap-2 px-3 py-1.5 hover:bg-muted/50"
                      onClick={() => togglePerm(p.key)}
                    >
                      <span
                        className={`mt-0.5 inline-flex h-4 w-4 shrink-0 items-center justify-center rounded border ${
                          checked ? 'border-primary bg-primary text-primary-foreground' : 'border-input'
                        }`}
                      >
                        {checked && <Check className="h-3 w-3" />}
                      </span>
                      <div className="min-w-0 flex-1 text-xs">
                        <div className="font-mono">{p.key}</div>
                        {p.description && (
                          <div className="truncate text-muted-foreground">{p.description}</div>
                        )}
                      </div>
                      <Badge variant="outline" className="text-[10px]">
                        {p.category}
                      </Badge>
                    </li>
                  );
                })}
                {filteredPerms.length === 0 && (
                  <li className="px-3 py-4 text-center text-xs text-muted-foreground">
                    Eşleşen yetki yok.
                  </li>
                )}
              </ul>
            </ScrollArea>
          </div>

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
