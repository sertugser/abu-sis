import { useMemo, useState } from 'react';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Loader2, Lock, LockOpen, Plus, X } from 'lucide-react';
import {
  useAccessPolicies,
  useCreateAccessPolicy,
  useDeleteAccessPolicy,
} from '../../../hooks/useCourseAccessPolicy';
import {
  useAdminDepartmentsByStructure,
} from '../../../hooks/useAdminAcademicStructure';
import { useFacultyLookup } from '../../../hooks/useAdminLookup';
import type { OpenedCourseSectionDto } from '../../../types/courseOpening';

interface SectionAccessDialogProps {
  /** Erişim yönetilecek şube. Null ise dialog kapalı kabul edilir. */
  section: OpenedCourseSectionDto | null;
  termId: number;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

/**
 * Bir dersin (ders+dönem bazında) erişim politikalarını yönetir.
 * Politika ders+dönem seviyesindedir; aynı dersin tüm şubelerini etkiler.
 * Mevcut /api/course-access-policy CRUD endpoint'lerini kullanır — backend'e ek değişiklik gerekmez.
 */
export function SectionAccessDialog({ section, termId, open, onOpenChange }: SectionAccessDialogProps) {
  const courseId = section?.courseId ?? null;
  const { data: policies = [], isLoading } = useAccessPolicies(courseId, termId);
  const createMut = useCreateAccessPolicy();
  const deleteMut = useDeleteAccessPolicy();

  const [addMode, setAddMode] = useState<'faculty' | 'department'>('department');
  const [selectedFacultyId, setSelectedFacultyId] = useState<string>('');
  const [selectedDeptId, setSelectedDeptId] = useState<string>('');

  const { data: faculties = [] } = useFacultyLookup();
  const { data: departments = [] } = useAdminDepartmentsByStructure(
    addMode === 'department' && selectedFacultyId ? Number(selectedFacultyId) : undefined,
  );

  const existingFacultyIds = useMemo(
    () => new Set(policies.filter((p) => p.facultyId).map((p) => p.facultyId!)),
    [policies],
  );
  const existingDeptIds = useMemo(
    () => new Set(policies.filter((p) => p.departmentId).map((p) => p.departmentId!)),
    [policies],
  );

  if (!section) return null;

  const handleAdd = () => {
    if (!courseId) return;
    if (addMode === 'faculty' && selectedFacultyId) {
      createMut.mutate({ courseId, termId, facultyId: Number(selectedFacultyId) });
      setSelectedFacultyId('');
    } else if (addMode === 'department' && selectedDeptId) {
      createMut.mutate({ courseId, termId, departmentId: Number(selectedDeptId) });
      setSelectedDeptId('');
      setSelectedFacultyId('');
    }
  };

  const handleRemove = (policyId: number) => {
    if (!courseId) return;
    deleteMut.mutate({ id: policyId, courseId, termId });
  };

  const hasPolicy = policies.length > 0;

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-lg">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            {hasPolicy ? <LockOpen className="h-4 w-4 text-primary" /> : <Lock className="h-4 w-4 text-muted-foreground" />}
            <span>Ders Erişimi</span>
          </DialogTitle>
          <DialogDescription>
            <span className="font-mono font-semibold">{section.courseCode}</span> ({section.courseName}) dersini hangi
            fakülte / bölüm öğrencileri alabilir? Politika dersin tüm şubelerine uygulanır.
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-3">
          {/* Mevcut politikalar */}
          <div>
            <div className="text-xs font-medium text-muted-foreground mb-1.5">Tanımlı Erişim ({policies.length})</div>
            {isLoading ? (
              <div className="flex items-center gap-2 text-xs text-muted-foreground">
                <Loader2 className="h-3 w-3 animate-spin" /> Yükleniyor…
              </div>
            ) : policies.length === 0 ? (
              <p className="text-xs text-muted-foreground italic">
                Henüz ek erişim tanımlı değil. Ders yalnızca kendi bölümünden alınabilir.
              </p>
            ) : (
              <ul className="space-y-1">
                {policies.map((p) => (
                  <li
                    key={p.id}
                    className="flex items-center justify-between gap-2 px-2 py-1.5 rounded bg-muted/50 text-sm"
                  >
                    <span>
                      {p.facultyName ? (
                        <>
                          <span className="text-xs uppercase text-muted-foreground mr-1">Fakülte</span>
                          {p.facultyName}
                        </>
                      ) : (
                        <>
                          <span className="text-xs uppercase text-muted-foreground mr-1">Bölüm</span>
                          {p.departmentName}
                        </>
                      )}
                    </span>
                    <Button
                      variant="ghost"
                      size="sm"
                      className="h-6 w-6 p-0"
                      onClick={() => handleRemove(p.id)}
                      disabled={deleteMut.isPending}
                      title="Politikayı kaldır"
                    >
                      <X className="h-3 w-3" />
                    </Button>
                  </li>
                ))}
              </ul>
            )}
          </div>

          {/* Yeni politika ekle */}
          <div className="border-t pt-3 space-y-2">
            <div className="text-xs font-medium text-muted-foreground">Yeni Erişim Ekle</div>

            <div className="flex gap-2">
              <Button
                type="button"
                variant={addMode === 'department' ? 'default' : 'outline'}
                size="sm"
                className="h-7 text-xs"
                onClick={() => setAddMode('department')}
              >
                Bölüm
              </Button>
              <Button
                type="button"
                variant={addMode === 'faculty' ? 'default' : 'outline'}
                size="sm"
                className="h-7 text-xs"
                onClick={() => setAddMode('faculty')}
              >
                Fakülte
              </Button>
            </div>

            {addMode === 'faculty' ? (
              <div className="flex gap-2">
                <Select value={selectedFacultyId} onValueChange={setSelectedFacultyId}>
                  <SelectTrigger className="h-8 text-sm">
                    <SelectValue placeholder="Fakülte seçin" />
                  </SelectTrigger>
                  <SelectContent>
                    {faculties
                      .filter((f) => !existingFacultyIds.has(f.id))
                      .map((f) => (
                        <SelectItem key={f.id} value={String(f.id)}>
                          {f.name}
                        </SelectItem>
                      ))}
                  </SelectContent>
                </Select>
                <Button
                  type="button"
                  size="sm"
                  className="h-8 px-3"
                  onClick={handleAdd}
                  disabled={!selectedFacultyId || createMut.isPending}
                >
                  {createMut.isPending ? <Loader2 className="h-3 w-3 animate-spin" /> : <Plus className="h-3 w-3" />}
                </Button>
              </div>
            ) : (
              <div className="space-y-2">
                <Select value={selectedFacultyId} onValueChange={setSelectedFacultyId}>
                  <SelectTrigger className="h-8 text-sm">
                    <SelectValue placeholder="Önce fakülte seçin" />
                  </SelectTrigger>
                  <SelectContent>
                    {faculties.map((f) => (
                      <SelectItem key={f.id} value={String(f.id)}>
                        {f.name}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                {selectedFacultyId && (
                  <div className="flex gap-2">
                    <Select value={selectedDeptId} onValueChange={setSelectedDeptId}>
                      <SelectTrigger className="h-8 text-sm">
                        <SelectValue placeholder="Bölüm seçin" />
                      </SelectTrigger>
                      <SelectContent>
                        {departments
                          .filter((d) => !existingDeptIds.has(d.id))
                          .map((d) => (
                            <SelectItem key={d.id} value={String(d.id)}>
                              {d.name}
                            </SelectItem>
                          ))}
                      </SelectContent>
                    </Select>
                    <Button
                      type="button"
                      size="sm"
                      className="h-8 px-3"
                      onClick={handleAdd}
                      disabled={!selectedDeptId || createMut.isPending}
                    >
                      {createMut.isPending ? (
                        <Loader2 className="h-3 w-3 animate-spin" />
                      ) : (
                        <Plus className="h-3 w-3" />
                      )}
                    </Button>
                  </div>
                )}
              </div>
            )}
          </div>
        </div>

        <DialogFooter>
          <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
            Kapat
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
