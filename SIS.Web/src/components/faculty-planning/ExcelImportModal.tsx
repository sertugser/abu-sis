import { useRef, useState } from 'react';
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter,
} from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { useImportApply, useImportPreview } from '../../hooks/useExcelSchedule';
import type { ImportPreviewDto, ImportResultDto, ImportRowDto } from '../../types/scheduleExcel';
import { ImportPreviewTable } from './ImportPreviewTable';

interface ExcelImportModalProps {
  open: boolean;
  onClose: () => void;
  facultyId: number;
  termId: number;
}

function buildErrorCsv(rows: ImportRowDto[]): string {
  const header = 'Satır,Ders,Şube,Hoca,Gün,Saat Başlangıç,Saat Bitiş,Derslik,Mesaj';
  const lines = rows
    .filter(r => r.status === 'Error')
    .map(r =>
      [r.rowNumber, r.courseCode, r.sectionCode, r.instructorName, r.day, r.startTime, r.endTime, r.classroomName, r.message]
        .map(v => `"${String(v).replace(/"/g, '""')}"`)
        .join(','),
    );
  return [header, ...lines].join('\n');
}

export function ExcelImportModal({ open, onClose, facultyId, termId }: ExcelImportModalProps) {
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [preview, setPreview] = useState<ImportPreviewDto | null>(null);
  const [result, setResult] = useState<ImportResultDto | null>(null);

  const previewMutation = useImportPreview();
  const applyMutation = useImportApply();

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;
    setPreview(null);
    setResult(null);
    previewMutation.mutate(
      { facultyId, termId, file },
      { onSuccess: setPreview },
    );
  };

  const handleDownloadErrors = () => {
    if (!preview) return;
    const csv = buildErrorCsv(preview.rows);
    const blob = new Blob(['\uFEFF' + csv], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = 'import-errors.csv';
    a.click();
    URL.revokeObjectURL(url);
  };

  const handleApply = () => {
    if (!preview) return;
    const applicableRows = preview.rows.filter(r => r.status !== 'Error');
    applyMutation.mutate(
      { facultyId, termId, rows: applicableRows },
      { onSuccess: setResult },
    );
  };

  const handleClose = () => {
    setPreview(null);
    setResult(null);
    previewMutation.reset();
    applyMutation.reset();
    if (fileInputRef.current) fileInputRef.current.value = '';
    onClose();
  };

  const validCount = preview ? preview.rows.filter(r => r.status === 'Valid').length : 0;
  const warningCount = preview ? preview.rows.filter(r => r.status === 'Warning').length : 0;
  const errorCount = preview ? preview.rows.filter(r => r.status === 'Error').length : 0;
  const applicableCount = validCount + warningCount;

  return (
    <Dialog open={open} onOpenChange={open => { if (!open) handleClose(); }}>
      <DialogContent className="max-w-4xl">
        <DialogHeader>
          <DialogTitle>Excel'den Program Yükle</DialogTitle>
        </DialogHeader>

        {/* Result state */}
        {result && (
          <div className="space-y-3">
            <div className="rounded-lg border border-emerald-200 bg-emerald-50 px-4 py-3 text-emerald-800">
              <p className="font-medium text-sm">İçe aktarma tamamlandı.</p>
              <p className="text-sm mt-1">
                {result.appliedCount} şube atandı
                {result.failedCount > 0 && `, ${result.failedCount} satır başarısız`}.
              </p>
            </div>
            {result.failures.length > 0 && (
              <div className="max-h-48 overflow-y-auto rounded border border-slate-200 bg-white text-sm">
                {result.failures.map(f => (
                  <div key={f.rowNumber} className="flex gap-2 border-b border-slate-100 px-3 py-2 last:border-0">
                    <span className="tabular-nums text-slate-400">#{f.rowNumber}</span>
                    <span className="font-medium">{f.courseCode}</span>
                    <span className="text-slate-600">{f.reason}</span>
                  </div>
                ))}
              </div>
            )}
          </div>
        )}

        {/* Preview state */}
        {!result && preview && (
          <div className="space-y-3">
            <div className="flex items-center gap-4 text-sm">
              <span className="text-emerald-700 font-medium">✅ {validCount} geçerli</span>
              <span className="text-amber-700 font-medium">⚠ {warningCount} uyarı</span>
              <span className="text-red-700 font-medium">❌ {errorCount} hata</span>
            </div>
            <ImportPreviewTable rows={preview.rows} />
          </div>
        )}

        {/* Initial / loading state */}
        {!result && !preview && (
          <div className="py-8 flex flex-col items-center gap-3">
            {previewMutation.isPending ? (
              <p className="text-sm text-slate-500">Dosya analiz ediliyor...</p>
            ) : (
              <>
                <p className="text-sm text-slate-500">
                  .xlsx formatında bir program dosyası seçin.
                </p>
                <Button
                  variant="outline"
                  onClick={() => fileInputRef.current?.click()}
                >
                  Dosya Seç
                </Button>
              </>
            )}
            {previewMutation.isError && (
              <p className="text-sm text-red-600">Dosya okunamadı. Lütfen geçerli bir Excel dosyası seçin.</p>
            )}
          </div>
        )}

        <input
          ref={fileInputRef}
          type="file"
          accept=".xlsx"
          className="hidden"
          onChange={handleFileChange}
        />

        <DialogFooter className="gap-2">
          {!result && preview && errorCount > 0 && (
            <Button variant="outline" onClick={handleDownloadErrors}>
              Hataları İndir
            </Button>
          )}
          <Button variant="outline" onClick={handleClose}>
            {result ? 'Kapat' : 'İptal'}
          </Button>
          {!result && preview && applicableCount > 0 && (
            <Button
              onClick={handleApply}
              disabled={applyMutation.isPending}
            >
              {applyMutation.isPending
                ? 'Atanıyor...'
                : `${applicableCount} Geçerli Ata`}
            </Button>
          )}
          {!result && !preview && (
            <Button onClick={() => fileInputRef.current?.click()}>
              Dosya Seç
            </Button>
          )}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
