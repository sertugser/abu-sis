import { useRef, useState } from 'react';
import { useOutletContext } from 'react-router-dom';
import { type StudentCardOutletContext } from '../StudentCardPage';
import { usePermissions } from '../../../context/PermissionContext';
import {
  useStudentDocuments,
  useUploadStudentDocument,
  useDeleteStudentDocument,
  downloadStudentDocument,
  type StudentDocumentStatus,
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

const MAX_FILE_SIZE = 5_000_000;
const ACCEPT = 'application/pdf,image/png,image/jpeg';
const ALLOWED_TYPES = ['application/pdf', 'image/png', 'image/jpeg'];

function formatDate(iso: string | null): string {
  if (!iso) return '';
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return '';
  return date.toLocaleString('tr-TR', {
    day: '2-digit',
    month: 'short',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
}

/** Client-side guard mirroring the server limits; returns an error message or null. */
function validateFile(file: File): string | null {
  if (file.size > MAX_FILE_SIZE) return "Dosya 5MB'tan büyük olamaz.";
  if (!ALLOWED_TYPES.includes(file.type)) return 'Yalnızca PDF/JPEG/PNG.';
  return null;
}

interface DocumentRowProps {
  doc: StudentDocumentStatus;
  studentId: number;
  canEdit: boolean;
  onUpload: (documentTypeId: number, file: File) => void;
  onDelete: (doc: StudentDocumentStatus) => void;
  isUploading: boolean;
  isDeleting: boolean;
}

function DocumentRow({
  doc,
  studentId,
  canEdit,
  onUpload,
  onDelete,
  isUploading,
  isDeleting,
}: DocumentRowProps) {
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [downloading, setDownloading] = useState(false);
  const [downloadError, setDownloadError] = useState<string | null>(null);

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    // Reset the input so re-selecting the same file fires onChange again.
    e.target.value = '';
    if (!file) return;
    onUpload(doc.documentTypeId, file);
  };

  const handleDownload = async () => {
    if (doc.documentId == null) return;
    setDownloadError(null);
    setDownloading(true);
    try {
      await downloadStudentDocument(studentId, doc.documentId, doc.originalFileName);
    } catch {
      setDownloadError('Belge indirilemedi.');
    } finally {
      setDownloading(false);
    }
  };

  const busy = isUploading || isDeleting;

  return (
    <tr className="hover:bg-muted/20 transition-colors align-top">
      <td className="px-4 py-3">
        <div className="flex items-center gap-2">
          <span className="font-medium text-foreground">{doc.name}</span>
          {doc.isRequired && <Badge variant="warning">Zorunlu</Badge>}
        </div>
      </td>
      <td className="px-4 py-3">
        {doc.uploaded ? (
          <div className="space-y-0.5">
            <Badge variant="success">Yüklendi</Badge>
            {doc.originalFileName && (
              <div className="text-xs text-muted-foreground">{doc.originalFileName}</div>
            )}
            {doc.uploadedAt && (
              <div className="text-xs text-muted-foreground">{formatDate(doc.uploadedAt)}</div>
            )}
          </div>
        ) : doc.isRequired ? (
          <Badge variant="destructive">Eksik</Badge>
        ) : (
          <span className="text-muted-foreground">—</span>
        )}
      </td>
      <td className="px-4 py-3">
        <div className="flex flex-wrap items-center gap-2">
          {doc.uploaded ? (
            <>
              <Button
                variant="outline"
                size="sm"
                onClick={() => void handleDownload()}
                disabled={downloading}
              >
                {downloading ? 'İndiriliyor...' : 'İndir'}
              </Button>
              {canEdit && (
                <>
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() => fileInputRef.current?.click()}
                    disabled={busy}
                  >
                    {isUploading ? 'Yükleniyor...' : 'Değiştir'}
                  </Button>
                  <Button
                    variant="destructive"
                    size="sm"
                    onClick={() => onDelete(doc)}
                    disabled={busy}
                  >
                    {isDeleting ? 'Siliniyor...' : 'Sil'}
                  </Button>
                </>
              )}
            </>
          ) : (
            canEdit && (
              <Button
                variant="outline"
                size="sm"
                onClick={() => fileInputRef.current?.click()}
                disabled={busy}
              >
                {isUploading ? 'Yükleniyor...' : 'Yükle'}
              </Button>
            )
          )}
          {canEdit && (
            <input
              ref={fileInputRef}
              type="file"
              accept={ACCEPT}
              className="hidden"
              onChange={handleFileChange}
            />
          )}
        </div>
        {downloadError && <p className="mt-1 text-xs text-destructive">{downloadError}</p>}
      </td>
    </tr>
  );
}

export default function DocumentsPanel() {
  const context = useOutletContext<StudentCardOutletContext | null>();
  const { hasPermission } = usePermissions();
  const canEdit = hasPermission('menu.ogrenci-rehberi.edit-documents');

  const studentId = context?.student.id ?? null;
  const { data, isLoading } = useStudentDocuments(studentId);
  const uploadDocument = useUploadStudentDocument();
  const deleteDocument = useDeleteStudentDocument();

  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);
  const [pendingTypeId, setPendingTypeId] = useState<number | null>(null);
  const [confirmDoc, setConfirmDoc] = useState<StudentDocumentStatus | null>(null);

  if (!context) return null;
  const { student } = context;

  const handleUpload = async (documentTypeId: number, file: File) => {
    setError(null);
    setSuccess(null);
    const validationError = validateFile(file);
    if (validationError) {
      setError(validationError);
      return;
    }
    setPendingTypeId(documentTypeId);
    try {
      await uploadDocument.mutateAsync({ studentId: student.id, documentTypeId, file });
      setSuccess('Belge yüklendi.');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Belge yüklenemedi.');
    } finally {
      setPendingTypeId(null);
    }
  };

  const handleDeleteConfirm = async () => {
    if (!confirmDoc || confirmDoc.documentId == null) return;
    setError(null);
    setSuccess(null);
    const documentId = confirmDoc.documentId;
    setConfirmDoc(null);
    try {
      await deleteDocument.mutateAsync({ studentId: student.id, documentId });
      setSuccess('Belge silindi.');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Belge silinemedi.');
    }
  };

  if (isLoading) {
    return (
      <div className="bg-white border border-border rounded-xl px-4 py-8 text-center text-muted-foreground">
        Yükleniyor...
      </div>
    );
  }

  const documents = data ?? [];
  const missingRequired = documents.filter((d) => d.isRequired && !d.uploaded).length;

  return (
    <div className="space-y-4">
      {missingRequired > 0 ? (
        <p className="text-sm font-medium text-destructive">
          Eksik zorunlu belge: {missingRequired}
        </p>
      ) : (
        <p className="text-sm font-medium text-emerald-600">Tüm zorunlu belgeler tamam</p>
      )}

      {error && <p className="text-sm text-destructive">{error}</p>}
      {success && <p className="text-sm text-emerald-600">{success}</p>}

      {documents.length === 0 ? (
        <div className="bg-white border border-border rounded-xl px-4 py-8 text-center text-muted-foreground">
          Belge türü tanımlı değil.
        </div>
      ) : (
        <div className="bg-white border border-border rounded-xl overflow-hidden">
          <table className="w-full text-sm">
            <thead className="bg-muted/40 border-b border-border">
              <tr>
                <th className="text-left px-4 py-3 font-semibold text-muted-foreground">
                  Belge Türü
                </th>
                <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Durum</th>
                <th className="text-left px-4 py-3 font-semibold text-muted-foreground">İşlemler</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-border">
              {documents.map((doc) => (
                <DocumentRow
                  key={doc.documentTypeId}
                  doc={doc}
                  studentId={student.id}
                  canEdit={canEdit}
                  onUpload={(typeId, file) => void handleUpload(typeId, file)}
                  onDelete={(d) => setConfirmDoc(d)}
                  isUploading={uploadDocument.isPending && pendingTypeId === doc.documentTypeId}
                  isDeleting={
                    deleteDocument.isPending && deleteDocument.variables?.documentId === doc.documentId
                  }
                />
              ))}
            </tbody>
          </table>
        </div>
      )}

      <AlertDialog open={confirmDoc != null} onOpenChange={(open) => !open && setConfirmDoc(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Belgeyi sil</AlertDialogTitle>
            <AlertDialogDescription>
              &apos;{confirmDoc?.name}&apos; belgesini silmek üzeresiniz. Bu işlem geri alınamaz.
              Onaylıyor musunuz?
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
