import apiClient from './axios';
import { toApiError } from '../lib/apiError';
import type {
  StudentRegistrationCatalog,
  SectionPickerResult,
  SectionPickerQuery,
} from '../types/registrationCatalog';
import type { ValidationResultDto } from '../types/registration';

/** GET /course/registration-catalog — yeni müfredat-ağacı katalog (Faz 2). */
export async function getRegistrationCatalog(): Promise<StudentRegistrationCatalog> {
  try {
    const r = await apiClient.get<StudentRegistrationCatalog>('/course/registration-catalog');
    return r.data;
  } catch (error) {
    throw toApiError(error, 'Ders kataloğu yüklenemedi.');
  }
}

/** GET /course/{courseId}/sections — "+"a basınca lazy şube listesi (filtreli + sayfalı). */
export async function getSectionPicker(
  courseId: number,
  query: SectionPickerQuery = {},
): Promise<SectionPickerResult> {
  try {
    const r = await apiClient.get<SectionPickerResult>(`/course/${courseId}/sections`, { params: query });
    return r.data;
  } catch (error) {
    throw toApiError(error, 'Şubeler yüklenemedi.');
  }
}

/** POST /registrations/validate — read-only "Kontrol Et"; mutasyon yapmaz. */
export async function validateRegistration(courseSectionIds: number[]): Promise<ValidationResultDto> {
  try {
    const r = await apiClient.post<ValidationResultDto>('/registrations/validate', { courseSectionIds });
    return r.data;
  } catch (error) {
    throw toApiError(error, 'Doğrulama yapılamadı.');
  }
}

/** GET /registrations/plan/pdf — "Yazdır" (seçilen dersler PDF). */
export async function downloadRegistrationPlanPdf(): Promise<Blob> {
  try {
    const r = await apiClient.get<Blob>('/registrations/plan/pdf', { responseType: 'blob' });
    return r.data;
  } catch (error) {
    throw toApiError(error, 'PDF oluşturulamadı.');
  }
}
