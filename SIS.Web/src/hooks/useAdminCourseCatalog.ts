import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import apiClient from '../api/axios';

const BASE = '/admin/courses';

export interface CourseListItemDto {
  id: number;
  code: string;
  name: string;
  englishName: string | null;
  ects: number;
  theoryHours: number;
  labHours: number;
  isNonGpa: boolean;
  examType: string | null;
  departmentId: number | null;
  departmentName: string | null;
  prerequisiteCount: number;
  equivalencyCount: number;
  language: number;
  teachingMethod: number;
  status: number;
  level: number;
  // Müfredat agregatları
  nationalCredit: number;
  semesterMin: number | null;
  semesterMax: number | null;
  requirementType: 'Compulsory' | 'Elective' | 'Mixed' | 'None';
  // Aktif dönem section bilgisi
  isOpenInActiveTerm: boolean;
  activeTermInstructorName: string | null;
}

export interface PrerequisiteDto { courseId: number; code: string; name: string; }
export interface EquivalencyDto { courseId: number; code: string; name: string; }
export interface CurriculumAssignmentDto {
  curriculumId: number;
  programName: string;
  curriculumVersion: string;
  isActive: boolean;
  suggestedSemester: number;
  isCompulsory: boolean;
}

export interface CourseStatsDto {
  totalTimesOpened: number;
  totalStudentsAllTime: number;
  prerequisiteForCount: number;
  lastTermName: string | null;
  lastTermSectionCount: number;
  lastTermTotalEnrolled: number;
  lastTermTotalCapacity: number;
  lastTermFillRate: number;
  gradeDistribution: Record<string, number>;
  passRate: number;
  totalWithdrawn: number;
  totalDropped: number;
  termTrends: TermTrendDto[];
}

export interface TermTrendDto {
  termName: string;
  sectionCount: number;
  enrolled: number;
  capacity: number;
}

export interface PrerequisiteChainNodeDto {
  courseId: number;
  code: string;
  name: string;
  prerequisites: PrerequisiteChainNodeDto[];
}

export type CourseOpenTypeValue = 'Standart' | 'Closed' | 'PendingApproval';

export interface CourseDetailDto {
  id: number;
  code: string;
  name: string;
  englishName: string | null;
  ects: number;
  theoryHours: number;
  labHours: number;
  isNonGpa: boolean;
  examType: string | null;
  departmentId: number | null;
  departmentName: string | null;
  prerequisites: PrerequisiteDto[];
  equivalencies: EquivalencyDto[];
  curriculumAssignments: CurriculumAssignmentDto[];
  language: number;
  teachingMethod: number;
  status: number;
  description: string | null;
  level: number;
  nationalCredit: number;
  // Proliz "Diğer Ön Koşul Tanımları"
  minCreditsRequired: number | null;
  minEctsRequired: number | null;
  requiresNoFailedLowerCourses: boolean;
  canBeTakenStandalone: boolean;
  // Phase 2 — Proliz B "Genel Bilgiler"
  alternativeName: string | null;
  showInInfoPackage: boolean;
  applicationHours: number | null;
  openType: CourseOpenTypeValue | null;
  stats: CourseStatsDto | null;
  prerequisiteChain: PrerequisiteChainNodeDto[] | null;
}

export interface CourseFilterParams {
  search?: string;
  departmentId?: number;
  facultyId?: number;
  language?: number | string;
  level?: number | string;
  status?: number | string;
}

export interface CreateCourseDto {
  code: string;
  name: string;
  englishName?: string | null;
  ects: number;
  theoryHours: number;
  labHours: number;
  isNonGpa: boolean;
  examType?: string | null;
  departmentId?: number | null;
  language: number | string;
  teachingMethod: number | string;
  status: number | string;
  description?: string | null;
  level: number | string;
  nationalCredit: number;
  // Proliz "Diğer Ön Koşul Tanımları" (opsiyonel — backend default'a düşer)
  minCreditsRequired?: number | null;
  minEctsRequired?: number | null;
  requiresNoFailedLowerCourses?: boolean;
  canBeTakenStandalone?: boolean;
  // Phase 2 — Proliz B "Genel Bilgiler"
  alternativeName?: string | null;
  showInInfoPackage?: boolean;
  applicationHours?: number | null;
  openType?: CourseOpenTypeValue | null;
}

