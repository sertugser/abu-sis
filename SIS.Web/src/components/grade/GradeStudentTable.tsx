// SIS.Web/src/components/grade/GradeStudentTable.tsx
// Not giriş öğrenci tablosu (Proliz): Snf/A.Tipi + her bileşen için GRUP kolon
// (başlıkta ad + %ağırlık + İlan rozeti; alt iki hücre: Girme Durum + Puan).
// Harf notu + ön izleme (ağırlıklı toplamdan) kolonları sonda.

import React from 'react';
import { User } from 'lucide-react';
import { ATTENDANCE_OPTIONS, resolveGrade } from './gradeDisplay';
import { GradeChip } from './GradeChip';
import { ComponentPublishBadge } from './ComponentPublishBadge';
import { Input } from '@/components/ui/input';
import type { ExamAttendance, GradeComponent, GradingCriteria, StudentGradeRow } from '../../types/grade';

interface GradeStudentTableProps {
  students: StudentGradeRow[];
  components: GradeComponent[];
  /** Düzenlenen puanlar: componentId → (studentId → string). */
  scores: Record<number, Record<number, string>>;
  /** İyimser katılım: componentId → (studentId → attendance). */
  optimisticAttendance: Record<number, Record<number, ExamAttendance>>;
  isFinalized: boolean;
  readOnly: boolean;
  criteria: GradingCriteria;
  showPreviousGrade: boolean;
  showPreview: boolean;
  isRelative: boolean;
  canPublishComponent: boolean;
  isPublishPending: boolean;
  onScoreChange: (componentId: number, studentId: number, value: string) => void;
  onAttendanceChange: (componentId: number, studentId: number, attendance: ExamAttendance) => void;
  onPublishComponent: (componentId: number) => void;
  onUnpublishComponent: (componentId: number) => void;
}

