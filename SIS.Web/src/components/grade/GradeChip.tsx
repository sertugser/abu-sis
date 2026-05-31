// SIS.Web/src/components/grade/GradeChip.tsx
// Harf notu rozeti.

import { GRADE_STYLE } from './gradeDisplay';

export function GradeChip({ grade }: { grade: string }) {
  const style = GRADE_STYLE[grade];
  if (!style) return <span className="text-muted-foreground">—</span>;
  return (
    <span className={`inline-flex items-center justify-center min-w-[2.5rem] px-2.5 py-1 rounded-md text-xs font-bold font-mono ${style.bg} ${style.text}`}>
      {grade}
    </span>
  );
}
