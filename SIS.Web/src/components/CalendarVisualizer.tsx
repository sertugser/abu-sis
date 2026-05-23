import { useState, useMemo, useCallback, useEffect, useRef, type CSSProperties, type ReactNode } from 'react';
import {
  DndContext,
  DragOverlay,
  PointerSensor,
  pointerWithin,
  rectIntersection,
  useDraggable,
  useDroppable,
  useSensor,
  useSensors,
  type CollisionDetection,
  type DragEndEvent,
  type DragMoveEvent,
  type DragOverEvent,
  type DragStartEvent,
  type UniqueIdentifier,
} from '@dnd-kit/core';
import { ChevronLeft, ChevronRight, Maximize2 } from 'lucide-react';
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/tooltip';
import { CAT_STYLE, calendarEventCategory, type CalendarEventCategory } from '@/lib/calendarEventTypes';
import { cn } from '@/lib/utils';
import {
  TODAY,
  addMonths,
  buildAcademicYearMonths,
  buildGrid,
  buildMonthRange,
  closestMonthTo,
  collectDropTargetDates,
  dayDifference,
  eventDateRange,
  isMonthInRange,
  isWeekendDateValue,
  monthKey,
  sameMonth,
  shiftDateValue,
  shouldRenderWeekdaysOnly,
  trDate,
  weekBars,
  weekdayDateSegments,
  type RenderedVisualizerEvent,
  type ViewMonth,
  type VisualizerEvent,
} from '@/lib/calendarVisualizer/geometry';

export type { VisualizerEvent } from '@/lib/calendarVisualizer/geometry';

const WEEKDAYS = ['PZT', 'SAL', 'ÇAR', 'PER', 'CUM', 'CMT', 'PAZ'];
const MONTH_NAMES = [
  'Ocak', 'Şubat', 'Mart', 'Nisan', 'Mayıs', 'Haziran',
  'Temmuz', 'Ağustos', 'Eylül', 'Ekim', 'Kasım', 'Aralık',
];
const MAX_LANES = 4;
const YEAR_MAX_LANES = 3;

type DragState = {
  eventId: VisualizerEvent['id'];
  sourceStartDate: string;
  sourceEndDate: string;
  dragAnchorDate: string;
};

type CalendarDragData = DragState & {
  kind: 'calendar-event';
  title: string;
  eventType: string;
  templateItemKey?: string | null;
};

type CalendarDropData = {
  kind: 'calendar-day';
  targetDate: string;
};

export type DropFeedback = {
  allowed: boolean;
  reason?: string;
};

export type DropFeedbackRequest = {
  eventId: VisualizerEvent['id'];
  targetDate: string;
  sourceStartDate: string;
  sourceEndDate: string;
  dragAnchorDate: string;
};

export interface CalendarVisualizerProps {
  events: VisualizerEvent[];
  compact?: boolean;
  multiMonth?: boolean;
  inlineYearView?: boolean;
  filterableLegend?: boolean;
  headerActions?: ReactNode;
  /** Academic year start (e.g. 2025 for 2025-2026). Year view starts with that September and expands to every month touched by events. */
  academicYearStart?: number;
  onEventDrop?: (event: {
    eventId: VisualizerEvent['id'];
    targetDate: string;
    sourceStartDate: string;
    sourceEndDate: string;
    dragAnchorDate: string;
  }) => void;
  getDropFeedback?: (event: DropFeedbackRequest) => DropFeedback;
  onInvalidEventDrop?: (event: DropFeedbackRequest & { reason?: string }) => void;
  dragDisabled?: boolean;
}

const calendarCollisionDetection: CollisionDetection = args => {
  const pointerCollisions = pointerWithin(args);
  return pointerCollisions.length > 0 ? pointerCollisions : rectIntersection(args);
};

function isCalendarDragData(value: unknown): value is CalendarDragData {
  if (!value || typeof value !== 'object')
    return false;

  const maybe = value as Partial<CalendarDragData>;
  return maybe.kind === 'calendar-event'
    && maybe.eventId !== undefined
    && typeof maybe.sourceStartDate === 'string'
    && typeof maybe.sourceEndDate === 'string'
    && typeof maybe.dragAnchorDate === 'string'
    && typeof maybe.title === 'string'
    && typeof maybe.eventType === 'string';
}

function isCalendarDropData(value: unknown): value is CalendarDropData {
  if (!value || typeof value !== 'object')
    return false;

  const maybe = value as Partial<CalendarDropData>;
  return maybe.kind === 'calendar-day' && typeof maybe.targetDate === 'string';
}

function dragStateFromData(data: CalendarDragData): DragState {
  return {
    eventId: data.eventId,
    sourceStartDate: data.sourceStartDate,
    sourceEndDate: data.sourceEndDate,
    dragAnchorDate: data.dragAnchorDate,
  };
}

