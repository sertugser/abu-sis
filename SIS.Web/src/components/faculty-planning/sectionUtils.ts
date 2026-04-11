import type { FacultyPlanningTimeSlot } from '../../types/facultyPlanning';

export const STATUS_CONFIG: Record<string, { label: string; variant: 'default' | 'success' | 'destructive' | 'warning' | 'outline' }> = {
  Unscheduled: { label: 'Zamanlanmamış', variant: 'outline' },
  Scheduled: { label: 'Zamanlanmış', variant: 'success' },
  Conflicted: { label: 'Çakışmalı', variant: 'destructive' },
  Partial: { label: 'Kısmi', variant: 'warning' },
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
