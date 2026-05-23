import { SEMESTERS } from '@/lib/calendarGenerator/semesters';
import { displayText, type CalendarProcessOption } from '@/lib/calendarGenerator/types';

export function processOptionSemesterLabel(option: CalendarProcessOption) {
  return SEMESTERS.find(s => s.value === option.semester)?.label ?? option.semester;
}

export function processOptionLabel(option: CalendarProcessOption) {
  return `${displayText(option.title)} ${processOptionSemesterLabel(option)}`;
}

export function normalizeSearch(value: string) {
  return value.trim().toLocaleLowerCase('tr-TR');
}

export function filterProcessOptions(options: CalendarProcessOption[], query: string) {
  const normalized = normalizeSearch(query);
  if (!normalized) return options;

  return options.filter(option => [
    displayText(option.title),
    displayText(option.eventTypeLabel),
    option.chainCode,
    processOptionSemesterLabel(option),
  ].some(value => normalizeSearch(value).includes(normalized)));
}

export function draftStatusBadgeVariant(status: string) {
  if (status === 'Draft') return 'secondary';
  if (status === 'Applied') return 'success';
  if (status === 'RolledBack') return 'warning';
  return 'outline';
}
