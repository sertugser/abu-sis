import { useState } from 'react';
import { Skeleton } from '@/components/ui/skeleton';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { useCurriculumStatus } from '../hooks/useCurriculumStatus';
import { useStudentInfo } from '../hooks/useStudentInfo';
import { AlertCircle, Printer, BookX, Search } from 'lucide-react';
import type { CurriculumStatusDto } from '../types/curriculum';
import type { StudentInfoDto } from '../types/student';
import { SemesterTable } from '../components/curriculum/SemesterTable';

export default function CurriculumStatusPage() {
  const { data, isLoading, error } = useCurriculumStatus();
  const studentQuery = useStudentInfo();
  const [searchTerm, setSearchTerm] = useState('');

  if (isLoading) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-24 rounded-xl" />
        <Skeleton className="h-8 w-64" />
        {[1, 2, 3].map(i => <Skeleton key={i} className="h-40 rounded-xl" />)}
      </div>
    );
  }

  if (error || !data) {
    return (
      <Alert variant="destructive">
        <AlertCircle className="h-4 w-4" />
        <AlertDescription>Müfredat bilgisi yüklenirken hata oluştu.</AlertDescription>
      </Alert>
    );
  }

  const handlePrint = () => {
    window.print();
  };

  const allCourses = data.semesters.flatMap(semester => semester.courses);
  const failedCourseCount = allCourses.filter(course => course.gradeStatus === 'failed').length;
  const filteredSemesters = data.semesters
    .map(semester => ({
      ...semester,
      courses: semester.courses.filter(course => {
        if (!searchTerm.trim()) return true;
        const normalizedSearch = searchTerm.toLowerCase();
        return `${course.code} ${course.name} ${course.takenCourseCode ?? ''} ${course.takenCourseName ?? ''}`.toLowerCase().includes(normalizedSearch);
      }),
    }))
    .filter(semester => semester.courses.length > 0);

  return (
    <>
      {/* ══════ Screen Layout ══════ */}
      <div className="space-y-6 print:hidden">
        {/* Summary Bar */}
        <Card>
          <CardContent className="px-5 py-4">
            <div className="flex items-start justify-between gap-4">
              <div className="space-y-1.5 text-sm">
                <p>
                  <span className="text-muted-foreground">Genel Kredi: </span>
                  <span className="font-bold text-foreground">{data.totalCredits}</span>
                  <span className="text-muted-foreground ml-3">AKTS: </span>
                  <span className="font-bold text-foreground">{data.totalEcts}</span>
                  <span className="text-muted-foreground ml-3">Genel Ort: </span>
                  <span className={`font-bold ${data.cgpa < 2.0 ? 'text-destructive' : 'text-primary'}`}>{data.cgpa.toFixed(2)}</span>
                </p>
                <p>
                  <span className="text-muted-foreground">Başarılı Olunan Kredi: </span>
                  <span className="font-bold text-success">{data.passedCredits}</span>
                  <span className="text-muted-foreground ml-3">AKTS: </span>
                  <span className="font-bold text-success">{data.passedEcts}</span>
                </p>
                <p>
                  <span className="text-muted-foreground">Başarısız Ders: </span>
                  <span className={`font-bold ${failedCourseCount > 0 ? 'text-destructive' : 'text-foreground'}`}>{failedCourseCount}</span>
                </p>
              </div>
              <Button variant="outline" size="sm" onClick={handlePrint} className="gap-2 shrink-0">
                <Printer className="h-4 w-4" />
                Müfredat Durum Yazdır
              </Button>
            </div>
          </CardContent>
        </Card>

        {/* Search */}
        <div className="relative">
          <Search className="h-4 w-4 text-muted-foreground absolute left-3 top-1/2 -translate-y-1/2" />
          <Input
            value={searchTerm}
            onChange={event => setSearchTerm(event.target.value)}
            placeholder="Ders kodu veya ders adı ile filtrele"
            className="pl-9"
          />
        </div>

        {/* Semester Tables */}
        {filteredSemesters.map(sem => (
          <SemesterTable key={sem.semesterNumber} semester={sem} />
        ))}

        {filteredSemesters.length === 0 && (
          <div className="text-center py-16">
            <BookX className="h-12 w-12 text-muted-foreground/40 mx-auto mb-4" />
            <h3 className="font-heading text-lg font-semibold text-foreground">
              {data.semesters.length === 0 ? 'Müfredat bulunamadı' : 'Filtreye uyan ders bulunamadı'}
            </h3>
          </div>
        )}
      </div>

      {/* ══════ Print Layout ══════ */}
      <PrintLayout data={data} student={studentQuery.data} />
    </>
  );
}


