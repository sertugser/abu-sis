import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import {
  DndContext, KeyboardSensor, MeasuringStrategy, PointerSensor,
  pointerWithin, rectIntersection,
  useSensor, useSensors,
  type CollisionDetection,
  type DragEndEvent, type DragStartEvent,
} from '@dnd-kit/core';
import {
  AlertCircle, AlertTriangle, ArrowLeft, CheckCircle2, CheckSquare,
  Loader2, Maximize2, Minimize2, Search, Trash2, X,
} from 'lucide-react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { DelayedConfirmDialog } from '@/components/ui/delayed-confirm-dialog';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/tooltip';
import { useActiveTerm } from '../hooks/useActiveTerm';
import { useFacultyLookup } from '../hooks/useAdminLookup';
import {
  useAssignSection, useFacultyPlanningConflicts, useFacultyPlanningSections,
  useFacultyPlanningTimeSlots, useUnassignSection,
} from '../hooks/useFacultyPlanning';
import { getAvailableClassrooms, validateCellsBulk } from '../api/facultyPlanning.api';
import { useFullscreen } from '../hooks/useFullscreen';
import { CalendarGrid } from '../components/faculty-planning/CalendarGrid';
import { UnassignedSectionsPanel, UNASSIGNED_POOL_DROP_ID } from '../components/faculty-planning/UnassignedSectionsPanel';
import type {
  AssignSectionRequest,
  FacultyPlanningSection, FacultyPlanningTimeSlot, ScheduleBlockType,
} from '../types/facultyPlanning';
import type { ScheduleSlot, SchedulingConflict } from '../types/scheduling';

interface DragData {
  /** 'block' = grid'deki atanmış kart taşınıyor; 'pool' = atanmamış havuzdan yeni atama. */
  kind: 'block' | 'pool';
  sectionId: number;
  classroomId: number | null;
  durationMinutes: number;
  // Yalnız kind='block' için doluk
  blockType?: ScheduleBlockType;
  currentDay?: number;
  currentStartTime?: string;
  currentEndTime?: string;
}

/* Frontend pre-check (computeInvalidDropIds) kaldırıldı — Option A.
 * Tüm doğrulama backend RuleEngine'e bırakıldı (28 kural).
 * Frontend sadece backend bulk-validate sonuçlarını renklendirme için kullanır.
 * Drop her zaman backend'e gider; conflict varsa override dialog gösterilir.
 */

/**
 * Smart suggestion — invalidDropIds dışındaki valid hücrelerden "en uygun 3"ünü öner.
 * Skorlama (heuristic):
 *  - +30: section'ın diğer block'larıyla aynı gün (uzaklıkta gruplama bonus)
 *  - +20: öğle saati (10:00–14:00 arası başlayan slot)
 *  - +10: hücre az kalabalık (cellBlocks <= 1)
 * Top 3 dropId döner.
 */
function computeSmartSuggestions(
  draggedSection: FacultyPlanningSection,
  invalid: Set<string>,
  timeSlots: FacultyPlanningTimeSlot[],
  allSections: FacultyPlanningSection[],
): Set<string> {
  // Hücre yoğunluk lookup — bir block kaç slot'a yayılıyorsa hepsi dolu sayılır (range overlap).
  // ESKİ HATA: sadece block'un startTime'ı match olan slot dolu sayılıyordu; 3-slot dersin 2 devam
  // bloğu boş gözüküp smart suggestion'a kendi yerini öneriyordu.
  const cellLoadMap = new Map<string, number>();
  for (const s of allSections) {
    for (const b of s.schedules) {
      const blockStart = timeToMinutes(b.startTime.slice(0, 5));
      const blockEnd = timeToMinutes(b.endTime.slice(0, 5));
      for (const ts of timeSlots) {
        if (ts.day !== b.day) continue;
        const slotStart = timeToMinutes(ts.startTime.slice(0, 5));
        const slotEnd = timeToMinutes(ts.endTime.slice(0, 5));
        if (slotStart < blockEnd && slotEnd > blockStart) {
          const key = `cell-${ts.day}-${ts.slotOrder}`;
          cellLoadMap.set(key, (cellLoadMap.get(key) ?? 0) + 1);
        }
      }
    }
  }

  const sameDays = new Set(draggedSection.schedules.map(b => b.day));
  const scored: { dropId: string; score: number }[] = [];
  for (const ts of timeSlots) {
    const dropId = `cell-${ts.day}-${ts.slotOrder}`;
    if (invalid.has(dropId)) continue;
    // Yalnız boş hücreler önerilir (smart suggestion drop hedefleri)
    if ((cellLoadMap.get(dropId) ?? 0) > 0) continue;
    const startHour = Number(ts.startTime.slice(0, 2));
    let score = 0;
    if (sameDays.has(ts.day)) score += 30;
    if (startHour >= 10 && startHour <= 14) score += 20;
    // boş hücre zaten — load 0 olduğu için ek bonus mantıksız, ama yine de:
    score += 10;
    scored.push({ dropId, score });
  }
  scored.sort((a, b) => b.score - a.score);
  return new Set(scored.slice(0, 3).map(c => c.dropId));
}

interface DragPreviewState {
  section: FacultyPlanningSection;
  data: DragData;
}

interface FeedbackState {
  kind: 'success' | 'error';
  message: string;
}

interface BulkCandidate {
  cellKey: string;
  scheduleSlots: ScheduleSlot[];
}

/**
 * Drag-start akışında backend bulk-validate'e gönderilecek aday hücre listesini hazırlar.
 *
 * Her görünür slot için section'ın o hücreye gitmesi durumundaki TAM schedule slot listesini üretir:
 *   - kind='pool'   → sadece aday block (section atanmamıştı, başka block yok)
 *   - kind='block'  → section'ın sürüklenen block dışındaki diğer blokları + aday block
 *
 * Dolu hücreler (başka section o slotta) atlanır — backend bulk validate'te sırasız hızlandırma.
 * Gün sonuna sığmayan veya ardışıklığı bozan adaylar da atlanır — backend yine kıyaslar ama
 * önden eler, bant genişliği israfı olmasın.
 */
