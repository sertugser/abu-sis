import {
  useFacultyDeletionSummary,
  type FacultyDto,
} from '../../hooks/useAdminAcademicStructure';
import { AcademicDeleteDialog } from './AcademicDeleteDialog';

interface FacultyDeleteDialogProps {
  faculty: FacultyDto | null;
  onClose: () => void;
  onConfirm: () => void;
  isPending: boolean;
}

export function FacultyDeleteDialog({ faculty, onClose, onConfirm, isPending }: FacultyDeleteDialogProps) {
  const { data: summary, isLoading, isError } = useFacultyDeletionSummary(faculty?.id ?? null);

  return (
    <AcademicDeleteDialog
      open={faculty !== null}
      title="Fakülteyi Sil"
      intro={<><strong className="text-foreground">{faculty?.name}</strong> fakültesini silmek üzeresiniz.</>}
      stats={summary && [
        { label: 'Bölüm', value: summary.departmentCount },
        { label: 'Program', value: summary.programCount },
        { label: 'Müfredat', value: summary.curriculumCount },
        { label: 'Öğrenci', value: summary.studentCount },
        { label: 'Öğretim Üyesi', value: summary.instructorCount },
      ]}
      canDelete={summary?.canDelete}
      isLoading={isLoading}
      isError={isError}
      emptyReason={<>Bu fakülteye bağlı hiçbir kayıt yok. Silme işlemi <strong>geri alınamaz</strong>.</>}
      blockReason={summary && (
        <>
          Bu fakülte silinemez: bağlı <strong>{summary.departmentCount} bölüm</strong> var
          {summary.studentCount > 0 && (
            <> ve bunlara kayıtlı <strong>{summary.studentCount} öğrenci</strong> bulunuyor</>
          )}
          . Önce bağlı bölümleri (ve altındaki program/müfredatları) kaldırmalısınız.
        </>
      )}
      onClose={onClose}
      onConfirm={onConfirm}
      isPending={isPending}
    />
  );
}
