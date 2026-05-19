import { useEffect, useState } from 'react';
import { Loader2 } from 'lucide-react';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { useDepartmentInstructors } from '@/hooks/useCourseOpening';
import type { CourseGroup } from '../types';

export interface AddSectionPayload {
  capacityPerSection: number;
  internalQuota: number | null;
  externalQuota: number | null;
  instructorId: number | null;
}

interface AddSectionDialogProps {
  open: boolean;
  course: CourseGroup | null;
  departmentId: number | null;
  termId: number | null;
  isSubmitting: boolean;
  onClose: () => void;
  onSubmit: (payload: AddSectionPayload) => void;
}

export function AddSectionDialog({
  open,
  course,
  departmentId,
  termId,
  isSubmitting,
  onClose,
  onSubmit,
}: AddSectionDialogProps) {
  const instructorsQuery = useDepartmentInstructors(departmentId, termId, true);
  const instructors = instructorsQuery.data ?? [];

  const defaultCapacity = course && course.sections.length > 0
    ? Math.round(course.sections.reduce((sum, s) => sum + s.capacity, 0) / course.sections.length)
    : 40;

  const [capacity, setCapacity] = useState(String(defaultCapacity));
  const [internalQuota, setInternalQuota] = useState('');
  const [externalQuota, setExternalQuota] = useState('');
  const [instructorId, setInstructorId] = useState('');
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (open) {
      setCapacity(String(defaultCapacity));
      setInternalQuota('');
      setExternalQuota('');
      setInstructorId('');
      setError(null);
    }
  }, [open, defaultCapacity]);

  const nextSectionNumber = course && course.sections.length > 0
    ? Math.max(...course.sections.map(s => s.sectionNumber)) + 1
    : 1;

  const handleSubmit = () => {
    const capNum = Number(capacity);
    if (!Number.isFinite(capNum) || capNum <= 0) {
      setError('Kontenjan pozitif bir tam sayı olmalı.');
      return;
    }
    const internal = internalQuota.trim() === '' ? null : Number(internalQuota);
    const external = externalQuota.trim() === '' ? null : Number(externalQuota);

    if (internal !== null && (!Number.isFinite(internal) || internal < 0)) {
      setError('İç kontenjan 0 veya pozitif bir tam sayı olmalı.');
      return;
    }
    if (external !== null && (!Number.isFinite(external) || external < 0)) {
      setError('Dış kontenjan 0 veya pozitif bir tam sayı olmalı.');
      return;
    }
    if (internal !== null && external !== null && internal + external > capNum) {
      setError('İç + dış kontenjan toplamı, toplam kontenjandan büyük olamaz.');
      return;
    }

    const inst = instructorId.trim() === '' ? null : Number(instructorId);
    if (inst !== null && (!Number.isFinite(inst) || inst <= 0)) {
      setError('Geçersiz öğretim elemanı seçimi.');
      return;
    }

    setError(null);
    onSubmit({
      capacityPerSection: capNum,
      internalQuota: internal,
      externalQuota: external,
      instructorId: inst,
    });
  };

  return (
    <Dialog open={open} onOpenChange={isOpen => { if (!isOpen) onClose(); }}>
      <DialogContent className="sm:max-w-[480px]">
        <DialogHeader>
          <DialogTitle>Yeni Şube Ekle</DialogTitle>
          <DialogDescription>
            {course ? (
              <>
                <span className="font-mono font-semibold text-blue-700">{course.courseCode}</span> {course.courseName} dersine{' '}
                <strong>Şube {nextSectionNumber}</strong> eklenecek.
              </>
            ) : (
              'Ders seçilmedi.'
            )}
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-3">
          <div>
            <Label htmlFor="add-section-capacity" className="text-sm font-medium">Kontenjan</Label>
            <Input
              id="add-section-capacity"
              type="number"
              min={1}
              value={capacity}
              onChange={e => setCapacity(e.target.value)}
              className="mt-1 h-10 text-sm"
              disabled={isSubmitting}
            />
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div>
              <Label htmlFor="add-section-internal" className="text-sm font-medium">İç Kontenjan</Label>
              <Input
                id="add-section-internal"
                type="number"
                min={0}
                placeholder="Boş = limit yok"
                value={internalQuota}
                onChange={e => setInternalQuota(e.target.value)}
                className="mt-1 h-10 text-sm"
                disabled={isSubmitting}
              />
            </div>
            <div>
              <Label htmlFor="add-section-external" className="text-sm font-medium">Dış Kontenjan</Label>
              <Input
                id="add-section-external"
                type="number"
                min={0}
                placeholder="Boş = limit yok"
                value={externalQuota}
                onChange={e => setExternalQuota(e.target.value)}
                className="mt-1 h-10 text-sm"
                disabled={isSubmitting}
              />
            </div>
          </div>
          <div>
            <Label className="text-sm font-medium">Öğretim Elemanı (opsiyonel)</Label>
            <select
              value={instructorId}
              onChange={e => setInstructorId(e.target.value)}
              className="mt-1 h-10 w-full rounded-md border border-input bg-background px-3 text-sm outline-none focus:ring-2 focus:ring-ring"
              disabled={isSubmitting || instructorsQuery.isLoading}
            >
              <option value="">— Sonra atanır —</option>
              {instructors.map(instructor => (
                <option key={instructor.instructorId} value={instructor.instructorId}>
                  {instructor.fullName}
                  {instructor.departmentCode ? ` (${instructor.departmentCode})` : ''}
                </option>
              ))}
            </select>
          </div>
          {error && <p className="text-xs text-red-600">{error}</p>}
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={onClose} disabled={isSubmitting}>İptal</Button>
          <Button onClick={handleSubmit} disabled={isSubmitting || !course}>
            {isSubmitting && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
            Şube Ekle
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
