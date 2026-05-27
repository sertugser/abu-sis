import { type ClassValue, clsx } from "clsx"
import { twMerge } from "tailwind-merge"

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs))
}

export function getEctsLimit(cgpa: number): number {
  if (cgpa >= 2.50) return 48;
  if (cgpa >= 2.00) return 42;
  return 30;
}

export function formatDate(dateStr: string): string {
  const date = new Date(dateStr);
  return date.toLocaleDateString('tr-TR', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
  });
}

/** Tarih + saat (Proliz "30.09.2020 00:18:03" formatına yakın). */
export function formatDateTime(dateStr: string): string {
  const date = new Date(dateStr);
  return date.toLocaleString('tr-TR', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
}