function buildBulkCandidates(
  draggedData: DragData,
  draggedSection: FacultyPlanningSection,
  timeSlots: FacultyPlanningTimeSlot[],
): BulkCandidate[] {
  const candidates: BulkCandidate[] = [];
  const durationMin = draggedData.durationMinutes;
  const targetClassroomId = draggedData.classroomId ?? draggedSection.classroomId ?? undefined;

  // Section'ın korunacak blokları:
  //  - block (taşıma): sürüklenen block hariç tüm mevcut bloklar
  //  - pool (yeni atama, kısmi destek): section'ın mevcut tüm bloklar korunur (Teori bloğu daha önce
  //    atanmışsa Lab bloğu eklerken Teori bloğu yerinde kalmalı; aksi halde tek block ezerek section'ı
  //    yeniden başlatır)
  const otherBlocks = draggedData.kind === 'block'
    ? draggedSection.schedules.filter(b => !(
        b.day === draggedData.currentDay
        && b.startTime.slice(0, 5) === draggedData.currentStartTime?.slice(0, 5)
        && b.blockType === draggedData.blockType
      ))
    : draggedSection.schedules;

  const otherBlockSlots: ScheduleSlot[] = otherBlocks.map(b => ({
    day: b.day,
    startTime: b.startTime.slice(0, 5),
    endTime: b.endTime.slice(0, 5),
    classroomId: b.classroomId ?? targetClassroomId,
  }));

  // Section'ın KENDİ korunan blokları — kendi diğer bloğunun zamanına gidemez.
  // Başka section'ların dolu hücreleri artık ATLANMAZ: backend H1 (derslik) ve H2 (hoca)
  // kurallarıyla kontrol eder. Farklı derslik/hoca ile aynı saatte ders olabilir.
  const selfBlockCells = new Set<string>();
  for (const b of otherBlocks) {
    const blockStart = timeToMinutes(b.startTime.slice(0, 5));
    const blockEnd = timeToMinutes(b.endTime.slice(0, 5));
    for (const ts of timeSlots) {
      if (ts.day !== b.day) continue;
      const slotStart = timeToMinutes(ts.startTime.slice(0, 5));
      const slotEnd = timeToMinutes(ts.endTime.slice(0, 5));
      if (slotStart < blockEnd && slotEnd > blockStart) {
        selfBlockCells.add(`cell-${ts.day}-${ts.slotOrder}`);
      }
    }
  }

  // Gün başına slot'ları sırala — ardışıklık kontrolü için
  const slotsByDay = new Map<number, FacultyPlanningTimeSlot[]>();
  for (const ts of timeSlots) {
    const list = slotsByDay.get(ts.day) ?? [];
    list.push(ts);
    slotsByDay.set(ts.day, list);
  }
  for (const list of slotsByDay.values()) {
    list.sort((a, b) => a.slotOrder - b.slotOrder);
  }

  for (const [day, daySlots] of slotsByDay) {
    for (let i = 0; i < daySlots.length; i += 1) {
      const startSlot = daySlots[i];
      const cellKey = `cell-${day}-${startSlot.slotOrder}`;

      // Section'ın kendi diğer bloğu bu hücredeyse atla (kendi kendine çakışma)
      if (selfBlockCells.has(cellKey)) continue;

      const startHm = startSlot.startTime.slice(0, 5);
      const startMin = timeToMinutes(startHm);
      const slotSizeMin = timeToMinutes(startSlot.endTime.slice(0, 5)) - startMin;
      const needed = slotSizeMin > 0 ? Math.max(1, Math.round(durationMin / slotSizeMin)) : 1;

      // Gün sonu — sığmıyor
      if (i + needed > daySlots.length) continue;

      // Ardışıklık (öğle arası slot atlaması)
      let consecutive = true;
      for (let k = 1; k < needed; k += 1) {
        if (daySlots[i + k].slotOrder !== daySlots[i + k - 1].slotOrder + 1) {
          consecutive = false;
          break;
        }
      }
      if (!consecutive) continue;

      const endHm = daySlots[i + needed - 1].endTime.slice(0, 5);

      candidates.push({
        cellKey,
        scheduleSlots: [
          ...otherBlockSlots,
          {
            day,
            startTime: startHm,
            endTime: endHm,
            classroomId: targetClassroomId,
          },
        ],
      });
    }
  }

  return candidates;
}

function timeToMinutes(t: string): number {
  const [h, m] = t.split(':').map(Number);
  return h * 60 + m;
}

function extractError(error: unknown, fallback: string): string {
  const data = (error as { response?: { data?: { error?: string; message?: string; title?: string } } })?.response?.data;
  return data?.error ?? data?.message ?? data?.title ?? fallback;
}

/**
 * Ders Programı Düzenleyici — drag-drop + zenginleştirilmiş filtre sayfası.
 * AppLayout dışında render edilir; sidebar/topbar görünmez, viewport'u tamamen kaplar.
 *
 * Faz 2 (bu sürüm): drag-drop ile atanmış kartı boş hücreye taşıma.
 * Drag handle: kartın sağ üstündeki GripVertical ikonu (8px hareket gerekir).
 * Drop: boş hücre üzerinde yeşil/kırmızı vurgu, bırakınca backend `assign` çağrısı.
 * Pinli (`isPinned`) kartlar sürüklenemez.
 *
 * Sonraki fazlar: sol panel havuzu, filter chip'ler, conflict-only mode, view modes.
 */
