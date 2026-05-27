import {
  useProgramDeletionSummary,
  type ProgramDto,
} from '../../hooks/useAdminAcademicStructure';
import { AcademicDeleteDialog } from './AcademicDeleteDialog';

interface ProgramDeleteDialogProps {
  program: ProgramDto | null;
  onClose: () => void;
  onConfirm: () => void;
  isPending: boolean;
}

export function ProgramDeleteDialog({ program, onClose, onConfirm, isPending }: ProgramDeleteDialogProps) {
  const { data: summary, isLoading, isError } = useProgramDeletionSummary(program?.id ?? null);

  return (
    <AcademicDeleteDialog
      open={program !== null}
      title="Programı Sil"
      intro={<><strong className="text-foreground">{program?.name}</strong> programını silmek üzeresiniz.</>}
      stats={summary && [
        { label: 'Müfredat', value: summary.curriculumCount },
        { label: 'Öğrenci', value: summary.studentCount },
      ]}
      canDelete={summary?.canDelete}
      isLoading={isLoading}
      isError={isError}
      emptyReason={
        summary && summary.curriculumCount > 0
          ? <>Bu programa kayıtlı öğrenci yok. {summary.curriculumCount} müfredat da birlikte silinecek. Silme işlemi <strong>geri alınamaz</strong>.</>
          : <>Bu programa bağlı kayıt yok. Silme işlemi <strong>geri alınamaz</strong>.</>
      }
      blockReason={summary && (
        <>
          Bu program silinemez: kayıtlı <strong>{summary.studentCount} öğrenci</strong> var.
          Önce öğrenci kayıtları başka programa taşınmalı veya kapatılmalı.
        </>
      )}
      onClose={onClose}
      onConfirm={onConfirm}
      isPending={isPending}
    />
  );
}
