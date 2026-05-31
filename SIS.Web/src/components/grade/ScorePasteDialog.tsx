// SIS.Web/src/components/grade/ScorePasteDialog.tsx
// "Toplu Not Aktar": metin yapıştırma ile not aktarımı (Excel import ile ortak çekirdek).
// Önizle → diff tablosu (ImportPreviewResponse) → Uygula.

import { useState } from 'react';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Badge } from '@/components/ui/badge';
import { ClipboardPaste, Eye, Upload } from 'lucide-react';
import { useToast } from '@/components/ui/global-toast';
import { usePastePreview, usePasteApply } from '../../hooks/useGradeData';
import { getErrorMessage } from '../../lib/apiError';
import type { ImportPreviewResponse } from '../../types/grade';

interface ScorePasteDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  sectionId: number;
  componentId: number;
}

export function ScorePasteDialog({ open, onOpenChange, sectionId, componentId }: ScorePasteDialogProps) {
  const { showSuccess, showError } = useToast();
  const previewMut = usePastePreview(sectionId);
  const applyMut = usePasteApply(sectionId);

  const [text, setText] = useState('');
  const [separator, setSeparator] = useState('');
  const [preview, setPreview] = useState<ImportPreviewResponse | null>(null);

  const reset = () => {
    setText('');
    setSeparator('');
    setPreview(null);
  };

  const handleOpenChange = (next: boolean) => {
    if (!next) reset();
    onOpenChange(next);
  };

  const handlePreview = () => {
    if (!text.trim()) {
      showError('Metin boş', 'Lütfen aktarılacak satırları yapıştırın.');
      return;
    }
    previewMut.mutate(
      { componentId, text, separator },
      {
        onSuccess: (data) => setPreview(data),
        onError: (err: unknown) => showError('Önizleme alınamadı', getErrorMessage(err, 'Önizleme başarısız.')),
      },
    );
  };

  const handleApply = () => {
    if (!preview) return;
    applyMut.mutate(
      { componentId, text, separator },
      {
        onSuccess: (data) => {
          showSuccess(
            'Toplu not aktarımı tamamlandı',
            `Eklenen: ${data.inserted} · Güncellenen: ${data.updated} · Atlanan: ${data.skipped}`,
          );
          handleOpenChange(false);
        },
        onError: (err: unknown) => showError('İçe aktarma başarısız', getErrorMessage(err, 'Aktarım başarısız.')),
      },
    );
  };

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogContent className="max-w-4xl">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <ClipboardPaste className="h-5 w-5 text-primary" />
            Toplu Not Aktar
          </DialogTitle>
        </DialogHeader>

        <div className="space-y-4 pt-2">
          <div className="grid gap-4 sm:grid-cols-[1fr_180px]">
            <div className="space-y-2">
              <Label className="text-sm font-medium">Satırlar (Öğrenci No &lt;ayraç&gt; Not)</Label>
              <Textarea
                value={text}
                onChange={(e) => { setText(e.target.value); setPreview(null); }}
                placeholder={'20210001 85\n20210002 72\n20210003 60'}
                className="h-44 font-mono text-sm"
              />
            </div>
            <div className="space-y-2">
              <Label className="text-sm font-medium">Ayraç</Label>
              <Input
                value={separator}
                onChange={(e) => { setSeparator(e.target.value); setPreview(null); }}
                placeholder="boş = boşluk"
                className="font-mono"
                maxLength={3}
              />
              <p className="text-xs text-muted-foreground leading-relaxed">
                Örnek: <span className="font-mono">; : - ,</span>
                <br />Boş bırakılırsa boşluk/sekme ile ayrılır.
              </p>
            </div>
          </div>

          {preview && (
            <div className="rounded-lg border overflow-hidden">
              <div className="flex flex-wrap gap-2 p-4 border-b bg-muted/20">
                <Badge variant="secondary">Toplam: {preview.totalRows}</Badge>
                <Badge variant="secondary">Geçerli: {preview.validCount}</Badge>
                <Badge variant="secondary">Uyarı: {preview.warningCount}</Badge>
                <Badge variant="secondary">Hata: {preview.errorCount}</Badge>
                <Badge variant="secondary">Boş: {preview.emptyCount}</Badge>
              </div>
              {preview.errorCount > 0 && (
                <div className="px-4 py-3 border-b bg-red-50 text-red-700 text-sm">
                  Hatalı satırlar atlanacaktır. Yalnızca geçerli satırlar kaydedilir.
                </div>
              )}
              <div className="max-h-72 overflow-auto">
                <table className="w-full text-sm">
                  <thead className="sticky top-0 bg-background border-b">
                    <tr>
                      <th className="text-left px-3 py-2 font-medium text-muted-foreground">Satır</th>
                      <th className="text-left px-3 py-2 font-medium text-muted-foreground">Öğrenci</th>
                      <th className="text-center px-3 py-2 font-medium text-muted-foreground">Mevcut</th>
                      <th className="text-center px-3 py-2 font-medium text-muted-foreground">Yeni</th>
                      <th className="text-center px-3 py-2 font-medium text-muted-foreground">Durum</th>
                      <th className="text-left px-3 py-2 font-medium text-muted-foreground">Mesaj</th>
                    </tr>
                  </thead>
                  <tbody>
                    {preview.rows.map((row) => {
                      const statusStyle =
                        row.status === 'Valid'
                          ? 'bg-emerald-50 text-emerald-700'
                          : row.status === 'Update'
                            ? 'bg-amber-50 text-amber-700'
                            : row.status === 'Warning'
                              ? 'bg-orange-50 text-orange-700'
                              : row.status === 'Error'
                                ? 'bg-red-50 text-red-700'
                                : 'bg-muted text-muted-foreground';
                      return (
                        <tr key={row.rowNumber} className="border-b last:border-0">
                          <td className="px-3 py-2 font-mono">{row.rowNumber}</td>
                          <td className="px-3 py-2">
                            <div className="font-medium">{row.firstName} {row.lastName}</div>
                            <div className="text-xs text-muted-foreground font-mono">{row.studentNumber}</div>
                          </td>
                          <td className="px-3 py-2 text-center font-mono">{row.currentScore ?? '—'}</td>
                          <td className="px-3 py-2 text-center font-mono">{(row.newScore ?? row.rawNewScore) || '—'}</td>
                          <td className="px-3 py-2 text-center">
                            <span className={`inline-flex px-2 py-1 rounded-md text-xs font-medium ${statusStyle}`}>
                              {row.status}
                            </span>
                          </td>
                          <td className="px-3 py-2 text-xs text-muted-foreground">{row.message}</td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            </div>
          )}
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={() => handleOpenChange(false)}>Kapat</Button>
          <Button
            variant="outline"
            onClick={handlePreview}
            disabled={!text.trim() || previewMut.isPending}
            className="gap-1.5"
          >
            <Eye className="h-4 w-4" />
            {previewMut.isPending ? 'Önizleniyor...' : 'Önizle'}
          </Button>
          <Button
            onClick={handleApply}
            disabled={!preview || preview.errorCount > 0 || applyMut.isPending}
            className="gap-1.5"
          >
            <Upload className="h-4 w-4" />
            {applyMut.isPending ? 'Uygulanıyor...' : 'Uygula'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
