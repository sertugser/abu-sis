import { useQuery } from '@tanstack/react-query';
import apiClient from '../api/axios';

export interface PoolProgressDto {
  poolId: number;
  poolCode: string;
  poolName: string;
  /** Faz C C5: aynı grubun farklı slot'larını ayırt etmek için (örn. TE-1, TE-2) */
  slotName: string;
  /** Faz C C4: "üstten yarıyıl" toggle filter'ı için yarıyıl aralığı */
  suggestedSemesterStart: number;
  suggestedSemesterEnd: number;
  requiredCourseCount: number;
  completedCourseCount: number;
  requiredEcts: number;
  earnedEcts: number;
  ectsPerCourse: number | null;
  isFulfilled: boolean;
}

export function useStudentPoolStatus(studentId: number | null) {
  return useQuery({
    queryKey: ['student', 'pool-status', studentId],
    queryFn: async () => {
      if (!studentId) return [] as PoolProgressDto[];
      const res = await apiClient.get<PoolProgressDto[]>(`/curriculum/student/${studentId}/pool-status`);
      return res.data;
    },
    enabled: !!studentId,
    staleTime: 5 * 60 * 1000,
  });
}
