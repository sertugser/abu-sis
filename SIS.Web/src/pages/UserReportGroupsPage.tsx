import { useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import {
  Check,
  FileBarChart2,
  FolderTree,
  Loader2,
  Pencil,
  Plus,
  ShieldCheck,
  Trash2,
  X,
} from 'lucide-react';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Badge } from '@/components/ui/badge';
import { Textarea } from '@/components/ui/textarea';
import { Alert, AlertDescription } from '@/components/ui/alert';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
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
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Switch } from '@/components/ui/switch';
import { getAssignableRoles, type RoleDto } from '@/api/permissionAdmin.api';
import { usePermissions } from '../context/PermissionContext';
import {
  useCreateReport,
  useCreateReportCategory,
  useDeleteReport,
  useDeleteReportCategory,
  useDeleteReportOverride,
  useReportCategories,
  useReportOverrides,
  useReportPermissionMatrix,
  useReports,
  useSetCategoryRoles,
  useUpdateReport,
  useUpdateReportCategory,
  useUpsertReportOverride,
} from '../hooks/useReportAuthorization';
import { getRoleDisplayLabel } from '../lib/roles';
import type {
  ReportCategoryDto,
  ReportDefinitionDto,
  ReportPermissionOverrideDto,
} from '@/api/reportAuthorization.api';

function extractError(err: unknown, fallback: string): string {
  if (err && typeof err === 'object') {
    const resp = (err as { response?: { data?: { message?: string } } }).response;
    if (resp?.data?.message) return resp.data.message;
  }
  return fallback;
}

export default function UserReportGroupsPage() {
  const { hasPermission } = usePermissions();
  const canManage = hasPermission('menu.kullanici-rapor-gruplari.manage');

  return (
    <div className="space-y-6">
      <Tabs defaultValue="categories" className="space-y-4">
        <TabsList>
          <TabsTrigger value="categories">
            <FolderTree className="h-4 w-4 mr-2" />
            Kategoriler
          </TabsTrigger>
          <TabsTrigger value="reports">
            <FileBarChart2 className="h-4 w-4 mr-2" />
            Raporlar
          </TabsTrigger>
          <TabsTrigger value="permissions">
            <ShieldCheck className="h-4 w-4 mr-2" />
            Yetkiler
          </TabsTrigger>
        </TabsList>

        <TabsContent value="categories">
          <CategoriesTab canManage={canManage} />
        </TabsContent>
        <TabsContent value="reports">
          <ReportsTab canManage={canManage} />
        </TabsContent>
        <TabsContent value="permissions">
          <PermissionsTab canManage={canManage} />
        </TabsContent>
      </Tabs>
    </div>
  );
}

// ════════════════════════════════════════════════════════════
// Categories Tab
// ════════════════════════════════════════════════════════════

