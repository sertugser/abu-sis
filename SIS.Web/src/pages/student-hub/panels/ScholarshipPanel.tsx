import { useState } from 'react';
import { useOutletContext } from 'react-router-dom';
import { type StudentCardOutletContext } from '../StudentCardPage';
import { usePermissions } from '../../../context/PermissionContext';
import {
  useScholarships,
  useCreateScholarship,
  useUpdateScholarship,
  useDeleteScholarship,
  type ScholarshipDetail,
  type UpsertScholarship,
} from '../../../hooks/useStudentCard';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import {
  AlertDialog,
  AlertDialogContent,
  AlertDialogHeader,
  AlertDialogFooter,
  AlertDialogTitle,
  AlertDialogDescription,
  AlertDialogAction,
  AlertDialogCancel,
} from '@/components/ui/alert-dialog';
import ScholarshipFormDialog from './ScholarshipFormDialog';
import { SCHOLARSHIP_TYPE_LABELS } from './scholarshipConstants';

function formatDate(iso: string | null): string {
  if (!iso) return '';
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return '';
  return date.toLocaleDateString('tr-TR', {
    day: '2-digit',
    month: 'short',
    year: 'numeric',
  });
}

/** Render the start–end period as a single cell, '—' when both are absent. */
function formatPeriod(startDate: string | null, endDate: string | null): string {
  const start = formatDate(startDate);
  const end = formatDate(endDate);
  if (!start && !end) return '—';
  return `${start || '…'} – ${end || '…'}`;
}

