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
import { Info, X } from 'lucide-react';
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

type PermissionBucket = 'view' | 'edit' | 'advanced';

const ADVANCED_ACTIONS = new Set([
  'access-policy.view',
  'access-policy.create',
  'access-policy.delete',
  'access-rule.view',
  'access-rule.create',
  'access-rule.edit',
  'access-rule.delete',
  'activate',
  'apply',
  'archive',
  'bulk-open',
  'close-all',
  'copy',
  'critical-path',
  'cross-list',
  'difficulty',
  'equivalency',
  'export',
  'forecast',
  'generate',
  'import',
  'override',
  'prerequisite',
  'publish',
  'reactivate',
  'recommendation',
  'rollback',
  'simulate',
]);

function parsePermissionKey(key: string) {
  const parts = key.split('.');
  return {
    namespace: parts[0] ?? 'other',
    resource: parts[1] ?? 'unknown',
    action: parts.slice(2).join('.') || 'view',
  };
}

function resourceLabel(resource: string) {
  return resource
    .split('-')
    .map(part => part.charAt(0).toLocaleUpperCase('tr-TR') + part.slice(1))
    .join(' ');
}

function permissionBucket(permission: PermissionDto): PermissionBucket {
  const { action } = parsePermissionKey(permission.key);
  if (action === 'view' || action.endsWith('.view')) return 'view';
  if (ADVANCED_ACTIONS.has(action)) return 'advanced';
  return 'edit';
}

function bucketLabel(bucket: PermissionBucket) {
  if (bucket === 'view') return 'Görüntüle';
  if (bucket === 'edit') return 'Düzenle';
  return 'Gelişmiş İşlemler';
}

function groupPermissionsByResource(permissions: PermissionDto[]) {
  const grouped = new Map<string, PermissionDto[]>();
  for (const permission of permissions) {
    const { resource } = parsePermissionKey(permission.key);
    grouped.set(resource, [...(grouped.get(resource) ?? []), permission]);
  }
  return Array.from(grouped.entries()).sort(([a], [b]) => a.localeCompare(b, 'tr'));
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
  const [expandedAdvanced, setExpandedAdvanced] = useState<Set<string>>(new Set());

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
    setExpandedAdvanced(new Set());
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

  const setPermissionGroup = (perms: PermissionDto[], bucket: PermissionBucket, checked: boolean) => {
    const keys = perms
      .filter(permission => permissionBucket(permission) === bucket)
      .map(permission => permission.key);
    setSelectedKeys(prev => {
      const next = new Set(prev);
      if (checked) keys.forEach(key => next.add(key));
      else keys.forEach(key => next.delete(key));
      return Array.from(next);
    });
  };

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
                {groupPermissionsByResource(filteredPerms).map(([resource, perms]) => (
                  <li key={resource} className="px-3 py-2">
                    <div className="mb-1 flex items-center justify-between gap-2">
                      <div className="min-w-0">
                        <div className="truncate text-xs font-semibold">{resourceLabel(resource)}</div>
                        <div className="text-[10px] text-muted-foreground">
                          {perms.filter(permission => selectedKeys.includes(permission.key)).length}/{perms.length} yetki seçili
                        </div>
                      </div>
                      <Badge variant="outline" className="text-[10px]">
                        {perms[0]?.category}
                      </Badge>
                    </div>
                    <div className="grid grid-cols-3 gap-2">
                      {(['view', 'edit', 'advanced'] as PermissionBucket[]).map(bucket => {
                        const bucketPerms = perms.filter(permission => permissionBucket(permission) === bucket);
                        const checked = bucketPerms.length > 0 && bucketPerms.every(permission => selectedKeys.includes(permission.key));
                        const someChecked = bucketPerms.some(permission => selectedKeys.includes(permission.key)) && !checked;
                        const isAdvanced = bucket === 'advanced';
                        const advancedOpen = expandedAdvanced.has(resource);
                        const title = bucketPerms.length > 0
                          ? bucketPerms.map(permission => `${permission.key}${permission.description ? ` — ${permission.description}` : ''}`).join('\n')
                          : `${bucketLabel(bucket)} yetkisi yok.`;
                        return (
                          <div key={bucket} className="flex min-w-0 items-center gap-1.5">
                            <label
                              className="flex min-w-0 cursor-pointer items-center gap-1.5 text-[11px] text-muted-foreground hover:text-foreground"
                              title={title}
                            >
                            <input
                              type="checkbox"
                              checked={checked}
                              disabled={bucketPerms.length === 0}
                              ref={el => { if (el) el.indeterminate = someChecked; }}
                              onChange={event => setPermissionGroup(perms, bucket, event.target.checked)}
                              className="h-3.5 w-3.5 rounded border-border accent-primary disabled:cursor-not-allowed disabled:opacity-50"
                            />
                              <span className="truncate">{bucketLabel(bucket)}</span>
                            </label>
                            {isAdvanced && bucketPerms.length > 0 && (
                              <button
                                type="button"
                                className="inline-flex h-5 w-5 shrink-0 items-center justify-center rounded-full text-muted-foreground hover:bg-muted hover:text-foreground"
                                title="Gelişmiş işlemleri göster"
                                aria-label={`${resourceLabel(resource)} gelişmiş işlemlerini ${advancedOpen ? 'gizle' : 'göster'}`}
                                onClick={() => {
                                  setExpandedAdvanced(prev => {
                                    const next = new Set(prev);
                                    if (next.has(resource)) next.delete(resource);
                                    else next.add(resource);
                                    return next;
                                  });
                                }}
                              >
                                <Info className="h-3.5 w-3.5" />
                              </button>
                            )}
                          </div>
                        );
                      })}
                    </div>
                    {expandedAdvanced.has(resource) && (
                      <div className="mt-2 rounded-md border bg-muted/30 px-3 py-2">
                        <p className="mb-1 text-[10px] font-semibold uppercase tracking-wide text-muted-foreground">
                          Gelişmiş işlemler
                        </p>
                        <ul className="space-y-1">
                          {perms
                            .filter(permission => permissionBucket(permission) === 'advanced')
                            .map(permission => (
                              <li key={permission.key} className="text-[11px] text-muted-foreground">
                                <span className="font-mono text-foreground">{permission.key}</span>
                                {permission.description && (
                                  <span> — {permission.description}</span>
                                )}
                              </li>
                            ))}
                        </ul>
                      </div>
                    )}
                  </li>
                ))}
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
