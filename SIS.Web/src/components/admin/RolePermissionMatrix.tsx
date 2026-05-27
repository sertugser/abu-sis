import { useMemo, useState } from 'react';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { ScrollArea } from '@/components/ui/scroll-area';
import { Skeleton } from '@/components/ui/skeleton';
import {
  Collapsible,
  CollapsibleContent,
  CollapsibleTrigger,
} from '@/components/ui/collapsible';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { ChevronDown, ChevronRight, Info, Save, RotateCcw, Package, Lock, Trash2, RefreshCw, AlertTriangle, Users } from 'lucide-react';
import { cn } from '@/lib/utils';
import { getRoleDisplayLabel } from '../../lib/roles';
import {
  useAllPermissions,
  useAssignableRoles,
  useRolePermissions,
  useUpdateRolePermissions,
} from '../../hooks/useRolePermissions';
import {
  useApplyBundleToRole,
  useAppliedBundles,
  usePermissionBundles,
  useRemoveBundleFromRole,
} from '../../hooks/usePermissionBundles';
import { usePermissions } from '../../context/PermissionContext';
import type { PermissionDto } from '../../api/permissionAdmin.api';

/**
 * Reusable Admin UI komponent: rol bazında permission atama matrisi.
 *
 * Karar 9 (v2): Phase 1'de production sidebar'a/route'a EKLENMEZ. Sadece dosya olarak hazırdır.
 * Phase 2'de "Kullanıcı Yetki Grupları" placeholder içine import edilecek.
 *
 * Karar 4 (v2): SystemAdministrator rol listesinde GÖZÜKMEZ (locked, kod-shortcut).
 *
 * Karar 7 (v2): lookup.* permission'ları DB'ye seed edilmediği için backend zaten
 * göndermez — burada ek filter gerekmez.
 *
 * Davranış:
 *   - Sol: rol listesi (radio)
 *   - Sağ: kategori → resource → action checkbox tree
 *   - Optimistic UI: değişiklikler local state'te
 *   - Diff preview: "+3 / -1"
 *   - Save: useUpdateRolePermissions mutation tetikler, success'te draft = original
 */

function resourceLabel(resource: string): string {
  // "akademik-donem" → "Akademik Donem"
  return resource
    .split('-')
    .map(w => w.charAt(0).toLocaleUpperCase('tr-TR') + w.slice(1))
    .join(' ');
}

interface ParsedKey {
  category: string;
  resource: string;
  action: string;
}

function parseKey(key: string): ParsedKey {
  const parts = key.split('.');
  return {
    category: parts[0] ?? 'other',
    resource: parts[1] ?? 'unknown',
    action: parts.slice(2).join('.') || 'view',
  };
}

interface GroupedPermissions {
  [category: string]: {
    [resource: string]: PermissionDto[];
  };
}

function groupByCategoryAndResource(perms: PermissionDto[]): GroupedPermissions {
  const result: GroupedPermissions = {};
  for (const p of perms) {
    const { category, resource } = parseKey(p.key);
    if (!result[category]) result[category] = {};
    if (!result[category][resource]) result[category][resource] = [];
    result[category][resource].push(p);
  }
  return result;
}

const CATEGORY_LABELS: Record<string, string> = {
  menu: 'Menü Yetkileri',
  system: 'Sistem Yetkileri',
};

const EMPTY_NUMBER_ARRAY: readonly number[] = [];

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
  return {
    view: perms.filter(permission => permissionBucket(permission) === 'view'),
    edit: perms.filter(permission => permissionBucket(permission) === 'edit'),
    advanced: perms.filter(permission => permissionBucket(permission) === 'advanced'),
  };
}

