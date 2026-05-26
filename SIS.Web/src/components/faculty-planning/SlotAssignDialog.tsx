import { useMemo, useState } from 'react';
import { Search, Users, BookOpen } from 'lucide-react';
import { Dialog } from '@/components/ui/dialog';
import ResizableDialogContent from '@/components/ui/resizable-dialog';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { ScrollArea } from '@/components/ui/scroll-area';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { useAssignSection, useAvailableClassrooms } from '../../hooks/useFacultyPlanning';
import type { FacultyPlanningSection, TimeSlotAllowedMode, TeachingMethod } from '../../types/facultyPlanning';
import { cn } from '@/lib/utils';

const DAY_NAMES: Record<number, string> = {
  1: 'Pazartesi',
  2: 'Salı',
  3: 'Çarşamba',
  4: 'Perşembe',
  5: 'Cuma',
  6: 'Cumartesi',
  0: 'Pazar',
};

const TEACHING_METHOD_LABELS: Record<TeachingMethod, string> = {
  FaceToFace: 'Yüz-yüze',
  Online: 'Online',
  Hybrid: 'Hibrit',
};

/**
 * Backend `TeachingMethodPolicy.IsSlotModeCompatible` ile birebir senkron.
 */
function isSlotCompatibleWithMethod(
  slotMode: TimeSlotAllowedMode,
  method: TeachingMethod,
): boolean {
  switch (slotMode) {
    case 'Both':
      return true;
    case 'OnlineOnly':
      return method !== 'FaceToFace';
    case 'FaceToFaceOnly':
      return method !== 'Online';
  }
}

interface SlotAssignDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  day: number;
  slotOrder: number;
  startTime: string;
  endTime: string;
  allowedMode: TimeSlotAllowedMode;
  termId: number;
  facultyId: number;
  sections: FacultyPlanningSection[];
}

type SortKey = 'code' | 'capacity-desc' | 'capacity-asc' | 'hours-desc';
type DepartmentFilter = 'all' | string;
type MethodFilter = 'all' | TeachingMethod;

interface FilterState {
  search: string;
  department: DepartmentFilter;
  method: MethodFilter;
  sort: SortKey;
}

const INITIAL_FILTERS: FilterState = {
  search: '',
  department: 'all',
  method: 'all',
  sort: 'code',
};

