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
import { ChevronDown, ChevronRight, Save, RotateCcw } from 'lucide-react';
import { cn } from '@/lib/utils';
import { getRoleDisplayLabel } from '../../lib/roles';
import {
  useAllPermissions,
  useAssignableRoles,
  useRolePermissions,
  useUpdateRolePermissions,
} from '../../hooks/useRolePermissions';
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

const ACTION_LABELS_TR: Record<string, string> = {
  view: 'Görüntüle',
  create: 'Oluştur',
  edit: 'Düzenle',
  delete: 'Sil',
  cancel: 'İptal Et',
  uncancel: 'İptali Geri Al',
  approve: 'Onayla',
  reject: 'Reddet',
  override: 'Override',
  export: 'Dışa Aktar',
  import: 'İçe Aktar',
  assign: 'Ata',
  publish: 'Yayınla',
  generate: 'Üret',
  apply: 'Uygula',
  rollback: 'Geri Al',
  simulate: 'Simüle Et',
  finalize: 'Sonuçlandır',
  unfinalize: 'Sonucu Geri Al',
  submit: 'Gönder',
  'reset-password': 'Şifre Sıfırla',
  'set-status': 'Durumu Değiştir',
  'update-roles': 'Rolleri Değiştir',
  'lock-grades': 'Notları Kilitle',
  'unlock-grades': 'Not Kilidini Aç',
  'calculate-gpa': 'GPA Hesapla',
  'open-new-term': 'Yeni Dönem Aç',
  promote: 'Sınıf Atlat',
  regenerate: 'Yeniden Üret',
  transfer: 'Transfer Et',
};

function actionLabel(action: string): string {
  return ACTION_LABELS_TR[action] ?? action;
}

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

export function RolePermissionMatrix() {
  const { data: permissions = [], isLoading: permsLoading } = useAllPermissions();
  const { data: roles = [], isLoading: rolesLoading } = useAssignableRoles();
  const [selectedRoleId, setSelectedRoleId] = useState<number | null>(null);
  const { data: rolePermIds = [], isLoading: rolePermsLoading } = useRolePermissions(selectedRoleId);
  const updateMutation = useUpdateRolePermissions();

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

  function togglePermission(id: number) {
    setDraft(prev => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  }

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

  function reset() {
    setDraft(new Set(rolePermIds));
  }

  function save() {
    if (selectedRoleId === null) return;
    updateMutation.mutate({ roleId: selectedRoleId, permissionIds: Array.from(draft) });
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
          <p className="pt-3 text-[11px] text-muted-foreground">
            SystemAdministrator listede yok — kod düzeyinde tüm yetkilere otomatik erişir
            (Karar 4 — locked).
          </p>
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
                    onTogglePermission={togglePermission}
                    onToggleResourceAll={toggleResourceAll}
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
  onTogglePermission: (id: number) => void;
  onToggleResourceAll: (perms: PermissionDto[]) => void;
}

function CategoryGroup({
  category,
  resources,
  draft,
  onTogglePermission,
  onToggleResourceAll,
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
              onTogglePermission={onTogglePermission}
              onToggleResourceAll={onToggleResourceAll}
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
  onTogglePermission: (id: number) => void;
  onToggleResourceAll: (perms: PermissionDto[]) => void;
}

function ResourceGroup({
  resource,
  perms,
  draft,
  onTogglePermission,
  onToggleResourceAll,
}: ResourceGroupProps) {
  const checkedCount = perms.filter(p => draft.has(p.id)).length;
  const allChecked = checkedCount === perms.length && perms.length > 0;
  const someChecked = checkedCount > 0 && checkedCount < perms.length;

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
      <div className="ml-6 grid grid-cols-2 md:grid-cols-3 gap-1">
        {perms
          .sort((a, b) => a.key.localeCompare(b.key, 'tr'))
          .map(perm => {
            const { action } = parseKey(perm.key);
            const checked = draft.has(perm.id);
            return (
              <label
                key={perm.id}
                className="flex items-center gap-1.5 text-xs cursor-pointer hover:text-foreground text-muted-foreground"
                title={perm.description ?? perm.key}
              >
                <input
                  type="checkbox"
                  checked={checked}
                  onChange={() => onTogglePermission(perm.id)}
                  className="h-3.5 w-3.5 rounded border-border accent-primary cursor-pointer"
                />
                {actionLabel(action)}
              </label>
            );
          })}
      </div>
    </div>
  );
}

export default RolePermissionMatrix;