export function useAdminCourses(filter?: CourseFilterParams) {
  return useQuery({
    queryKey: ['admin', 'courses', filter?.search, filter?.departmentId, filter?.facultyId, filter?.language, filter?.level, filter?.status],
    queryFn: async () => {
      const { data } = await apiClient.get<CourseListItemDto[]>(BASE, { params: filter });
      return data;
    },
    staleTime: 2 * 60 * 1000,
  });
}

export function useAdminCourseDetail(id: number | null) {
  return useQuery({
    queryKey: ['admin', 'courses', 'detail', id],
    queryFn: async () => {
      const { data } = await apiClient.get<CourseDetailDto>(`${BASE}/${id}`);
      return data;
    },
    enabled: id !== null,
    staleTime: 60 * 1000,
  });
}

function invalidateCourses(qc: ReturnType<typeof useQueryClient>) {
  qc.invalidateQueries({ queryKey: ['admin', 'courses'] });
}

export function useCreateCourse() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (dto: CreateCourseDto) => {
      const { data } = await apiClient.post<CourseDetailDto>(BASE, dto);
      return data;
    },
    onSuccess: () => invalidateCourses(qc),
  });
}

export function useUpdateCourse() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, ...dto }: { id: number } & CreateCourseDto) => {
      const { data } = await apiClient.put<CourseDetailDto>(`${BASE}/${id}`, dto);
      return data;
    },
    onSuccess: () => invalidateCourses(qc),
  });
}

export function useDeleteCourse() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (id: number) => { await apiClient.delete(`${BASE}/${id}`); },
    onSuccess: () => invalidateCourses(qc),
  });
}

export function useAddPrerequisite() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ courseId, requiredCourseId }: { courseId: number; requiredCourseId: number }) => {
      await apiClient.post(`${BASE}/${courseId}/prerequisites`, { requiredCourseId });
    },
    onSuccess: (_, { courseId }) => {
      qc.invalidateQueries({ queryKey: ['admin', 'courses', 'detail', courseId] });
    },
  });
}

export function useRemovePrerequisite() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ courseId, requiredCourseId }: { courseId: number; requiredCourseId: number }) => {
      await apiClient.delete(`${BASE}/${courseId}/prerequisites/${requiredCourseId}`);
    },
    onSuccess: (_, { courseId }) => {
      qc.invalidateQueries({ queryKey: ['admin', 'courses', 'detail', courseId] });
    },
  });
}

export function useAddEquivalency() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ courseId, equivalentCourseId }: { courseId: number; equivalentCourseId: number }) => {
      await apiClient.post(`${BASE}/${courseId}/equivalencies`, { equivalentCourseId });
    },
    onSuccess: (_, { courseId }) => {
      qc.invalidateQueries({ queryKey: ['admin', 'courses', 'detail', courseId] });
    },
  });
}

export function useRemoveEquivalency() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ courseId, equivalentCourseId }: { courseId: number; equivalentCourseId: number }) => {
      await apiClient.delete(`${BASE}/${courseId}/equivalencies/${equivalentCourseId}`);
    },
    onSuccess: (_, { courseId }) => {
      qc.invalidateQueries({ queryKey: ['admin', 'courses', 'detail', courseId] });
    },
  });
}

// Proliz "Diğer Ön Koşul Tanımları" — sadece 4 kural alanını güncellemek için ayrı endpoint
export interface UpdateCoursePrereqRulesDto {
  minCreditsRequired: number | null;
  minEctsRequired: number | null;
  requiresNoFailedLowerCourses: boolean;
  canBeTakenStandalone: boolean;
}

export function useUpdateCoursePrereqRules() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ courseId, dto }: { courseId: number; dto: UpdateCoursePrereqRulesDto }) => {
      const { data } = await apiClient.put<CourseDetailDto>(`${BASE}/${courseId}/prereq-rules`, dto);
      return data;
    },
    onSuccess: (_, { courseId }) => {
      qc.invalidateQueries({ queryKey: ['admin', 'courses', 'detail', courseId] });
      qc.invalidateQueries({ queryKey: ['admin', 'courses'] });
    },
  });
}

