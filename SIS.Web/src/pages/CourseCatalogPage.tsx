import { useState, useMemo } from 'react';
import { Link } from 'react-router-dom';
import { Skeleton } from '@/components/ui/skeleton';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Progress } from '@/components/ui/progress';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table';
import ValidationErrors from '../components/registration/ValidationErrors';
import CourseCard from '../components/course/CourseCard';
import RegistrationStatusBadge from '../components/registration/RegistrationStatusBadge';
import SlotCandidatesTable from '../components/registration/SlotCandidatesTable';
import { useStudentCatalog } from '../hooks/useStudentCatalog';
import { useMyRegistrations } from '../hooks/useMyRegistrations';
import { useAddToPlan } from '../hooks/useAddToPlan';
import { useDropCourse } from '../hooks/useDropCourse';
import { useSubmitRegistration } from '../hooks/useSubmitRegistration';
import { useStudentInfo } from '../hooks/useStudentInfo';
import { useStudentPoolStatus, type PoolProgressDto } from '../hooks/useStudentPoolStatus';
import { useActiveTerm } from '../hooks/useActiveTerm';
import {
  Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle,
} from '@/components/ui/dialog';
import { formatDate, cn } from '../lib/utils';
import { ROUTES } from '../lib/constants';
import { classYearOptions } from '../lib/academicYear';
import { ApiError } from '../lib/apiError';
import type { ValidationError } from '../types/registration';
import {
  AlertCircle, AlertTriangle, BookX, Trash2, Send, CheckCircle2, ChevronRight,
  CircleAlert, Layers, Loader2, RotateCcw, GraduationCap, Sparkles, Calendar, Clock,
} from 'lucide-react';

const TAKE_TYPE_COLORS: Record<string, string> = {
  Zorunlu: 'text-primary',
  Secmeli: 'text-accent-foreground',
  Alttan: 'text-destructive',
  Yukseltme: 'text-warning',
};

type ViewFilter = 'all' | 'alttan' | 'new' | 'taken';

