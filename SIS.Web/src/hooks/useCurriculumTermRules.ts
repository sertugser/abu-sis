import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { curriculumApi } from '../api/curriculum.api';
import type {
  CreateTermRuleRequest,
  UpdateTermRuleRequest,
} from '../types/curriculumTermRule';

const KEYS = {
  list: (curriculumId: number) => ['curriculum', 'termRules', curriculumId] as const,
};

export function useCurriculumTermRules(curriculumId: number | null) {
  return useQuery({
    queryKey: KEYS.list(curriculumId ?? 0),
    queryFn: () => curriculumApi.listTermRules(curriculumId!),
    enabled: curriculumId !== null,
    staleTime: 60 * 1000,
  });
}

export function useCreateTermRule() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ curriculumId, ...req }: { curriculumId: number } & CreateTermRuleRequest) =>
      curriculumApi.createTermRule(curriculumId, req),
    onSuccess: (_data, { curriculumId }) => {
      qc.invalidateQueries({ queryKey: KEYS.list(curriculumId) });
    },
  });
}

export function useUpdateTermRule() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ curriculumId, ruleId, ...req }: { curriculumId: number; ruleId: number } & UpdateTermRuleRequest) =>
      curriculumApi.updateTermRule(curriculumId, ruleId, req),
    onSuccess: (_data, { curriculumId }) => {
      qc.invalidateQueries({ queryKey: KEYS.list(curriculumId) });
    },
  });
}

export function useDeleteTermRule() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ curriculumId, ruleId }: { curriculumId: number; ruleId: number }) =>
      curriculumApi.deleteTermRule(curriculumId, ruleId),
    onSuccess: (_data, { curriculumId }) => {
      qc.invalidateQueries({ queryKey: KEYS.list(curriculumId) });
    },
  });
}
