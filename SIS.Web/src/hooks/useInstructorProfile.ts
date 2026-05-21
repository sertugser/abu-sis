import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  createAcademicCvActivity,
  getAcademicCv,
  getInstructorProfile,
  updateAcademicCvActivity,
  updateInstructorProfile,
  type CreateAcademicCvActivityDto,
  type UpdateInstructorProfileDto,
} from '../api/instructor.api';

export function useInstructorProfile() {
  return useQuery({
    queryKey: ['instructorProfile'],
    queryFn: getInstructorProfile,
    staleTime: 5 * 60 * 1000,
  });
}

export function useUpdateInstructorProfile() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (dto: UpdateInstructorProfileDto) => updateInstructorProfile(dto),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['instructorProfile'] });
      queryClient.invalidateQueries({ queryKey: ['admin', 'lookup', 'advisors'] });
    },
  });
}

export function useInstructorAcademicCv() {
  return useQuery({
    queryKey: ['instructorAcademicCv'],
    queryFn: getAcademicCv,
    staleTime: 60 * 1000,
  });
}

export function useCreateAcademicCvActivity() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (dto: CreateAcademicCvActivityDto) => createAcademicCvActivity(dto),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['instructorAcademicCv'] });
    },
  });
}

export function useUpdateAcademicCvActivity() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, dto }: { id: number; dto: CreateAcademicCvActivityDto }) => updateAcademicCvActivity(id, dto),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['instructorAcademicCv'] });
    },
  });
}
