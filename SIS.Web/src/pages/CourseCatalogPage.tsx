import { useState, useMemo } from 'react';
import { Skeleton } from '@/components/ui/skeleton';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Progress } from '@/components/ui/progress';
import { Separator } from '@/components/ui/separator';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table';
import ValidationErrors from '../components/registration/ValidationErrors';
import CourseCard from '../components/course/CourseCard';
import RegistrationStatusBadge from '../components/registration/RegistrationStatusBadge';
import { useStudentCatalog } from '../hooks/useStudentCatalog';
import { useMyRegistrations } from '../hooks/useMyRegistrations';
import { useAddToPlan } from '../hooks/useAddToPlan';
import { useDropCourse } from '../hooks/useDropCourse';
import { useSubmitRegistration } from '../hooks/useSubmitRegistration';
import { useStudentInfo } from '../hooks/useStudentInfo';
import { useActiveTerm } from '../hooks/useActiveTerm';
import { formatDate } from '../lib/utils';
import type { ValidationError } from '../types/registration';
import {
  AlertCircle, AlertTriangle, BookX, Trash2, Send, CheckCircle2,
  Loader2, ClipboardList, GraduationCap, User, Calendar, Clock, RotateCcw
} from 'lucide-react';

function getEctsExplanation(cgpa: number): string {
  if (cgpa >= 2.50) return `GANO 2.50 ve ustu oldugundan 48 AKTS alinabilir.`;
  if (cgpa >= 2.00) return `GANO 2.00-2.49 arasinda oldugundan 42 AKTS alinabilir.`;
  return `GANO 2.00 altinda oldugundan 30 AKTS alinabilir.`;
}

const TAKE_TYPE_COLORS: Record<string, string> = {
  Zorunlu: 'text-primary',
  Secmeli: 'text-accent',
  Alttan: 'text-destructive',
  Yukseltme: 'text-warning',
};

type ViewFilter = 'all' | 'alttan' | 'new' | 'taken';

