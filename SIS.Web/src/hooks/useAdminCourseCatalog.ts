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
  nationalCredit: number | null;
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
  nationalCredit?: number | null;
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
