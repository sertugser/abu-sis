import { Fragment, useMemo, useState } from 'react';
import { useDraggable, useDroppable } from '@dnd-kit/core';
import { cn } from '@/lib/utils';
import { dayNumberToTurkish, weekOrderKey } from '@/lib/scheduleUtils';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip';
import { Badge } from '@/components/ui/badge';
import { GripVertical, Lock, MapPin, Plus, User, Users, Clock, Coffee } from 'lucide-react';
import type { FacultyPlanningSection, FacultyPlanningTimeSlot, ScheduleBlockType } from '../../types/facultyPlanning';
import { useScheduleSettings } from '../../hooks/useScheduleSettings';
import { STATUS_CONFIG } from './sectionUtils';

/* ── 16 renk paleti — courseCode hash ile sabit renk ──
   Pastel açık arka plan + SİYAH yazı (text-slate-900) — en yüksek kontrast.
   Renk farklılığı sol kenardaki accent + arka plan tonu ile sağlanır.
*/
const COURSE_COLORS = [
  { bg: 'bg-blue-100',    border: 'border-blue-300',    text: 'text-slate-900', accent: '#2563eb' },
  { bg: 'bg-emerald-100', border: 'border-emerald-300', text: 'text-slate-900', accent: '#059669' },
  { bg: 'bg-violet-100',  border: 'border-violet-300',  text: 'text-slate-900', accent: '#7c3aed' },
  { bg: 'bg-amber-100',   border: 'border-amber-300',   text: 'text-slate-900', accent: '#d97706' },
  { bg: 'bg-rose-100',    border: 'border-rose-300',    text: 'text-slate-900', accent: '#e11d48' },
  { bg: 'bg-cyan-100',    border: 'border-cyan-300',    text: 'text-slate-900', accent: '#0891b2' },
  { bg: 'bg-orange-100',  border: 'border-orange-300',  text: 'text-slate-900', accent: '#ea580c' },
  { bg: 'bg-indigo-100',  border: 'border-indigo-300',  text: 'text-slate-900', accent: '#4f46e5' },
  { bg: 'bg-teal-100',    border: 'border-teal-300',    text: 'text-slate-900', accent: '#0d9488' },
  { bg: 'bg-pink-100',    border: 'border-pink-300',    text: 'text-slate-900', accent: '#db2777' },
  { bg: 'bg-lime-100',    border: 'border-lime-300',    text: 'text-slate-900', accent: '#65a30d' },
  { bg: 'bg-fuchsia-100', border: 'border-fuchsia-300', text: 'text-slate-900', accent: '#c026d3' },
  { bg: 'bg-sky-100',     border: 'border-sky-300',     text: 'text-slate-900', accent: '#0284c7' },
  { bg: 'bg-red-100',     border: 'border-red-300',     text: 'text-slate-900', accent: '#dc2626' },
  { bg: 'bg-yellow-100',  border: 'border-yellow-400',  text: 'text-slate-900', accent: '#ca8a04' },
  { bg: 'bg-purple-100',  border: 'border-purple-300',  text: 'text-slate-900', accent: '#9333ea' },
];

const CONFLICT_COLOR = { bg: 'bg-red-50', border: 'border-red-400', text: 'text-slate-900', accent: '#dc2626' };

/** Müfredat yılına göre renk: 1.sınıf = mavi, 2.sınıf = yeşil, 3.sınıf = mor, 4.sınıf = turuncu */
const YEAR_COLOR_MAP: Record<number, number> = { 1: 0, 2: 1, 3: 2, 4: 3 };

function getBlockColor(suggestedSemester: number, status: string) {
  if (status === 'Conflicted') return CONFLICT_COLOR;
  const year = suggestedSemester > 0 ? Math.ceil(suggestedSemester / 2) : 0;
  const colorIdx = YEAR_COLOR_MAP[year] ?? (year % COURSE_COLORS.length);
  return COURSE_COLORS[colorIdx];
}

/* ── Blok tanımları ── */
interface ParsedBlock {
  sectionId: number;
  section: FacultyPlanningSection;
  day: number;
  startTime: string;
  endTime: string;
  blockType: ScheduleBlockType;
  classroomId: number | null;
  classroomName: string | null;
}

function parseSectionSchedule(section: FacultyPlanningSection): ParsedBlock[] {
  // Backend authoritative payload: each ScheduleBlock keeps BlockType + per-block classroom.
  return (section.schedules ?? []).map(b => ({
    sectionId: section.sectionId,
    section,
    day: b.day,
    startTime: b.startTime.slice(0, 5),
    endTime: b.endTime.slice(0, 5),
    blockType: b.blockType,
    classroomId: b.classroomId,
    classroomName: b.classroomName,
  }));
}

function timeToMinutes(t: string): number {
  const [h, m] = t.split(':').map(Number);
  return h * 60 + m;
}

/** Compact T/L pill — semantic colors (green=Theory, indigo=Lab). */
function BlockTypeBadge({ blockType, className }: { blockType: ScheduleBlockType; className?: string }) {
  const isTheory = blockType === 'Theory';
  return (
    <span
      className={cn(
        'inline-flex items-center justify-center rounded font-mono font-bold text-[9px] leading-none px-1 py-0.5 border',
        isTheory
          ? 'bg-emerald-100 text-emerald-800 border-emerald-300'
          : 'bg-indigo-100 text-indigo-800 border-indigo-300',
        className,
      )}
      title={isTheory ? 'Teori' : 'Lab/Uygulama'}
      aria-label={isTheory ? 'Teori' : 'Lab/Uygulama'}
    >
      {isTheory ? 'T' : 'L'}
    </span>
  );
}

/* ── Plain CalendarCell ──
 * Editable mod açıkken `dropId` ile useDroppable hook'una bağlanır.
 *
 * NOT (dnd-kit kısıtı): setNodeRef <td> üzerinde rect doğru ölçülmüyor (display: table-cell
 * getBoundingClientRect anomalisi). Bu yüzden droppable ref'i içerideki bir <div>'e bağlanır;
 * <td> yalnız semantic ve grid layout için kullanılır.
 */