export default function SchedulingEditorPage() {
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const rootRef = useRef<HTMLDivElement>(null);

  const { data: activeTerm, isLoading: termLoading } = useActiveTerm();
  const { data: faculties, isLoading: facultiesLoading } = useFacultyLookup();
  const { isFullscreen, toggle } = useFullscreen();

  const urlFacultyId = searchParams.get('facultyId');
  const urlTermId = searchParams.get('termId');
  const urlSearch = searchParams.get('q') ?? '';
  const urlDept = searchParams.get('dept') ?? '';
  const urlYear = searchParams.get('year') ?? '';
  const conflictMode = searchParams.get('conflicts') === '1';

  const facultyId = urlFacultyId
    ? Number(urlFacultyId)
    : faculties?.[0]?.id ?? null;
  const termId = urlTermId
    ? Number(urlTermId)
    : activeTerm?.id ?? null;

  const sectionsQuery = useFacultyPlanningSections(facultyId, termId, undefined, undefined, 1, 200);
  const timeSlotsQuery = useFacultyPlanningTimeSlots(termId);
  const conflictsQuery = useFacultyPlanningConflicts(facultyId, termId);
  const assignMutation = useAssignSection();
  const unassignMutation = useUnassignSection();
  const sections = useMemo(() => sectionsQuery.data?.items ?? [], [sectionsQuery.data]);
  const timeSlots = useMemo(() => timeSlotsQuery.data ?? [], [timeSlotsQuery.data]);

  /** Gerçek slot süresi — ilk slot'tan hesaplanır. Pool drag için durationMinutes hesabında kullanılır. */
  const slotMinutes = useMemo(() => {
    if (timeSlots.length === 0) return 50;
    const first = timeSlots[0];
    const start = timeToMinutes(first.startTime.slice(0, 5));
    const end = timeToMinutes(first.endTime.slice(0, 5));
    return Math.max(1, end - start);
  }, [timeSlots]);

  // Filter alanları için unique listeler (mevcut section'lardan türetilir, "boş" seçenek doğal)
  const departmentOptions = useMemo(() => {
    const set = new Set<string>();
    for (const s of sections) set.add(s.departmentCode);
    return Array.from(set).filter(Boolean).sort();
  }, [sections]);

  const yearOptions = useMemo(() => {
    const set = new Set<number>();
    for (const s of sections) {
      const y = s.suggestedSemester > 0 ? Math.ceil(s.suggestedSemester / 2) : 0;
      if (y > 0) set.add(y);
    }
    return Array.from(set).sort((a, b) => a - b);
  }, [sections]);

  // Aktif filtrelerle daraltılmış section listesi — grid + havuz ortak kullanır
  const filteredSections = useMemo(() => {
    const q = urlSearch.trim().toLowerCase();
    const yearNum = urlYear ? Number(urlYear) : null;
    return sections.filter(s => {
      if (urlDept && s.departmentCode !== urlDept) return false;
      if (yearNum != null && !Number.isNaN(yearNum)) {
        const sectionYear = s.suggestedSemester > 0 ? Math.ceil(s.suggestedSemester / 2) : 0;
        if (sectionYear !== yearNum) return false;
      }
      if (q) {
        const hit = s.courseCode.toLowerCase().includes(q)
          || s.courseName.toLowerCase().includes(q)
          || (s.instructorName?.toLowerCase().includes(q) ?? false);
        if (!hit) return false;
      }
      return true;
    });
  }, [sections, urlSearch, urlDept, urlYear]);

  const activeFilterCount = [urlSearch, urlDept, urlYear].filter(Boolean).length;

  /** Conflict-only mod: çakışmalı + atanmamış section'ları normal, diğerlerini silikleştirir. */
  const focusedSectionIds = useMemo(() => {
    if (!conflictMode) return undefined;
    const ids = new Set<number>();
    for (const c of conflictsQuery.data ?? []) {
      ids.add(c.sectionId);
      if (c.relatedSectionId) ids.add(c.relatedSectionId);
    }
    for (const s of sections) {
      if (s.status === 'Unscheduled' || s.status === 'Conflicted' || s.status === 'Partial') {
        ids.add(s.sectionId);
      }
    }
    return ids;
  }, [conflictMode, conflictsQuery.data, sections]);

  const conflictCount = conflictsQuery.data?.length ?? 0;

  /**
   * Section bazında backend conflict mesajları haritası. Kart kırmızıyken kullanıcı fareyle
   * üstüne gelince hangi kuralı ihlal ettiğini tooltip'te görür. Mesajlar tamamen DB-driven
   * (section adı, derslik kodu, hoca, saat ile RuleEngine'den geliyor — hardcoded yok).
   * Conflict her iki section'a (sectionId + relatedSectionId) yansıtılır.
   */
  const sectionConflicts = useMemo(() => {
    const map = new Map<number, string[]>();
    for (const c of conflictsQuery.data ?? []) {
      const push = (sid: number) => {
        const list = map.get(sid) ?? [];
        if (!list.includes(c.message)) list.push(c.message);
        map.set(sid, list);
      };
      push(c.sectionId);
      if (c.relatedSectionId) push(c.relatedSectionId);
    }
    return map;
  }, [conflictsQuery.data]);

  const termStatus = activeTerm?.status;
  const isTermEditable = termStatus === 'Active';

  /**
   * Override akışı state — backend Force=false ile çağrıldığında conflict varsa atama yazılmaz
   * ve requiresConfirmation=true döner. Bu state dolduğunda AlertDialog açılır; kullanıcı
   * "Devam Et" derse aynı request Force=true ile tekrar gönderilir.
   *
   * `request` ve `successMessage` orijinal payload'u + feedback metnini saklar → onay sonrası
   * aynısı tekrar kullanılır. `conflicts` backend'den gelen tam RuleEngine mesajları (hardcoded
   * metin yok, hepsi DB'deki section/derslik/hoca bilgisinden üretilmiş).
   */
  const [pendingConfirmation, setPendingConfirmation] = useState<{
    request: AssignSectionRequest;
    conflicts: SchedulingConflict[];
    successMessage: string;
  } | null>(null);

  /**
   * Toplu unassign onay state'i. Yıkıcı işlem (N section'ın atamasını silmek) — kullanıcının
   * yanlışlıkla tıklayıp veri kaybetmesini önlemek için DelayedConfirmDialog ile onay alınır.
   * Etkilenecek section listesi DB-driven mesajlarla gösterilir (kod + ad + program özeti).
   */
  const [pendingBulkUnassign, setPendingBulkUnassign] = useState<{
    eligibleIds: number[];
    skippedCount: number;
    preview: FacultyPlanningSection[];
  } | null>(null);

  const [dragPreview, setDragPreview] = useState<DragPreviewState | null>(null);
  const [invalidDropIds, setInvalidDropIds] = useState<Set<string>>(new Set());
  /**
   * Backend bulk-validate cevabından doldurulur — dropId → ilk conflict mesajı.
   * Tooltip'te kırmızı hücre üzerinde gösterilir. Drag start'ta sıfırlanır,
   * backend cevabı gelince doldurulur, drag end/cancel'da temizlenir.
   */
  const [cellReasons, setCellReasons] = useState<Map<string, string>>(new Map());
  const [suggestedDropIds, setSuggestedDropIds] = useState<Set<string>>(new Set());
  const [feedback, setFeedback] = useState<FeedbackState | null>(null);
  const [multiSelectMode, setMultiSelectMode] = useState(false);
  const [selectedSectionIds, setSelectedSectionIds] = useState<Set<number>>(new Set());
  const [bulkPending, setBulkPending] = useState(false);

  /**
   * Aktif drag'in sectionId'sini takip eder. Async bulk-validate cevabı dönerken kullanıcı
   * sürüklemeyi iptal etmiş veya başka bir section'a geçmişse geç gelen cevap atılır.
   */
  const activeDragSectionRef = useRef<number | null>(null);

  // 8px activation distance → tıklama Popover açar, sürükleyince drag başlar
  const sensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 8 } }),
    useSensor(KeyboardSensor),
  );

  // Collision detection: pointer-based primary + rectIntersection fallback.
  // Default `rectIntersection` grid→grid drag'inde source cell ile drop overlay'leri arasında
  // intersection bulamıyor ve `over` her DragMove'da null kalıyordu (pool→grid çalışıyordu çünkü
  // source aside ayrı container). `pointerWithin` mouse cursor'un hangi cell içinde olduğuna
  // doğrudan bakar; pointer hücreler arasında boşluktayken `rectIntersection` fallback en yakın
  // droppable'ı bulur. Pool drop'u (UNASSIGNED_POOL_DROP_ID) da bu yolla doğru hedef olarak gelir.
  const collisionDetection: CollisionDetection = useCallback((args) => {
    const pointerCollisions = pointerWithin(args);
    if (pointerCollisions.length > 0) return pointerCollisions;
    return rectIntersection(args);
  }, []);

  // URL'de fakülte/dönem yoksa default'ları yaz — link paylaşılabilir kalsın
  useEffect(() => {
    if (facultyId == null || termId == null) return;
    if (urlFacultyId && urlTermId) return;
    const next = new URLSearchParams(searchParams);
    next.set('facultyId', String(facultyId));
    next.set('termId', String(termId));
    setSearchParams(next, { replace: true });
  }, [facultyId, termId, urlFacultyId, urlTermId, searchParams, setSearchParams]);

  // ESC tuşu: fullscreen'deyken native API kapanır, değilse sayfayı kapat
  useEffect(() => {
    const handler = (e: KeyboardEvent) => {
      if (e.key === 'Escape' && !isFullscreen && !dragPreview) {
        navigate(-1);
      }
    };
    window.addEventListener('keydown', handler);
    return () => window.removeEventListener('keydown', handler);
  }, [isFullscreen, dragPreview, navigate]);

  // Feedback otomatik kapan
  useEffect(() => {
    if (!feedback) return;
    const t = setTimeout(() => setFeedback(null), 3000);
    return () => clearTimeout(t);
  }, [feedback]);

  const handleFacultyChange = (newId: number) => {
    const next = new URLSearchParams(searchParams);
    next.set('facultyId', String(newId));
    // Fakülte değişirse bölüm filtresi anlam kaybeder, sıfırla
    next.delete('dept');
    setSearchParams(next, { replace: true });
  };

  const updateFilter = useCallback((key: 'q' | 'dept' | 'year', value: string) => {
    const next = new URLSearchParams(searchParams);
    if (value) next.set(key, value);
    else next.delete(key);
    setSearchParams(next, { replace: true });
  }, [searchParams, setSearchParams]);

  const clearFilters = useCallback(() => {
    const next = new URLSearchParams(searchParams);
    next.delete('q');
    next.delete('dept');
    next.delete('year');
    setSearchParams(next, { replace: true });
  }, [searchParams, setSearchParams]);

  const handleClose = () => navigate(-1);
  const handleToggleFullscreen = () => toggle(rootRef.current ?? undefined);

  const toggleSectionSelect = useCallback((sectionId: number) => {
    setSelectedSectionIds(prev => {
      const next = new Set(prev);
      if (next.has(sectionId)) next.delete(sectionId);
      else next.add(sectionId);
      return next;
    });
  }, []);

  const exitMultiSelect = useCallback(() => {
    setMultiSelectMode(false);
    setSelectedSectionIds(new Set());
  }, []);

  /** Seçili kartların atamasını paralel kaldırır — onay dialog'u açar. Asıl mutate handleConfirmBulkUnassign'da. */
  const handleBulkUnassign = useCallback(() => {
    if (selectedSectionIds.size === 0) return;
    const ids = Array.from(selectedSectionIds);
    const eligibleSections = sections.filter(s =>
      ids.includes(s.sectionId) && !s.isPinned && s.status !== 'Unscheduled'
    );
    const skippedCount = ids.length - eligibleSections.length;
    if (eligibleSections.length === 0) {
      setFeedback({ kind: 'error', message: 'Seçili kartların hepsi pinli veya zaten atanmamış.' });
      return;
    }
    setPendingBulkUnassign({
      eligibleIds: eligibleSections.map(s => s.sectionId),
      skippedCount,
      preview: eligibleSections,
    });
  }, [selectedSectionIds, sections]);

  /** Kullanıcı onay verince mutate eder. 5 sn delay sonrası tıklanabilir buton tetikler. */
  const handleConfirmBulkUnassign = useCallback(async () => {
    if (!pendingBulkUnassign) return;
    const { eligibleIds, skippedCount } = pendingBulkUnassign;
    setPendingBulkUnassign(null);
    setBulkPending(true);
    const results = await Promise.allSettled(
      eligibleIds.map(id => unassignMutation.mutateAsync(id)),
    );
    setBulkPending(false);
    const okCount = results.filter(r => r.status === 'fulfilled').length;
    const errCount = results.length - okCount;
    setFeedback({
      kind: errCount === 0 ? 'success' : 'error',
      message: `${okCount} şube kaldırıldı${errCount > 0 ? `, ${errCount} hata` : ''}${skippedCount > 0 ? ` · ${skippedCount} pinli/atanmamış atlandı` : ''}`,
    });
    exitMultiSelect();
  }, [pendingBulkUnassign, unassignMutation, exitMultiSelect]);

  const handleDragStart = useCallback((event: DragStartEvent) => {
    const data = event.active.data.current as DragData | undefined;
    if (!data) return;
    const section = sections.find(s => s.sectionId === data.sectionId);
    if (!section) return;

    setDragPreview({ section, data });
    setCellReasons(new Map());
    activeDragSectionRef.current = section.sectionId;

    // Backend bulk-validate için aday hücreleri hesapla
    const candidates = buildBulkCandidates(data, section, timeSlots);
    const candidateKeys = new Set(candidates.map(c => c.cellKey));

    // Aday olmayan hücreler anında kırmızı — fiziksel imkansızlık (dolu, gün sonu, ardışık değil).
    // Aday hücreler nötr başlar, backend cevabıyla yeşil veya kırmızı olur.
    const nonCandidateInvalid = new Set<string>();
    for (const ts of timeSlots) {
      const cellId = `cell-${ts.day}-${ts.slotOrder}`;
      if (!candidateKeys.has(cellId)) {
        nonCandidateInvalid.add(cellId);
      }
    }
    setInvalidDropIds(nonCandidateInvalid);
    setSuggestedDropIds(new Set());

    if (candidates.length === 0) return;

    const requestedSectionId = section.sectionId;
    validateCellsBulk({
      sectionId: section.sectionId,
      instructorId: section.instructorId ?? undefined,
      classroomId: data.classroomId ?? section.classroomId ?? undefined,
      candidates,
    }).then(result => {
      if (activeDragSectionRef.current !== requestedSectionId) return;

      // Non-candidate hücreler kırmızı kalır + backend'in reddettiği adaylar da eklenir
      const newInvalid = new Set<string>(nonCandidateInvalid);
      const newReasons = new Map<string, string>();

      for (const r of result.results) {
        if (!r.isValid) {
          newInvalid.add(r.cellKey);
          if (r.reason) newReasons.set(r.cellKey, r.reason);
        }
      }

      setInvalidDropIds(newInvalid);
      setCellReasons(newReasons);
      setSuggestedDropIds(computeSmartSuggestions(section, newInvalid, timeSlots, sections));
    }).catch(() => {
      // Backend hata verirse non-candidate kırmızı kalır, adaylar nötr — drop backend assign sonucuyla belirlenir
    });
  }, [sections, timeSlots]);

  const handleDragCancel = useCallback(() => {
    setDragPreview(null);
    setInvalidDropIds(new Set());
    setCellReasons(new Map());
    setSuggestedDropIds(new Set());
    activeDragSectionRef.current = null;
  }, []);

  const handleDragEnd = useCallback(async (event: DragEndEvent) => {
    setDragPreview(null);
    setInvalidDropIds(new Set());
    setCellReasons(new Map());
    setSuggestedDropIds(new Set());
    activeDragSectionRef.current = null;
    const { active, over } = event;
    if (!over) {
      console.warn('[ScheduleEditor] drag end: over=null', { activeId: active.id, data: active.data.current });
      setFeedback({
        kind: 'error',
        message: 'Sürükleme bir hedef hücreye ulaşmadı. Kartı bir hücrenin üzerine bırakın.',
      });
      return;
    }

    const dropId = String(over.id);
    const data = active.data.current as DragData | undefined;

    // Atanmamış havuza drop → unassign akışı (yalnız grid kartları için anlamlı)
    if (dropId === UNASSIGNED_POOL_DROP_ID) {
      if (!data || data.kind !== 'block') return;
      const section = sections.find(s => s.sectionId === data.sectionId);
      if (!section) return;
      if (section.isPinned) {
        setFeedback({ kind: 'error', message: `${section.courseCode}: sabitlenmiş şube, kaldırılamaz.` });
        return;
      }
      unassignMutation.mutate(section.sectionId, {
        onSuccess: () => {
          setFeedback({ kind: 'success', message: `${section.courseCode} ataması kaldırıldı.` });
        },
        onError: (error) => {
          setFeedback({ kind: 'error', message: extractError(error, 'Kaldırma başarısız.') });
        },
      });
      return;
    }

    if (!dropId.startsWith('cell-')) return;

    const [, dayStr, slotOrderStr] = dropId.split('-');
    const targetDay = Number(dayStr);
    const targetSlotOrder = Number(slotOrderStr);

    // Option A: frontend pre-check kaldırıldı. Drop her zaman backend'e gider.
    // Backend Force=false ile çağrıldığında conflict varsa requiresConfirmation döner,
    // override dialog spesifik mesajlarla açılır — tutarlı UX.

    if (!data) return;
    const section = sections.find(s => s.sectionId === data.sectionId);
    if (!section) return;

    // Hedef güne ait slot'ları sırala, başlangıç slot'unu bul
    const sameDaySlots = timeSlots
      .filter(ts => ts.day === targetDay)
      .sort((a, b) => a.slotOrder - b.slotOrder);
    const startIdx = sameDaySlots.findIndex(ts => ts.slotOrder === targetSlotOrder);
    if (startIdx < 0) {
      setFeedback({ kind: 'error', message: 'Hedef slot bulunamadı.' });
      return;
    }

    const startSlot = sameDaySlots[startIdx];
    const startHm = startSlot.startTime.slice(0, 5);
    const startSlotMinutes = timeToMinutes(startSlot.endTime.slice(0, 5)) - timeToMinutes(startHm);
    const neededSlots = startSlotMinutes > 0
      ? Math.max(1, Math.round(data.durationMinutes / startSlotMinutes))
      : 1;
    if (startIdx + neededSlots > sameDaySlots.length) {
      setFeedback({ kind: 'error', message: `${section.courseCode}: hedef slot aralığı gün sonuna sığmıyor.` });
      return;
    }
    // Ardışık slot kontrolü (slotOrder'lar birbirini izlemeli)
    for (let i = 1; i < neededSlots; i += 1) {
      if (sameDaySlots[startIdx + i].slotOrder !== sameDaySlots[startIdx + i - 1].slotOrder + 1) {
        setFeedback({ kind: 'error', message: `${section.courseCode}: hedef saatlerde ardışık slot yok (öğle arası olabilir).` });
        return;
      }
    }
    const endHm = sameDaySlots[startIdx + neededSlots - 1].endTime.slice(0, 5);

    // ── Pool kaynağı: yeni atama akışı ──
    if (data.kind === 'pool') {
      let classroomId = data.classroomId ?? undefined;
      // FaceToFace + Hybrid dersler için derslik zorunlu; en uygun ilk dersliği API'den al
      if (section.teachingMethod !== 'Online') {
        if (termId == null) {
          setFeedback({ kind: 'error', message: 'Aktif dönem bulunamadı, atama yapılamaz.' });
          return;
        }
        // Backend Assignment.cs:60-61 ile birebir aynı türetim: pür-lab dersler "Lab", diğerleri
        // "Lecture" derslik bekler. type=undefined geçmek getAvailableClassrooms'i tüm tiplere açar
        // ve ilk dönen yanlış tipte olunca H4 ClassroomType backend assign'da reddeder
        // ("Teori dersi (X) Lab/uzman tipi dersliğe atanamaz") → kullanıcı yeşil hücreye drop yapar,
        // dropta sessizce yanlış derslik seçilir, backend reddeder.
        const requiredType = section.labHours > 0 && section.theoryHours === 0 ? 'Lab' : 'Lecture';
        try {
          const available = await getAvailableClassrooms(
            termId, targetDay, startHm, endHm, section.capacity, requiredType,
          );
          if (available.length === 0) {
            setFeedback({
              kind: 'error',
              message: `${section.courseCode}: uygun derslik yok (${requiredType} tipi). Boş hücreye tıklayıp manuel atama yapın.`,
            });
            return;
          }
          classroomId = available[0].classroomId;
        } catch (e) {
          setFeedback({ kind: 'error', message: extractError(e, 'Uygun derslik sorgulanamadı.') });
          return;
        }
      }

      // Kısmi atama desteği — section'ın mevcut blokları korunur, yeni block (data.blockType
      // ile gelen Teori veya Lab) eklenir. Pool drag handle artık tek bölüm olarak çalışır
      // (3T+2L dersin Teori bloğu ayrı, Lab bloğu ayrı drag).
      const existingSlots: ScheduleSlot[] = section.schedules.map(b => ({
        day: b.day,
        startTime: b.startTime.slice(0, 5),
        endTime: b.endTime.slice(0, 5),
        classroomId: b.classroomId ?? section.classroomId ?? undefined,
      }));
      const newSlot: ScheduleSlot = { day: targetDay, startTime: startHm, endTime: endHm, classroomId };

      const poolRequest: AssignSectionRequest = {
        sectionId: section.sectionId,
        classroomId,
        instructorId: section.instructorId ?? undefined,
        scheduleSlots: [...existingSlots, newSlot],
      };
      const blockLabel = data.blockType === 'Theory' ? ' (Teori)' : data.blockType === 'Lab' ? ' (Lab)' : '';
      const poolSuccessMessage = `${section.courseCode}${blockLabel} atandı: ${startHm}-${endHm}${classroomId ? '' : ' (online)'}`;

      assignMutation.mutate(poolRequest, {
        onSuccess: (result) => {
          if (result.requiresConfirmation) {
            // Backend conflict gördü, yazmadı → dialog aç, kullanıcı override karar versin
            setPendingConfirmation({
              request: poolRequest,
              conflicts: result.conflicts,
              successMessage: poolSuccessMessage,
            });
            return;
          }
          setFeedback({ kind: 'success', message: poolSuccessMessage });
        },
        onError: (error) => {
          setFeedback({ kind: 'error', message: extractError(error, 'Atama başarısız.') });
        },
      });
      return;
    }

    // ── Grid kaynağı: taşıma akışı ──
    // Aynı yere bırakıldıysa no-op (mantıksız çağrı atma)
    if (
      targetDay === data.currentDay
      && startHm === data.currentStartTime?.slice(0, 5)
      && endHm === data.currentEndTime?.slice(0, 5)
    ) {
      return;
    }

    // Section'ın tüm bloklarını yeniden gönder; sürüklenen block hariç kalanlar aynı kalır
    const otherBlocks = section.schedules.filter(b => !(
      b.day === data.currentDay
      && b.startTime.slice(0, 5) === data.currentStartTime?.slice(0, 5)
      && b.blockType === data.blockType
    ));
    const newScheduleSlots: ScheduleSlot[] = [
      ...otherBlocks.map(b => ({
        day: b.day,
        startTime: b.startTime.slice(0, 5),
        endTime: b.endTime.slice(0, 5),
        classroomId: b.classroomId ?? data.classroomId ?? undefined,
      })),
      {
        day: targetDay,
        startTime: startHm,
        endTime: endHm,
        classroomId: data.classroomId ?? undefined,
      },
    ];

    const moveRequest: AssignSectionRequest = {
      sectionId: section.sectionId,
      classroomId: data.classroomId ?? section.classroomId ?? undefined,
      instructorId: section.instructorId ?? undefined,
      scheduleSlots: newScheduleSlots,
    };
    const moveSuccessMessage = `${section.courseCode} taşındı: ${startHm}-${endHm}`;

    assignMutation.mutate(moveRequest, {
      onSuccess: (result) => {
        if (result.requiresConfirmation) {
          setPendingConfirmation({
            request: moveRequest,
            conflicts: result.conflicts,
            successMessage: moveSuccessMessage,
          });
          return;
        }
        setFeedback({ kind: 'success', message: moveSuccessMessage });
      },
      onError: (error) => {
        setFeedback({ kind: 'error', message: extractError(error, 'Taşıma başarısız.') });
      },
    });
  }, [sections, timeSlots, assignMutation, unassignMutation, termId]);

  /**
   * Override onay handler — kullanıcı dialog'da "Yine de devam et" derse aynı request'i
   * force=true ile tekrar gönderir. Backend force=true ile çağrıldığında conflict'leri
   * warning olarak yansıtır ve atamayı yazar.
   */
  const handleConfirmOverride = useCallback(() => {
    if (!pendingConfirmation) return;
    const { request, successMessage } = pendingConfirmation;
    setPendingConfirmation(null);
    assignMutation.mutate(
      { ...request, force: true },
      {
        onSuccess: (result) => {
          if (result.warnings.length > 0) {
            // Override sonrası backend warning listesi döndü — kullanıcıya başarılı + uyarı
            setFeedback({
              kind: 'success',
              message: `${successMessage} (${result.warnings.length} uyarıyla)`,
            });
          } else {
            setFeedback({ kind: 'success', message: successMessage });
          }
        },
        onError: (error) => {
          setFeedback({ kind: 'error', message: extractError(error, 'Atama başarısız.') });
        },
      },
    );
  }, [pendingConfirmation, assignMutation]);

  const handleCancelOverride = useCallback(() => {
    setPendingConfirmation(null);
    setFeedback({ kind: 'error', message: 'Atama iptal edildi.' });
  }, []);

  if (termLoading || facultiesLoading) {
    return (
      <div className="fixed inset-0 bg-slate-50 flex items-center justify-center">
        <Skeleton className="w-[90vw] h-[80vh] rounded-xl" />
      </div>
    );
  }

  if (facultyId == null || termId == null) {
    return (
      <div className="fixed inset-0 bg-slate-50 flex items-center justify-center">
        <Alert className="max-w-md">
          <AlertCircle className="h-4 w-4" />
          <AlertDescription>
            Aktif dönem veya fakülte bulunamadı. Önceki sayfaya dönülüyor...
          </AlertDescription>
        </Alert>
      </div>
    );
  }

  return (
    <>
    <DndContext
      sensors={sensors}
      collisionDetection={collisionDetection}
      // autoScroll kapalı: scrollable parent (main overflow-auto) içinde aktif kartı takip etmek
      // için autoScroll cell rect'lerini yeniden ölçmeye çalışıyor ve Status.Initialized geçişi
      // (activeNodeRect ölçümü) hiç tamamlanmıyordu → over her zaman null.
      autoScroll={false}
      // Scrollable parent + position:fixed kombinasyonunda droppable rect'leri stale kalıyordu.
      // Always ile her drag-move'da rect'ler yeniden ölçülür.
      measuring={{ droppable: { strategy: MeasuringStrategy.Always } }}
      onDragStart={handleDragStart}
      onDragEnd={handleDragEnd}
      onDragCancel={handleDragCancel}
    >
      <div
        ref={rootRef}
        // position: fixed parent dnd-kit ile çakışıyordu (issue: droppable rect'leri scrollable
        // ancestor offset hesabıyla yanlış viewport koordinatlarına dönüşüyordu). Normal flow'a alındı.
        className="h-screen w-screen bg-slate-50 flex flex-col overflow-hidden"
      >
        {/* ── Minimal toolbar ── */}
        <header className="shrink-0 bg-white border-b border-slate-200 px-4 py-2 flex items-center gap-3">
          <TooltipProvider delayDuration={150}>
            <Tooltip>
              <TooltipTrigger asChild>
                <Button
                  variant="ghost"
                  size="sm"
                  className="h-8 w-8 p-0"
                  onClick={handleClose}
                  aria-label="Kapat"
                >
                  <ArrowLeft className="h-4 w-4" />
                </Button>
              </TooltipTrigger>
              <TooltipContent side="bottom">Geri Dön (ESC)</TooltipContent>
            </Tooltip>
          </TooltipProvider>

          <div className="flex items-center gap-2">
            <span className="text-sm font-semibold text-[#003366]">
              Ders Programı Düzenleyici
            </span>
            <span className="text-slate-300">•</span>
            <select
              className="h-7 rounded-md border border-slate-200 bg-white px-2 text-sm font-medium text-slate-700"
              value={facultyId}
              onChange={e => handleFacultyChange(Number(e.target.value))}
            >
              {(faculties ?? []).map(f => (
                <option key={f.id} value={f.id}>{f.code} — {f.name}</option>
              ))}
            </select>
          </div>

          {/* Mutation status mini-indikatör */}
          {(assignMutation.isPending || unassignMutation.isPending) && (
            <span className="flex items-center gap-1.5 text-[11px] text-slate-500">
              <Loader2 className="h-3 w-3 animate-spin" />
              Kaydediliyor...
            </span>
          )}

          {/* Filter chip alanı */}
          <div className="w-px h-5 bg-slate-200" />
          <div className="relative">
            <Search className="absolute left-2 top-1/2 -translate-y-1/2 h-3 w-3 text-slate-400 pointer-events-none" />
            <Input
              placeholder="Ders, hoca..."
              value={urlSearch}
              onChange={e => updateFilter('q', e.target.value)}
              className="pl-7 h-7 w-40 text-xs border-slate-200"
            />
          </div>
          <select
            className="h-7 rounded-md border border-slate-200 bg-white px-2 text-xs text-slate-700"
            value={urlDept}
            onChange={e => updateFilter('dept', e.target.value)}
            aria-label="Bölüm filtresi"
          >
            <option value="">Tüm Bölümler</option>
            {departmentOptions.map(code => (
              <option key={code} value={code}>{code}</option>
            ))}
          </select>
          <select
            className="h-7 rounded-md border border-slate-200 bg-white px-2 text-xs text-slate-700"
            value={urlYear}
            onChange={e => updateFilter('year', e.target.value)}
            aria-label="Sınıf yılı filtresi"
          >
            <option value="">Tüm Sınıflar</option>
            {yearOptions.map(y => (
              <option key={y} value={y}>{y}. sınıf</option>
            ))}
          </select>
          {activeFilterCount > 0 && (
            <button
              type="button"
              onClick={clearFilters}
              className="text-[11px] text-slate-500 hover:text-red-600 underline underline-offset-2"
            >
              Temizle ({activeFilterCount})
            </button>
          )}

          <div className="flex-1" />

          {/* Multi-select mod toggle */}
          <TooltipProvider delayDuration={150}>
            <Tooltip>
              <TooltipTrigger asChild>
                <Button
                  size="sm"
                  variant={multiSelectMode ? 'default' : 'outline'}
                  className={`h-8 gap-1.5 ${multiSelectMode ? 'bg-[#003366] hover:bg-[#002244] text-white' : ''}`}
                  onClick={() => {
                    if (multiSelectMode) exitMultiSelect();
                    else setMultiSelectMode(true);
                  }}
                >
                  <CheckSquare className="h-3.5 w-3.5" />
                  {multiSelectMode ? `${selectedSectionIds.size} seçili` : 'Çoklu Seç'}
                </Button>
              </TooltipTrigger>
              <TooltipContent side="bottom" className="text-xs">
                Birden fazla şube seçip toplu işlem yap
              </TooltipContent>
            </Tooltip>
          </TooltipProvider>

          {/* Conflict-only mode toggle */}
          <TooltipProvider delayDuration={150}>
            <Tooltip>
              <TooltipTrigger asChild>
                <Button
                  size="sm"
                  variant={conflictMode ? 'default' : 'outline'}
                  className={`h-8 gap-1.5 ${conflictMode ? 'bg-red-600 hover:bg-red-700 text-white' : ''}`}
                  onClick={() => {
                    const next = new URLSearchParams(searchParams);
                    if (conflictMode) next.delete('conflicts');
                    else next.set('conflicts', '1');
                    setSearchParams(next, { replace: true });
                  }}
                >
                  <AlertTriangle className="h-3.5 w-3.5" />
                  {conflictMode ? 'Tüm Şubeler' : 'Sadece Sorunlular'}
                  {!conflictMode && conflictCount > 0 && (
                    <span className="bg-red-100 text-red-700 text-[10px] tabular-nums rounded px-1 ml-0.5">
                      {conflictCount}
                    </span>
                  )}
                </Button>
              </TooltipTrigger>
              <TooltipContent side="bottom" className="text-xs">
                Atanmamış/çakışmalı şubelere odaklanır
              </TooltipContent>
            </Tooltip>
          </TooltipProvider>

          <TooltipProvider delayDuration={150}>
            <Tooltip>
              <TooltipTrigger asChild>
                <Button
                  variant="outline"
                  size="sm"
                  className="h-8 gap-1.5"
                  onClick={handleToggleFullscreen}
                >
                  {isFullscreen
                    ? <><Minimize2 className="h-3.5 w-3.5" /> Tam Ekrandan Çık</>
                    : <><Maximize2 className="h-3.5 w-3.5" /> Tam Ekran</>}
                </Button>
              </TooltipTrigger>
              <TooltipContent side="bottom">
                Tarayıcıyı tam ekrana getirir (sekmeler de gizlenir)
              </TooltipContent>
            </Tooltip>
          </TooltipProvider>
        </header>

        {/* ── Multi-select aksiyon bar'ı ── */}
        {multiSelectMode && (
          <div className="shrink-0 bg-[#003366] text-white px-4 py-1.5 flex items-center gap-3">
            <CheckSquare className="h-3.5 w-3.5" />
            <span className="text-xs font-medium">
              {selectedSectionIds.size} şube seçili
              {selectedSectionIds.size === 0 && ' — kartlara tıklayıp seçin'}
            </span>
            <div className="flex-1" />
            <Button
              size="sm"
              variant="ghost"
              className="h-7 text-xs text-white hover:bg-white/10"
              disabled={selectedSectionIds.size === 0 || bulkPending}
              onClick={handleBulkUnassign}
            >
              <Trash2 className="h-3.5 w-3.5 mr-1" />
              {bulkPending ? 'Kaldırılıyor...' : 'Atamayı Kaldır'}
            </Button>
            <Button
              size="sm"
              variant="ghost"
              className="h-7 text-xs text-white hover:bg-white/10"
              onClick={exitMultiSelect}
            >
              <X className="h-3.5 w-3.5 mr-1" />
              Kapat
            </Button>
          </div>
        )}

        {/* ── Term gate uyarısı ── */}
        {!isTermEditable && (
          <div className="shrink-0 bg-amber-50 border-b border-amber-200 px-4 py-1.5 text-center">
            <p className="text-xs text-amber-800 font-medium">
              Dönem durumu "{termStatus ?? 'bilinmiyor'}" — backend yalnız Active dönemde değişikliği kabul eder; drag-drop denenebilir, sonuç banner'da görünür.
            </p>
          </div>
        )}

        {/* ── Drag legend — sürükleme aktifken sağ üst köşede mini açıklama ── */}
        {dragPreview && (
          <div className="shrink-0 bg-slate-50 border-b border-slate-200 px-4 py-1 flex items-center justify-center gap-4 text-[11px] text-slate-600">
            <span className="flex items-center gap-1.5">
              <span className="inline-block w-3 h-3 rounded ring-2 ring-emerald-400 bg-emerald-50" />
              Bırakma hedefi
            </span>
            <span className="flex items-center gap-1.5">
              <span className="inline-block w-3 h-3 rounded ring-2 ring-amber-300 bg-amber-50/60" />
              Akıllı öneri (en uygun 3 hücre)
            </span>
            <span className="flex items-center gap-1.5">
              <span className="inline-block w-3 h-3 rounded ring-2 ring-red-400 bg-red-50/60" />
              Uyumsuz (çakışma/slot tipi)
            </span>
          </div>
        )}

        {/* ── Feedback overlay — ekran ortasında belirgin uyarı, 3 sn sonra veya X ile kapanır ── */}
        {feedback && (
          <div className="fixed inset-0 z-[100] flex items-center justify-center pointer-events-none">
            <div
              className={`pointer-events-auto flex items-center gap-3 px-6 py-4 rounded-xl shadow-2xl border backdrop-blur-sm animate-in fade-in zoom-in-95 duration-200 max-w-lg ${
                feedback.kind === 'success'
                  ? 'bg-emerald-50/95 border-emerald-300 text-emerald-900'
                  : 'bg-red-50/95 border-red-300 text-red-900'
              }`}
            >
              {feedback.kind === 'success'
                ? <CheckCircle2 className="h-6 w-6 shrink-0 text-emerald-600" />
                : <AlertCircle className="h-6 w-6 shrink-0 text-red-600" />}
              <span className="text-sm font-semibold flex-1">{feedback.message}</span>
              <button
                className={`p-1 rounded-lg transition-colors ${
                  feedback.kind === 'success'
                    ? 'hover:bg-emerald-200/60'
                    : 'hover:bg-red-200/60'
                }`}
                onClick={() => setFeedback(null)}
                aria-label="Kapat"
              >
                <X className="h-4 w-4" />
              </button>
            </div>
          </div>
        )}

        {/* ── Ana çalışma alanı ── */}
        <div className="flex-1 min-h-0 flex overflow-hidden">
          {/* Sol panel — atanmamış havuz + unassign drop hedefi (filtreyle daraltılır) */}
          <div className="hidden lg:flex">
            <UnassignedSectionsPanel
              sections={filteredSections}
              isDragging={dragPreview != null}
              canAcceptDrop={dragPreview != null && !dragPreview.section.isPinned}
              slotMinutes={slotMinutes}
            />
          </div>

          {/* Sağ — takvim grid (scroll burada; CalendarGrid içinde değil — dnd-kit measuring uyumluluğu için) */}
          <main className="flex-1 min-w-0 p-3 overflow-auto">
            {sectionsQuery.isLoading || timeSlotsQuery.isLoading ? (
              <Skeleton className="w-full h-full rounded-xl" />
            ) : (
              <div className="min-h-full">
                <div className="h-full">
                  <CalendarGrid
                    sections={filteredSections}
                    timeSlots={timeSlots}
                    // Multi-select aktifken drag kapanır (kart click → toggle select). Term status burada bloklama yapmaz;
                    // sonuç backend'den banner mesajıyla döner.
                    editable={!multiSelectMode}
                    invalidDropIds={invalidDropIds}
                    invalidDropReasons={cellReasons}
                    sectionConflicts={sectionConflicts}
                    suggestedDropIds={suggestedDropIds}
                    highlightedSectionIds={focusedSectionIds}
                    maxBlocksPerCell={3}
                    selectedSectionIds={multiSelectMode ? selectedSectionIds : undefined}
                    onSectionToggleSelect={multiSelectMode ? toggleSectionSelect : undefined}
                  />
                </div>
              </div>
            )}
          </main>
        </div>
      </div>

      {/* DragOverlay kaldırıldı: scrollable parent içinde `over` her DragMove'da null kalıyordu.
          Yerine kartlar useDraggable.transform ile in-place hareket eder (SectionBlock +
          UnassignedSectionCard içinde uygulanır). */}
    </DndContext>

    {/* Override Onay Dialog'u — backend Force=false ile çağrıldığında conflict varsa açılır.
        Conflict mesajları backend RuleEngine'den geliyor (hardcoded değil — section adı, derslik
        kodu, hoca, saat gibi DB'deki o anki state'ten dinamik üretilmiş). */}
    <DelayedConfirmDialog
      open={pendingConfirmation != null}
      onOpenChange={(open) => { if (!open) handleCancelOverride(); }}
      title="Bu atama planlama kurallarına aykırı"
      description="Devam ederseniz aşağıdaki durumlar oluşacak:"
      items={(pendingConfirmation?.conflicts ?? []).map((c, idx) => ({
        key: idx,
        label: c.message,
      }))}
      footnote='İstersen iptal edip farklı bir hücreye taşıyabilir, ya da bu kuralları yok sayarak devam edebilirsin. Devam edersen section "Çakışmalı" olarak işaretlenir.'
      confirmLabel="Yine de devam et"
      onConfirm={handleConfirmOverride}
      onCancel={handleCancelOverride}
      severity="warning"
    />

    {/* Toplu unassign onay dialog'u — yıkıcı işlem, kırmızı tema + 5sn delay.
        Etkilenecek section'lar DB'den gelen kod + ad ile listelenir. */}
    <DelayedConfirmDialog
      open={pendingBulkUnassign != null}
      onOpenChange={(open) => { if (!open) setPendingBulkUnassign(null); }}
      title={`${pendingBulkUnassign?.eligibleIds.length ?? 0} şubenin ataması kaldırılacak`}
      description="Aşağıdaki şubelerin program atamaları silinecek (hoca/derslik/zaman boş kalır):"
      items={(pendingBulkUnassign?.preview ?? []).map(s => ({
        key: s.sectionId,
        label: `${s.courseCode} -${s.sectionIndex} · ${s.courseName} · ${s.scheduleSummary || '(zamansız)'}`,
      }))}
      footnote={pendingBulkUnassign?.skippedCount
        ? `Bu işlem geri alınamaz. (${pendingBulkUnassign.skippedCount} kart pinli/atanmamış olduğu için atlanacak.)`
        : 'Bu işlem geri alınamaz.'}
      confirmLabel="Atamaları kaldır"
      onConfirm={handleConfirmBulkUnassign}
      onCancel={() => setPendingBulkUnassign(null)}
      severity="danger"
    />
    </>
  );
}
