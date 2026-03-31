import apiClient from './axios';

export interface ScheduleApprovalStatusDto {
  facultyId: number;
  facultyName: string;
  termId: number;
  termName: string;
  isApproved: boolean;
  approvedAt?: string;
  approvedByName?: string;
  note?: string;
  totalSections: number;
  scheduledSections: number;
}

export async function getScheduleApprovalStatus(facultyId: number, termId?: number): Promise<ScheduleApprovalStatusDto> {
  const params = termId ? { termId } : undefined;
  const { data } = await apiClient.get<ScheduleApprovalStatusDto>(`/schedule-approval/faculty/${facultyId}/status`, { params });
  return data;
}

export async function approveSchedule(facultyId: number, note?: string): Promise<void> {
  await apiClient.post(`/schedule-approval/faculty/${facultyId}/approve`, { note });
}

export async function revokeScheduleApproval(facultyId: number): Promise<void> {
  await apiClient.delete(`/schedule-approval/faculty/${facultyId}/revoke`);
}