function CalendarCell({
  isRestricted, dropId, isDropDisabled, isSuggested, rowHeightPx, children, invalidReason,
}: {
  isRestricted: boolean;
  dropId: string;
  isDropDisabled: boolean;
  /** Smart suggestion ile öneriliyorsa parıltılı vurgu uygula. */
  isSuggested: boolean;
  /** Density'ye göre değişen satır yüksekliği (px). */
  rowHeightPx: number;
  children: React.ReactNode;
  /** Geçersiz hücre üzerinde fareyle gezinince gösterilecek sebep (backend RuleEngine mesajı). */
  invalidReason?: string;
}) {
  // KRİTİK: useDroppable HER hücrede register edilir; `disabled` bağlamı yok.
  // Sebep: term Inactive/Test ise editable=false geliyordu ve isDroppable=false → disabled=true
  // → dnd-kit hiç register etmiyordu → `event.over` her drag-move'da null.
  // Drop akışı handleDragEnd'de frontend pre-check ile zaten yönetilir (dolu hücre/invalid → banner).
  const { setNodeRef, isOver } = useDroppable({
    id: dropId,
  });
  return (
    <td
      className="relative border-r-2 border-r-slate-300 border-b border-b-slate-300 p-0 align-top"
      style={{ minHeight: rowHeightPx }}
      title={isDropDisabled && invalidReason ? invalidReason : undefined}
    >
      {/* Drop target: absolute+inset:0 — table-layout:fixed altında doğru rect ölçümü için zorunlu
          (normal-flow rect 0 anomalisi). pointer-events kaldırılmadı (default auto) — eski
          pointer-events-none denemesi panel droppable detection'ını bozmuş olabilir. */}
      <div
        ref={setNodeRef}
        aria-hidden="true"
        className={cn(
          'absolute inset-0 transition-colors',
          isRestricted ? 'bg-slate-50/60' : 'bg-white',
          isSuggested && !isOver && 'ring-2 ring-amber-300 ring-inset bg-amber-50/30 animate-pulse',
          isOver && !isDropDisabled && 'ring-2 ring-emerald-400 ring-inset bg-emerald-50/40 animate-none',
          isOver && isDropDisabled && 'ring-2 ring-red-400 ring-inset bg-red-50/40 animate-none',
        )}
      />
      {/* İçerik katmanı — doğal akış, hücreyi içeriğe göre büyütür (2+ kart alt alta).
          relative + z-10 ile absolute overlay'in üstünde durur → kart tıklamaları/drag düzgün çalışır.
          minHeight = rowHeightPx zorunlu: boş hücre aksi halde ~4px olur, td küçülür, overlay
          (drop rect) küçülür → drop hedefi bulunamaz. */}
      <div
        className="relative z-10 px-1 py-0.5"
        style={{ minHeight: rowHeightPx }}
      >
        {children}
      </div>
    </td>
  );
}

/* ── Popover detay ── */
function SectionPopoverContent({
  section, color,
}: {
  section: FacultyPlanningSection;
  color: typeof COURSE_COLORS[0];
}) {
  const statusCfg = STATUS_CONFIG[section.status];
  const blocks = section.schedules ?? [];
  // Heuristic: when theory and lab share a single classroom (or no per-block override), the
  // section-level Derslik line is informative; mixed sections with per-block override get the
  // classroom inside the Program list and we suppress the redundant single-classroom line.
  const distinctRooms = new Set(blocks.map(b => b.classroomName ?? '').filter(n => n !== ''));
  const showSectionRoom = distinctRooms.size <= 1;
  return (
    <div className="space-y-3 p-1">
      <div>
        <div className="flex items-center gap-2 mb-1.5">
          <div className="w-3 h-3 rounded-sm" style={{ backgroundColor: color.accent }} />
          <span className="font-mono font-bold text-base" style={{ color: color.accent }}>
            {section.courseCode}
          </span>
          <Badge variant={statusCfg?.variant ?? 'outline'} className="text-[10px]">
            {statusCfg?.label ?? section.status}
          </Badge>
        </div>
        <p className="text-sm font-medium text-slate-800">{section.courseName}</p>
        <p className="text-xs text-slate-500">Şube {section.sectionIndex}</p>
      </div>

      <div className="space-y-2 text-sm">
        <div className="flex items-center gap-2">
          <Users className="h-3.5 w-3.5 text-slate-400" />
          <span className="text-slate-500">Bölüm:</span>
          <span className="text-slate-700 font-medium ml-auto">{section.departmentCode}</span>
        </div>
        <div className="flex items-center gap-2">
          <Clock className="h-3.5 w-3.5 text-slate-400" />
          <span className="text-slate-500">Yüklenme:</span>
          <Badge variant="outline" className="text-[10px] ml-auto">{section.theoryHours}T+{section.labHours}L</Badge>
        </div>
        <div className="flex items-center gap-2">
          <Users className="h-3.5 w-3.5 text-slate-400" />
          <span className="text-slate-500">Kapasite:</span>
          <span className="text-slate-700 font-medium ml-auto">{section.capacity}</span>
        </div>
        {showSectionRoom && (
          <div className="flex items-center gap-2">
            <MapPin className="h-3.5 w-3.5 text-slate-400" />
            <span className="text-slate-500">Derslik:</span>
            <span className="text-slate-700 font-medium ml-auto">{section.classroomName ?? '—'}</span>
          </div>
        )}
        <div className="flex items-center gap-2">
          <User className="h-3.5 w-3.5 text-slate-400" />
          <span className="text-slate-500">Hoca:</span>
          <span className="text-slate-700 font-medium ml-auto max-w-[160px] truncate">
            {section.instructorName ?? '—'}
          </span>
        </div>
        {blocks.length > 0 && (
          <div className="pt-1 border-t border-slate-100">
            <span className="text-slate-500 text-xs">Program</span>
            <ul className="mt-1 space-y-1">
              {blocks.map((b, i) => (
                <li key={`${b.day}-${b.startTime}-${i}`} className="flex items-center gap-2 text-xs">
                  <BlockTypeBadge blockType={b.blockType} />
                  <span className="font-mono text-slate-700">
                    {dayNumberToTurkish(b.day).slice(0, 3)} {b.startTime.slice(0, 5)}–{b.endTime.slice(0, 5)}
                  </span>
                  {!showSectionRoom && b.classroomName && (
                    <span className="ml-auto text-slate-500 truncate max-w-[140px]">{b.classroomName}</span>
                  )}
                </li>
              ))}
            </ul>
          </div>
        )}
      </div>
    </div>
  );
}

