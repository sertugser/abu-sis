import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  getOpenedCourses,
  getCurriculumCoursesForOpening,
  getDepartmentInstructors,
  getInstructorWorkload,
  getExternalCourses,
  getPoolSlotsForOpening,
  getPoolCandidates,
  openCourse,
  bulkOpenCourses,
  updateOpenedSection,
  closeSection,
  closeAllSections,
  deleteAllSectionsHard,
  reactivateSection,
  bulkAssignInstructor,
  getPreviousTermCourses,
  transferFromTerm,
  getTransferDefaults,
} from '../api/courseOpening.api';
import { QUERY_KEYS } from '../lib/constants';
import { useToast } from '../components/ui/global-toast';
import type { OpenCourseRequest, BulkOpenCoursesRequest, UpdateOpenedSectionRequest, BulkAssignInstructorRequest, TransferFromTermRequest } from '../types/courseOpening';

/**
 * Backend'den dönen hata mesajını standart şekilde çıkarır.
 * ASP.NET BadRequest({ error: string }) → axios error.response.data.error.
 * Network/5xx → error.message.
 */
function getApiErrorMessage(error: unknown): string {
  const e = error as {
    response?: { data?: { error?: string; title?: string; detail?: string } };
    message?: string;
  };
  return (
    e?.response?.data?.error ??
    e?.response?.data?.detail ??
    e?.response?.data?.title ??
    e?.message ??
    'Bilinmeyen hata'
  );
}

export function useOpenedCourses(departmentId: number | null, termId: number | null) {
  return useQuery({
    queryKey: QUERY_KEYS.courseOpeningList(departmentId ?? 0, termId ?? 0),
    queryFn: () => getOpenedCourses(departmentId!, termId!),
    enabled: departmentId !== null && termId !== null,
    staleTime: 30 * 1000,
  });
}

export function useCurriculumCoursesForOpening(
  departmentId: number | null,
  termId: number | null,
  showAll = false,
  curriculumId: number | null = null,
  includeUnmapped = false,
) {
  return useQuery({
    queryKey: [
      ...QUERY_KEYS.curriculumCoursesForOpening(departmentId ?? 0, termId ?? 0),
      showAll,
      curriculumId,
      includeUnmapped,
    ],
    queryFn: () => getCurriculumCoursesForOpening(departmentId!, termId!, { showAll, curriculumId, includeUnmapped }),
    enabled: departmentId !== null && termId !== null,
    staleTime: 60 * 1000,
  });
}

export function usePoolSlotsForOpening(curriculumId: number | null, termId: number | null) {
  return useQuery({
    queryKey: ['courseOpening', 'poolSlots', curriculumId, termId],
    queryFn: () => getPoolSlotsForOpening(curriculumId!, termId!),
    enabled: curriculumId !== null && termId !== null,
    staleTime: 60 * 1000,
  });
}

export function usePoolCandidates(poolId: number | null, termId: number | null) {
  return useQuery({
    queryKey: ['courseOpening', 'poolCandidates', poolId, termId],
    queryFn: () => getPoolCandidates(poolId!, termId!),
    enabled: poolId !== null && termId !== null,
    staleTime: 30 * 1000,
  });
}

export function useDepartmentInstructors(departmentId: number | null, termId?: number | null) {
  return useQuery({
    queryKey: [...QUERY_KEYS.departmentInstructors(departmentId ?? 0), termId],
    queryFn: () => getDepartmentInstructors(departmentId!, termId ?? undefined),
    enabled: departmentId !== null,
    staleTime: 30 * 1000,
  });
}

export function useInstructorWorkload(instructorId: number | null, termId: number | null) {
  return useQuery({
    queryKey: ['courseOpening', 'workload', instructorId, termId],
    queryFn: () => getInstructorWorkload(instructorId!, termId!),
    enabled: instructorId !== null && instructorId > 0 && termId !== null,
    staleTime: 30 * 1000,
  });
}

export function useExternalCourses(departmentId: number | null, termId: number | null) {
  return useQuery({
    queryKey: ['courseOpening', 'externalCourses', departmentId, termId],
    queryFn: () => getExternalCourses(departmentId!, termId!),
    enabled: departmentId !== null && termId !== null,
    staleTime: 60 * 1000,
  });
}

