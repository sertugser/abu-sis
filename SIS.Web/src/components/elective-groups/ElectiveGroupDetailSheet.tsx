import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Badge } from '@/components/ui/badge';
import {
  Sheet, SheetContent, SheetHeader, SheetTitle, SheetDescription,
} from '@/components/ui/sheet';
import { Switch } from '@/components/ui/switch';
import { Textarea } from '@/components/ui/textarea';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { AlertCircle, Loader2, Plus, Trash2 } from 'lucide-react';
import {
  useElectiveGroupDetail,
  useUpdateElectiveGroup,
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
  const removeMutation = useRemoveCourseFromGroup();

  const [name, setName] = useState(group?.name ?? '');
  const [code, setCode] = useState(group?.code ?? '');
  const [description, setDescription] = useState(group?.description ?? '');
  const [isActive, setIsActive] = useState(group?.isActive ?? true);
  const [addDialogOpen, setAddDialogOpen] = useState(false);

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

  function handleRemoveCourse(courseId: number) {
    if (!groupId) return;
    removeMutation.mutate({ groupId, courseId });
  }

  const existingCourseIds = group?.courses.map(c => c.courseId) ?? [];

  return (
    <>
      <Sheet open={groupId !== null} onOpenChange={open => { if (!open) onClose(); }}>
        <SheetContent className="w-[520px] sm:max-w-[520px] overflow-y-auto">
          <SheetHeader>
            <SheetTitle>Grup Detayi</SheetTitle>
            <SheetDescription>
              {group ? ELECTIVE_SUBTYPE_LABELS[group.electiveSubtype] : ''}
            </SheetDescription>
          </SheetHeader>

          {isLoading && (
            <div className="flex items-center justify-center py-12">
              <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
            </div>
          )}

          {error && (
            <Alert variant="destructive" className="mt-4">
              <AlertCircle className="h-4 w-4" />
              <AlertDescription>Grup bilgileri yuklenemedi.</AlertDescription>
            </Alert>
          )}

          {group && (
            <div className="mt-6 space-y-6">
              {/* ── Form ── */}
              <div className="space-y-4">
                <div>
                  <Label htmlFor="eg-name">Grup Adi</Label>
                  <Input id="eg-name" value={name} onChange={e => setName(e.target.value)} />
                </div>
                <div>
                  <Label htmlFor="eg-code">Kod</Label>
                  <Input id="eg-code" value={code} onChange={e => setCode(e.target.value.toUpperCase())} />
                </div>
                <div>
                  <Label htmlFor="eg-desc">Aciklama</Label>
                  <Textarea id="eg-desc" value={description} onChange={e => setDescription(e.target.value)} rows={3} />
                </div>
                <div className="flex items-center gap-3">
                  <Switch id="eg-active" checked={isActive} onCheckedChange={setIsActive} />
                  <Label htmlFor="eg-active">
                    {isActive ? 'Aktif' : 'Pasif'}
                  </Label>
                </div>
                <Button
                  onClick={handleSave}
                  disabled={updateMutation.isPending}
                  className="w-full"
                >
                  {updateMutation.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                  Kaydet
                </Button>
                {updateMutation.isSuccess && (
                  <p className="text-xs text-emerald-600">Kaydedildi.</p>
                )}
                {updateMutation.isError && (
                  <p className="text-xs text-destructive">Hata olustu.</p>
                )}
              </div>

              {/* ── Courses ── */}
              <div>
                <div className="flex items-center justify-between mb-2">
                  <h4 className="text-sm font-semibold">
                    Dersler
                    <Badge variant="secondary" className="ml-2 text-[10px]">{group.courses.length}</Badge>
                  </h4>
                  <Button size="sm" variant="outline" onClick={() => setAddDialogOpen(true)}>
                    <Plus className="h-3.5 w-3.5 mr-1" />
                    Ders Ekle
                  </Button>
                </div>

                {group.courses.length === 0 ? (
                  <p className="text-sm text-muted-foreground py-4 text-center">
                    Henuz ders eklenmedi.
                  </p>
                ) : (
                  <div className="border rounded-md overflow-auto max-h-[300px]">
                    <Table>
                      <TableHeader>
                        <TableRow>
                          <TableHead>Kod</TableHead>
                          <TableHead>Ders Adi</TableHead>
                          <TableHead className="w-14 text-center">AKTS</TableHead>
                          <TableHead className="w-14" />
                        </TableRow>
                      </TableHeader>
                      <TableBody>
                        {group.courses.map(c => (
                          <TableRow key={c.courseId}>
                            <TableCell className="font-mono text-xs">{c.courseCode}</TableCell>
                            <TableCell className="text-sm">{c.courseName}</TableCell>
                            <TableCell className="text-center text-sm">{c.ects}</TableCell>
                            <TableCell>
                              <Button
                                size="icon"
                                variant="ghost"
                                className="h-7 w-7 text-destructive hover:text-destructive"
                                onClick={() => handleRemoveCourse(c.courseId)}
                                disabled={removeMutation.isPending}
                              >
                                <Trash2 className="h-3.5 w-3.5" />
                              </Button>
                            </TableCell>
                          </TableRow>
                        ))}
                      </TableBody>
                    </Table>
                  </div>
                )}
              </div>
            </div>
          )}
        </SheetContent>
      </Sheet>

      {groupId !== null && (
        <AddCourseToGroupDialog
          groupId={groupId}
          existingCourseIds={existingCourseIds}
          open={addDialogOpen}
          onOpenChange={setAddDialogOpen}
        />
      )}
    </>
  );
}
