import { type ReactNode } from 'react';
import { Info } from 'lucide-react';

interface Props {
  /** Whether this field is shared across curricula (Course table) or curriculum-specific (CurriculumCourse table) */
  ownership: 'shared' | 'curriculum-specific';
  /** When true (Faz 1C), shows a small info hint that side-effect warning will appear at save time */
  showSharedHint?: boolean;
  children: ReactNode;
}

/**
 * Wraps a form field with ownership metadata.
 * - 'shared' → field lives in Course table; saving may affect other curricula
 * - 'curriculum-specific' → field lives in CurriculumCourse table; only this curriculum is affected
 *
 * Faz 1C will use this metadata to decide which fields trigger the side-effect confirmation dialog.
 */
export default function SharedFieldWrapper({ ownership, showSharedHint = false, children }: Props) {
  return (
    <div className="space-y-1">
      {children}
      {ownership === 'shared' && showSharedHint && (
        <p className="text-[11px] text-muted-foreground flex items-center gap-1">
          <Info className="h-3 w-3" />
          Paylaşımlı alan — değişiklik dersi kullanan tüm müfredatları etkiler.
        </p>
      )}
    </div>
  );
}
