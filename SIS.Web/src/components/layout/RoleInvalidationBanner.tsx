import { useEffect } from 'react';
import { AlertTriangle, X } from 'lucide-react';
import { getInvalidationMessage, useActiveRole } from '../../context/ActiveRoleContext';

const AUTO_DISMISS_MS = 8000;

export default function RoleInvalidationBanner() {
  const { invalidationNotice, dismissNotice } = useActiveRole();

  useEffect(() => {
    if (!invalidationNotice) return;
    const id = setTimeout(dismissNotice, AUTO_DISMISS_MS);
    return () => clearTimeout(id);
  }, [invalidationNotice, dismissNotice]);

  if (!invalidationNotice) return null;

  return (
    <div
      role="status"
      className="sticky top-20 z-30 mx-6 lg:mx-8 mt-2 flex items-start gap-3 rounded-lg border border-amber-300/60 bg-amber-50 px-4 py-2.5 text-sm text-amber-900 shadow-sm"
    >
      <AlertTriangle className="h-4 w-4 mt-0.5 shrink-0 text-amber-600" />
      <p className="flex-1 leading-snug">{getInvalidationMessage(invalidationNotice)}</p>
      <button
        type="button"
        onClick={dismissNotice}
        className="shrink-0 rounded p-0.5 text-amber-700 hover:bg-amber-100 hover:text-amber-900 transition-colors"
        aria-label="Bildirimi kapat"
      >
        <X className="h-4 w-4" />
      </button>
    </div>
  );
}
