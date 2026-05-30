import apiClient from './axios';

export interface OpenedDepartmentCourseItem {
  sectionId: number;
  sectionNumber: number;     // Şb
  courseCode: string;        // Ders Kodu
  courseName: string;        // Ders Adı
  classYear: number;         // Sınıf (1-4) — fallback ile her zaman dolu
  applicationHours: number;  // U
  labHours: number;          // L
  theoryHours: number;       // T
  isCompulsory: boolean;     // Z (true=Zorunlu, false=Seçmeli)
  nationalCredit: number;    // KRD
  ects: number;              // AKTS
}

export type OpenedDepartmentCoursesEmptyReason =
  | 'None'
  | 'StudentNotFound'
  | 'NoMainProgram'
  | 'NoActiveTerm'
  | 'Empty';

export interface OpenedDepartmentCourses {
  termLabel: string | null;
  departmentName: string | null;
  emptyReason: OpenedDepartmentCoursesEmptyReason;
  items: OpenedDepartmentCourseItem[];
}

export const getOpenedDepartmentCourses = () =>
  apiClient.get<OpenedDepartmentCourses>('/opened-department-courses/me').then(r => r.data);
