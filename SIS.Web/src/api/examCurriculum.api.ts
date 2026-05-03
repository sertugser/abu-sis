import apiClient from './axios';

const API = '/exam-curriculum';

export interface ExamCurriculumTerm {
  id: number;
  name: string;
  shortName: string;
  year: number;
  termType: string;
}

export interface ExamCurriculumLookup {
  id: number;
  name: string;
  version: string;
  programId: number;
  programName: string;
  departmentId: number;
  departmentName: string;
  isActive: boolean;
}

export interface ExamCurriculumCourseLookup {
  courseId: number;
  courseCode: string;
  courseName: string;
  suggestedSemester: number;
  isCompulsory: boolean;
  ects: number;
}

export interface ExamCurriculumComponent {
  id: number;
  name: string;
  examType: string;
  weight: number;
  order: number;
  isRequired: boolean;
  minimumScore: number | null;
  requiresAttendanceEligibility: boolean;
  prerequisiteGradeComponentId: number | null;
}

export interface ExamCurriculumTemplate {
  curriculumId: number;
  curriculumName: string;
  programId: number;
  programName: string;
  departmentId: number;
  departmentName: string;
  courseId: number;
  courseCode: string;
  courseName: string;
  academicTermId: number;
  academicTermName: string;
  calculationMethod: 'Absolute' | 'Relative' | string;
  totalWeight: number;
  isComplete: boolean;
  components: ExamCurriculumComponent[];
}

export interface SaveExamCurriculumComponent {
  id?: number | null;
  clientKey?: string | null;
  name: string;
  examType: string;
  weight: number;
  order: number;
  isRequired: boolean;
  minimumScore?: number | null;
  requiresAttendanceEligibility: boolean;
  prerequisiteGradeComponentId?: number | null;
  prerequisiteClientKey?: string | null;
}

export interface SaveExamCurriculumTemplateRequest {
  curriculumId: number;
  courseId: number;
  academicTermId: number;
  calculationMethod: 'Absolute' | 'Relative';
  components: SaveExamCurriculumComponent[];
}

export async function getExamCurriculumTerms() {
  const { data } = await apiClient.get<ExamCurriculumTerm[]>(`${API}/terms`);
  return data;
}

export async function getExamCurricula(departmentId: number) {
  const { data } = await apiClient.get<ExamCurriculumLookup[]>(`${API}/curricula`, {
    params: { departmentId },
  });

  return data;
}

export async function getExamCurriculumCourses(curriculumId: number) {
  const { data } = await apiClient.get<ExamCurriculumCourseLookup[]>(
    `${API}/curriculum-courses`,
    { params: { curriculumId } }
  );

  return data;
}

export async function getExamCurriculumTemplate(params: {
  curriculumId: number;
  courseId: number;
  academicTermId: number;
}) {
  const { data } = await apiClient.get<ExamCurriculumTemplate>(`${API}/template`, {
    params,
  });

  return data;
}

export async function saveExamCurriculumTemplate(dto: SaveExamCurriculumTemplateRequest) {
  const { data } = await apiClient.put<ExamCurriculumTemplate>(`${API}/template`, dto);
  return data;
}