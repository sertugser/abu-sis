import { useMemo, useState } from 'react';
import { Dialog, DialogContent, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Badge } from '@/components/ui/badge';
import {
  Collapsible,
  CollapsibleContent,
  CollapsibleTrigger,
} from '@/components/ui/collapsible';
import { ChevronDown, ChevronRight, Info, Save, Loader2 } from 'lucide-react';
import { useAllPermissions } from '../../hooks/useRolePermissions';
import { useCreateBundle, useUpdateBundle, usePermissionBundle } from '../../hooks/usePermissionBundles';
import type { PermissionDto } from '../../api/permissionAdmin.api';

interface Props {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  bundleId: number | null;
}

const CATEGORY_LABELS: Record<string, string> = {
  menu: 'Menü Yetkileri',
  system: 'Sistem Yetkileri',
};

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

function parseKey(key: string) {
  const parts = key.split('.');
  return {
    category: parts[0] ?? 'other',
    resource: parts[1] ?? 'unknown',
    action: parts.slice(2).join('.') || 'view',
  };
}

function actionLabel(action: string) {
  if (isViewAction(action)) return 'Görüntüle';
  if (ADVANCED_ACTIONS.has(action)) return 'Gelişmiş İşlemler';
  return 'Düzenle';
}

function resourceLabel(resource: string) {
  return resource
    .split('-')
    .map(w => w.charAt(0).toLocaleUpperCase('tr-TR') + w.slice(1))
    .join(' ');
}

type Grouped = Record<string, Record<string, PermissionDto[]>>;

function isViewAction(action: string) {
  return action === 'view' || action.endsWith('.view');
}

function permissionBucket(permission: PermissionDto): PermissionBucket {
  const { action } = parseKey(permission.key);
  if (isViewAction(action)) return 'view';
  if (ADVANCED_ACTIONS.has(action)) return 'advanced';
  return 'edit';
}

function bucketLabel(bucket: PermissionBucket) {
  if (bucket === 'view') return 'Görüntüle';
  if (bucket === 'edit') return 'Düzenle';
  return 'Gelişmiş İşlemler';
}

function splitResourcePermissions(perms: PermissionDto[]) {
  const view = perms.filter(permission => isViewAction(parseKey(permission.key).action));
  const edit = perms.filter(permission => permissionBucket(permission) === 'edit');
  const advanced = perms.filter(permission => permissionBucket(permission) === 'advanced');
  return { view, edit, advanced };
}

function groupPerms(perms: PermissionDto[]): Grouped {
  const out: Grouped = {};
  for (const p of perms) {
    const { category, resource } = parseKey(p.key);
    if (!out[category]) out[category] = {};
    if (!out[category][resource]) out[category][resource] = [];
    out[category][resource].push(p);
  }
  return out;
}

