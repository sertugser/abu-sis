import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  getClassrooms,
  createClassroom,
  updateClassroom,
  deleteClassroom,
  type CreateClassroomDto,
} from '../api/classroom.api';

const CLASSROOMS_KEY = ['admin', 'classrooms'] as const;

export function useClassrooms(type?: string) {
  return useQuery({
    queryKey: [...CLASSROOMS_KEY, type],
    queryFn: () => getClassrooms(type),
    staleTime: 60 * 1000,
  });
}

export function useCreateClassroom() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (dto: CreateClassroomDto) => createClassroom(dto),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: CLASSROOMS_KEY });
    },
  });
}

export function useUpdateClassroom() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, dto }: { id: number; dto: CreateClassroomDto }) => updateClassroom(id, dto),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: CLASSROOMS_KEY });
    },
  });
}

export function useDeleteClassroom() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: number) => deleteClassroom(id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: CLASSROOMS_KEY });
    },
  });
}
