import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import {
  FileBarChart2,
  FolderTree,
  Loader2,
  Pencil,
  Plus,
  ShieldCheck,
  Trash2,
  Users,
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
import { useToast } from '@/components/ui/global-toast';
import { getAssignableRoles } from '@/api/permissionAdmin.api';
import { UserSearchCombobox } from '@/components/audit-log/UserSearchCombobox';
import { usePermissions } from '../context/PermissionContext';
import {
  useAddGroupMember,
  useCreateReport,
  useCreateReportCategory,
  useCreateReportUserGroup,
  useDeleteReport,
  useDeleteReportCategory,
  useDeleteReportGrant,
  useDeleteReportUserGroup,
  useReportCategories,
  useReportGrants,
  useReportGroupMembers,
  useReports,
  useReportUserGroups,
  useRemoveGroupMember,
  useSetReportGrant,
  useUpdateReport,
  useUpdateReportCategory,
} from '../hooks/useReportAuthorization';
import { getRoleDisplayLabel } from '../lib/roles';
import type {
  ReportCategoryDto,
  ReportDefinitionDto,
  SetReportAccessGrantDto,
} from '@/api/reportAuthorization.api';

// DB kolon limitleriyle aynı (backend ValidateCode/ValidateName ile hizalı).
const FIELD_LIMITS = {
  categoryCode: 64,
  reportCode: 100,
  name: 200,
  description: 500,
  reason: 500,
} as const;

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
          <TabsTrigger value="groups">
            <Users className="h-4 w-4 mr-2" />
            Kullanıcı Grupları
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
        <TabsContent value="groups">
          <GroupsTab canManage={canManage} />
        </TabsContent>
      </Tabs>
    </div>
  );
}

// ════════════════════════════════════════════════════════════
// Categories Tab
// ════════════════════════════════════════════════════════════