export function GradeStudentTable({
  students,
  components,
  scores,
  optimisticAttendance,
  isFinalized,
  readOnly,
  criteria,
  showPreviousGrade,
  showPreview,
  isRelative,
  canPublishComponent,
  isPublishPending,
  onScoreChange,
  onAttendanceChange,
  onPublishComponent,
  onUnpublishComponent,
}: GradeStudentTableProps) {
  const resolveScoreStr = (student: StudentGradeRow, componentId: number): string => {
    const edited = scores[componentId]?.[student.studentId];
    if (edited != null) return edited;
    const persisted = student.scores[componentId];
    return persisted != null ? String(persisted) : '';
  };

  const resolveAttendance = (student: StudentGradeRow, componentId: number): ExamAttendance => {
    const optimistic = optimisticAttendance[componentId]?.[student.studentId];
    if (optimistic != null) return optimistic;
    return student.examAttendances?.[componentId] ?? 'NotMarked';
  };

  /**
   * Ağırlıklı toplam ön izleme: tüm bileşenlerin puan + katılımından (kaydetmeden) hesaplanır.
   * Mazeretli bileşen toplam dışı bırakılır (ağırlığı pay/paydadan düşülür); Girmedi → 0.
   * En az bir bileşen geçerliyse sonuç döner; aksi halde null.
   */
  const previewWeightedTotal = (student: StudentGradeRow): number | null => {
    let weightedSum = 0;
    let weightUsed = 0;
    let hasAny = false;
    for (const comp of components) {
      const attendance = resolveAttendance(student, comp.id);
      if (attendance === 'Excused') continue;
      const scoreStr = resolveScoreStr(student, comp.id);
      if (attendance === 'Absent') {
        weightUsed += comp.weight;
        hasAny = true;
        continue;
      }
      if (scoreStr === '') continue;
      const num = Number(scoreStr);
      if (Number.isNaN(num) || num < 0 || num > 100) continue;
      weightedSum += num * comp.weight;
      weightUsed += comp.weight;
      hasAny = true;
    }
    if (!hasAny || weightUsed === 0) return null;
    return weightedSum / weightUsed;
  };

  return (
    <div className="overflow-x-auto">
      <table className="w-full text-[15px]">
        <thead>
          {/* Üst başlık satırı: kimlik kolonları (rowSpan) + bileşen grup başlıkları (colSpan=2) */}
          <tr className="border-b bg-muted/30">
            <th rowSpan={2} className="text-center py-3 px-2 font-medium text-muted-foreground w-8"></th>
            <th rowSpan={2} className="text-center py-3 px-2 font-medium text-muted-foreground w-10">No</th>
            <th rowSpan={2} className="text-left py-3 px-3 font-medium text-muted-foreground w-28 whitespace-nowrap">Öğrenci No</th>
            <th rowSpan={2} className="text-left py-3 px-3 font-medium text-muted-foreground w-32 whitespace-nowrap">Adı</th>
            <th rowSpan={2} className="text-left py-3 px-3 font-medium text-muted-foreground w-32 whitespace-nowrap">Soyadı</th>
            <th rowSpan={2} className="text-center py-3 px-2 font-medium text-muted-foreground w-12">Snf</th>
            <th rowSpan={2} className="text-center py-3 px-2 font-medium text-muted-foreground w-20">A.Tipi</th>
            {components.map((comp) => {
              const published = Boolean(comp.publishedAt);
              return (
                <th
                  key={comp.id}
                  colSpan={2}
                  className="text-center py-2 px-3 font-medium text-foreground border-l"
                >
                  <div className="flex flex-col items-center gap-1">
                    <div className="flex items-center justify-center gap-1.5 flex-wrap">
                      <span className="truncate max-w-[160px]" title={`${comp.name} (%${comp.weight})`}>
                        {comp.name}
                      </span>
                      <span className="font-mono text-[11px] text-muted-foreground">%{comp.weight}</span>
                    </div>
                    {canPublishComponent ? (
                      <ComponentPublishBadge
                        published={published}
                        publishedAt={comp.publishedAt}
                        disabled={isPublishPending}
                        onPublish={() => onPublishComponent(comp.id)}
                        onUnpublish={() => onUnpublishComponent(comp.id)}
                      />
                    ) : (
                      <span
                        className={`text-[10px] font-medium ${published ? 'text-emerald-600' : 'text-muted-foreground'}`}
                      >
                        {published ? 'İlan Edildi' : 'İlan Edilmedi'}
                      </span>
                    )}
                  </div>
                </th>
              );
            })}
            <th rowSpan={2} className="text-center py-3 px-4 font-medium text-muted-foreground w-24 border-l">Harf Notu</th>
            {showPreview && (
              <>
                <th rowSpan={2} className="text-center py-3 px-4 font-medium text-muted-foreground w-24 border-l">Öniz. HBN</th>
                <th rowSpan={2} className="text-center py-3 px-4 font-medium text-muted-foreground w-24">Önizl. Harf</th>
                <th rowSpan={2} className="text-center py-3 px-4 font-medium text-muted-foreground w-20">Geç. D</th>
              </>
            )}
          </tr>
          {/* Alt başlık satırı: her bileşen için Girme / Puan (esnek — fazla genişliği bunlar yutar) */}
          <tr className="border-b bg-muted/20">
            {components.map((comp) => (
              <React.Fragment key={comp.id}>
                <th className="text-center py-2 px-2 text-[11px] font-medium text-muted-foreground border-l min-w-[7rem]">Girme</th>
                <th className="text-center py-2 px-2 text-[11px] font-medium text-muted-foreground min-w-[6rem]">Puan</th>
              </React.Fragment>
            ))}
          </tr>
        </thead>
        <tbody>
          {students.map((student, index) => {
            const previewTotal = previewWeightedTotal(student);
            const previewRounded = previewTotal != null ? Math.round(previewTotal) : null;
            const previewLetter = previewRounded != null ? resolveGrade(previewRounded, criteria) : null;
            const previewPass = previewLetter != null && previewLetter !== 'FF' && previewLetter !== 'NA';

            const displayedLetterGrade = isFinalized
              ? student.letterGrade ?? null
              : previewLetter;

            return (
              <tr key={student.studentId} className="border-b last:border-0 hover:bg-muted/20 transition-colors">
                <td className="py-3 px-2 text-center text-muted-foreground">
                  <User className="h-4 w-4 mx-auto opacity-40" aria-hidden />
                </td>
                <td className="py-3 px-2 text-center text-muted-foreground font-mono text-sm">{index + 1}</td>
                <td className="py-3 px-3 font-mono text-sm whitespace-nowrap">{student.studentNumber}</td>
                <td className="py-3 px-3 font-medium whitespace-nowrap">{student.firstName}</td>
                <td className="py-3 px-3 font-medium whitespace-nowrap">{student.lastName}</td>
                <td className="py-3 px-2 text-center font-mono text-sm">{student.classYear}</td>
                <td className="py-3 px-2 text-center">
                  <span className={`text-[11px] font-medium px-2 py-0.5 rounded-md ${
                    student.enrollmentType === 'Seçmeli'
                      ? 'bg-violet-50 text-violet-600'
                      : 'bg-slate-100 text-slate-600'
                  }`}>
                    {student.enrollmentType}
                  </span>
                </td>
                {components.map((comp) => {
                  const scoreStr = resolveScoreStr(student, comp.id);
                  const scoreNum = scoreStr !== '' ? Number(scoreStr) : null;
                  const isValidScore =
                    scoreNum != null && !Number.isNaN(scoreNum) && scoreNum >= 0 && scoreNum <= 100;
                  const attendance = resolveAttendance(student, comp.id);
                  const cellAbsent = attendance === 'Absent';
                  const cellExcused = attendance === 'Excused';
                  const cellLocked = cellAbsent || cellExcused;
                  const attendancePending =
                    optimisticAttendance[comp.id]?.[student.studentId] != null;

                  return (
                    <React.Fragment key={comp.id}>
                      {/* Girme Durum (per-component) */}
                      <td className="py-3 px-2 text-center border-l">
                        {isFinalized || readOnly ? (
                          <span className={`text-xs font-medium px-2 py-0.5 rounded-md ${
                            cellAbsent ? 'bg-red-50 text-red-600' :
                            cellExcused ? 'bg-amber-50 text-amber-600' :
                            attendance === 'Present' ? 'bg-emerald-50 text-emerald-600' :
                            'text-muted-foreground'
                          }`}>
                            {ATTENDANCE_OPTIONS.find((o) => o.value === attendance)?.label ?? '—'}
                          </span>
                        ) : (
                          <select
                            value={attendance}
                            onChange={(e) =>
                              onAttendanceChange(comp.id, student.studentId, e.target.value as ExamAttendance)
                            }
                            disabled={attendancePending}
                            className={`h-8 w-full rounded-md border text-xs font-medium px-1.5 pr-5 appearance-none bg-background cursor-pointer transition-colors focus:outline-none focus:ring-2 focus:ring-ring focus:ring-offset-1 ${
                              cellAbsent ? 'border-red-300 text-red-600 bg-red-50' :
                              cellExcused ? 'border-amber-300 text-amber-600 bg-amber-50' :
                              attendance === 'Present' ? 'border-emerald-300 text-emerald-600 bg-emerald-50' :
                              'border-input text-muted-foreground'
                            }`}
                          >
                            {ATTENDANCE_OPTIONS.map((opt) => (
                              <option key={opt.value} value={opt.value}>{opt.label}</option>
                            ))}
                          </select>
                        )}
                      </td>
                      {/* Puan (per-component) */}
                      <td className="py-3 px-2 text-center">
                        {isFinalized ? (
                          <span className="font-mono text-sm font-semibold">
                            {cellAbsent ? '0' : cellExcused ? 'Mazeretli' : scoreStr || '—'}
                          </span>
                        ) : cellLocked ? (
                          <span className={`text-xs font-mono font-medium ${cellAbsent ? 'text-red-500' : 'text-amber-500'}`}>
                            {cellAbsent ? '0' : 'Mazeretli'}
                          </span>
                        ) : (
                          <Input
                            type="number"
                            min={0}
                            max={100}
                            value={scoreStr}
                            disabled={readOnly}
                            onChange={(e) => onScoreChange(comp.id, student.studentId, e.target.value)}
                            className={`w-20 mx-auto text-center h-9 font-mono text-[15px] ${
                              scoreStr !== '' && !isValidScore ? 'border-red-500 focus-visible:ring-red-500' : ''
                            }`}
                          />
                        )}
                      </td>
                    </React.Fragment>
                  );
                })}
                <td className="py-3 px-4 text-center border-l">
                  {displayedLetterGrade ? (
                    <GradeChip grade={displayedLetterGrade} />
                  ) : showPreviousGrade && student.letterGrade ? (
                    <GradeChip grade={student.letterGrade} />
                  ) : (
                    <span className="text-muted-foreground">—</span>
                  )}
                </td>
                {showPreview && (
                  <>
                    <td className="py-3 px-4 text-center font-mono text-sm border-l">
                      {previewRounded != null ? previewRounded : <span className="text-muted-foreground">—</span>}
                    </td>
                    <td className="py-3 px-4 text-center">
                      {isRelative ? (
                        <span className="text-[11px] text-muted-foreground">çan finalize'da</span>
                      ) : previewLetter ? (
                        <GradeChip grade={previewLetter} />
                      ) : (
                        <span className="text-muted-foreground">—</span>
                      )}
                    </td>
                    <td className="py-3 px-4 text-center">
                      {previewLetter == null || isRelative ? (
                        <span className="text-muted-foreground">—</span>
                      ) : previewPass ? (
                        <span className="text-emerald-600 font-semibold text-sm">Geçti</span>
                      ) : (
                        <span className="text-red-600 font-semibold text-sm">Kaldı</span>
                      )}
                    </td>
                  </>
                )}
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}