export default function PermissionBundleFormDialog({ open, onOpenChange, bundleId }: Props) {
  const isEdit = bundleId !== null;
  const { data: permissions = [] } = useAllPermissions();
  const { data: existing, isLoading: loadingExisting } = usePermissionBundle(open ? bundleId : null);
  const createMut = useCreateBundle();
  const updateMut = useUpdateBundle();

  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [category, setCategory] = useState('');
  const [permissionSearch, setPermissionSearch] = useState('');
  const [selectedIds, setSelectedIds] = useState<Set<number>>(new Set());
  const [error, setError] = useState<string | null>(null);

  // React 19 — adjust state during render pattern (useEffect setState yasak)
  // Sync local form state when the dialog opens or the loaded bundle changes.
  const [prevOpen, setPrevOpen] = useState(false);
  const [prevExistingId, setPrevExistingId] = useState<number | null>(null);
  const existingId = existing?.id ?? null;
  if (open !== prevOpen || existingId !== prevExistingId) {
    setPrevOpen(open);
    setPrevExistingId(existingId);
    if (open) {
      if (isEdit && existing) {
        setName(existing.name);
        setDescription(existing.description ?? '');
        setCategory(existing.category ?? '');
        setSelectedIds(new Set(existing.permissionIds));
      } else if (!isEdit) {
        setName('');
        setDescription('');
        setCategory('');
        setPermissionSearch('');
        setSelectedIds(new Set());
      }
      setError(null);
    }
  }

  const filteredPermissions = useMemo(() => {
    const needle = permissionSearch.trim().toLocaleLowerCase('tr-TR');
    if (!needle) return permissions;

    return permissions.filter(permission => {
      const { resource, action } = parseKey(permission.key);
      return [
        permission.key,
        permission.description ?? '',
        resourceLabel(resource),
        actionLabel(action),
      ].some(value => value.toLocaleLowerCase('tr-TR').includes(needle));
    });
  }, [permissionSearch, permissions]);

  const grouped = useMemo(() => groupPerms(filteredPermissions), [filteredPermissions]);

  function toggleResource(perms: PermissionDto[]) {
    const allChecked = perms.every(p => selectedIds.has(p.id));
    setSelectedIds(prev => {
      const next = new Set(prev);
      if (allChecked) perms.forEach(p => next.delete(p.id));
      else perms.forEach(p => next.add(p.id));
      return next;
    });
  }

  function setResourceBucket(perms: PermissionDto[], bucket: PermissionBucket, checked: boolean) {
    const { view, edit, advanced } = splitResourcePermissions(perms);
    setSelectedIds(prev => {
      const next = new Set(prev);
      if (bucket === 'view' && checked) {
        view.forEach(p => next.add(p.id));
      } else if (bucket === 'view') {
        view.forEach(p => next.delete(p.id));
        edit.forEach(p => next.delete(p.id));
        advanced.forEach(p => next.delete(p.id));
      } else if (bucket === 'edit' && checked) {
        view.forEach(p => next.add(p.id));
        edit.forEach(p => next.add(p.id));
      } else if (bucket === 'edit') {
        edit.forEach(p => next.delete(p.id));
      } else if (bucket === 'advanced' && checked) {
        view.forEach(p => next.add(p.id));
        advanced.forEach(p => next.add(p.id));
      } else {
        advanced.forEach(p => next.delete(p.id));
      }
      return next;
    });
  }

  async function handleSave() {
    setError(null);
    const trimmed = name.trim();
    if (!trimmed) {
      setError('Paket adı zorunlu.');
      return;
    }

    const payload = {
      name: trimmed,
      description: description.trim() || null,
      category: category.trim() || null,
      permissionIds: Array.from(selectedIds),
    };

    try {
      if (isEdit && bundleId !== null) {
        await updateMut.mutateAsync({ id: bundleId, payload });
      } else {
        await createMut.mutateAsync(payload);
      }
      onOpenChange(false);
    } catch (e: unknown) {
      const msg = (e as { response?: { data?: { error?: string } } }).response?.data?.error;
      setError(msg ?? 'Kayıt sırasında bir hata oluştu.');
    }
  }

  const isPending = createMut.isPending || updateMut.isPending;
  const isSystemDefault = isEdit && existing?.isSystemDefault === true;

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="top-[5vh] translate-y-0 max-w-3xl max-h-[90vh] min-h-0 flex flex-col overflow-y-auto">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            {isEdit ? 'Paketi Düzenle' : 'Yeni Yetki Paketi'}
            {isSystemDefault && (
              <Badge variant="secondary" className="text-[10px]">Sistem paketi</Badge>
            )}
          </DialogTitle>
        </DialogHeader>

        {isEdit && loadingExisting ? (
          <div className="flex items-center justify-center py-12">
            <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
          </div>
        ) : (
          <>
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              <div>
                <Label htmlFor="bundle-name" className="text-xs">Paket Adı</Label>
                <Input
                  id="bundle-name"
                  value={name}
                  onChange={e => setName(e.target.value)}
                  placeholder="Örn. Akademik İşlemler — Görüntüleme"
                  maxLength={200}
                />
              </div>
              <div>
                <Label htmlFor="bundle-category" className="text-xs">Kategori (opsiyonel)</Label>
                <Input
                  id="bundle-category"
                  value={category}
                  onChange={e => setCategory(e.target.value)}
                  placeholder="Örn. Akademik İşlemler"
                  maxLength={100}
                />
              </div>
              <div className="md:col-span-2">
                <Label htmlFor="bundle-description" className="text-xs">Açıklama (opsiyonel)</Label>
                <Textarea
                  id="bundle-description"
                  value={description}
                  onChange={e => setDescription(e.target.value)}
                  rows={2}
                  maxLength={1000}
                />
              </div>
            </div>

            <div className="flex items-center justify-between mt-2 mb-1">
              <Label className="text-xs">Yetkiler</Label>
              <Badge variant="outline" className="text-[10px]">
                {selectedIds.size} / {permissions.length} seçili
              </Badge>
            </div>
            <Input
              value={permissionSearch}
              onChange={e => setPermissionSearch(e.target.value)}
              placeholder="Yetki ara: duyuru, akademik takvim, görüntüle..."
              className="mb-3"
            />
            <div className="border rounded-md p-3">
              <div className="space-y-3">
                {Object.entries(grouped)
                  .sort(([a], [b]) => a.localeCompare(b))
                  .map(([cat, resources]) => (
                    <CategoryGroup
                      key={cat}
                      category={cat}
                      resources={resources}
                      selectedIds={selectedIds}
                      onToggleResource={toggleResource}
                      onSetResourceBucket={setResourceBucket}
                    />
                  ))}
                {filteredPermissions.length === 0 && (
                  <p className="text-sm text-muted-foreground">
                    Aramaya uyan yetki bulunamadı.
                  </p>
                )}
              </div>
            </div>

            {error && <p className="text-sm text-destructive">{error}</p>}

            <div className="flex justify-end gap-2 pt-2">
              <Button variant="outline" onClick={() => onOpenChange(false)} disabled={isPending}>
                Vazgeç
              </Button>
              <Button onClick={handleSave} disabled={isPending} className="gap-1.5">
                {isPending ? <Loader2 className="h-4 w-4 animate-spin" /> : <Save className="h-4 w-4" />}
                {isPending ? 'Kaydediliyor…' : 'Kaydet'}
              </Button>
            </div>
          </>
        )}
      </DialogContent>
    </Dialog>
  );
}

