// SIS.Web/src/components/grade/GradeReportDocument.tsx
// Proliz tarzı resmi not listesi belgesi — SADECE yazdırmada görünür (hidden print:block).
// Ekran görüntüsü değil; üniversite başlığı + ders bilgisi + öğrenci/bileşen/HBN/harf/sonuç tablosu.
// Kaydedilmiş (persisted) puanları yansıtır; düzenleme tamponunu değil.

import { resolveGrade } from './gradeDisplay';
import type { ExamAttendance, GradeComponent, GradingCriteria, StudentGradeRow } from '../../types/grade';

interface GradeReportDocumentProps {
  courseCode: string;
  courseName: string;
  students: StudentGradeRow[];
  components: GradeComponent[];
  criteria: GradingCriteria;
  isFinalized: boolean;
}

const P = {
  navy: '#1a2e4a',
  navyLight: '#2c4a70',
  green: '#166534',
  red: '#b91c1c',
  gray: '#6b7280',
  grayLight: '#e5e7eb',
  headerBg: '#edf1f7',
  rowAlt: '#f6f8fb',
} as const;

const thBase: React.CSSProperties = {
  padding: '3px 4px',
  fontSize: '8px',
  fontWeight: 700,
  color: P.navy,
  borderBottom: `1px solid ${P.navy}`,
  whiteSpace: 'nowrap',
};

const tdBase: React.CSSProperties = {
  padding: '2.5px 4px',
  fontSize: '8px',
  borderBottom: `0.5px solid ${P.grayLight}`,
  whiteSpace: 'nowrap',
};

/** Kaydedilmiş puanlardan ağırlıklı ortalama (Mazeretli hariç, Girmedi → 0). */
function persistedWeightedTotal(
  student: StudentGradeRow,
  components: GradeComponent[],
): number | null {
  let weightedSum = 0;
  let weightUsed = 0;
  let hasAny = false;
  for (const comp of components) {
    const attendance: ExamAttendance = student.examAttendances?.[comp.id] ?? 'NotMarked';
    if (attendance === 'Excused') continue;
    if (attendance === 'Absent') {
      weightUsed += comp.weight;
      hasAny = true;
      continue;
    }
    const score = student.scores[comp.id];
    if (score == null) continue;
    weightedSum += score * comp.weight;
    weightUsed += comp.weight;
    hasAny = true;
  }
  if (!hasAny || weightUsed === 0) return null;
  return weightedSum / weightUsed;
}

/** Bileşen hücresi: puan, ya da G (girmedi) / M (mazeretli) / — (boş). */
function componentCellText(student: StudentGradeRow, comp: GradeComponent): string {
  const attendance: ExamAttendance = student.examAttendances?.[comp.id] ?? 'NotMarked';
  if (attendance === 'Absent') return 'G';
  if (attendance === 'Excused') return 'M';
  const score = student.scores[comp.id];
  return score != null ? String(score) : '—';
}