function CalendarDropCell({
  id,
  targetDate,
  feedback,
  disabled,
  className,
  style,
  children,
}: {
  id: UniqueIdentifier;
  targetDate: string;
  feedback: DropFeedback | null;
  disabled: boolean;
  className: string;
  style: CSSProperties;
  children: ReactNode;
}) {
  const { setNodeRef } = useDroppable({
    id,
    disabled,
    data: {
      kind: 'calendar-day',
      targetDate,
    } satisfies CalendarDropData,
  });

  return (
    <div ref={setNodeRef} title={feedback?.reason} className={className} style={style}>
      {children}
    </div>
  );
}

function DraggableCalendarEvent({
  id,
  event,
  disabled,
  className,
  style,
  children,
}: {
  id: UniqueIdentifier;
  event: RenderedVisualizerEvent;
  disabled: boolean;
  className: string;
  style: CSSProperties;
  children: ReactNode;
}) {
  const dragData = useMemo<CalendarDragData>(() => ({
    kind: 'calendar-event',
    eventId: event.sourceEventId,
    sourceStartDate: event.sourceStartDate,
    sourceEndDate: event.sourceEndDate,
    dragAnchorDate: event.startDate,
    title: event.title,
    eventType: event.eventType,
    templateItemKey: event.templateItemKey,
  }), [event]);

  const { attributes, isDragging, listeners, setNodeRef } = useDraggable({
    id,
    data: dragData,
    disabled,
    attributes: {
      roleDescription: 'Takvim etkinligi',
    },
  });

  return (
    <TooltipTrigger asChild>
      <div
        ref={setNodeRef}
        className={cn(className, isDragging && 'opacity-50')}
        style={{ ...style, touchAction: disabled ? undefined : 'none' }}
        {...attributes}
        {...listeners}
      >
        {children}
      </div>
    </TooltipTrigger>
  );
}

function CalendarDragOverlay({ event }: { event: CalendarDragData | null }) {
  if (!event)
    return null;

  const style = CAT_STYLE[calendarEventCategory(event.eventType, event.title, event.templateItemKey)];

  return (
    <div
      className={cn(
        'flex h-6 max-w-[260px] items-center rounded border-l-[3px] px-2 text-xs font-semibold shadow-xl ring-2 ring-white',
        style.bg,
        style.border,
        style.text,
      )}
    >
      <span className="truncate">{event.title}</span>
    </div>
  );
}