// ── Madde 14: Cross-list hook'ları ──────────────────────────────
export interface CrossListItem {
  courseId: number;
  code: string;
  name: string;
  ects: number;
}

export function useCrossLists(courseId: number | null) {
  return useQuery({
    queryKey: ['admin', 'courses', 'cross-lists', courseId],
    queryFn: async () => {
      if (!courseId) return [] as CrossListItem[];
      const res = await apiClient.get<CrossListItem[]>(`${BASE}/${courseId}/cross-lists`);
      return res.data;
    },
    enabled: !!courseId,
  });
}

export function useAddCrossList() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ courseId, linkedCourseId }: { courseId: number; linkedCourseId: number }) => {
      await apiClient.post(`${BASE}/${courseId}/cross-lists/${linkedCourseId}`);
    },
    onSuccess: (_, { courseId, linkedCourseId }) => {
      qc.invalidateQueries({ queryKey: ['admin', 'courses', 'cross-lists', courseId] });
      qc.invalidateQueries({ queryKey: ['admin', 'courses', 'cross-lists', linkedCourseId] });
    },
  });
}

export function useRemoveCrossList() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ courseId, linkedCourseId }: { courseId: number; linkedCourseId: number }) => {
      await apiClient.delete(`${BASE}/${courseId}/cross-lists/${linkedCourseId}`);
    },
    onSuccess: (_, { courseId, linkedCourseId }) => {
      qc.invalidateQueries({ queryKey: ['admin', 'courses', 'cross-lists', courseId] });
      qc.invalidateQueries({ queryKey: ['admin', 'courses', 'cross-lists', linkedCourseId] });
    },
  });
}

// ── Curriculum usage — bu dersi kullanan müfredatların listesi (yan etki uyarısı için) ──
export interface CourseCurriculumUsageDto {
  curriculumId: number;
  programName: string;
  curriculumVersion: string;
  isActive: boolean;
}

export function useCourseCurriculumUsage(courseId: number | null) {
  return useQuery({
    queryKey: ['admin', 'courses', 'curriculum-usage', courseId],
    queryFn: async () => {
      const { data } = await apiClient.get<CourseCurriculumUsageDto[]>(`${BASE}/${courseId}/curriculum-usage`);
      return data;
    },
    enabled: courseId !== null,
    staleTime: 60 * 1000,
  });
}

// ── Bu dersin tüm dönemlerdeki şubeleri (Ders Programı + Öğretim Üyeleri sekmeleri) ──
export interface SectionScheduleItemDto {
  dayOfWeek: string;
  startTime: string;
  endTime: string;
  classroom: string | null;
}

export interface CourseSectionItemDto {
  sectionId: number;
  termId: number;
  termName: string;
  sectionNumber: number;
  instructorId: number | null;
  instructorFullName: string | null;
  capacity: number;
  enrolledCount: number;
  status: string;
  schedules: SectionScheduleItemDto[];
}

export function useCourseSections(courseId: number | null) {
  return useQuery({
    queryKey: ['admin', 'courses', 'sections', courseId],
    queryFn: async () => {
      const { data } = await apiClient.get<CourseSectionItemDto[]>(`${BASE}/${courseId}/sections`);
      return data;
    },
    enabled: courseId !== null,
    staleTime: 30 * 1000,
  });
}

// ── Toplu silme — frontend'de Promise.allSettled ile sıralı tek-tek silme ──
export interface BulkDeleteResultItem {
  id: number;
  success: boolean;
  message: string | null;
}

export function useBulkDeleteCourses() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (ids: number[]): Promise<BulkDeleteResultItem[]> => {
      const results = await Promise.allSettled(
        ids.map(id => apiClient.delete(`${BASE}/${id}`))
      );
      return results.map((r, i) => {
        if (r.status === 'fulfilled') {
          return { id: ids[i], success: true, message: null };
        }
        const err = r.reason as { response?: { data?: { message?: string } }; message?: string };
        return {
          id: ids[i],
          success: false,
          message: err?.response?.data?.message ?? err?.message ?? 'Hata',
        };
      });
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['admin', 'courses'] });
    },
  });
}
