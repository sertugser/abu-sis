import { useMutation, useQuery, useQueryClient, type QueryClient } from '@tanstack/react-query';
import {
  getClassrooms,
  createClassroom,
  updateClassroom,
  deleteClassroom,
  type CreateClassroomDto,
  type UpdateClassroomDto,
} from '../api/classroom.api';

const CLASSROOMS_KEY = ['admin', 'classrooms'] as const;

// Derslik mutasyonu, dersliği tüketen diğer sayfaların da cache'ini bayatlatır:
// doluluk dashboard'u, fakülte atamaları, scheduling filtre/çakışma görünümleri.
// Bu key prefix'leri ilgili sayfa hook'larıyla aynı olmalı (useScheduling, useClassroomAssignment).
const DEPENDENT_KEYS: readonly (readonly unknown[])[] = [
  ['admin', 'classroomAssignments'],
  ['classroomDashboard'],
  ['classroomFilterOptions'],
  ['availableClassrooms'],
  ['schedulingConflicts'],
];

function invalidateClassroomCaches(qc: QueryClient) {
  qc.invalidateQueries({ queryKey: CLASSROOMS_KEY });
  for (const key of DEPENDENT_KEYS) {
    qc.invalidateQueries({ queryKey: key });
  }
}

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
    onSuccess: () => invalidateClassroomCaches(qc),
  });
}

export function useUpdateClassroom() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, dto }: { id: number; dto: UpdateClassroomDto }) => updateClassroom(id, dto),
    onSuccess: () => invalidateClassroomCaches(qc),
  });
}

export function useDeleteClassroom() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: number) => deleteClassroom(id),
    onSuccess: () => invalidateClassroomCaches(qc),
  });
}