/* ── Tek ders bloğu ── */
type ContinuationPosition = 'standalone' | 'first' | 'middle' | 'last';

function SectionBlock({
  section, block, rowSpan, isContinuation, continuationPosition, editable, dimmed, onSectionClick,
  isSelected, onToggleSelect, cardDensity, hoveredSectionId, onHoverSection, conflictMessages,
}: {
  section: FacultyPlanningSection;
  /** The specific schedule block (theory or lab) this card represents in the grid. */
  block: ParsedBlock;
  rowSpan: number;
  isContinuation: boolean;
  /** İlk/orta/son/standalone — bağlı blok hissi için border-radius koşullu. */
  continuationPosition: ContinuationPosition;
  /** editable=true iken kart sürüklenebilir hale gelir. Pinli/devam-bloğu kartlar sürüklenemez. */
  editable: boolean;
  /** Conflict-only mod'da dışarıdaki kartları silikleştir. */
  dimmed?: boolean;
  /** Karta tıklanınca tetiklenir; varsa Popover yerine bu çağrılır (taşı dialog gibi). */
  onSectionClick?: (section: FacultyPlanningSection) => void;
  /** Multi-select modunda kart seçili mi (mavi ring). */
  isSelected?: boolean;
  /** Multi-select aktifken tıklanınca seçim toggle; popover atlanır. */
  onToggleSelect?: (sectionId: number) => void;
  cardDensity: CardDensity;
  /** Group highlight: parent CalendarGrid'in tuttuğu, şu anda hover'da olan section'ın id'si (yoksa null). */
  hoveredSectionId: number | null;
  /** Hover state'i değiştirmek için parent callback. mouseEnter ile section.sectionId, mouseLeave ile null çağrılır. */
  onHoverSection: (sectionId: number | null) => void;
  /** Section conflict halindeyse backend'den gelen ihlal mesajları. Tooltip'te gösterilir.
   *  Boş veya undefined ise tooltip yok. Mesajlar DB-driven (section/derslik/hoca/saat ile). */
  conflictMessages?: string[];
}) {
  const color = getBlockColor(section.suggestedSemester, section.status);
  // isContinuation=true → bu kart bir başka satırdaki "ana" blok'un alt satırı; sürüklenemez (üst kartı taşırsın).
  const isDraggable = editable && !section.isPinned && !isContinuation;
  const dragId = `section-${section.sectionId}-${block.day}-${block.startTime}-${block.blockType}`;
  const { attributes, listeners, setNodeRef, transform, isDragging } = useDraggable({
    id: dragId,
    disabled: !isDraggable,
    data: {
      kind: 'block',
      sectionId: section.sectionId,
      classroomId: block.classroomId ?? section.classroomId,
      blockType: block.blockType,
      currentDay: block.day,
      currentStartTime: block.startTime,
      currentEndTime: block.endTime,
      durationMinutes: timeToMinutes(block.endTime) - timeToMinutes(block.startTime),
    },
  });

  // DragOverlay yerine transform-based drag: kartın kendisi cursor'la birlikte hareket eder.
  // Sebep: dnd-kit DragOverlay + scrollable parent (main overflow-auto) + grid kartı (kendi
  // cell'inin droppable'ı içinde başlıyor) kombinasyonunda `over` her DragMove'da null kalıyordu.
  // Transform style orijinal node'u translate ederek dnd-kit'in collision detection'ını
  // doğru cell rect'leriyle eşleştirilebilir hale getirir.
  const dragTransformStyle = transform
    ? { transform: `translate3d(${transform.x}px, ${transform.y}px, 0)`, zIndex: 50 }
    : undefined;

  // Continuation = üst slotun devamı; noktalı üst border + daha silik
  const continuationClass = isContinuation
    ? 'opacity-60 border-t border-dashed'
    : '';

  // Block-level classroom (mixed dersde teori/lab odası farklı olabilir).
  const cardClassroomName = block.classroomName ?? section.classroomName;
  const instructorLastName = section.instructorName?.split(' ').pop() ?? null;
  const totalHours = section.theoryHours + section.labHours;

  // Density'ye göre border-radius — bağlı blok hissi
  const borderRadiusClass = (() => {
    if (cardDensity === 'spacious') return 'rounded-md';
    switch (continuationPosition) {
      case 'standalone': return 'rounded';
      case 'first':      return 'rounded-t rounded-b-none';
      case 'middle':     return 'rounded-none';
      case 'last':       return 'rounded-b rounded-t-none';
    }
  })();

  const isCompactDensity = cardDensity === 'normal';

  const cardInner = (
    <div
      // ref koşulsuz: React 19'da `ref={cond ? cb : undefined}` callback'i bağlamıyor — bu yüzden
      // useDraggable'ın node.current'i null kalıyordu, dnd-kit aktif kartın rect'ini ölçemiyordu,
      // collisionDetection hiç çağrılmıyor → over=null. useDraggable disabled=true ise drag zaten
      // tetiklenmediği için ref'in her zaman bağlı olması güvenli.
      ref={setNodeRef}
      {...(isDraggable ? attributes : {})}
      {...(isDraggable ? listeners : {})}
      data-section-id={section.sectionId}
      onMouseEnter={isCompactDensity ? () => onHoverSection(section.sectionId) : undefined}
      onMouseLeave={isCompactDensity ? () => onHoverSection(null) : undefined}
      title={
        conflictMessages && conflictMessages.length > 0
          ? `Çakışma sebepleri:\n• ${conflictMessages.join('\n• ')}`
          : undefined
      }
      className={cn(
        'relative transition-all duration-150 ease-out',
        borderRadiusClass,
        // Spacious modunda eski 4px şerit + padding + shadow + hover translate
        cardDensity === 'spacious' && 'border-l-[4px] shadow-sm hover:shadow-lg hover:-translate-y-0.5 hover:border-l-[6px] px-2 py-1',
        // Compact modlarda 3px şerit + kompakt padding + hafif hover shadow
        isCompactDensity && 'border-l-[3px] px-1.5 py-0.5 hover:shadow-sm',
        isDraggable ? 'cursor-grab active:cursor-grabbing' : 'cursor-pointer',
        color.bg, color.border, color.text,
        // isContinuation soluk hâli SADECE spacious modda aktif
        cardDensity === 'spacious' && continuationClass,
        isDragging && 'opacity-40 ring-2 ring-[#003366]',
        dimmed && 'opacity-25 grayscale',
        isSelected && 'ring-2 ring-[#003366] ring-offset-1',
        // Group highlight: aynı section hover'da → ring; başka section hover'da → dim
        isCompactDensity && hoveredSectionId === section.sectionId && 'ring-1 ring-[#003366] z-10',
        isCompactDensity && hoveredSectionId !== null && hoveredSectionId !== section.sectionId && 'opacity-60',
      )}
      style={{ borderLeftColor: color.accent, ...dragTransformStyle }}
      onClick={e => {
        e.stopPropagation();
        if (onToggleSelect) {
          onToggleSelect(section.sectionId);
          return;
        }
        if (onSectionClick) onSectionClick(section);
      }}
    >
      {/* Draggable ipucu — yalnız spacious modda görünür (kompakt modlarda yatay yer tasarrufu) */}
      {isDraggable && cardDensity === 'spacious' && (
        <span
          className="absolute top-0.5 right-0.5 p-0.5 rounded text-slate-500/40 pointer-events-none"
          aria-hidden="true"
        >
          <GripVertical className="h-3 w-3" />
        </span>
      )}
      {editable && section.isPinned && cardDensity === 'spacious' && (
        <Lock className="absolute top-0.5 right-0.5 h-3 w-3 text-amber-500" aria-label="Sabitlenmiş" />
      )}

      {cardDensity === 'spacious' ? (
        // ── SPACIOUS (düzenleyici — çok satırlı kart, mevcut davranış aynen korunur) ──
        isContinuation ? (
          <div className="flex items-center gap-1.5">
            <span className="font-mono font-bold text-sm">{section.courseCode}</span>
            <span className="text-xs font-mono font-bold">·{section.sectionIndex}</span>
            <BlockTypeBadge blockType={block.blockType} className="ml-auto" />
          </div>
        ) : (
          <>
            <div className="flex items-baseline gap-1.5 leading-tight">
              <span className="font-mono font-bold text-base tracking-tight">{section.courseCode}</span>
              <span className="text-xs font-mono font-bold">·{section.sectionIndex}</span>
              <BlockTypeBadge blockType={block.blockType} className="ml-auto self-center" />
            </div>
            {rowSpan >= 2 && (
              <div className="text-xs font-semibold leading-snug mt-0.5 truncate">
                {section.courseName}
              </div>
            )}
            {rowSpan >= 2 && (cardClassroomName || section.instructorName) && (
              <div className="flex items-center gap-2 mt-0.5 text-xs font-semibold">
                {cardClassroomName && (
                  <span className="flex items-center gap-1">
                    <MapPin className="h-3 w-3 shrink-0" />
                    <span className="truncate">{cardClassroomName}</span>
                  </span>
                )}
                {section.instructorName && (
                  <span className="flex items-center gap-1 truncate">
                    <User className="h-3 w-3 shrink-0" />
                    <span className="truncate">{instructorLastName}</span>
                  </span>
                )}
              </div>
            )}
            {rowSpan >= 3 && (
              <div className="flex items-center gap-1 mt-0.5 text-xs font-semibold">
                <Users className="h-3 w-3 shrink-0" />
                <span>{section.capacity} kişi</span>
              </div>
            )}
          </>
        )
      ) : (
        // ── NORMAL (salt-okunur — 36px iki satır kompakt) ──
        <div className="flex flex-col gap-0.5 h-[32px] overflow-hidden">
          <div className="flex items-center gap-1.5">
            <span className="font-mono font-bold text-[11px] tabular-nums whitespace-nowrap shrink-0">
              {section.courseCode}·{section.sectionIndex}
            </span>
            <BlockTypeBadge blockType={block.blockType} className="shrink-0" />
            {cardClassroomName && (
              <span className="text-[10px] text-slate-700 font-medium ml-auto whitespace-nowrap shrink-0">
                {cardClassroomName}
              </span>
            )}
          </div>
          <div className="flex items-center gap-1 text-[10px] text-slate-600">
            {section.instructorName && (
              <span className="truncate min-w-0">{section.instructorName}</span>
            )}
            {totalHours > 0 && (
              <span className="text-slate-400 shrink-0 ml-auto">{section.theoryHours}T+{section.labHours}L</span>
            )}
          </div>
        </div>
      )}
    </div>
  );

  // Tooltip içeriği: tam ders bilgisi (kompakt modlarda kritik — derslik+hoca gizli)
  const tooltipContent = (
    <div className="text-xs space-y-1 max-w-xs">
      <p className="font-semibold text-slate-800">{section.courseName}</p>
      <p className="text-slate-600">
        <span className="font-mono font-bold">{section.courseCode}</span>·{section.sectionIndex}
        {' · '}
        <span>{section.departmentCode}</span>
      </p>
      {section.instructorName && (
        <p className="text-slate-600">Hoca: {section.instructorName}</p>
      )}
      {cardClassroomName && (
        <p className="text-slate-600">Derslik: {cardClassroomName}</p>
      )}
      <p className="text-slate-500 text-[10px]">
        {section.theoryHours}T + {section.labHours}L · {section.capacity} kişi
      </p>
    </div>
  );

  // Salt-okunur + spacious modda Popover detay taşır; tooltip gereksiz.
  // Kompakt modda tooltip hover ile detayı verir.
  const wrapWithTooltip = (child: React.ReactElement): React.ReactElement => {
    if (cardDensity === 'spacious') return child;
    return (
      <Tooltip>
        <TooltipTrigger asChild>{child}</TooltipTrigger>
        <TooltipContent side="right" align="start" className="bg-white border border-slate-200 shadow-lg text-slate-800">
          {tooltipContent}
        </TooltipContent>
      </Tooltip>
    );
  };

  // editable iken hem Popover hem Tooltip devre dışı: Radix `asChild` ref composition
  // useDraggable'ın setNodeRef'iyle çakışıp dnd-kit'in `activeNodeRect` ölçümünü null bırakıyordu
  // (kanıt: useDndMonitor → initialRect=null her DragMove'da, dolayısıyla Status.Initialized'a
  // geçilemiyor → collisionDetection hiç çağrılmıyor → over=null). Düzenleyici drag-drop için
  // kurulduğundan tooltip/popover gereksiz; kart yüzeyi zaten ders kodu/derslik/hoca veriyor.
  if (editable) {
    return cardInner;
  }
  if (onSectionClick || onToggleSelect) {
    return wrapWithTooltip(cardInner);
  }

  return (
    <Popover>
      <PopoverTrigger asChild>{wrapWithTooltip(cardInner)}</PopoverTrigger>
      <PopoverContent className="w-80" side="right" align="start">
        <SectionPopoverContent
          section={section}
          color={color}
        />
      </PopoverContent>
    </Popover>
  );
}

