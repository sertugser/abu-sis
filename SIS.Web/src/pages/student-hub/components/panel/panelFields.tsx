import type { ReactNode } from 'react';

/** Shared form-control styling, copied verbatim from the original panels. */
export const INPUT_CLASS =
  'mt-1 w-full rounded-md border border-input bg-background px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-ring disabled:cursor-not-allowed disabled:opacity-60';

export const SELECT_CLASS = INPUT_CLASS;

export const FIELD_LABEL = 'text-xs text-muted-foreground font-medium uppercase tracking-wide';

interface SummaryItemProps {
  label: string;
  children: ReactNode;
}

/** Read-only label/value row for the right summary column. */
export function SummaryItem({ label, children }: SummaryItemProps) {
  return (
    <div className="border-b border-border/60 pb-2 last:border-b-0 last:pb-0">
      <p className={FIELD_LABEL}>{label}</p>
      <div className="mt-0.5 text-sm text-foreground">{children}</div>
    </div>
  );
}

interface MockFieldProps {
  label: string;
  value?: ReactNode;
}

/**
 * Display-only field for not-yet-wired data. Never part of form submission.
 * The dashed underline + tooltip signal that the value is informational only.
 */
export function MockField({ label, value }: MockFieldProps) {
  return (
    <div title="Bilgi amaçlı — kaydedilmez">
      <span className={`${FIELD_LABEL} inline-flex items-center gap-1`}>
        {label}
        <span aria-hidden="true" className="h-1 w-1 rounded-full bg-muted-foreground/40" />
      </span>
      <div className="mt-1 border-b border-dashed border-border/60 pb-2 text-sm text-muted-foreground">
        {value ?? '—'}
      </div>
    </div>
  );
}
