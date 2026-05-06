import { useState } from 'react';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Copy as CopyIcon, Pencil, Trash2, Users } from 'lucide-react';
import type { OpenedCourseSectionDto, InstructorOptionDto } from '../../../types/courseOpening';
import { semesterToClassLevel } from '../types';
import { SectionEditDialog } from './SectionEditDialog';
import { AddSectionDialog } from './AddSectionDialog';
import { DeleteSectionDialog } from './DeleteSectionDialog';
import { SectionAccessDialog } from './SectionAccessDialog';

interface SectionManagementDialogProps {
  section: OpenedCourseSectionDto | null;
  departmentId: number;
  termId: number;
  instructors: InstructorOptionDto[];
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

/**
 * Şube yönetim ekranı — tek pencere, tüm aksiyonlar (düzenle, çoğalt, erişim, sil)
 * burada toplanır. Tabloda satıra çift tıklanınca açılır.
 *
 * Sub-dialog'lar bu pencerenin içinden tetiklenir; üst üste açıldıklarında
 * yönetim penceresi arka planda açık kalır.
 */
export function SectionManagementDialog({
  section,
  departmentId,
  termId,
  instructors,
  open,
  onOpenChange,
}: SectionManagementDialogProps) {
  const [editOpen, setEditOpen] = useState(false);
  const [cloneOpen, setCloneOpen] = useState(false);
  const [accessOpen, setAccessOpen] = useState(false);
  const [deleteOpen, setDeleteOpen] = useState(false);

  if (!section) return null;

  const ratio =
    section.capacity > 0 ? Math.round((section.enrolledCount / section.capacity) * 100) : 0;

  return (
    <>
      <Dialog open={open} onOpenChange={onOpenChange}>
        <DialogContent className="max-w-2xl">
          <DialogHeader>
            <DialogTitle className="font-mono">
              {section.courseCode} — Şube {section.sectionNumber}
            </DialogTitle>
            <DialogDescription>{section.courseName}</DialogDescription>
          </DialogHeader>

          {/* Şube detayları — sade grid */}
          <div className="grid grid-cols-3 gap-2 mt-1">
            <Cell label="Sınıf" value={semesterToClassLevel(section.suggestedSemester) || '—'} />
            <Cell label="Kredi" value={section.credits} />
            <Cell label="AKTS" value={section.ects} />
            <Cell label="Teori Saat" value={section.theoryHours} />
            <Cell label="Lab Saat" value={section.labHours} />
            <Cell label="Durum" value={section.isActive ? 'Aktif' : 'Pasif'} />
            <Cell label="Kontenjan" value={section.capacity} />
            <Cell label="İç Kota" value={section.internalQuota ?? '—'} />
            <Cell label="Dış Kota" value={section.externalQuota ?? '—'} />
            <Cell label="Kayıtlı" value={section.enrolledCount} />
            <Cell label="Beklemede" value={section.draftOrPendingCount} />
            <Cell label="Doluluk" value={`%${ratio}`} />
            <Cell label="Cross-list Grup" value={section.crossListGroupId ?? '—'} />
            <Cell label="Hoca" value={section.instructorName ?? '— Atanmamış —'} fullWidth />
          </div>

          {/* Aksiyon butonları */}
          <DialogFooter className="gap-2 flex-wrap sm:justify-start">
            <Button variant="outline" size="sm" className="h-8 text-xs gap-1.5" onClick={() => setEditOpen(true)}>
              <Pencil className="h-3.5 w-3.5" />
              Düzenle
            </Button>
            <Button
              variant="outline"
              size="sm"
              className="h-8 text-xs gap-1.5"
              onClick={() => setCloneOpen(true)}
              title="Bu şubeyi (hoca, kontenjan, kota dahil) kopyalayarak yeni şube oluştur"
            >
              <CopyIcon className="h-3.5 w-3.5" />
              Çoğalt
            </Button>
            <Button
              variant="outline"
              size="sm"
              className="h-8 text-xs gap-1.5"
              onClick={() => setAccessOpen(true)}
              title="Bu dersin erişim politikasını yönet"
            >
              <Users className="h-3.5 w-3.5" />
              Erişim
            </Button>
            <Button
              variant="outline"
              size="sm"
              className="h-8 text-xs gap-1.5 text-destructive hover:bg-destructive/10"
              onClick={() => setDeleteOpen(true)}
            >
              <Trash2 className="h-3.5 w-3.5" />
              Sil
            </Button>
            <Button variant="ghost" size="sm" className="h-8 text-xs ml-auto" onClick={() => onOpenChange(false)}>
              Kapat
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Sub-dialog'lar — yönetim penceresinin üzerine açılır */}
      <SectionEditDialog
        section={editOpen ? section : null}
        departmentId={departmentId}
        termId={termId}
        instructors={instructors}
        open={editOpen}
        onOpenChange={setEditOpen}
      />
      <AddSectionDialog
        section={cloneOpen ? section : null}
        departmentId={departmentId}
        termId={termId}
        open={cloneOpen}
        onOpenChange={setCloneOpen}
      />
      <SectionAccessDialog
        section={accessOpen ? section : null}
        termId={termId}
        open={accessOpen}
        onOpenChange={setAccessOpen}
      />
      <DeleteSectionDialog
        section={deleteOpen ? section : null}
        departmentId={departmentId}
        termId={termId}
        open={deleteOpen}
        onOpenChange={setDeleteOpen}
      />
    </>
  );
}

function Cell({
  label,
  value,
  fullWidth,
}: {
  label: string;
  value: string | number;
  fullWidth?: boolean;
}) {
  return (
    <div className={`p-2.5 bg-muted/30 rounded-md ${fullWidth ? 'col-span-3' : ''}`}>
      <div className="text-[10px] text-muted-foreground uppercase tracking-wide">{label}</div>
      <div className="text-sm font-medium mt-0.5">{value}</div>
    </div>
  );
}