/* ── Ana CalendarGrid ── */
export type CardDensity = 'normal' | 'spacious';

/**
 * Hücre içi kartların yerleşim sınıfı.
 *
 * Her iki modda da dikey stack: kart sayısı ne olursa olsun her kart hücrenin tam
 * genişliğini alır. 2+ kartlı hücrede satır içeriğe göre otomatik uzar (CalendarCell'in
 * içerik katmanı doğal akışta — `<td>` doğal şekilde büyür). Bu, normal modda eski
 * `grid grid-cols-2`'nin yarattığı yatay sıkışmayı (truncate, derslik+hoca kesilmesi)
 * tamamen ortadan kaldırır.
 */
function getCellLayoutClass(_density: CardDensity, _blockCount: number): string {
  return 'flex flex-col gap-0.5';
}

export interface CalendarGridProps {
  sections: FacultyPlanningSection[];
  timeSlots: FacultyPlanningTimeSlot[];
  onEmptyCellClick?: (
    day: number,
    slotOrder: number,
    startTime: string,
    endTime: string,
    allowedMode: FacultyPlanningTimeSlot['allowedMode'],
  ) => void;
  /** Sürükle-bırak için aktif edilir; her atanmış blok draggable, her boş hücre droppable olur.
   *  Parent'ta DndContext kurulmalı, drop akışı için handleDragEnd implement edilmelidir. */
  editable?: boolean;
  /** Sürükleme esnasında kırmızı vurgulanacak hücrelerin dropId set'i. Parent hesaplar.
   *  dropId formatı: "cell-{day}-{slotOrder}". */
  invalidDropIds?: Set<string>;
  /** Geçersiz hücrelerin backend gerekçe haritası (`dropId` → mesaj). Tooltip için kullanılır.
   *  Parent backend bulk-validate cevabından doldurur. */
  invalidDropReasons?: Map<string, string>;
  /** Section bazında aktif conflict mesajları (sectionId → backend RuleEngine mesajları).
   *  Kart kırmızıyken üstüne fareyle gelince tooltip'te neden çakışmalı olduğunu gösterir.
   *  Parent useFacultyPlanningConflicts hook'undan toplar. */
  sectionConflicts?: Map<number, string[]>;
  /** Smart suggestion — drag esnasında en uygun 3 hücre, parıltılı highlight ile. */
  suggestedDropIds?: Set<string>;
  /** Conflict-only modu — yalnız çakışmalı/atanmamış section'ları göster, diğerleri silikleştir. */
  highlightedSectionIds?: Set<number>;
  /** Bir section'a tıklayınca tetiklenir (kart popover yerine harici aksiyon — ör. taşı dialog). */
  onSectionClick?: (section: FacultyPlanningSection) => void;
  /** Hücrede gösterilecek maksimum kart sayısı; üzeri "+N daha" rozetiyle popover'a katlanır. */
  maxBlocksPerCell?: number;
  /** Multi-select mod aktif (Set varsa); seçili kartların id'leri burada. Card click toggle yapar. */
  selectedSectionIds?: Set<number>;
  /** Multi-select aktifken karta tıklanınca tetiklenir (toggle). */
  onSectionToggleSelect?: (sectionId: number) => void;
  /**
   * Kart yoğunluk modu. Salt-okunur sayfa (AutoScheduleTab) `'normal'` (36px iki satır) geçer;
   * düzenleyici prop geçmez → `'spacious'` (çok-satır kart, sürükle-bırak hissi) aktif kalır.
   * @default 'spacious'
   */
  cardDensity?: CardDensity;
}

