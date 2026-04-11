import { useState } from 'react';
import { Link } from 'react-router-dom';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Progress } from '@/components/ui/progress';
import { Skeleton } from '@/components/ui/skeleton';
import { Textarea } from '@/components/ui/textarea';
import { AlertCircle, AlertTriangle, CheckCircle2, XCircle } from 'lucide-react';
import { useActiveTerm } from '../hooks/useActiveTerm';
import { useAdminFaculties } from '../hooks/useAdminAcademicStructure';
import {
  useScheduleApprovalStatus,
  useApproveSchedule,
  useRevokeScheduleApproval,
} from '../hooks/useScheduleApproval';

export default function ScheduleApprovalPage() {
  const { data: activeTerm } = useActiveTerm();
  const { data: faculties, isLoading: facultiesLoading } = useAdminFaculties();
  const [selectedFacultyId, setSelectedFacultyId] = useState<number | null>(null);
  const facultyId = selectedFacultyId ?? faculties?.[0]?.id ?? null;
  const termId = activeTerm?.id;

  const { data: status, isLoading, isError } = useScheduleApprovalStatus(facultyId, termId);
  const approveMutation = useApproveSchedule(facultyId ?? 0);
  const revokeMutation = useRevokeScheduleApproval(facultyId ?? 0);

  const [note, setNote] = useState('');

  const handleApprove = () => {
    approveMutation.mutate(note || undefined, {
      onSuccess: () => setNote(''),
    });
  };

  const handleRevoke = () => {
    if (confirm('Onayı geri almak istediğinize emin misiniz?')) {
      revokeMutation.mutate();
    }
  };

  if (facultiesLoading) return <Skeleton className="h-96 rounded-xl" />;

  if (!facultyId) {
    return (
      <Alert>
        <AlertCircle className="h-4 w-4" />
        <AlertTitle>Fakülte bulunamadı</AlertTitle>
        <AlertDescription>Sistemde tanımlı fakülte bulunmamaktadır.</AlertDescription>
      </Alert>
    );
  }

  return (
    <div className="space-y-6">
      {/* Faculty selector */}
      <div className="flex flex-wrap items-center gap-3">
        <label className="text-sm font-medium">Fakülte:</label>
        <select
          className="h-9 rounded-md border border-input bg-background px-3 text-sm w-full sm:w-auto sm:min-w-[300px]"
          value={facultyId}
          onChange={e => setSelectedFacultyId(Number(e.target.value))}
        >
          {(faculties ?? []).map(f => (
            <option key={f.id} value={f.id}>{f.code} — {f.name}</option>
          ))}
        </select>
        {activeTerm && (
          <Badge variant="outline">{activeTerm.name}</Badge>
        )}
      </div>

      {/* Status */}
      {isLoading ? (
        <Skeleton className="h-48 rounded-xl" />
      ) : isError ? (
        <Alert variant="destructive">
          <AlertCircle className="h-4 w-4" />
          <AlertDescription>Onay durumu yüklenemedi.</AlertDescription>
        </Alert>
      ) : status ? (
        <div className="space-y-4">
          <Card>
            <CardHeader>
              <CardTitle className="text-sm flex items-center gap-2">
                Onay Durumu
                {status.isApproved ? (
                  <Badge variant="success">Onaylandı</Badge>
                ) : (
                  <Badge variant="warning">Onaylanmadı</Badge>
                )}
              </CardTitle>
            </CardHeader>
            <CardContent className="space-y-3">
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4 text-sm">
                <div>
                  <span className="text-muted-foreground">Fakülte:</span>
                  <span className="ml-2 font-medium">{status.facultyName}</span>
                </div>
                <div>
                  <span className="text-muted-foreground">Dönem:</span>
                  <span className="ml-2 font-medium">{status.termName}</span>
                </div>
                <div>
                  <span className="text-muted-foreground">Toplam Şube:</span>
                  <span className="ml-2 font-medium">{status.totalSections}</span>
                </div>
                <div>
                  <span className="text-muted-foreground">Zamanlanan:</span>
                  <span className="ml-2 font-medium">{status.scheduledSections}</span>
                </div>
                {status.approvedAt && (
                  <div>
                    <span className="text-muted-foreground">Onay Tarihi:</span>
                    <span className="ml-2 font-medium">
                      {new Date(status.approvedAt).toLocaleString('tr-TR')}
                    </span>
                  </div>
                )}
                {status.approvedByName && (
                  <div>
                    <span className="text-muted-foreground">Onaylayan:</span>
                    <span className="ml-2 font-medium">{status.approvedByName}</span>
                  </div>
                )}
              </div>

              {/* Progress bar */}
              {status.totalSections > 0 && (
                <div className="flex items-center gap-3">
                  <Progress value={(status.scheduledSections / status.totalSections) * 100} className="w-48 h-2" />
                  <span className="text-sm">{status.scheduledSections}/{status.totalSections} programlandı</span>
                </div>
              )}

              {status.note && (
                <div className="text-sm">
                  <span className="text-muted-foreground">Not:</span>
                  <p className="mt-1 bg-muted p-2 rounded text-sm">{status.note}</p>
                </div>
              )}
            </CardContent>
          </Card>

          {/* Unscheduled warning */}
          {status.totalSections > status.scheduledSections && (
            <Alert variant="warning">
              <AlertTriangle className="h-4 w-4" />
              <AlertDescription>
                {status.totalSections - status.scheduledSections} şube henüz programlanmamış.
              </AlertDescription>
            </Alert>
          )}

          {/* Actions */}
          <Card>
            <CardHeader>
              <CardTitle className="text-sm">İşlemler</CardTitle>
            </CardHeader>
            <CardContent className="space-y-3">
              {!status.isApproved ? (
                <>
                  <div className="space-y-1">
                    <label className="text-xs font-medium">Onay Notu (Opsiyonel)</label>
                    <Textarea
                      placeholder="Onay ile ilgili not ekleyebilirsiniz..."
                      value={note}
                      onChange={e => setNote(e.target.value)}
                      rows={3}
                    />
                  </div>
                  <div className="flex items-center gap-3">
                    <Button onClick={handleApprove} disabled={approveMutation.isPending} className="gap-2">
                      <CheckCircle2 className="h-4 w-4" />
                      {approveMutation.isPending ? 'Onaylanıyor...' : 'Programı Onayla'}
                    </Button>
                    <Link to="/ders-programi-olusturma" className="text-sm text-blue-600 hover:underline">
                      Programı Görüntüle →
                    </Link>
                  </div>
                </>
              ) : (
                <div className="flex items-center gap-3">
                  <Button
                    variant="destructive"
                    onClick={handleRevoke}
                    disabled={revokeMutation.isPending}
                    className="gap-2"
                  >
                    <XCircle className="h-4 w-4" />
                    {revokeMutation.isPending ? 'Geri alınıyor...' : 'Onayı Geri Al'}
                  </Button>
                  <Link to="/ders-programi-olusturma" className="text-sm text-blue-600 hover:underline">
                    Programı Görüntüle →
                  </Link>
                </div>
              )}

              {approveMutation.isError && (
                <Alert variant="destructive">
                  <AlertCircle className="h-4 w-4" />
                  <AlertDescription>Onay işlemi başarısız oldu.</AlertDescription>
                </Alert>
              )}
              {approveMutation.isSuccess && (
                <Alert variant="success">
                  <CheckCircle2 className="h-4 w-4" />
                  <AlertDescription>Program başarıyla onaylandı.</AlertDescription>
                </Alert>
              )}
              {revokeMutation.isError && (
                <Alert variant="destructive">
                  <AlertCircle className="h-4 w-4" />
                  <AlertDescription>Onay geri alma işlemi başarısız oldu.</AlertDescription>
                </Alert>
              )}
            </CardContent>
          </Card>
        </div>
      ) : null}
    </div>
  );
}
