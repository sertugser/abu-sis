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
import type { DepartmentSectionClassList } from '@/types/academicOperations';

export interface CapacityUpdate {
  capacity: number;
  internalQuota: number | null;
  externalQuota: number | null;
}

interface EditCapacityDialogProps {
  open: boolean;
  section: DepartmentSectionClassList | null;
  isSubmitting: boolean;
  onClose: () => void;
  onSubmit: (update: CapacityUpdate) => void;
}

export function EditCapacityDialog({ open, section, isSubmitting, onClose, onSubmit }: EditCapacityDialogProps) {
  const [capacity, setCapacity] = useState('');
  const [internalQuota, setInternalQuota] = useState('');
  const [externalQuota, setExternalQuota] = useState('');
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (section) {
      setCapacity(String(section.capacity));
      setInternalQuota(section.internalQuota === null ? '' : String(section.internalQuota));
      setExternalQuota(section.externalQuota === null ? '' : String(section.externalQuota));
      setError(null);
    }
  }, [section?.sectionId, open]); // eslint-disable-line react-hooks/exhaustive-deps

  const handleSubmit = () => {
    const capNum = Number(capacity);
    if (!Number.isFinite(capNum) || capNum < 0) {
      setError('Kontenjan 0 veya pozitif bir tam sayı olmalı.');
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

    setError(null);
    onSubmit({ capacity: capNum, internalQuota: internal, externalQuota: external });
  };

  return (
    <Dialog open={open} onOpenChange={isOpen => { if (!isOpen) onClose(); }}>
      <DialogContent className="sm:max-w-[480px]">
        <DialogHeader>
          <DialogTitle>Kontenjan Düzenle</DialogTitle>
          <DialogDescription>
            {section ? (
              <>
                <span className="font-mono font-semibold text-blue-700">{section.courseCode}</span> — Şube {section.sectionNumber}
                <span className="ml-2 text-xs text-gray-500">
                  (Kayıtlı: {section.activeStudentCount} öğrenci)
                </span>
              </>
            ) : (
              'Şube seçilmedi.'
            )}
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-3">
          <div>
            <Label htmlFor="capacity" className="text-sm font-medium">Toplam Kontenjan</Label>
            <Input
              id="capacity"
              type="number"
              min={0}
              value={capacity}
              onChange={e => setCapacity(e.target.value)}
              className="mt-1 h-10 text-sm"
              disabled={isSubmitting}
            />
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div>
              <Label htmlFor="internalQuota" className="text-sm font-medium">İç Kontenjan</Label>
              <Input
                id="internalQuota"
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
              <Label htmlFor="externalQuota" className="text-sm font-medium">Dış Kontenjan</Label>
              <Input
                id="externalQuota"
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
          {error && (
            <p className="text-xs text-red-600">{error}</p>
          )}
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={onClose} disabled={isSubmitting}>
            İptal
          </Button>
          <Button onClick={handleSubmit} disabled={isSubmitting}>
            {isSubmitting && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
            Kaydet
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