export function CalendarGrid({
  sections, timeSlots, onEmptyCellClick, editable = false,
  invalidDropIds, invalidDropReasons, sectionConflicts, suggestedDropIds, highlightedSectionIds, onSectionClick, maxBlocksPerCell,
  selectedSectionIds, onSectionToggleSelect,
  cardDensity = 'spacious',
}: CalendarGridProps) {
  // Group highlight: kompakt modlarda aynı sectionId'ye sahip tüm satırları birlikte vurgulamak için
  const [hoveredSectionId, setHoveredSectionId] = useState<number | null>(null);

  /* Görünür günler — veride slot tanımlı olan günleri sırala */
  const visibleDays = useMemo(() => {
    const groups = new Map<number, FacultyPlanningTimeSlot[]>();
    for (const slot of timeSlots) {
      const arr = groups.get(slot.day) ?? [];
      arr.push(slot);
      groups.set(slot.day, arr);
    }
    return Array.from(groups.entries())
      .map(([day, slots]) => ({
        day,
        label: dayNumberToTurkish(day),
        isAllOnline: slots.length > 0 && slots.every(s => s.allowedMode === 'OnlineOnly'),
      }))
      .sort((a, b) => weekOrderKey(a.day) - weekOrderKey(b.day));
  }, [timeSlots]);

  /* (day, startTime) → slot haritası — her hücre kendi AllowedMode'unu bilsin */
  const slotLookup = useMemo(() => {
    const map = new Map<string, FacultyPlanningTimeSlot>();
    for (const slot of timeSlots) {
      map.set(`${slot.day}-${slot.startTime.slice(0, 5)}`, slot);
    }
    return map;
  }, [timeSlots]);

  /* Zaman satırları */
  const timeRows = useMemo(() => {
    const seen = new Map<number, { slotOrder: number; startTime: string; endTime: string }>();
    for (const slot of timeSlots) {
      if (!seen.has(slot.slotOrder)) {
        seen.set(slot.slotOrder, { slotOrder: slot.slotOrder, startTime: slot.startTime, endTime: slot.endTime });
      }
    }
    return Array.from(seen.entries())
      .sort(([a], [b]) => a - b)
      .map(([, v]) => v);
  }, [timeSlots]);

  // Density'ye göre satır yüksekliği (px). Spacious mevcut 44px'i korur; salt-okunur 'normal' 36px iki satır kart.
  const rowHeightPx = cardDensity === 'spacious' ? 44 : 36;

  /* Blokları parse et */
  const allBlocks = useMemo(() => {
    return sections
      .filter(s => s.status !== 'Unscheduled')
      .flatMap(s => parseSectionSchedule(s));
  }, [sections]);

  /* Hücre → blok haritası */
  const blockMap = useMemo(() => {
    const map = new Map<string, ParsedBlock[]>();
    for (const block of allBlocks) {
      for (const row of timeRows) {
        const rowStart = timeToMinutes(row.startTime.slice(0, 5));
        const rowEnd = timeToMinutes(row.endTime.slice(0, 5));
        const blockStart = timeToMinutes(block.startTime);
        const blockEnd = timeToMinutes(block.endTime);
        if (blockStart < rowEnd && blockEnd > rowStart) {
          const key = `${block.day}-${row.startTime}`;
          const arr = map.get(key) ?? [];
          // De-dup by (sectionId + blockType + startTime) so a mixed section with theory and
          // lab blocks both appearing in the same row (rare) still renders as two cards.
          const dedupKey = `${block.sectionId}-${block.blockType}-${block.startTime}`;
          if (!arr.some(b => `${b.sectionId}-${b.blockType}-${b.startTime}` === dedupKey)) {
            arr.push(block);
          }
          map.set(key, arr);
        }
      }
    }
    return map;
  }, [allBlocks, timeRows]);

  /* rowSpan hesapla */
  const blockStartCells = useMemo(() => {
    const starts = new Map<string, { block: ParsedBlock; rowSpan: number }>();
    const covered = new Set<string>();
    const lastRowKeys = new Set<string>();

    for (const block of allBlocks) {
      const spanRows: number[] = [];
      for (let i = 0; i < timeRows.length; i++) {
        const row = timeRows[i];
        const rowStart = timeToMinutes(row.startTime.slice(0, 5));
        const rowEnd = timeToMinutes(row.endTime.slice(0, 5));
        const blockStart = timeToMinutes(block.startTime);
        const blockEnd = timeToMinutes(block.endTime);
        if (blockStart < rowEnd && blockEnd > rowStart) {
          spanRows.push(i);
        }
      }
      if (spanRows.length > 0) {
        const firstRow = timeRows[spanRows[0]];
        const lastRowIdx = spanRows[spanRows.length - 1];
        // Include startTime + blockType so the same section's theory and lab blocks track
        // their own rowSpan when both fall on the same day at different times.
        const startKey = `${block.sectionId}-${block.day}-${firstRow.startTime}-${block.blockType}`;
        if (!starts.has(startKey)) {
          starts.set(startKey, { block, rowSpan: spanRows.length });
          for (let k = 1; k < spanRows.length; k++) {
            covered.add(`${block.sectionId}-${block.day}-${timeRows[spanRows[k]].startTime}-${block.blockType}`);
          }
          lastRowKeys.add(`${block.sectionId}-${block.day}-${timeRows[lastRowIdx].startTime}-${block.blockType}`);
        }
      }
    }
    return { starts, covered, lastRowKeys };
  }, [allBlocks, timeRows]);

  /* Öğle arası: config'ten oku, uygun satırlar arasına ayırıcı bas */
  const settings = useScheduleSettings();
  const lunchBreakIdx = useMemo(() => {
    if (settings.isLoading) return -1;
    const lunchStartMin = timeToMinutes(settings.lunchBreakStart);
    // Öğle arasından HEMEN ÖNCE biten son sabah satırını bul → onun sonrasına ayırıcı
    let idx = -1;
    for (let i = 0; i < timeRows.length; i++) {
      const end = timeToMinutes(timeRows[i].endTime.slice(0, 5));
      if (end <= lunchStartMin) idx = i;
    }
    // Ayırıcı, idx+1 satırından önce basılır. Ama öğle sonrası satırı yoksa basma.
    if (idx < 0 || idx >= timeRows.length - 1) return -1;
    return idx + 1;
  }, [timeRows, settings.lunchBreakStart, settings.isLoading]);

  const totalColSpan = 1 + visibleDays.length;

  if (timeRows.length === 0 || visibleDays.length === 0) {
    return (
      <div className="h-full border border-slate-200 rounded-xl flex items-center justify-center bg-slate-50">
        <p className="text-base text-slate-400">Zaman dilimi bilgisi bulunamadı</p>
      </div>
    );
  }

  const isEmpty = allBlocks.length === 0;

  return (
    // overflow-auto burada YOK: iç içe scroll dnd-kit measuring'i karıştırır.
    // min-h-full: parent kısa olduğunda esner; içerik uzunsa natural flow'da büyür ve
    // parent'ın overflow-auto'su scroll verir (single-layer scroll architecture).
    <div className="min-h-full border border-slate-200 rounded-xl bg-white shadow-sm relative flex flex-col">
      {isEmpty && (
        <div className="sticky top-0 z-20 flex items-center justify-center gap-2 bg-slate-50/90 backdrop-blur-sm border-b border-slate-200/60 py-1.5 text-xs text-slate-400">
          <span>Henüz atama yok</span>
          <span className="text-slate-300">—</span>
          <span>Hücreye tıklayın veya <span className="font-medium text-slate-500">Otomatik Programla</span> kullanın</span>
        </div>
      )}
      {/* İç scroll YOK — natural flow. Tablo içerik kadar büyür, dış container (main veya
          tab parent) overflow-auto verir. */}
      <div>
      <table className="w-full border-collapse" style={{ tableLayout: 'fixed' }}>
        <colgroup>
          <col style={{ width: 72 }} />
          {visibleDays.map(wd => <col key={wd.day} />)}
        </colgroup>
        <thead className="sticky top-0 z-10">
          <tr className="bg-slate-50/95 backdrop-blur-sm">
            <th className="border-r-[3px] border-r-slate-300 border-b border-b-slate-200 p-2 text-center font-semibold text-slate-500 text-xs uppercase tracking-wider">
              Saat
            </th>
            {visibleDays.map(wd => (
              <th
                key={wd.day}
                className={cn(
                  'border-r-2 border-r-slate-300 border-b border-b-slate-200 p-2 text-center',
                  wd.isAllOnline && 'bg-slate-100/80',
                )}
              >
                <div className={cn(
                  'font-semibold text-sm',
                  wd.isAllOnline ? 'text-slate-500' : 'text-slate-700',
                )}>
                  {wd.label}
                </div>
                {wd.isAllOnline && (
                  <div className="text-[9px] text-slate-400 font-normal mt-0.5">Online</div>
                )}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {timeRows.map((row, rowIdx) => {
            const showLunchSeparator = rowIdx === lunchBreakIdx;

            return (
              <Fragment key={row.startTime}>
                {showLunchSeparator && (
                  <tr>
                    <td colSpan={totalColSpan} className="bg-amber-50/60 border-y border-slate-300 py-0.5 px-3">
                      <div className="flex items-center gap-2 text-[11px] text-amber-700 font-medium">
                        <Coffee className="h-3 w-3" />
                        <span>Öğle Arası — {settings.lunchBreakStart}–{settings.lunchBreakEnd}</span>
                        <div className="flex-1 h-px bg-amber-200" />
                      </div>
                    </td>
                  </tr>
                )}
                <tr>
                  {/* Saat etiketi */}
                  <td className="border-r-[3px] border-r-slate-300 border-b border-b-slate-300 px-1 py-0.5 text-center align-top bg-white">
                    <span className="font-mono font-semibold text-[12px] text-slate-600">
                      {row.startTime.slice(0, 5)}
                    </span>
                    <div className="font-mono text-[9px] text-slate-300 leading-none">{row.endTime.slice(0, 5)}</div>
                  </td>

                  {/* Gün hücreleri */}
                  {visibleDays.map(wd => {
                    const cellKey = `${wd.day}-${row.startTime}`;
                    const cellBlocks = blockMap.get(cellKey) ?? [];
                    const slot = slotLookup.get(`${wd.day}-${row.startTime.slice(0, 5)}`);
                    const isOnlineOnly = slot?.allowedMode === 'OnlineOnly';
                    const slotMissing = slot === undefined;

                    // OnlineOnly slot da tıklanabilir — sheet TeachingMethod-aware filter yapar.
                    // Yalnızca slotMissing veya zaten dolu hücreler tıklanamaz.
                    const isClickableEmpty =
                      cellBlocks.length === 0 &&
                      !slotMissing &&
                      onEmptyCellClick != null;

                    // Drop yalnız boş hücrelere ve geçerli slot'lara izinli; OnlineOnly veya slot eksik hücreler hâlâ droppable ama
                      //   isDropDisabled true ile kırmızı vurgulama yapar (kullanıcı kuralı görsün).
                      const dropId = `cell-${wd.day}-${row.slotOrder}`;
                      const dropDisabled = invalidDropIds?.has(dropId) ?? false;
                      const isSuggested = (suggestedDropIds?.has(dropId) ?? false) && !dropDisabled;
                      const invalidReason = invalidDropReasons?.get(dropId);

                    return (
                      <CalendarCell
                        key={cellKey}
                        isRestricted={isOnlineOnly || slotMissing}
                        dropId={dropId}
                        isDropDisabled={dropDisabled}
                        isSuggested={isSuggested}
                        rowHeightPx={rowHeightPx}
                        invalidReason={invalidReason}
                      >
                        <div
                          className={cn(
                            getCellLayoutClass(cardDensity, cellBlocks.length),
                            cellBlocks.length > 0 && cardDensity === 'spacious' && 'min-h-[2.5rem]',
                            isClickableEmpty && 'cursor-pointer hover:bg-blue-50/50 rounded transition-colors group min-h-[2.5rem] items-center justify-center',
                          )}
                          onClick={isClickableEmpty ? () => onEmptyCellClick(wd.day, row.slotOrder, row.startTime, row.endTime, slot!.allowedMode) : undefined}
                        >
                          {(() => {
                            const isOverflow = maxBlocksPerCell != null && cellBlocks.length > maxBlocksPerCell;
                            const visibleBlocks = isOverflow ? cellBlocks.slice(0, maxBlocksPerCell - 1) : cellBlocks;
                            const hiddenBlocks = isOverflow ? cellBlocks.slice(maxBlocksPerCell - 1) : [];
                            return (
                              <>
                                {visibleBlocks.map(b => {
                                  const startKey = `${b.sectionId}-${b.day}-${row.startTime}-${b.blockType}`;
                                  const isContinuation = blockStartCells.covered.has(startKey);
                                  const isLastRow = blockStartCells.lastRowKeys.has(startKey);
                                  const startInfo = blockStartCells.starts.get(startKey);
                                  const blockRowSpan = startInfo?.rowSpan ?? 1;
                                  const isDimmed = highlightedSectionIds != null
                                    && !highlightedSectionIds.has(b.sectionId);

                                  // ContinuationPosition: continuation satırlarda startInfo yok (blockRowSpan=1),
                                  // bu yüzden önce covered/lastRow set'lerini kontrol et — bunlar multi-row blokların
                                  // alt satırlarını güvenilir şekilde işaretler.
                                  let continuationPosition: ContinuationPosition;
                                  if (isContinuation) {
                                    continuationPosition = isLastRow ? 'last' : 'middle';
                                  } else if (blockRowSpan > 1) {
                                    // Multi-row blok ilk satırı
                                    continuationPosition = 'first';
                                  } else {
                                    // Tek satır blok
                                    continuationPosition = 'standalone';
                                  }

                                  return (
                                    <SectionBlock
                                      key={`${b.sectionId}-${b.blockType}-${b.startTime}`}
                                      section={b.section}
                                      block={b}
                                      rowSpan={blockRowSpan}
                                      isContinuation={isContinuation}
                                      continuationPosition={continuationPosition}
                                      editable={editable}
                                      dimmed={isDimmed}
                                      onSectionClick={onSectionClick}
                                      isSelected={selectedSectionIds?.has(b.sectionId)}
                                      onToggleSelect={onSectionToggleSelect}
                                      cardDensity={cardDensity}
                                      hoveredSectionId={hoveredSectionId}
                                      onHoverSection={setHoveredSectionId}
                                      conflictMessages={sectionConflicts?.get(b.sectionId)}
                                    />
                                  );
                                })}
                                {hiddenBlocks.length > 0 && (
                                  <OverflowPill blocks={hiddenBlocks} highlightedSectionIds={highlightedSectionIds} />
                                )}
                              </>
                            );
                          })()}
                          {isClickableEmpty && (
                            <Plus className="h-3.5 w-3.5 text-blue-400 opacity-0 group-hover:opacity-100 transition-opacity" />
                          )}
                        </div>
                      </CalendarCell>
                    );
                  })}
                </tr>
              </Fragment>
            );
          })}
        </tbody>
      </table>
      </div>
    </div>
  );
}

/** Hücre kapasitesi aşıldığında "+N daha" rozet — tıklayınca Popover ile geri kalan kartlar listelenir. */
function OverflowPill({
  blocks, highlightedSectionIds,
}: {
  blocks: ParsedBlock[];
  highlightedSectionIds?: Set<number>;
}) {
  return (
    <Popover>
      <PopoverTrigger asChild>
        <button
          type="button"
          onClick={e => e.stopPropagation()}
          className="text-[10px] font-medium text-slate-700 bg-slate-100 hover:bg-slate-200 rounded px-1.5 py-0.5 border border-slate-300"
          aria-label={`${blocks.length} daha`}
        >
          +{blocks.length} daha
        </button>
      </PopoverTrigger>
      <PopoverContent className="w-80 p-2" side="right" align="start">
        <p className="text-[10px] font-semibold uppercase tracking-wider text-slate-500 mb-2 px-1">
          Bu hücrede {blocks.length} ders daha
        </p>
        <div className="space-y-1 max-h-72 overflow-y-auto">
          {blocks.map(b => {
            const color = getBlockColor(b.section.suggestedSemester, b.section.status);
            const isDimmed = highlightedSectionIds != null && !highlightedSectionIds.has(b.sectionId);
            return (
              <div
                key={`${b.sectionId}-${b.blockType}-${b.startTime}`}
                className={cn(
                  'rounded-md border-l-[3px] px-2 py-1.5 text-xs',
                  color.bg, color.border, color.text,
                  isDimmed && 'opacity-40',
                )}
                style={{ borderLeftColor: color.accent }}
              >
                <div className="flex items-center gap-1.5">
                  <span className="font-mono font-bold">{b.section.courseCode}</span>
                  <span className="text-[10px]">·{b.section.sectionIndex}</span>
                  <span className="ml-auto text-[10px] text-slate-500 truncate max-w-[120px]">
                    {b.classroomName ?? '—'}
                  </span>
                </div>
                <p className="truncate text-[11px] mt-0.5">{b.section.courseName}</p>
                {b.section.instructorName && (
                  <p className="truncate text-[10px] text-slate-500 mt-0.5">
                    {b.section.instructorName}
                  </p>
                )}
              </div>
            );
          })}
        </div>
      </PopoverContent>
    </Popover>
  );
}
