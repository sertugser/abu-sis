import type { FacultyPlanningTimeSlot } from '../../types/facultyPlanning';

export const STATUS_CONFIG: Record<string, {
  label: string;
  variant: 'default' | 'success' | 'destructive' | 'warning' | 'outline';
  calendarColors: string;
}> = {
  Unscheduled: { label: 'Zamanlanmamış', variant: 'outline', calendarColors: 'bg-gray-100 border-gray-300 text-gray-700' },
  Scheduled: { label: 'Zamanlanmış', variant: 'success', calendarColors: 'bg-blue-100 border-blue-300 text-blue-900 hover:bg-blue-200' },
  Conflicted: { label: 'Çakışmalı', variant: 'destructive', calendarColors: 'bg-red-100 border-red-300 text-red-900 hover:bg-red-200' },
  Partial: { label: 'Kısmi', variant: 'warning', calendarColors: 'bg-amber-100 border-amber-300 text-amber-900 hover:bg-amber-200' },
};

export interface ManualScheduleOption {
  key: string;
  day: number;
  startTime: string;
  endTime: string;
  label: string;
}

function formatSlotTime(value: string) {
  return value.slice(0, 5);
}

export function buildManualScheduleOptions(timeSlots: FacultyPlanningTimeSlot[], requiredSlotCount: number): ManualScheduleOption[] {
  if (requiredSlotCount <= 0) {
    return [];
  }

  const slotsByDay = new Map<number, FacultyPlanningTimeSlot[]>();
  timeSlots.forEach(slot => {
    const daySlots = slotsByDay.get(slot.day) ?? [];
    daySlots.push(slot);
    slotsByDay.set(slot.day, daySlots);
  });

  const options: ManualScheduleOption[] = [];

  Array.from(slotsByDay.entries())
    .sort(([leftDay], [rightDay]) => leftDay - rightDay)
    .forEach(([day, daySlots]) => {
      const sortedSlots = [...daySlots].sort((left, right) => left.slotOrder - right.slotOrder);

      for (let index = 0; index <= sortedSlots.length - requiredSlotCount; index += 1) {
        const window = sortedSlots.slice(index, index + requiredSlotCount);
        const isConsecutive = window.every((slot, slotIndex) =>
          slotIndex === 0 || slot.slotOrder === window[slotIndex - 1].slotOrder + 1);

        if (!isConsecutive) {
          continue;
        }

        const startTime = window[0].startTime;
        const endTime = window[window.length - 1].endTime;
        options.push({
          key: `${day}-${window[0].slotOrder}-${window[window.length - 1].slotOrder}`,
          day,
          startTime,
          endTime,
          label: `${formatSlotTime(startTime)}-${formatSlotTime(endTime)} (${requiredSlotCount} slot)`,
        });
      }
    });

  return options;
}
