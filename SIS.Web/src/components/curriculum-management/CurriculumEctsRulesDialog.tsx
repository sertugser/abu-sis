import { useEffect, useState } from 'react';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Info } from 'lucide-react';
import type { CurriculumDetailDto } from '../../types/curriculumManagement';
import { useUpdateCurriculum } from '../../hooks/useCurriculumManagement';

interface Props {
  curriculum: CurriculumDetailDto;
  onClose: () => void;
}

/**
 * Müfredat-özel dönem AKTS tavanı kurallarını düzenler.
 * Proliz "Müfredat Sınıf Dönem Tanımları"nın muadili.
 * Üç CGPA bandı için override değerleri atanır veya temizlenir (global fallback).
 */
export function CurriculumEctsRulesDialog({ curriculum, onClose }: Props) {
  const [useOverride, setUseOverride] = useState(
    curriculum.ectsLimitBelow200 != null
      || curriculum.ectsLimit200To249 != null
      || curriculum.ectsLimitAbove250 != null,
  );
  const [below200, setBelow200] = useState<string>(
    curriculum.ectsLimitBelow200?.toString() ?? '30',
  );
  const [mid, setMid] = useState<string>(
    curriculum.ectsLimit200To249?.toString() ?? '42',
  );
  const [above250, setAbove250] = useState<string>(
    curriculum.ectsLimitAbove250?.toString() ?? '48',
  );

  const updateMut = useUpdateCurriculum();

  useEffect(() => {
    setUseOverride(
      curriculum.ectsLimitBelow200 != null
        || curriculum.ectsLimit200To249 != null
        || curriculum.ectsLimitAbove250 != null,
    );
    setBelow200(curriculum.ectsLimitBelow200?.toString() ?? '30');
    setMid(curriculum.ectsLimit200To249?.toString() ?? '42');
    setAbove250(curriculum.ectsLimitAbove250?.toString() ?? '48');
  }, [curriculum]);

  const parsePositive = (s: string): number | null => {
    const n = Number(s);
    if (Number.isFinite(n) && n > 0 && n <= 200) return Math.round(n);
    return null;
  };

  const handleSave = () => {
    if (useOverride) {
      const v1 = parsePositive(below200);
      const v2 = parsePositive(mid);
      const v3 = parsePositive(above250);
      if (v1 === null || v2 === null || v3 === null) {
        return;
      }
      updateMut.mutate(
        {
          id: curriculum.id,
          ectsLimitBelow200: v1,
          ectsLimit200To249: v2,
          ectsLimitAbove250: v3,
          ectsLimitsProvided: true,
        },
        { onSuccess: onClose },
      );
    } else {
      // Tüm override'ları temizle — global varsayılana dön
      updateMut.mutate(
        {
          id: curriculum.id,
          ectsLimitBelow200: null,
          ectsLimit200To249: null,
          ectsLimitAbove250: null,
          ectsLimitsProvided: true,
        },
        { onSuccess: onClose },
      );
    }
  };

  const canSave = !useOverride
    || (parsePositive(below200) !== null
      && parsePositive(mid) !== null
      && parsePositive(above250) !== null);

  return (
    <Dialog open onOpenChange={(o) => { if (!o) onClose(); }}>
      <DialogContent className="max-w-lg">
        <DialogHeader>
          <DialogTitle>Dönem AKTS Tavanı Kuralları</DialogTitle>
          <DialogDescription>
            CGPA'ya göre bir öğrencinin bir dönemde alabileceği maksimum AKTS miktarı.
            Bu müfredat için özel kural tanımlayabilir veya sistemin varsayılanını kullanabilirsiniz.
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-4">
          <div className="space-y-2">
            <label className="flex items-start gap-2 text-sm cursor-pointer">
              <input
                type="radio"
                checked={!useOverride}
                onChange={() => setUseOverride(false)}
                className="mt-1"
              />
              <div>
                <div className="font-medium">Sistem varsayılanını kullan</div>
                <div className="text-xs text-muted-foreground">
                  Genel sistem ayarlarındaki AKTS tavanı uygulanır (SystemConfig).
                </div>
              </div>
            </label>
            <label className="flex items-start gap-2 text-sm cursor-pointer">
              <input
                type="radio"
                checked={useOverride}
                onChange={() => setUseOverride(true)}
                className="mt-1"
              />
              <div>
                <div className="font-medium">Bu müfredata özel kural tanımla</div>
                <div className="text-xs text-muted-foreground">
                  Yalnızca bu müfredatı kullanan öğrencilere uygulanır.
                </div>
              </div>
            </label>
          </div>

          {useOverride && (
            <div className="space-y-3 border rounded-lg p-3 bg-gray-50/60">
              <div className="grid grid-cols-3 gap-3">
                <div className="space-y-1">
                  <Label htmlFor="below200" className="text-xs">
                    CGPA &lt; 2.00
                  </Label>
                  <Input
                    id="below200"
                    type="number"
                    min={1}
                    max={200}
                    value={below200}
                    onChange={(e) => setBelow200(e.target.value)}
                  />
                </div>
                <div className="space-y-1">
                  <Label htmlFor="mid" className="text-xs">
                    CGPA 2.00–2.49
                  </Label>
                  <Input
                    id="mid"
                    type="number"
                    min={1}
                    max={200}
                    value={mid}
                    onChange={(e) => setMid(e.target.value)}
                  />
                </div>
                <div className="space-y-1">
                  <Label htmlFor="above250" className="text-xs">
                    CGPA ≥ 2.50
                  </Label>
                  <Input
                    id="above250"
                    type="number"
                    min={1}
                    max={200}
                    value={above250}
                    onChange={(e) => setAbove250(e.target.value)}
                  />
                </div>
              </div>
              <Alert>
                <Info className="h-4 w-4" />
                <AlertTitle>Uygulama</AlertTitle>
                <AlertDescription className="text-xs">
                  Bu değerler, bu müfredata bağlı öğrencilerin ders kayıt doğrulamasında
                  global varsayılanın yerine kullanılır. Her alan 1–200 arası olmalıdır.
                </AlertDescription>
              </Alert>
            </div>
          )}
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={onClose}>İptal</Button>
          <Button onClick={handleSave} disabled={!canSave || updateMut.isPending}>
            {updateMut.isPending ? 'Kaydediliyor...' : 'Kaydet'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
