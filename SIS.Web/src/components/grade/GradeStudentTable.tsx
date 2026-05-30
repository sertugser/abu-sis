// SIS.Web/src/components/grade/GradeStudentTable.tsx
// Not giriş öğrenci tablosu: katılım, puan input, önceki harf notu + ön izleme kolonları.

import { ATTENDANCE_OPTIONS, resolveGrade } from './gradeDisplay';
import { GradeChip } from './GradeChip';
import { Input } from '@/components/ui/input';
import type { ExamAttendance, GradingCriteria, StudentGradeRow } from '../../types/grade';

interface GradeStudentTableProps {
  students: StudentGradeRow[];
  componentId: number;
  scores: Record<number, string>;
  optimisticAttendance: Record<number, ExamAttendance>;
  isFinalized: boolean;
  readOnly: boolean;
  criteria: GradingCriteria;
  showPreviousGrade: boolean;
  showPreview: boolean;
  isRelative: boolean;
  onScoreChange: (studentId: number, value: string) => void;
  onAttendanceChange: (studentId: number, attendance: ExamAttendance) => void;
}

export function GradeStudentTable({
  students,
  componentId,
  scores,
  optimisticAttendance,
  isFinalized,
  readOnly,
  criteria,
  showPreviousGrade,
  showPreview,
  isRelative,
  onScoreChange,
  onAttendanceChange,
}: GradeStudentTableProps) {
  return (
    <div className="overflow-x-auto">
      <table className="w-full text-[15px]">
        <thead>
          <tr className="border-b bg-muted/30">
            <th className="text-center py-3 px-4 font-medium text-muted-foreground w-14">#</th>
            <th className="text-left py-3 px-4 font-medium text-muted-foreground">Öğrenci No</th>
            <th className="text-left py-3 px-4 font-medium text-muted-foreground">Adı</th>
            <th className="text-left py-3 px-4 font-medium text-muted-foreground">Soyadı</th>
            <th className="text-center py-3 px-4 font-medium text-muted-foreground w-36">Girme Durum</th>
            <th className="text-center py-3 px-4 font-medium text-muted-foreground w-32">Puan</th>
            {showPreviousGrade && (
              <th className="text-center py-3 px-4 font-medium text-muted-foreground w-24">Harf Notu</th>
            )}
            {showPreview && (
              <>
                <th className="text-center py-3 px-4 font-medium text-muted-foreground w-24">Öniz. HBN</th>
                <th className="text-center py-3 px-4 font-medium text-muted-foreground w-24">Önizl. Harf</th>
                <th className="text-center py-3 px-4 font-medium text-muted-foreground w-20">Geç. D</th>
              </>
            )}
          </tr>
        </thead>
        <tbody>
          {students.map((student, index) => {
            const persistedScore = student.scores[componentId];
            const scoreStr = scores[student.studentId] ?? (persistedScore != null ? String(persistedScore) : '');
            const scoreNum = scoreStr !== '' ? Number(scoreStr) : null;
            const isValidScore = scoreNum != null && !Number.isNaN(scoreNum) && scoreNum >= 0 && scoreNum <= 100;

            const serverAttendance: ExamAttendance =
              (student.examAttendances?.[componentId]) || 'NotMarked';
            const attendance: ExamAttendance = optimisticAttendance[student.studentId] ?? serverAttendance;
            const isAbsent = attendance === 'Absent';
            const isExcused = attendance === 'Excused';
            const scoreDisabled = isAbsent || isExcused;

            const livePreviewGrade =
              isValidScore && !scoreDisabled ? resolveGrade(Math.round(scoreNum!), criteria) : null;
            const displayedLetterGrade = isFinalized ? student.letterGrade ?? null : livePreviewGrade;

            // Ön izleme: kaydetmeden, mevcut puandan tahmini harf (mutlak değerlendirme).
            const previewRounded = isValidScore && !isAbsent ? Math.round(scoreNum!) : isAbsent ? 0 : null;
            const previewLetter = previewRounded != null ? resolveGrade(previewRounded, criteria) : null;
            const previewPass = previewLetter != null && previewLetter !== 'FF' && previewLetter !== 'NA';

            return (
              <tr key={student.studentId} className="border-b last:border-0 hover:bg-muted/20 transition-colors">
                <td className="py-3 px-4 text-center text-muted-foreground font-mono text-sm">{index + 1}</td>
                <td className="py-3 px-4 font-mono text-sm">{student.studentNumber}</td>
                <td className="py-3 px-4 font-medium">{student.firstName}</td>
                <td className="py-3 px-4 font-medium">{student.lastName}</td>
                <td className="py-3 px-4 text-center">
                  {isFinalized || readOnly ? (
                    <span className={`text-xs font-medium px-2 py-0.5 rounded-md ${
                      isAbsent ? 'bg-red-50 text-red-600' :
                      isExcused ? 'bg-amber-50 text-amber-600' :
                      attendance === 'Present' ? 'bg-emerald-50 text-emerald-600' :
                      'text-muted-foreground'
                    }`}>
                      {ATTENDANCE_OPTIONS.find((o) => o.value === attendance)?.label ?? '—'}
                    </span>
                  ) : (
                    <select
                      value={attendance}
                      onChange={(e) => onAttendanceChange(student.studentId, e.target.value as ExamAttendance)}
                      disabled={student.studentId in optimisticAttendance}
                      className={`h-8 rounded-md border text-xs font-medium px-2 pr-6 appearance-none bg-background cursor-pointer transition-colors focus:outline-none focus:ring-2 focus:ring-ring focus:ring-offset-1 ${
                        isAbsent ? 'border-red-300 text-red-600 bg-red-50' :
                        isExcused ? 'border-amber-300 text-amber-600 bg-amber-50' :
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
                <td className="py-3 px-4 text-center">
                  {isFinalized ? (
                    <span className="font-mono text-sm font-semibold">
                      {isAbsent ? '0 (Girmedi)' : isExcused ? 'Mazeretli' : scoreStr || '—'}
                    </span>
                  ) : scoreDisabled ? (
                    <span className={`text-xs font-mono font-medium ${isAbsent ? 'text-red-500' : 'text-amber-500'}`}>
                      {isAbsent ? '0 (Girmedi)' : 'Mazeretli'}
                    </span>
                  ) : (
                    <Input
                      type="number"
                      min={0}
                      max={100}
                      value={scoreStr}
                      disabled={readOnly}
                      onChange={(e) => onScoreChange(student.studentId, e.target.value)}
                      className={`w-24 mx-auto text-center h-9 font-mono text-[15px] ${
                        scoreStr !== '' && !isValidScore ? 'border-red-500 focus-visible:ring-red-500' : ''
                      }`}
                    />
                  )}
                </td>
                {showPreviousGrade && (
                  <td className="py-3 px-4 text-center">
                    {displayedLetterGrade ? (
                      <GradeChip grade={displayedLetterGrade} />
                    ) : (
                      <span className="text-muted-foreground">—</span>
                    )}
                  </td>
                )}
                {showPreview && (
                  <>
                    <td className="py-3 px-4 text-center font-mono text-sm">
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
