// SIS.Web/src/components/grade/gradeDisplay.ts
// Not giriş ekranı için paylaşılan sabitler ve harf notu çözümleme yardımcısı.

import type { ExamAttendance, GradeComponentType, GradingCriteria } from '../../types/grade';

export const COMPONENT_TYPE_LABELS: Record<GradeComponentType, string> = {
  Other: 'Diğer',
  Midterm: 'Ara Sınav',
  Final: 'Final',
};

export const COMPONENT_TYPE_BADGE_CLASS: Record<GradeComponentType, string> = {
  Other: 'bg-slate-100 text-slate-600',
  Midterm: 'bg-blue-50 text-blue-600',
  Final: 'bg-purple-50 text-purple-700',
};

export const ATTENDANCE_OPTIONS: { value: ExamAttendance; label: string }[] = [
  { value: 'NotMarked', label: '—' },
  { value: 'Present', label: 'Girdi' },
  { value: 'Absent', label: 'Girmedi' },
  { value: 'Excused', label: 'Mazeretli' },
];

export const GRADE_STYLE: Record<string, { bg: string; text: string }> = {
  AA: { bg: 'bg-emerald-100', text: 'text-emerald-700' },
  BA: { bg: 'bg-emerald-50', text: 'text-emerald-600' },
  BB: { bg: 'bg-blue-50', text: 'text-blue-600' },
  CB: { bg: 'bg-blue-50', text: 'text-blue-500' },
  CC: { bg: 'bg-sky-50', text: 'text-sky-600' },
  DC: { bg: 'bg-amber-50', text: 'text-amber-600' },
  DD: { bg: 'bg-orange-50', text: 'text-orange-600' },
  FF: { bg: 'bg-red-50', text: 'text-red-600' },
  NA: { bg: 'bg-red-50', text: 'text-red-600' },
};

export const LETTER_ORDER = ['AA', 'BA', 'BB', 'CB', 'CC', 'DC', 'DD', 'FF', 'NA'] as const;

export function resolveGrade(score: number, criteria: GradingCriteria): string {
  if (score >= criteria.aaMin) return 'AA';
  if (score >= criteria.baMin) return 'BA';
  if (score >= criteria.bbMin) return 'BB';
  if (score >= criteria.cbMin) return 'CB';
  if (score >= criteria.ccMin) return 'CC';
  if (score >= criteria.dcMin) return 'DC';
  if (score >= criteria.ddMin) return 'DD';
  return 'FF';
}
