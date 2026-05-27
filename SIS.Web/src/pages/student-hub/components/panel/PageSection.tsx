import type { ReactNode } from 'react';

interface PageSectionProps {
  title?: string;
  children: ReactNode;
}

/** A titled block of form fields laid out in a responsive two-column grid. */
export function PageSection({ title, children }: PageSectionProps) {
  return (
    <section>
      {title && <h3 className="text-sm font-semibold text-foreground mb-3">{title}</h3>}
      <div className="grid grid-cols-1 sm:grid-cols-2 gap-x-6 gap-y-4">{children}</div>
    </section>
  );
}