function CategoryGroup({
  category,
  resources,
  selectedIds,
  onToggleResource,
  onSetResourceBucket,
}: {
  category: string;
  resources: Record<string, PermissionDto[]>;
  selectedIds: Set<number>;
  onToggleResource: (perms: PermissionDto[]) => void;
  onSetResourceBucket: (perms: PermissionDto[], bucket: PermissionBucket, checked: boolean) => void;
}) {
  const [open, setOpen] = useState(true);
  const [expandedAdvanced, setExpandedAdvanced] = useState<Set<string>>(new Set());
  const all = Object.values(resources).flat();
  const checked = all.filter(p => selectedIds.has(p.id)).length;
  const label = CATEGORY_LABELS[category] ?? category;

  return (
    <Collapsible open={open} onOpenChange={setOpen}>
      <CollapsibleTrigger asChild>
        <button
          type="button"
          className="flex items-center gap-2 w-full p-1.5 rounded hover:bg-muted text-left"
        >
          {open ? <ChevronDown className="h-4 w-4" /> : <ChevronRight className="h-4 w-4" />}
          <span className="font-semibold text-sm">{label}</span>
          <Badge variant="secondary" className="text-[10px] ml-auto">
            {checked} / {all.length}
          </Badge>
        </button>
      </CollapsibleTrigger>
      <CollapsibleContent className="pl-5 mt-1 space-y-1.5">
        {Object.entries(resources)
          .sort(([a], [b]) => a.localeCompare(b, 'tr'))
          .map(([resource, perms]) => {
            const checkedCount = perms.filter(p => selectedIds.has(p.id)).length;
            const allChecked = checkedCount === perms.length;
            const someChecked = checkedCount > 0 && checkedCount < perms.length;
            const { advanced } = splitResourcePermissions(perms);
            return (
              <div key={resource} className="border-l-2 border-border/50 pl-2">
                <div className="flex items-center gap-2 mb-0.5">
                  <input
                    type="checkbox"
                    checked={allChecked}
                    ref={el => { if (el) el.indeterminate = someChecked; }}
                    onChange={() => onToggleResource(perms)}
                    className="h-4 w-4 rounded border-border accent-primary cursor-pointer"
                  />
                  <span className="text-xs font-medium">{resourceLabel(resource)}</span>
                  <span className="text-[10px] text-muted-foreground">
                    ({checkedCount}/{perms.length})
                  </span>
                </div>
                <div className="ml-6 grid grid-cols-3 gap-1">
                  {(['view', 'edit', 'advanced'] as PermissionBucket[]).map(bucket => {
                    const bucketPerms = perms.filter(permission => permissionBucket(permission) === bucket);
                    const bucketChecked = bucketPerms.length > 0 && bucketPerms.every(p => selectedIds.has(p.id));
                    const bucketSomeChecked = bucketPerms.some(p => selectedIds.has(p.id)) && !bucketChecked;
                    const title = bucketPerms.length > 0
                      ? bucketPerms.map(permission => permission.description ?? permission.key).join('\n')
                      : `Bu başlık için ${bucketLabel(bucket).toLocaleLowerCase('tr-TR')} yetkisi yok.`;
                    const advancedOpen = expandedAdvanced.has(resource);
                    return (
                      <div key={bucket} className="flex min-w-0 items-center gap-1.5">
                        <label
                          className="flex min-w-0 items-center gap-1.5 text-[11px] cursor-pointer text-muted-foreground hover:text-foreground"
                          title={title}
                        >
                          <input
                            type="checkbox"
                            checked={bucketChecked}
                            disabled={bucketPerms.length === 0}
                            ref={el => { if (el) el.indeterminate = bucketSomeChecked; }}
                            onChange={event => onSetResourceBucket(perms, bucket, event.target.checked)}
                            className="h-3.5 w-3.5 rounded border-border accent-primary cursor-pointer disabled:cursor-not-allowed disabled:opacity-50"
                          />
                          <span className="truncate">{bucketLabel(bucket)}</span>
                        </label>
                        {bucket === 'advanced' && bucketPerms.length > 0 && (
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
                  <div className="ml-6 mt-2 rounded-md border bg-muted/30 px-3 py-2">
                    <p className="mb-1 text-[10px] font-semibold uppercase tracking-wide text-muted-foreground">
                      Gelişmiş işlemler
                    </p>
                    <ul className="space-y-1">
                      {advanced.map(permission => (
                        <li key={permission.key} className="text-[11px] text-muted-foreground">
                          <span className="font-mono text-foreground">{permission.key}</span>
                          {permission.description && <span> — {permission.description}</span>}
                        </li>
                      ))}
                    </ul>
                  </div>
                )}
              </div>
            );
          })}
      </CollapsibleContent>
    </Collapsible>
  );
}
