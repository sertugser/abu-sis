import { useEffect, useState } from 'react';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Button } from '@/components/ui/button';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { useUpdateOpenedSection } from '../../../hooks/useCourseOpening';
import type { OpenedCourseSectionDto, InstructorOptionDto } from '../../../types/courseOpening';

interface SectionEditDialogProps {
  section: OpenedCourseSectionDto | null;
  departmentId: number;
  termId: number;
  instructors: InstructorOptionDto[];
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

/** Açılmış şubenin kontenjan, kotalar ve hoca bilgisini günceller. */
export function SectionEditDialog({ section, departmentId, termId, instructors, open, onOpenChange }: SectionEditDialogProps) {
  const [capacity, setCapacity] = useState<number>(0);
  const [internalQuota, setInternalQuota] = useState<number | null>(null);
  const [externalQuota, setExternalQuota] = useState<number | null>(null);
  const [instructorId, setInstructorId] = useState<number | null>(null);
  const [error, setError] = useState<string | null>(null);

  const mutation = useUpdateOpenedSection(departmentId, termId);

  useEffect(() => {
    if (section) {
      setCapacity(section.capacity);
      setInternalQuota(section.internalQuota ?? null);
      setExternalQuota(section.externalQuota ?? null);
      setInstructorId(section.instructorId ?? null);
      setError(null);
    }
  }, [section]);

  if (!section) return null;

  const handleSave = async () => {
    setError(null);
    try {
      await mutation.mutateAsync({
        sectionId: section.sectionId,
        capacity,
        internalQuota: internalQuota ?? 0,
        externalQuota: externalQuota ?? 0,
        instructorId: instructorId ?? undefined,
      });
      onOpenChange(false);
    } catch (e) {
      const err = e as { response?: { data?: { error?: string } }; message?: string };
      setError(err?.response?.data?.error ?? err?.message ?? 'Bilinmeyen hata');
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-lg">
        <DialogHeader>
          <DialogTitle className="font-mono">
            {section.courseCode} — Şube {section.sectionNumber}
          </DialogTitle>
          <DialogDescription>{section.courseName}</DialogDescription>
        </DialogHeader>

        <div className="space-y-3 mt-2">
          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1">
              <Label className="text-xs">Kontenjan</Label>
              <Input
                type="number"
                min={1}
                value={capacity}
                onChange={(e) => setCapacity(Math.max(1, Number(e.target.value) || 1))}
                className="h-9"
              />
              <p className="text-[10px] text-muted-foreground">Kayıtlı: {section.enrolledCount}</p>
            </div>

            <div className="space-y-1">
              <Label className="text-xs">Öğretim Üyesi</Label>
              <Select
                value={instructorId != null ? String(instructorId) : 'none'}
                onValueChange={(v) => setInstructorId(v === 'none' ? null : Number(v))}
              >
                <SelectTrigger className="h-9">
                  <SelectValue placeholder="Hoca" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="none">— Atanmamış —</SelectItem>
                  {instructors.map((inst) => (
                    <SelectItem key={inst.instructorId} value={String(inst.instructorId)}>
                      {inst.fullName}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1">
              <Label className="text-xs">İç Kota</Label>
              <Input
                type="number"
                min={0}
                value={internalQuota ?? ''}
                onChange={(e) => setInternalQuota(e.target.value === '' ? null : Math.max(0, Number(e.target.value)))}
                className="h-9"
                placeholder="—"
              />
              <p className="text-[10px] text-muted-foreground">Boş = sınırsız</p>
            </div>

            <div className="space-y-1">
              <Label className="text-xs">Dış Kota</Label>
              <Input
                type="number"
                min={0}
                value={externalQuota ?? ''}
                onChange={(e) => setExternalQuota(e.target.value === '' ? null : Math.max(0, Number(e.target.value)))}
                className="h-9"
                placeholder="—"
              />
              <p className="text-[10px] text-muted-foreground">Boş = sınırsız</p>
            </div>
          </div>

          {error && (
            <div className="text-xs text-destructive p-2 bg-destructive/10 rounded border border-destructive/30">
              {error}
            </div>
          )}
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)} disabled={mutation.isPending}>
            İptal
          </Button>
          <Button onClick={handleSave} disabled={mutation.isPending}>
            {mutation.isPending ? 'Kaydediliyor...' : 'Kaydet'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
