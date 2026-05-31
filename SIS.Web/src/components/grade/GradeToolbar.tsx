// SIS.Web/src/components/grade/GradeToolbar.tsx
// Proliz tarzı eşit-genişlikli, hizalı, gruplu araç çubuğu grid'i.
// İlan Et per-component oldu (kolon başlıklarında) — burada yok.
// Grup A (görünüm/çıktı) · Grup B (giriş) · Grup C (eylem, Kaydet dahil).

import {
  CheckCircle,
  ClipboardPaste,
  Eye,
  Printer,
  Save,
  Settings,
  Upload,
  XCircle,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { BulkAttendanceMenu, type BulkAttendanceItem } from './BulkAttendanceMenu';
import { TemplateDownloadMenu } from './TemplateDownloadMenu';
import type { GradeComponent, StudentGradeRow } from '../../types/grade';

interface GradeToolbarProps {
  students: StudentGradeRow[];
  components: GradeComponent[];
  isFinalized: boolean;
  showPreview: boolean;
  showPreviousGrade: boolean;
  isSaving: boolean;
  isDownloadingTemplate: boolean;
  isUnfinalizePending: boolean;
  onTogglePreview: () => void;
  onTogglePreviousGrade: () => void;
  onOpenCriteria: () => void;
  onPrint: () => void;
  onOpenPaste: () => void;
  onBulkAttendance: (componentId: number, items: BulkAttendanceItem[]) => void;
  onDownloadTemplate: (componentId: number) => void;
  onOpenImport: () => void;
  onSave: () => void;
  onFinalize: () => void;
  onUnfinalize: () => void;
}

const BTN = 'w-full justify-start h-9 gap-1.5 text-sm';

export function GradeToolbar({
  students,
  components,
  isFinalized,
  showPreview,
  showPreviousGrade,
  isSaving,
  isDownloadingTemplate,
  isUnfinalizePending,
  onTogglePreview,
  onTogglePreviousGrade,
  onOpenCriteria,
  onPrint,
  onOpenPaste,
  onBulkAttendance,
  onDownloadTemplate,
  onOpenImport,
  onSave,
  onFinalize,
  onUnfinalize,
}: GradeToolbarProps) {
  return (
    <div className="grid grid-cols-2 sm:grid-cols-4 lg:grid-cols-6 gap-2">
      {/* ── Grup A: görünüm / çıktı ── */}
      <Button variant={showPreview ? 'default' : 'outline'} size="sm" onClick={onTogglePreview} className={BTN}>
        <Eye className="h-4 w-4" /> Ön İzleme
      </Button>
      <Button
        variant={showPreviousGrade ? 'default' : 'outline'}
        size="sm"
        onClick={onTogglePreviousGrade}
        className={BTN}
      >
        <Eye className="h-4 w-4" /> Önceki Harf Notu
      </Button>
      <Button variant="outline" size="sm" onClick={onOpenCriteria} disabled={isFinalized} className={BTN}>
        <Settings className="h-4 w-4" /> Harf Aralığı
      </Button>
      <Button variant="outline" size="sm" onClick={onPrint} className={BTN}>
        <Printer className="h-4 w-4" /> Yazdır
      </Button>

      {/* ── Grup B: giriş ── */}
      <Button
        variant="outline"
        size="sm"
        onClick={onOpenPaste}
        disabled={isFinalized}
        className={BTN}
      >
        <ClipboardPaste className="h-4 w-4" /> Toplu Not Aktar
      </Button>
      <Button
        variant="outline"
        size="sm"
        onClick={onOpenImport}
        disabled={isFinalized}
        className={BTN}
      >
        <Upload className="h-4 w-4" /> Excel Yükle
      </Button>
      <div className="[&>button]:w-full [&>button]:justify-start [&>button]:h-9">
        <TemplateDownloadMenu
          components={components}
          disabled={isFinalized}
          isDownloading={isDownloadingTemplate}
          onDownload={onDownloadTemplate}
        />
      </div>
      <div className="[&>button]:w-full [&>button]:justify-start [&>button]:h-9">
        <BulkAttendanceMenu
          students={students}
          components={components}
          onBulkSet={onBulkAttendance}
          disabled={isFinalized}
        />
      </div>

      {/* ── Grup C: eylem ── */}
      {!isFinalized && (
        <Button size="sm" onClick={onSave} disabled={isSaving} className={BTN}>
          <Save className="h-4 w-4" />
          {isSaving ? 'Kaydediliyor...' : 'Kaydet'}
        </Button>
      )}

      {isFinalized ? (
        <Button variant="outline" size="sm" onClick={onUnfinalize} disabled={isUnfinalizePending} className={BTN}>
          <XCircle className="h-4 w-4" /> Geri Al
        </Button>
      ) : (
        <Button size="sm" onClick={onFinalize} className={BTN}>
          <CheckCircle className="h-4 w-4" /> Kesinleştir
        </Button>
      )}
    </div>
  );
}
