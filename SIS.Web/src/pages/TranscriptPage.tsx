import { useEffect, type CSSProperties } from 'react';
import { useNavigate } from 'react-router-dom';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { formatDate } from '@/lib/utils';
import { AlertCircle, Download, Globe, X } from 'lucide-react';
import { useStudentTranscript } from '../hooks/useStudentTranscript';
import { STUDENT_STATUS_LABELS } from '../lib/constants';
import type { StudentTranscriptCourseDto, StudentTranscriptDto, StudentTranscriptTermDto } from '../types/transcript';
import logo from '/logo.png';

const GRADE_LEGEND = [
  ['AA', '4.00'],
  ['BA', '3.50'],
  ['BB', '3.00'],
  ['CB', '2.50'],
  ['CC', '2.00'],
  ['DC', '1.50'],
  ['DD', '1.00'],
  ['FF / NA', '0.00'],
  ['S / U / W / I / NI', 'GPA dışı'],
] as const;

const REPORT = {
  ink: '#1f2937',
  muted: '#6b7280',
  border: '#cfd8e3',
  borderStrong: '#b9c7d8',
  line: '#e5e7eb',
  page: '#ffffff',
  pageAlt: '#f8fafc',
  panel: '#dfe6ee',
  navy: '#223a5e',
  navySoft: '#ecf2f8',
  green: '#166534',
  red: '#b91c1c',
  gray: '#475569',
} as const;

const infoLabelStyle: CSSProperties = {
  border: `1px solid ${REPORT.borderStrong}`,
  backgroundColor: REPORT.navySoft,
  color: REPORT.gray,
  fontSize: '8px',
  fontWeight: 700,
  padding: '4px 6px',
  width: '15%',
  whiteSpace: 'nowrap',
};

const infoValueStyle: CSSProperties = {
  border: `1px solid ${REPORT.borderStrong}`,
  color: REPORT.ink,
  fontSize: '8px',
  fontWeight: 600,
  padding: '4px 6px',
  width: '35%',
};

const tableHeadStyle: CSSProperties = {
  padding: '4px 5px',
  fontSize: '7px',
  fontWeight: 700,
  borderBottom: `1px solid ${REPORT.navy}`,
  color: REPORT.navy,
  backgroundColor: '#fff',
  whiteSpace: 'nowrap',
};

const tableCellStyle: CSSProperties = {
  padding: '3px 5px',
  fontSize: '7px',
  color: REPORT.ink,
  borderBottom: `0.5px solid ${REPORT.line}`,
  verticalAlign: 'top',
};

const summaryLabelStyle: CSSProperties = {
  ...tableCellStyle,
  color: REPORT.gray,
  fontWeight: 700,
  borderBottom: 'none',
};

const summaryValueStyle: CSSProperties = {
  ...tableCellStyle,
  textAlign: 'right',
  fontWeight: 700,
  borderBottom: 'none',
};

export default function TranscriptPage() {
  const navigate = useNavigate();
  const transcriptQuery = useStudentTranscript({ forDocument: true });

  useEffect(() => {
    const previousTitle = document.title;
    document.title = 'Transkript';
    return () => {
      document.title = previousTitle;
    };
  }, []);

  const handleClose = () => {
    if (window.history.length > 1) {
      navigate(-1);
      return;
    }

    navigate('/');
  };

  const handleDownload = () => {
    window.print();
  };

  return (
    <div className="min-h-screen bg-[#dbe3ec] print:bg-white">
      <style>{`
        @media print {
          @page {
            size: A4 portrait;
            margin: 10mm;
          }

          body {
            background: white;
          }
        }
      `}</style>

      <header className="sticky top-0 z-30 flex items-center justify-between border-b border-white/10 bg-[#1f1f1f] px-5 py-4 text-white shadow-[0_14px_38px_rgba(15,23,42,0.28)] print:hidden">
        <div className="flex items-center gap-3">
          <div className="flex h-10 w-10 items-center justify-center rounded-full bg-white/10">
            <Globe className="h-5 w-5" />
          </div>
          <div>
            <h1 className="font-heading text-[1.7rem] font-semibold leading-none tracking-tight">Transkript</h1>
            <p className="mt-1 text-xs text-white/60">Öğrenci transkript raporu</p>
          </div>
        </div>

        <div className="flex items-center gap-2">
          <Button
            type="button"
            size="sm"
            variant="secondary"
            onClick={handleDownload}
            disabled={transcriptQuery.isLoading || transcriptQuery.isError || !transcriptQuery.data}
            className="border border-white/15 bg-white/10 text-white hover:bg-white/20"
          >
            <Download className="h-4 w-4" />
            PDF İndir
          </Button>

          <button
            type="button"
            onClick={handleClose}
            className="flex h-10 w-10 items-center justify-center rounded-full text-white/80 transition-colors hover:bg-white/10 hover:text-white"
            aria-label="Raporu kapat"
            title="Kapat"
          >
            <X className="h-5 w-5" />
          </button>
        </div>
      </header>

      <div className="px-4 py-5 md:px-8 md:py-7 print:p-0">
        {transcriptQuery.isLoading ? (
          <LoadingState />
        ) : transcriptQuery.isError || !transcriptQuery.data ? (
          <ErrorState />
        ) : (
          <div className="overflow-x-auto print:overflow-visible">
            <div className="mx-auto w-[860px] max-w-none rounded-[18px] border border-slate-300 bg-white shadow-[0_28px_80px_rgba(15,23,42,0.18)] print:w-auto print:rounded-none print:border-0 print:shadow-none">
              <TranscriptReportDocument transcript={transcriptQuery.data} />
            </div>
          </div>
        )}
      </div>
    </div>
  );
}

