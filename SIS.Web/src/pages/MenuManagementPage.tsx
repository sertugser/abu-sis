import { createElement, useState } from 'react';
import {
  AlertCircle,
  ArrowDown,
  ArrowUp,
  ChevronDown,
  ChevronRight,
  Loader2,
  Pencil,
  Plus,
  Trash2,
} from 'lucide-react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { cn } from '@/lib/utils';
import { getRoleDisplayLabel } from '../lib/roles';
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
import { usePermissions } from '../context/PermissionContext';
import {
  useAvailableMenuPermissions,
  useCreateItem,
  useCreateModule,
  useDeleteItem,
  useDeleteModule,
  useMenuTree,
  useReorderItems,
  useReorderModules,
  useUpdateItem,
  useUpdateModule,
} from '../hooks/useMenuAdmin';
import MenuModuleFormDialog from '../components/admin/MenuModuleFormDialog';
import MenuItemFormDialog from '../components/admin/MenuItemFormDialog';
import type { MenuItemAdminDto, MenuModuleAdminDto } from '@/api/menuAdmin.api';
import { ICON_MAP } from '../components/layout/SidebarModules';

type ModuleDialogState =
  | { mode: 'create'; parentId: number | null }
  | { mode: 'edit'; module: MenuModuleAdminDto }
  | null;

type ItemDialogState =
  | { mode: 'create'; moduleId: number }
  | { mode: 'edit'; item: MenuItemAdminDto }
  | null;

type DeleteTarget =
  | { kind: 'module'; module: MenuModuleAdminDto }
  | { kind: 'item'; item: MenuItemAdminDto }
  | null;