export function CalendarVisualizer({
  events,
  compact = false,
  multiMonth = false,
  inlineYearView = false,
  filterableLegend = false,
  headerActions,
  academicYearStart: explicitAcademicYearStart,
  onEventDrop,
  getDropFeedback,
  onInvalidEventDrop,
  dragDisabled = false,
}: CalendarVisualizerProps) {
  const initialMonth = useMemo<ViewMonth>(() => ({ y: TODAY.getFullYear(), m: TODAY.getMonth() }), []);
  const [vm, setVm] = useState<ViewMonth>(initialMonth);
  const [showYearView, setShowYearView] = useState(false);
  const [expandedMonth, setExpandedMonth] = useState<ViewMonth | null>(null);
  const [activeCategory, setActiveCategory] = useState<CalendarEventCategory | null>(null);
  const [dragState, setDragState] = useState<DragState | null>(null);
  const [dragOverDate, setDragOverDate] = useState<string | null>(null);
  const [activeDragOverlay, setActiveDragOverlay] = useState<CalendarDragData | null>(null);
  const dragStateRef = useRef<DragState | null>(null);
  const scrollContainerRef = useRef<HTMLDivElement | null>(null);
  const monthRefs = useRef<Record<string, HTMLDivElement | null>>({});
  const lastEventSignatureRef = useRef<string | null>(null);
  const programmaticScrollRef = useRef(false);
  const scrollResetTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const sensors = useSensors(
    useSensor(PointerSensor, {
      activationConstraint: {
        distance: 1,
      },
    }),
  );

  const visibleEvents = useMemo(
    () => activeCategory
      ? events.filter(event => calendarEventCategory(event.eventType, event.title, event.templateItemKey) === activeCategory)
      : events,
    [activeCategory, events],
  );
  const renderedEvents = useMemo<RenderedVisualizerEvent[]>(
    () => visibleEvents.flatMap(event => {
      const range = eventDateRange(event);
      if (!range)
        return [];

      const segments = shouldRenderWeekdaysOnly(event)
        ? weekdayDateSegments(range.startDate, range.endDate)
        : [range];

      return segments.map(segment => ({
        ...event,
        sourceEventId: event.id,
        sourceStartDate: range.startDate,
        sourceEndDate: range.endDate,
        startDate: segment.startDate,
        endDate: segment.endDate,
      }));
    }),
    [visibleEvents],
  );
  const allMonths = useMemo(() => buildMonthRange(renderedEvents, initialMonth), [renderedEvents, initialMonth]);
  const academicYearMonths = useMemo(() => buildAcademicYearMonths(renderedEvents, vm, explicitAcademicYearStart), [renderedEvents, vm, explicitAcademicYearStart]);
  const academicYearLastMonth = academicYearMonths[academicYearMonths.length - 1];
  const eventSignature = useMemo(
    () => renderedEvents.map(event => `${event.id}:${event.startDate}:${event.endDate}:${event.isChangedPreview ? '1' : '0'}`).join('|'),
    [renderedEvents],
  );

  useEffect(() => {
    if (lastEventSignatureRef.current === eventSignature)
      return;

    lastEventSignatureRef.current = eventSignature;
    setVm(prev => {
      if (isMonthInRange(initialMonth, allMonths)) {
        if (multiMonth) programmaticScrollRef.current = true;
        return sameMonth(prev, initialMonth) ? prev : initialMonth;
      }

      if (isMonthInRange(prev, allMonths))
        return prev;

      if (multiMonth) programmaticScrollRef.current = true;
      return allMonths[0] ?? initialMonth;
    });
  }, [allMonths, eventSignature, initialMonth, multiMonth]);

  useEffect(() => {
    if (!multiMonth || !programmaticScrollRef.current) return;

    const container = scrollContainerRef.current;
    const target = monthRefs.current[monthKey(vm)];
    if (!container || !target) return;

    const containerRect = container.getBoundingClientRect();
    const targetRect = target.getBoundingClientRect();
    const nextTop = targetRect.top - containerRect.top + container.scrollTop;
    container.scrollTo({ top: nextTop, behavior: 'smooth' });

    if (scrollResetTimerRef.current) clearTimeout(scrollResetTimerRef.current);
    scrollResetTimerRef.current = setTimeout(() => {
      programmaticScrollRef.current = false;
    }, 400);
  }, [multiMonth, vm]);

  useEffect(() => {
    return () => {
      if (scrollResetTimerRef.current) clearTimeout(scrollResetTimerRef.current);
    };
  }, []);

  const viewMonths = useMemo(() => {
    if (!multiMonth || renderedEvents.length === 0) return [{ y: vm.y, m: vm.m }];
    return allMonths;
  }, [allMonths, renderedEvents.length, multiMonth, vm]);
  const dropFeedbackMonths = useMemo(
    () => (inlineYearView || showYearView ? academicYearMonths : viewMonths),
    [academicYearMonths, inlineYearView, showYearView, viewMonths],
  );

  const scrollToMonth = useCallback((month: ViewMonth) => {
    if (multiMonth) programmaticScrollRef.current = true;
    setVm(month);
  }, [multiMonth]);

  const updateVisibleMonth = useCallback(() => {
    if (!multiMonth || programmaticScrollRef.current) return;

    const container = scrollContainerRef.current;
    if (!container) return;

    const containerRect = container.getBoundingClientRect();
    const anchorTop = containerRect.top + 12;

    let nextMonth = vm;
    let bestDistance = Number.POSITIVE_INFINITY;

    for (const month of allMonths) {
      const node = monthRefs.current[monthKey(month)];
      if (!node) continue;

      const rect = node.getBoundingClientRect();
      if (rect.bottom < containerRect.top || rect.top > containerRect.bottom) continue;

      const distance = Math.abs(rect.top - anchorTop);
      if (distance < bestDistance) {
        bestDistance = distance;
        nextMonth = month;
      }
    }

    if (!sameMonth(nextMonth, vm)) setVm(nextMonth);
  }, [allMonths, multiMonth, vm]);

  const prev = useCallback(() => {
    if (!multiMonth) {
      setVm(prevMonth => addMonths(prevMonth, -1));
      return;
    }

    const currentIndex = allMonths.findIndex(month => sameMonth(month, vm));
    if (currentIndex > 0) scrollToMonth(allMonths[currentIndex - 1]);
  }, [allMonths, multiMonth, scrollToMonth, vm]);

  const next = useCallback(() => {
    if (!multiMonth) {
      setVm(prevMonth => addMonths(prevMonth, 1));
      return;
    }

    const currentIndex = allMonths.findIndex(month => sameMonth(month, vm));
    if (currentIndex >= 0 && currentIndex < allMonths.length - 1) scrollToMonth(allMonths[currentIndex + 1]);
  }, [allMonths, multiMonth, scrollToMonth, vm]);

  const today = useCallback(() => {
    scrollToMonth(isMonthInRange(initialMonth, allMonths) ? initialMonth : closestMonthTo(initialMonth, allMonths));
  }, [allMonths, initialMonth, scrollToMonth]);

  const resetDrag = useCallback(() => {
    dragStateRef.current = null;
    setDragState(null);
    setDragOverDate(null);
    setActiveDragOverlay(null);
  }, []);

  const buildDropRequest = useCallback((currentDragState: DragState, targetDate: string): DropFeedbackRequest => ({
    eventId: currentDragState.eventId,
    targetDate,
    sourceStartDate: currentDragState.sourceStartDate,
    sourceEndDate: currentDragState.sourceEndDate,
    dragAnchorDate: currentDragState.dragAnchorDate,
  }), []);

  const visibleDropFeedbackByDate = useMemo(() => {
    if (!dragState || !onEventDrop || dragDisabled)
      return {} as Record<string, DropFeedback>;

    const feedbackByDate: Record<string, DropFeedback> = {};
    for (const targetDate of collectDropTargetDates(dropFeedbackMonths)) {
      feedbackByDate[targetDate] = getDropFeedback?.(buildDropRequest(dragState, targetDate)) ?? { allowed: true };
    }

    return feedbackByDate;
  }, [buildDropRequest, dragDisabled, dragState, dropFeedbackMonths, getDropFeedback, onEventDrop]);
  const activeDropRange = useMemo(() => {
    if (!dragState || !dragOverDate)
      return null;

    const shiftDays = dayDifference(dragOverDate, dragState.dragAnchorDate);
    return {
      start: shiftDateValue(dragState.sourceStartDate, shiftDays),
      end: shiftDateValue(dragState.sourceEndDate, shiftDays),
    };
  }, [dragOverDate, dragState]);
  const activeDropFeedback = dragOverDate ? visibleDropFeedbackByDate[dragOverDate] ?? null : null;

  const getDropFeedbackClass = useCallback((feedback: DropFeedback | null, targetDate: string) => {
    if (!dragState || !feedback)
      return null;

    const isInActiveRange = !!activeDropRange && targetDate >= activeDropRange.start && targetDate <= activeDropRange.end;
    const rangeFeedback = isInActiveRange && activeDropFeedback ? activeDropFeedback : feedback;

    return rangeFeedback.allowed
      ? cn('bg-emerald-50 ring-1 ring-inset ring-emerald-300', isInActiveRange && 'bg-emerald-100 ring-2 ring-emerald-500')
      : cn('bg-rose-50 ring-1 ring-inset ring-rose-300', isInActiveRange && 'bg-rose-100 ring-2 ring-rose-500');
  }, [activeDropFeedback, activeDropRange, dragState]);

  const handleDndDragStart = useCallback(({ active }: DragStartEvent) => {
    if (!onEventDrop || dragDisabled)
      return;

    const dragData = active.data.current;
    if (!isCalendarDragData(dragData))
      return;

    const nextDragState = dragStateFromData(dragData);
    dragStateRef.current = nextDragState;
    setDragState(nextDragState);
    setActiveDragOverlay(dragData);
  }, [dragDisabled, onEventDrop]);

  const updateActiveDropTarget = useCallback((over: DragOverEvent['over']) => {
    const dropData = over?.data.current;
    const targetDate = isCalendarDropData(dropData) ? dropData.targetDate : null;

    setDragOverDate(current => current === targetDate ? current : targetDate);
  }, []);

  const handleDndDragMove = useCallback(({ over }: DragMoveEvent) => {
    updateActiveDropTarget(over);
  }, [updateActiveDropTarget]);

  const handleDndDragOver = useCallback(({ over }: DragOverEvent) => {
    updateActiveDropTarget(over);
  }, [updateActiveDropTarget]);

  const handleDndDragEnd = useCallback(({ active, over }: DragEndEvent) => {
    if (!onEventDrop || dragDisabled) {
      resetDrag();
      return;
    }

    const dragData = active.data.current;
    const dropData = over?.data.current;
    const currentDragState = isCalendarDragData(dragData)
      ? dragStateFromData(dragData)
      : dragStateRef.current ?? dragState;

    if (!currentDragState || !isCalendarDropData(dropData)) {
      resetDrag();
      return;
    }

    const request = buildDropRequest(currentDragState, dropData.targetDate);

    const feedback = getDropFeedback?.(request) ?? { allowed: true };
    if (feedback?.allowed === false) {
      onInvalidEventDrop?.({
        ...request,
        reason: feedback.reason,
      });
      resetDrag();
      return;
    }

    onEventDrop(request);
    resetDrag();
  }, [buildDropRequest, dragDisabled, dragState, getDropFeedback, onEventDrop, onInvalidEventDrop, resetDrag]);

  function renderYearMonth(y: number, m: number) {
    const weeks = buildGrid(y, m);
    const weekData = weeks.map(w => {
      const bars = weekBars(w, renderedEvents);
      return { bars, maxLane: bars.reduce((mx, b) => Math.max(mx, b.lane), -1) + 1 };
    });

    return (
      <div key={`year-${y}-${m}`} className="flex h-full flex-col overflow-hidden rounded-xl border border-gray-200 bg-white shadow-sm">
        <div className="flex shrink-0 items-center justify-between bg-[#003366] px-3 py-1.5 text-white">
          <button
            type="button"
            onClick={() => setExpandedMonth({ y, m })}
            className="flex min-w-0 flex-1 items-center gap-1.5 text-left text-sm font-bold leading-none transition hover:text-white/85 focus:outline-none focus:ring-2 focus:ring-white/70"
            aria-label={`${MONTH_NAMES[m]} ${y} ayını büyüt`}
            title={`${MONTH_NAMES[m]} ${y}`}
          >
            <span className="truncate">{MONTH_NAMES[m]} {y}</span>
            <Maximize2 className="h-3.5 w-3.5 shrink-0 opacity-80" />
          </button>
          {sameMonth({ y, m }, vm) && (
            <span className="rounded-full bg-white/15 px-1.5 py-0.5 text-[9px] font-bold text-white">
              GÖRÜNEN
            </span>
          )}
        </div>

        <div className="grid shrink-0 grid-cols-7 border-b border-gray-200 bg-gray-50 text-center text-[9px] font-bold uppercase tracking-wide text-gray-400">
          {WEEKDAYS.map((day, index) => (
            <div key={day} className={cn('border-r border-gray-100 py-1 last:border-r-0', index >= 5 && 'text-rose-500')}>
              {day.substring(0, 2)}
            </div>
          ))}
        </div>

        {weeks.map((week, wi) => {
          const { bars, maxLane } = weekData[wi];
          const visible = bars.filter(b => b.lane < YEAR_MAX_LANES);
          const overflow = bars.length - visible.length;
          const eventTopBase = 18;
          const eventH = Math.min(maxLane, YEAR_MAX_LANES) * 17 + (overflow > 0 ? 12 : 0);
          const cellMinHeight = Math.max(38, eventTopBase + eventH);

          return (
            <div key={wi} className="relative grow shrink-0 border-b border-gray-100 last:border-b-0 overflow-hidden">
              <div className="grid h-full grid-cols-7">
                {week.map((day, di) => {
                  const dropFeedback = visibleDropFeedbackByDate[day.str] ?? null;
                  const isWeekend = isWeekendDateValue(day.str);
                  return (
                    <CalendarDropCell
                      id={`calendar-day:year:${y}-${m}:${wi}-${di}:${day.str}`}
                      key={di}
                      targetDate={day.str}
                      feedback={dropFeedback}
                      disabled={!onEventDrop || dragDisabled}
                      className={cn(
                        'border-r border-gray-100 px-1 pt-1 text-[10px] leading-none last:border-r-0 transition-colors duration-75',
                        isWeekend && day.isMonth && 'bg-slate-50',
                        isWeekend && !day.isMonth && 'bg-slate-50/50',
                        !isWeekend && !day.isMonth && 'bg-gray-50/60',
                        getDropFeedbackClass(dropFeedback, day.str),
                      )}
                      style={{ minHeight: `${cellMinHeight}px` }}
                    >
                      <span
                        className={cn(
                          'inline-flex h-4 w-4 items-center justify-center rounded-full',
                          day.isToday && 'bg-[#003366] text-white font-bold',
                          !day.isToday && day.isMonth && (isWeekend ? 'text-slate-400 font-medium' : 'text-gray-800 font-medium'),
                          !day.isToday && !day.isMonth && (isWeekend ? 'text-slate-300' : 'text-gray-300'),
                        )}
                      >
                        {day.day}
                      </span>
                    </CalendarDropCell>
                  );
                })}
              </div>

              {visible.map((b, i) => {
                const s = CAT_STYLE[b.cat];
                const isDraggable = b.event.draggable === true && !dragDisabled && !!onEventDrop;

                return (
                  <TooltipProvider key={`year-${b.event.id}-${y}-${m}-${wi}-${i}`} delayDuration={100}>
                    <Tooltip>
                      <DraggableCalendarEvent
                        id={`calendar-event:year:${y}-${m}:${wi}-${i}:${String(b.event.id)}`}
                        event={b.event}
                        disabled={!isDraggable}
                        className={cn(
                          'absolute flex h-4 items-center overflow-hidden rounded border-l-4 px-1.5 text-[10px] font-bold leading-none transition-shadow hover:z-10 hover:shadow-md',
                          s.bg,
                          s.border,
                          s.text,
                          b.event.isChangedPreview && 'ring-1 ring-primary bg-primary/20',
                          isDraggable ? 'cursor-grab active:cursor-grabbing' : 'cursor-default',
                          dragState && 'pointer-events-none',
                        )}
                        style={{
                          top: `${eventTopBase + b.lane * 17}px`,
                          left: `calc(${(b.col / 7) * 100}% + 2px)`,
                          width: `calc(${(b.span / 7) * 100}% - 4px)`,
                        }}
                      >
                        <span className="truncate">{b.event.title}</span>
                      </DraggableCalendarEvent>
                      <TooltipContent side="top" className="max-w-xs z-[100]">
                        <p className="font-semibold">{b.event.title}</p>
                        <p className="text-xs text-gray-500 mt-0.5">
                          {trDate(b.event.startDate)}
                          {b.event.endDate !== b.event.startDate && ` - ${trDate(b.event.endDate)}`}
                        </p>
                      </TooltipContent>
                    </Tooltip>
                  </TooltipProvider>
                );
              })}

              {overflow > 0 && (
                <button
                  type="button"
                  onClick={() => setExpandedMonth({ y, m })}
                  className="absolute left-1 text-[9px] font-bold text-[#003366] transition hover:underline"
                  style={{ top: `${eventTopBase + YEAR_MAX_LANES * 17}px` }}
                  title={`${MONTH_NAMES[m]} ${y} ayını büyüt`}
                >
                  +{overflow} daha
                </button>
              )}
            </div>
          );
        })}
      </div>
    );
  }

  function renderMonth(y: number, m: number, expanded = false) {
    const laneCap = expanded ? Number.POSITIVE_INFINITY : MAX_LANES;
    const weeks = buildGrid(y, m);
    const weekData = weeks.map(w => {
      const bars = weekBars(w, renderedEvents);
      return { bars, maxLane: bars.reduce((mx, b) => Math.max(mx, b.lane), -1) + 1 };
    });

    return (
      <div
        key={`${y}-${m}`}
        ref={node => {
          monthRefs.current[monthKey({ y, m })] = node;
        }}
        className={cn('border border-gray-200 rounded-xl overflow-hidden bg-white shadow-sm', multiMonth && 'mb-4')}
      >
        {multiMonth && (
          <div className="bg-[#003366] text-white px-3 py-1.5 text-xs font-bold tracking-wide uppercase text-center border-b border-[#002244]">
            {MONTH_NAMES[m]} {y}
          </div>
        )}

        <div className="grid grid-cols-7 border-b border-gray-200 bg-gray-50">
          {WEEKDAYS.map((d, index) => (
            <div key={d} className={cn('text-center text-[9px] sm:text-[11px] font-bold text-gray-400 uppercase tracking-wider py-1.5 sm:py-3 border-r border-gray-100 last:border-r-0', index >= 5 && 'text-rose-500')}>
              {d.substring(0, 3)}
            </div>
          ))}
        </div>

        {weeks.map((week, wi) => {
          const { bars, maxLane } = weekData[wi];
          const visible = bars.filter(b => b.lane < laneCap);
          const overflow = bars.length - visible.length;
          const eventH = Math.min(maxLane, laneCap) * 24 + (overflow > 0 ? 16 : 0);
          const eventTopBase = compact ? 28 : 36;
          const cellMinHeight = Math.max(compact ? 56 : 96, eventTopBase + eventH);

          return (
            <div key={wi} className="relative border-b border-gray-100 last:border-b-0 overflow-hidden">
              <div className="grid grid-cols-7">
                {week.map((day, di) => {
                  const dropFeedback = visibleDropFeedbackByDate[day.str] ?? null;
                  const isWeekend = isWeekendDateValue(day.str);
                  return (
                    <CalendarDropCell
                      id={`calendar-day:month:${y}-${m}:${wi}-${di}:${day.str}`}
                      key={di}
                      targetDate={day.str}
                      feedback={dropFeedback}
                      disabled={!onEventDrop || dragDisabled}
                      className={cn(
                        'border-r border-gray-100 last:border-r-0 px-1 pt-1 transition-colors duration-75 sm:px-2 sm:pt-2',
                        isWeekend && day.isMonth && 'bg-slate-50',
                        isWeekend && !day.isMonth && 'bg-slate-50/50',
                        !isWeekend && !day.isMonth && 'bg-gray-50/60',
                        getDropFeedbackClass(dropFeedback, day.str),
                      )}
                      style={{ minHeight: `${cellMinHeight}px` }}
                    >
                      <span
                        className={cn(
                          'inline-flex items-center justify-center w-5 h-5 sm:w-7 sm:h-7 text-xs sm:text-sm rounded-full',
                          day.isToday && 'bg-[#003366] text-white font-bold',
                          !day.isToday && day.isMonth && (isWeekend ? 'text-slate-400 font-medium' : 'text-gray-800 font-medium'),
                          !day.isToday && !day.isMonth && (isWeekend ? 'text-slate-300' : 'text-gray-300'),
                        )}
                      >
                        {day.day}
                      </span>
                    </CalendarDropCell>
                  );
                })}
              </div>

              {visible.map((b, i) => {
                const s = CAT_STYLE[b.cat];
                const isContinuation = !b.start;
                const isDraggable = b.event.draggable === true && !dragDisabled && !!onEventDrop;
                return (
                  <TooltipProvider key={`${b.event.id}-${wi}-${i}`} delayDuration={100}>
                    <Tooltip>
                      <DraggableCalendarEvent
                        id={`calendar-event:month:${y}-${m}:${wi}-${i}:${String(b.event.id)}`}
                        event={b.event}
                        disabled={!isDraggable}
                        className={cn(
                          'absolute flex h-5 items-center rounded border-l-[3px] px-1 text-[10px] font-medium truncate transition-shadow hover:z-10 hover:shadow-md sm:h-6 sm:px-2 sm:text-[13px]',
                          s.bg,
                          s.border,
                          s.text,
                          isContinuation && 'opacity-90',
                          b.event.isChangedPreview && 'ring-2 ring-primary border-primary bg-primary/20 bg-blend-multiply',
                          isDraggable ? 'cursor-grab active:cursor-grabbing' : 'cursor-default',
                          dragState && 'pointer-events-none',
                        )}
                        style={{
                          top: `${eventTopBase + b.lane * 24}px`,
                          left: `calc(${(b.col / 7) * 100}% + 3px)`,
                          width: `calc(${(b.span / 7) * 100}% - 6px)`,
                        }}
                      >
                        <span className="uppercase text-[9px] sm:text-[10px] font-bold opacity-60 mr-1.5 shrink-0 hidden sm:inline">
                          {s.label}
                        </span>
                        <span className="truncate">{b.event.title}</span>
                      </DraggableCalendarEvent>
                      <TooltipContent side="top" className="max-w-xs z-[100]">
                        <p className="font-semibold">{b.event.title}</p>
                        {b.event.isChangedPreview && (
                          <p className="text-xs text-primary font-bold my-0.5">Onizlemede Degistirildi</p>
                        )}
                        <p className="text-xs text-gray-500 mt-0.5">
                          {trDate(b.event.startDate)}
                          {b.event.endDate !== b.event.startDate && ` - ${trDate(b.event.endDate)}`}
                        </p>
                        {b.event.note && (
                          <p className="text-xs text-amber-600 mt-1">{b.event.note}</p>
                        )}
                      </TooltipContent>
                    </Tooltip>
                  </TooltipProvider>
                );
              })}

              {overflow > 0 && (
                <div
                  className="absolute left-1 sm:left-2 text-[10px] sm:text-xs font-semibold text-[#003366]"
                  style={{ top: `${eventTopBase + laneCap * 24}px` }}
                >
                  +{overflow} daha
                </div>
              )}
            </div>
          );
        })}
      </div>
    );
  }

  function renderLegend(prefix: string, iconClass = 'h-3.5 w-5', textClass = 'text-[11px] font-semibold text-gray-600') {
    return (
      <>
        {(Object.entries(CAT_STYLE) as [CalendarEventCategory, typeof CAT_STYLE[CalendarEventCategory]][]).map(([category, s]) => {
          const selected = activeCategory === category;
          const content = (
            <>
              <div className={cn(iconClass, 'rounded border-l-4', s.bg, s.border)} />
              <span className={cn(textClass, selected && 'text-[#003366]')}>{s.label}</span>
            </>
          );

          if (!filterableLegend) {
            return (
              <div key={`${prefix}-${s.label}`} className="flex items-center gap-1.5">
                {content}
              </div>
            );
          }

          return (
            <button
              key={`${prefix}-${s.label}`}
              type="button"
              aria-pressed={selected}
              title={selected ? 'Filtreyi kaldır' : `${s.label} etkinliklerini göster`}
              onClick={() => setActiveCategory(current => current === category ? null : category)}
              className={cn(
                'flex items-center gap-1.5 rounded-full border px-2 py-1 transition',
                selected
                  ? 'border-[#003366]/40 bg-[#003366]/10 shadow-sm'
                  : 'border-transparent hover:border-gray-200 hover:bg-white',
              )}
            >
              {content}
            </button>
          );
        })}
        {filterableLegend && activeCategory && (
          <button
            type="button"
            onClick={() => setActiveCategory(null)}
            className="rounded-full border border-gray-200 px-2 py-1 text-[10px] font-semibold text-gray-500 transition hover:bg-white hover:text-[#003366]"
          >
            Tümünü göster
          </button>
        )}
      </>
    );
  }

  function renderInlineYearView() {
    const firstMonth = academicYearMonths[0];
    const lastMonth = academicYearMonths[academicYearMonths.length - 1];
    const expandedMonthTitle = expandedMonth ? `${MONTH_NAMES[expandedMonth.m]} ${expandedMonth.y}` : '';

    return (
      <>
        <div className="flex flex-col overflow-hidden rounded-2xl border border-gray-200 bg-white shadow-sm">
          <div className="flex shrink-0 flex-col gap-3 border-b border-gray-200 px-4 py-3 sm:flex-row sm:items-start sm:justify-between sm:px-6">
            <div>
              <p className="text-xs font-bold uppercase tracking-[0.2em] text-gray-400">Akademik yıl</p>
              <h2 className="text-xl font-bold text-[#003366] sm:text-2xl">
                {firstMonth && MONTH_NAMES[firstMonth.m]} {firstMonth?.y}
                {' - '}
                {lastMonth && MONTH_NAMES[lastMonth.m]} {lastMonth?.y}
              </h2>
            </div>
            <div className="flex flex-col items-start gap-2 sm:items-end">
              <div className="flex flex-wrap gap-2 sm:justify-end">
                {renderLegend('inline-year-legend')}
              </div>
              {headerActions && (
                <div className="flex shrink-0">
                  {headerActions}
                </div>
              )}
            </div>
          </div>
          <div className="bg-slate-50 p-2">
            <div className="grid min-h-0 auto-rows-fr gap-3 sm:grid-cols-2 lg:grid-cols-3">
              {academicYearMonths.map(month => renderYearMonth(month.y, month.m))}
            </div>
          </div>
        </div>

        {expandedMonth && (
          <div
            className="fixed inset-0 z-40 flex items-center justify-center bg-slate-950/60 p-2 sm:p-6"
            role="dialog"
            aria-modal="true"
            aria-labelledby="expanded-calendar-month-title"
            onMouseDown={(event) => {
              if (event.target === event.currentTarget) setExpandedMonth(null);
            }}
          >
            <div className="flex max-h-[calc(100dvh-1rem)] w-full max-w-6xl flex-col overflow-hidden rounded-2xl bg-white shadow-2xl">
              <div className="flex shrink-0 items-center justify-between gap-3 border-b border-gray-200 px-4 py-3 sm:px-6">
                <div>
                  <p className="text-xs font-bold uppercase tracking-[0.2em] text-gray-400">Ay görünümü</p>
                  <h2 id="expanded-calendar-month-title" className="text-xl font-bold text-[#003366] sm:text-2xl">
                    {expandedMonthTitle}
                  </h2>
                </div>
                <button
                  type="button"
                  onClick={() => setExpandedMonth(null)}
                  className="rounded-lg border border-gray-300 px-4 py-2 text-sm font-semibold text-gray-700 transition hover:bg-gray-50"
                >
                  Kapat
                </button>
              </div>
              <div className="min-h-0 flex-1 overflow-auto bg-slate-50 p-2 sm:p-4">
                {renderMonth(expandedMonth.y, expandedMonth.m, true)}
              </div>
            </div>
          </div>
        )}
      </>
    );
  }

  function renderDndShell(content: ReactNode) {
    return (
      <DndContext
        sensors={sensors}
        collisionDetection={calendarCollisionDetection}
        onDragStart={handleDndDragStart}
        onDragMove={handleDndDragMove}
        onDragOver={handleDndDragOver}
        onDragEnd={handleDndDragEnd}
        onDragCancel={resetDrag}
      >
        {content}
        <DragOverlay dropAnimation={null} zIndex={120}>
          <CalendarDragOverlay event={activeDragOverlay} />
        </DragOverlay>
      </DndContext>
    );
  }

  if (inlineYearView) {
    return renderDndShell(renderInlineYearView());
  }

  return renderDndShell(
    <div className={cn('space-y-3', multiMonth && 'flex flex-col max-h-[70vh]')}>
      <div className={cn(multiMonth && 'sticky top-0 z-10 bg-white pb-1')}>
        <div className="flex flex-wrap items-center justify-between gap-2">
          <div>
            <h2 className="text-lg font-bold text-[#003366]">
              {MONTH_NAMES[vm.m]} {vm.y}
            </h2>
          </div>
          <div className="flex flex-wrap items-center justify-end gap-2">
            <div className="flex gap-1">
              <button type="button" onClick={prev} className="p-2 rounded-lg border border-gray-300 hover:bg-gray-100 text-gray-600">
                <ChevronLeft className="h-5 w-5" />
              </button>
              <button type="button" onClick={next} className="p-2 rounded-lg border border-gray-300 hover:bg-gray-100 text-gray-600">
                <ChevronRight className="h-5 w-5" />
              </button>
            </div>
            <button
              type="button"
              onClick={() => setShowYearView(true)}
              className="rounded-lg border border-[#003366]/30 bg-[#003366]/5 px-3 py-1.5 text-sm font-semibold text-[#003366] hover:bg-[#003366]/10"
            >
              Yıl olarak görüntüle
            </button>
            <button
              type="button"
              onClick={today}
              className="rounded-lg border border-gray-300 px-4 py-1.5 text-sm font-semibold text-gray-700 hover:bg-gray-50"
            >
              Bugün
            </button>
          </div>
        </div>
      </div>

      <div
        ref={scrollContainerRef}
        onScroll={updateVisibleMonth}
        className={cn(multiMonth && 'overflow-y-auto pr-2 styled-scrollbar flex-1')}
      >
        {viewMonths.map(month => renderMonth(month.y, month.m))}
      </div>

      <div className="flex flex-wrap gap-2 px-1 border-t border-gray-100 pt-2">
        {renderLegend('month-legend', 'w-4 h-3', 'text-[11px] text-gray-500 font-medium')}
      </div>

      {showYearView && (
        <div className="fixed inset-0 z-[70] flex items-center justify-center bg-slate-950/60 p-2">
          <div className="flex h-[calc(100dvh-1rem)] w-full max-w-none flex-col overflow-hidden rounded-2xl bg-white shadow-2xl">
            <div className="flex shrink-0 flex-col gap-2 border-b border-gray-200 px-4 py-3 sm:flex-row sm:items-center sm:justify-between sm:px-6">
              <div>
                <p className="text-xs font-bold uppercase tracking-[0.2em] text-gray-400">Akademik yıl</p>
                <h2 className="text-xl font-bold text-[#003366] sm:text-2xl">
                  {academicYearMonths[0] && MONTH_NAMES[academicYearMonths[0].m]} {academicYearMonths[0]?.y}
                  {' - '}
                  {academicYearLastMonth && MONTH_NAMES[academicYearLastMonth.m]} {academicYearLastMonth?.y}
                </h2>
                <div className="mt-2 flex flex-wrap gap-2">
                  {renderLegend('year-legend')}
                </div>
              </div>
              <button
                type="button"
                onClick={() => setShowYearView(false)}
                className="self-start rounded-lg border border-gray-300 px-4 py-2 text-sm font-semibold text-gray-700 hover:bg-gray-50 sm:self-auto"
              >
                Kapat
              </button>
            </div>
            <div className="min-h-0 flex-1 overflow-y-auto bg-slate-50 p-2">
              <div className="grid min-h-0 auto-rows-fr gap-3 sm:grid-cols-2 lg:grid-cols-3 2xl:grid-cols-4">
                {academicYearMonths.map(month => renderYearMonth(month.y, month.m))}
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
