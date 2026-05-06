import { useState, useMemo } from 'react';
import { Link } from 'react-router-dom';
import { Skeleton } from '@/components/ui/skeleton';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Progress } from '@/components/ui/progress';
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
import { useStudentPoolStatus, type PoolProgressDto } from '../hooks/useStudentPoolStatus';
import { useActiveTerm } from '../hooks/useActiveTerm';
import {
  Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle,
} from '@/components/ui/dialog';
import { formatDate } from '../lib/utils';
import { ROUTES } from '../lib/constants';
import type { ValidationError } from '../types/registration';
import {
  AlertCircle, AlertTriangle, BookX, Trash2, Send, CheckCircle2,
  CircleAlert, Layers, Loader2, RotateCcw, GraduationCap, Calendar, Clock, User,
} from 'lucide-react';

function getEctsExplanation(cgpa: number, ectsLimit: number): string {
  if (cgpa >= 2.50) return `GANO 2.50 ve üstü olduğundan ${ectsLimit} AKTS alınabilir.`;
  if (cgpa >= 2.00) return `GANO 2.00-2.49 arasında olduğundan ${ectsLimit} AKTS alınabilir.`;
  return `GANO 2.00 altında olduğundan ${ectsLimit} AKTS alınabilir.`;
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
  const classYear = student ? Math.min(Math.floor(student.completedSemesters / 2) + 1, 4) : 1;
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
  const ectsLimit = student?.ectsLimit ?? 30;
  const ectsPercent = Math.min((totalEcts / ectsLimit) * 100, 100);
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

    addToPlan.mutate(sectionId);
  };

  const handleRemove = (registrationId: number) => {
    if (!registrationActionsOpen) {
      setValidationErrors([{ rule: 'RegistrationClosed', message: registrationClosedMessage }]);
      setValidationWarnings([]);
      return;
    }

    dropCourse.mutate(registrationId);
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
        if (data.validationResult && !data.validationResult.isValid) {
          setValidationErrors(data.validationResult.errors);
          setValidationWarnings(data.validationResult.warnings ?? []);
        } else if (data.success) {
          setSubmitSuccess(true);
          setValidationErrors([]);
          setValidationWarnings(data.validationResult?.warnings ?? []);
        }
      },
      onError: (error) => {
        setValidationWarnings([]);
        setValidationErrors([{ rule: 'System', message: error instanceof Error ? error.message : 'Kayit sirasinda bir hata olustu.' }]);
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
    const errMsg = error instanceof Error ? error.message : '';

    if (errMsg.includes('yayınlanmadı') || errMsg.includes('Dönem durumu')) {
      return (
        <Alert variant="default" className="border-blue-200 bg-blue-50 dark:border-blue-800 dark:bg-blue-950">
          <Clock className="h-4 w-4 text-blue-600" />
          <AlertDescription className="text-blue-800 dark:text-blue-200">
            <p className="font-semibold">Ders katalogu henuz yayinlanmadi</p>
            <p className="mt-1 text-sm">Aktif donem su anda planlama asamasinda. Ders katalog yayinlandiginda bu sayfada gorebilirsin.</p>
          </AlertDescription>
        </Alert>
      );
    }

    if (errMsg.includes('Aktif donem bulunamadi')) {
      return (
        <Alert variant="default" className="border-blue-200 bg-blue-50 dark:border-blue-800 dark:bg-blue-950">
          <Calendar className="h-4 w-4 text-blue-600" />
          <AlertDescription className="text-blue-800 dark:text-blue-200">
            <p className="font-semibold">Aktif akademik donem bulunmuyor</p>
            <p className="mt-1 text-sm">Yeni donem acildiginda ders katalog burada gorunecek.</p>
          </AlertDescription>
        </Alert>
      );
    }

    if (errMsg.includes('Aktif program bulunamadi')) {
      return (
        <Alert variant="destructive">
          <AlertTriangle className="h-4 w-4" />
          <AlertDescription>
            <p className="font-semibold">Aktif programiniz bulunamadi</p>
            <p className="mt-1 text-sm">Lutfen ogrenci isleri birimi ile iletisime gecin.</p>
          </AlertDescription>
        </Alert>
      );
    }

    if (errMsg.includes('Ogrenci bilgisi bulunamadi')) {
      return (
        <Alert variant="destructive">
          <AlertTriangle className="h-4 w-4" />
          <AlertDescription>
            <p className="font-semibold">Ogrenci kaydina erisilemiyor</p>
            <p className="mt-1 text-sm">Hesabinizla iliskili ogrenci kaydi bulunamadi. Lutfen ogrenci isleri birimi ile iletisime gecin.</p>
          </AlertDescription>
        </Alert>
      );
    }

    return (
      <Alert variant="destructive">
        <AlertCircle className="h-4 w-4" />
        <AlertDescription>
          <p className="font-semibold">Dersler yuklenirken hata olustu</p>
          {errMsg && <p className="mt-1 text-sm">{errMsg}</p>}
          {!errMsg && <p className="mt-1 text-sm">Lutfen sayfayi yenileyip tekrar deneyin.</p>}
        </AlertDescription>
      </Alert>
    );
  }

  return (
    <div className="space-y-5">
      {/* ── Takvim eksikliği uyarısı ── */}
      {term && !term.hasAcademicCalendar && (
        <div className="rounded-lg border-2 border-amber-300 bg-amber-50 p-4 flex items-start gap-3">
          <AlertTriangle className="w-5 h-5 text-amber-600 flex-shrink-0 mt-0.5" />
          <div>
            <h3 className="font-semibold text-amber-900">Kayıt alınamıyor</h3>
            <p className="text-sm text-amber-800 mt-1">
              Aktif dönemin akademik takvimi henüz tanımlanmamış. Kayıt ve ekle/bırak dönemleri belli değil. Öğrenci işleri daire başkanlığı ile iletişime geçin.
            </p>
          </div>
        </div>
      )}

      {/* ── Öğrenci Bilgileri + Durum ── */}
      {student && (
        <Card className="overflow-hidden shadow-sm">
          <CardContent className="p-0">
            {/* Kompakt öğrenci bilgi şeridi */}
            <div className="bg-[#003366] px-6 py-4">
              <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
                <InfoCell
                  icon={User}
                  label="Öğrenci"
                  value={`${student.studentNumber} — ${student.firstName} ${student.lastName}`}
                />
                <InfoCell
                  icon={GraduationCap}
                  label="Program"
                  value={`${student.programName}`}
                />
                <InfoCell
                  icon={Calendar}
                  label="Sınıf / Dönem"
                  value={`${classYear}. Sınıf, ${student.completedSemesters + 1}. Dönem`}
                />
                <InfoCell
                  label="Genel Ortalama"
                  value={student.cgpa.toFixed(2)}
                  highlight={student.cgpa >= 2.0 ? 'success' : 'danger'}
                />
              </div>
            </div>

            {/* Kayıt tarihleri + durum */}
            <div className="px-6 py-3 flex flex-wrap items-center gap-x-8 gap-y-2 text-sm border-b bg-muted/20">
              {term && (
                <>
                  <div className="flex items-center gap-1.5 text-muted-foreground">
                    <Calendar className="h-3.5 w-3.5" />
                    <span>Kayıt: <span className="text-foreground font-medium">{formatDate(term.registrationStart)} – {formatDate(term.registrationEnd)}</span></span>
                  </div>
                  <div className="flex items-center gap-1.5 text-muted-foreground">
                    <Clock className="h-3.5 w-3.5" />
                    <span>Ek/Bırak: <span className="text-foreground font-medium">{formatDate(term.addDropStart)} – {formatDate(term.addDropEnd)}</span></span>
                  </div>
                </>
              )}

              {allSelectedItems.length > 0 && (
                <div className="flex items-center gap-4 ml-auto">
                  <div className="flex items-center gap-1.5">
                    <span className="text-muted-foreground text-xs">Kesinleştirme:</span>
                    {isFinalized ? (
                      <Badge variant="success" className="text-[10px]">Kesinleştirildi</Badge>
                    ) : planItems.length > 0 ? (
                      <Badge variant="warning" className="text-[10px]">Bekliyor</Badge>
                    ) : (
                      <Badge variant="secondary" className="text-[10px]">—</Badge>
                    )}
                  </div>
                  <div className="flex items-center gap-1.5">
                    <span className="text-muted-foreground text-xs">Danışman:</span>
                    {hasAdvisorApproval && confirmedItems.length > 0 ? (
                      <Badge variant="success" className="text-[10px]">Onaylandı</Badge>
                    ) : confirmedItems.some(r => r.status === 'PendingAdvisorApproval') ? (
                      <Badge variant="warning" className="text-[10px]">Onay Bekleniyor</Badge>
                    ) : (
                      <Badge variant="secondary" className="text-[10px]">—</Badge>
                    )}
                  </div>
                </div>
              )}
            </div>
          </CardContent>
        </Card>
      )}

      <Card className="border-dashed border-primary/30 bg-gradient-to-r from-primary/5 via-background to-accent/10">
        <CardContent className="flex flex-col gap-4 p-5 lg:flex-row lg:items-center lg:justify-between">
          <div className="space-y-1">
            <div className="inline-flex items-center gap-2 rounded-full bg-primary/10 px-3 py-1 text-xs font-semibold text-primary">
              <GraduationCap className="h-3.5 w-3.5" />
              Yeni
            </div>
            <p className="text-base font-semibold text-foreground">Akilli ders onerileri hazir</p>
            <p className="text-sm text-muted-foreground">
              Bu donem icin sistemin onerilerini skor, zorluk ve kritik yol etkisiyle birlikte inceleyebilirsin.
            </p>
          </div>
          <Button asChild className="gap-2 rounded-xl bg-[#003366] hover:bg-[#002244]">
            <Link to={ROUTES.SMART_PLANNING}>
              <GraduationCap className="h-4 w-4" />
              Akilli Planlama Sayfasina Git
            </Link>
          </Button>
        </CardContent>
      </Card>

      {/* ── Müfredat Slotlarım (Faz C C2 + C4) ── */}
      {poolStatus.length > 0 && (() => {
        const currentSemester = (student?.completedSemesters ?? 0) + 1;
        const visiblePools = showUpperSemesterPools
          ? poolStatus
          : poolStatus.filter(p => p.suggestedSemesterStart <= currentSemester);
        const hiddenUpperCount = poolStatus.length - visiblePools.length;

        return (
        <Card>
          <CardContent className="px-5 py-4">
            <div className="flex items-center justify-between mb-3 gap-3 flex-wrap">
              <div className="flex items-center gap-2">
                <Layers className="h-4 w-4 text-violet-700" />
                <h3 className="font-semibold text-sm text-foreground">Müfredat Slotlarım</h3>
                <Badge variant="secondary" className="text-[10px]">
                  {visiblePools.filter(p => p.isFulfilled).length}/{visiblePools.length} doldu
                </Badge>
              </div>
              {/* Faz C C4: üstten yarıyıl toggle */}
              {(showUpperSemesterPools || hiddenUpperCount > 0) && (
                <label className="flex items-center gap-2 text-xs cursor-pointer select-none">
                  <input
                    type="checkbox"
                    checked={showUpperSemesterPools}
                    onChange={e => setShowUpperSemesterPools(e.target.checked)}
                    className="h-3.5 w-3.5 rounded border-input accent-violet-600 cursor-pointer"
                  />
                  <span className="text-muted-foreground">
                    Üstten yarıyıl havuzlarını göster
                    {!showUpperSemesterPools && hiddenUpperCount > 0 && (
                      <span className="ml-1 text-violet-700">({hiddenUpperCount} gizli)</span>
                    )}
                  </span>
                </label>
              )}
            </div>
            <p className="text-[11px] text-muted-foreground mb-3">
              Müfredatındaki seçmeli grup slot'ları. Slot'a tıklayarak o slot için aday dersleri görebilirsin.
            </p>
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-2">
              {visiblePools.map(pool => (
                <button
                  key={pool.poolId}
                  type="button"
                  onClick={() => setSlotCandidatesTarget(pool)}
                  className={`text-left border rounded-lg p-3 transition-colors ${
                    pool.isFulfilled
                      ? 'border-success/40 bg-success/5 hover:bg-success/10'
                      : 'border-border hover:bg-violet-50/40 hover:border-violet-300'
                  }`}
                >
                  <div className="flex items-center gap-2 mb-1.5">
                    {pool.isFulfilled
                      ? <CheckCircle2 className="h-4 w-4 text-success shrink-0" />
                      : <CircleAlert className="h-4 w-4 text-amber-500 shrink-0" />
                    }
                    <span className="text-xs font-mono font-bold text-foreground">
                      {pool.poolCode}
                      {pool.slotName && (
                        <span className="text-violet-700 font-semibold"> / {pool.slotName}</span>
                      )}
                    </span>
                  </div>
                  <p className="text-[11px] text-muted-foreground truncate mb-1.5">{pool.poolName}</p>
                  <div className="flex items-center justify-between text-[10px]">
                    <span className="text-muted-foreground">
                      {pool.requiredCourseCount > 0 && `${pool.completedCourseCount}/${pool.requiredCourseCount} ders`}
                      {pool.requiredCourseCount > 0 && pool.requiredEcts > 0 && ' · '}
                      {pool.requiredEcts > 0 && `${pool.earnedEcts}/${pool.requiredEcts} AKTS`}
                    </span>
                    <span className="text-violet-700">→ adaylar</span>
                  </div>
                </button>
              ))}
            </div>
            {visiblePools.length === 0 && (
              <p className="text-xs text-muted-foreground italic text-center py-4">
                Bu yarıyıla denk gelen aktif slot yok. Üstten yarıyıl havuzlarını
                görmek için yukarıdaki kutuyu işaretle.
              </p>
            )}
          </CardContent>
        </Card>
        );
      })()}

      {/* ── Uyarılar ── */}
      {submitSuccess && (
        <Alert variant="success">
          <CheckCircle2 className="h-4 w-4" />
          <AlertDescription>Kayıt başarıyla kesinleştirildi!</AlertDescription>
        </Alert>
      )}
      {(validationErrors.length > 0 || validationWarnings.length > 0) && (
        <ValidationErrors errors={validationErrors} warnings={validationWarnings} />
      )}
      {!registrationActionsOpen && (
        <Alert variant="warning">
          <AlertTriangle className="h-4 w-4" />
          <AlertDescription>{registrationClosedMessage}</AlertDescription>
        </Alert>
      )}
      {addToPlan.error && (
        <Alert variant="destructive">
          <AlertCircle className="h-4 w-4" />
          <AlertDescription>{addToPlan.error instanceof Error ? addToPlan.error.message : 'Plana eklenirken hata oluştu. Lütfen tekrar deneyin.'}</AlertDescription>
        </Alert>
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
            <strong>{alttanCourses.length} ders</strong>ten başarısız (FF/NA) notunuz var ve tekrar almanız gerekmektedir:{' '}
            {alttanCourses.map(c => `${c.code} (${c.previousGrade})`).join(', ')}
          </AlertDescription>
        </Alert>
      )}

      {/* ── Seçilen Dersler ── */}
      {allSelectedItems.length > 0 && (
        <Card className="overflow-hidden shadow-sm">
          <CardContent className="p-0">
            {/* AKTS Progress Bar — en üstte, dikkat çekici */}
            <div className="px-6 py-3 bg-muted/30 border-b flex flex-wrap items-center gap-x-6 gap-y-2">
              <div className="flex items-center gap-3 flex-1 min-w-[200px]">
                <span className="text-sm font-semibold text-foreground whitespace-nowrap">
                  AKTS: {totalEcts}/{ectsLimit}
                </span>
                <Progress
                  value={ectsPercent}
                  indicatorClassName={ectsPercent > 90 ? 'bg-destructive' : 'bg-[#003366]'}
                  className="h-2.5 flex-1 max-w-[300px]"
                />
                {student && (
                  <span className="text-[11px] text-muted-foreground hidden lg:inline">
                    ({getEctsExplanation(student.cgpa, ectsLimit)})
                  </span>
                )}
              </div>
              <div className="flex items-center gap-4 text-sm text-muted-foreground">
                <span>Saat: <strong className="text-foreground">{totalHours}</strong></span>
                <span>Ders: <strong className="text-foreground">{allSelectedItems.length}</strong></span>
              </div>
            </div>

            {/* Tablo */}
            <div className="overflow-x-auto">
              <Table>
                <TableHeader>
                  <TableRow className="bg-muted/20 hover:bg-muted/20">
                    <TableHead className="w-28 pl-6 font-semibold">Ders Kodu</TableHead>
                    <TableHead className="font-semibold">Ders Adı</TableHead>
                    <TableHead className="text-center w-14 font-semibold">Z/S</TableHead>
                    <TableHead className="text-center w-16 font-semibold">T+U</TableHead>
                    <TableHead className="text-center w-16 font-semibold">AKTS</TableHead>
                    <TableHead className="text-center w-14 hidden md:table-cell font-semibold">Snf</TableHead>
                    <TableHead className="w-24 hidden sm:table-cell font-semibold">Alış</TableHead>
                    <TableHead className="text-center w-24 font-semibold">Durum</TableHead>
                    <TableHead className="text-right w-16 pr-6 font-semibold">İşlem</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {allSelectedItems.map((item, idx) => (
                    <TableRow key={item.registrationId} className={idx % 2 === 1 ? 'bg-muted/10' : undefined}>
                      <TableCell className="pl-6 font-mono text-sm font-semibold text-[#003366]">{item.courseCode}</TableCell>
                      <TableCell className="font-medium">{item.courseName}</TableCell>
                      <TableCell className="text-center">
                        <span className={`text-xs font-bold ${item.isCompulsory ? 'text-[#003366]' : 'text-accent'}`}>
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
                      <TableCell className="text-right pr-6">
                        {(item.status === 'Draft' || item.status === 'Rejected') && (
                          <Button
                            variant="ghost"
                            size="icon"
                            onClick={() => handleRemove(item.registrationId)}
                            disabled={dropCourse.isPending || !registrationActionsOpen}
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

            {/* Kesinleştir butonu */}
            {planItems.length > 0 && (
              <div className="px-6 py-3 border-t flex justify-end bg-muted/10">
                <Button
                  size="lg"
                  onClick={handleSubmit}
                  disabled={submitRegistration.isPending || !registrationActionsOpen}
                  className="gap-2 rounded-xl bg-[#003366] hover:bg-[#002244]"
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

      {/* ── Açık Dersler ── */}
      <div className="space-y-3">
        {/* Filtre çubuğu */}
        <Card className="overflow-hidden shadow-sm">
          <CardContent className="px-5 py-3">
            <div className="flex flex-wrap items-center gap-3">
              <h3 className="text-sm font-bold text-foreground mr-1">Açık Dersler</h3>

              <div className="h-5 w-px bg-border hidden sm:block" />

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

              <div className="h-5 w-px bg-border hidden sm:block" />

              <div className="flex items-center gap-1.5">
                <span className="text-xs text-muted-foreground">Sınıf:</span>
                <select
                  value={effectiveClassYear}
                  onChange={e => setClassYearFilter(Number(e.target.value))}
                  className="h-7 text-xs rounded-md border border-border bg-background px-2 outline-none focus:ring-1 focus:ring-primary"
                >
                  {[1, 2, 3, 4].map(y => (
                    <option key={y} value={y}>{y}. Sınıf</option>
                  ))}
                </select>
              </div>

              <div className="h-5 w-px bg-border hidden sm:block" />

              <div className="flex items-center gap-2">
                {(['all', 'guz', 'bahar'] as const).map(val => (
                  <label key={val} className="flex items-center gap-1 cursor-pointer">
                    <input
                      type="radio"
                      name="termType"
                      value={val}
                      checked={termTypeFilter === val}
                      onChange={() => setTermTypeFilter(val)}
                      className="accent-[#003366] w-3 h-3"
                    />
                    <span className="text-xs text-muted-foreground">
                      {val === 'all' ? 'Tümü' : val === 'guz' ? 'Güz' : 'Bahar'}
                    </span>
                  </label>
                ))}
              </div>

              <span className="text-xs text-muted-foreground ml-auto font-medium">
                {filteredCourses.length} ders
              </span>
            </div>
          </CardContent>
        </Card>

        {/* Ders listesi */}
        {filteredCourses.length > 0 ? (
          filteredCourses.map(course => (
            <CourseCard
              key={course.courseId}
              course={course}
              onAddToPlan={handleAddToPlan}
              isAdding={addToPlan.isPending}
              registeredSectionIds={registeredSectionIds}
              registrationActionsOpen={registrationActionsOpen}
            />
          ))
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

      {/* ── Slot Aday Dersler Dialog (Faz C C2) ── */}
      <Dialog
        open={slotCandidatesTarget !== null}
        onOpenChange={(o) => { if (!o) setSlotCandidatesTarget(null); }}
      >
        <DialogContent className="max-w-2xl">
          <DialogHeader>
            <DialogTitle>
              {slotCandidatesTarget && (
                <span>
                  <span className="font-mono text-violet-700">{slotCandidatesTarget.poolCode} / {slotCandidatesTarget.slotName}</span>
                  <span className="ml-2 text-sm font-normal text-muted-foreground">
                    — {slotCandidatesTarget.poolName}
                  </span>
                </span>
              )}
            </DialogTitle>
            <DialogDescription className="text-xs">
              Bu slot için müfredatında aday olan dersler. Listedeki bir derse kayıt olduğunda
              bu slot için sayılır.
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

/* ── Slot Aday Dersler Tablosu ── */

function SlotCandidatesTable({
  pool,
  courses,
}: {
  pool: PoolProgressDto;
  courses: import('../types/course').StudentCourseDto[];
}) {
  if (courses.length === 0) {
    return (
      <div className="text-center py-8 text-sm text-muted-foreground">
        Bu slot için aday ders bulunamadı.
        <p className="text-xs mt-1">
          Grup boş olabilir veya öğrenci kaydı için açık dersi yok.
        </p>
      </div>
    );
  }

  return (
    <div className="space-y-3">
      <div className="flex items-center justify-between text-xs text-muted-foreground">
        <span>{courses.length} aday ders</span>
        {pool.ectsPerCourse !== null && (
          <Badge variant="outline" className="text-[10px] bg-blue-50 text-blue-700 border-blue-200">
            Bu havuzda her ders {pool.ectsPerCourse} AKTS
          </Badge>
        )}
      </div>
      <div className="overflow-y-auto max-h-[60vh] -mx-1">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead className="w-24">Kod</TableHead>
              <TableHead>Ad</TableHead>
              <TableHead className="text-right w-16">AKTS</TableHead>
              <TableHead className="text-center w-24">Durum</TableHead>
              <TableHead className="text-center w-20">Açık?</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {courses.map(c => (
              <TableRow key={c.courseId}>
                <TableCell className="font-mono text-xs">{c.code}</TableCell>
                <TableCell className="text-sm">{c.name}</TableCell>
                <TableCell className="text-right tabular-nums">{c.ects}</TableCell>
                <TableCell className="text-center">
                  {c.takeStatus === 'Alttan' && (
                    <Badge variant="destructive" className="text-[10px]">Alttan</Badge>
                  )}
                  {c.takeStatus === 'OncedenAlindi' && (
                    <Badge variant="secondary" className="text-[10px]">Alındı</Badge>
                  )}
                  {c.takeStatus === 'IlkDefa' && (
                    <Badge variant="outline" className="text-[10px]">İlk defa</Badge>
                  )}
                </TableCell>
                <TableCell className="text-center">
                  {c.sections.length > 0 ? (
                    <Badge variant="outline" className="text-[10px] bg-emerald-50 text-emerald-700 border-emerald-200">
                      {c.sections.length} şube
                    </Badge>
                  ) : (
                    <span className="text-[10px] text-muted-foreground italic">Açık değil</span>
                  )}
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </div>
    </div>
  );
}

/* ── Yardımcı bileşenler ── */

function InfoCell({ icon: Icon, label, value, highlight }: {
  icon?: React.ComponentType<{ className?: string }>;
  label: string;
  value: string;
  highlight?: 'success' | 'danger';
}) {
  return (
    <div className="flex items-center gap-2.5 min-w-0">
      {Icon && (
        <div className="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg bg-white/10">
          <Icon className="h-4 w-4 text-white/80" />
        </div>
      )}
      <div className="min-w-0">
        <p className="text-[10px] uppercase tracking-wider text-white/50 font-medium">{label}</p>
        <p className={`text-sm font-semibold truncate ${
          highlight === 'success' ? 'text-emerald-300' :
          highlight === 'danger' ? 'text-red-300' :
          'text-white'
        }`}>
          {value}
        </p>
      </div>
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
      className={`h-7 text-xs rounded-lg ${active && !variant ? 'bg-[#003366] hover:bg-[#002244]' : ''}`}
    >
      {children}
    </Button>
  );
}