export function useOpenCourse(departmentId: number, termId: number) {
  const queryClient = useQueryClient();
  const { showSuccess, showError } = useToast();
  return useMutation({
    mutationFn: (request: OpenCourseRequest) => openCourse(departmentId, termId, request),
    onSuccess: (sections) => {
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.courseOpeningList(departmentId, termId) });
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.curriculumCoursesForOpening(departmentId, termId) });
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.forecastDepartmentSummary(departmentId) });
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.academicPlanningSummary(departmentId, termId) });
      const count = sections?.length ?? 0;
      if (count > 0) {
        const first = sections[0];
        const detail = count === 1
          ? `${first.courseCode} — Şube ${first.sectionNumber}`
          : `${first.courseCode} için ${count} şube`;
        showSuccess('Ders başarıyla açıldı', detail);
      } else {
        showSuccess('Ders başarıyla açıldı');
      }
    },
    onError: (error) => {
      showError('Ders açılamadı', getApiErrorMessage(error));
    },
  });
}

export function useBulkOpenCourses(departmentId: number, termId: number) {
  const queryClient = useQueryClient();
  const { showError } = useToast();
  return useMutation({
    mutationFn: (request: BulkOpenCoursesRequest) => bulkOpenCourses(departmentId, termId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.courseOpeningList(departmentId, termId) });
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.curriculumCoursesForOpening(departmentId, termId) });
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.forecastDepartmentSummary(departmentId) });
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.academicPlanningSummary(departmentId, termId) });
      // Detaylı sonuç sayfa banner'ında render ediliyor (BulkOpenResultBanner). Toast yok — çift bildirim olmasın.
    },
    onError: (error) => {
      showError('Toplu ders açma başarısız', getApiErrorMessage(error));
    },
  });
}

export function useUpdateOpenedSection(departmentId: number, termId: number) {
  const queryClient = useQueryClient();
  const { showSuccess, showError } = useToast();
  return useMutation({
    mutationFn: (request: UpdateOpenedSectionRequest) => updateOpenedSection(request),
    onSuccess: (section) => {
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.courseOpeningList(departmentId, termId) });
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.forecastDepartmentSummary(departmentId) });
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.academicPlanningSummary(departmentId, termId) });
      queryClient.invalidateQueries({ queryKey: ['courseOpening', 'workload'] });
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.departmentInstructors(departmentId) });
      const detail = section
        ? `${section.courseCode} — Şube ${section.sectionNumber}`
        : undefined;
      showSuccess('Şube güncellendi', detail);
    },
    onError: (error) => {
      showError('Şube güncellenemedi', getApiErrorMessage(error));
    },
  });
}

export function useBulkAssignInstructor(departmentId: number, termId: number) {
  const queryClient = useQueryClient();
  const { showSuccess, showError } = useToast();
  return useMutation({
    mutationFn: (request: BulkAssignInstructorRequest) => bulkAssignInstructor(departmentId, termId, request),
    onSuccess: (result) => {
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.courseOpeningList(departmentId, termId) });
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.academicPlanningSummary(departmentId, termId) });
      queryClient.invalidateQueries({ queryKey: ['courseOpening', 'workload'] });
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.departmentInstructors(departmentId) });
      if (result) {
        const ok = result.successCount ?? 0;
        const fail = result.failedCount ?? 0;
        if (fail === 0) {
          showSuccess('Toplu atama tamamlandı', `${ok} şubeye hoca atandı.`);
        } else {
          showError(
            'Toplu atama kısmen tamamlandı',
            `${ok} başarılı, ${fail} hata. Detay: ${(result.errors ?? []).slice(0, 2).join(' • ')}`,
          );
        }
      }
    },
    onError: (error) => {
      showError('Toplu hoca atama başarısız', getApiErrorMessage(error));
    },
  });
}

export function usePreviousTermCourses(
  departmentId: number | null,
  sourceTermId: number | null,
  targetTermId: number | null,
  filter?: { programId?: number | null; classLevel?: number | null },
) {
  return useQuery({
    queryKey: [
      'courseOpening',
      'previousTerm',
      departmentId,
      sourceTermId,
      targetTermId,
      filter?.programId ?? null,
      filter?.classLevel ?? null,
    ],
    queryFn: () =>
      getPreviousTermCourses(departmentId!, sourceTermId!, targetTermId!, {
        programId: filter?.programId,
        classLevel: filter?.classLevel,
      }),
    enabled: departmentId !== null && sourceTermId !== null && targetTermId !== null,
    staleTime: 60 * 1000,
  });
}