function CategoriesTab({ canManage }: { canManage: boolean }) {
  const { data: categories, isLoading } = useReportCategories();
  const createMut = useCreateReportCategory();
  const updateMut = useUpdateReportCategory();
  const deleteMut = useDeleteReportCategory();

  const [dialogOpen, setDialogOpen] = useState(false);
  const [editTarget, setEditTarget] = useState<ReportCategoryDto | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<ReportCategoryDto | null>(null);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between">
        <div>
          <CardTitle>Kategoriler</CardTitle>
          <CardDescription>
            Raporları gruplayan kategoriler. Code stable bir slug olmalı (sonradan değişmez).
          </CardDescription>
        </div>
        {canManage && (
          <Button onClick={() => { setEditTarget(null); setErrorMsg(null); setDialogOpen(true); }}>
            <Plus className="h-4 w-4 mr-2" />
            Yeni Kategori
          </Button>
        )}
      </CardHeader>
      <CardContent>
        {isLoading ? (
          <div className="flex items-center gap-2 py-8 justify-center text-muted-foreground">
            <Loader2 className="h-4 w-4 animate-spin" />Yükleniyor...
          </div>
        ) : !categories || categories.length === 0 ? (
          <div className="py-12 text-center text-muted-foreground text-sm">Kategori yok.</div>
        ) : (
          <table className="w-full text-sm">
            <thead className="bg-muted/40 text-xs uppercase text-muted-foreground">
              <tr>
                <th className="px-3 py-2 text-left font-medium">Kod</th>
                <th className="px-3 py-2 text-left font-medium">Ad</th>
                <th className="px-3 py-2 text-left font-medium">Rapor Sayısı</th>
                <th className="px-3 py-2 text-right font-medium">İşlem</th>
              </tr>
            </thead>
            <tbody className="divide-y">
              {categories.map((c) => (
                <tr key={c.id} className="hover:bg-muted/30">
                  <td className="px-3 py-2 font-mono text-xs">{c.code}</td>
                  <td className="px-3 py-2 font-medium">{c.name}</td>
                  <td className="px-3 py-2"><Badge variant="secondary">{c.reportCount}</Badge></td>
                  <td className="px-3 py-2 text-right">
                    {canManage && (
                      <>
                        <Button
                          variant="ghost"
                          size="sm"
                          onClick={() => { setEditTarget(c); setErrorMsg(null); setDialogOpen(true); }}
                        >
                          <Pencil className="h-4 w-4" />
                        </Button>
                        <Button
                          variant="ghost"
                          size="sm"
                          onClick={() => setDeleteTarget(c)}
                          className="text-destructive"
                          disabled={c.reportCount > 0}
                          title={c.reportCount > 0 ? 'Önce raporları boşalt' : 'Sil'}
                        >
                          <Trash2 className="h-4 w-4" />
                        </Button>
                      </>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </CardContent>

      <CategoryFormDialog
        open={dialogOpen}
        onOpenChange={setDialogOpen}
        target={editTarget}
        errorMsg={errorMsg}
        isPending={createMut.isPending || updateMut.isPending}
        onSubmit={async (values) => {
          setErrorMsg(null);
          try {
            if (editTarget) {
              await updateMut.mutateAsync({ id: editTarget.id, dto: { name: values.name } });
            } else {
              await createMut.mutateAsync({ code: values.code, name: values.name });
            }
            setDialogOpen(false);
          } catch (err) {
            setErrorMsg(extractError(err, 'İşlem başarısız.'));
          }
        }}
      />

      <AlertDialog open={!!deleteTarget} onOpenChange={(o) => { if (!o) setDeleteTarget(null); }}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Kategoriyi sil</AlertDialogTitle>
            <AlertDialogDescription>
              <strong>{deleteTarget?.name}</strong> kategorisi silinecek. Bu işlem geri alınamaz.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Vazgeç</AlertDialogCancel>
            <AlertDialogAction
              onClick={async () => {
                if (deleteTarget) {
                  try {
                    await deleteMut.mutateAsync(deleteTarget.id);
                  } catch {
                    /* error swallowed; consider toast */
                  }
                  setDeleteTarget(null);
                }
              }}
              className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
            >
              Sil
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </Card>
  );
}

function CategoryFormDialog({
  open, onOpenChange, target, onSubmit, isPending, errorMsg,
}: {
  open: boolean;
  onOpenChange: (o: boolean) => void;
  target: ReportCategoryDto | null;
  onSubmit: (v: { code: string; name: string }) => Promise<void>;
  isPending: boolean;
  errorMsg: string | null;
}) {
  const [code, setCode] = useState('');
  const [name, setName] = useState('');
  const isEdit = !!target;

  const handleOpen = (o: boolean) => {
    if (o) {
      setCode(target?.code ?? '');
      setName(target?.name ?? '');
    }
    onOpenChange(o);
  };

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    await onSubmit({ code: code.trim(), name: name.trim() });
  };

  return (
    <Dialog open={open} onOpenChange={handleOpen}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{isEdit ? 'Kategoriyi Düzenle' : 'Yeni Kategori'}</DialogTitle>
          <DialogDescription>
            {isEdit ? 'Sadece adı güncelleyebilirsiniz.' : 'Code stable bir slug olmalı (a-z, 0-9, -, _).'}
          </DialogDescription>
        </DialogHeader>
        <form onSubmit={submit} className="space-y-4">
          <div className="space-y-2">
            <Label htmlFor="cat-code">Kod</Label>
            <Input
              id="cat-code"
              value={code}
              onChange={(e) => setCode(e.target.value)}
              disabled={isEdit}
              placeholder="academic"
              required
            />
          </div>
          <div className="space-y-2">
            <Label htmlFor="cat-name">Ad</Label>
            <Input
              id="cat-name"
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder="Akademik Raporlar"
              required
            />
          </div>
          {errorMsg && <Alert variant="destructive"><AlertDescription>{errorMsg}</AlertDescription></Alert>}
          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)} disabled={isPending}>
              İptal
            </Button>
            <Button type="submit" disabled={isPending || !code.trim() || !name.trim()}>
              {isPending && <Loader2 className="h-4 w-4 mr-2 animate-spin" />}
              {isEdit ? 'Güncelle' : 'Oluştur'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}

// ════════════════════════════════════════════════════════════
// Reports Tab
// ════════════════════════════════════════════════════════════

function ReportsTab({ canManage }: { canManage: boolean }) {
  const { data: categories } = useReportCategories();
  const [filterCategoryId, setFilterCategoryId] = useState<string>('all');
  const filterId = filterCategoryId === 'all' ? undefined : Number(filterCategoryId);
  const { data: reports, isLoading } = useReports(filterId);

  const createMut = useCreateReport();
  const updateMut = useUpdateReport();
  const deleteMut = useDeleteReport();

  const [dialogOpen, setDialogOpen] = useState(false);
  const [editTarget, setEditTarget] = useState<ReportDefinitionDto | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<ReportDefinitionDto | null>(null);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between gap-4">
        <div>
          <CardTitle>Raporlar</CardTitle>
          <CardDescription>
            Her raporun stable bir kodu, başlığı ve bağlı bir kategorisi olur.
          </CardDescription>
        </div>
        <div className="flex items-center gap-3">
          <Select value={filterCategoryId} onValueChange={setFilterCategoryId}>
            <SelectTrigger className="w-56">
              <SelectValue placeholder="Kategoriye göre filtrele" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">Tümü</SelectItem>
              {categories?.map((c) => (
                <SelectItem key={c.id} value={String(c.id)}>{c.name}</SelectItem>
              ))}
            </SelectContent>
          </Select>
          {canManage && (
            <Button
              onClick={() => { setEditTarget(null); setErrorMsg(null); setDialogOpen(true); }}
              disabled={!categories || categories.length === 0}
              title={!categories?.length ? 'Önce kategori oluştur' : ''}
            >
              <Plus className="h-4 w-4 mr-2" />
              Yeni Rapor
            </Button>
          )}
        </div>
      </CardHeader>
      <CardContent>
        {isLoading ? (
          <div className="flex items-center gap-2 py-8 justify-center text-muted-foreground">
            <Loader2 className="h-4 w-4 animate-spin" />Yükleniyor...
          </div>
        ) : !reports || reports.length === 0 ? (
          <div className="py-12 text-center text-muted-foreground text-sm">Rapor yok.</div>
        ) : (
          <table className="w-full text-sm">
            <thead className="bg-muted/40 text-xs uppercase text-muted-foreground">
              <tr>
                <th className="px-3 py-2 text-left font-medium">Kod</th>
                <th className="px-3 py-2 text-left font-medium">Ad</th>
                <th className="px-3 py-2 text-left font-medium">Kategori</th>
                <th className="px-3 py-2 text-right font-medium">İşlem</th>
              </tr>
            </thead>
            <tbody className="divide-y">
              {reports.map((r) => (
                <tr key={r.id} className="hover:bg-muted/30">
                  <td className="px-3 py-2 font-mono text-xs">{r.code}</td>
                  <td className="px-3 py-2">
                    <div className="font-medium">{r.name}</div>
                    {r.description && (
                      <div className="text-xs text-muted-foreground line-clamp-1">{r.description}</div>
                    )}
                  </td>
                  <td className="px-3 py-2"><Badge variant="outline">{r.categoryName}</Badge></td>
                  <td className="px-3 py-2 text-right">
                    {canManage && (
                      <>
                        <Button
                          variant="ghost" size="sm"
                          onClick={() => { setEditTarget(r); setErrorMsg(null); setDialogOpen(true); }}
                        >
                          <Pencil className="h-4 w-4" />
                        </Button>
                        <Button
                          variant="ghost" size="sm"
                          onClick={() => setDeleteTarget(r)}
                          className="text-destructive"
                        >
                          <Trash2 className="h-4 w-4" />
                        </Button>
                      </>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </CardContent>

      <ReportFormDialog
        open={dialogOpen}
        onOpenChange={setDialogOpen}
        target={editTarget}
        categories={categories ?? []}
        errorMsg={errorMsg}
        isPending={createMut.isPending || updateMut.isPending}
        onSubmit={async (v) => {
          setErrorMsg(null);
          try {
            if (editTarget) {
              await updateMut.mutateAsync({
                id: editTarget.id,
                dto: { name: v.name, description: v.description, categoryId: v.categoryId },
              });
            } else {
              await createMut.mutateAsync({
                code: v.code, name: v.name, description: v.description, categoryId: v.categoryId,
              });
            }
            setDialogOpen(false);
          } catch (err) {
            setErrorMsg(extractError(err, 'İşlem başarısız.'));
          }
        }}
      />

      <AlertDialog open={!!deleteTarget} onOpenChange={(o) => { if (!o) setDeleteTarget(null); }}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Raporu sil</AlertDialogTitle>
            <AlertDialogDescription>
              <strong>{deleteTarget?.name}</strong> raporu silinecek. Bağlı override'lar da otomatik temizlenir.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Vazgeç</AlertDialogCancel>
            <AlertDialogAction
              onClick={async () => {
                if (deleteTarget) {
                  await deleteMut.mutateAsync(deleteTarget.id);
                  setDeleteTarget(null);
                }
              }}
              className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
            >
              Sil
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </Card>
  );
}

function ReportFormDialog({
  open, onOpenChange, target, categories, onSubmit, isPending, errorMsg,
}: {
  open: boolean;
  onOpenChange: (o: boolean) => void;
  target: ReportDefinitionDto | null;
  categories: ReportCategoryDto[];
  onSubmit: (v: { code: string; name: string; description: string | null; categoryId: number }) => Promise<void>;
  isPending: boolean;
  errorMsg: string | null;
}) {
  const isEdit = !!target;
  const [code, setCode] = useState('');
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [categoryId, setCategoryId] = useState<string>('');

  const handleOpen = (o: boolean) => {
    if (o) {
      setCode(target?.code ?? '');
      setName(target?.name ?? '');
      setDescription(target?.description ?? '');
      setCategoryId(target ? String(target.categoryId) : (categories[0] ? String(categories[0].id) : ''));
    }
    onOpenChange(o);
  };

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!categoryId) return;
    await onSubmit({
      code: code.trim(),
      name: name.trim(),
      description: description.trim() || null,
      categoryId: Number(categoryId),
    });
  };

  return (
    <Dialog open={open} onOpenChange={handleOpen}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{isEdit ? 'Raporu Düzenle' : 'Yeni Rapor'}</DialogTitle>
          <DialogDescription>
            {isEdit ? 'Code değiştirilemez.' : 'Code stable bir slug olmalı.'}
          </DialogDescription>
        </DialogHeader>
        <form onSubmit={submit} className="space-y-4">
          <div className="space-y-2">
            <Label htmlFor="rep-code">Kod</Label>
            <Input
              id="rep-code"
              value={code}
              onChange={(e) => setCode(e.target.value)}
              disabled={isEdit}
              placeholder="ogrenci-mezuniyet-listesi"
              required
            />
          </div>
          <div className="space-y-2">
            <Label htmlFor="rep-name">Ad</Label>
            <Input
              id="rep-name"
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder="Mezuniyet Listesi"
              required
            />
          </div>
          <div className="space-y-2">
            <Label htmlFor="rep-cat">Kategori</Label>
            <Select value={categoryId} onValueChange={setCategoryId}>
              <SelectTrigger id="rep-cat">
                <SelectValue placeholder="Kategori seç" />
              </SelectTrigger>
              <SelectContent>
                {categories.map((c) => (
                  <SelectItem key={c.id} value={String(c.id)}>{c.name}</SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-2">
            <Label htmlFor="rep-desc">Açıklama (opsiyonel)</Label>
            <Textarea
              id="rep-desc"
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              rows={2}
            />
          </div>
          {errorMsg && <Alert variant="destructive"><AlertDescription>{errorMsg}</AlertDescription></Alert>}
          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)} disabled={isPending}>
              İptal
            </Button>
            <Button type="submit" disabled={isPending || !code.trim() || !name.trim() || !categoryId}>
              {isPending && <Loader2 className="h-4 w-4 mr-2 animate-spin" />}
              {isEdit ? 'Güncelle' : 'Oluştur'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}

// ════════════════════════════════════════════════════════════
// Permissions Tab (matrix + override list)
// ════════════════════════════════════════════════════════════

function PermissionsTab({ canManage }: { canManage: boolean }) {
  const { data: matrix, isLoading: matrixLoading } = useReportPermissionMatrix();
  const { data: roles } = useQuery({
    queryKey: ['admin', 'roles'],
    queryFn: () => getAssignableRoles(),
    staleTime: 60_000,
  });
  const setMut = useSetCategoryRoles();
  const { data: overrides, isLoading: overridesLoading } = useReportOverrides();
  const { data: reports } = useReports();
  const upsertOverride = useUpsertReportOverride();
  const deleteOverride = useDeleteReportOverride();

  const [overrideDialogOpen, setOverrideDialogOpen] = useState(false);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  const orderedRoles = useMemo(() => {
    return (roles ?? []).filter((r) => r.name !== 'SystemAdministrator');
  }, [roles]);

  const toggleCell = async (categoryId: number, roleId: number, currentlyAllowed: boolean) => {
    if (!canManage) return;
    const row = matrix?.find((r) => r.categoryId === categoryId);
    if (!row) return;
    const newRoleIds = currentlyAllowed
      ? row.allowedRoleIds.filter((id) => id !== roleId)
      : [...row.allowedRoleIds, roleId];
    try {
      await setMut.mutateAsync({ categoryId, roleIds: newRoleIds });
    } catch (err) {
      console.error(err);
    }
  };

  return (
    <div className="space-y-6">
      <Card>
        <CardHeader>
          <CardTitle>Kategori × Rol Matrisi</CardTitle>
          <CardDescription>
            Kategoriye işaretlenen rol, o kategorinin tüm raporlarına erişebilir. Per-rapor istisnalar
            aşağıdaki override listesinden yönetilir.
          </CardDescription>
        </CardHeader>
        <CardContent>
          {matrixLoading ? (
            <div className="flex items-center gap-2 py-8 justify-center text-muted-foreground">
              <Loader2 className="h-4 w-4 animate-spin" />Yükleniyor...
            </div>
          ) : !matrix || matrix.length === 0 ? (
            <div className="py-12 text-center text-muted-foreground text-sm">Önce kategori oluştur.</div>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full text-sm border-collapse">
                <thead>
                  <tr>
                    <th className="px-3 py-2 text-left font-medium bg-muted/40 sticky left-0">Kategori</th>
                    {orderedRoles.map((r) => (
                      <th key={r.id} className="px-3 py-2 text-center font-medium bg-muted/40 whitespace-nowrap">
                        {getRoleDisplayLabel(r.name)}
                      </th>
                    ))}
                  </tr>
                </thead>
                <tbody className="divide-y">
                  {matrix.map((row) => (
                    <tr key={row.categoryId} className="hover:bg-muted/20">
                      <td className="px-3 py-2 font-medium sticky left-0 bg-background">{row.categoryName}</td>
                      {orderedRoles.map((r) => {
                        const checked = row.allowedRoleIds.includes(r.id);
                        return (
                          <td key={r.id} className="px-3 py-2 text-center">
                            <button
                              type="button"
                              disabled={!canManage || setMut.isPending}
                              onClick={() => toggleCell(row.categoryId, r.id, checked)}
                              className={`inline-flex h-6 w-6 items-center justify-center rounded border transition-colors ${
                                checked
                                  ? 'border-primary bg-primary text-primary-foreground'
                                  : 'border-input hover:border-primary/50'
                              } ${!canManage ? 'cursor-default opacity-60' : 'cursor-pointer'}`}
                            >
                              {checked && <Check className="h-4 w-4" />}
                            </button>
                          </td>
                        );
                      })}
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader className="flex flex-row items-center justify-between">
          <div>
            <CardTitle>Per-Rapor Override Listesi</CardTitle>
            <CardDescription>
              Belirli bir rapor için belirli bir role allow veya deny istisnası. Deny, allow'dan baskındır.
            </CardDescription>
          </div>
          {canManage && (
            <Button
              onClick={() => { setErrorMsg(null); setOverrideDialogOpen(true); }}
              disabled={!reports?.length || !orderedRoles.length}
            >
              <Plus className="h-4 w-4 mr-2" />
              Yeni Override
            </Button>
          )}
        </CardHeader>
        <CardContent>
          {overridesLoading ? (
            <div className="flex items-center gap-2 py-8 justify-center text-muted-foreground">
              <Loader2 className="h-4 w-4 animate-spin" />Yükleniyor...
            </div>
          ) : !overrides || overrides.length === 0 ? (
            <div className="py-8 text-center text-muted-foreground text-sm">Override yok.</div>
          ) : (
            <table className="w-full text-sm">
              <thead className="bg-muted/40 text-xs uppercase text-muted-foreground">
                <tr>
                  <th className="px-3 py-2 text-left font-medium">Rapor</th>
                  <th className="px-3 py-2 text-left font-medium">Rol</th>
                  <th className="px-3 py-2 text-left font-medium">Tür</th>
                  <th className="px-3 py-2 text-left font-medium">Sebep</th>
                  <th className="px-3 py-2 text-right font-medium">İşlem</th>
                </tr>
              </thead>
              <tbody className="divide-y">
                {overrides.map((o: ReportPermissionOverrideDto) => (
                  <tr key={o.id} className="hover:bg-muted/30">
                    <td className="px-3 py-2">
                      <div className="font-medium">{o.reportName}</div>
                      <div className="text-xs text-muted-foreground font-mono">{o.reportCode}</div>
                    </td>
                    <td className="px-3 py-2">{getRoleDisplayLabel(o.roleName)}</td>
                    <td className="px-3 py-2">
                      {o.isAllowed ? (
                        <Badge className="bg-emerald-100 text-emerald-700 hover:bg-emerald-200">ALLOW</Badge>
                      ) : (
                        <Badge variant="destructive">DENY</Badge>
                      )}
                    </td>
                    <td className="px-3 py-2 max-w-xs">
                      <span className="line-clamp-1 text-muted-foreground" title={o.reason ?? ''}>
                        {o.reason ?? '—'}
                      </span>
                    </td>
                    <td className="px-3 py-2 text-right">
                      {canManage && (
                        <Button
                          variant="ghost" size="sm"
                          className="text-destructive"
                          onClick={() => deleteOverride.mutate(o.id)}
                        >
                          <X className="h-4 w-4" />
                        </Button>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </CardContent>
      </Card>

      <OverrideFormDialog
        open={overrideDialogOpen}
        onOpenChange={setOverrideDialogOpen}
        reports={reports ?? []}
        roles={orderedRoles}
        errorMsg={errorMsg}
        isPending={upsertOverride.isPending}
        onSubmit={async (v) => {
          setErrorMsg(null);
          try {
            await upsertOverride.mutateAsync(v);
            setOverrideDialogOpen(false);
          } catch (err) {
            setErrorMsg(extractError(err, 'İşlem başarısız.'));
          }
        }}
      />
    </div>
  );
}

function OverrideFormDialog({
  open, onOpenChange, reports, roles, onSubmit, isPending, errorMsg,
}: {
  open: boolean;
  onOpenChange: (o: boolean) => void;
  reports: ReportDefinitionDto[];
  roles: RoleDto[];
  onSubmit: (v: { reportId: number; roleId: number; isAllowed: boolean; reason: string | null }) => Promise<void>;
  isPending: boolean;
  errorMsg: string | null;
}) {
  const [reportId, setReportId] = useState<string>('');
  const [roleId, setRoleId] = useState<string>('');
  const [isAllowed, setIsAllowed] = useState(true);
  const [reason, setReason] = useState('');

  const handleOpen = (o: boolean) => {
    if (o) {
      setReportId('');
      setRoleId('');
      setIsAllowed(true);
      setReason('');
    }
    onOpenChange(o);
  };

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!reportId || !roleId) return;
    await onSubmit({
      reportId: Number(reportId),
      roleId: Number(roleId),
      isAllowed,
      reason: reason.trim() || null,
    });
  };

  return (
    <Dialog open={open} onOpenChange={handleOpen}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Yeni Override</DialogTitle>
          <DialogDescription>
            Aynı (rapor, rol) için mevcut override varsa üzerine yazılır (upsert).
          </DialogDescription>
        </DialogHeader>
        <form onSubmit={submit} className="space-y-4">
          <div className="space-y-2">
            <Label>Rapor</Label>
            <Select value={reportId} onValueChange={setReportId}>
              <SelectTrigger><SelectValue placeholder="Rapor seç" /></SelectTrigger>
              <SelectContent>
                {reports.map((r) => (
                  <SelectItem key={r.id} value={String(r.id)}>{r.name} <span className="text-muted-foreground">— {r.categoryName}</span></SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-2">
            <Label>Rol</Label>
            <Select value={roleId} onValueChange={setRoleId}>
              <SelectTrigger><SelectValue placeholder="Rol seç" /></SelectTrigger>
              <SelectContent>
                {roles.map((r) => (
                  <SelectItem key={r.id} value={String(r.id)}>{getRoleDisplayLabel(r.name)}</SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="flex items-center justify-between rounded-md border px-3 py-2">
            <div>
              <Label htmlFor="is-allowed">İzin Türü</Label>
              <p className="text-xs text-muted-foreground">
                {isAllowed ? 'ALLOW — kategori kapalı olsa bile bu raporu aç' : 'DENY — kategori açık olsa bile bu raporu kapat'}
              </p>
            </div>
            <Switch id="is-allowed" checked={isAllowed} onCheckedChange={setIsAllowed} />
          </div>
          <div className="space-y-2">
            <Label htmlFor="ovr-reason">Sebep (opsiyonel)</Label>
            <Textarea
              id="ovr-reason"
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              rows={2}
              placeholder="Neden bu override gerekiyor?"
            />
          </div>
          {errorMsg && <Alert variant="destructive"><AlertDescription>{errorMsg}</AlertDescription></Alert>}
          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)} disabled={isPending}>
              İptal
            </Button>
            <Button type="submit" disabled={isPending || !reportId || !roleId}>
              {isPending && <Loader2 className="h-4 w-4 mr-2 animate-spin" />}
              Kaydet
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
