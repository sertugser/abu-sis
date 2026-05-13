import apiClient from './axios';

export interface AdvisorInfo {
  hasAdvisor: boolean;
  title: string | null;
  firstName: string | null;
  lastName: string | null;
  fullName: string | null;
  facultyName: string | null;
  departmentName: string | null;
  programName: string | null;
  phone: string | null;
  email: string | null;
}

export interface AdvisorScheduleItem {
  sectionId: number;
  courseCode: string;
  courseName: string;
  sectionNumber: number;
  dayOfWeek: number;     // 1=Pzt..7=Paz
  startTime: string;     // "09:00:00"
  endTime: string;       // "09:50:00"
  classroomCode: string | null;
  blockType: string;     // "Theory" | "Lab"
}

export interface AdvisorSchedule {
  termLabel: string | null;
  items: AdvisorScheduleItem[];
}

export const getMyAdvisor = () =>
  apiClient.get<AdvisorInfo>('/advisor-info/me').then(r => r.data);

export const getAdvisorSchedule = () =>
  apiClient.get<AdvisorSchedule>('/advisor-info/schedule').then(r => r.data);