function LoadingState() {
  return (
    <div className="mx-auto w-full max-w-[860px] rounded-[18px] border border-slate-300 bg-white p-6 shadow-[0_28px_80px_rgba(15,23,42,0.18)]">
      <Skeleton className="mb-5 h-20 rounded-xl" />
      <Skeleton className="mb-4 h-24 rounded-xl" />
      <Skeleton className="mb-4 h-64 rounded-xl" />
      <Skeleton className="h-64 rounded-xl" />
    </div>
  );
}

function ErrorState() {
  return (
    <div className="mx-auto w-full max-w-[860px]">
      <Alert variant="destructive" className="border border-destructive/30 bg-white shadow-md">
        <AlertCircle className="h-4 w-4" />
        <AlertTitle>Transkript yüklenemedi</AlertTitle>
        <AlertDescription>
          Rapor için gerekli transkript bilgileri alınırken bir hata oluştu. Lütfen sayfayı yeniden deneyin.
        </AlertDescription>
      </Alert>
    </div>
  );
}

function TranscriptReportDocument({ transcript }: { transcript: StudentTranscriptDto }) {
  const statusLabel = STUDENT_STATUS_LABELS[transcript.studentStatus] || transcript.studentStatus;
  const initials = getInitials(transcript.fullName);

  return (
    <article
      style={{
        fontFamily: 'Arial, Helvetica, sans-serif',
        color: REPORT.ink,
        background: REPORT.page,
        WebkitPrintColorAdjust: 'exact',
        printColorAdjust: 'exact',
      }}
    >
      <div style={{ padding: '22px 24px 28px' }}>
        <header style={{ marginBottom: '10px' }}>
          <div
            style={{
              display: 'grid',
              gridTemplateColumns: '140px 1fr 64px',
              gap: '12px',
              alignItems: 'start',
              marginBottom: '8px',
            }}
          >
            <div style={{ display: 'flex', alignItems: 'center', minHeight: '58px' }}>
              <img
                src={logo}
                alt="Ankara Bilim Üniversitesi"
                style={{ width: '118px', maxHeight: '42px', objectFit: 'contain' }}
              />
            </div>

            <div style={{ textAlign: 'center', paddingTop: '2px' }}>
              <div style={{ fontSize: '13px', fontWeight: 700, color: REPORT.ink }}>
                Ankara Bilim Üniversitesi
              </div>
              <div style={{ marginTop: '3px', fontSize: '11px', fontWeight: 400, color: REPORT.ink }}>
                Öğrenci Not Belgesi
              </div>
            </div>

            <div
              style={{
                justifySelf: 'end',
                display: 'flex',
                height: '72px',
                width: '58px',
                alignItems: 'center',
                justifyContent: 'center',
                border: `1px solid ${REPORT.borderStrong}`,
                backgroundColor: REPORT.pageAlt,
                color: REPORT.navy,
                fontSize: '14px',
                fontWeight: 700,
                letterSpacing: '0.08em',
              }}
            >
              {initials}
            </div>
          </div>
        </header>

        <table style={{ width: '100%', borderCollapse: 'collapse', marginBottom: '10px' }}>
          <tbody>
            <tr>
              <td style={infoLabelStyle}>Ad Soyad</td>
              <td style={infoValueStyle}>{transcript.fullName}</td>
              <td style={infoLabelStyle}>Kayıt Tarihi</td>
              <td style={infoValueStyle}>{formatDate(transcript.enrollmentDate)}</td>
            </tr>
            <tr>
              <td style={infoLabelStyle}>Öğrenci No</td>
              <td style={infoValueStyle}>{transcript.studentNumber}</td>
              <td style={infoLabelStyle}>Öğrenim Durumu</td>
              <td style={infoValueStyle}>{statusLabel}</td>
            </tr>
            <tr>
              <td style={infoLabelStyle}>Fakülte</td>
              <td style={infoValueStyle}>{transcript.facultyName}</td>
              <td style={infoLabelStyle}>Belge Tarihi</td>
              <td style={infoValueStyle}>{formatDateTime(transcript.generatedAt)}</td>
            </tr>
            <tr>
              <td style={infoLabelStyle}>Bölüm</td>
              <td style={infoValueStyle}>{transcript.departmentName}</td>
              <td style={infoLabelStyle}>Program</td>
              <td style={infoValueStyle}>{transcript.programName}</td>
            </tr>
          </tbody>
        </table>

        {transcript.terms.length === 0 ? (
          <div
            style={{
              border: `1px solid ${REPORT.borderStrong}`,
              padding: '28px 24px',
              textAlign: 'center',
              color: REPORT.muted,
              fontSize: '10px',
            }}
          >
            Bu öğrenci için henüz transkriptte gösterilecek harf notlu ders kaydı bulunmuyor.
          </div>
        ) : (
          transcript.terms.map(term => (
            <TranscriptTermSection key={term.termId} term={term} />
          ))
        )}

        <footer style={{ marginTop: '12px' }}>
          <div
            style={{
              display: 'grid',
              gridTemplateColumns: 'repeat(3, minmax(0, 1fr))',
              gap: '8px',
              marginBottom: '10px',
            }}
          >
            <SummaryBox label="Güncel AGNO" value={formatFixed(transcript.currentCgpa)} />
            <SummaryBox label="Toplam AGNO AKTS / Puan" value={`${transcript.totalGpaEcts} / ${formatFixed(transcript.totalGpaPoints)}`} />
            <SummaryBox label="Geçilen AKTS / Kredi" value={`${transcript.totalEarnedEcts} / ${transcript.totalEarnedCredits}`} />
          </div>

          <div style={{ fontSize: '7px', color: REPORT.muted }}>
            <div style={{ marginBottom: '3px', fontWeight: 700, color: REPORT.navy }}>
              Not Katsayıları
            </div>

            <div style={{ display: 'flex', flexWrap: 'wrap', gap: '6px 14px', marginBottom: '4px' }}>
              {GRADE_LEGEND.map(([grade, point]) => (
                <span key={grade}>
                  <strong>{grade}</strong>: {point}
                </span>
              ))}
            </div>

            <div>* işareti, dersin tekrar veya not yükseltme amacıyla yeniden alındığını gösterir.</div>
            <div style={{ marginTop: '6px' }}>
              Bu belge Ankara Bilim Üniversitesi Öğrenci Bilgi Sistemi tarafından oluşturulmuştur.
            </div>
          </div>
        </footer>
      </div>
    </article>
  );
}

