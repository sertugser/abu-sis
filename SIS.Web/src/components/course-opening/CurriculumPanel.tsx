import { useMemo, useRef, useState, type ReactNode } from 'react';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { cn } from '@/lib/utils';
import { Plus, Search, Sparkles } from 'lucide-react';
import type {
  CurriculumCourseForOpeningDto,
  PoolSlotForOpeningDto,
} from '../../types/courseOpening';
import { CurriculumCourseRow } from './CurriculumCourseRow';
import { PoolSlotRow } from './PoolSlotRow';

interface Props {
  courses: CurriculumCourseForOpeningDto[] | undefined;
  poolSlots: PoolSlotForOpeningDto[] | undefined;
  isLoading: boolean;

  // Parent'tan gelen filtreler (CurriculumPanelFilters'tan)
  classYear: number | null;
  // 'summer' seçilirse parite filtresi uygulanmaz (yaz okulunda tüm yarıyıllardan ders alınabilir)
  semester: 'fall' | 'spring' | 'summer' | null;
  codeSearch: string;
  nameSearch: string;

  selectedIds: Set<number>;
  onToggleSelect: (courseId: number) => void;
  onOpenSelected: () => void;
  onOpenCourseDetail: (course: CurriculumCourseForOpeningDto) => void;
  onOpenPoolSlot: (slot: PoolSlotForOpeningDto) => void;

  // Filtre alanı parent'tan render edilir (üst kısma yerleştirilir)
  filtersSlot?: ReactNode;
  onOpenFreeCourseDialog?: () => void;
  freeDialogDisabled?: boolean;
}

const HEADER_GRID =
  'grid grid-cols-[28px_100px_1fr_32px_48px_52px_40px_80px_32px] items-center gap-2 px-3 py-2';

type ListItem =
  | { kind: 'course'; data: CurriculumCourseForOpeningDto; sortSemester: number; sortKey: string }
  | { kind: 'slot'; data: PoolSlotForOpeningDto; sortSemester: number; sortKey: string };

