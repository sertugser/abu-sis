import { createElement, useState } from 'react';
import {
  AlertCircle,
  ChevronDown,
  ChevronRight,
  ChevronsUp,
  ChevronsDown,
  Loader2,
  Pencil,
  Plus,
  Trash2,
} from 'lucide-react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card';
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
    <div className="space-y-4">
      {canManage && (
        <div className="flex justify-end">
          <Button onClick={() => setModuleDialog({ mode: 'create', parentId: null })}>
            <Plus className="mr-2 h-4 w-4" />
            Üst Seviye Modül
          </Button>
        </div>
      )}

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
          <CardContent className="py-12 text-center text-sm text-muted-foreground">
            Henüz hiç modül yok. "Üst Seviye Modül" butonuna basarak başlayın.
          </CardContent>
        </Card>
      )}

      {tree && tree.length > 0 && (
        <div className="space-y-3">
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
  const childCount = module.children.length + module.items.length;
  const isFirst = index === 0;
  const isLast = index === siblings.length - 1;

  return (
    <Card className={module.isActive ? '' : 'opacity-60'}>
      <CardHeader className="flex flex-row items-center gap-3 space-y-0 pb-3">
        <button
          type="button"
          onClick={() => onToggle(module.id)}
          className="inline-flex h-7 w-7 shrink-0 items-center justify-center rounded hover:bg-muted"
          aria-label={isExpanded ? 'Daralt' : 'Genişlet'}
        >
          {isExpanded ? <ChevronDown className="h-4 w-4" /> : <ChevronRight className="h-4 w-4" />}
        </button>
        {createElement(ICON_MAP[iconName], { className: 'h-4 w-4 text-muted-foreground' })}
        <div className="min-w-0 flex-1">
          <CardTitle className="flex items-center gap-2 text-base">
            <span className="truncate">{module.label}</span>
            {!module.isActive && <Badge variant="outline">Pasif</Badge>}
            {module.allowedRolesCsv && (
              <Badge variant="secondary" className="text-[10px]">
                {module.allowedRolesCsv}
              </Badge>
            )}
          </CardTitle>
          <CardDescription className="font-mono text-xs">
            {module.slug} · {childCount} öğe
          </CardDescription>
        </div>
        {canManage && (
          <div className="flex items-center gap-1">
            <Button
              variant="ghost"
              size="icon"
              disabled={isFirst}
              onClick={() => onMoveModule(siblings, parentId, index, -1)}
              title="Yukarı taşı"
            >
              <ChevronsUp className="h-4 w-4" />
            </Button>
            <Button
              variant="ghost"
              size="icon"
              disabled={isLast}
              onClick={() => onMoveModule(siblings, parentId, index, 1)}
              title="Aşağı taşı"
            >
              <ChevronsDown className="h-4 w-4" />
            </Button>
            <Button variant="ghost" size="icon" onClick={() => onEditModule(module)} title="Düzenle">
              <Pencil className="h-4 w-4" />
            </Button>
            <Button
              variant="ghost"
              size="icon"
              onClick={() => onDeleteModule(module)}
              title="Sil"
              className="text-destructive hover:text-destructive"
            >
              <Trash2 className="h-4 w-4" />
            </Button>
          </div>
        )}
      </CardHeader>
      {isExpanded && (
        <CardContent className="space-y-3 pl-12">
          {canManage && (
            <div className="flex flex-wrap gap-2">
              <Button variant="outline" size="sm" onClick={() => onAddItem(module)}>
                <Plus className="mr-1 h-3 w-3" /> Sayfa ekle
              </Button>
              <Button variant="outline" size="sm" onClick={() => onAddSubModule(module)}>
                <Plus className="mr-1 h-3 w-3" /> Alt modül ekle
              </Button>
            </div>
          )}

          {module.items.length > 0 && (
            <div className="space-y-1">
              {module.items.map((item, i) => (
                <div
                  key={item.id}
                  className={`flex items-center gap-2 rounded-md border bg-card px-3 py-2 ${
                    item.isActive ? '' : 'opacity-60'
                  }`}
                >
                  <div className="min-w-0 flex-1">
                    <div className="flex items-center gap-2 text-sm">
                      <span className="font-medium">{item.label}</span>
                      {!item.isActive && <Badge variant="outline">Pasif</Badge>}
                      {item.openInNewTab && (
                        <Badge variant="secondary" className="text-[10px]">
                          yeni sekme
                        </Badge>
                      )}
                    </div>
                    <div className="flex items-center gap-2 text-xs text-muted-foreground">
                      <span className="font-mono">{item.route}</span>
                      <span>·</span>
                      <span>{item.permissionKeys.length} yetki</span>
                    </div>
                  </div>
                  {canManage && (
                    <div className="flex items-center gap-0.5">
                      <Button
                        variant="ghost"
                        size="icon"
                        disabled={i === 0}
                        onClick={() => onMoveItem(module.items, module.id, i, -1)}
                        title="Yukarı"
                      >
                        <ChevronsUp className="h-3.5 w-3.5" />
                      </Button>
                      <Button
                        variant="ghost"
                        size="icon"
                        disabled={i === module.items.length - 1}
                        onClick={() => onMoveItem(module.items, module.id, i, 1)}
                        title="Aşağı"
                      >
                        <ChevronsDown className="h-3.5 w-3.5" />
                      </Button>
                      <Button variant="ghost" size="icon" onClick={() => onEditItem(item)} title="Düzenle">
                        <Pencil className="h-3.5 w-3.5" />
                      </Button>
                      <Button
                        variant="ghost"
                        size="icon"
                        onClick={() => onDeleteItem(item)}
                        title="Sil"
                        className="text-destructive hover:text-destructive"
                      >
                        <Trash2 className="h-3.5 w-3.5" />
                      </Button>
                    </div>
                  )}
                </div>
              ))}
            </div>
          )}

          {module.children.length > 0 && (
            <div className="space-y-3 border-l pl-4">
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
        </CardContent>
      )}
    </Card>
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
