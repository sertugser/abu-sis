import {
  useDepartmentDeletionSummary,
  type DepartmentDto,
} from '../../hooks/useAdminAcademicStructure';
import { AcademicDeleteDialog } from './AcademicDeleteDialog';

interface DepartmentDeleteDialogProps {
  department: DepartmentDto | null;
  onClose: () => void;
  onConfirm: () => void;
  isPending: boolean;
}

export function DepartmentDeleteDialog({ department, onClose, onConfirm, isPending }: DepartmentDeleteDialogProps) {
  const { data: summary, isLoading, isError } = useDepartmentDeletionSummary(department?.id ?? null);

  return (
    <AcademicDeleteDialog
      open={department !== null}
      title="Bölümü Sil"
      intro={<><strong className="text-foreground">{department?.name}</strong> bölümünü silmek üzeresiniz.</>}
      stats={summary && [
        { label: 'Program', value: summary.programCount },
        { label: 'Müfredat', value: summary.curriculumCount },
        { label: 'Öğrenci', value: summary.studentCount },
        { label: 'Öğretim Üyesi', value: summary.instructorCount },
      ]}
      canDelete={summary?.canDelete}
      isLoading={isLoading}
      isError={isError}
      emptyReason={<>Bu bölüme bağlı program veya öğretim üyesi yok. Silme işlemi <strong>geri alınamaz</strong>.</>}
      blockReason={summary && (
        <>
          Bu bölüm silinemez:
          {summary.programCount > 0 && <> bağlı <strong>{summary.programCount} program</strong></>}
          {summary.programCount > 0 && summary.instructorCount > 0 && ' ve'}
          {summary.instructorCount > 0 && <> atanmış <strong>{summary.instructorCount} öğretim üyesi</strong></>}
          {' '}var. Önce bunları kaldırmalısınız.
        </>
      )}
      onClose={onClose}
      onConfirm={onConfirm}
      isPending={isPending}
    />
  );
}
