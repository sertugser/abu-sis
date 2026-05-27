import { CalendarRange } from 'lucide-react';
import { fmt, utcDate } from '@/lib/calendarGenerator/dates';
import { SEMESTERS } from '@/lib/calendarGenerator/semesters';
import { buildExamDayCounts, buildSemesterWeekAnalyses } from '@/lib/calendarGenerator/eventRules';
import type { CalendarView } from '@/lib/calendarGenerator/types';

interface TermSummaryCardsProps {
  current: CalendarView;
}

export function TermSummaryCards({ current }: TermSummaryCardsProps) {
  const examDayCounts = buildExamDayCounts(current.events);
  const analyses = buildSemesterWeekAnalyses(current.events);
  const analysesBySemester = new Map(analyses.map(analysis => [analysis.semester, analysis]));

  return (
    <div className="rounded-xl border border-slate-200 bg-white p-3 shadow-sm">
      <h2 className="mb-3 flex items-center gap-2 text-sm font-semibold text-slate-900">
        <CalendarRange className="h-4 w-4 text-[#003366]" />
        Dönem Özeti
      </h2>
      <div className="overflow-x-auto rounded-md border border-slate-200">
        <table className="w-full text-left text-sm text-slate-600">
          <thead className="bg-slate-50 text-xs uppercase text-slate-500 border-b border-slate-200">
            <tr>
              <th className="px-3 py-2 font-medium">Dönem</th>
              <th className="px-3 py-2 font-medium">Tarihler</th>
              <th className="px-3 py-2 font-medium">Ders / Tatil / Sınav (Gün)</th>
              <th className="px-3 py-2 font-medium">Vize Öncesi</th>
              <th className="px-3 py-2 font-medium">Vize Haftası</th>
              <th className="px-3 py-2 font-medium">Vize Sonrası</th>
              <th className="px-3 py-2 font-medium">Final Haftası</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-200 bg-white">
            {current.termSummaries.map(summary => {
              const analysis = analysesBySemester.get(summary.semester);
              const isSummer = summary.semester === 'Summer';
              const examDays = examDayCounts[summary.semester] ?? summary.examDays;
              return (
                <tr key={summary.semester} className="hover:bg-slate-50 transition-colors">
                  <td className="whitespace-nowrap px-3 py-2 font-medium text-slate-900">
                    {SEMESTERS.find(s => s.value === summary.semester)?.label}
                  </td>
                  <td className="whitespace-nowrap px-3 py-2">
                    {fmt(summary.termStart)} - {fmt(summary.termEnd)}
                  </td>
                  <td className="whitespace-nowrap px-3 py-2">
                    <span className="font-medium text-slate-700">{summary.teachingDays}</span> /{' '}
                    <span className="text-slate-500">{summary.holidayDaysInTerm}</span> /{' '}
                    <span className="text-red-600">{examDays}</span>
                  </td>
                  <td className="whitespace-nowrap px-3 py-2">
                    {isSummer || !analysis ? '-' : (
                      <span title={analysis.midtermStart ? `Vize Başlangıcı: ${fmt(utcDate(analysis.midtermStart))}` : 'Tarih yok'} className={analysis.midtermStart ? 'cursor-help border-b border-dotted border-slate-400' : ''}>
                        {analysis.weeksBeforeMidterm ?? '-'} hafta
                      </span>
                    )}
                  </td>
                  <td className="whitespace-nowrap px-3 py-2">
                    {isSummer || !analysis ? '-' : (
                      <span title={analysis.midtermStart && analysis.midtermEnd ? `${fmt(utcDate(analysis.midtermStart))} - ${fmt(utcDate(analysis.midtermEnd))}` : 'Tarih yok'} className={analysis.midtermStart ? 'cursor-help border-b border-dotted border-slate-400' : ''}>
                        {analysis.midtermWeek ? `${analysis.midtermWeek}. hafta` : '-'}
                      </span>
                    )}
                  </td>
                  <td className="whitespace-nowrap px-3 py-2">
                    {isSummer || !analysis ? '-' : (
                      <span title={analysis.finalStart ? `Final Başlangıcı: ${fmt(utcDate(analysis.finalStart))}` : 'Tarih yok'} className={analysis.finalStart ? 'cursor-help border-b border-dotted border-slate-400' : ''}>
                        {analysis.weeksAfterMidterm ?? '-'} hafta
                      </span>
                    )}
                  </td>
                  <td className="whitespace-nowrap px-3 py-2">
                    {isSummer || !analysis ? '-' : (
                      <span title={analysis.finalStart && analysis.finalEnd ? `${fmt(utcDate(analysis.finalStart))} - ${fmt(utcDate(analysis.finalEnd))}` : 'Tarih yok'} className={analysis.finalStart ? 'cursor-help border-b border-dotted border-slate-400' : ''}>
                        {analysis.finalWeek ? `${analysis.finalWeek}. hafta` : '-'}
                      </span>
                    )}
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </div>
  );
}