export function RolePermissionMatrix() {
  const { data: permissionsData, isLoading: permsLoading } = useAllPermissions();
  const { data: rolesData, isLoading: rolesLoading } = useAssignableRoles();
  const [selectedRoleId, setSelectedRoleId] = useState<number | null>(null);
  const { data: roleStateData, isLoading: rolePermsLoading } = useRolePermissions(selectedRoleId);
  const updateMutation = useUpdateRolePermissions();
  const { data: bundlesData } = usePermissionBundles();
  const { data: appliedBundlesData } = useAppliedBundles(selectedRoleId);
  const applyBundleMut = useApplyBundleToRole();
  const removeBundleMut = useRemoveBundleFromRole();
  const { hasPermission } = usePermissions();
  const canApplyBundle = hasPermission('system.permission-bundle.apply');

  // React Query default destructure (`data = []`) her render'da YENİ array ref üretiyor;
  // aşağıdaki adjust-state-during-render karşılaştırması sürekli setState tetikliyor
  // ("Too many re-renders" döngüsü). Stabil referans için useMemo ile sabitle.
  const permissions = useMemo(() => permissionsData ?? [], [permissionsData]);
  const roles = useMemo(() => rolesData ?? [], [rolesData]);
  const bundles = useMemo(() => bundlesData ?? [], [bundlesData]);
  const appliedBundles = useMemo(() => appliedBundlesData ?? [], [appliedBundlesData]);

  // Manuel (düzenlenebilir) ve paketten gelen (kilitli) permission ID'leri ayrı.
  const manualIds = useMemo<readonly number[]>(
    () => roleStateData?.manualPermissionIds ?? EMPTY_NUMBER_ARRAY,
    [roleStateData],
  );
  const bundleSet = useMemo<Set<number>>(
    () => new Set(roleStateData?.bundlePermissionIds ?? []),
    [roleStateData],
  );

  // Local draft (optimistic) — Set<permissionId>, SADECE manuel yetkiler.
  const [draft, setDraft] = useState<Set<number>>(new Set());

  // Server'dan rol state gelince draft'ı (manuel set) senkronize et — adjust-state-during-render
  // pattern (React 19+: useEffect içinde setState yasaklandı, yerine prev-tracking ile render'da update).
  const [prevManualIds, setPrevManualIds] = useState(manualIds);
  const [prevSelectedRoleId, setPrevSelectedRoleId] = useState(selectedRoleId);
  if (prevManualIds !== manualIds || prevSelectedRoleId !== selectedRoleId) {
    setPrevManualIds(manualIds);
    setPrevSelectedRoleId(selectedRoleId);
    setDraft(new Set(manualIds));
  }

  const grouped = useMemo(() => groupByCategoryAndResource(permissions), [permissions]);
  const original = useMemo(() => new Set(manualIds), [manualIds]);

  const diff = useMemo(() => {
    const added: number[] = [];
    const removed: number[] = [];
    draft.forEach(id => { if (!original.has(id)) added.push(id); });
    original.forEach(id => { if (!draft.has(id)) removed.push(id); });
    return { added, removed, isDirty: added.length > 0 || removed.length > 0 };
  }, [draft, original]);

  // Paketten gelen yetkiler kilitlidir; matris toggle'ları sadece manuel-edilebilir
  // (bundleSet'te olmayan) yetkilere dokunur.
  function toggleResourceAll(resourcePerms: PermissionDto[]) {
    const editable = resourcePerms.filter(p => !bundleSet.has(p.id));
    if (editable.length === 0) return;
    const allChecked = editable.every(p => draft.has(p.id));
    setDraft(prev => {
      const next = new Set(prev);
      if (allChecked) {
        editable.forEach(p => next.delete(p.id));
      } else {
        editable.forEach(p => next.add(p.id));
      }
      return next;
    });
  }

  function setResourceBucket(resourcePerms: PermissionDto[], bucket: PermissionBucket, checked: boolean) {
    const editablePerms = resourcePerms.filter(p => !bundleSet.has(p.id));
    const { view, edit, advanced } = splitResourcePermissions(editablePerms);
    setDraft(prev => {
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

  function reset() {
    setDraft(new Set(manualIds));
  }

  function save() {
    if (selectedRoleId === null) return;
    updateMutation.mutate({ roleId: selectedRoleId, permissionIds: Array.from(draft) });
  }

  function applyBundle(bundleIdStr: string) {
    if (selectedRoleId === null) return;
    const bundleId = Number(bundleIdStr);
    if (!Number.isFinite(bundleId)) return;
    applyBundleMut.mutate({ bundleId, roleId: selectedRoleId });
  }

  function reapplyBundle(bundleId: number) {
    if (selectedRoleId === null) return;
    applyBundleMut.mutate({ bundleId, roleId: selectedRoleId });
  }

  function removeBundle(bundleId: number) {
    if (selectedRoleId === null) return;
    removeBundleMut.mutate({ bundleId, roleId: selectedRoleId });
  }

  const bundleNameById = useMemo(() => {
    const map = new Map<number, string>();
    bundles.forEach(b => map.set(b.id, b.name));
    return map;
  }, [bundles]);

  if (permsLoading || rolesLoading) {
    return <Skeleton className="h-96 w-full" />;
  }

  return (
    <div className="grid h-full min-h-0 grid-cols-1 lg:grid-cols-[300px_1fr]">
      {/* Sol: Rol listesi */}
      <div className="flex min-h-0 flex-col overflow-hidden border-b border-border lg:border-b-0 lg:border-r">
        <div className="border-b border-border px-4 py-3">
          <span className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">Rol Seç</span>
        </div>
        <div className="min-h-0 flex-1 space-y-1 overflow-auto p-2">
          {roles.map(role => {
            const active = selectedRoleId === role.id;
            return (
              <button
                key={role.id}
                type="button"
                onClick={() => setSelectedRoleId(role.id)}
                className={cn(
                  'w-full rounded-lg px-3 py-2.5 text-left text-sm transition-colors',
                  active
                    ? 'bg-primary text-primary-foreground shadow-sm'
                    : 'hover:bg-muted',
                )}
              >
                <div className="font-medium">{getRoleDisplayLabel(role.name)}</div>
                <div className={cn('text-[11px]', active ? 'text-primary-foreground/70' : 'text-muted-foreground')}>
                  {role.name}
                </div>
              </button>
            );
          })}
        </div>
      </div>

      {/* Sağ: Permission matrisi */}
      <div className="flex min-h-0 flex-col overflow-hidden">
        <div className="flex flex-row items-center justify-between gap-3 border-b border-border px-4 py-2.5">
          <div className="min-w-0">
            <span className="text-base font-semibold">Yetki Matrisi</span>
            {selectedRoleId !== null && (
              <p className="mt-0.5 text-xs text-muted-foreground">
                {permissions.length} yetki · {original.size} manuel · {bundleSet.size} paketten
              </p>
            )}
          </div>
          <div className="flex items-center gap-3">
            {canApplyBundle && selectedRoleId !== null && bundles.length > 0 && (
              <Select
                value=""
                onValueChange={applyBundle}
                disabled={applyBundleMut.isPending || rolePermsLoading}
              >
                <SelectTrigger className="h-9 w-[240px] text-xs">
                  <Package className="mr-1.5 h-3.5 w-3.5 text-muted-foreground" />
                  <SelectValue placeholder={applyBundleMut.isPending ? 'Uygulanıyor…' : 'Paket Uygula'} />
                </SelectTrigger>
                <SelectContent>
                  {bundles.map(b => (
                    <SelectItem key={b.id} value={String(b.id)}>
                      <div className="flex items-center gap-2">
                        <span>{b.name}</span>
                        <Badge variant="outline" className="text-[10px] tabular-nums">
                          {b.permissionCount}
                        </Badge>
                      </div>
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            )}
            {diff.isDirty && (
              <Badge variant="outline" className="text-xs">
                <span className="font-medium text-emerald-600">+{diff.added.length}</span>
                <span className="mx-1 text-muted-foreground">·</span>
                <span className="font-medium text-destructive">−{diff.removed.length}</span>
              </Badge>
            )}
            <Button
              size="sm"
              variant="outline"
              onClick={reset}
              disabled={!diff.isDirty || rolePermsLoading}
              className="gap-1"
            >
              <RotateCcw className="h-3.5 w-3.5" />
              Sıfırla
            </Button>
            <Button
              size="sm"
              onClick={save}
              disabled={!diff.isDirty || updateMutation.isPending || selectedRoleId === null}
              className="gap-1"
            >
              <Save className="h-3.5 w-3.5" />
              {updateMutation.isPending ? 'Kaydediliyor…' : 'Kaydet'}
            </Button>
          </div>
        </div>
        <div className="flex min-h-0 flex-1 flex-col p-4">
          {selectedRoleId === null ? (
            <div className="flex flex-1 flex-col items-center justify-center text-center text-muted-foreground">
              <Users className="mb-3 h-10 w-10 text-muted-foreground/30" />
              <p className="text-sm font-medium text-foreground">Bir rol seçin</p>
              <p className="mt-1 text-xs">Soldaki listeden bir rol seçerek yetkilerini düzenleyin.</p>
            </div>
          ) : rolePermsLoading ? (
            <Skeleton className="h-full min-h-64 w-full" />
          ) : (
            <>
              {appliedBundles.length > 0 && (
                <div className="mb-4 rounded-lg border border-border bg-muted/30 p-3">
                  <p className="mb-2 flex items-center gap-1.5 text-xs font-semibold text-muted-foreground">
                    <Package className="h-3.5 w-3.5" />
                    Uygulanmış Paketler
                  </p>
                  <div className="flex flex-wrap gap-2">
                    {appliedBundles.map(ab => {
                      const name = bundleNameById.get(ab.bundleId) ?? `#${ab.bundleId}`;
                      const busy = applyBundleMut.isPending || removeBundleMut.isPending;
                      return (
                        <div
                          key={ab.bundleId}
                          className={cn(
                            'flex items-center gap-1.5 rounded-md border px-2 py-1 text-xs',
                            ab.isStale
                              ? 'border-amber-300 bg-amber-50 dark:border-amber-700/60 dark:bg-amber-950/30'
                              : 'border-border bg-background',
                          )}
                        >
                          <span className="font-medium">{name}</span>
                          {ab.isStale && (
                            <span
                              className="flex items-center gap-1 text-amber-700 dark:text-amber-400"
                              title="Paket içeriği güncellendi; role uygulanmış hali eski. Yeniden uygulayın."
                            >
                              <AlertTriangle className="h-3 w-3" />
                              güncel değil
                            </span>
                          )}
                          {canApplyBundle && ab.isStale && (
                            <button
                              type="button"
                              className="inline-flex h-5 w-5 items-center justify-center rounded text-amber-700 hover:bg-amber-100 disabled:opacity-50 dark:text-amber-400 dark:hover:bg-amber-900/40"
                              title="Yeniden uygula"
                              disabled={busy}
                              onClick={() => reapplyBundle(ab.bundleId)}
                            >
                              <RefreshCw className="h-3.5 w-3.5" />
                            </button>
                          )}
                          {canApplyBundle && (
                            <button
                              type="button"
                              className="inline-flex h-5 w-5 items-center justify-center rounded text-muted-foreground hover:bg-destructive/10 hover:text-destructive disabled:opacity-50"
                              title="Paketi rolden kaldır"
                              disabled={busy}
                              onClick={() => removeBundle(ab.bundleId)}
                            >
                              <Trash2 className="h-3.5 w-3.5" />
                            </button>
                          )}
                        </div>
                      );
                    })}
                  </div>
                </div>
              )}
              <ScrollArea className="min-h-0 flex-1 pr-4">
                <div className="space-y-4">
                  {Object.entries(grouped).map(([category, resources]) => (
                    <CategoryGroup
                      key={category}
                      category={category}
                      resources={resources}
                      draft={draft}
                      bundleSet={bundleSet}
                      onToggleResourceAll={toggleResourceAll}
                      onSetResourceBucket={setResourceBucket}
                    />
                  ))}
                </div>
              </ScrollArea>
            </>
          )}
        </div>
      </div>
    </div>
  );
}

interface CategoryGroupProps {
  category: string;
  resources: Record<string, PermissionDto[]>;
  draft: Set<number>;
  bundleSet: Set<number>;
  onToggleResourceAll: (perms: PermissionDto[]) => void;
  onSetResourceBucket: (perms: PermissionDto[], bucket: PermissionBucket, checked: boolean) => void;
}

function CategoryGroup({
  category,
  resources,
  draft,
  bundleSet,
  onToggleResourceAll,
  onSetResourceBucket,
}: CategoryGroupProps) {
  const [open, setOpen] = useState(true);
  const label = CATEGORY_LABELS[category] ?? category;
  const allPerms = Object.values(resources).flat();
  // Etkin sayım: manuel (draft) ∪ paketten (bundleSet)
  const checkedCount = allPerms.filter(p => draft.has(p.id) || bundleSet.has(p.id)).length;

  return (
    <Collapsible open={open} onOpenChange={setOpen}>
      <CollapsibleTrigger asChild>
        <button
          type="button"
          className="flex items-center gap-2 w-full p-2 rounded hover:bg-muted text-left"
        >
          {open ? <ChevronDown className="h-4 w-4" /> : <ChevronRight className="h-4 w-4" />}
          <span className="font-semibold">{label}</span>
          <Badge variant="secondary" className="text-[10px] ml-auto">
            {checkedCount} / {allPerms.length}
          </Badge>
        </button>
      </CollapsibleTrigger>
      <CollapsibleContent className="pl-6 mt-2 space-y-2">
        {Object.entries(resources)
          .sort(([a], [b]) => a.localeCompare(b, 'tr'))
          .map(([resource, perms]) => (
            <ResourceGroup
              key={resource}
              resource={resource}
              perms={perms}
              draft={draft}
              bundleSet={bundleSet}
              onToggleResourceAll={onToggleResourceAll}
              onSetResourceBucket={onSetResourceBucket}
            />
          ))}
      </CollapsibleContent>
    </Collapsible>
  );
}

interface ResourceGroupProps {
  resource: string;
  perms: PermissionDto[];
  draft: Set<number>;
  bundleSet: Set<number>;
  onToggleResourceAll: (perms: PermissionDto[]) => void;
  onSetResourceBucket: (perms: PermissionDto[], bucket: PermissionBucket, checked: boolean) => void;
}

function ResourceGroup({
  resource,
  perms,
  draft,
  bundleSet,
  onToggleResourceAll,
  onSetResourceBucket,
}: ResourceGroupProps) {
  const [advancedOpen, setAdvancedOpen] = useState(false);
  const isGranted = (id: number) => draft.has(id) || bundleSet.has(id);
  const editablePerms = perms.filter(p => !bundleSet.has(p.id));
  const grantedCount = perms.filter(p => isGranted(p.id)).length;
  const allChecked = grantedCount === perms.length && perms.length > 0;
  const someChecked = grantedCount > 0 && grantedCount < perms.length;
  // Tüm yetkiler paketten geliyorsa resource-level toggle kilitli.
  const resourceLocked = editablePerms.length === 0;
  const { advanced } = splitResourcePermissions(perms);

  return (
    <div className="border-l-2 border-border/50 pl-3">
      <div className="flex items-center gap-2 mb-1">
        <input
          type="checkbox"
          checked={allChecked}
          disabled={resourceLocked}
          ref={el => { if (el) el.indeterminate = someChecked; }}
          onChange={() => onToggleResourceAll(perms)}
          className="h-4 w-4 rounded border-border accent-primary cursor-pointer disabled:cursor-not-allowed disabled:opacity-50"
        />
        <span className="text-sm font-medium">{resourceLabel(resource)}</span>
        <span className="text-[10px] text-muted-foreground">
          ({grantedCount}/{perms.length})
        </span>
        {resourceLocked && (
          <Lock className="h-3 w-3 text-muted-foreground" aria-label="Paketten geliyor (kilitli)" />
        )}
      </div>
      <div className="ml-6 grid grid-cols-3 gap-1">
        {(['view', 'edit', 'advanced'] as PermissionBucket[]).map(bucket => {
          const bucketPerms = perms.filter(permission => permissionBucket(permission) === bucket);
          const editableInBucket = bucketPerms.filter(permission => !bundleSet.has(permission.id));
          const checked = bucketPerms.length > 0 && bucketPerms.every(permission => isGranted(permission.id));
          const someCheckedInBucket = bucketPerms.some(permission => isGranted(permission.id)) && !checked;
          // Bucket'taki tüm yetkiler paketten geliyorsa (veya yoksa) checkbox kilitli.
          const bucketLocked = editableInBucket.length === 0;
          const lockedFromBundle = bucketPerms.length > 0 && bucketLocked;
          const title = bucketPerms.length > 0
            ? bucketPerms.map(permission => permission.description ?? permission.key).join('\n')
            : `Bu başlık için ${bucketLabel(bucket).toLocaleLowerCase('tr-TR')} yetkisi yok.`;
          return (
            <div key={bucket} className="flex min-w-0 items-center gap-1.5">
              <label
                className="flex min-w-0 items-center gap-1.5 text-xs cursor-pointer hover:text-foreground text-muted-foreground"
                title={lockedFromBundle ? `${title}\n\n(Paketten geliyor — kilitli)` : title}
              >
                <input
                  type="checkbox"
                  checked={checked}
                  disabled={bucketLocked}
                  ref={el => { if (el) el.indeterminate = someCheckedInBucket; }}
                  onChange={event => onSetResourceBucket(perms, bucket, event.target.checked)}
                  className="h-3.5 w-3.5 rounded border-border accent-primary cursor-pointer disabled:cursor-not-allowed disabled:opacity-50"
                />
                <span className="truncate">{bucketLabel(bucket)}</span>
              </label>
              {lockedFromBundle && (
                <Lock className="h-3 w-3 shrink-0 text-muted-foreground" aria-label="Paketten geliyor (kilitli)" />
              )}
              {bucket === 'advanced' && bucketPerms.length > 0 && (
                <button
                  type="button"
                  className="inline-flex h-5 w-5 shrink-0 items-center justify-center rounded-full text-muted-foreground hover:bg-muted hover:text-foreground"
                  title="Gelişmiş işlemleri göster"
                  aria-label={`${resourceLabel(resource)} gelişmiş işlemlerini ${advancedOpen ? 'gizle' : 'göster'}`}
                  onClick={() => setAdvancedOpen(prev => !prev)}
                >
                  <Info className="h-3.5 w-3.5" />
                </button>
              )}
            </div>
          );
        })}
      </div>
      {advancedOpen && (
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
}

export default RolePermissionMatrix;
