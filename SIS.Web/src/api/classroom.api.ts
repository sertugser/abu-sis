import apiClient from './axios';

export interface ClassroomDto {
  id: number;
  name: string;
  buildingName: string;
  floor: number;
  capacity: number;
  type: string;
}

export interface CreateClassroomDto {
  name: string;
  buildingName: string;
  floor: number;
  capacity: number;
  type: string;
}

export async function getClassrooms(type?: string): Promise<ClassroomDto[]> {
  const params = type ? { type } : undefined;
  const { data } = await apiClient.get<ClassroomDto[]>('/admin/classrooms', { params });
  return data;
}

export async function getClassroom(id: number): Promise<ClassroomDto> {
  const { data } = await apiClient.get<ClassroomDto>(`/admin/classrooms/${id}`);
  return data;
}

export async function createClassroom(dto: CreateClassroomDto): Promise<ClassroomDto> {
  const { data } = await apiClient.post<ClassroomDto>('/admin/classrooms', dto);
  return data;
}

export async function updateClassroom(id: number, dto: CreateClassroomDto): Promise<ClassroomDto> {
  const { data } = await apiClient.put<ClassroomDto>(`/admin/classrooms/${id}`, dto);
  return data;
}

export async function deleteClassroom(id: number): Promise<void> {
  await apiClient.delete(`/admin/classrooms/${id}`);
}
