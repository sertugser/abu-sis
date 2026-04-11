import { useState } from 'react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Zap } from 'lucide-react';
import {
  useApplySchedule, useAutoSchedulePreview, useBatchUnassignSections,
} from '../../hooks/useFacultyPlanning';

export function AutoScheduleTab({ facultyId, termId }: { facultyId: number; termId: number | null }) {
  const previewMutation = useAutoSchedulePreview();
  const applyMutation = useApplySchedule();
  const batchUnassign = useBatchUnassignSections();
  const [selectedIds, setSelectedIds] = useState<Set<number>>(new Set());
  const [lastAppliedIds, setLastAppliedIds] = useState<number[]>([]);

  if (!termId) return null;

  const handlePreview = () => {
    previewMutation.mutate({ facultyId, termId });
  };

  const handleApplySelected = () => {
    const items = (previewMutation.data?.assignments ?? [])
      .filter(a => selectedIds.has(a.sectionId))
      .map(a => ({ sectionId: a.sectionId, classroomId: a.classroomId, scheduleSlots: a.scheduleSlots, instructorId: a.instructorId ?? undefined }));
    if (items.length > 0) {
      const request = { assignments: items };
      applyMutation.mutate(request, {
        onSuccess: () => setLastAppliedIds(request.assignments.map(a => a.sectionId)),
      });
    }
  };

  const handleApplyAll = () => {
    const items = (previewMutation.data?.assignments ?? [])
      .map(a => ({ sectionId: a.sectionId, classroomId: a.classroomId, scheduleSlots: a.scheduleSlots, instructorId: a.instructorId ?? undefined }));
    if (items.length > 0) {
      const request = { assignments: items };
      applyMutation.mutate(request, {
        onSuccess: () => setLastAppliedIds(request.assignments.map(a => a.sectionId)),
      });
    }
  };

  const toggleSelect = (id: number) => setSelectedIds(prev => {
    const next = new Set(prev);
    if (next.has(id)) { next.delete(id); } else { next.add(id); }
    return next;
  });

  return (
    <div className="space-y-4">
      <Card>
        <CardHeader className="flex flex-row items-center justify-between">
          <div>
            <CardTitle>Otomatik Zamanlama</CardTitle>
            <CardDescription>Zamanlanmamış şubeler için derslik ve saat önerisi üretir.</CardDescription>
          </div>
          <Button onClick={handlePreview} disabled={previewMutation.isPending}>
            <Zap className="h-4 w-4 mr-1" />
            {previewMutation.isPending ? 'Hesaplanıyor...' : 'Otomatik Ata'}
          </Button>
        </CardHeader>
      </Card>

      {previewMutation.data && (
        <>
          <div className="flex gap-2">
            <Badge variant="success">{previewMutation.data.successCount} başarılı</Badge>
            <Badge variant="destructive">{previewMutation.data.failedCount} başarısız</Badge>
          </div>

          {previewMutation.data.assignments.length > 0 && (
            <Card>
              <CardHeader className="flex flex-row items-center justify-between">
                <CardTitle className="text-sm">Önizleme</CardTitle>
                <div className="flex gap-2">
                  <Button size="sm" onClick={handleApplySelected} disabled={selectedIds.size === 0 || applyMutation.isPending}>
                    Seçilenleri Uygula ({selectedIds.size})
                  </Button>
                  <Button size="sm" onClick={handleApplyAll} disabled={applyMutation.isPending}>
                    Tümünü Uygula
                  </Button>
                </div>
              </CardHeader>
              <CardContent>
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead className="w-8" />
                      <TableHead>Ders</TableHead>
                      <TableHead>Bölüm</TableHead>
                      <TableHead>Derslik</TableHead>
                      <TableHead>Saat</TableHead>
                      <TableHead>Uyum</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {previewMutation.data.assignments.map(a => (
                      <TableRow key={a.sectionId}>
                        <TableCell>
                          <input type="checkbox" checked={selectedIds.has(a.sectionId)} onChange={() => toggleSelect(a.sectionId)} />
                        </TableCell>
                        <TableCell><span className="font-mono text-xs">{a.courseCode}</span> {a.courseName}</TableCell>
                        <TableCell>{a.departmentCode}</TableCell>
                        <TableCell>{a.classroomName}</TableCell>
                        <TableCell>{a.scheduleSummary}</TableCell>
                        <TableCell>%{(a.fitScore * 100).toFixed(0)}</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </CardContent>
            </Card>
          )}

          {previewMutation.data.failures.length > 0 && (
            <Card>
              <CardHeader><CardTitle className="text-sm text-destructive">Atanamayan Şubeler</CardTitle></CardHeader>
              <CardContent>
                {previewMutation.data.failures.map(f => (
                  <div key={f.sectionId} className="text-sm py-1">
                    <span className="font-mono">{f.courseCode}</span> — {f.reason}
                  </div>
                ))}
              </CardContent>
            </Card>
          )}

          {applyMutation.data && (
            <Alert variant={applyMutation.data.failedCount > 0 ? 'destructive' : 'default'}>
              <AlertTitle>Uygulama Sonucu</AlertTitle>
              <AlertDescription>
                {applyMutation.data.successCount} başarılı, {applyMutation.data.failedCount} başarısız
                {applyMutation.data.errors.map((e, i) => <p key={i} className="text-xs">{e}</p>)}
              </AlertDescription>
            </Alert>
          )}

          {lastAppliedIds.length > 0 && (
            <Button
              variant="outline"
              size="sm"
              onClick={() => {
                if (confirm(`${lastAppliedIds.length} şubenin atamasını kaldırmak istediğinize emin misiniz?`)) {
                  batchUnassign.mutate(lastAppliedIds, {
                    onSuccess: () => setLastAppliedIds([]),
                  });
                }
              }}
              disabled={batchUnassign.isPending}
            >
              {batchUnassign.isPending
                ? 'Geri alınıyor...'
                : `Son Uygulamayı Geri Al (${lastAppliedIds.length} şube)`}
            </Button>
          )}
        </>
      )}
    </div>
  );
}