function CategoriesTab({ canManage }: { canManage: boolean }) {
  const { showSuccess, showError } = useToast();
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
                if (!deleteTarget) return;
                try {
                  await deleteMut.mutateAsync(deleteTarget.id);
                  showSuccess('Kategori silindi', deleteTarget.name);
                  setDeleteTarget(null);
                } catch (err) {
                  showError('Kategori silinemedi', extractError(err, 'İşlem başarısız.'));
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
              maxLength={FIELD_LIMITS.categoryCode}
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
              maxLength={FIELD_LIMITS.name}
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
  const { showSuccess, showError } = useToast();
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
                if (!deleteTarget) return;
                try {
                  await deleteMut.mutateAsync(deleteTarget.id);
                  showSuccess('Rapor silindi', deleteTarget.name);
                  setDeleteTarget(null);
                } catch (err) {
                  showError('Rapor silinemedi', extractError(err, 'İşlem başarısız.'));
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
              maxLength={FIELD_LIMITS.reportCode}
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
              maxLength={FIELD_LIMITS.name}
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
              maxLength={FIELD_LIMITS.description}
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
// Permissions Tab (grant editor: rol/kişi/grup × kategori/rapor × izin/yasak)
// ════════════════════════════════════════════════════════════

const SCOPE_LABELS = ['Kategori', 'Rapor'];
const SUBJECT_LABELS = ['Rol', 'Kişi', 'Grup'];

function PermissionsTab({ canManage }: { canManage: boolean }) {
  const { showSuccess, showError } = useToast();
  const { data: grants, isLoading } = useReportGrants();
  const setMut = useSetReportGrant();
  const deleteMut = useDeleteReportGrant();
  const [dialogOpen, setDialogOpen] = useState(false);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between">
        <div>
          <CardTitle>Yetkiler</CardTitle>
          <CardDescription>
            Rol, kişi veya gruba; kategori veya rapor için İzin/Yasak. Yasak her zaman izinden baskındır;
            rapor seviyesi kategori seviyesinden baskındır.
          </CardDescription>
        </div>
        {canManage && (
          <Button onClick={() => { setErrorMsg(null); setDialogOpen(true); }}>
            <Plus className="h-4 w-4 mr-2" />Yeni Yetki
          </Button>
        )}
      </CardHeader>
      <CardContent>
        {isLoading ? (
          <div className="flex items-center gap-2 py-8 justify-center text-muted-foreground">
            <Loader2 className="h-4 w-4 animate-spin" />Yükleniyor...
          </div>
        ) : !grants || grants.length === 0 ? (
          <div className="py-12 text-center text-muted-foreground text-sm">Henüz yetki tanımı yok.</div>
        ) : (
          <table className="w-full text-sm">
            <thead className="bg-muted/40 text-xs uppercase text-muted-foreground">
              <tr>
                <th className="px-3 py-2 text-left font-medium">Özne</th>
                <th className="px-3 py-2 text-left font-medium">Hedef</th>
                <th className="px-3 py-2 text-left font-medium">Tür</th>
                <th className="px-3 py-2 text-right font-medium">İşlem</th>
              </tr>
            </thead>
            <tbody className="divide-y">
              {grants.map((g) => (
                <tr key={g.id} className="hover:bg-muted/30">
                  <td className="px-3 py-2">
                    <div className="font-medium">{g.subjectLabel}</div>
                    <div className="text-xs text-muted-foreground">{SUBJECT_LABELS[g.subjectKind]}</div>
                  </td>
                  <td className="px-3 py-2">
                    <div className="font-medium">{g.targetLabel}</div>
                    <div className="text-xs text-muted-foreground">{SCOPE_LABELS[g.scopeKind]}</div>
                  </td>
                  <td className="px-3 py-2">
                    {g.effect === 0
                      ? <Badge className="bg-emerald-100 text-emerald-700 hover:bg-emerald-200">İZİN</Badge>
                      : <Badge variant="destructive">YASAK</Badge>}
                  </td>
                  <td className="px-3 py-2 text-right">
                    {canManage && (
                      <Button variant="ghost" size="sm" className="text-destructive"
                        onClick={async () => {
                          try { await deleteMut.mutateAsync(g.id); showSuccess('Yetki silindi'); }
                          catch (err) { showError('Silinemedi', extractError(err, 'İşlem başarısız.')); }
                        }}>
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

      <GrantFormDialog
        open={dialogOpen}
        onOpenChange={setDialogOpen}
        errorMsg={errorMsg}
        isPending={setMut.isPending}
        onSubmit={async (dto) => {
          setErrorMsg(null);
          try { await setMut.mutateAsync(dto); showSuccess('Yetki kaydedildi'); setDialogOpen(false); }
          catch (err) { setErrorMsg(extractError(err, 'İşlem başarısız.')); }
        }}
      />
    </Card>
  );
}

function GrantFormDialog({
  open, onOpenChange, onSubmit, isPending, errorMsg,
}: {
  open: boolean;
  onOpenChange: (o: boolean) => void;
  onSubmit: (dto: SetReportAccessGrantDto) => Promise<void>;
  isPending: boolean;
  errorMsg: string | null;
}) {
  const { data: categories } = useReportCategories();
  const { data: reports } = useReports();
  const { data: groups } = useReportUserGroups();
  const { data: roles } = useQuery({ queryKey: ['admin', 'roles'], queryFn: () => getAssignableRoles(), staleTime: 60_000 });

  const [scopeKind, setScopeKind] = useState('0');
  const [targetId, setTargetId] = useState('');
  const [subjectKind, setSubjectKind] = useState('0');
  const [subjectId, setSubjectId] = useState('');
  const [effect, setEffect] = useState('0');

  const handleOpen = (o: boolean) => {
    if (o) { setScopeKind('0'); setTargetId(''); setSubjectKind('0'); setSubjectId(''); setEffect('0'); }
    onOpenChange(o);
  };

  const orderedRoles = (roles ?? []).filter((r) => r.name !== 'SystemAdministrator');

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!targetId || !subjectId) return;
    await onSubmit({
      scopeKind: Number(scopeKind), targetId: Number(targetId),
      subjectKind: Number(subjectKind), subjectId: Number(subjectId),
      effect: Number(effect), reason: null,
    });
  };

  return (
    <Dialog open={open} onOpenChange={handleOpen}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Yeni Yetki</DialogTitle>
          <DialogDescription>Aynı (hedef, özne) için mevcut yetki üzerine yazılır.</DialogDescription>
        </DialogHeader>
        <form onSubmit={submit} className="space-y-4">
          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-2">
              <Label>Özne Tipi</Label>
              <Select value={subjectKind} onValueChange={(v) => { setSubjectKind(v); setSubjectId(''); }}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>
                  <SelectItem value="0">Rol</SelectItem>
                  <SelectItem value="1">Kişi</SelectItem>
                  <SelectItem value="2">Grup</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label>Özne</Label>
              {subjectKind === '1' ? (
                <UserSearchCombobox value={subjectId} onChange={setSubjectId} />
              ) : (
                <Select value={subjectId} onValueChange={setSubjectId}>
                  <SelectTrigger><SelectValue placeholder="Seç" /></SelectTrigger>
                  <SelectContent>
                    {subjectKind === '0'
                      ? orderedRoles.map((r) => <SelectItem key={r.id} value={String(r.id)}>{getRoleDisplayLabel(r.name)}</SelectItem>)
                      : (groups ?? []).map((g) => <SelectItem key={g.id} value={String(g.id)}>{g.name}</SelectItem>)}
                  </SelectContent>
                </Select>
              )}
            </div>
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-2">
              <Label>Hedef Tipi</Label>
              <Select value={scopeKind} onValueChange={(v) => { setScopeKind(v); setTargetId(''); }}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>
                  <SelectItem value="0">Kategori</SelectItem>
                  <SelectItem value="1">Rapor</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label>Hedef</Label>
              <Select value={targetId} onValueChange={setTargetId}>
                <SelectTrigger><SelectValue placeholder="Seç" /></SelectTrigger>
                <SelectContent>
                  {scopeKind === '0'
                    ? (categories ?? []).map((c) => <SelectItem key={c.id} value={String(c.id)}>{c.name}</SelectItem>)
                    : (reports ?? []).map((r) => <SelectItem key={r.id} value={String(r.id)}>{r.name}</SelectItem>)}
                </SelectContent>
              </Select>
            </div>
          </div>

          <div className="space-y-2">
            <Label>İzin Türü</Label>
            <Select value={effect} onValueChange={setEffect}>
              <SelectTrigger><SelectValue /></SelectTrigger>
              <SelectContent>
                <SelectItem value="0">İzin (Allow)</SelectItem>
                <SelectItem value="1">Yasak (Deny)</SelectItem>
              </SelectContent>
            </Select>
          </div>

          {errorMsg && <Alert variant="destructive"><AlertDescription>{errorMsg}</AlertDescription></Alert>}
          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)} disabled={isPending}>İptal</Button>
            <Button type="submit" disabled={isPending || !targetId || !subjectId}>
              {isPending && <Loader2 className="h-4 w-4 mr-2 animate-spin" />}Kaydet
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}

// ════════════════════════════════════════════════════════════
// Kullanıcı Grupları Tab
// ════════════════════════════════════════════════════════════

function GroupsTab({ canManage }: { canManage: boolean }) {
  const { showSuccess, showError } = useToast();
  const { data: groups, isLoading } = useReportUserGroups();
  const createMut = useCreateReportUserGroup();
  const deleteMut = useDeleteReportUserGroup();
  const addMember = useAddGroupMember();
  const removeMember = useRemoveGroupMember();

  const [newName, setNewName] = useState('');
  const [openGroupId, setOpenGroupId] = useState<number | null>(null);
  const { data: members } = useReportGroupMembers(openGroupId);
  const [memberUserId, setMemberUserId] = useState('');

  return (
    <Card>
      <CardHeader>
        <CardTitle>Kullanıcı Grupları</CardTitle>
        <CardDescription>Rapor yetkisi verilebilen adlandırılmış kullanıcı listeleri (örn. "Fakülte Sekreterleri").</CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        {canManage && (
          <div className="flex gap-2">
            <Input placeholder="Yeni grup adı" value={newName} onChange={(e) => setNewName(e.target.value)} maxLength={FIELD_LIMITS.name} className="max-w-xs" />
            <Button
              disabled={!newName.trim() || createMut.isPending}
              onClick={async () => {
                try { await createMut.mutateAsync({ name: newName.trim() }); setNewName(''); showSuccess('Grup oluşturuldu'); }
                catch (err) { showError('Oluşturulamadı', extractError(err, 'İşlem başarısız.')); }
              }}>
              <Plus className="h-4 w-4 mr-2" />Ekle
            </Button>
          </div>
        )}

        {isLoading ? (
          <div className="flex items-center gap-2 py-8 justify-center text-muted-foreground"><Loader2 className="h-4 w-4 animate-spin" />Yükleniyor...</div>
        ) : !groups || groups.length === 0 ? (
          <div className="py-12 text-center text-muted-foreground text-sm">Grup yok.</div>
        ) : (
          <div className="space-y-2">
            {groups.map((g) => (
              <div key={g.id} className="rounded-md border">
                <div className="flex items-center justify-between px-3 py-2">
                  <button type="button" className="flex items-center gap-2 text-left"
                    onClick={() => setOpenGroupId(openGroupId === g.id ? null : g.id)}>
                    <span className="font-medium">{g.name}</span>
                    <Badge variant="secondary">{g.memberCount} üye</Badge>
                  </button>
                  {canManage && (
                    <Button variant="ghost" size="sm" className="text-destructive"
                      onClick={async () => {
                        try { await deleteMut.mutateAsync(g.id); showSuccess('Grup silindi'); if (openGroupId === g.id) setOpenGroupId(null); }
                        catch (err) { showError('Silinemedi', extractError(err, 'İşlem başarısız.')); }
                      }}>
                      <Trash2 className="h-4 w-4" />
                    </Button>
                  )}
                </div>

                {openGroupId === g.id && (
                  <div className="border-t px-3 py-3 space-y-3 bg-muted/20">
                    {canManage && (
                      <div className="flex items-end gap-2">
                        <div className="space-y-1">
                          <Label className="text-xs">Üye ekle</Label>
                          <UserSearchCombobox value={memberUserId} onChange={setMemberUserId} />
                        </div>
                        <Button size="sm" disabled={!memberUserId || addMember.isPending}
                          onClick={async () => {
                            try { await addMember.mutateAsync({ groupId: g.id, userId: Number(memberUserId) }); setMemberUserId(''); showSuccess('Üye eklendi'); }
                            catch (err) { showError('Eklenemedi', extractError(err, 'İşlem başarısız.')); }
                          }}>Ekle</Button>
                      </div>
                    )}
                    {!members || members.length === 0 ? (
                      <p className="text-sm text-muted-foreground">Üye yok.</p>
                    ) : (
                      <ul className="divide-y rounded-md border bg-background">
                        {members.map((m) => (
                          <li key={m.userId} className="flex items-center justify-between px-3 py-2 text-sm">
                            <span>{m.fullName} <span className="text-muted-foreground text-xs">{m.email}</span></span>
                            {canManage && (
                              <Button variant="ghost" size="sm" className="text-destructive"
                                onClick={async () => {
                                  try { await removeMember.mutateAsync({ groupId: g.id, userId: m.userId }); showSuccess('Üye çıkarıldı'); }
                                  catch (err) { showError('Çıkarılamadı', extractError(err, 'İşlem başarısız.')); }
                                }}>
                                <X className="h-4 w-4" />
                              </Button>
                            )}
                          </li>
                        ))}
                      </ul>
                    )}
                  </div>
                )}
              </div>
            ))}
          </div>
        )}
      </CardContent>
    </Card>
  );
}
