import { useState } from 'react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { Textarea } from '@/components/ui/textarea';
import { AlertCircle, CheckCircle2, XCircle } from 'lucide-react';
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
    if (confirm('Onayi geri almak istediginize emin misiniz?')) {
      revokeMutation.mutate();
    }
  };

  if (facultiesLoading) return <Skeleton className="h-96 rounded-xl" />;

  if (!facultyId) {
    return (
      <Alert>
        <AlertCircle className="h-4 w-4" />
        <AlertTitle>Fakulte bulunamadi</AlertTitle>
        <AlertDescription>Sistemde tanimli fakulte bulunmamaktadir.</AlertDescription>
      </Alert>
    );
  }

  return (
    <div className="space-y-6">
      <h1 className="text-xl font-bold">Program Onayi</h1>

      {/* Faculty selector */}
      <div className="flex items-center gap-3">
        <label className="text-sm font-medium">Fakulte:</label>
        <select
          className="h-9 rounded-md border border-input bg-background px-3 text-sm min-w-[300px]"
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
          <AlertDescription>Onay durumu yuklenemedi.</AlertDescription>
        </Alert>
      ) : status ? (
        <div className="space-y-4">
          <Card>
            <CardHeader>
              <CardTitle className="text-sm flex items-center gap-2">
                Onay Durumu
                {status.isApproved ? (
                  <Badge variant="success">Onaylandi</Badge>
                ) : (
                  <Badge variant="warning">Onaylanmadi</Badge>
                )}
              </CardTitle>
            </CardHeader>
            <CardContent className="space-y-3">
              <div className="grid grid-cols-2 gap-4 text-sm">
                <div>
                  <span className="text-muted-foreground">Fakulte:</span>
                  <span className="ml-2 font-medium">{status.facultyName}</span>
                </div>
                <div>
                  <span className="text-muted-foreground">Donem:</span>
                  <span className="ml-2 font-medium">{status.termName}</span>
                </div>
                <div>
                  <span className="text-muted-foreground">Toplam Sube:</span>
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
              {status.note && (
                <div className="text-sm">
                  <span className="text-muted-foreground">Not:</span>
                  <p className="mt-1 bg-muted p-2 rounded text-sm">{status.note}</p>
                </div>
              )}
            </CardContent>
          </Card>

          {/* Actions */}
          <Card>
            <CardHeader>
              <CardTitle className="text-sm">Islemler</CardTitle>
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
                  <Button onClick={handleApprove} disabled={approveMutation.isPending} className="gap-2">
                    <CheckCircle2 className="h-4 w-4" />
                    {approveMutation.isPending ? 'Onaylaniyor...' : 'Programi Onayla'}
                  </Button>
                </>
              ) : (
                <Button
                  variant="destructive"
                  onClick={handleRevoke}
                  disabled={revokeMutation.isPending}
                  className="gap-2"
                >
                  <XCircle className="h-4 w-4" />
                  {revokeMutation.isPending ? 'Geri aliniyor...' : 'Onayi Geri Al'}
                </Button>
              )}

              {approveMutation.isError && (
                <Alert variant="destructive">
                  <AlertCircle className="h-4 w-4" />
                  <AlertDescription>Onay islemi basarisiz oldu.</AlertDescription>
                </Alert>
              )}
              {approveMutation.isSuccess && (
                <Alert variant="success">
                  <CheckCircle2 className="h-4 w-4" />
                  <AlertDescription>Program basariyla onaylandi.</AlertDescription>
                </Alert>
              )}
              {revokeMutation.isError && (
                <Alert variant="destructive">
                  <AlertCircle className="h-4 w-4" />
                  <AlertDescription>Onay geri alma islemi basarisiz oldu.</AlertDescription>
                </Alert>
              )}
            </CardContent>
          </Card>
        </div>
      ) : null}
    </div>
  );
}
