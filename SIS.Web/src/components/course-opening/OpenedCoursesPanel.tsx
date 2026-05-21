import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { TablePagination } from '@/components/ui/table-pagination';
import { cn } from '@/lib/utils';
import { Download, ExternalLink, HelpCircle, Layers, Loader2, Search, Trash2 } from 'lucide-react';
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/tooltip';
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog';
import type { OpenedCourseSectionDto, InstructorOptionDto } from '../../types/courseOpening';
import type { CourseDemandForecastDto } from '../../types/forecast';
import type { InstructorWorkloadDto } from '../../types/courseOpening';
import type { ExternalCourseDto } from '../../api/courseOpening.api';
import { OpenedCourseRow } from './OpenedCourseRow';
import { CourseManagementDialog } from './CourseManagementDialog';

type SortField = 'code' | 'sections' | 'capacity' | 'enrolled' | 'demand';
type SortDir = 'asc' | 'desc';

interface Props {
  sections: OpenedCourseSectionDto[] | undefined;
  isLoading: boolean;
  termId: number;
  forecastMap: Map<number, CourseDemandForecastDto>;
  instructors: InstructorOptionDto[];

  // Parent'tan gelen filtreler
  classYear: number | null;
  codeSearch: string;
  nameSearch: string;

  onUpdateSection: (sectionId: number, capacity: number, instructorId: number | undefined, internalQuota?: number | null, externalQuota?: number | null) => void;
  onCloseSection: (sectionId: number) => void;
  onCloseAllSections: (courseId: number) => Promise<unknown>;
  /** Hard-delete: dersin tüm şubelerini DB'den fiziksel sil (geri alınamaz). */
  onDeleteAllSectionsHard: (courseId: number) => Promise<unknown>;
  onReactivateSection: (sectionId: number) => Promise<unknown>;
  onAddSection: (courseId: number, capacity: number, instructorId: number | undefined, internalQuota?: number, externalQuota?: number) => void;
  updatePending: boolean;
  closePending: boolean;
  closeError: Error | null;
  workloadData: InstructorWorkloadDto | undefined;
  onInstructorSelect: (id: number | null) => void;
  onExportCsv: () => void;

  // Filter slot — parent'tan render edilen filter bar
  filtersSlot?: ReactNode;
  // Dış dersler — drawer açma
  externalCourses?: ExternalCourseDto[];
  onOpenExternalDrawer?: () => void;
  // Geçmiş dönem read-only
  readOnly?: boolean;
}

interface CourseGroup {
  courseId: number;
  courseCode: string;
  courseName: string;
  sections: OpenedCourseSectionDto[];
}

