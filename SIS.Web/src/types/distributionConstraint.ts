export interface DistributionConstraintDto {
  id: number;
  distributionType: string;
  preferenceLevel: string;
  termId: number;
  departmentId: number | null;
  departmentName: string | null;
  reason: string | null;
  createdAt: string;
  courses: DistributionConstraintCourseDto[];
}

export interface DistributionConstraintCourseDto {
  id: number;
  courseId: number;
  courseCode: string;
  courseName: string;
  courseSectionId: number | null;
  sectionCode: string | null;
  sortOrder: number;
}

export interface CreateDistributionConstraintRequest {
  distributionType: string;
  preferenceLevel: string;
  termId: number;
  departmentId?: number | null;
  reason?: string | null;
  courses: { courseId: number; courseSectionId?: number | null; sortOrder: number }[];
}
