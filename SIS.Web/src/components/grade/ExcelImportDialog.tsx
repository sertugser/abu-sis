// SIS.Web/src/components/grade/ExcelImportDialog.tsx
// Excel (.xlsx) ile not içe aktarma: dosya seç → önizleme diff → uygula.

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
import { Badge } from '@/components/ui/badge';
import { FileSpreadsheet, Upload } from 'lucide-react';
import { useToast } from '@/components/ui/global-toast';
import { usePreviewImport, useApplyImport } from '../../hooks/useGradeData';
import type { ImportPreviewResponse } from '../../types/grade';

interface ExcelImportDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  sectionId: number;
}

function getErrorMessage(err: unknown, fallback: string): string {
  if (err && typeof err === 'object' && 'response' in err) {
    const data = (err as { response?: { data?: { error?: string } } }).response?.data;
    if (data?.error) return data.error;
  }
  return fallback;
}

export function ExcelImportDialog({ open, onOpenChange, sectionId }: ExcelImportDialogProps) {
  const { showSuccess, showError } = useToast();
  const previewMut = usePreviewImport(sectionId);
  const applyMut = useApplyImport(sectionId);

  const [file, setFile] = useState<File | null>(null);
  const [preview, setPreview] = useState<ImportPreviewResponse | null>(null);

  const reset = () => {
    setFile(null);
    setPreview(null);
  };

  const handleOpenChange = (next: boolean) => {
    if (!next) reset();
    onOpenChange(next);
  };

  const handlePreview = () => {
    if (!file) {
      showError('Dosya seçilmedi', 'Lütfen bir Excel dosyası seçin.');
      return;
    }
    previewMut.mutate(file, {
      onSuccess: (data) => setPreview(data),
      onError: (err: unknown) => showError('Önizleme alınamadı', getErrorMessage(err, 'Önizleme başarısız.')),
    });
  };

  const handleApply = () => {
    if (!file) return;
    applyMut.mutate(file, {
      onSuccess: (data) => {
        showSuccess(
          'İçe aktarma tamamlandı',
          `Eklenen: ${data.inserted} · Güncellenen: ${data.updated} · Atlanan: ${data.skipped}`,
        );
        handleOpenChange(false);
      },
      onError: (err: unknown) => showError('İçe aktarma başarısız', getErrorMessage(err, 'Aktarım başarısız.')),
    });
  };

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogContent className="max-w-4xl">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <FileSpreadsheet className="h-5 w-5 text-emerald-600" />
            Excel ile Not İçe Aktar
          </DialogTitle>
        </DialogHeader>

        <div className="space-y-4 pt-2">
          <div className="space-y-2">
            <Label className="text-sm font-medium">Excel Dosyası (.xlsx)</Label>
            <Input
              type="file"
              accept=".xlsx"
              onChange={(e) => {
                setFile(e.target.files?.[0] ?? null);
                setPreview(null);
              }}
            />
            <p className="text-xs text-muted-foreground">
              Önce aktif bileşen için şablonu indirip doldurun, sonra burada yükleyin.
            </p>
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
                  Hatalı satırlar içe aktarma sırasında atlanacaktır. Yalnızca geçerli satırlar kaydedilir.
                </div>
              )}
              <div className="max-h-80 overflow-auto">
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
            disabled={!file || previewMut.isPending}
            className="gap-1.5"
          >
            <FileSpreadsheet className="h-4 w-4" />
            {previewMut.isPending ? 'Önizleniyor...' : 'Önizleme Al'}
          </Button>
          <Button
            onClick={handleApply}
            disabled={!file || !preview || preview.errorCount > 0 || applyMut.isPending}
            className="gap-1.5"
          >
            <Upload className="h-4 w-4" />
            {applyMut.isPending ? 'İçe Aktarılıyor...' : 'Onayla ve İçe Aktar'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