export function useTransferFromTerm(departmentId: number, targetTermId: number) {
  const queryClient = useQueryClient();
  const { showError } = useToast();
  return useMutation({
    mutationFn: (request: TransferFromTermRequest) => transferFromTerm(request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.courseOpeningList(departmentId, targetTermId) });
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.curriculumCoursesForOpening(departmentId, targetTermId) });
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.forecastDepartmentSummary(departmentId) });
      // Detay sayfada TransferResultBanner ile gösteriliyor — toast yok.
    },
    onError: (error) => {
      showError('Aktarım başarısız', getApiErrorMessage(error));
    },
  });
}

export function useTransferDefaults() {
  return useQuery({
    queryKey: ['courseOpening', 'transferDefaults'],
    queryFn: getTransferDefaults,
    staleTime: 5 * 60 * 1000,
  });
}

export function useCloseSection(departmentId: number, termId: number) {
  const queryClient = useQueryClient();
  const { showSuccess, showError } = useToast();
  return useMutation({
    mutationFn: (sectionId: number) => closeSection(sectionId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.courseOpeningList(departmentId, termId) });
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.curriculumCoursesForOpening(departmentId, termId) });
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.forecastDepartmentSummary(departmentId) });
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.academicPlanningSummary(departmentId, termId) });
      showSuccess('Şube kapatıldı');
    },
    onError: (error) => {
      showError('Şube kapatılamadı', getApiErrorMessage(error));
    },
  });
}

export function useCloseAllSections(departmentId: number, termId: number) {
  const queryClient = useQueryClient();
  const { showSuccess, showError } = useToast();
  return useMutation({
    mutationFn: (courseId: number) => closeAllSections(courseId, termId),
    onSuccess: (result) => {
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.courseOpeningList(departmentId, termId) });
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.curriculumCoursesForOpening(departmentId, termId) });
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.forecastDepartmentSummary(departmentId) });
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.academicPlanningSummary(departmentId, termId) });
      const closed = result?.closedSectionCount ?? 0;
      const dropped = result?.droppedRegistrationCount ?? 0;
      const detail = dropped > 0
        ? `${closed} şube kapatıldı, ${dropped} kayıt iptal edildi.`
        : `${closed} şube kapatıldı.`;
      showSuccess('Tüm şubeler kapatıldı', detail);
    },
    onError: (error) => {
      showError('Şubeler kapatılamadı', getApiErrorMessage(error));
    },
  });
}

/**
 * Hard-delete: Dersin tüm şubelerini DB'den fiziksel olarak siler.
 * useCloseAllSections'tan farkı: geri alınamaz, ders "hiç açılmamış" gibi olur.
 * Backend safety: aktif/tamamlanmış kayıt, değerlendirme veya yoklama varsa reddedilir.
 */
export function useDeleteAllSectionsHard(departmentId: number, termId: number) {
  const queryClient = useQueryClient();
  const { showSuccess, showError } = useToast();
  return useMutation({
    mutationFn: (courseId: number) => deleteAllSectionsHard(courseId, termId),
    onSuccess: (result) => {
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.courseOpeningList(departmentId, termId) });
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.curriculumCoursesForOpening(departmentId, termId) });
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.forecastDepartmentSummary(departmentId) });
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.academicPlanningSummary(departmentId, termId) });
      const deleted = result?.deletedSectionCount ?? 0;
      const detail =
        deleted === 0
          ? 'Bu dönemden kaldırıldı.'
          : deleted === 1
            ? '1 şube bu dönemden kaldırıldı.'
            : `${deleted} şube bu dönemden kaldırıldı.`;
      showSuccess('Ders bu dönemden kaldırıldı', detail);
    },
    onError: (error) => {
      showError('Ders kaldırılamadı', getApiErrorMessage(error));
    },
  });
}

export function useReactivateSection(departmentId: number, termId: number) {
  const queryClient = useQueryClient();
  const { showSuccess, showError } = useToast();
  return useMutation({
    mutationFn: (sectionId: number) => reactivateSection(sectionId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.courseOpeningList(departmentId, termId) });
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.curriculumCoursesForOpening(departmentId, termId) });
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.forecastDepartmentSummary(departmentId) });
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.academicPlanningSummary(departmentId, termId) });
      showSuccess('Şube yeniden aktifleştirildi');
    },
    onError: (error) => {
      showError('Şube yeniden açılamadı', getApiErrorMessage(error));
    },
  });
}
