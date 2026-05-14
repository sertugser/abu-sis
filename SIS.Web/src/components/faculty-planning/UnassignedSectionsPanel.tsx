import { useEffect, useMemo, useRef, useState } from 'react';
import { useDraggable, useDroppable } from '@dnd-kit/core';
import { ChevronDown, ChevronUp, GripVertical, Inbox, Search, Trash2, Users } from 'lucide-react';
import { cn } from '@/lib/utils';
import { Input } from '@/components/ui/input';
import type { FacultyPlanningSection } from '../../types/facultyPlanning';

export const UNASSIGNED_POOL_DROP_ID = 'unassigned-pool';
export const POOL_DRAG_ID_PREFIX = 'pool-section-';

/** Pool kartı için varsayılan slot süresi (dk) — Türkiye'de standart 50; gerçek değer parent'tan geçer. */
const DEFAULT_SLOT_MINUTES = 50;

interface UnassignedSectionsPanelProps {
  /** Tüm fakülte section'ları — panel kendisi 'Unscheduled' filtresi uygular. */
  sections: FacultyPlanningSection[];
  /** Mevcut sürükleme aktif mi — havuza drop hint için. */
  isDragging: boolean;
  /** Sürüklenen kart pool'da drop edilebilir mi (yalnız grid'den sürükleme + section atanmış olmalı). */
  canAcceptDrop: boolean;
  /** Gerçek slot süresi (dk) — pool drag için durationMinutes hesabı. Yoksa 50 dk varsayılır. */
  slotMinutes?: number;
}

/**
 * Sol panel — atanmamış section'ların listesi + grid kartlarının "kaldır" hedefi.
 *
 * - 'Unscheduled' statüsündeki section'lar burada listelenir.
 * - Search ile hızlı filtre (ders kodu / ad / hoca).
 * - Grid kartını bu panele bırakırsanız → unassign tetiklenir (handler parent'ta).
 *
 * Faz 4 sürümü: panel kartları sürüklenmez (yalnız gösterim + drop target).
 * Pool'dan grid'e atama için boş hücreye tıklayıp SlotAssignDialog kullanılır.
 */
export function UnassignedSectionsPanel({
  sections, isDragging, canAcceptDrop, slotMinutes = DEFAULT_SLOT_MINUTES,
}: UnassignedSectionsPanelProps) {
  const [search, setSearch] = useState('');
  const [collapsed, setCollapsed] = useState(false);
  // KRİTİK: disabled drag state'ine bağlanmaz — drag start'ta dnd-kit ilk ölçümünü yaparken
  // React state batched update ile gecikme yaratıyor; havuz "kayıtsız" kalıp drop hedefi olarak
  // bulunamıyordu. canAcceptDrop yalnız görsel feedback için; drop akışı handleDragEnd'de
  // pinli/atanmamış kontrolü ile yönetilir.
  const { setNodeRef, isOver } = useDroppable({
    id: UNASSIGNED_POOL_DROP_ID,
  });

  const unscheduled = useMemo(
    () => sections.filter(s => s.status === 'Unscheduled'),
    [sections],
  );

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    if (!q) return unscheduled;
    return unscheduled.filter(s =>
      s.courseCode.toLowerCase().includes(q)
      || s.courseName.toLowerCase().includes(q)
      || (s.instructorName?.toLowerCase().includes(q) ?? false)
      || s.departmentCode.toLowerCase().includes(q),
    );
  }, [unscheduled, search]);

  const showAcceptHint = isDragging && canAcceptDrop;

  // <aside> doğrudan ref'lenir — flex-item context'inde getBoundingClientRect doğru ölçer.
  const asideRef = useRef<HTMLElement | null>(null);
  // Debug: drag aktif olduğunda pool rect'i ne, isOver ne — kullanıcı drop yapınca ne döndüğünü görelim
  useEffect(() => {
    if (!isDragging || !asideRef.current) return;
    const r = asideRef.current.getBoundingClientRect();
    // eslint-disable-next-line no-console
    console.log('[Pool] drag aktif, aside rect:', `${r.left.toFixed(0)},${r.top.toFixed(0)} ${r.width.toFixed(0)}x${r.height.toFixed(0)}`, 'isOver:', isOver);
  }, [isDragging, isOver]);
  return (
    <aside
      ref={(node) => {
        setNodeRef(node);
        asideRef.current = node;
      }}
      className={cn(
        'shrink-0 bg-white border-r border-slate-200 flex flex-col transition-colors',
        collapsed ? 'w-12' : 'w-72',
        isOver && canAcceptDrop && 'bg-red-50/60 ring-2 ring-red-300 ring-inset',
        showAcceptHint && !isOver && 'bg-amber-50/40',
      )}
    >
      {/* Header */}
      <div className="px-3 py-2.5 border-b border-slate-100 flex items-center gap-2">
        <button
          type="button"
          onClick={() => setCollapsed(prev => !prev)}
          className="p-1 rounded hover:bg-slate-100 text-slate-500"
          aria-label={collapsed ? 'Aç' : 'Daralt'}
        >
          {collapsed ? <ChevronDown className="h-3.5 w-3.5" /> : <ChevronUp className="h-3.5 w-3.5" />}
        </button>
        {!collapsed && (
          <>
            <Inbox className="h-3.5 w-3.5 text-slate-400" />
            <h2 className="text-xs font-semibold uppercase tracking-wider text-slate-600 flex-1">
              Atanmamış
            </h2>
            <span className="text-[10px] tabular-nums bg-slate-100 text-slate-600 rounded px-1.5 py-0.5 font-medium">
              {filtered.length}{search ? `/${unscheduled.length}` : ''}
            </span>
          </>
        )}
      </div>

      {collapsed ? (
        <div className="flex-1 flex items-start justify-center pt-3">
          <span className="text-[10px] font-bold tabular-nums text-slate-500 rotate-180" style={{ writingMode: 'vertical-rl' }}>
            {unscheduled.length} adet
          </span>
        </div>
      ) : (
        <>
          {/* Search */}
          <div className="px-3 py-2 border-b border-slate-100">
            <div className="relative">
              <Search className="absolute left-2 top-1/2 -translate-y-1/2 h-3 w-3 text-slate-400" />
              <Input
                placeholder="Ders, hoca, bölüm..."
                value={search}
                onChange={e => setSearch(e.target.value)}
                className="pl-7 h-7 text-xs border-slate-200"
              />
            </div>
          </div>

          {/* Drop hint */}
          {showAcceptHint && (
            <div className="px-3 py-2 bg-red-50/80 border-b border-red-200">
              <p className="text-[11px] text-red-700 flex items-center gap-1.5 font-medium">
                <Trash2 className="h-3 w-3" />
                Kartı buraya bırakarak atamayı kaldır
              </p>
            </div>
          )}

          {/* List */}
          <div className="flex-1 overflow-y-auto p-2 space-y-1">
            {filtered.length === 0 ? (
              <div className="py-6 text-center">
                <Inbox className="h-6 w-6 text-slate-300 mx-auto mb-1.5" />
                <p className="text-[11px] text-slate-400">
                  {search ? 'Eşleşen şube yok' : 'Tüm şubeler atanmış'}
                </p>
              </div>
            ) : (
              filtered.map(s => (
                <UnassignedSectionCard key={s.sectionId} section={s} slotMinutes={slotMinutes} />
              ))
            )}
          </div>
        </>
      )}
    </aside>
  );
}