function TranscriptTermSection({ term }: { term: StudentTranscriptTermDto }) {
  return (
    <section style={{ marginBottom: '10px', breakInside: 'avoid' }}>
      <div
        style={{
          border: `1px solid ${REPORT.borderStrong}`,
          borderBottom: 'none',
          backgroundColor: REPORT.pageAlt,
          color: REPORT.navy,
          padding: '4px 8px',
          textAlign: 'center',
          fontSize: '8px',
          fontWeight: 700,
        }}
      >
        {term.termName}
      </div>

      <table style={{ width: '100%', borderCollapse: 'collapse', border: `1px solid ${REPORT.borderStrong}` }}>
        <thead>
          <tr>
            <th style={{ ...tableHeadStyle, textAlign: 'left', width: '16%' }}>Ders Kodu</th>
            <th style={{ ...tableHeadStyle, textAlign: 'left', width: '42%' }}>Ders Adı</th>
            <th style={{ ...tableHeadStyle, textAlign: 'center', width: '9%' }}>Krd</th>
            <th style={{ ...tableHeadStyle, textAlign: 'center', width: '9%' }}>AKTS</th>
            <th style={{ ...tableHeadStyle, textAlign: 'center', width: '14%' }}>T.AKTS</th>
            <th style={{ ...tableHeadStyle, textAlign: 'center', width: '10%' }}>Harf Notu</th>
          </tr>
        </thead>
        <tbody>
          {term.courses.map((course, index) => (
            <TranscriptCourseRow key={course.registrationId} course={course} isEven={index % 2 === 1} />
          ))}

          <tr>
            <td colSpan={4} style={summaryLabelStyle}>ANO</td>
            <td colSpan={2} style={summaryValueStyle}>{formatFixed(term.sgpa)}</td>
          </tr>
          <tr>
            <td colSpan={4} style={summaryLabelStyle}>AGNO</td>
            <td colSpan={2} style={summaryValueStyle}>{formatFixed(term.cgpa)}</td>
          </tr>
          <tr>
            <td colSpan={4} style={summaryLabelStyle}>Dönem AKTS / T.AKTS</td>
            <td style={summaryValueStyle}>{term.attemptedEcts}</td>
            <td style={summaryValueStyle}>{formatFixed(term.gpaPoints)}</td>
          </tr>
          <tr>
            <td colSpan={4} style={{ ...summaryLabelStyle, borderBottom: `1px solid ${REPORT.line}` }}>Toplam AKTS / T.AKTS</td>
            <td style={{ ...summaryValueStyle, borderBottom: `1px solid ${REPORT.line}` }}>{term.cumulativeGpaEcts}</td>
            <td style={{ ...summaryValueStyle, borderBottom: `1px solid ${REPORT.line}` }}>{formatFixed(term.cumulativeGpaPoints)}</td>
          </tr>
        </tbody>
      </table>
    </section>
  );
}

