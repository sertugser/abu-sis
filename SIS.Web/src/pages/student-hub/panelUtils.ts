/** Trim an ISO datetime string down to a yyyy-MM-dd value for a date input. */
export function toDateInput(value: string | null): string {
  return value ? value.slice(0, 10) : '';
}

export function toNullable(value: string): string | null {
  const trimmed = value.trim();
  return trimmed === '' ? null : trimmed;
}
