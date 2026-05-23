import { useMemo } from 'react';
import type { ClassroomDashboard } from '@/types/scheduling';

const DAY_LABELS = ['Pzt', 'Sal', 'Çar', 'Per', 'Cum'];

interface ClassroomPrintReportProps {
  dashboard: ClassroomDashboard;
  termName: string;
  conflictsCount: number;
  filterSummary?: string;
}

export default function ClassroomPrintReport({ dashboard, termName, conflictsCount, filterSummary }: ClassroomPrintReportProps) {
  const slots = useMemo(() => {
    const slotMap = new Map<number, { slotOrder: number; label: string }>();
    for (const cell of dashboard.heatmap) {
      if (!slotMap.has(cell.slotOrder)) {
        slotMap.set(cell.slotOrder, { slotOrder: cell.slotOrder, label: cell.label });
      }
    }
    return [...slotMap.values()].sort((a, b) => a.slotOrder - b.slotOrder);
  }, [dashboard.heatmap]);

  const cellMap = new Map(dashboard.heatmap.map(h => [`${h.day}-${h.slotOrder}`, h]));

  return (
    <div className="p-8 max-w-4xl mx-auto bg-white print:p-4">
      <style>{`
        @media print {
          body * { visibility: hidden; }
          .print-report, .print-report * { visibility: visible; }
          .print-report { position: absolute; left: 0; top: 0; width: 100%; }
          .no-print { display: none !important; }
        }
      `}</style>

      <div className="print-report space-y-6">
        <div className="text-center border-b pb-4">
          <h1 className="text-lg font-bold">ANKARA BİLİM ÜNİVERSİTESİ</h1>
          <h2 className="text-base font-semibold mt-1">Derslik Doluluk Raporu</h2>
          <p className="text-sm text-muted-foreground mt-1">{termName}</p>
          {filterSummary && <p className="text-sm text-muted-foreground">{filterSummary}</p>}
          <p className="text-xs text-muted-foreground mt-1">
            Oluşturma: {new Date().toLocaleDateString('tr-TR')}
          </p>
        </div>

        <div className="grid grid-cols-3 gap-4 text-center">
          <div className="border rounded-lg p-3">
            <p className="text-2xl font-bold">{dashboard.totalClassrooms}</p>
            <p className="text-xs text-muted-foreground">Toplam Derslik</p>
          </div>
          <div className="border rounded-lg p-3">
            <p className="text-2xl font-bold">%{dashboard.occupancyRate}</p>
            <p className="text-xs text-muted-foreground">Ortalama Doluluk</p>
          </div>
          <div className="border rounded-lg p-3">
            <p className="text-2xl font-bold">{conflictsCount}</p>
            <p className="text-xs text-muted-foreground">Çakışma</p>
          </div>
        </div>

        <div>
          <h3 className="text-sm font-semibold mb-2">Doluluk Tablosu (Dolu Derslik Sayısı)</h3>
          <table className="w-full border-collapse border text-xs">
            <thead>
              <tr>
                <th className="border p-1.5 text-left">Slot</th>
                {DAY_LABELS.map(d => (
                  <th key={d} className="border p-1.5 text-center">{d}</th>
                ))}
              </tr>
            </thead>
            <tbody>
              {slots.map(slot => (
                <tr key={slot.slotOrder}>
                  <td className="border p-1.5 font-mono">{slot.label}</td>
                  {DAY_LABELS.map((_, dayIdx) => {
                    const cell = cellMap.get(`${dayIdx}-${slot.slotOrder}`);
                    return (
                      <td key={dayIdx} className="border p-1.5 text-center">
                        {cell?.usedClassrooms ?? 0}
                      </td>
                    );
                  })}
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        <div>
          <h3 className="text-sm font-semibold mb-2">Derslik Bazında Özet</h3>
          <table className="w-full border-collapse border text-xs">
            <thead>
              <tr>
                <th className="border p-1.5 text-left">Derslik</th>
                <th className="border p-1.5 text-left">Tip</th>
                <th className="border p-1.5 text-right">Kapasite</th>
                <th className="border p-1.5 text-right">Ders</th>
                <th className="border p-1.5 text-right">Doluluk</th>
              </tr>
            </thead>
            <tbody>
              {dashboard.classrooms.map(c => {
                const courseCount = new Set(c.schedules.map(s => s.courseCode)).size;
                const occ = c.weeklyOccupancyRate;
                return (
                  <tr key={c.id}>
                    <td className="border p-1.5">{c.name}</td>
                    <td className="border p-1.5">{c.type}</td>
                    <td className="border p-1.5 text-right">{c.capacity}</td>
                    <td className="border p-1.5 text-right">{courseCount}</td>
                    <td className="border p-1.5 text-right">%{occ}</td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}