export default function CourseCatalogPage() {
  const { data: courses, isLoading, error } = useStudentCatalog();
  const { data: registrations } = useMyRegistrations();
  const { data: student } = useStudentInfo();
  const { data: term } = useActiveTerm();
  const addToPlan = useAddToPlan();
  const dropCourse = useDropCourse();
  const submitRegistration = useSubmitRegistration();
  const [validationErrors, setValidationErrors] = useState<ValidationError[]>([]);
  const [submitSuccess, setSubmitSuccess] = useState(false);
  const [viewFilter, setViewFilter] = useState<ViewFilter>('all');
  const classYear = student ? Math.min(Math.floor(student.completedSemesters / 2) + 1, 4) : 1;
  const [classYearFilter, setClassYearFilter] = useState<number | null>(null);
  const [termTypeFilter, setTermTypeFilter] = useState<'all' | 'guz' | 'bahar'>('all');
  const effectiveClassYear = classYearFilter ?? classYear;

  const registeredSectionIds = registrations
    ?.filter(r => ['Draft', 'Active', 'PendingAdvisorApproval'].includes(r.status))
    .map(r => r.courseSectionId) || [];

  const planItems = registrations?.filter(r => r.status === 'Draft') || [];
  const confirmedItems = registrations?.filter(r => ['Active', 'PendingAdvisorApproval'].includes(r.status)) || [];
  const allSelectedItems = [...confirmedItems, ...planItems];
  const totalEcts = allSelectedItems.reduce((sum, r) => sum + r.ects, 0);
  const ectsLimit = student?.ectsLimit ?? 30;
  const ectsPercent = Math.min((totalEcts / ectsLimit) * 100, 100);
  const totalHours = allSelectedItems.reduce((sum, r) => sum + r.theoryHours + r.labHours, 0);

  const isFinalized = confirmedItems.length > 0 && planItems.length === 0;
  const hasAdvisorApproval = confirmedItems.every(r => r.status === 'Active');

  // Counts by take status
  const statusCounts = useMemo(() => {
    if (!courses) return { alttan: 0, new_: 0, taken: 0, total: 0 };
    return {
      alttan: courses.filter(c => c.takeStatus === 'Alttan').length,
      new_: courses.filter(c => c.takeStatus === 'IlkDefa').length,
      taken: courses.filter(c => c.takeStatus === 'OncedenAlindi').length,
      total: courses.length,
    };
  }, [courses]);

  // Filter courses
  const filteredCourses = useMemo(() => {
    if (!courses) return [];
    return courses.filter(c => {
      if (viewFilter === 'alttan' && c.takeStatus !== 'Alttan') return false;
      if (viewFilter === 'new' && c.takeStatus !== 'IlkDefa') return false;
      if (viewFilter === 'taken' && c.takeStatus !== 'OncedenAlindi') return false;
      // Class year filter
      if (effectiveClassYear > 0) {
        const guzSemester = (effectiveClassYear - 1) * 2 + 1;
        const baharSemester = (effectiveClassYear - 1) * 2 + 2;
        if (termTypeFilter === 'guz' && c.suggestedSemester !== guzSemester) return false;
        if (termTypeFilter === 'bahar' && c.suggestedSemester !== baharSemester) return false;
        if (termTypeFilter === 'all' && c.suggestedSemester !== guzSemester && c.suggestedSemester !== baharSemester) return false;
      }
      return true;
    });
  }, [courses, viewFilter, effectiveClassYear, termTypeFilter]);

  // Alttan courses for warning
  const alttanCourses = useMemo(() => courses?.filter(c => c.takeStatus === 'Alttan') || [], [courses]);

  const handleAddToPlan = (sectionId: number) => {
    addToPlan.mutate(sectionId);
  };

  const handleRemove = (registrationId: number) => {
    dropCourse.mutate(registrationId);
  };

  const handleSubmit = () => {
    setValidationErrors([]);
    setSubmitSuccess(false);
    const sectionIds = planItems.map(r => r.courseSectionId);
    submitRegistration.mutate(sectionIds, {
      onSuccess: (data) => {
        if (data.validationResult && !data.validationResult.isValid) {
          setValidationErrors(data.validationResult.errors);
        } else if (data.success) {
          setSubmitSuccess(true);
          setValidationErrors([]);
        }
      },
      onError: () => {
        setValidationErrors([{ rule: 'System', message: 'Kayit sirasinda bir hata olustu.' }]);
      },
    });
  };

  if (isLoading) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-32 rounded-xl" />
        <Skeleton className="h-8 w-48" />
        {[1,2,3].map(i => <Skeleton key={i} className="h-20 rounded-xl" />)}
      </div>
    );
  }

  if (error) {
    return (
      <Alert variant="destructive">
        <AlertCircle className="h-4 w-4" />
        <AlertDescription>Dersler yüklenirken hata oluştu.</AlertDescription>
      </Alert>
    );
  }

  return (
    <div className="space-y-6">
      {/* Student Info Card */}
      {student && (
        <Card className="border border-[#003366]/20 shadow-md overflow-hidden">
          <CardContent className="p-0">
            <div className="bg-[#003366] px-5 py-2.5">
              <h3 className="font-heading text-sm font-extrabold text-white flex items-center gap-2 uppercase tracking-tight">
                <GraduationCap className="h-4 w-4 text-white/80" />
                Ders Kayıt Öğrenci Bilgileri
              </h3>
            </div>
            <div className="p-5">
              <div className="grid grid-cols-1 md:grid-cols-2 gap-x-8 gap-y-2 text-sm">
                <InfoRow icon={User} label="Ögrenci No / Adı Soyadı" value={`${student.studentNumber} / ${student.firstName} ${student.lastName}`} />
                <InfoRow icon={GraduationCap} label="Fakülte - Program" value={`${student.facultyName} - ${student.programName}`} />
                <InfoRow icon={Calendar} label="Sınıf / Dönem" value={`${classYear}. Sınıf, ${student.completedSemesters + 1}. Dönem`} />
                <InfoRow label="Genel Ortalama" value={student.cgpa.toFixed(2)} valueClass={student.cgpa < 2.0 ? 'text-destructive font-bold' : 'text-success font-bold'} />
              </div>
              {term && (
                <>
                  <Separator className="my-3" />
                  <div className="grid grid-cols-1 md:grid-cols-2 gap-x-8 gap-y-2 text-sm">
                    <InfoRow icon={Calendar} label="Kayıt Tarihleri" value={`${formatDate(term.registrationStart)} - ${formatDate(term.registrationEnd)}`} />
                    <InfoRow icon={Clock} label="Ek/Bırak Tarihleri" value={`${formatDate(term.addDropStart)} - ${formatDate(term.addDropEnd)}`} />
                  </div>
                </>
              )}
            </div>
          </CardContent>
        </Card>
      )}

      {/* Registration Status */}
      {allSelectedItems.length > 0 && (
        <Card className="border border-[#003366]/20 shadow-md overflow-hidden">
          <CardContent className="p-0">
            <div className="bg-[#003366] px-5 py-2.5">
              <h3 className="font-heading text-sm font-extrabold text-white uppercase tracking-tight">
                Ders Kayıt Durumu
              </h3>
            </div>
            <div className="px-5 py-3 flex flex-wrap gap-x-12 gap-y-2 text-sm">
              <div className="flex items-center gap-2">
                <span className="text-muted-foreground">Kesinleştirme Durumu:</span>
                {isFinalized ? (
                  <Badge variant="success">Kesinleştirildi</Badge>
                ) : planItems.length > 0 ? (
                  <Badge variant="warning">Kesinleştirilmedi</Badge>
                ) : (
                  <Badge variant="secondary">Bekleniyor</Badge>
                )}
              </div>
              <div className="flex items-center gap-2">
                <span className="text-muted-foreground">Danışman Onay Durumu:</span>
                {hasAdvisorApproval && confirmedItems.length > 0 ? (
                  <Badge variant="success">Onaylandı</Badge>
                ) : confirmedItems.some(r => r.status === 'PendingAdvisorApproval') ? (
                  <Badge variant="warning">Onay Bekleniyor</Badge>
                ) : (
                  <Badge variant="secondary">—</Badge>
                )}
              </div>
            </div>
          </CardContent>
        </Card>
      )}

      {/* Alerts */}
      {submitSuccess && (
        <Alert variant="success">
          <CheckCircle2 className="h-4 w-4" />
          <AlertDescription>Kayıt başarıyla kesinleştirildi!</AlertDescription>
        </Alert>
      )}

      {validationErrors.length > 0 && <ValidationErrors errors={validationErrors} />}

      {!term?.isRegistrationOpen && !term?.isAddDropOpen && (
        <Alert variant="warning">
          <AlertTriangle className="h-4 w-4" />
          <AlertDescription>Kayıt dönemi şu anda kapalıdır. Dersler görüntülenebilir ancak plana eklenemez.</AlertDescription>
        </Alert>
      )}

      {addToPlan.error && (
        <Alert variant="destructive">
          <AlertCircle className="h-4 w-4" />
          <AlertDescription>Plana eklenirken hata oluştu. Lütfen tekrar deneyin.</AlertDescription>
        </Alert>
      )}

      {/* Alttan Warning */}
      {alttanCourses.length > 0 && viewFilter !== 'alttan' && (
        <Alert variant="destructive">
          <RotateCcw className="h-4 w-4" />
          <AlertDescription>
            <strong>{alttanCourses.length} ders</strong>ten başarısız (FF/NA) notunuz var ve tekrar almanız gerekmektedir:{' '}
            {alttanCourses.map(c => `${c.code} (${c.previousGrade})`).join(', ')}
          </AlertDescription>
        </Alert>
      )}

      {/* Selected Courses Table */}
      {allSelectedItems.length > 0 && (
        <Card className="border border-[#003366] shadow-md overflow-hidden">
          <CardContent className="p-0">
            <div className="bg-[#003366] px-5 py-2.5 flex items-center gap-2">
              <ClipboardList className="h-4 w-4 text-white/80" />
              <h3 className="font-heading text-sm font-extrabold text-white uppercase tracking-tight">
                Seçilen Dersler
              </h3>
            </div>

            <div className="overflow-x-auto">
              <Table>
                <TableHeader>
                  <TableRow className="bg-muted/30">
                    <TableHead className="w-28">Ders Kodu</TableHead>
                    <TableHead>Ders Adı</TableHead>
                    <TableHead className="text-center w-14">Z/S</TableHead>
                    <TableHead className="text-center w-16">T+U</TableHead>
                    <TableHead className="text-center w-16">AKTS</TableHead>
                    <TableHead className="text-center w-14 hidden md:table-cell">Snf</TableHead>
                    <TableHead className="w-24 hidden sm:table-cell">Alış</TableHead>
                    <TableHead className="text-center w-24">Durum</TableHead>
                    <TableHead className="text-right w-16">İşlem</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {allSelectedItems.map(item => (
                    <TableRow key={item.registrationId}>
                      <TableCell className="font-mono text-sm font-semibold text-primary">{item.courseCode}</TableCell>
                      <TableCell className="font-medium">{item.courseName}</TableCell>
                      <TableCell className="text-center">
                        <span className={`text-xs font-bold ${item.isCompulsory ? 'text-primary' : 'text-accent'}`}>
                          {item.isCompulsory ? 'Z' : 'S'}
                        </span>
                      </TableCell>
                      <TableCell className="text-center font-mono text-sm">{item.theoryHours}+{item.labHours}</TableCell>
                      <TableCell className="text-center">
                        <Badge variant="secondary" className="font-mono">{item.ects}</Badge>
                      </TableCell>
                      <TableCell className="text-center hidden md:table-cell text-sm text-muted-foreground">
                        {item.suggestedSemester > 0 ? item.suggestedSemester : '—'}
                      </TableCell>
                      <TableCell className="hidden sm:table-cell">
                        <span className={`text-xs font-medium ${TAKE_TYPE_COLORS[item.takeType] || 'text-muted-foreground'}`}>
                          {item.takeType}
                        </span>
                      </TableCell>
                      <TableCell className="text-center">
                        <RegistrationStatusBadge status={item.status} />
                      </TableCell>
                      <TableCell className="text-right">
                        {item.status === 'Draft' && (
                          <Button
                            variant="ghost"
                            size="icon"
                            onClick={() => handleRemove(item.registrationId)}
                            disabled={dropCourse.isPending}
                            className="h-7 w-7 text-muted-foreground hover:text-destructive hover:bg-destructive/10"
                          >
                            <Trash2 className="h-3.5 w-3.5" />
                          </Button>
                        )}
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </div>

            {/* Summary footer */}
            <div className="px-5 py-3 border-t border-border bg-muted/30">
              <div className="flex flex-wrap items-center gap-x-6 gap-y-2 text-sm">
                <span className="text-muted-foreground">
                  AKTS: <strong className="text-foreground">{totalEcts}/{ectsLimit}</strong>
                </span>
                {student && (
                  <span className="text-xs text-muted-foreground">
                    ({getEctsExplanation(student.cgpa)})
                  </span>
                )}
                <span className="text-muted-foreground">
                  Saat: <strong className="text-foreground">{totalHours}</strong>
                </span>
                <span className="text-muted-foreground">
                  Ders Sayisi: <strong className="text-foreground">{allSelectedItems.length}</strong>
                </span>
              </div>
              <Progress value={ectsPercent} indicatorClassName={ectsPercent > 90 ? 'bg-destructive' : 'bg-primary'} className="h-2 mt-2" />
            </div>

            {/* Submit button */}
            {planItems.length > 0 && (
              <div className="px-5 py-3 border-t border-border flex justify-end">
                <Button
                  size="lg"
                  onClick={handleSubmit}
                  disabled={submitRegistration.isPending}
                  className="gap-2 rounded-xl"
                >
                  {submitRegistration.isPending ? (
                    <>
                      <Loader2 className="h-4 w-4 animate-spin" />
                      Gönderiliyor...
                    </>
                  ) : (
                    <>
                      <Send className="h-4 w-4" />
                      Kaydı Kesinleştir
                    </>
                  )}
                </Button>
              </div>
            )}
          </CardContent>
        </Card>
      )}

      {/* Course Catalog Filters */}
      <Card className="border border-[#003366]/20 shadow-md overflow-hidden">
        <CardContent className="p-0">
          <div className="bg-[#003366] px-5 py-2.5">
            <h3 className="font-heading text-sm font-extrabold text-white uppercase tracking-tight">
              Açık Dersler
            </h3>
          </div>
          <div className="px-5 py-3 flex flex-wrap items-center gap-3">
            <div className="flex flex-wrap gap-1.5">
              <FilterButton active={viewFilter === 'all'} onClick={() => setViewFilter('all')}>
                Tümü ({statusCounts.total})
              </FilterButton>
              {statusCounts.alttan > 0 && (
                <FilterButton active={viewFilter === 'alttan'} onClick={() => setViewFilter('alttan')} variant="destructive">
                  Alttan ({statusCounts.alttan})
                </FilterButton>
              )}
              <FilterButton active={viewFilter === 'new'} onClick={() => setViewFilter('new')}>
                Alınmamış ({statusCounts.new_})
              </FilterButton>
              <FilterButton active={viewFilter === 'taken'} onClick={() => setViewFilter('taken')}>
                Önceden Alınan ({statusCounts.taken})
              </FilterButton>
            </div>

            <Separator orientation="vertical" className="h-5 hidden sm:block" />
            <div className="flex items-center gap-1.5">
              <span className="text-xs text-muted-foreground">Sinif:</span>
              <select
                value={effectiveClassYear}
                onChange={e => setClassYearFilter(Number(e.target.value))}
                className="h-7 text-xs rounded-lg border border-border bg-background px-2 outline-none focus:ring-1 focus:ring-primary"
              >
                {[1, 2, 3, 4].map(y => (
                  <option key={y} value={y}>{y}. Sınıf</option>
                ))}
              </select>
            </div>
            <Separator orientation="vertical" className="h-5 hidden sm:block" />
            <div className="flex items-center gap-1.5">
              <label className="flex items-center gap-1 cursor-pointer">
                <input type="radio" name="termType" value="all" checked={termTypeFilter === 'all'} onChange={() => setTermTypeFilter('all')} className="accent-primary w-3 h-3" />
                <span className="text-xs text-muted-foreground">Tümü</span>
              </label>
              <label className="flex items-center gap-1 cursor-pointer">
                <input type="radio" name="termType" value="guz" checked={termTypeFilter === 'guz'} onChange={() => setTermTypeFilter('guz')} className="accent-primary w-3 h-3" />
                <span className="text-xs text-muted-foreground">Güz</span>
              </label>
              <label className="flex items-center gap-1 cursor-pointer">
                <input type="radio" name="termType" value="bahar" checked={termTypeFilter === 'bahar'} onChange={() => setTermTypeFilter('bahar')} className="accent-primary w-3 h-3" />
                <span className="text-xs text-muted-foreground">Bahar</span>
              </label>
            </div>

            <span className="text-xs text-muted-foreground ml-auto">
              {filteredCourses.length} ders
            </span>
          </div>
        </CardContent>
      </Card>

      {/* Course list */}
      {filteredCourses.length > 0 ? (
        <div className="space-y-3">
          {filteredCourses.map(course => (
            <CourseCard
              key={course.courseId}
              course={course}
              onAddToPlan={handleAddToPlan}
              isAdding={addToPlan.isPending}
              registeredSectionIds={registeredSectionIds}
            />
          ))}
        </div>
      ) : (
        <div className="text-center py-16">
          <BookX className="h-12 w-12 text-muted-foreground/40 mx-auto mb-4" />
          <h3 className="font-heading text-lg font-semibold text-foreground">Ders bulunamadı</h3>
          <p className="text-muted-foreground mt-1">
            {viewFilter !== 'all' || termTypeFilter !== 'all'
              ? 'Seçilen filtrelere uygun ders bulunmamaktadır.'
              : 'Bu dönem için açık ders bulunmamaktadır.'}
          </p>
        </div>
      )}
    </div>
  );
}

function FilterButton({ active, onClick, variant, children }: {
  active: boolean;
  onClick: () => void;
  variant?: 'destructive';
  children: React.ReactNode;
}) {
  return (
    <Button
      variant={active ? (variant === 'destructive' ? 'destructive' : 'default') : 'outline'}
      size="sm"
      onClick={onClick}
      className="h-7 text-xs rounded-lg"
    >
      {children}
    </Button>
  );
}

function InfoRow({ icon: Icon, label, value, valueClass }: {
  icon?: React.ComponentType<{ className?: string }>;
  label: string;
  value: string;
  valueClass?: string;
}) {
  return (
    <div className="flex items-start gap-2">
      {Icon && <Icon className="h-4 w-4 text-muted-foreground mt-0.5 shrink-0" />}
      <div className="flex gap-1.5 flex-wrap">
        <span className="text-muted-foreground">{label}:</span>
        <span className={`font-medium ${valueClass || 'text-foreground'}`}>{value}</span>
      </div>
    </div>
  );
}