export default function CourseCatalogPage() {
  const { data: courses, isLoading, error } = useStudentCatalog();
  const { data: registrations } = useMyRegistrations();
  const { data: student } = useStudentInfo();
  const { data: term } = useActiveTerm();
  const { data: poolStatus = [] } = useStudentPoolStatus(student?.id ?? null);
  const [slotCandidatesTarget, setSlotCandidatesTarget] = useState<PoolProgressDto | null>(null);
  // Faz C C4: "Üstten yarıyıl" toggle — default kapalı (Karar 5=a). Açık ise üst
  // yarıyıl slot'ları da listelenir; kapalı ise sadece mevcut yarıyıl ve geri dönük.
  const [showUpperSemesterPools, setShowUpperSemesterPools] = useState(false);
  const addToPlan = useAddToPlan();
  const dropCourse = useDropCourse();
  const submitRegistration = useSubmitRegistration();
  const [validationErrors, setValidationErrors] = useState<ValidationError[]>([]);
  const [validationWarnings, setValidationWarnings] = useState<ValidationError[]>([]);
  const [submitSuccess, setSubmitSuccess] = useState(false);
  const [viewFilter, setViewFilter] = useState<ViewFilter>('all');
  // Backend StudentInfoDto.ClassYear zaten hesaplıyor; UI tarafında tekrar etme.
  const classYear = student?.classYear ?? 1;
  const [classYearFilter, setClassYearFilter] = useState<number | null>(null);
  const [termTypeFilter, setTermTypeFilter] = useState<'all' | 'guz' | 'bahar'>('all');
  const effectiveClassYear = classYearFilter ?? classYear;

  const registeredSectionIds = registrations
    ?.filter(r => ['Draft', 'Active', 'PendingAdvisorApproval', 'Rejected'].includes(r.status))
    .map(r => r.courseSectionId) || [];

  const planItems = registrations?.filter(r => r.status === 'Draft' || r.status === 'Rejected') || [];
  const rejectedItems = registrations?.filter(r => r.status === 'Rejected') || [];
  const advisorNote = rejectedItems.length > 0 ? rejectedItems[0]?.advisorNote : undefined;
  const confirmedItems = registrations?.filter(r => ['Active', 'PendingAdvisorApproval'].includes(r.status)) || [];
  const allSelectedItems = [...confirmedItems, ...planItems];
  const totalEcts = allSelectedItems.reduce((sum, r) => sum + r.ects, 0);
  // Backend EctsLimit non-nullable; loading durumunda student undefined → progress'i 0 göster.
  const ectsLimit = student?.ectsLimit ?? 0;
  const ectsPercent = ectsLimit > 0 ? Math.min((totalEcts / ectsLimit) * 100, 100) : 0;
  const totalHours = allSelectedItems.reduce((sum, r) => sum + r.theoryHours + r.labHours, 0);

  const isFinalized = confirmedItems.length > 0 && planItems.length === 0;
  const hasAdvisorApproval = confirmedItems.every(r => r.status === 'Active');
  const registrationActionsOpen = Boolean(term?.isRegistrationOpen || term?.isAddDropOpen);
  const registrationClosedMessage = term?.studentRegistrationMessage
    ?? 'Kayıt dönemi şu anda kapalıdır. Dersler görüntülenebilir ancak plana eklenemez.';

  const statusCounts = useMemo(() => {
    if (!courses) return { alttan: 0, new_: 0, taken: 0, total: 0 };
    return {
      alttan: courses.filter(c => c.takeStatus === 'Alttan').length,
      new_: courses.filter(c => c.takeStatus === 'IlkDefa').length,
      taken: courses.filter(c => c.takeStatus === 'OncedenAlindi').length,
      total: courses.length,
    };
  }, [courses]);

  const filteredCourses = useMemo(() => {
    if (!courses) return [];
    return courses.filter(c => {
      if (viewFilter === 'alttan' && c.takeStatus !== 'Alttan') return false;
      if (viewFilter === 'new' && c.takeStatus !== 'IlkDefa') return false;
      if (viewFilter === 'taken' && c.takeStatus !== 'OncedenAlindi') return false;
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

  const alttanCourses = useMemo(() => courses?.filter(c => c.takeStatus === 'Alttan') || [], [courses]);

  const handleAddToPlan = (sectionId: number) => {
    if (!registrationActionsOpen) {
      setValidationErrors([{ rule: 'RegistrationClosed', message: registrationClosedMessage }]);
      setValidationWarnings([]);
      return;
    }

    setValidationErrors([]);
    setValidationWarnings([]);
    addToPlan.mutate(sectionId, {
      onError: (error) => {
        if (error instanceof ApiError && error.validationErrors.length > 0) {
          setValidationErrors(error.validationErrors);
          setValidationWarnings(error.validationWarnings);
        } else {
          setValidationErrors([{ rule: 'System', message: error instanceof Error ? error.message : 'Plana eklenirken hata oluştu.' }]);
        }
      },
    });
  };

  const handleRemove = (registrationId: number) => {
    if (!registrationActionsOpen) {
      setValidationErrors([{ rule: 'RegistrationClosed', message: registrationClosedMessage }]);
      setValidationWarnings([]);
      return;
    }

    setValidationErrors([]);
    setValidationWarnings([]);
    dropCourse.mutate(registrationId, {
      onError: (error) => {
        if (error instanceof ApiError && error.validationErrors.length > 0) {
          setValidationErrors(error.validationErrors);
          setValidationWarnings(error.validationWarnings);
        } else {
          setValidationErrors([{ rule: 'System', message: error instanceof Error ? error.message : 'Ders kaldırılamadı.' }]);
        }
      },
    });
  };

  const handleSubmit = () => {
    setValidationErrors([]);
    setValidationWarnings([]);
    setSubmitSuccess(false);
    if (!registrationActionsOpen) {
      setValidationErrors([{ rule: 'RegistrationClosed', message: registrationClosedMessage }]);
      return;
    }

    const sectionIds = planItems.map(r => r.courseSectionId);
    submitRegistration.mutate(sectionIds, {
      onSuccess: (data) => {
        if (data.success) {
          setSubmitSuccess(true);
          setValidationErrors([]);
          setValidationWarnings(data.validationResult?.warnings ?? []);
        }
      },
      onError: (error) => {
        if (error instanceof ApiError && error.validationErrors.length > 0) {
          setValidationErrors(error.validationErrors);
          setValidationWarnings(error.validationWarnings);
        } else {
          setValidationErrors([{ rule: 'System', message: error instanceof Error ? error.message : 'Kayıt sırasında bir hata oluştu.' }]);
        }
      },
    });
  };

  if (isLoading) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-12 w-full rounded-md" />
        <Skeleton className="h-32 w-full rounded-md" />
        {[1, 2, 3].map(i => <Skeleton key={i} className="h-20 w-full rounded-md" />)}
      </div>
    );
  }

  if (error) {
    const errorCode = error instanceof ApiError ? error.code : undefined;
    const errMsg = error instanceof Error ? error.message : '';

    if (errorCode === 'ActiveTermNotFound') {
      return (
        <Alert>
          <Calendar className="h-4 w-4" />
          <AlertDescription>
            <p className="font-semibold">Aktif akademik dönem bulunmuyor</p>
            <p className="mt-1 text-sm text-muted-foreground">Yeni dönem açıldığında ders katalog burada görünecek.</p>
          </AlertDescription>
        </Alert>
      );
    }

    if (errorCode === 'ActiveProgramNotFound') {
      return (
        <Alert variant="destructive">
          <AlertTriangle className="h-4 w-4" />
          <AlertDescription>
            <p className="font-semibold">Aktif programınız bulunamadı</p>
            <p className="mt-1 text-sm">Lütfen öğrenci işleri birimi ile iletişime geçin.</p>
          </AlertDescription>
        </Alert>
      );
    }

    if (errorCode === 'StudentNotFound') {
      return (
        <Alert variant="destructive">
          <AlertTriangle className="h-4 w-4" />
          <AlertDescription>
            <p className="font-semibold">Öğrenci kaydına erişilemiyor</p>
            <p className="mt-1 text-sm">Hesabınızla ilişkili öğrenci kaydı bulunamadı. Lütfen öğrenci işleri birimi ile iletişime geçin.</p>
          </AlertDescription>
        </Alert>
      );
    }

    return (
      <Alert variant="destructive">
        <AlertCircle className="h-4 w-4" />
        <AlertDescription>
          <p className="font-semibold">Dersler yüklenirken hata oluştu</p>
          {errMsg && <p className="mt-1 text-sm">{errMsg}</p>}
          {!errMsg && <p className="mt-1 text-sm">Lütfen sayfayı yenileyip tekrar deneyin.</p>}
        </AlertDescription>
      </Alert>
    );
  }

  // Tek üst banner — öncelik: takvim eksikliği > kayıt kapalı > reddedilen plan
  const topBanner = (() => {
    if (term && !term.hasAcademicCalendar) {
      return (
        <Alert variant="warning">
          <AlertTriangle className="h-4 w-4" />
          <AlertDescription>
            <p className="font-semibold">Kayıt alınamıyor</p>
            <p className="mt-1 text-sm">
              Aktif dönemin akademik takvimi henüz tanımlanmamış. Öğrenci işleri daire başkanlığı ile iletişime geçin.
            </p>
          </AlertDescription>
        </Alert>
      );
    }
    if (!registrationActionsOpen) {
      return (
        <Alert>
          <Clock className="h-4 w-4" />
          <AlertDescription>{registrationClosedMessage}</AlertDescription>
        </Alert>
      );
    }
    return null;
  })();

  return (
    <div className="space-y-5">
      {topBanner}

      {/* ── Öğrenci bilgi kartı ── */}
      {student && (
        <Card className="overflow-hidden border-border/70 shadow-sm">
          <CardContent className="p-6">
            <div className="flex flex-col gap-4 border-b border-border/60 pb-5 sm:flex-row sm:items-center">
              <div className="flex h-16 w-16 shrink-0 items-center justify-center rounded-full bg-muted">
                <GraduationCap className="h-8 w-8 text-muted-foreground" aria-hidden />
              </div>
              <div className="flex-1 min-w-0">
                <h2 className="text-xl font-semibold text-foreground truncate">
                  {student.firstName} {student.lastName}
                </h2>
                <p className="text-sm text-muted-foreground">
                  {student.studentNumber} · {student.programName}
                </p>
              </div>
              <div className="flex items-baseline gap-2 sm:flex-col sm:items-end sm:gap-0">
                <span className="text-xs uppercase tracking-wide text-muted-foreground">GANO</span>
                <span className={`text-2xl font-semibold tabular-nums ${student.cgpa >= 2.0 ? 'text-success' : 'text-destructive'}`}>
                  {student.cgpa.toFixed(2)}
                </span>
              </div>
            </div>

            <div className="grid grid-cols-1 gap-x-10 gap-y-4 pt-5 sm:grid-cols-2">
              <InfoRow label="Sınıf / Dönem" value={`${classYear}. Sınıf · ${student.completedSemesters + 1}. Dönem`} />
              <InfoRow
                label="AKTS Limiti"
                value={ectsLimit > 0 ? `${ectsLimit} AKTS` : '—'}
              />
              {term && (
                <>
                  <InfoRow label="Kayıt Dönemi" value={`${formatDate(term.registrationStart)} – ${formatDate(term.registrationEnd)}`} />
                  <InfoRow label="Ek/Bırak Dönemi" value={`${formatDate(term.addDropStart)} – ${formatDate(term.addDropEnd)}`} />
                </>
              )}
              {allSelectedItems.length > 0 && (
                <>
                  <div className="grid grid-cols-[140px_1fr] gap-3 text-sm">
                    <span className="text-muted-foreground">Kesinleştirme</span>
                    <span>
                      {isFinalized ? (
                        <Badge variant="success">Kesinleştirildi</Badge>
                      ) : planItems.length > 0 ? (
                        <Badge variant="warning">Bekliyor</Badge>
                      ) : (
                        <Badge variant="secondary">—</Badge>
                      )}
                    </span>
                  </div>
                  <div className="grid grid-cols-[140px_1fr] gap-3 text-sm">
                    <span className="text-muted-foreground">Danışman</span>
                    <span>
                      {hasAdvisorApproval && confirmedItems.length > 0 ? (
                        <Badge variant="success">Onaylandı</Badge>
                      ) : confirmedItems.some(r => r.status === 'PendingAdvisorApproval') ? (
                        <Badge variant="warning">Onay Bekleniyor</Badge>
                      ) : (
                        <Badge variant="secondary">—</Badge>
                      )}
                    </span>
                  </div>
                </>
              )}
            </div>
          </CardContent>
        </Card>
      )}

      {/* ── Bildirimler ── */}
      {submitSuccess && (
        <Alert variant="success">
          <CheckCircle2 className="h-4 w-4" />
          <AlertDescription>Kayıt başarıyla kesinleştirildi.</AlertDescription>
        </Alert>
      )}
      {(validationErrors.length > 0 || validationWarnings.length > 0) && (
        <ValidationErrors errors={validationErrors} warnings={validationWarnings} />
      )}
      {rejectedItems.length > 0 && (
        <Alert variant="destructive">
          <AlertCircle className="h-4 w-4" />
          <AlertDescription>
            <strong>Kayıt planınız danışmanınız tarafından reddedildi.</strong>
            {advisorNote && <span className="ml-1">Notu: {advisorNote}</span>}
            <span className="ml-1">Planınızı düzenleyip tekrar gönderebilirsiniz.</span>
          </AlertDescription>
        </Alert>
      )}
      {alttanCourses.length > 0 && viewFilter !== 'alttan' && (
        <Alert variant="destructive">
          <RotateCcw className="h-4 w-4" />
          <AlertDescription>
            <strong>{alttanCourses.length} dersten</strong> başarısız (FF/NA) notunuz var ve tekrar almanız gerekmektedir:{' '}
            {alttanCourses.map(c => `${c.code} (${c.previousGrade})`).join(', ')}
          </AlertDescription>
        </Alert>
      )}

      {/* ── Seçilen dersler ── */}
      {allSelectedItems.length > 0 && (
        <Card className="overflow-hidden border-border/70 shadow-sm">
          <CardHeader className="pb-3">
            <div className="flex flex-wrap items-center justify-between gap-3">
              <CardTitle className="text-base">Seçilen Dersler</CardTitle>
              <div className="flex items-center gap-4 text-sm text-muted-foreground">
                <span>Ders: <strong className="text-foreground">{allSelectedItems.length}</strong></span>
                <span>Saat: <strong className="text-foreground">{totalHours}</strong></span>
              </div>
            </div>

            {ectsLimit > 0 && (
              <div className="flex flex-wrap items-center gap-3 pt-2">
                <span className="text-sm font-medium text-foreground tabular-nums whitespace-nowrap">
                  AKTS: {totalEcts} / {ectsLimit}
                </span>
                <Progress
                  value={ectsPercent}
                  indicatorClassName={ectsPercent > 90 ? 'bg-destructive' : undefined}
                  className="h-2 flex-1 max-w-[320px]"
                />
                {student && (
                  <span className="text-xs text-muted-foreground hidden lg:inline">
                    GANO {student.cgpa.toFixed(2)} → en fazla {ectsLimit} AKTS
                  </span>
                )}
              </div>
            )}
          </CardHeader>

          <div className="overflow-x-auto border-t border-border/60">
            <Table>
              <TableHeader>
                <TableRow className="bg-muted/30 hover:bg-muted/30">
                  <TableHead className="w-28 pl-6 font-semibold text-foreground">Ders Kodu</TableHead>
                  <TableHead className="font-semibold text-foreground">Ders Adı</TableHead>
                  <TableHead className="w-14 text-center font-semibold text-foreground">Z/S</TableHead>
                  <TableHead className="w-16 text-center font-semibold text-foreground">T+U</TableHead>
                  <TableHead className="w-16 text-center font-semibold text-foreground">AKTS</TableHead>
                  <TableHead className="hidden w-14 text-center font-semibold text-foreground md:table-cell">Sınıf</TableHead>
                  <TableHead className="hidden w-24 font-semibold text-foreground sm:table-cell">Alış</TableHead>
                  <TableHead className="w-28 text-center font-semibold text-foreground">Durum</TableHead>
                  <TableHead className="w-16 pr-6 text-right font-semibold text-foreground">İşlem</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {allSelectedItems.map(item => (
                  <TableRow key={item.registrationId} className="transition-colors hover:bg-muted/20">
                    <TableCell className="py-3 pl-6 font-mono text-xs font-semibold text-primary whitespace-nowrap">{item.courseCode}</TableCell>
                    <TableCell className="py-3 font-medium">{item.courseName}</TableCell>
                    <TableCell className="py-3 text-center">
                      <span className={`text-xs font-bold ${item.isCompulsory ? 'text-primary' : 'text-muted-foreground'}`}>
                        {item.isCompulsory ? 'Z' : 'S'}
                      </span>
                    </TableCell>
                    <TableCell className="py-3 text-center font-mono text-sm tabular-nums">{item.theoryHours}+{item.labHours}</TableCell>
                    <TableCell className="py-3 text-center">
                      <Badge variant="secondary" className="font-mono">{item.ects}</Badge>
                    </TableCell>
                    <TableCell className="hidden py-3 text-center text-sm text-muted-foreground tabular-nums md:table-cell">
                      {item.suggestedSemester > 0 ? item.suggestedSemester : '—'}
                    </TableCell>
                    <TableCell className="hidden py-3 sm:table-cell">
                      <span className={`text-xs font-medium ${TAKE_TYPE_COLORS[item.takeType] || 'text-muted-foreground'}`}>
                        {item.takeType}
                      </span>
                    </TableCell>
                    <TableCell className="py-3 text-center">
                      <RegistrationStatusBadge status={item.status} />
                    </TableCell>
                    <TableCell className="py-3 pr-6 text-right">
                      {(item.status === 'Draft' || item.status === 'Rejected') && (
                        <Button
                          variant="ghost"
                          size="icon"
                          onClick={() => handleRemove(item.registrationId)}
                          disabled={dropCourse.isPending || !registrationActionsOpen}
                          className="h-7 w-7 text-muted-foreground hover:bg-destructive/10 hover:text-destructive"
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

          {planItems.length > 0 && (
            <div className="flex justify-end border-t border-border/60 bg-muted/20 px-6 py-3">
              <Button
                onClick={handleSubmit}
                disabled={submitRegistration.isPending || !registrationActionsOpen}
                className="gap-2"
              >
                {submitRegistration.isPending ? (
                  <>
                    <Loader2 className="h-4 w-4 animate-spin" />
                    Gönderiliyor…
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
        </Card>
      )}

      {/* ── Açık dersler (müfredat slotları + akıllı planlama dahil) ── */}
      <Card className="overflow-hidden border-border/70 shadow-sm">
        <CardHeader className="pb-3">
          <div className="flex flex-wrap items-center justify-between gap-3">
            <div className="flex flex-wrap items-center gap-x-4 gap-y-2">
              <CardTitle className="text-base">Açık Dersler</CardTitle>

              <label className="flex items-center gap-2 text-xs text-muted-foreground">
                Sınıf
                <select
                  value={effectiveClassYear}
                  onChange={e => setClassYearFilter(Number(e.target.value))}
                  className="h-7 rounded-md border border-input bg-background px-2 text-xs outline-none focus:ring-1 focus:ring-ring"
                >
                  {classYearOptions().map(y => (
                    <option key={y} value={y}>{y}. Sınıf</option>
                  ))}
                </select>
              </label>

              <div className="flex items-center gap-2 text-xs text-muted-foreground">
                <span>Dönem</span>
                {(['all', 'guz', 'bahar'] as const).map(val => (
                  <label key={val} className="flex cursor-pointer items-center gap-1">
                    <input
                      type="radio"
                      name="termType"
                      value={val}
                      checked={termTypeFilter === val}
                      onChange={() => setTermTypeFilter(val)}
                      className="h-3 w-3 accent-primary"
                    />
                    {val === 'all' ? 'Tümü' : val === 'guz' ? 'Güz' : 'Bahar'}
                  </label>
                ))}
              </div>

              <div className="flex items-center gap-2 text-xs text-muted-foreground">
                <span>Durum</span>
                <label className="flex cursor-pointer items-center gap-1">
                  <input
                    type="radio"
                    name="viewFilter"
                    value="all"
                    checked={viewFilter === 'all'}
                    onChange={() => setViewFilter('all')}
                    className="h-3 w-3 accent-primary"
                  />
                  Tümü ({statusCounts.total})
                </label>
                <label className="flex cursor-pointer items-center gap-1">
                  <input
                    type="radio"
                    name="viewFilter"
                    value="new"
                    checked={viewFilter === 'new'}
                    onChange={() => setViewFilter('new')}
                    className="h-3 w-3 accent-primary"
                  />
                  Alınmamış ({statusCounts.new_})
                </label>
                <label className="flex cursor-pointer items-center gap-1">
                  <input
                    type="radio"
                    name="viewFilter"
                    value="taken"
                    checked={viewFilter === 'taken'}
                    onChange={() => setViewFilter('taken')}
                    className="h-3 w-3 accent-primary"
                  />
                  Önceden ({statusCounts.taken})
                </label>
                {statusCounts.alttan > 0 && (
                  <label className="flex cursor-pointer items-center gap-1 text-destructive">
                    <input
                      type="radio"
                      name="viewFilter"
                      value="alttan"
                      checked={viewFilter === 'alttan'}
                      onChange={() => setViewFilter('alttan')}
                      className="h-3 w-3 accent-destructive"
                    />
                    Alttan ({statusCounts.alttan})
                  </label>
                )}
              </div>
            </div>

            <Button asChild variant="outline" size="sm" className="gap-2">
              <Link to={ROUTES.SMART_PLANNING}>
                <Sparkles className="h-3.5 w-3.5" />
                Akıllı Planlamaya Git
              </Link>
            </Button>
          </div>
        </CardHeader>

        <CardContent className="pt-0">
          {(() => {
            // Müfredat slot satırları — derslerle aynı listede, ders satırı görsel pattern'ı
            const currentSemester = (student?.completedSemesters ?? 0) + 1;
            const visiblePools = poolStatus.length === 0
              ? []
              : (showUpperSemesterPools
                  ? poolStatus
                  : poolStatus.filter(p => p.suggestedSemesterStart <= currentSemester));
            const hiddenUpperCount = poolStatus.length - visiblePools.length;

            if (filteredCourses.length === 0 && visiblePools.length === 0) {
              return (
                <div className="py-12 text-center">
                  <BookX className="mx-auto mb-3 h-10 w-10 text-muted-foreground/40" />
                  <p className="font-medium text-foreground">Ders bulunamadı</p>
                  <p className="mt-1 text-sm text-muted-foreground">
                    {viewFilter !== 'all' || termTypeFilter !== 'all'
                      ? 'Seçilen filtrelere uygun ders bulunmamaktadır.'
                      : 'Bu dönem için açık ders bulunmamaktadır.'}
                  </p>
                </div>
              );
            }

            return (
              <div className="space-y-3">
                {visiblePools.map(pool => (
                  <button
                    key={`pool-${pool.poolId}`}
                    type="button"
                    onClick={() => setSlotCandidatesTarget(pool)}
                    className={cn(
                      'w-full flex items-center justify-between gap-4 rounded-md border bg-card p-4 text-left transition-all duration-200 hover:shadow-sm',
                      pool.isFulfilled
                        ? 'border-success/40 bg-success/5 hover:bg-success/10'
                        : 'border-warning/40 bg-warning/5 hover:bg-warning/10',
                    )}
                  >
                    <div className="flex flex-wrap items-center gap-2.5 min-w-0">
                      <Layers className="h-4 w-4 text-muted-foreground" />
                      <span className="font-mono text-sm font-semibold text-foreground">
                        {pool.slotName || pool.poolCode}
                      </span>
                      <Badge variant="secondary" className="text-[11px]">Seçmeli Grup</Badge>
                      {pool.requiredEcts > 0 && (
                        <Badge variant="default" className="text-[11px] font-mono">
                          {pool.requiredEcts} AKTS
                        </Badge>
                      )}
                      {pool.isFulfilled ? (
                        <Badge variant="success" className="text-[11px] gap-1">
                          <CheckCircle2 className="h-3 w-3" />
                          Dolduruldu
                        </Badge>
                      ) : (
                        <Badge variant="warning" className="text-[11px] gap-1">
                          <CircleAlert className="h-3 w-3" />
                          {pool.completedCourseCount}/{pool.requiredCourseCount} ders
                          {pool.requiredEcts > 0 && ` · ${pool.earnedEcts}/${pool.requiredEcts} AKTS`}
                        </Badge>
                      )}
                    </div>
                    <div className="flex items-center gap-2 shrink-0 ml-4">
                      <span className="text-xs text-muted-foreground">aday dersler</span>
                      <ChevronRight className="h-4 w-4 text-muted-foreground" />
                    </div>
                  </button>
                ))}

                {(showUpperSemesterPools || hiddenUpperCount > 0) && (
                  <label className="flex cursor-pointer select-none items-center gap-2 px-1 py-1 text-xs text-muted-foreground">
                    <input
                      type="checkbox"
                      checked={showUpperSemesterPools}
                      onChange={e => setShowUpperSemesterPools(e.target.checked)}
                      className="h-3.5 w-3.5 cursor-pointer rounded border-input"
                    />
                    Üst yarıyıl havuzlarını da göster
                    {!showUpperSemesterPools && hiddenUpperCount > 0 && (
                      <span>({hiddenUpperCount} gizli)</span>
                    )}
                  </label>
                )}

                {filteredCourses.map(course => (
                  <CourseCard
                    key={course.courseId}
                    course={course}
                    onAddToPlan={handleAddToPlan}
                    isAdding={addToPlan.isPending}
                    registeredSectionIds={registeredSectionIds}
                    registrationActionsOpen={registrationActionsOpen}
                  />
                ))}
              </div>
            );
          })()}
        </CardContent>
      </Card>

      {/* ── Slot aday dersler dialog ── */}
      <Dialog
        open={slotCandidatesTarget !== null}
        onOpenChange={(o) => { if (!o) setSlotCandidatesTarget(null); }}
      >
        <DialogContent className="max-w-2xl">
          <DialogHeader>
            <DialogTitle>
              {slotCandidatesTarget && (
                <span>
                  <span className="font-mono text-foreground">{slotCandidatesTarget.poolCode} / {slotCandidatesTarget.slotName}</span>
                  <span className="ml-2 text-sm font-normal text-muted-foreground">
                    — {slotCandidatesTarget.poolName}
                  </span>
                </span>
              )}
            </DialogTitle>
            <DialogDescription className="text-xs">
              Bu slot için müfredatında aday olan dersler. Listedeki bir derse kayıt olduğunda bu slot için sayılır.
            </DialogDescription>
          </DialogHeader>
          {slotCandidatesTarget && (
            <SlotCandidatesTable
              pool={slotCandidatesTarget}
              courses={(courses ?? []).filter(c =>
                c.applicablePoolSlots.some(s => s.poolId === slotCandidatesTarget.poolId)
              )}
            />
          )}
        </DialogContent>
      </Dialog>
    </div>
  );
}

interface InfoRowProps {
  label: string;
  value: string;
}

function InfoRow({ label, value }: InfoRowProps) {
  return (
    <div className="grid grid-cols-[140px_1fr] gap-3 text-sm">
      <span className="text-muted-foreground">{label}</span>
      <span className="font-medium text-foreground">{value && value.trim().length > 0 ? value : '—'}</span>
    </div>
  );
}
