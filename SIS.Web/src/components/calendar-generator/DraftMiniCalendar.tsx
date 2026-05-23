import type { ComponentProps } from 'react';
import { CalendarVisualizer, type VisualizerEvent } from '@/components/CalendarVisualizer';
import { academicYearStart, dateInput } from '@/lib/calendarGenerator/dates';
import { isEditableDraftEvent } from '@/lib/calendarGenerator/eventRules';
import { displayText, type CalendarView } from '@/lib/calendarGenerator/types';
import type { WhatIfResultDto } from '@/hooks/useCalendarGenerator';

type VisualizerProps = ComponentProps<typeof CalendarVisualizer>;

interface DraftMiniCalendarProps {
  current: CalendarView;
  whatIfData?: WhatIfResultDto;
  onEventDrop: VisualizerProps['onEventDrop'];
  getDropFeedback: VisualizerProps['getDropFeedback'];
  onInvalidEventDrop: VisualizerProps['onInvalidEventDrop'];
  dragDisabled: boolean;
}

export function DraftMiniCalendar({
  current,
  whatIfData,
  onEventDrop,
  getDropFeedback,
  onInvalidEventDrop,
  dragDisabled,
}: DraftMiniCalendarProps) {
  const defaultEvents = current.events;

  // Map to VisualizerEvent format
  const eventsToRender: VisualizerEvent[] = defaultEvents.map(e => {
    // Find if this event is modified in the what-if state
    const whatIfEvent = whatIfData?.events.find(we => we.draftEventId === e.draftEventId);

    let isChangedPreview = false;
    let targetEvent = e;

    if (whatIfEvent && (dateInput(whatIfEvent.startDate) !== dateInput(e.startDate) || dateInput(whatIfEvent.endDate) !== dateInput(e.endDate))) {
      isChangedPreview = true;
      targetEvent = whatIfEvent; // Render the new dates
    }

    return {
      id: targetEvent.draftEventId,
      eventType: targetEvent.eventType,
      title: displayText(targetEvent.title),
      startDate: dateInput(targetEvent.startDate),
      endDate: dateInput(targetEvent.endDate) || dateInput(targetEvent.startDate),
      note: null,
      isChangedPreview,
      templateItemKey: targetEvent.templateItemKey,
      draggable: isEditableDraftEvent(e),
    };
  });

  if (eventsToRender.length === 0) return null;

  // Use the calendar's academic year to correctly anchor the year view range
  const calendarYearStart = academicYearStart(current.academicYear);

  return (
    <div className="rounded-lg border border-slate-200 bg-white p-3 shadow-sm">
      <h2 className="text-sm font-semibold text-slate-900 mb-3">Takvim Görünümü</h2>
      <CalendarVisualizer
        events={eventsToRender}
        compact={true}
        multiMonth={true}
        academicYearStart={calendarYearStart}
        onEventDrop={onEventDrop}
        getDropFeedback={getDropFeedback}
        onInvalidEventDrop={onInvalidEventDrop}
        dragDisabled={dragDisabled}
      />
    </div>
  );
}
