// Tests deferred — no runner configured (no vitest/jest in package.json scripts).
import type { SelectedCourse } from '../types/registrationCatalog';

export type FinalizationState = 'Empty' | 'HasDraft' | 'SubmittedPending' | 'Approved';
export type AdvisorState = 'None' | 'Pending' | 'Approved' | 'Rejected';

export interface RegistrationStatusView {
  finalizationLabel: string;
  finalizationVariant: 'secondary' | 'warning' | 'success';
  advisorLabel: string;
  advisorVariant: 'secondary' | 'warning' | 'success' | 'destructive';
}

/**
 * Faz 3 — #13 fix: "Kesinleştirildi" yeşili YALNIZCA danışman onayı tamamlandığında.
 * Statuses come from SelectedCourse.status string
 * ("Draft" | "PendingAdvisorApproval" | "Active" | "Rejected").
 */
export function computeRegistrationStatus(
  selected: ReadonlyArray<SelectedCourse>,
): RegistrationStatusView {
  if (selected.length === 0) {
    return {
      finalizationLabel: '—',
      finalizationVariant: 'secondary',
      advisorLabel: '—',
      advisorVariant: 'secondary',
    };
  }

  const drafts = selected.filter(s => s.status === 'Draft' || s.status === 'Rejected');
  const pending = selected.filter(s => s.status === 'PendingAdvisorApproval');
  const active = selected.filter(s => s.status === 'Active');
  const rejected = selected.filter(s => s.status === 'Rejected');

  // Finalization
  let finalizationLabel: string;
  let finalizationVariant: 'secondary' | 'warning' | 'success';

  if (drafts.length > 0 && pending.length === 0 && active.length === 0) {
    finalizationLabel = 'Kesinleştirilmedi';
    finalizationVariant = 'secondary';
  } else if (active.length > 0 && drafts.length === 0 && pending.length === 0) {
    // Tüm derslerin onayı bitti — gerçekten kesinleştirildi
    finalizationLabel = 'Kesinleştirildi';
    finalizationVariant = 'success';
  } else {
    // Submit edildi ama henüz tamamı onaylı değil — bekliyor
    // (karma durum: bir kısmı pending + bir kısmı draft, ya da sadece pending)
    finalizationLabel = 'Onay Bekliyor';
    finalizationVariant = 'warning';
  }

  // Advisor
  let advisorLabel: string;
  let advisorVariant: 'secondary' | 'warning' | 'success' | 'destructive';

  if (rejected.length > 0) {
    advisorLabel = 'Reddedildi';
    advisorVariant = 'destructive';
  } else if (active.length === selected.length) {
    advisorLabel = 'Onaylandı';
    advisorVariant = 'success';
  } else if (pending.length > 0) {
    advisorLabel = 'Onay Bekleniyor';
    advisorVariant = 'warning';
  } else {
    advisorLabel = '—';
    advisorVariant = 'secondary';
  }

  return { finalizationLabel, finalizationVariant, advisorLabel, advisorVariant };
}
