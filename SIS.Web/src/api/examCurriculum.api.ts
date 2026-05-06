import apiClient from './axios';
import type { AdminTermDto } from '../hooks/useAdminTerms';
import type {
  CurriculumCourseItemDto,
  CurriculumListDto,
} from '../types/curriculumManagement';

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
  const { data } = await apiClient.get<AdminTermDto[]>('/admin/terms');

  return data.map((term) => ({
    id: term.id,
    name: term.name,
    shortName: term.shortName,
    year: term.year,
    termType: term.termType,
  }));
}

export async function getExamCurricula(departmentId: number) {
  const { data } = await apiClient.get<CurriculumListDto[]>('/curriculum', {
    params: { departmentId },
  });

  return data.map((curriculum) => ({
    id: curriculum.id,
    name: curriculum.name ?? `${curriculum.programName} ${curriculum.version}`,
    version: curriculum.version,
    programId: curriculum.programId,
    programName: curriculum.programName,
    departmentId,
    departmentName: curriculum.departmentName,
    isActive: curriculum.isActive,
  }));
}

export async function getExamCurriculumCourses(curriculumId: number) {
  const { data } = await apiClient.get<CurriculumCourseItemDto[]>(
    `/curriculum/${curriculumId}/courses`
  );

  return data.map((course) => ({
    courseId: course.courseId,
    courseCode: course.courseCode,
    courseName: course.courseName,
    suggestedSemester: course.suggestedSemester,
    isCompulsory: course.isCompulsory,
    ects: course.ects,
  }));
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
