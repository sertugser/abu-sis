import apiClient from './axios';

export interface InstructorProfile {
  instructorId: number;
  title: string;
  firstName: string;
  lastName: string;
  email: string;
  departmentName: string;
  facultyName: string;
  maxWeeklyHours: number;
}

export interface InstructorSection {
  sectionId: number;
  courseCode: string;
  courseName: string;
  ects: number;
  theoryHours: number;
  labHours: number;
  capacity: number;
  enrolledCount: number;
  availableSeats: number;
  classroomName: string;
  schedule: string;
  isActive: boolean;
  activeStudents: number;
}

export interface InstructorSectionsResponse {
  term: string;
  totalSections: number;
  totalStudents: number;
  sections: InstructorSection[];
}

export interface SectionStudent {
  studentId: number;
  studentNumber: string;
  firstName: string;
  lastName: string;
  email: string;
  status: string;
  grade: string | null;
}

export interface SectionStudentsResponse {
  courseCode: string;
  courseName: string;
  sectionId: number;
  students: SectionStudent[];
}

export async function getInstructorProfile(): Promise<InstructorProfile> {
  const { data } = await apiClient.get('/instructor/profile');
  return data;
}

export async function getInstructorSections(): Promise<InstructorSectionsResponse> {
  const { data } = await apiClient.get('/instructor/my-sections');
  return data;
}

export async function getSectionStudents(sectionId: number): Promise<SectionStudentsResponse> {
  const { data } = await apiClient.get(`/instructor/section/${sectionId}/students`);
  return data;
}