/* ═══════════════════════════════════════════════════════════════
   Print-only layout — official report document
   ═══════════════════════════════════════════════════════════════ */

const P = {
  navy: '#1a2e4a',
  navyLight: '#2c4a70',
  green: '#166534',
  red: '#b91c1c',
  blue: '#1d4ed8',
  gray: '#6b7280',
  grayLight: '#e5e7eb',
  rowAlt: '#f6f8fb',
  headerBg: '#edf1f7',
  divider: '#94a3b8',
} as const;

const gradeColor = (status: string | null) =>
  status === 'passed' ? P.green : status === 'failed' ? P.red : status === 'current' ? P.blue : '#000';

const thStyle: React.CSSProperties = {
  padding: '2px 3px', fontSize: '7px', fontWeight: 600,
  borderBottom: `1px solid ${P.navy}`, whiteSpace: 'nowrap', color: P.navy,
};
const tdStyle: React.CSSProperties = {
  padding: '1.5px 3px', fontSize: '7px',
  borderBottom: `0.5px solid ${P.grayLight}`, overflow: 'hidden', textOverflow: 'ellipsis',
};

function PrintLayout({ data, student }: { data: CurriculumStatusDto; student?: StudentInfoDto }) {
  const today = new Date().toLocaleDateString('tr-TR', { day: '2-digit', month: '2-digit', year: 'numeric' });

  return (
    <div className="hidden print:block" style={{ fontFamily: 'Arial, Helvetica, sans-serif', color: '#1f2937', lineHeight: 1.3, width: '100%', margin: 0, padding: 0 }}>

      {/* ── Document header ── */}
      <div style={{ textAlign: 'center', marginBottom: '6px', borderBottom: `2px solid ${P.navy}`, paddingBottom: '5px' }}>
        <div style={{ fontSize: '12px', fontWeight: 800, color: P.navy, letterSpacing: '0.5px' }}>
          ANKARA BİLİM ÜNİVERSİTESİ
        </div>
        <div style={{ fontSize: '9px', fontWeight: 700, color: P.navyLight, marginTop: '1px' }}>
          MÜFREDAT DURUM BELGESİ
        </div>
        <div style={{ fontSize: '7px', color: P.gray, marginTop: '2px' }}>
          Oluşturulma Tarihi: {today}
        </div>
      </div>

      {/* ── Student info grid ── */}
      <table style={{ width: '100%', fontSize: '7.5px', marginBottom: '6px', borderCollapse: 'collapse' }}>
        <tbody>
          <tr>
            <td style={{ padding: '2px 0', color: P.gray, width: '12%' }}>Bölüm</td>
            <td style={{ padding: '2px 0', fontWeight: 600, width: '38%' }}>: {student?.departmentName ?? '—'}</td>
            <td style={{ padding: '2px 0', color: P.gray, width: '22%', textAlign: 'right', paddingRight: '8px' }}>Başarılan Kredi / Toplam Kredi</td>
            <td style={{ padding: '2px 0', fontWeight: 700, width: '28%' }}>
              : <span style={{ color: P.green }}>{data.passedCredits}</span> / {data.totalCredits}
            </td>
          </tr>
          <tr>
            <td style={{ padding: '2px 0', color: P.gray }}>Program</td>
            <td style={{ padding: '2px 0', fontWeight: 600 }}>: {data.programName}</td>
            <td style={{ padding: '2px 0', color: P.gray, textAlign: 'right', paddingRight: '8px' }}>Başarılan AKTS / Toplam AKTS</td>
            <td style={{ padding: '2px 0', fontWeight: 700 }}>
              : <span style={{ color: P.green }}>{data.passedEcts}</span> / {data.totalEcts}
            </td>
          </tr>
          <tr>
            <td style={{ padding: '2px 0', color: P.gray }}>Öğrenim Tipi</td>
            <td style={{ padding: '2px 0', fontWeight: 600 }}>: {data.curriculumVersion}</td>
            <td style={{ padding: '2px 0', color: P.gray, textAlign: 'right', paddingRight: '8px' }}>Genel Not Ortalaması</td>
            <td style={{ padding: '2px 0', fontWeight: 700 }}>
              : <span style={{ color: data.cgpa < 2.0 ? P.red : P.navy }}>{data.cgpa.toFixed(2)}</span>
            </td>
          </tr>
        </tbody>
      </table>

      {/* ── Semesters ── */}
      {data.semesters.map(sem => (
        <div key={sem.semesterNumber} style={{ marginBottom: '4px' }}>
          {/* Semester bar */}
          <div style={{
            background: `linear-gradient(135deg, ${P.navy}, ${P.navyLight})`,
            color: '#fff', fontSize: '7.5px', fontWeight: 700,
            padding: '2px 6px', letterSpacing: '0.3px',
          }}>
            {sem.label}
          </div>

          {/* Table */}
          <table style={{ width: '100%', borderCollapse: 'collapse' }}>
            <thead>
              <tr style={{ backgroundColor: P.headerBg }}>
                <th style={{ ...thStyle, textAlign: 'left', width: '7%' }}>Ders Kodu</th>
                <th style={{ ...thStyle, textAlign: 'left', width: '22%' }}>Müfredat Ders Adı</th>
                <th style={{ ...thStyle, textAlign: 'center', width: '3%' }}>Z/S</th>
                <th style={{ ...thStyle, textAlign: 'center', width: '3.5%' }}>Krd</th>
                <th style={{ ...thStyle, textAlign: 'center', width: '4%' }}>AKTS</th>
                <th style={{ width: '0.3%', backgroundColor: P.divider, padding: 0 }}></th>
                <th style={{ ...thStyle, textAlign: 'left', width: '5.5%' }}>Dönem</th>
                <th style={{ ...thStyle, textAlign: 'left', width: '7%' }}>Ders Kodu</th>
                <th style={{ ...thStyle, textAlign: 'left', width: '22%' }}>Alınan Ders Adı</th>
                <th style={{ ...thStyle, textAlign: 'center', width: '3%' }}>Z/S</th>
                <th style={{ ...thStyle, textAlign: 'center', width: '3.5%' }}>Krd</th>
                <th style={{ ...thStyle, textAlign: 'center', width: '4%' }}>AKTS</th>
                <th style={{ ...thStyle, textAlign: 'center', width: '4%' }}>Harf</th>
              </tr>
            </thead>
            <tbody>
              {sem.courses.map((course, i) => (
                <tr key={course.courseId} style={{ backgroundColor: i % 2 === 1 ? P.rowAlt : '#fff' }}>
                  <td style={{ ...tdStyle, fontWeight: 700, color: P.navy, fontFamily: 'monospace' }}>{course.code}</td>
                  <td style={tdStyle}>{course.name}</td>
                  <td style={{ ...tdStyle, textAlign: 'center', fontWeight: 600, color: course.isCompulsory ? P.navy : '#0d9488' }}>
                    {course.isCompulsory ? 'Z' : 'S'}
                  </td>
                  <td style={{ ...tdStyle, textAlign: 'center', fontFamily: 'monospace' }}>{course.credits}</td>
                  <td style={{ ...tdStyle, textAlign: 'center', fontFamily: 'monospace' }}>{course.ects}</td>
                  <td style={{ backgroundColor: P.divider, padding: 0, borderBottom: `0.5px solid ${P.grayLight}` }}></td>
                  {course.takenTermLabel ? (
                    <>
                      <td style={{ ...tdStyle, color: P.gray, fontSize: '6.5px' }}>{course.takenTermLabel}</td>
                      <td style={{ ...tdStyle, fontWeight: 700, fontFamily: 'monospace' }}>{course.takenCourseCode}</td>
                      <td style={tdStyle}>{course.takenCourseName}</td>
                      <td style={{ ...tdStyle, textAlign: 'center', fontWeight: 600, color: course.takenIsCompulsory ? P.navy : '#0d9488' }}>
                        {course.takenIsCompulsory ? 'Z' : 'S'}
                      </td>
                      <td style={{ ...tdStyle, textAlign: 'center', fontFamily: 'monospace' }}>{course.takenCredits}</td>
                      <td style={{ ...tdStyle, textAlign: 'center', fontFamily: 'monospace' }}>{course.takenEcts}</td>
                      <td style={{ ...tdStyle, textAlign: 'center', fontWeight: 700, color: gradeColor(course.gradeStatus) }}>
                        {course.grade}
                      </td>
                    </>
                  ) : (
                    <td colSpan={7} style={{ ...tdStyle, color: '#9ca3af', fontStyle: 'italic' }}>Henüz alınmadı</td>
                  )}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ))}

      {/* ── Footer ── */}
      <div style={{ marginTop: '8px', paddingTop: '4px', borderTop: `1px solid ${P.grayLight}`, fontSize: '6.5px', color: P.gray, display: 'flex', justifyContent: 'space-between' }}>
        <span>Bu belge Ankara Bilim Üniversitesi Öğrenci Bilgi Sistemi tarafından oluşturulmuştur.</span>
        <span>{data.studentName} — {data.programName}</span>
      </div>
    </div>
  );
}
