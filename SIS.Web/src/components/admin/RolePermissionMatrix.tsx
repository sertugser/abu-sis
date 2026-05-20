import { useMemo, useState } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
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
import { ChevronDown, ChevronRight, Info, Save, RotateCcw, Package } from 'lucide-react';
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
  usePermissionBundles,
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
  const { data: rolePermIdsData, isLoading: rolePermsLoading } = useRolePermissions(selectedRoleId);
  const updateMutation = useUpdateRolePermissions();
  const { data: bundlesData } = usePermissionBundles();
  const applyBundleMut = useApplyBundleToRole();
  const { hasPermission } = usePermissions();
  const canApplyBundle = hasPermission('system.permission-bundle.apply');

  // React Query default destructure (`data = []`) her render'da YENİ array ref üretiyor;
  // aşağıdaki adjust-state-during-render karşılaştırması sürekli setState tetikliyor
  // ("Too many re-renders" döngüsü). Stabil referans için useMemo ile sabitle.
  const permissions = useMemo(() => permissionsData ?? [], [permissionsData]);
  const roles = useMemo(() => rolesData ?? [], [rolesData]);
  const bundles = useMemo(() => bundlesData ?? [], [bundlesData]);
  const rolePermIds = useMemo<readonly number[]>(
    () => rolePermIdsData ?? EMPTY_NUMBER_ARRAY,
    [rolePermIdsData],
  );

  // Local draft (optimistic) — Set<permissionId>
  const [draft, setDraft] = useState<Set<number>>(new Set());

  // Server'dan rol permission'ları gelince draft'ı senkronize et — adjust-state-during-render
  // pattern (React 19+: useEffect içinde setState yasaklandı, yerine prev-tracking ile render'da update).
  const [prevRolePermIds, setPrevRolePermIds] = useState(rolePermIds);
  const [prevSelectedRoleId, setPrevSelectedRoleId] = useState(selectedRoleId);
  if (prevRolePermIds !== rolePermIds || prevSelectedRoleId !== selectedRoleId) {
    setPrevRolePermIds(rolePermIds);
    setPrevSelectedRoleId(selectedRoleId);
    setDraft(new Set(rolePermIds));
  }

  const grouped = useMemo(() => groupByCategoryAndResource(permissions), [permissions]);
  const original = useMemo(() => new Set(rolePermIds), [rolePermIds]);

  const diff = useMemo(() => {
    const added: number[] = [];
    const removed: number[] = [];
    draft.forEach(id => { if (!original.has(id)) added.push(id); });
    original.forEach(id => { if (!draft.has(id)) removed.push(id); });
    return { added, removed, isDirty: added.length > 0 || removed.length > 0 };
  }, [draft, original]);

  function toggleResourceAll(resourcePerms: PermissionDto[]) {
    const allChecked = resourcePerms.every(p => draft.has(p.id));
    setDraft(prev => {
      const next = new Set(prev);
      if (allChecked) {
        resourcePerms.forEach(p => next.delete(p.id));
      } else {
        resourcePerms.forEach(p => next.add(p.id));
      }
      return next;
    });
  }

  function setResourceBucket(resourcePerms: PermissionDto[], bucket: PermissionBucket, checked: boolean) {
    const { view, edit, advanced } = splitResourcePermissions(resourcePerms);
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
    setDraft(new Set(rolePermIds));
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

  if (permsLoading || rolesLoading) {
    return <Skeleton className="h-96 w-full" />;
  }

  return (
    <div className="grid grid-cols-1 lg:grid-cols-[260px_1fr] gap-6">
      {/* Sol: Rol listesi */}
      <Card>
        <CardHeader>
          <CardTitle className="text-sm font-semibold">Rol Seç</CardTitle>
        </CardHeader>
        <CardContent className="space-y-1">
          {roles.map(role => (
            <button
              key={role.id}
              type="button"
              onClick={() => setSelectedRoleId(role.id)}
              className={cn(
                'w-full text-left px-3 py-2 rounded-md text-sm transition-colors',
                selectedRoleId === role.id
                  ? 'bg-primary text-primary-foreground'
                  : 'hover:bg-muted',
              )}
            >
              {getRoleDisplayLabel(role.name)}
              <div className="text-[11px] opacity-70">{role.name}</div>
            </button>
          ))}
        </CardContent>
      </Card>

      {/* Sağ: Permission matrisi */}
      <Card>
        <CardHeader className="flex flex-row items-center justify-between">
          <div>
            <CardTitle className="text-base font-semibold">Yetki Matrisi</CardTitle>
            {selectedRoleId !== null && (
              <p className="text-xs text-muted-foreground mt-1">
                {permissions.length} permission · {original.size} atanmış
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
                <SelectTrigger className="w-[260px] h-9 text-xs">
                  <Package className="h-3.5 w-3.5 mr-1.5 text-muted-foreground" />
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
                <span className="text-emerald-600 font-medium">+{diff.added.length}</span>
                <span className="mx-1 text-muted-foreground">·</span>
                <span className="text-destructive font-medium">−{diff.removed.length}</span>
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
        </CardHeader>
        <CardContent>
          {selectedRoleId === null ? (
            <p className="text-sm text-muted-foreground">Soldan bir rol seçin.</p>
          ) : rolePermsLoading ? (
            <Skeleton className="h-64 w-full" />
          ) : (
            <ScrollArea className="h-[600px] pr-4">
              <div className="space-y-4">
                {Object.entries(grouped).map(([category, resources]) => (
                  <CategoryGroup
                    key={category}
                    category={category}
                    resources={resources}
                    draft={draft}
                    onToggleResourceAll={toggleResourceAll}
                    onSetResourceBucket={setResourceBucket}
                  />
                ))}
              </div>
            </ScrollArea>
          )}
        </CardContent>
      </Card>
    </div>
  );
}

interface CategoryGroupProps {
  category: string;
  resources: Record<string, PermissionDto[]>;
  draft: Set<number>;
  onToggleResourceAll: (perms: PermissionDto[]) => void;
  onSetResourceBucket: (perms: PermissionDto[], bucket: PermissionBucket, checked: boolean) => void;
}

function CategoryGroup({
  category,
  resources,
  draft,
  onToggleResourceAll,
  onSetResourceBucket,
}: CategoryGroupProps) {
  const [open, setOpen] = useState(true);
  const label = CATEGORY_LABELS[category] ?? category;
  const allPerms = Object.values(resources).flat();
  const checkedCount = allPerms.filter(p => draft.has(p.id)).length;

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
  onToggleResourceAll: (perms: PermissionDto[]) => void;
  onSetResourceBucket: (perms: PermissionDto[], bucket: PermissionBucket, checked: boolean) => void;
}

function ResourceGroup({
  resource,
  perms,
  draft,
  onToggleResourceAll,
  onSetResourceBucket,
}: ResourceGroupProps) {
  const [advancedOpen, setAdvancedOpen] = useState(false);
  const checkedCount = perms.filter(p => draft.has(p.id)).length;
  const allChecked = checkedCount === perms.length && perms.length > 0;
  const someChecked = checkedCount > 0 && checkedCount < perms.length;
  const { advanced } = splitResourcePermissions(perms);

  return (
    <div className="border-l-2 border-border/50 pl-3">
      <div className="flex items-center gap-2 mb-1">
        <input
          type="checkbox"
          checked={allChecked}
          ref={el => { if (el) el.indeterminate = someChecked; }}
          onChange={() => onToggleResourceAll(perms)}
          className="h-4 w-4 rounded border-border accent-primary cursor-pointer"
        />
        <span className="text-sm font-medium">{resourceLabel(resource)}</span>
        <span className="text-[10px] text-muted-foreground">
          ({checkedCount}/{perms.length})
        </span>
      </div>
      <div className="ml-6 grid grid-cols-3 gap-1">
        {(['view', 'edit', 'advanced'] as PermissionBucket[]).map(bucket => {
          const bucketPerms = perms.filter(permission => permissionBucket(permission) === bucket);
          const checked = bucketPerms.length > 0 && bucketPerms.every(permission => draft.has(permission.id));
          const someCheckedInBucket = bucketPerms.some(permission => draft.has(permission.id)) && !checked;
          const title = bucketPerms.length > 0
            ? bucketPerms.map(permission => permission.description ?? permission.key).join('\n')
            : `Bu başlık için ${bucketLabel(bucket).toLocaleLowerCase('tr-TR')} yetkisi yok.`;
          return (
            <div key={bucket} className="flex min-w-0 items-center gap-1.5">
              <label
                className="flex min-w-0 items-center gap-1.5 text-xs cursor-pointer hover:text-foreground text-muted-foreground"
                title={title}
              >
                <input
                  type="checkbox"
                  checked={checked}
                  disabled={bucketPerms.length === 0}
                  ref={el => { if (el) el.indeterminate = someCheckedInBucket; }}
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
