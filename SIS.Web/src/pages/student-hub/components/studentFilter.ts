export interface StudentFilterState {
  search: string;
  departmentId: number | undefined;
  programId: number | undefined;
  status: string;
  advisorFilter: string;
  minCgpa: string;
  maxCgpa: string;
  classYear: string;
  curriculumId: number | undefined;
  studentNoFrom: string;
  studentNoTo: string;
  enrollmentFrom: string;
  enrollmentTo: string;
  advisorId: number | undefined;
  hasFailingCourse: boolean;
}

export const EMPTY_FILTER_STATE: StudentFilterState = {
  search: '',
  departmentId: undefined,
  programId: undefined,
  status: '',
  advisorFilter: '',
  minCgpa: '',
  maxCgpa: '',
  classYear: '',
  curriculumId: undefined,
  studentNoFrom: '',
  studentNoTo: '',
  enrollmentFrom: '',
  enrollmentTo: '',
  advisorId: undefined,
  hasFailingCourse: false,
};
