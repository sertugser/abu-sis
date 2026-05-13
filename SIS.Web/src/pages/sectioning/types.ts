import type { DepartmentSectionClassList } from '@/types/academicOperations';

export interface CourseGroup {
  courseId: number;
  courseCode: string;
  courseName: string;
  sections: DepartmentSectionClassList[];
  totalStudents: number;
  uniqueInstructorCount: number;
}