export default function ScholarshipPanel() {
  const context = useOutletContext<StudentCardOutletContext | null>();
  const { hasPermission } = usePermissions();
  const canEdit = hasPermission('menu.ogrenci-rehberi.edit-scholarship');

  const studentId = context?.student.id ?? null;
  const { data, isLoading } = useScholarships(studentId);
  const createScholarship = useCreateScholarship();
  const updateScholarship = useUpdateScholarship();
  const deleteScholarship = useDeleteScholarship();

  const [formOpen, setFormOpen] = useState(false);
  const [editRecord, setEditRecord] = useState<ScholarshipDetail | null>(null);
  const [formError, setFormError] = useState<string | null>(null);
  const [confirmRecord, setConfirmRecord] = useState<ScholarshipDetail | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);

  if (!context) return null;
  const { student } = context;

  const openAdd = () => {
    setEditRecord(null);
    setFormError(null);
    setSuccess(null);
    setFormOpen(true);
  };

  const openEdit = (record: ScholarshipDetail) => {
    setEditRecord(record);
    setFormError(null);
    setSuccess(null);
    setFormOpen(true);
  };

  // Clear any stale status lines when the dialog is dismissed (Escape /
  // outside-click / "Vazgeç") so a previous success/error does not flash on the
  // next open.
  const handleFormOpenChange = (open: boolean) => {
    if (!open) {
      setSuccess(null);
      setError(null);
      setFormError(null);
    }
    setFormOpen(open);
  };

  const handleSubmit = async (dto: UpsertScholarship) => {
    setFormError(null);
    setError(null);
    try {
      if (editRecord) {
        await updateScholarship.mutateAsync({ studentId: student.id, id: editRecord.id, dto });
        setSuccess('Burs kaydı güncellendi.');
      } else {
        await createScholarship.mutateAsync({ studentId: student.id, dto });
        setSuccess('Burs kaydı eklendi.');
      }
      setFormOpen(false);
    } catch (err) {
      setFormError(err instanceof Error ? err.message : 'Burs kaydı kaydedilemedi.');
    }
  };

  const handleDeleteConfirm = async () => {
    if (!confirmRecord) return;
    const id = confirmRecord.id;
    setConfirmRecord(null);
    setError(null);
    setSuccess(null);
    try {
      await deleteScholarship.mutateAsync({ studentId: student.id, id });
      setSuccess('Burs kaydı silindi.');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Burs kaydı silinemedi.');
    }
  };

  const records = data ?? [];
  const isSaving = createScholarship.isPending || updateScholarship.isPending;

  return (
    <div className="space-y-4">
      <div className="rounded-lg border border-border bg-muted/20 px-4 py-3">
        <p className="text-sm font-medium text-foreground">
          {student.isScholarshipStudent
            ? 'Akademik kayıtta burslu olarak işaretli.'
            : 'Akademik kayıtta burssuz olarak işaretli.'}
        </p>
        <p className="mt-1 text-xs text-muted-foreground">
          Bu işaret Akademik Bilgiler sayfasından yönetilir ve aşağıdaki detaylı burs
          kayıtlarından bağımsızdır.
        </p>
      </div>

      {error && <p className="text-sm text-destructive">{error}</p>}
      {success && <p className="text-sm text-emerald-600">{success}</p>}

      {canEdit && (
        <div className="flex justify-end">
          <Button onClick={openAdd}>Burs Ekle</Button>
        </div>
      )}

      {isLoading ? (
        <div className="rounded-xl border border-border bg-white px-4 py-8 text-center text-muted-foreground">
          Yükleniyor...
        </div>
      ) : records.length === 0 ? (
        <div className="rounded-xl border border-border bg-white px-4 py-8 text-center text-muted-foreground">
          Burs kaydı yok.
        </div>
      ) : (
        <div className="overflow-hidden rounded-xl border border-border bg-white">
          <table className="w-full text-sm">
            <thead className="border-b border-border bg-muted/40">
              <tr>
                <th className="px-4 py-3 text-left font-semibold text-muted-foreground">Tür</th>
                <th className="px-4 py-3 text-left font-semibold text-muted-foreground">Oran</th>
                <th className="px-4 py-3 text-left font-semibold text-muted-foreground">Kaynak</th>
                <th className="px-4 py-3 text-left font-semibold text-muted-foreground">Dönem</th>
                <th className="px-4 py-3 text-left font-semibold text-muted-foreground">Durum</th>
                {canEdit && (
                  <th className="px-4 py-3 text-left font-semibold text-muted-foreground">
                    İşlemler
                  </th>
                )}
              </tr>
            </thead>
            <tbody className="divide-y divide-border">
              {records.map((record) => (
                <tr key={record.id} className="align-top transition-colors hover:bg-muted/20">
                  <td className="px-4 py-3 font-medium text-foreground">
                    {record.typeLabel || SCHOLARSHIP_TYPE_LABELS[record.type] || record.type}
                  </td>
                  <td className="px-4 py-3">
                    {record.ratePercent != null ? `%${record.ratePercent}` : '—'}
                  </td>
                  <td className="px-4 py-3">{record.source ?? '—'}</td>
                  <td className="px-4 py-3">{formatPeriod(record.startDate, record.endDate)}</td>
                  <td className="px-4 py-3">
                    {record.isActive ? (
                      <Badge variant="success">Aktif</Badge>
                    ) : (
                      <Badge variant="secondary">Pasif</Badge>
                    )}
                  </td>
                  {canEdit && (
                    <td className="px-4 py-3">
                      <div className="flex flex-wrap items-center gap-2">
                        <Button variant="outline" size="sm" onClick={() => openEdit(record)}>
                          Düzenle
                        </Button>
                        <Button
                          variant="destructive"
                          size="sm"
                          onClick={() => setConfirmRecord(record)}
                          disabled={
                            deleteScholarship.isPending &&
                            deleteScholarship.variables?.id === record.id
                          }
                        >
                          {deleteScholarship.isPending &&
                          deleteScholarship.variables?.id === record.id
                            ? 'Siliniyor...'
                            : 'Sil'}
                        </Button>
                      </div>
                    </td>
                  )}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {canEdit && (
        <ScholarshipFormDialog
          open={formOpen}
          record={editRecord}
          isSaving={isSaving}
          error={formError}
          onOpenChange={handleFormOpenChange}
          onSubmit={(dto) => void handleSubmit(dto)}
        />
      )}

      <AlertDialog
        open={confirmRecord != null}
        onOpenChange={(open) => !open && setConfirmRecord(null)}
      >
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Burs kaydını sil</AlertDialogTitle>
            <AlertDialogDescription>
              &apos;
              {confirmRecord?.typeLabel ||
                (confirmRecord ? SCHOLARSHIP_TYPE_LABELS[confirmRecord.type] : '')}
              &apos; burs kaydını silmek üzeresiniz. Bu işlem geri alınamaz. Onaylıyor musunuz?
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Vazgeç</AlertDialogCancel>
            <AlertDialogAction onClick={() => void handleDeleteConfirm()}>Sil</AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}
