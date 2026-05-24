export function cgpaColor(cgpa: number | null): string {
  if (cgpa === null) return 'text-muted-foreground';
  if (cgpa >= 3.0) return 'text-green-600';
  if (cgpa >= 2.0) return 'text-yellow-600';
  return 'text-red-600';
}
