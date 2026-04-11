import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  getPromotionRules,
  createPromotionRule,
  updatePromotionRule,
  deletePromotionRule,
  type CreatePromotionRuleDto,
  type UpdatePromotionRuleDto,
} from '../api/promotionRule.api';
import { QUERY_KEYS } from '../lib/constants';

export function usePromotionRules(programId?: number) {
  return useQuery({
    queryKey: QUERY_KEYS.promotionRules(programId),
    queryFn: () => getPromotionRules(programId),
    staleTime: 60 * 1000,
  });
}

export function useCreatePromotionRule() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (dto: CreatePromotionRuleDto) => createPromotionRule(dto),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['admin', 'promotionRules'] });
    },
  });
}

export function useUpdatePromotionRule() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, dto }: { id: number; dto: UpdatePromotionRuleDto }) => updatePromotionRule(id, dto),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['admin', 'promotionRules'] });
    },
  });
}

export function useDeletePromotionRule() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: number) => deletePromotionRule(id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['admin', 'promotionRules'] });
    },
  });
}