export default function MenuManagementPage() {
  const { hasPermission } = usePermissions();
  const canManage = hasPermission('system.menu.manage');
  const { data: tree, isLoading, error } = useMenuTree();
  const { data: permissions } = useAvailableMenuPermissions();

  const createModule = useCreateModule();
  const updateModule = useUpdateModule();
  const deleteModule = useDeleteModule();
  const reorderModules = useReorderModules();
  const createItem = useCreateItem();
  const updateItem = useUpdateItem();
  const deleteItem = useDeleteItem();
  const reorderItems = useReorderItems();

  const [moduleDialog, setModuleDialog] = useState<ModuleDialogState>(null);
  const [itemDialog, setItemDialog] = useState<ItemDialogState>(null);
  const [deleteTarget, setDeleteTarget] = useState<DeleteTarget>(null);
  const [expanded, setExpanded] = useState<Set<number>>(new Set());
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  const toggleExpand = (id: number) =>
    setExpanded((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });

  const moduleMutationPending =
    createModule.isPending || updateModule.isPending;
  const itemMutationPending = createItem.isPending || updateItem.isPending;
  const reorderPending = reorderModules.isPending || reorderItems.isPending;

  const handleSubmitModule = async (values: {
    slug: string;
    label: string;
    icon: string | null;
    allowedRolesCsv: string | null;
    isActive: boolean;
  }) => {
    setErrorMsg(null);
    try {
      if (moduleDialog?.mode === 'edit') {
        await updateModule.mutateAsync({
          moduleId: moduleDialog.module.id,
          payload: {
            label: values.label,
            icon: values.icon,
            allowedRolesCsv: values.allowedRolesCsv,
            isActive: values.isActive,
          },
        });
      } else if (moduleDialog?.mode === 'create') {
        await createModule.mutateAsync({
          parentId: moduleDialog.parentId,
          slug: values.slug,
          label: values.label,
          icon: values.icon,
          allowedRolesCsv: values.allowedRolesCsv,
        });
      }
      setModuleDialog(null);
    } catch (e: unknown) {
      setErrorMsg(extractError(e));
    }
  };

  const handleSubmitItem = async (values: {
    slug: string;
    label: string;
    route: string;
    openInNewTab: boolean;
    isActive: boolean;
    permissionKeys: string[];
  }) => {
    setErrorMsg(null);
    try {
      if (itemDialog?.mode === 'edit') {
        await updateItem.mutateAsync({
          itemId: itemDialog.item.id,
          payload: {
            label: values.label,
            route: values.route,
            openInNewTab: values.openInNewTab,
            isActive: values.isActive,
            permissionKeys: values.permissionKeys,
          },
        });
      } else if (itemDialog?.mode === 'create') {
        await createItem.mutateAsync({
          moduleId: itemDialog.moduleId,
          slug: values.slug,
          label: values.label,
          route: values.route,
          openInNewTab: values.openInNewTab,
          permissionKeys: values.permissionKeys,
        });
      }
      setItemDialog(null);
    } catch (e: unknown) {
      setErrorMsg(extractError(e));
    }
  };

  const handleConfirmDelete = async () => {
    if (!deleteTarget) return;
    try {
      if (deleteTarget.kind === 'module') {
        await deleteModule.mutateAsync(deleteTarget.module.id);
      } else {
        await deleteItem.mutateAsync(deleteTarget.item.id);
      }
      setDeleteTarget(null);
    } catch (e: unknown) {
      setErrorMsg(extractError(e));
      setDeleteTarget(null);
    }
  };

  const handleMoveModule = async (
    siblings: MenuModuleAdminDto[],
    parentId: number | null,
    index: number,
    direction: -1 | 1,
  ) => {
    const target = index + direction;
    if (target < 0 || target >= siblings.length) return;
    const reordered = [...siblings];
    const [m] = reordered.splice(index, 1);
    reordered.splice(target, 0, m);
    const entries = reordered.map((mod, i) => ({ id: mod.id, sortOrder: i + 1 }));
    await reorderModules.mutateAsync({ parentId, entries });
  };

  const handleMoveItem = async (
    items: MenuItemAdminDto[],
    moduleId: number,
    index: number,
    direction: -1 | 1,
  ) => {
    const target = index + direction;
    if (target < 0 || target >= items.length) return;
    const reordered = [...items];
    const [it] = reordered.splice(index, 1);
    reordered.splice(target, 0, it);
    const entries = reordered.map((item, i) => ({ id: item.id, sortOrder: i + 1 }));
    await reorderItems.mutateAsync({ moduleId, entries });
  };

  return (
    <div className="animate-fade-in space-y-4">
      {errorMsg && (
        <Alert variant="destructive">
          <AlertCircle className="h-4 w-4" />
          <AlertDescription>{errorMsg}</AlertDescription>
        </Alert>
      )}

      {isLoading && (
        <div className="flex items-center gap-2 text-sm text-muted-foreground">
          <Loader2 className="h-4 w-4 animate-spin" /> Yükleniyor...
        </div>
      )}
      {error && (
        <Alert variant="destructive">
          <AlertCircle className="h-4 w-4" />
          <AlertDescription>Menü ağacı yüklenemedi.</AlertDescription>
        </Alert>
      )}

      {tree && tree.length === 0 && (
        <Card>
          <CardContent className="flex flex-col items-center gap-4 py-12 text-center text-sm text-muted-foreground">
            <p>Henüz menü grubu yok.</p>
            {canManage && (
              <Button onClick={() => setModuleDialog({ mode: 'create', parentId: null })}>
                <Plus className="mr-2 h-4 w-4" />
                Yeni Menü Grubu
              </Button>
            )}
          </CardContent>
        </Card>
      )}

      {tree && tree.length > 0 && (
        <div className="overflow-hidden rounded-lg border bg-card shadow-sm">
          {canManage && (
            <div className="flex justify-end border-b bg-muted/30 px-3 py-2">
              <Button size="sm" onClick={() => setModuleDialog({ mode: 'create', parentId: null })}>
                <Plus className="mr-2 h-4 w-4" />
                Yeni Menü Grubu
              </Button>
            </div>
          )}
          <div className="stagger-children divide-y">
            {tree.map((mod, idx) => (
              <ModuleCard
                key={mod.id}
                module={mod}
                siblings={tree}
                index={idx}
                parentId={null}
                expanded={expanded}
                onToggle={toggleExpand}
                canManage={canManage}
                reorderPending={reorderPending}
                onEditModule={(m) => setModuleDialog({ mode: 'edit', module: m })}
                onAddSubModule={(parent) =>
                  setModuleDialog({ mode: 'create', parentId: parent.id })
                }
                onDeleteModule={(m) => setDeleteTarget({ kind: 'module', module: m })}
                onMoveModule={handleMoveModule}
                onAddItem={(m) => setItemDialog({ mode: 'create', moduleId: m.id })}
                onEditItem={(i) => setItemDialog({ mode: 'edit', item: i })}
                onDeleteItem={(i) => setDeleteTarget({ kind: 'item', item: i })}
                onMoveItem={handleMoveItem}
              />
            ))}
          </div>
        </div>
      )}

      <MenuModuleFormDialog
        open={moduleDialog !== null}
        onOpenChange={(open) => !open && setModuleDialog(null)}
        module={moduleDialog?.mode === 'edit' ? moduleDialog.module : null}
        parentId={moduleDialog?.mode === 'create' ? moduleDialog.parentId : null}
        onSubmit={handleSubmitModule}
        isPending={moduleMutationPending}
        errorMessage={errorMsg}
      />
      <MenuItemFormDialog
        open={itemDialog !== null}
        onOpenChange={(open) => !open && setItemDialog(null)}
        item={itemDialog?.mode === 'edit' ? itemDialog.item : null}
        permissions={permissions ?? []}
        onSubmit={handleSubmitItem}
        isPending={itemMutationPending}
        errorMessage={errorMsg}
      />

      <AlertDialog open={deleteTarget !== null} onOpenChange={(open) => !open && setDeleteTarget(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Silme onayı</AlertDialogTitle>
            <AlertDialogDescription>
              {deleteTarget?.kind === 'module'
                ? `"${deleteTarget.module.label}" modülü ve içindeki tüm alt modüller + sayfalar silinecek.`
                : deleteTarget?.kind === 'item'
                  ? `"${deleteTarget.item.label}" sayfası silinecek.`
                  : ''}
              {' '}Bu işlem geri alınamaz.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>İptal</AlertDialogCancel>
            <AlertDialogAction onClick={handleConfirmDelete}>Sil</AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}

interface ModuleCardProps {
  module: MenuModuleAdminDto;
  siblings: MenuModuleAdminDto[];
  index: number;
  parentId: number | null;
  expanded: Set<number>;
  onToggle: (id: number) => void;
  canManage: boolean;
  reorderPending: boolean;
  onEditModule: (m: MenuModuleAdminDto) => void;
  onAddSubModule: (parent: MenuModuleAdminDto) => void;
  onDeleteModule: (m: MenuModuleAdminDto) => void;
  onMoveModule: (
    siblings: MenuModuleAdminDto[],
    parentId: number | null,
    index: number,
    direction: -1 | 1,
  ) => void;
  onAddItem: (m: MenuModuleAdminDto) => void;
  onEditItem: (i: MenuItemAdminDto) => void;
  onDeleteItem: (i: MenuItemAdminDto) => void;
  onMoveItem: (
    items: MenuItemAdminDto[],
    moduleId: number,
    index: number,
    direction: -1 | 1,
  ) => void;
}

function ModuleCard({
  module,
  siblings,
  index,
  parentId,
  expanded,
  onToggle,
  canManage,
  reorderPending,
  onEditModule,
  onAddSubModule,
  onDeleteModule,
  onMoveModule,
  onAddItem,
  onEditItem,
  onDeleteItem,
  onMoveItem,
}: ModuleCardProps) {
  const isExpanded = expanded.has(module.id);
  const iconName = module.icon && module.icon in ICON_MAP ? module.icon : 'HelpCircle';
  const groupCount = module.children.length;
  const pageCount = module.items.length;
  const isFirst = index === 0;
  const isLast = index === siblings.length - 1;
  const roles = module.allowedRolesCsv
    ? module.allowedRolesCsv.split(',').map((r) => r.trim()).filter(Boolean)
    : [];

  const summary =
    groupCount > 0 && pageCount > 0
      ? `${groupCount} grup · ${pageCount} sayfa`
      : groupCount > 0
        ? `${groupCount} grup`
        : pageCount > 0
          ? `${pageCount} sayfa`
          : 'Boş';

  return (
    <div className={cn(!module.isActive && 'opacity-60')}>
      <div className="group flex items-center gap-2 px-3 py-2.5 transition-colors hover:bg-muted/40">
        <button
          type="button"
          onClick={() => onToggle(module.id)}
          className="inline-flex h-7 w-7 shrink-0 items-center justify-center rounded text-muted-foreground hover:bg-muted"
          aria-label={isExpanded ? 'Daralt' : 'Genişlet'}
        >
          {isExpanded ? <ChevronDown className="h-4 w-4" /> : <ChevronRight className="h-4 w-4" />}
        </button>
        <span className="flex h-8 w-8 shrink-0 items-center justify-center rounded-md bg-accent text-primary">
          {createElement(ICON_MAP[iconName], { className: 'h-4 w-4' })}
        </span>
        <button type="button" onClick={() => onToggle(module.id)} className="min-w-0 flex-1 text-left">
          <div className="flex items-center gap-2">
            <span className="truncate text-sm font-semibold">{module.label}</span>
            {!module.isActive && (
              <Badge variant="outline" className="text-[10px]">Pasif</Badge>
            )}
            {roles.map((r) => (
              <Badge key={r} variant="secondary" className="text-[10px]">
                {getRoleDisplayLabel(r)}
              </Badge>
            ))}
          </div>
          <div className="text-xs text-muted-foreground">{summary}</div>
        </button>
        {canManage && (
          <div className="flex items-center gap-0.5 opacity-0 transition-opacity focus-within:opacity-100 group-hover:opacity-100">
            <Button variant="ghost" size="icon" className="h-8 w-8" disabled={isFirst || reorderPending} onClick={() => onMoveModule(siblings, parentId, index, -1)} title="Yukarı taşı">
              <ArrowUp className="h-4 w-4" />
            </Button>
            <Button variant="ghost" size="icon" className="h-8 w-8" disabled={isLast || reorderPending} onClick={() => onMoveModule(siblings, parentId, index, 1)} title="Aşağı taşı">
              <ArrowDown className="h-4 w-4" />
            </Button>
            <Button variant="ghost" size="icon" className="h-8 w-8" onClick={() => onEditModule(module)} title="Düzenle">
              <Pencil className="h-4 w-4" />
            </Button>
            <Button variant="ghost" size="icon" className="h-8 w-8 text-destructive hover:text-destructive" onClick={() => onDeleteModule(module)} title="Sil">
              <Trash2 className="h-4 w-4" />
            </Button>
          </div>
        )}
      </div>
      {isExpanded && (
        <div className="space-y-2 border-t px-3 py-2 pl-12">
          {canManage && (
            <div className="flex flex-wrap gap-2">
              <Button variant="outline" size="sm" onClick={() => onAddItem(module)}>
                <Plus className="mr-1 h-3 w-3" /> Sayfa ekle
              </Button>
              <Button variant="outline" size="sm" onClick={() => onAddSubModule(module)}>
                <Plus className="mr-1 h-3 w-3" /> Alt grup ekle
              </Button>
            </div>
          )}

          {pageCount > 0 && (
            <div className="space-y-0.5">
              {module.items.map((item, i) => (
                <div
                  key={item.id}
                  className={cn(
                    'group flex items-center gap-2 rounded-md px-2 py-1.5 hover:bg-muted/60',
                    !item.isActive && 'opacity-60',
                  )}
                >
                  <span className="h-1.5 w-1.5 shrink-0 rounded-full bg-muted-foreground/40" />
                  <div className="min-w-0 flex-1">
                    <div className="flex items-center gap-2 text-sm">
                      <span className="truncate font-medium">{item.label}</span>
                      {!item.isActive && (
                        <Badge variant="outline" className="text-[10px]">Pasif</Badge>
                      )}
                      {item.openInNewTab && (
                        <Badge variant="secondary" className="text-[10px]">yeni sekme</Badge>
                      )}
                    </div>
                    <div className="text-xs text-muted-foreground">
                      {item.permissionKeys.length} yetki
                    </div>
                  </div>
                  {canManage && (
                    <div className="flex items-center gap-0.5 opacity-0 transition-opacity focus-within:opacity-100 group-hover:opacity-100">
                      <Button variant="ghost" size="icon" className="h-7 w-7" disabled={i === 0 || reorderPending} onClick={() => onMoveItem(module.items, module.id, i, -1)} title="Yukarı taşı">
                        <ArrowUp className="h-3.5 w-3.5" />
                      </Button>
                      <Button variant="ghost" size="icon" className="h-7 w-7" disabled={i === pageCount - 1 || reorderPending} onClick={() => onMoveItem(module.items, module.id, i, 1)} title="Aşağı taşı">
                        <ArrowDown className="h-3.5 w-3.5" />
                      </Button>
                      <Button variant="ghost" size="icon" className="h-7 w-7" onClick={() => onEditItem(item)} title="Düzenle">
                        <Pencil className="h-3.5 w-3.5" />
                      </Button>
                      <Button variant="ghost" size="icon" className="h-7 w-7 text-destructive hover:text-destructive" onClick={() => onDeleteItem(item)} title="Sil">
                        <Trash2 className="h-3.5 w-3.5" />
                      </Button>
                    </div>
                  )}
                </div>
              ))}
            </div>
          )}

          {groupCount > 0 && (
            <div className="space-y-2 border-l pl-3">
              {module.children.map((child, i) => (
                <ModuleCard
                  key={child.id}
                  module={child}
                  siblings={module.children}
                  index={i}
                  parentId={module.id}
                  expanded={expanded}
                  onToggle={onToggle}
                  canManage={canManage}
                  reorderPending={reorderPending}
                  onEditModule={onEditModule}
                  onAddSubModule={onAddSubModule}
                  onDeleteModule={onDeleteModule}
                  onMoveModule={onMoveModule}
                  onAddItem={onAddItem}
                  onEditItem={onEditItem}
                  onDeleteItem={onDeleteItem}
                  onMoveItem={onMoveItem}
                />
              ))}
            </div>
          )}
        </div>
      )}
    </div>
  );
}

function extractError(e: unknown): string {
  if (typeof e === 'object' && e !== null) {
    const ax = e as {
      response?: { data?: { error?: string; message?: string } };
      message?: string;
    };
    return (
      ax.response?.data?.error ??
      ax.response?.data?.message ??
      ax.message ??
      'Bilinmeyen bir hata oluştu.'
    );
  }
  return 'Bilinmeyen bir hata oluştu.';
}