export function SlotAssignDialog({
  open,
  onOpenChange,
  day,
  slotOrder: _slotOrder,
  startTime,
  endTime,
  allowedMode,
  termId,
  facultyId,
  sections,
}: SlotAssignDialogProps) {
  const [filters, setFilters] = useState<FilterState>(INITIAL_FILTERS);
  const [activeSectionId, setActiveSectionId] = useState<number | null>(null);
  const assignMutation = useAssignSection();

  // ── Atanabilir section listesi ──────────────────────────────
  // Filtreler: status === 'Unscheduled', isPinned === false, slot tipi uyumlu.
  // Atanamaz olanlar burada hiç görünmez — manuel atama akışı sadece bu listeyi kullanır.
  const eligible = useMemo(() => {
    return sections.filter(s =>
      s.status === 'Unscheduled' &&
      !s.isPinned &&
      isSlotCompatibleWithMethod(allowedMode, s.teachingMethod)
    );
  }, [sections, allowedMode]);

  // ── Mevcut bölüm seçenekleri (filtre paneli için) ───────────
  const departmentOptions = useMemo(() => {
    const codes = new Set<string>();
    eligible.forEach(s => codes.add(s.departmentCode));
    return Array.from(codes).sort();
  }, [eligible]);

  // ── Filtreleme + sıralama ───────────────────────────────────
  const visible = useMemo(() => {
    const q = filters.search.trim().toLowerCase();
    const filtered = eligible.filter(s => {
      if (q && !(
        s.courseCode.toLowerCase().includes(q) ||
        s.courseName.toLowerCase().includes(q) ||
        (s.instructorName?.toLowerCase().includes(q) ?? false)
      )) return false;
      if (filters.department !== 'all' && s.departmentCode !== filters.department) return false;
      if (filters.method !== 'all' && s.teachingMethod !== filters.method) return false;
      return true;
    });

    const sorted = [...filtered];
    switch (filters.sort) {
      case 'capacity-desc': sorted.sort((a, b) => b.capacity - a.capacity); break;
      case 'capacity-asc': sorted.sort((a, b) => a.capacity - b.capacity); break;
      case 'hours-desc':
        sorted.sort((a, b) => (b.theoryHours + b.labHours) - (a.theoryHours + a.labHours));
        break;
      case 'code':
      default:
        sorted.sort((a, b) =>
          a.courseCode.localeCompare(b.courseCode) ||
          a.sectionIndex - b.sectionIndex);
    }
    return sorted;
  }, [eligible, filters]);

  const timeLabel = `${startTime.slice(0, 5)}–${endTime.slice(0, 5)}`;
  const dayName = DAY_NAMES[day] ?? `Gün ${day}`;
  const slotModeLabel = allowedMode === 'OnlineOnly'
    ? 'Online Slot'
    : allowedMode === 'FaceToFaceOnly'
      ? 'Yüz-yüze Slot'
      : null;

  const handleAssign = (sectionId: number, classroomId: number) => {
    assignMutation.mutate(
      {
        sectionId,
        classroomId,
        scheduleSlots: [
          { day, startTime: startTime.slice(0, 5), endTime: endTime.slice(0, 5), classroomId },
        ],
      },
      {
        onSuccess: () => {
          setActiveSectionId(null);
          onOpenChange(false);
        },
      },
    );
  };

  const resetFilters = () => setFilters(INITIAL_FILTERS);

  const filterCount =
    (filters.search ? 1 : 0) +
    (filters.department !== 'all' ? 1 : 0) +
    (filters.method !== 'all' ? 1 : 0);

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <ResizableDialogContent
        defaultWidth={960}
        defaultHeight={620}
        minWidth={620}
        minHeight={420}
        className="!p-0 !overflow-hidden flex flex-col"
      >
        {/* ── Header ─────────────────────────────────────── */}
        {/* Sağ üstte ResizableDialog'un kendi X + Fullscreen butonları otomatik gelir.
            Burada sadece başlık + bilgi metni — extra X butonu yok. */}
        <div className="px-6 py-4 border-b border-slate-100 flex items-center gap-3 shrink-0 pr-24">
          <div className="flex-1">
            <div className="flex items-center gap-2">
              <h3 className="text-base font-semibold text-[#003366]">
                {dayName} {timeLabel}
              </h3>
              <span className="text-slate-300">•</span>
              <span className="text-sm text-slate-600">Şube Ata</span>
              {slotModeLabel && (
                <Badge variant="outline" className="text-[10px] uppercase">
                  {slotModeLabel}
                </Badge>
              )}
            </div>
            <p className="text-xs text-slate-400 mt-0.5">
              {visible.length === eligible.length
                ? `${eligible.length} ders atanabilir`
                : `${visible.length} ders gösteriliyor • ${eligible.length} atanabilir`}
            </p>
          </div>
        </div>

        {/* ── Body: filtreler + grid ─────────────────────── */}
        <div className="flex-1 min-h-0 flex">
          {/* Sol panel — filtreler */}
          <aside className="w-[230px] border-r border-slate-100 px-4 py-4 overflow-y-auto shrink-0">
            <div className="space-y-4">
              {/* Search */}
              <div>
                <label className="text-[11px] font-medium uppercase tracking-wider text-slate-500 mb-1.5 block">
                  Ara
                </label>
                <div className="relative">
                  <Search className="absolute left-2.5 top-1/2 -translate-y-1/2 h-3.5 w-3.5 text-slate-400 pointer-events-none" />
                  <Input
                    placeholder="Ders kodu, ad, hoca…"
                    value={filters.search}
                    onChange={e => setFilters(f => ({ ...f, search: e.target.value }))}
                    className="pl-8 h-8 text-sm"
                  />
                </div>
              </div>

              {/* Sıralama */}
              <div>
                <label className="text-[11px] font-medium uppercase tracking-wider text-slate-500 mb-1.5 block">
                  Sıralama
                </label>
                <Select
                  value={filters.sort}
                  onValueChange={(v) => setFilters(f => ({ ...f, sort: v as SortKey }))}
                >
                  <SelectTrigger className="h-8 text-sm">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="code">Ders kodu (A-Z)</SelectItem>
                    <SelectItem value="capacity-desc">Kapasite ↓</SelectItem>
                    <SelectItem value="capacity-asc">Kapasite ↑</SelectItem>
                    <SelectItem value="hours-desc">Saat (T+L) ↓</SelectItem>
                  </SelectContent>
                </Select>
              </div>

              {/* Bölüm */}
              {departmentOptions.length > 1 && (
                <div>
                  <label className="text-[11px] font-medium uppercase tracking-wider text-slate-500 mb-1.5 block">
                    Bölüm
                  </label>
                  <Select
                    value={filters.department}
                    onValueChange={(v) => setFilters(f => ({ ...f, department: v }))}
                  >
                    <SelectTrigger className="h-8 text-sm">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="all">Tümü</SelectItem>
                      {departmentOptions.map(code => (
                        <SelectItem key={code} value={code}>{code}</SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
              )}

              {/* Ders tipi */}
              <div>
                <label className="text-[11px] font-medium uppercase tracking-wider text-slate-500 mb-1.5 block">
                  Ders Tipi
                </label>
                <Select
                  value={filters.method}
                  onValueChange={(v) => setFilters(f => ({ ...f, method: v as MethodFilter }))}
                >
                  <SelectTrigger className="h-8 text-sm">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="all">Tümü</SelectItem>
                    <SelectItem value="FaceToFace">{TEACHING_METHOD_LABELS.FaceToFace}</SelectItem>
                    <SelectItem value="Online">{TEACHING_METHOD_LABELS.Online}</SelectItem>
                    <SelectItem value="Hybrid">{TEACHING_METHOD_LABELS.Hybrid}</SelectItem>
                  </SelectContent>
                </Select>
              </div>

              {filterCount > 0 && (
                <button
                  type="button"
                  onClick={resetFilters}
                  className="text-[11px] text-slate-500 hover:text-[#003366] underline underline-offset-2"
                >
                  Filtreleri temizle ({filterCount})
                </button>
              )}
            </div>
          </aside>

          {/* Sağ panel — section grid */}
          <ScrollArea className="flex-1 min-h-0">
            {visible.length === 0 ? (
              <div className="h-full min-h-[300px] flex flex-col items-center justify-center text-center px-8 text-slate-400">
                <BookOpen className="h-8 w-8 mb-2 opacity-40" />
                <p className="text-sm font-medium">
                  {eligible.length === 0
                    ? 'Bu slot için atanabilir ders yok'
                    : 'Filtreyle eşleşen ders bulunamadı'}
                </p>
                {eligible.length > 0 && filterCount > 0 && (
                  <button
                    type="button"
                    onClick={resetFilters}
                    className="text-xs text-[#003366] hover:underline mt-2"
                  >
                    Filtreleri temizle
                  </button>
                )}
              </div>
            ) : (
              <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3 p-4">
                {visible.map(s => (
                  <SectionCard
                    key={s.sectionId}
                    section={s}
                    expanded={activeSectionId === s.sectionId}
                    onExpand={() => setActiveSectionId(prev => prev === s.sectionId ? null : s.sectionId)}
                    termId={termId}
                    facultyId={facultyId}
                    day={day}
                    startTime={startTime}
                    endTime={endTime}
                    onAssign={handleAssign}
                    isAssigning={assignMutation.isPending}
                  />
                ))}
              </div>
            )}
          </ScrollArea>
        </div>

        {/* Error feedback */}
        {assignMutation.isError && (
          <div className="px-6 py-2 bg-red-50 border-t border-red-200 shrink-0">
            <p className="text-xs text-red-600">
              Atama başarısız: {assignMutation.error instanceof Error ? assignMutation.error.message : 'Bilinmeyen hata'}
            </p>
          </div>
        )}
      </ResizableDialogContent>
    </Dialog>
  );
}

// ──────────────────────────────────────────────────────────────
// Section Card — kompakt kart, tıklayınca classroom seçim
// ──────────────────────────────────────────────────────────────

interface SectionCardProps {
  section: FacultyPlanningSection;
  expanded: boolean;
  onExpand: () => void;
  termId: number;
  facultyId: number;
  day: number;
  startTime: string;
  endTime: string;
  onAssign: (sectionId: number, classroomId: number) => void;
  isAssigning: boolean;
}

function SectionCard({
  section,
  expanded,
  onExpand,
  termId,
  facultyId,
  day,
  startTime,
  endTime,
  onAssign,
  isAssigning,
}: SectionCardProps) {
  // Pür-lab dersler Lab tipi, diğerleri Lecture bekler (editör/Assignment.cs ile birebir türetim).
  // Tip + facultyId classroom sorgusuna geçer → yalnız fakülteye atanmış, doğru tipte derslikler önerilir
  // (yanlış tip / başka fakülte odası önerip backend'in reddetmesi engellenir).
  const requiredType = section.labHours > 0 && section.theoryHours === 0 ? 'Lab' : 'Lecture';
  const classroomsQuery = useAvailableClassrooms(
    termId,
    day,
    startTime,
    endTime,
    section.capacity,
    requiredType,
    facultyId,
  );
  const [selectedClassroomId, setSelectedClassroomId] = useState<string>('');

  const handleConfirm = () => {
    const id = parseInt(selectedClassroomId, 10);
    if (!isNaN(id)) onAssign(section.sectionId, id);
  };

  const totalHours = section.theoryHours + section.labHours;

  return (
    <div className={cn(
      'border rounded-lg bg-white transition-all',
      expanded ? 'border-[#003366] shadow-md' : 'border-slate-200 hover:border-slate-300 hover:shadow-sm'
    )}>
      <button
        type="button"
        onClick={onExpand}
        className="w-full text-left px-3.5 py-3"
      >
        <div className="flex items-start justify-between gap-2">
          <div className="flex-1 min-w-0">
            <div className="flex items-center gap-1.5 flex-wrap">
              <span className="font-mono font-bold text-base text-slate-800">
                {section.courseCode}
              </span>
              <Badge variant="outline" className="text-[10px] py-0 px-1.5 h-4">
                Ş.{section.sectionIndex}
              </Badge>
            </div>
            <p className="text-xs text-slate-600 mt-0.5 line-clamp-1">
              {section.courseName}
            </p>
          </div>
          <Badge variant="outline" className="text-[10px] shrink-0 bg-slate-50">
            {section.departmentCode}
          </Badge>
        </div>

        <div className="flex items-center gap-3 mt-2 text-[11px] text-slate-500">
          <span className="flex items-center gap-1">
            <Users className="h-3 w-3" />
            {section.capacity}
          </span>
          <span>{section.theoryHours}T + {section.labHours}L</span>
          {totalHours > 0 && (
            <span className="text-slate-300">• {totalHours} saat</span>
          )}
        </div>

        {section.instructorName ? (
          <p className="text-[11px] text-slate-400 mt-1 truncate">
            {section.instructorName}
          </p>
        ) : (
          <p className="text-[11px] text-slate-300 italic mt-1">
            Hoca atanmamış
          </p>
        )}
      </button>

      {expanded && (
        <div className="px-3.5 pb-3 pt-1 space-y-2 border-t border-slate-100 mt-1">
          <Select
            value={selectedClassroomId}
            onValueChange={setSelectedClassroomId}
            disabled={classroomsQuery.isLoading}
          >
            <SelectTrigger className="h-8 text-sm">
              <SelectValue
                placeholder={classroomsQuery.isLoading ? 'Yükleniyor…' : 'Derslik seç'}
              />
            </SelectTrigger>
            <SelectContent>
              {(!classroomsQuery.data || classroomsQuery.data.length === 0) && !classroomsQuery.isLoading && (
                <div className="px-3 py-2 text-sm text-slate-400">Uygun derslik yok</div>
              )}
              {classroomsQuery.data?.map(cr => (
                <SelectItem key={cr.classroomId} value={String(cr.classroomId)}>
                  <div className="flex items-center gap-2">
                    <span className="font-medium">{cr.name}</span>
                    <span className="text-xs text-slate-400">
                      {cr.type} · {cr.capacity} kişi
                    </span>
                  </div>
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
          <Button
            size="sm"
            className="w-full h-8 text-[12px] bg-[#003366] hover:bg-[#002244]"
            disabled={!selectedClassroomId || isAssigning}
            onClick={handleConfirm}
          >
            {isAssigning ? 'Atanıyor…' : 'Onayla'}
          </Button>
        </div>
      )}
    </div>
  );
}