/** Kompakt sürüklenebilir kart — atanmamış section bilgisi. Drop sonrası backend `assign` çağrılır. */
function UnassignedSectionCard({ section, slotMinutes }: { section: FacultyPlanningSection; slotMinutes: number }) {
  const totalHours = section.theoryHours + section.labHours;
  const { attributes, listeners, setNodeRef, isDragging } = useDraggable({
    id: `${POOL_DRAG_ID_PREFIX}${section.sectionId}`,
    disabled: section.isPinned,
    data: {
      kind: 'pool',
      sectionId: section.sectionId,
      classroomId: null,
      // KRİTİK: "1 ders saati = 1 slot" varsayımı. theoryHours+labHours kadar slot gerekli.
      // Hardcoded 60 yerine gerçek slotMinutes — yoksa 3T+0L (180dk) sistemde 50dk slot'a 4 slot
      // hesaplanıyor ve "ardışık değil" hatası tüm hücreleri invalid yapıyordu.
      durationMinutes: totalHours * slotMinutes,
    },
  });

  return (
    <div
      ref={setNodeRef}
      {...attributes}
      {...listeners}
      className={cn(
        'relative rounded-md border border-slate-200 bg-white hover:border-slate-300 hover:shadow-sm transition-all px-2.5 py-2',
        section.isPinned ? 'cursor-not-allowed opacity-60' : 'cursor-grab active:cursor-grabbing',
        isDragging && 'opacity-40 ring-2 ring-[#003366]',
      )}
    >
      {/* Görsel sürükle ipucu — listener tüm kartta, bu sadece ikon */}
      {!section.isPinned && (
        <span className="absolute top-1 right-1 text-slate-300 pointer-events-none" aria-hidden="true">
          <GripVertical className="h-3 w-3" />
        </span>
      )}
      <div className="flex items-baseline gap-1.5 min-w-0 pr-4">
        <span className="font-mono font-bold text-xs text-slate-800 shrink-0">
          {section.courseCode}
        </span>
        <span className="text-[10px] text-slate-400 shrink-0">·{section.sectionIndex}</span>
        <span className="text-[10px] text-slate-400 ml-auto bg-slate-50 px-1 py-0.5 rounded shrink-0">
          {section.departmentCode}
        </span>
      </div>
      <p className="text-[11px] text-slate-600 truncate mt-0.5">
        {section.courseName}
      </p>
      <div className="flex items-center gap-2 mt-1 text-[10px] text-slate-400">
        <span className="flex items-center gap-0.5">
          <Users className="h-2.5 w-2.5" />
          {section.capacity}
        </span>
        <span>{section.theoryHours}T+{section.labHours}L</span>
        {totalHours > 0 && <span className="text-slate-300">· {totalHours}sa</span>}
        {section.instructorName && (
          <span className="ml-auto truncate max-w-[100px]">{section.instructorName.split(' ').pop()}</span>
        )}
      </div>
    </div>
  );
}