function TranscriptCourseRow({
  course,
  isEven,
}: {
  course: StudentTranscriptCourseDto;
  isEven: boolean;
}) {
  const gradeColor = course.isPassed
    ? REPORT.green
    : course.countsTowardsGpa
      ? REPORT.red
      : REPORT.gray;

  return (
    <tr style={{ backgroundColor: isEven ? REPORT.pageAlt : '#fff' }}>
      <td style={{ ...tableCellStyle, fontFamily: 'monospace', fontWeight: 700, color: REPORT.navy }}>
        {course.courseCode}
        {course.isRepeated ? ' *' : ''}
      </td>
      <td style={tableCellStyle}>{course.courseName}</td>
      <td style={{ ...tableCellStyle, textAlign: 'center', fontFamily: 'monospace' }}>{course.credits}</td>
      <td style={{ ...tableCellStyle, textAlign: 'center', fontFamily: 'monospace' }}>{course.ects}</td>
      <td style={{ ...tableCellStyle, textAlign: 'center', fontFamily: 'monospace' }}>
        {course.weightedEcts == null ? '—' : formatFixed(course.weightedEcts)}
      </td>
      <td style={{ ...tableCellStyle, textAlign: 'center', color: gradeColor, fontWeight: 700 }}>
        {course.grade}
      </td>
    </tr>
  );
}

function SummaryBox({ label, value }: { label: string; value: string }) {
  return (
    <div style={{ border: `1px solid ${REPORT.borderStrong}`, padding: '6px 8px' }}>
      <div style={{ fontSize: '6.8px', fontWeight: 700, color: REPORT.muted, marginBottom: '2px' }}>{label}</div>
      <div style={{ fontSize: '8px', fontWeight: 700, color: REPORT.navy }}>{value}</div>
    </div>
  );
}

function getInitials(fullName: string) {
  return fullName
    .split(' ')
    .filter(Boolean)
    .slice(0, 2)
    .map(part => part[0]?.toUpperCase())
    .join('');
}

function formatFixed(value: number) {
  return value.toLocaleString('tr-TR', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  });
}

function formatDateTime(dateStr: string) {
  return new Date(dateStr).toLocaleString('tr-TR', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
}
