import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { useAdminPrograms } from '../hooks/useAdminLookup';
import { useExamRegulations, useDeleteExamRegulation, useCloneExamRegulation, useAttendancePolicy } from '../hooks/useExamRegulations';
import { ExamRegulationFormDialog } from '../components/exam-regulation/ExamRegulationFormDialog';
import type { ExamRegulation } from '../types/examRegulation';
import { TERM_LABELS, LEVEL_LABELS } from '../lib/examRegulationLabels';

const ATTENDANCE_METHOD_LABELS: Record<string, string> = {
  Separate: 'Teori/Uygulama Ayrı',
  TotalHours: 'Toplam Saate Göre',
  Passive: 'Pasif',
  MarkOnly: 'Sadece İşaretle',
};

export default function ExamRegulationManagementPage() {
  const programs = useAdminPrograms();
  const [programId, setProgramId] = useState<number | undefined>(undefined);
  const regulations = useExamRegulations(programId);
  const deleteMut = useDeleteExamRegulation();
  const cloneMut = useCloneExamRegulation();
  const attendancePolicy = useAttendancePolicy();

  const [dialogOpen, setDialogOpen] = useState(false);
  const [editTarget, setEditTarget] = useState<ExamRegulation | null>(null);

  const openNew = () => { setEditTarget(null); setDialogOpen(true); };
  const openEdit = (r: ExamRegulation) => { setEditTarget(r); setDialogOpen(true); };

  const clone = (r: ExamRegulation) => {
    const year = Number(window.prompt('Hedef yıl?', String(r.year + 1)));
    if (!year) return;
    cloneMut.mutate({ id: r.id, payload: { year } });
  };

  const remove = (r: ExamRegulation) => {
    if (window.confirm(`"${r.name}" silinsin mi?`)) deleteMut.mutate(r.id);
  };

  return (
    <div className="container mx-auto p-6 space-y-4">
      <div className="flex items-center justify-between gap-3">
        <div>
          <h1 className="text-xl font-semibold">Sınav Müfredatı (Sınav Yönergeleri)</h1>
          <p className="text-sm text-muted-foreground">Program bazında sınav ve değerlendirme kuralları.</p>
        </div>
        <Button onClick={openNew}>Yeni Yönerge</Button>
      </div>

      <div className="flex items-center gap-2">
        <select className="h-9 rounded-md border px-3 text-sm" value={programId ?? ''}
          onChange={(e) => setProgramId(e.target.value ? Number(e.target.value) : undefined)}>
          <option value="">Tüm programlar</option>
          {(programs.data ?? []).map((p) => <option key={p.id} value={p.id}>{p.name}</option>)}
        </select>
      </div>

      <Card>
        <CardContent className="p-0 overflow-x-auto">
          <table className="w-full text-sm">
            <thead className="border-b bg-muted/40">
              <tr>
                <th className="px-3 py-2 text-left">Öğr. Tipi</th>
                <th className="px-3 py-2 text-left">Dönem Tipi</th>
                <th className="px-3 py-2 text-center">Yıl</th>
                <th className="px-3 py-2 text-center">Aktif</th>
                <th className="px-3 py-2 text-left">Adı</th>
                <th className="px-3 py-2 text-center">Ara Sın.</th>
                <th className="px-3 py-2 text-center">YYS Sın.</th>
                <th className="px-3 py-2 text-center">En Az Ara %</th>
                <th className="px-3 py-2 text-center">En Az YYS %</th>
                <th className="px-3 py-2 text-right">İşlem</th>
              </tr>
            </thead>
            <tbody>
              {(regulations.data ?? []).map((r) => (
                <tr key={r.id} className="border-b last:border-0 hover:bg-muted/30 cursor-pointer" onClick={() => openEdit(r)}>
                  <td className="px-3 py-2">{LEVEL_LABELS[r.degreeLevel] ?? r.degreeLevel}</td>
                  <td className="px-3 py-2">{TERM_LABELS[r.termType] ?? r.termType}</td>
                  <td className="px-3 py-2 text-center">{r.year}</td>
                  <td className="px-3 py-2 text-center">{r.isActive ? '✓' : '—'}</td>
                  <td className="px-3 py-2">{r.name}</td>
                  <td className="px-3 py-2 text-center">{r.midtermCount}</td>
                  <td className="px-3 py-2 text-center">{r.finalCount}</td>
                  <td className="px-3 py-2 text-center">{r.minMidtermWeightPercent}</td>
                  <td className="px-3 py-2 text-center">{r.minFinalWeightPercent}</td>
                  <td className="px-3 py-2 text-right" onClick={(e) => e.stopPropagation()}>
                    <Button variant="outline" size="sm" className="mr-1" onClick={() => clone(r)}>Çoğalt</Button>
                    <Button variant="outline" size="sm" onClick={() => remove(r)}>Sil</Button>
                  </td>
                </tr>
              ))}
              {(regulations.data ?? []).length === 0 && (
                <tr><td colSpan={10} className="px-3 py-10 text-center text-muted-foreground">Henüz yönerge yok.</td></tr>
              )}
            </tbody>
          </table>
        </CardContent>
      </Card>

      <Card>
        <CardContent className="p-4 text-sm text-muted-foreground">
          {attendancePolicy.data ? (
            <>
              Devamsızlık Yöntemi:{' '}
              <strong>{ATTENDANCE_METHOD_LABELS[attendancePolicy.data.attendanceMethod] ?? attendancePolicy.data.attendanceMethod}</strong>
              {' · '}{attendancePolicy.data.termWeekCount} hafta · Teori{' '}
              <strong>%{attendancePolicy.data.theoryAbsenceLimitPercent}</strong> · Uygulama{' '}
              <strong>%{attendancePolicy.data.labAbsenceLimitPercent}</strong>{' '}
              (<strong>Sistem Ayarları</strong>'ndan yönetilir)
            </>
          ) : (
            <>
              Devamsızlık eşikleri (hafta sayısı, teori/uygulama oranı) tüm üniversite için ortak olup
              <strong> Sistem Ayarları</strong> sayfasından yönetilir.
            </>
          )}
        </CardContent>
      </Card>

      <ExamRegulationFormDialog open={dialogOpen} onOpenChange={setDialogOpen} editTarget={editTarget} />
    </div>
  );
}
