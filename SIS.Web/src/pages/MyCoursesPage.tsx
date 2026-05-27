import { useMemo, useState } from 'react';
import { useCourseHistory } from '../hooks/useCourseHistory';
import { downloadCourseHistoryPdf } from '../api/registration.api';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { AlertCircle, GraduationCap, Printer } from 'lucide-react';
import { cn } from '@/lib/utils';
import { getAcquisitionBadgeClass, getGradeBadgeClass } from '@/lib/gradeUtils';
import { CourseActionMenu } from '@/components/my-courses/CourseActionMenu';
import { CourseInfoSheet } from '@/components/my-courses/CourseInfoSheet';
import { CourseDetailSheet } from '@/components/my-courses/CourseDetailSheet';
import { useToast } from '@/components/ui/global-toast';
import { toApiError } from '@/lib/apiError';

const ALL_TERMS_VALUE = 'all';

export default function MyCoursesPage() {
  const { data: history, isLoading, error } = useCourseHistory();
  const { showError } = useToast();
  const [selectedTermId, setSelectedTermId] = useState<string>(ALL_TERMS_VALUE);
  const [infoCourseId, setInfoCourseId] = useState<number | null>(null);
  const [detailRegistrationId, setDetailRegistrationId] = useState<number | null>(null);
  const [isPrinting, setIsPrinting] = useState(false);

  const filteredTerms = useMemo(() => {
    if (!history) return [];
    if (selectedTermId === ALL_TERMS_VALUE) return history;
    const id = Number(selectedTermId);
    return history.filter(t => t.termId === id);
  }, [history, selectedTermId]);

  const totals = useMemo(() => {
    const courses = filteredTerms.flatMap(t => t.courses);
    return {
      courseCount: courses.length,
      hoursTotal: courses.reduce((s, c) => s + c.theoryHours + c.labHours + (c.applicationHours ?? 0), 0),
      creditTotal: courses.reduce((s, c) => s + c.nationalCredit, 0),
      ectsTotal: courses.reduce((s, c) => s + c.ects, 0),
    };
  }, [filteredTerms]);

  const handlePrint = async () => {
    try {
      setIsPrinting(true);
      const blob = await downloadCourseHistoryPdf();
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
        <AlertDescription>Ders geçmişi yüklenirken hata oluştu.</AlertDescription>
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
    <div className="min-w-0 space-y-6">
      {/* ── Tek entegre konteyner: toolbar + tablo + alt toplam ── */}
      <div className="overflow-hidden rounded-md border border-border bg-card shadow-sm">
        {/* Toolbar (üst satır — filtre + yazdır) */}
        <div className="flex flex-col gap-2 border-b border-accent bg-accent/50 px-4 py-2 md:flex-row md:items-center md:justify-between">
          <div className="flex flex-1 items-center gap-2">
            <span className="text-[11px] font-semibold uppercase tracking-wide text-muted-foreground">
              Dönem
            </span>
            <Select value={selectedTermId} onValueChange={setSelectedTermId}>
              <SelectTrigger className="h-8 w-full max-w-xs bg-card text-sm">
                <SelectValue placeholder="Dönem seçin" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value={ALL_TERMS_VALUE}>Tüm Dönemler</SelectItem>
                {history.map(t => (
                  <SelectItem key={t.termId} value={String(t.termId)}>
                    {t.termName}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <Button
            onClick={handlePrint}
            disabled={isPrinting}
            variant="outline"
            size="sm"
            className="h-8 gap-2"
          >
            <Printer className="h-3.5 w-3.5" />
            {isPrinting ? 'Hazırlanıyor…' : 'Yazdır'}
          </Button>
        </div>

        {/* Dönem blokları (peş peşe, aynı container içinde, ayırıcı border'la) */}
        {filteredTerms.map((term, idx) => (
          <div key={term.termId} className={cn(idx > 0 && 'border-t border-border')}>
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
                    <TableHead className={cn(thCls, 'w-28 text-center')}>Alış Şekli</TableHead>
                    <TableHead className={cn(thCls, 'min-w-[140px]')}>Program</TableHead>
                    <TableHead className={cn(thCls, 'w-20')}>Dil</TableHead>
                    <TableHead className={cn(thCls, 'min-w-[180px]')}>Öğretim Elemanı</TableHead>
                    <TableHead className={cn(thCls, 'w-12 text-center')}>Not</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {term.courses.map((course, rowIdx) => (
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
                        {course.isRepeated && (
                          <span className="ml-2 text-[10px] font-semibold text-warning-foreground">(Tekrar)</span>
                        )}
                      </TableCell>
                      <TableCell className={cn(tdCls, 'text-center font-mono')}>{course.nationalCredit}</TableCell>
                      <TableCell className={cn(tdCls, 'text-center font-mono')}>
                        {course.theoryHours}+{course.applicationHours ?? 0}+{course.labHours}
                      </TableCell>
                      <TableCell className={cn(tdCls, 'text-center font-mono')}>{course.ects}</TableCell>
                      <TableCell className={cn(tdCls, 'text-center')}>{course.classYear ?? '—'}</TableCell>
                      <TableCell className={cn(tdCls, 'text-center')}>
                        <span
                          className={cn(
                            'inline-flex rounded px-1.5 py-0.5 text-[10px] font-semibold',
                            getAcquisitionBadgeClass(course.acquisitionType),
                          )}
                        >
                          {course.acquisitionType}
                        </span>
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
                  ))}
                </TableBody>
              </Table>
            </div>
          </div>
        ))}

        {/* Alt toplam — aynı container'ın footer'ı */}
        <div className="flex flex-wrap items-center gap-x-5 gap-y-1 border-t border-border bg-muted/30 px-4 py-2 text-[13px] font-semibold text-foreground">
          <span>
            Toplam Ders Sayısı: <span className="ml-1 font-mono text-primary">{totals.courseCount}</span>
          </span>
          <span className="text-border">•</span>
          <span>
            Ders Saati: <span className="ml-1 font-mono text-primary">{totals.hoursTotal}</span>
          </span>
          <span className="text-border">•</span>
          <span>
            Kredi: <span className="ml-1 font-mono text-primary">{totals.creditTotal}</span>
          </span>
          <span className="text-border">•</span>
          <span>
            AKTS: <span className="ml-1 font-mono text-primary">{totals.ectsTotal}</span>
          </span>
        </div>
      </div>

      {/* Legend — kompakt 2 kolon, sade */}
      <div className="grid gap-x-8 gap-y-3 border-t border-dashed border-border px-1 pt-4 pb-2 text-[12px] leading-snug md:grid-cols-2">
        <div>
          <p className="font-semibold text-destructive">
            Not sütununda "—" görünüyorsa ders henüz sonuçlandırılmamış olabilir:
          </p>
          <ol className="list-decimal pl-5 text-muted-foreground">
            <li>Yarıyıl sonu sınavı notu henüz girilmedi</li>
            <li>Tüm sınav notları girildi, harf notu hesaplanmadı</li>
            <li>Not girişi tamamlanmayan bütünleme sınavı var</li>
            <li>Bütünleme notları girildi, harf notu hesaplanmadı</li>
          </ol>
        </div>
        <div>
          <p className="font-semibold text-primary">Harf notu hesaplandığı halde "—" görünüyorsa:</p>
          <p className="text-muted-foreground">
            Akademik takvime göre harf notlarının ilan tarihi bekleniyor.
            İlgili tarihleri akademik takvimden kontrol edebilirsiniz.
          </p>
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