export function OpenedCoursesPanel({
  sections,
  isLoading,
  termId,
  forecastMap,
  instructors,
  classYear,
  codeSearch,
  nameSearch,
  onUpdateSection,
  onCloseSection,
  onCloseAllSections: _onCloseAllSections,
  onDeleteAllSectionsHard,
  onReactivateSection,
  onAddSection,
  updatePending,
  closePending,
  closeError,
  workloadData,
  onInstructorSelect,
  onExportCsv,
  filtersSlot,
  externalCourses,
  onOpenExternalDrawer,
  readOnly = false,
}: Props) {
  const [sortField, _setSortField] = useState<SortField>('code');
  const [sortDir, _setSortDir] = useState<SortDir>('asc');
  // Multi-select — birden fazla ders seçilebilir, toplu silme için
  const [selectedCourseIds, setSelectedCourseIds] = useState<Set<number>>(new Set());
  const [managementDialogOpen, setManagementDialogOpen] = useState(false);
  const [managementCourseId, setManagementCourseId] = useState<number | null>(null);
  // Toplu silme onay dialog'u + işlem state'i
  const [bulkDeleteOpen, setBulkDeleteOpen] = useState(false);
  const [isBulkDeleting, setIsBulkDeleting] = useState(false);
  const [bulkDeleteError, setBulkDeleteError] = useState<string | null>(null);
  const scrollRef = useRef<HTMLDivElement>(null);
  const [showTopShadow, setShowTopShadow] = useState(false);
  const [showBottomShadow, setShowBottomShadow] = useState(true);

  const toggleCourseSelection = useCallback((courseId: number) => {
    setSelectedCourseIds((prev) => {
      const next = new Set(prev);
      if (next.has(courseId)) next.delete(courseId);
      else next.add(courseId);
      return next;
    });
  }, []);

  const courseGroups: CourseGroup[] = useMemo(() => {
    if (!sections) return [];
    const map = new Map<number, CourseGroup>();
    for (const sec of sections) {
      if (!map.has(sec.courseId)) {
        map.set(sec.courseId, {
          courseId: sec.courseId,
          courseCode: sec.courseCode,
          courseName: sec.courseName,
          sections: [],
        });
      }
      map.get(sec.courseId)!.sections.push(sec);
    }
    const groups = [...map.values()];
    groups.sort((a, b) => a.courseCode.localeCompare(b.courseCode));
    return groups;
  }, [sections]);

  const filtered = useMemo(() => {
    let result = courseGroups;
    const code = codeSearch.trim().toLowerCase();
    const name = nameSearch.trim().toLowerCase();
    if (code) result = result.filter((g) => g.courseCode.toLowerCase().includes(code));
    if (name) result = result.filter((g) => g.courseName.toLowerCase().includes(name));
    // Sınıf filtresi: ders kodundaki ilk rakamdan sınıf çıkar (BLG101 → 1, BLG201 → 2 vb.)
    // Mükemmel değil ama Proliz konvansiyonuna yakın
    if (classYear !== null) {
      result = result.filter((g) => {
        const match = g.courseCode.match(/(\d)\d{2}/);
        if (!match) return true;
        return Number(match[1]) === classYear;
      });
    }
    const dir = sortDir === 'asc' ? 1 : -1;
    result = [...result].sort((a, b) => {
      switch (sortField) {
        case 'code': return a.courseCode.localeCompare(b.courseCode) * dir;
        case 'sections': return (a.sections.length - b.sections.length) * dir;
        case 'capacity': return (a.sections.reduce((s, x) => s + x.capacity, 0) - b.sections.reduce((s, x) => s + x.capacity, 0)) * dir;
        case 'enrolled': return (a.sections.reduce((s, x) => s + x.enrolledCount, 0) - b.sections.reduce((s, x) => s + x.enrolledCount, 0)) * dir;
        case 'demand': {
          const aDemand = forecastMap.get(a.courseId)?.totalPredictedDemand ?? 0;
          const bDemand = forecastMap.get(b.courseId)?.totalPredictedDemand ?? 0;
          return (aDemand - bDemand) * dir;
        }
        default: return 0;
      }
    });
    return result;
  }, [courseGroups, codeSearch, nameSearch, classYear, sortField, sortDir, forecastMap]);

  const totalSections = sections?.length ?? 0;
  const totalCapacity = (sections ?? []).reduce((s, sec) => s + sec.capacity, 0);
  const totalEnrolled = (sections ?? []).reduce((s, sec) => s + sec.enrolledCount, 0);

  // ── Sayfalama ───────────────────────────────────────────────────────────
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(25);
  const totalPages = Math.max(1, Math.ceil(filtered.length / pageSize));
  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    if (page > totalPages) setPage(totalPages);
  }, [page, totalPages]);
  const safePage = Math.min(page, totalPages);
  const pageItems = useMemo(
    () => filtered.slice((safePage - 1) * pageSize, safePage * pageSize),
    [filtered, safePage, pageSize],
  );

  // Seçili dersler — toplu silme onay ekranı için kullanılır
  const selectedCourses = useMemo(
    () => courseGroups.filter((g) => selectedCourseIds.has(g.courseId)),
    [courseGroups, selectedCourseIds],
  );

  // Silinemeyecek dersler — kayıtlı öğrencisi olanlar (frontend ön-uyarı)
  const blockedSelectedCourses = useMemo(
    () =>
      selectedCourses.filter((g) =>
        g.sections.some((s) => s.enrolledCount > 0 || s.registeredCount > 0),
      ),
    [selectedCourses],
  );

  const handleBulkDelete = useCallback(async () => {
    if (selectedCourseIds.size === 0) return;
    setIsBulkDeleting(true);
    setBulkDeleteError(null);
    const errors: string[] = [];
    for (const courseId of selectedCourseIds) {
      try {
        // Hard-delete: dersin tüm şubeleri DB'den fiziksel silinir (geri alınamaz)
        await onDeleteAllSectionsHard(courseId);
      } catch (e) {
        const err = e as { response?: { data?: { error?: string } }; message?: string };
        const code = courseGroups.find((g) => g.courseId === courseId)?.courseCode ?? `#${courseId}`;
        errors.push(`${code}: ${err?.response?.data?.error ?? err?.message ?? 'Bilinmeyen hata'}`);
      }
    }
    setIsBulkDeleting(false);
    if (errors.length > 0) {
      setBulkDeleteError(errors.join('\n'));
    } else {
      setBulkDeleteOpen(false);
      setSelectedCourseIds(new Set());
    }
  }, [selectedCourseIds, onDeleteAllSectionsHard, courseGroups]);

  const handleScroll = () => {
    const el = scrollRef.current;
    if (!el) return;
    setShowTopShadow(el.scrollTop > 4);
    setShowBottomShadow(el.scrollTop + el.clientHeight < el.scrollHeight - 4);
  };

  return (
    <div className="flex flex-col min-h-0 h-full overflow-hidden">
      {/* Üst başlık barı — Hareket tarzı: title + ? tooltip + toolbar + Dış Dersler */}
      <div className="border-b bg-muted/30 shrink-0">
        <div className="px-3 py-1.5 flex items-center gap-2 border-b border-border/40">
          <h3 className="text-sm font-semibold">Açılan Dersler</h3>
          <TooltipProvider delayDuration={200}>
            <Tooltip>
              <TooltipTrigger asChild>
                <button
                  type="button"
                  className="text-muted-foreground hover:text-foreground transition-colors"
                  aria-label="Bu panel hakkında bilgi"
                >
                  <HelpCircle className="h-3.5 w-3.5" />
                </button>
              </TooltipTrigger>
              <TooltipContent side="bottom" align="start" className="max-w-sm text-xs leading-relaxed">
                <p className="font-semibold mb-1.5">Bu dönemde halihazırda açılmış olan dersler ve şubeleri burada görürsün.</p>
                <p className="mb-1.5">Bir derse <strong>çift tıklayarak</strong> şu işlemleri yapabilirsin:</p>
                <ul className="list-disc pl-4 space-y-0.5">
                  <li><strong>Şube ekle / kapat</strong></li>
                  <li><strong>Kontenjan ve hoca</strong> düzenle</li>
                  <li><strong>Erişim politikası</strong> yönet</li>
                </ul>
                <p className="mt-2 text-[11px] text-muted-foreground">
                  İpucu: Üstteki filtrelerle bölüm, sınıf veya ders kodu / adına göre listeyi daraltabilirsin.
                </p>
              </TooltipContent>
            </Tooltip>
          </TooltipProvider>
          <div className="ml-auto flex items-center gap-1">
            {externalCourses && externalCourses.length > 0 && onOpenExternalDrawer && (
              <Button
                type="button"
                variant="ghost"
                size="sm"
                className="h-7 px-2 gap-1 text-xs text-violet-700 hover:bg-violet-50"
                onClick={onOpenExternalDrawer}
                title="Bölümünüzün müfredatında olan ama başka bölüme ait dersler"
              >
                <ExternalLink className="h-3.5 w-3.5" />
                Dış Dersler
                <span className="ml-0.5 rounded bg-violet-100 px-1.5 py-0.5 text-[10px] tabular-nums">
                  {externalCourses.length}
                </span>
              </Button>
            )}
            <Button
              type="button"
              variant="ghost"
              size="sm"
              className="h-7 w-7 p-0"
              onClick={onExportCsv}
              title="CSV / Excel olarak indir"
            >
              <Download className="h-3.5 w-3.5" />
            </Button>
          </div>
        </div>

        {/* Filtreler — parent'tan render edilen filter bar */}
        {filtersSlot}
      </div>

      {/* Tablo başlığı — sol panelle uyumlu (Z/S, Krd, AKTS sütunları) */}
      <div className="shrink-0 grid grid-cols-[28px_100px_1fr_32px_48px_52px_72px] items-center gap-2 px-2 py-1.5 bg-muted/50 border-b border-gray-200 text-xs font-semibold text-gray-700">
        <span className="text-center" /> {/* checkbox sütunu */}
        <span className="text-left">Kod</span>
        <span className="text-left">Ders Adı</span>
        <span className="text-center" title="Zorunlu (Z) / Seçmeli (S)">Z/S</span>
        <span className="text-center" title="Kredi">Krd</span>
        <span className="text-center">AKTS</span>
        <span className="text-center">Kontenjan</span>
      </div>

      {/* Scrollable content */}
      <div className="relative flex-1 min-h-0">
        {/* Top scroll shadow */}
        <div className={cn(
          'absolute top-0 left-0 right-0 h-3 bg-gradient-to-b from-black/[0.06] to-transparent z-[5] pointer-events-none transition-opacity',
          showTopShadow ? 'opacity-100' : 'opacity-0',
        )} />

        <div
          ref={scrollRef}
          onScroll={handleScroll}
          className="h-full overflow-y-auto overscroll-contain"
        >
          {isLoading ? (
            <div className="p-4 space-y-3">
              {Array.from({ length: 6 }).map((_, i) => <Skeleton key={i} className="h-12 rounded-lg" />)}
            </div>
          ) : filtered.length === 0 ? (
            <div className="h-full flex flex-col items-center justify-center py-12 text-gray-400">
              {courseGroups.length === 0 ? (
                <>
                  <Layers className="h-9 w-9 mb-2 text-muted-foreground/30" />
                  <p className="text-sm text-muted-foreground">Henüz ders açılmamış.</p>
                  <p className="text-[11px] text-muted-foreground/70 mt-1">Sol panelden müfredat derslerini seçip açabilirsiniz.</p>
                </>
              ) : (
                <>
                  <Search className="h-9 w-9 mb-2 text-muted-foreground/30" />
                  <p className="text-sm text-muted-foreground">Eşleşen açılan ders bulunamadı.</p>
                </>
              )}
            </div>
          ) : (
            pageItems.map((group) => (
              <OpenedCourseRow
                key={group.courseId}
                courseCode={group.courseCode}
                courseName={group.courseName}
                courseId={group.courseId}
                sections={group.sections}
                selected={selectedCourseIds.has(group.courseId)}
                onSelect={toggleCourseSelection}
                onOpenManagement={(cid) => {
                  setManagementCourseId(cid);
                  setManagementDialogOpen(true);
                }}
              />
            ))
          )}
        </div>

        {/* Bottom scroll shadow */}
        <div className={cn(
          'absolute bottom-0 left-0 right-0 h-3 bg-gradient-to-t from-black/[0.06] to-transparent z-[5] pointer-events-none transition-opacity',
          showBottomShadow ? 'opacity-100' : 'opacity-0',
        )} />
      </div>

      {/* Sayfalama + toplam istatistik / silme aksiyonu — Hareket sayfasıyla aynı tek altbar */}
      {filtered.length > 0 && (
        <div className="shrink-0 mt-auto">
          <TablePagination
            totalCount={filtered.length}
            page={safePage}
            pageSize={pageSize}
            onPageChange={setPage}
            onPageSizeChange={(s) => {
              setPageSize(s);
              setPage(1);
            }}
            itemLabel="ders"
            extraInfo={
              selectedCourseIds.size > 0 ? (
                <span className="flex items-center gap-2">
                  ·{' '}
                  <strong className="text-foreground">{selectedCourseIds.size}</strong> ders seçili
                  <Button
                    variant="destructive"
                    size="sm"
                    className="h-6 text-[11px] px-2 gap-1"
                    onClick={() => setBulkDeleteOpen(true)}
                    title="Seçili dersleri sil (kayıtlı öğrenci olmaması şartıyla)"
                    disabled={readOnly}
                  >
                    <Trash2 className="h-3 w-3" />
                    Seçilenleri Sil
                  </Button>
                </span>
              ) : (
                <>
                  · <strong className="text-foreground">{totalSections}</strong> şube ·{' '}
                  <strong className="text-foreground">{totalCapacity}</strong> kontenjan ·{' '}
                  <strong
                    className={cn('tabular-nums', totalEnrolled > 0 ? 'text-foreground' : 'text-muted-foreground/70')}
                  >
                    {totalEnrolled}
                  </strong>{' '}
                  kayıtlı
                </>
              )
            }
          />
        </div>
      )}

      {/* Toplu silme onay dialog'u — hard delete (geri alınamaz) */}
      <AlertDialog open={bulkDeleteOpen} onOpenChange={(o) => !o && !isBulkDeleting && setBulkDeleteOpen(false)}>
        <AlertDialogContent className="max-w-lg">
          <AlertDialogHeader>
            <AlertDialogTitle>
              Bu dönemden {selectedCourses.length} dersin açılışını sil?
            </AlertDialogTitle>
            <AlertDialogDescription asChild>
              <div className="space-y-2 text-sm">
                <p>
                  Aşağıdaki derslerin <strong>sadece bu dönemde açılmış şubeleri</strong> DB'den fiziksel olarak silinecek
                  — bu dönemde sanki hiç açılmamış gibi olur.{' '}
                  <span className="text-destructive font-medium">Bu işlem geri alınamaz.</span>
                </p>
                <ul className="text-[11px] text-muted-foreground list-disc pl-4 space-y-0.5">
                  <li><strong>Silinmez:</strong> ders kataloğu (BIO101 vs.), müfredat ataması</li>
                  <li><strong>Silinmez:</strong> diğer dönemlerdeki şubeler (geçmiş/gelecek dönemler etkilenmez)</li>
                  <li><strong>Silinir:</strong> bu dönemin şubeleri + program tablosu + erişim politikası</li>
                </ul>
                <p className="text-xs text-muted-foreground border-t pt-2 mt-2">
                  Aktif/tamamlanmış kaydı, öğrenci değerlendirmesi veya yoklama kaydı olan dersler reddedilir.
                </p>
                <ul className="text-xs space-y-0.5 max-h-40 overflow-y-auto bg-muted/30 rounded p-2">
                  {selectedCourses.map((g) => (
                    <li key={g.courseId} className="flex items-center gap-2">
                      <span className="font-mono font-medium">{g.courseCode}</span>
                      <span className="text-muted-foreground truncate flex-1">{g.courseName}</span>
                      <span className="text-[10px] text-muted-foreground shrink-0">
                        {g.sections.length} şube
                      </span>
                    </li>
                  ))}
                </ul>
                {blockedSelectedCourses.length > 0 && (
                  <p className="text-destructive text-xs p-2 bg-destructive/10 rounded">
                    ⚠ <strong>{blockedSelectedCourses.length} ders</strong> kayıtlı öğrenci içerdiği için silinemez:{' '}
                    {blockedSelectedCourses.map((g) => g.courseCode).join(', ')}
                  </p>
                )}
                {bulkDeleteError && (
                  <pre className="text-[11px] text-destructive p-2 bg-destructive/10 rounded whitespace-pre-wrap">
                    {bulkDeleteError}
                  </pre>
                )}
              </div>
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel disabled={isBulkDeleting}>İptal</AlertDialogCancel>
            <AlertDialogAction
              onClick={(e) => {
                e.preventDefault();
                handleBulkDelete();
              }}
              className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
              disabled={isBulkDeleting}
            >
              {isBulkDeleting ? (
                <>
                  <Loader2 className="h-3.5 w-3.5 mr-1 animate-spin" />
                  Siliniyor...
                </>
              ) : (
                <>
                  <Trash2 className="h-3.5 w-3.5 mr-1" />
                  Bu dönemden {selectedCourses.length} dersi sil
                </>
              )}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      {/* Course Management Dialog — çift tıkla açılır, ders+şube yönetimi */}
      <CourseManagementDialog
        open={managementDialogOpen}
        onOpenChange={(o) => {
          setManagementDialogOpen(o);
          if (!o) setManagementCourseId(null);
        }}
        course={(() => {
          if (managementCourseId == null) return null;
          const group = courseGroups.find((g) => g.courseId === managementCourseId);
          if (!group) return null;
          const first = group.sections[0];
          return {
            courseId: managementCourseId,
            courseCode: group.courseCode,
            courseName: group.courseName,
            theoryHours: first?.theoryHours,
            labHours: first?.labHours,
            credits: first?.credits,
            ects: first?.ects,
            suggestedSemester: first?.suggestedSemester,
            departmentName: first?.departmentName ?? null,
          };
        })()}
        sections={
          managementCourseId != null
            ? courseGroups.find((g) => g.courseId === managementCourseId)?.sections ?? []
            : []
        }
        instructors={instructors}
        termId={termId}
        onAddSection={onAddSection}
        onUpdateSection={onUpdateSection}
        onCloseSection={onCloseSection}
        onReactivateSection={onReactivateSection}
        updatePending={updatePending}
        closePending={closePending}
        closeError={closeError}
        workloadData={workloadData}
        onInstructorSelect={onInstructorSelect}
        readOnly={readOnly}
      />
    </div>
  );
}
