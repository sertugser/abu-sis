import { useEffect, useState } from 'react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Badge } from '@/components/ui/badge';
import {
  Dialog, DialogHeader, DialogTitle, DialogDescription,
} from '@/components/ui/dialog';
import ResizableDialogContent from '@/components/ui/resizable-dialog';
import { Switch } from '@/components/ui/switch';
import { Textarea } from '@/components/ui/textarea';
import {
  AlertDialog, AlertDialogAction, AlertDialogCancel, AlertDialogContent,
  AlertDialogDescription, AlertDialogFooter, AlertDialogHeader, AlertDialogTitle,
} from '@/components/ui/alert-dialog';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { AlertCircle, Loader2, Plus, Trash2, Save, BookOpen } from 'lucide-react';
import {
  useElectiveGroupDetail,
  useUpdateElectiveGroup,
  useDeleteElectiveGroup,
  useRemoveCourseFromGroup,
} from '../../hooks/useElectiveGroups';
import { ELECTIVE_SUBTYPE_LABELS } from '../../types/electiveGroup';
import type { UpdateElectiveGroupRequest } from '../../types/electiveGroup';
import AddCourseToGroupDialog from './AddCourseToGroupDialog';

interface Props {
  groupId: number | null;
  onClose: () => void;
}

export default function ElectiveGroupDetailSheet({ groupId, onClose }: Props) {
  const { data: group, isLoading, error } = useElectiveGroupDetail(groupId);
  const updateMutation = useUpdateElectiveGroup();
  const deleteMutation = useDeleteElectiveGroup();
  const removeMutation = useRemoveCourseFromGroup();

  const [name, setName] = useState('');
  const [code, setCode] = useState('');
  const [description, setDescription] = useState('');
  const [isActive, setIsActive] = useState(true);
  const [addDialogOpen, setAddDialogOpen] = useState(false);
  const [courseToRemove, setCourseToRemove] = useState<{ id: number; code: string } | null>(null);
  const [deleteGroupConfirm, setDeleteGroupConfirm] = useState(false);

  useEffect(() => {
    if (group) {
      setName(group.name);
      setCode(group.code);
      setDescription(group.description ?? '');
      setIsActive(group.isActive);
    }
  }, [group]);

  function handleSave() {
    if (!groupId) return;
    const req: UpdateElectiveGroupRequest = {
      name: name.trim() || undefined,
      code: code.trim() || undefined,
      description: description.trim() || undefined,
      isActive,
    };
    updateMutation.mutate({ id: groupId, ...req });
  }

  function handleConfirmRemoveCourse() {
    if (!groupId || !courseToRemove) return;
    removeMutation.mutate(
      { groupId, courseId: courseToRemove.id },
      { onSuccess: () => setCourseToRemove(null) },
    );
  }

  function handleDeleteGroup() {
    if (!groupId) return;
    deleteMutation.mutate(groupId, {
      onSuccess: () => {
        setDeleteGroupConfirm(false);
        onClose();
      },
    });
  }

  const existingCourseIds = group?.courses.map(c => c.courseId) ?? [];
  const totalEcts = group?.courses.reduce((sum, c) => sum + c.ects, 0) ?? 0;

  return (
    <>
      <Dialog open={groupId !== null} onOpenChange={open => { if (!open) onClose(); }}>
        <ResizableDialogContent defaultWidth={1100} defaultHeight={560} minWidth={700} minHeight={400} className="flex flex-col">
          {/* Header */}
          <DialogHeader>
            <div className="flex items-center gap-3">
              <DialogTitle className="text-lg">{group?.name ?? 'Grup Detayı'}</DialogTitle>
              {group && (
                <Badge variant="outline" className="text-[10px] font-medium bg-blue-50 text-blue-700 border-blue-200">
                  {ELECTIVE_SUBTYPE_LABELS[group.electiveSubtype]}
                </Badge>
              )}
              {group && (
                <Badge variant={group.isActive ? 'default' : 'secondary'} className={group.isActive ? 'bg-emerald-50 text-emerald-700 border-emerald-200' : 'bg-red-50 text-red-600 border-red-200'}>
                  {group.isActive ? 'Aktif' : 'Pasif'}
                </Badge>
              )}
            </div>
            <DialogDescription>
              {group ? `${group.code} · ${group.courses.length} ders · ${totalEcts} AKTS` : ''}
            </DialogDescription>
          </DialogHeader>

          {isLoading && (
            <div className="flex items-center justify-center py-16">
              <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
            </div>
          )}

          {error && (
            <Alert variant="destructive">
              <AlertCircle className="h-4 w-4" />
              <AlertDescription>Grup bilgileri yüklenemedi.</AlertDescription>
            </Alert>
          )}

          {group && (
            <div className="flex-1 overflow-y-auto">
              <div className="grid grid-cols-[320px_1fr] gap-6">
                {/* Left: Form */}
                <div className="space-y-4 border-r pr-6">
                  <div>
                    <Label htmlFor="eg-name" className="text-xs text-muted-foreground">Grup Adı</Label>
                    <Input id="eg-name" value={name} onChange={e => setName(e.target.value)} className="h-9" />
                  </div>
                  <div>
                    <Label htmlFor="eg-code" className="text-xs text-muted-foreground">Kod</Label>
                    <Input id="eg-code" value={code} onChange={e => setCode(e.target.value.toUpperCase())} className="h-9" />
                  </div>
                  <div>
                    <Label htmlFor="eg-desc" className="text-xs text-muted-foreground">Açıklama</Label>
                    <Textarea id="eg-desc" value={description} onChange={e => setDescription(e.target.value)} rows={2} />
                  </div>
                  <div className="flex items-center gap-3">
                    <Switch id="eg-active" checked={isActive} onCheckedChange={setIsActive} />
                    <Label htmlFor="eg-active" className="text-sm">
                      {isActive ? 'Aktif' : 'Pasif'}
                    </Label>
                  </div>

                  <Button onClick={handleSave} disabled={updateMutation.isPending} size="sm" className="w-full">
                    {updateMutation.isPending ? <Loader2 className="mr-2 h-3.5 w-3.5 animate-spin" /> : <Save className="mr-2 h-3.5 w-3.5" />}
                    Kaydet
                  </Button>
                  {updateMutation.isSuccess && <p className="text-xs text-emerald-600 text-center">Kaydedildi.</p>}
                  {updateMutation.isError && <p className="text-xs text-destructive text-center">Hata oluştu.</p>}

                  <div className="pt-2 border-t">
                    <Button
                      variant="ghost"
                      size="sm"
                      className="w-full text-xs text-destructive hover:text-destructive hover:bg-destructive/10"
                      onClick={() => setDeleteGroupConfirm(true)}
                    >
                      <Trash2 className="h-3 w-3 mr-1.5" />
                      Grubu Sil
                    </Button>
                  </div>
                </div>

                {/* Right: Courses */}
                <div className="space-y-3">
                  <div className="flex items-center justify-between">
                    <div className="flex items-center gap-2">
                      <BookOpen className="h-4 w-4 text-muted-foreground" />
                      <h4 className="text-sm font-semibold">Dersler</h4>
                      <Badge variant="secondary" className="text-[10px]">{group.courses.length}</Badge>
                    </div>
                    <Button size="sm" variant="outline" className="h-8 text-xs" onClick={() => setAddDialogOpen(true)}>
                      <Plus className="h-3.5 w-3.5 mr-1" />
                      Ders Ekle
                    </Button>
                  </div>

                  {group.courses.length === 0 ? (
                    <div className="text-center py-12 border rounded-lg bg-muted/20">
                      <BookOpen className="h-8 w-8 text-muted-foreground/30 mx-auto mb-2" />
                      <p className="text-sm text-muted-foreground">Henüz ders eklenmedi.</p>
                      <p className="text-xs text-muted-foreground mt-1">Yukarıdaki "Ders Ekle" butonunu kullanın.</p>
                    </div>
                  ) : (
                    <div className="border rounded-lg overflow-hidden">
                      <div className="overflow-auto max-h-[400px]">
                        <table className="w-full">
                          <thead className="bg-muted/30 sticky top-0">
                            <tr>
                              <th className="text-left px-3 py-2 text-xs font-semibold text-muted-foreground w-24">Kod</th>
                              <th className="text-left px-3 py-2 text-xs font-semibold text-muted-foreground">Ders Adı</th>
                              <th className="text-center px-3 py-2 text-xs font-semibold text-muted-foreground w-16">AKTS</th>
                              <th className="w-10" />
                            </tr>
                          </thead>
                          <tbody className="divide-y divide-border">
                            {group.courses.map(c => (
                              <tr key={c.courseId} className="hover:bg-muted/10 transition-colors">
                                <td className="px-3 py-2 font-mono text-xs font-medium">{c.courseCode}</td>
                                <td className="px-3 py-2 text-sm">{c.courseName}</td>
                                <td className="px-3 py-2 text-center text-sm">{c.ects}</td>
                                <td className="px-1 py-2">
                                  <Button
                                    size="icon"
                                    variant="ghost"
                                    className="h-6 w-6 text-muted-foreground hover:text-destructive"
                                    onClick={() => setCourseToRemove({ id: c.courseId, code: c.courseCode })}
                                    disabled={removeMutation.isPending}
                                    aria-label={`${c.courseCode} dersini gruptan çıkar`}
                                  >
                                    <Trash2 className="h-3 w-3" />
                                  </Button>
                                </td>
                              </tr>
                            ))}
                          </tbody>
                        </table>
                      </div>
                      <div className="px-3 py-2 border-t bg-muted/20 flex items-center justify-between">
                        <span className="text-xs text-muted-foreground">
                          {group.courses.length} ders
                        </span>
                        <span className="text-xs text-muted-foreground">
                          Toplam <span className="font-medium text-foreground">{totalEcts}</span> AKTS
                        </span>
                      </div>
                    </div>
                  )}
                </div>
              </div>
            </div>
          )}
        </ResizableDialogContent>
      </Dialog>

      {groupId !== null && (
        <AddCourseToGroupDialog
          groupId={groupId}
          existingCourseIds={existingCourseIds}
          open={addDialogOpen}
          onOpenChange={setAddDialogOpen}
        />
      )}

      {/* Remove Course Confirmation */}
      <AlertDialog open={courseToRemove !== null} onOpenChange={open => { if (!open) setCourseToRemove(null); }}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Dersi gruptan çıkar</AlertDialogTitle>
            <AlertDialogDescription>
              <strong>{courseToRemove?.code}</strong> kodlu dersi bu gruptan çıkarmak istediğinize emin misiniz?
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>İptal</AlertDialogCancel>
            <AlertDialogAction
              onClick={handleConfirmRemoveCourse}
              disabled={removeMutation.isPending}
              className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
            >
              {removeMutation.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Çıkar
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      {/* Delete Group Confirmation */}
      <AlertDialog open={deleteGroupConfirm} onOpenChange={setDeleteGroupConfirm}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Grubu sil</AlertDialogTitle>
            <AlertDialogDescription>
              <strong>{group?.name}</strong> grubunu silmek istediğinize emin misiniz? Bu işlem grubu pasif hale getirecektir.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>İptal</AlertDialogCancel>
            <AlertDialogAction
              onClick={handleDeleteGroup}
              disabled={deleteMutation.isPending}
              className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
            >
              {deleteMutation.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Sil
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </>
  );
}
