import type { ReactNode } from 'react';

interface SummaryColumnProps {
  title?: string;
  children: ReactNode;
}

/** Read-only "identity / summary" card for the right column of a panel page. */
export function SummaryColumn({ title = 'Kimlik / Özet', children }: SummaryColumnProps) {
  return (
    <aside className="rounded-xl border border-border bg-muted/20 p-4">
      <h3 className="text-sm font-semibold mb-3">{title}</h3>
      <div className="space-y-2.5">{children}</div>
    </aside>
  );
}