export function GradeReportDocument({
  courseCode,
  courseName,
  students,
  components,
  criteria,
  isFinalized,
}: GradeReportDocumentProps) {
  const today = new Date().toLocaleDateString('tr-TR', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
  });

  return (
    <div
      className="hidden print:block print-no-break"
      style={{ fontFamily: 'Arial, Helvetica, sans-serif', color: '#1f2937', width: '100%' }}
    >
      {/* ── Belge başlığı ── */}
      <div
        style={{
          display: 'flex',
          alignItems: 'flex-start',
          justifyContent: 'space-between',
          borderBottom: `2px solid ${P.navy}`,
          paddingBottom: '6px',
          marginBottom: '6px',
        }}
      >
        <div>
          <div style={{ fontSize: '13px', fontWeight: 800, color: P.navy, letterSpacing: '0.4px' }}>
            ANKARA BİLİM ÜNİVERSİTESİ
          </div>
          <div style={{ fontSize: '9px', fontWeight: 700, color: P.navyLight, marginTop: '1px' }}>
            NOT LİSTESİ
          </div>
        </div>
        <div style={{ textAlign: 'right', fontSize: '8px', color: P.gray }}>
          <div>{today}</div>
          <div style={{ marginTop: '2px' }}>{students.length} öğrenci</div>
        </div>
      </div>

      {/* ── Ders bilgisi ── */}
      <div
        style={{
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
          backgroundColor: P.headerBg,
          padding: '4px 6px',
          marginBottom: '6px',
          fontSize: '9px',
        }}
      >
        <span style={{ fontWeight: 700, color: P.navy }}>{courseName}</span>
        <span style={{ fontWeight: 600, color: P.navyLight }}>
          Ders Kodu: {courseCode}
          {isFinalized ? ' · Kesinleştirilmiş' : ''}
        </span>
      </div>

      {/* ── Not tablosu ── */}
      <table style={{ width: '100%', borderCollapse: 'collapse' }}>
        <thead>
          <tr style={{ backgroundColor: P.headerBg }}>
            <th style={{ ...thBase, textAlign: 'center', width: '24px' }}>No</th>
            <th style={{ ...thBase, textAlign: 'left' }}>Öğrenci No</th>
            <th style={{ ...thBase, textAlign: 'left' }}>Adı</th>
            <th style={{ ...thBase, textAlign: 'left' }}>Soyadı</th>
            {components.map((comp) => (
              <th key={comp.id} style={{ ...thBase, textAlign: 'center' }}>
                {comp.name}
                <span style={{ fontWeight: 400, color: P.gray }}> (%{comp.weight})</span>
              </th>
            ))}
            <th style={{ ...thBase, textAlign: 'center' }}>HBN</th>
            <th style={{ ...thBase, textAlign: 'center' }}>B.Notu</th>
            <th style={{ ...thBase, textAlign: 'center' }}>Sonuç</th>
          </tr>
        </thead>
        <tbody>
          {students.map((student, index) => {
            const weighted = persistedWeightedTotal(student, components);
            const rounded = weighted != null ? Math.round(weighted) : null;
            const letter = isFinalized
              ? student.letterGrade ?? null
              : rounded != null
                ? resolveGrade(rounded, criteria)
                : null;
            const passed = letter != null && letter !== 'FF' && letter !== 'NA';
            const sonucColor = letter == null ? P.gray : passed ? P.green : P.red;

            return (
              <tr key={student.studentId} style={index % 2 === 1 ? { backgroundColor: P.rowAlt } : undefined}>
                <td style={{ ...tdBase, textAlign: 'center', color: P.gray }}>{index + 1}</td>
                <td style={{ ...tdBase, textAlign: 'left', fontFamily: 'monospace' }}>{student.studentNumber}</td>
                <td style={{ ...tdBase, textAlign: 'left', fontWeight: 600 }}>{student.firstName}</td>
                <td style={{ ...tdBase, textAlign: 'left', fontWeight: 600 }}>{student.lastName}</td>
                {components.map((comp) => (
                  <td key={comp.id} style={{ ...tdBase, textAlign: 'center', fontFamily: 'monospace' }}>
                    {componentCellText(student, comp)}
                  </td>
                ))}
                <td style={{ ...tdBase, textAlign: 'center', fontFamily: 'monospace', fontWeight: 700 }}>
                  {rounded != null ? rounded : '—'}
                </td>
                <td style={{ ...tdBase, textAlign: 'center', fontWeight: 700, color: sonucColor }}>
                  {letter ?? '—'}
                </td>
                <td style={{ ...tdBase, textAlign: 'center', fontWeight: 700, color: sonucColor }}>
                  {letter == null ? '—' : passed ? 'Geçti' : 'Kaldı'}
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>

      {/* ── Footer ── */}
      <div style={{ marginTop: '8px', fontSize: '7px', color: P.gray }}>
        G: Girmedi · M: Mazeretli · HBN: Ham Başarı Notu (ağırlıklı ortalama)
        {!isFinalized && ' · Bu liste kesinleştirilmemiştir, ön izleme amaçlıdır.'}
      </div>
    </div>
  );
}
