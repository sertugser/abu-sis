import { useEffect, useMemo, useState } from 'react';
import { useCourseHistory } from '../hooks/useCourseHistory';
import { downloadCourseHistoryPdf } from '../api/registration.api';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { TablePagination } from '@/components/ui/table-pagination';
import { AlertCircle, GraduationCap, Printer } from 'lucide-react';
import { cn } from '@/lib/utils';
import { getGradeBadgeClass } from '@/lib/gradeUtils';
import { CourseActionMenu } from '@/components/my-courses/CourseActionMenu';
import { CourseInfoSheet } from '@/components/my-courses/CourseInfoSheet';
import { CourseDetailSheet } from '@/components/my-courses/CourseDetailSheet';
import { TermPickerCombobox } from '@/components/my-courses/TermPickerCombobox';
import { NotlarInfoBanner } from '@/components/my-courses/NotlarInfoBanner';
import { useToast } from '@/components/ui/global-toast';
import { toApiError } from '@/lib/apiError';

const DEFAULT_PAGE_SIZE = 10;

export default function MyCoursesPage() {
  const { data: history, isLoading, error, refetch } = useCourseHistory();
  const { showError } = useToast();
  const [selectedTermId, setSelectedTermId] = useState<number | null>(null);
  const [infoCourseId, setInfoCourseId] = useState<number | null>(null);
  const [detailRegistrationId, setDetailRegistrationId] = useState<number | null>(null);
  const [isPrinting, setIsPrinting] = useState(false);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(DEFAULT_PAGE_SIZE);

  // Varsayılan dönem: backend listesinin ilk öğesi (en yeni = aktif/son dönem).
  // Kullanıcı seçmediği sürece sayfa her zaman tek bir dönemi gösterir.
  useEffect(() => {
    if (selectedTermId === null && history && history.length > 0) {
      setSelectedTermId(history[0].termId);
    }
  }, [history, selectedTermId]);

  const selectedTerm = useMemo(
    () => history?.find(t => t.termId === selectedTermId) ?? null,
    [history, selectedTermId],
  );

  // Pagination: yalnız seçili dönemin dersleri
  const courses = selectedTerm?.courses ?? [];
  const totalCourses = courses.length;
  const totalPages = Math.max(1, Math.ceil(totalCourses / pageSize));
  const safePage = Math.min(Math.max(1, page), totalPages);
  const pagedCourses = useMemo(
    () => courses.slice((safePage - 1) * pageSize, safePage * pageSize),
    [courses, safePage, pageSize],
  );

  // Dönem değişince sayfayı sıfırla
  useEffect(() => {
    setPage(1);
  }, [selectedTermId, pageSize]);

  const handlePrint = async () => {
    if (selectedTermId === null) return;
    try {
      setIsPrinting(true);
      const blob = await downloadCourseHistoryPdf(selectedTermId);
      const url = window.URL.createObjectURL(blob);
      const newWindow = window.open(url, '_blank', 'noopener,noreferrer');
      // Yeni sekme açılamadıysa (popup blocker) indirme tetikle
      if (!newWindow) {
        const a = document.createElement('a');
        a.href = url;
        a.download = `alinan-dersler-${new Date().toISOString().slice(0, 10)}.pdf`;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
      }
      // URL'i sonra revoke et — yeni sekme yüklemeyi bitirsin
      setTimeout(() => window.URL.revokeObjectURL(url), 60_000);
    } catch (err) {
      showError('PDF oluşturulamadı', toApiError(err, 'Alınan dersler raporu hazırlanamadı.').message);
    } finally {
      setIsPrinting(false);
    }
  };

  if (isLoading) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-10 w-64" />
        <Skeleton className="h-64 rounded-xl" />
      </div>
    );
  }

  if (error) {
    return (
      <Alert variant="destructive">
        <AlertCircle className="h-4 w-4" />
        <AlertDescription className="flex items-center justify-between gap-4">
          <span>Ders geçmişi yüklenirken hata oluştu.</span>
          <Button size="sm" variant="outline" onClick={() => refetch()}>
            Yeniden Dene
          </Button>
        </AlertDescription>
      </Alert>
    );
  }

  if (!history || history.length === 0) {
    return (
      <div className="py-16 text-center">
        <GraduationCap className="mx-auto mb-4 h-12 w-12 text-muted-foreground/40" />
        <h3 className="font-heading text-lg font-semibold text-foreground">Ders geçmişi bulunamadı</h3>
        <p className="mt-1 text-muted-foreground">Henüz alınmış bir ders kaydınız bulunmamaktadır.</p>
      </div>
    );
  }

  // Kompakt tablo class'ları — Proliz tarzı yoğun satır
  const thCls = 'h-8 px-2 py-1 text-[11px] font-semibold uppercase tracking-wide';
  const tdCls = 'px-2 py-1.5 text-[13px]';

  return (
    <div className="flex min-w-0 flex-1 flex-col">
      {/* ── Tek entegre konteyner: toolbar + tablo + pagination + notlar ── */}
      <div className="flex flex-1 flex-col overflow-hidden rounded-md border border-border bg-card shadow-sm">
        {/* Toolbar (üst satır — dönem seçici + yazdır) */}
        <div className="flex flex-col gap-2 border-b border-accent bg-accent/50 px-4 py-2 md:flex-row md:items-center md:justify-between">
          <div className="flex flex-1 items-center gap-2">
            <span className="text-[11px] font-semibold uppercase tracking-wide text-muted-foreground">
              Dönem
            </span>
            <TermPickerCombobox
              terms={history ?? []}
              selectedTermId={selectedTermId}
              onSelect={setSelectedTermId}
            />
          </div>

          <Button
            onClick={handlePrint}
            disabled={isPrinting || selectedTermId === null}
            variant="outline"
            size="sm"
            className="h-8 gap-2"
          >
            <Printer className="h-3.5 w-3.5" />
            {isPrinting ? 'Hazırlanıyor…' : 'Yazdır'}
          </Button>
        </div>

        {/* Tablo — sadece seçili dönem */}
        <div className="overflow-x-auto">
          <Table className="min-w-[1280px]">
            <TableHeader>
              <TableRow className="bg-muted/40 hover:bg-muted/40">
                <TableHead className={cn(thCls, 'w-9 text-center')}>#</TableHead>
                <TableHead className={cn(thCls, 'w-24')}>Ders Kodu</TableHead>
                <TableHead className={cn(thCls, 'w-12 text-center')}>Şb</TableHead>
                <TableHead className={cn(thCls, 'min-w-[220px]')}>Ders Adı</TableHead>
                <TableHead className={cn(thCls, 'w-14 text-center')}>Krd</TableHead>
                <TableHead className={cn(thCls, 'w-20 text-center')}>T+U+L</TableHead>
                <TableHead className={cn(thCls, 'w-14 text-center')}>AKTS</TableHead>
                <TableHead className={cn(thCls, 'w-12 text-center')}>Snf</TableHead>
                <TableHead className={cn(thCls, 'w-12 text-center')} title="Zorunlu / Seçmeli">Z/S</TableHead>
                <TableHead className={cn(thCls, 'min-w-[140px]')}>Program</TableHead>
                <TableHead className={cn(thCls, 'w-20')}>Dil</TableHead>
                <TableHead className={cn(thCls, 'min-w-[180px]')}>Öğretim Elemanı</TableHead>
                <TableHead className={cn(thCls, 'w-12 text-center')}>Not</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {pagedCourses.length === 0 ? (
                <TableRow>
                  <TableCell colSpan={13} className="py-8 text-center text-sm text-muted-foreground">
                    Bu döneme ait ders bulunamadı.
                  </TableCell>
                </TableRow>
              ) : (
                pagedCourses.map((course, rowIdx) => (
                  <TableRow
                    key={course.registrationId}
                    className={cn('hover:bg-accent/20', rowIdx % 2 === 1 && 'bg-muted/10')}
                  >
                    <TableCell className={cn(tdCls, 'text-center')}>
                      <CourseActionMenu
                        onOpenCourseInfo={() => setInfoCourseId(course.courseId)}
                        onOpenCourseDetail={() => setDetailRegistrationId(course.registrationId)}
                      />
                    </TableCell>
                    <TableCell className={cn(tdCls, 'font-mono font-semibold text-primary')}>
                      {course.courseCode}
                    </TableCell>
                    <TableCell className={cn(tdCls, 'text-center')}>{course.sectionNumber}</TableCell>
                    <TableCell className={cn(tdCls, 'font-medium')}>
                      {course.courseName}
                    </TableCell>
                    <TableCell className={cn(tdCls, 'text-center font-mono')}>{course.nationalCredit}</TableCell>
                    <TableCell className={cn(tdCls, 'text-center font-mono')}>
                      {course.theoryHours}+{course.applicationHours ?? 0}+{course.labHours}
                    </TableCell>
                    <TableCell className={cn(tdCls, 'text-center font-mono')}>{course.ects}</TableCell>
                    <TableCell className={cn(tdCls, 'text-center')}>{course.classYear ?? '—'}</TableCell>
                    <TableCell
                      className={cn(tdCls, 'text-center font-mono font-semibold')}
                      title={course.acquisitionType}
                    >
                      {course.isCompulsory ? 'Z' : 'S'}
                    </TableCell>
                    <TableCell className={cn(tdCls, 'text-muted-foreground')}>
                      {course.ownerDepartmentName ?? '—'}
                    </TableCell>
                    <TableCell className={cn(tdCls, 'text-muted-foreground')}>
                      {course.languageDisplay}
                    </TableCell>
                    <TableCell className={cn(tdCls, 'text-muted-foreground')}>
                      {course.instructorName || '—'}
                    </TableCell>
                    <TableCell className={cn(tdCls, 'text-center')}>
                      {course.grade ? (
                        <span
                          className={cn(
                            'inline-flex min-w-6 justify-center rounded px-1 py-0.5 font-mono text-[11px] font-bold',
                            getGradeBadgeClass(course.grade),
                          )}
                        >
                          {course.grade}
                        </span>
                      ) : (
                        <span className="text-xs text-muted-foreground">—</span>
                      )}
                    </TableCell>
                  </TableRow>
                ))
              )}
            </TableBody>
          </Table>
        </div>

        {/* Pagination (Proliz tarzı, paylaşılan TablePagination) */}
        <TablePagination
          totalCount={totalCourses}
          page={safePage}
          pageSize={pageSize}
          onPageChange={setPage}
          onPageSizeChange={setPageSize}
          itemLabel="ders"
          pageSizeOptions={[10, 25, 50]}
        />

        {/* Notlar paneli — kartın EN ALTI (mt-auto ile pagination'dan sonra
            kalan tüm boş alan yukarıda kalır, banner aşağıya yapışır). */}
        <div className="mt-auto px-4 pb-2">
          <NotlarInfoBanner />
        </div>
      </div>

      {/* Sheets — dişli menüsünden açılır */}
      <CourseInfoSheet
        courseId={infoCourseId}
        onOpenChange={(open) => { if (!open) setInfoCourseId(null); }}
      />
      <CourseDetailSheet
        registrationId={detailRegistrationId}
        onOpenChange={(open) => { if (!open) setDetailRegistrationId(null); }}
      />
    </div>
  );
}
