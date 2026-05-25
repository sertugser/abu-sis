import { useState } from 'react';
import { useOutletContext } from 'react-router-dom';
import { type StudentCardOutletContext } from '../StudentCardPage';
import { usePermissions } from '../../../context/PermissionContext';
import {
  useIssuedDocuments,
  useGenerateStudentInfoForm,
  useGenerateEnrollmentCertificate,
} from '../../../hooks/useStudentCard';
import { Button } from '@/components/ui/button';

function formatDate(iso: string | null): string {
  if (!iso) return '—';
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return '—';
  return date.toLocaleString('tr-TR', {
    day: '2-digit',
    month: 'short',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
}

export default function IssuedDocumentsPanel() {
  const context = useOutletContext<StudentCardOutletContext | null>();
  const { hasPermission } = usePermissions();
  const canGenerate = hasPermission('menu.ogrenci-rehberi.generate-documents');

  const studentId = context?.student.id ?? null;
  const { data, isLoading } = useIssuedDocuments(studentId);
  const generateInfoForm = useGenerateStudentInfoForm();
  const generateCertificate = useGenerateEnrollmentCertificate();

  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);

  if (!context) return null;
  const { student } = context;

  const handleGenerate = async (kind: 'info-form' | 'certificate') => {
    setError(null);
    setSuccess(null);
    try {
      if (kind === 'info-form') {
        await generateInfoForm.mutateAsync({ studentId: student.id });
      } else {
        await generateCertificate.mutateAsync({ studentId: student.id });
      }
      setSuccess('Belge üretildi ve indirildi.');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Belge üretilemedi.');
    }
  };

  const documents = data ?? [];

  return (
    <div className="space-y-4">
      {canGenerate && (
        <div className="bg-white border border-border rounded-xl p-4 space-y-3">
          <h3 className="text-sm font-semibold text-foreground">Belge Üret</h3>
          <div className="flex flex-wrap items-center gap-2">
            <Button
              variant="outline"
              size="sm"
              onClick={() => void handleGenerate('info-form')}
              disabled={generateInfoForm.isPending}
            >
              {generateInfoForm.isPending ? 'Üretiliyor...' : 'Öğrenci Bilgi Formu'}
            </Button>
            <Button
              variant="outline"
              size="sm"
              onClick={() => void handleGenerate('certificate')}
              disabled={generateCertificate.isPending}
            >
              {generateCertificate.isPending ? 'Üretiliyor...' : 'Öğrenci Belgesi'}
            </Button>
          </div>
          {error && <p className="text-sm text-destructive">{error}</p>}
          {success && <p className="text-sm text-emerald-600">{success}</p>}
        </div>
      )}

      <div className="space-y-2">
        <h3 className="text-sm font-semibold text-foreground">Verilen Belgeler</h3>
        {isLoading ? (
          <div className="bg-white border border-border rounded-xl px-4 py-8 text-center text-muted-foreground">
            Yükleniyor...
          </div>
        ) : documents.length === 0 ? (
          <div className="bg-white border border-border rounded-xl px-4 py-8 text-center text-muted-foreground">
            Henüz belge üretilmedi.
          </div>
        ) : (
          <div className="bg-white border border-border rounded-xl overflow-hidden">
            <table className="w-full text-sm">
              <thead className="bg-muted/40 border-b border-border">
                <tr>
                  <th className="text-left px-4 py-3 font-semibold text-muted-foreground">
                    Belge Türü
                  </th>
                  <th className="text-left px-4 py-3 font-semibold text-muted-foreground">
                    Belge No
                  </th>
                  <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Tarih</th>
                  <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Veren</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-border">
                {documents.map((doc) => (
                  <tr key={doc.id} className="hover:bg-muted/20 transition-colors">
                    <td className="px-4 py-3 font-medium text-foreground">{doc.kindLabel}</td>
                    <td className="px-4 py-3 text-muted-foreground">{doc.documentNumber ?? '—'}</td>
                    <td className="px-4 py-3 text-muted-foreground">{formatDate(doc.issuedAt)}</td>
                    <td className="px-4 py-3 text-muted-foreground">{doc.issuedByName ?? '—'}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
}
