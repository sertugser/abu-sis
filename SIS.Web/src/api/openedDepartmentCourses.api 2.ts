import apiClient from './axios';

export interface OpenedDepartmentCourseItem {
  sectionId: number;
  sectionNumber: number;     // Şb
  courseCode: string;        // Ders Kodu
  courseName: string;        // Ders Adı
  classYear: number | null;  // Sınıf (1-4); müfredat dışıysa null
  applicationHours: number;  // U
  labHours: number;          // L
  theoryHours: number;       // T
  nationalCredit: number;    // KRD
  ects: number;              // AKTS
}

export interface OpenedDepartmentCourses {
  termLabel: string | null;
  departmentName: string | null;
  items: OpenedDepartmentCourseItem[];
}

export const getOpenedDepartmentCourses = () =>
  apiClient.get<OpenedDepartmentCourses>('/opened-department-courses/me').then(r => r.data);
