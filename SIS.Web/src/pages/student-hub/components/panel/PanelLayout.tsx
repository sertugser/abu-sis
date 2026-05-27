import type { ReactNode } from 'react';

interface PanelLayoutProps {
  children: ReactNode;
  aside?: ReactNode;
}

/**
 * Two-column panel page: editable main content on the left, optional read-only
 * summary column on the right. Stacks to a single column below the xl breakpoint;
 * when no aside is provided the main content spans the full width.
 */
export function PanelLayout({ children, aside }: PanelLayoutProps) {
  if (!aside) {
    return <div>{children}</div>;
  }
  return (
    <div className="grid grid-cols-1 gap-6 xl:grid-cols-[minmax(0,1fr)_340px]">
      <div>{children}</div>
      <div>{aside}</div>
    </div>
  );
}