export function CurriculumPanel({
  courses,
  poolSlots,
  isLoading,
  classYear,
  semester,
  codeSearch,
  nameSearch,
  selectedIds,
  onToggleSelect,
  onOpenSelected,
  onOpenCourseDetail,
  onOpenPoolSlot,
  filtersSlot,
  onOpenFreeCourseDialog,
  freeDialogDisabled = false,
}: Props) {
  const scrollRef = useRef<HTMLDivElement>(null);
  const [showTopShadow, setShowTopShadow] = useState(false);
  const [showBottomShadow, setShowBottomShadow] = useState(true);

  // Zorunlu + tekil seçmeli dersler (havuz üyeleri ayrı slot satırı olarak gösterilir)
  const filteredCourses = useMemo(() => {
    if (!courses) return [];
    const code = codeSearch.trim().toLowerCase();
    const name = nameSearch.trim().toLowerCase();
    return courses.filter((c) => {
      // Havuz üyelerini ana listeye dahil etme — slot satırı tıklanınca modal'da gösterilir
      if (c.source === 'Pool') return false;

      // Sınıf filtresi
      if (classYear !== null) {
        const courseYear = c.suggestedSemester > 0 ? Math.ceil(c.suggestedSemester / 2) : null;
        if (courseYear !== classYear) return false;
      }
      // Dönem filtresi (Güz/Bahar parite). Summer → filtre yok.
      if (semester === 'fall' || semester === 'spring') {
        if (c.suggestedSemester > 0) {
          const isFall = c.suggestedSemester % 2 === 1;
          if (semester === 'fall' && !isFall) return false;
          if (semester === 'spring' && isFall) return false;
        }
      }
      // Ders kod / ad araması
      if (code && !c.courseCode.toLowerCase().includes(code)) return false;
      if (name && !c.courseName.toLowerCase().includes(name)) return false;
      return true;
    });
  }, [courses, classYear, semester, codeSearch, nameSearch]);

  // Pool slot'larını da aynı sınıf/dönem/arama filtrelerine sok
  const filteredSlots = useMemo(() => {
    if (!poolSlots) return [];
    const code = codeSearch.trim().toLowerCase();
    const name = nameSearch.trim().toLowerCase();
    return poolSlots.filter((s) => {
      if (classYear !== null) {
        const slotYear = s.suggestedSemesterStart > 0 ? Math.ceil(s.suggestedSemesterStart / 2) : null;
        if (slotYear !== classYear) return false;
      }
      if (semester === 'fall' || semester === 'spring') {
        if (s.suggestedSemesterStart > 0) {
          const isFall = s.suggestedSemesterStart % 2 === 1;
          if (semester === 'fall' && !isFall) return false;
          if (semester === 'spring' && isFall) return false;
        }
      }
      // Slot adı veya grup adı ile arama
      if (code && !s.slotName.toLowerCase().includes(code)) return false;
      if (name && !s.electiveGroupName.toLowerCase().includes(name)) return false;
      return true;
    });
  }, [poolSlots, classYear, semester, codeSearch, nameSearch]);

  // Birleşik ve sıralı liste — yarıyıl + (slotlar sonda) + ders kodu/slot adı
  const items = useMemo<ListItem[]>(() => {
    const arr: ListItem[] = [];
    for (const c of filteredCourses) {
      arr.push({
        kind: 'course',
        data: c,
        sortSemester: c.suggestedSemester || 0,
        sortKey: c.courseCode,
      });
    }
    for (const s of filteredSlots) {
      arr.push({
        kind: 'slot',
        data: s,
        sortSemester: s.suggestedSemesterStart || 0,
        sortKey: `~~${s.slotName}`, // tilde ile aynı yarıyıl içinde slot'lar derslerden sonra
      });
    }
    return arr.sort((a, b) => {
      if (a.sortSemester !== b.sortSemester) return a.sortSemester - b.sortSemester;
      return a.sortKey.localeCompare(b.sortKey);
    });
  }, [filteredCourses, filteredSlots]);

  const handleScroll = () => {
    const el = scrollRef.current;
    if (!el) return;
    setShowTopShadow(el.scrollTop > 4);
    setShowBottomShadow(el.scrollTop + el.clientHeight < el.scrollHeight - 4);
  };

  const selectableCount = selectedIds.size;

  return (
    <div className="flex flex-col min-h-0 h-full overflow-hidden">
      {/* Filtreler — parent'tan render edilen filter bar */}
      {filtersSlot && <div className="shrink-0">{filtersSlot}</div>}

      {/* Tablo başlığı — Proliz tarzı kolon başlıkları */}
      <div
        className={cn(
          HEADER_GRID,
          'shrink-0 bg-slate-100 border-b border-gray-200 text-xs font-semibold text-gray-700 uppercase tracking-wide',
        )}
      >
        <span />
        <span className="text-left">Kod</span>
        <span className="text-left">Ders Adı</span>
        <span className="text-center" title="Zorunlu (Z) / Seçmeli (S)">Z/S</span>
        <span className="text-center" title="Kredi">KRD</span>
        <span className="text-center">AKTS</span>
        <span className="text-center" title="Sınıf">Snf</span>
        <span className="text-center">Grup</span>
        <span />
      </div>

      {/* Scrollable content */}
      <div className="relative flex-1 min-h-0">
        <div
          className={cn(
            'absolute top-0 left-0 right-0 h-3 bg-gradient-to-b from-black/[0.06] to-transparent z-[5] pointer-events-none transition-opacity',
            showTopShadow ? 'opacity-100' : 'opacity-0',
          )}
        />

        <div ref={scrollRef} onScroll={handleScroll} className="h-full overflow-y-auto overscroll-contain">
          {isLoading ? (
            <div className="p-4 space-y-2">
              {Array.from({ length: 8 }).map((_, i) => (
                <Skeleton key={i} className="h-7 rounded" />
              ))}
            </div>
          ) : items.length === 0 ? (
            <div className="flex flex-col items-center py-16 text-gray-400">
              <Search className="h-8 w-8 mb-2" />
              <p className="text-sm">Eşleşen ders bulunamadı.</p>
            </div>
          ) : (
            items.map((item) =>
              item.kind === 'course' ? (
                <CurriculumCourseRow
                  key={`c-${item.data.courseId}`}
                  course={item.data}
                  selected={selectedIds.has(item.data.courseId)}
                  onToggle={onToggleSelect}
                  onOpenDetail={onOpenCourseDetail}
                />
              ) : (
                <PoolSlotRow key={`s-${item.data.poolId}`} slot={item.data} onOpen={onOpenPoolSlot} />
              ),
            )
          )}
        </div>

        <div
          className={cn(
            'absolute bottom-0 left-0 right-0 h-3 bg-gradient-to-t from-black/[0.06] to-transparent z-[5] pointer-events-none transition-opacity',
            showBottomShadow ? 'opacity-100' : 'opacity-0',
          )}
        />
      </div>

      {/* Bottom action bar — Proliz tarzı geniş butonlar (h-14) */}
      <div
        className={cn(
          'shrink-0 h-14 border-t px-3 flex items-center gap-2 transition-colors z-10',
          selectableCount > 0 ? 'bg-blue-50 border-blue-200' : 'bg-white',
        )}
      >
        <Button
          variant="default"
          disabled={selectableCount === 0}
          className="h-9 px-3 gap-1.5 rounded-md"
          onClick={onOpenSelected}
        >
          <Plus className="h-4 w-4" />
          Seçilenleri Aç
          {selectableCount > 0 && (
            <span className="ml-1 rounded bg-white/20 px-1.5 py-0.5 text-[10px] tabular-nums">
              {selectableCount}
            </span>
          )}
        </Button>

        {onOpenFreeCourseDialog && (
          <Button
            variant="outline"
            className="h-9 px-3 gap-1.5 rounded-md ml-auto"
            onClick={onOpenFreeCourseDialog}
            disabled={freeDialogDisabled}
            title={freeDialogDisabled ? 'Aktif dönem gerekli' : 'Müfredatta olmayan bir dersi serbest aç'}
          >
            <Sparkles className="h-4 w-4" />
            Serbest Ders Aç
          </Button>
        )}
      </div>
    </div>
  );
}
