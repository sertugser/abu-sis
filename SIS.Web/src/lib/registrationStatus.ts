// Tests deferred — no runner configured (no vitest/jest in package.json scripts).
import type { SelectedCourse } from '../types/registrationCatalog';

export type FinalizationState = 'Empty' | 'HasDraft' | 'SubmittedPending' | 'Approved';
export type AdvisorState = 'None' | 'Pending' | 'Approved' | 'Rejected';

export interface RegistrationStatusView {
  finalizationLabel: string;
  finalizationVariant: 'neutralSoft' | 'warningSoft' | 'successSoft';
  advisorLabel: string;
  advisorVariant: 'neutralSoft' | 'warningSoft' | 'successSoft' | 'dangerSoft';
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
      finalizationVariant: 'neutralSoft',
      advisorLabel: '—',
      advisorVariant: 'neutralSoft',
    };
  }

  const drafts = selected.filter(s => s.status === 'Draft' || s.status === 'Rejected');
  const pending = selected.filter(s => s.status === 'PendingAdvisorApproval');
  const active = selected.filter(s => s.status === 'Active');
  const rejected = selected.filter(s => s.status === 'Rejected');

  // Finalization
  let finalizationLabel: string;
  let finalizationVariant: 'neutralSoft' | 'warningSoft' | 'successSoft';

  if (drafts.length > 0 && pending.length === 0 && active.length === 0) {
    finalizationLabel = 'Kesinleştirilmedi';
    finalizationVariant = 'neutralSoft';
  } else if (active.length > 0 && drafts.length === 0 && pending.length === 0) {
    // Tüm derslerin onayı bitti — gerçekten kesinleştirildi
    finalizationLabel = 'Kesinleştirildi';
    finalizationVariant = 'successSoft';
  } else {
    // Submit edildi ama henüz tamamı onaylı değil — bekliyor
    // (karma durum: bir kısmı pending + bir kısmı draft, ya da sadece pending)
    finalizationLabel = 'Onay Bekliyor';
    finalizationVariant = 'warningSoft';
  }

  // Advisor
  let advisorLabel: string;
  let advisorVariant: 'neutralSoft' | 'warningSoft' | 'successSoft' | 'dangerSoft';

  if (rejected.length > 0) {
    advisorLabel = 'Reddedildi';
    advisorVariant = 'dangerSoft';
  } else if (active.length === selected.length) {
    advisorLabel = 'Onaylandı';
    advisorVariant = 'successSoft';
  } else if (pending.length > 0) {
    advisorLabel = 'Onay Bekleniyor';
    advisorVariant = 'warningSoft';
  } else {
    advisorLabel = '—';
    advisorVariant = 'neutralSoft';
  }

  return { finalizationLabel, finalizationVariant, advisorLabel, advisorVariant };
}
