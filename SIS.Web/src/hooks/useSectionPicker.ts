import { useQuery } from '@tanstack/react-query';
import { getSectionPicker } from '../api/registrationCatalog.api';
import { QUERY_KEYS } from '../lib/constants';
import type { SectionPickerQuery, SectionPickerResult } from '../types/registrationCatalog';

/**
 * "+"a basınca açılan şube picker'ı besler. `enabled` ile dialog kapalıyken sorgu çalıştırmaz.
 */
export function useSectionPicker(courseId: number | null, query: SectionPickerQuery, enabled: boolean) {
  return useQuery<SectionPickerResult>({
    queryKey: QUERY_KEYS.sectionPicker(courseId ?? 0, query),
    queryFn: () => getSectionPicker(courseId!, query),
    enabled: enabled && courseId !== null,
    staleTime: 30_000,
  });
}
