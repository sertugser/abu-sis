import { useState } from 'react';
import { Skeleton } from '@/components/ui/skeleton';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { useCurriculumStatus } from '../hooks/useCurriculumStatus';
import { useStudentInfo } from '../hooks/useStudentInfo';
import { useStudentPoolStatus } from '../hooks/useStudentPoolStatus';
import { AlertCircle, Printer, BookX, AlertTriangle, Search, CheckCircle2, CircleAlert } from 'lucide-react';
import type { CurriculumStatusDto, CurriculumCourseStatusDto, CurriculumSemesterDto } from '../types/curriculum';
import type { StudentInfoDto } from '../types/student';


function getGradeBadgeVariant(grade: string | null, status: string | null): 'success' | 'destructive' | 'secondary' | 'outline' {
  if (!grade || !status) return 'outline';
  if (status === 'current') return 'secondary';
  if (status === 'passed') return 'success';
  if (status === 'failed') return 'destructive';
  return 'outline';
}

export default function CurriculumStatusPage() {
  const { data, isLoading, error } = useCurriculumStatus();
  const studentQuery = useStudentInfo();
  const { data: poolStatus = [] } = useStudentPoolStatus(studentQuery.data?.id ?? null);
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

        {/* Seçmeli Havuz Durumu (Madde 17) */}
        {poolStatus.length > 0 && (
          <Card>
            <CardContent className="px-5 py-4">
              <div className="flex items-center justify-between mb-3">
                <h3 className="font-semibold text-sm text-foreground">Seçmeli Havuz Tamamlama Durumu</h3>
                <Badge variant="secondary" className="text-xs">
                  {poolStatus.filter(p => p.isFulfilled).length}/{poolStatus.length} tamamlandı
                </Badge>
              </div>
              <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
                {poolStatus.map(pool => {
                  const courseProgress = pool.requiredCourseCount > 0
                    ? Math.min(100, (pool.completedCourseCount / pool.requiredCourseCount) * 100)
                    : 0;
                  const ectsProgress = pool.requiredEcts > 0
                    ? Math.min(100, (pool.earnedEcts / pool.requiredEcts) * 100)
                    : 0;
                  return (
                    <div
                      key={pool.poolId}
                      className={`border rounded-lg p-3 ${pool.isFulfilled ? 'border-success/40 bg-success/5' : 'border-border'}`}
                    >
                      <div className="flex items-center gap-2 mb-2">
                        {pool.isFulfilled
                          ? <CheckCircle2 className="h-4 w-4 text-success shrink-0" />
                          : <CircleAlert className="h-4 w-4 text-amber-500 shrink-0" />
                        }
                        <span className="text-xs font-mono font-bold text-foreground">{pool.poolCode}</span>
                        <span className="text-xs text-muted-foreground truncate">{pool.poolName}</span>
                      </div>
                      {pool.requiredCourseCount > 0 && (
                        <div className="space-y-1 mb-1.5">
                          <div className="flex justify-between text-[11px]">
                            <span className="text-muted-foreground">Ders</span>
                            <span className="tabular-nums font-medium">{pool.completedCourseCount}/{pool.requiredCourseCount}</span>
                          </div>
                          <div className="h-1.5 bg-muted rounded-full overflow-hidden">
                            <div
                              className={`h-full rounded-full transition-all ${pool.isFulfilled ? 'bg-success' : 'bg-primary'}`}
                              style={{ width: `${courseProgress}%` }}
                            />
                          </div>
                        </div>
                      )}
                      {pool.requiredEcts > 0 && (
                        <div className="space-y-1">
                          <div className="flex justify-between text-[11px]">
                            <span className="text-muted-foreground">AKTS</span>
                            <span className="tabular-nums font-medium">{pool.earnedEcts}/{pool.requiredEcts}</span>
                          </div>
                          <div className="h-1.5 bg-muted rounded-full overflow-hidden">
                            <div
                              className={`h-full rounded-full transition-all ${pool.isFulfilled ? 'bg-success' : 'bg-primary'}`}
                              style={{ width: `${ectsProgress}%` }}
                            />
                          </div>
                        </div>
                      )}
                      {pool.ectsPerCourse != null && (
                        <p className="text-[10px] text-muted-foreground mt-1.5">
                          Havuz kuralı: her ders {pool.ectsPerCourse} AKTS sayılır
                        </p>
                      )}
                    </div>
                  );
                })}
              </div>
            </CardContent>
          </Card>
        )}

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


function SemesterTable({ semester }: { semester: CurriculumSemesterDto }) {
  return (
    <Card className="overflow-hidden print-no-break">
      <CardContent className="p-0">
        {/* Semester header */}
        <div className="bg-primary px-4 py-2">
          <h3 className="text-sm font-bold text-primary-foreground">{semester.label}</h3>
        </div>

        {/* Table */}
        <div className="overflow-x-auto">
          <table className="w-full text-sm table-fixed print-table-auto">
            <colgroup>
              {/* Left side — 49.8% */}
              <col style={{ width: '8%' }} />
              <col style={{ width: '28.8%' }} />
              <col style={{ width: '4%' }} />
              <col style={{ width: '4%' }} />
              <col style={{ width: '5%' }} />
              {/* Divider — 0.4% */}
              <col style={{ width: '0.4%' }} />
              {/* Right side — 49.8% */}
              <col style={{ width: '5%' }} />
              <col style={{ width: '8%' }} />
              <col style={{ width: '21.8%' }} />
              <col style={{ width: '3.5%' }} />
              <col style={{ width: '3.5%' }} />
              <col style={{ width: '4%' }} />
              <col style={{ width: '4%' }} />
            </colgroup>
            <thead>
              <tr className="border-b border-border bg-muted/30">
                {/* Left side — curriculum */}
                <th className="text-left px-3 py-2 font-medium text-muted-foreground">Ders Kodu</th>
                <th className="text-left px-3 py-2 font-medium text-muted-foreground">Müfredat Ders Adı</th>
                <th className="text-center px-1 py-2 font-medium text-muted-foreground">Z/S</th>
                <th className="text-center px-1 py-2 font-medium text-muted-foreground">Krd</th>
                <th className="text-center px-1 py-2 font-medium text-muted-foreground">AKTS</th>
                {/* Divider */}
                <th className="bg-primary/20 p-0"></th>
                {/* Right side — taken */}
                <th className="text-left px-2 py-2 font-medium text-muted-foreground">Dönem</th>
                <th className="text-left px-2 py-2 font-medium text-muted-foreground">Ders Kodu</th>
                <th className="text-left px-3 py-2 font-medium text-muted-foreground">Ders Adı</th>
                <th className="text-center px-1 py-2 font-medium text-muted-foreground">Z/S</th>
                <th className="text-center px-1 py-2 font-medium text-muted-foreground">Krd</th>
                <th className="text-center px-1 py-2 font-medium text-muted-foreground">AKTS</th>
                <th className="text-center px-1 py-2 font-medium text-muted-foreground">Harf</th>
              </tr>
            </thead>
            <tbody>
              {semester.courses.map((course, index) => (
                <CourseRow key={course.courseId} course={course} isEven={index % 2 === 0} />
              ))}
            </tbody>
          </table>
        </div>

        {/* Semester summary */}
        <div className="flex flex-wrap gap-x-4 gap-y-1 px-4 py-2 border-t border-border bg-muted/20 text-xs text-muted-foreground">
          <span>Kredi: {semester.totalCredits}</span>
          <span>AKTS: {semester.totalEcts}</span>
          <span>Ders: {semester.courseCount}</span>
          <span>Zorunlu: {semester.compulsoryCount}</span>
          <span>Seçmeli: {semester.electiveCount}</span>
        </div>
      </CardContent>
    </Card>
  );
}

function CourseRow({ course, isEven }: { course: CurriculumCourseStatusDto; isEven: boolean }) {
  const hasPrereqWarning = course.prerequisites.length > 0 && !course.prerequisitesMet;

  return (
    <tr className={`border-b border-border/50 hover:bg-muted/30 transition-colors ${isEven ? 'bg-muted/10' : ''}`}>
      {/* Left side — curriculum */}
      <td className="px-3 py-2 font-mono text-sm font-bold text-foreground whitespace-nowrap">
        <span className="flex items-center gap-1.5">
          {course.code}
          {hasPrereqWarning && (
            <span title={`Ön Koşul: ${course.prerequisites.join(', ')}`}>
              <AlertTriangle className="h-3.5 w-3.5 text-destructive" />
            </span>
          )}
        </span>
      </td>
      <td className="px-3 py-2">
        <span className={course.gradeStatus === 'failed' ? 'text-destructive' : 'text-foreground'}>
          {course.name}
        </span>
        {hasPrereqWarning && (
          <span className="text-xs text-destructive ml-1">
            ({course.prerequisites.join(', ')})
          </span>
        )}
      </td>
      <td className="text-center px-2 py-2">
        <span className={`text-xs font-bold uppercase ${course.isCompulsory ? 'text-primary' : 'text-accent'}`}>
          {course.isCompulsory ? 'Z' : 'S'}
        </span>
      </td>
      <td className="text-center px-2 py-2 font-mono text-sm">{course.credits}</td>
      <td className="text-center px-2 py-2 font-mono text-sm">{course.ects}</td>

      {/* Divider */}
      <td className="bg-primary/20 p-0"></td>

      {/* Right side — taken */}
      {course.takenTermLabel ? (
        <>
          <td className="px-3 py-2 text-xs text-muted-foreground whitespace-nowrap">[{course.takenTermLabel}]</td>
          <td className="px-3 py-2 font-mono text-sm font-medium">{course.takenCourseCode}</td>
          <td className="px-3 py-2 text-sm">{course.takenCourseName}</td>
          <td className="text-center px-2 py-2">
            <span className={`text-xs font-bold uppercase ${course.takenIsCompulsory ? 'text-primary' : 'text-accent'}`}>
              {course.takenIsCompulsory ? 'Z' : 'S'}
            </span>
          </td>
          <td className="text-center px-2 py-2 font-mono text-sm">{course.takenCredits}</td>
          <td className="text-center px-2 py-2 font-mono text-sm">{course.takenEcts}</td>
          <td className="text-center px-2 py-2">
            {course.grade && (
              <Badge
                variant={getGradeBadgeVariant(course.grade, course.gradeStatus)}
                className={`w-10 justify-center text-xs font-bold ${
                  course.gradeStatus === 'passed'
                    ? 'bg-green-600/15 text-foreground border-green-600/30'
                    : course.gradeStatus === 'failed'
                      ? 'bg-red-600/15 text-foreground border-red-600/30'
                      : ''
                }`}
              >
                {course.grade}
              </Badge>
            )}
          </td>
        </>
      ) : (
        <td className="px-3 py-2" colSpan={7}>
          <span className="text-sm text-muted-foreground italic">Henüz alınmadı</span>
        </td>
      )}
    </tr>
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
