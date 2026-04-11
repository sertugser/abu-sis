export interface ConstraintDefinitionDto {
  id: number;
  code: string;
  name: string;
  description: string | null;
  category: 'Hard' | 'Soft';
  scope: string;
  isEnabled: boolean;
  parameters: string | null;
  sortOrder: number;
  exceptionCount: number;
}

export interface ConstraintExceptionDto {
  id: number;
  constraintDefinitionId: number;
  constraintCode: string;
  instructorId: number | null;
  instructorName: string | null;
  studentId: number | null;
  studentName: string | null;
  departmentId: number | null;
  departmentName: string | null;
  courseSectionId: number | null;
  courseSectionInfo: string | null;
  termId: number | null;
  termName: string | null;
  isExempt: boolean;
  parameterOverride: string | null;
  reason: string | null;
  createdAt: string;
}

export interface ConstraintDefinitionDetailDto extends Omit<ConstraintDefinitionDto, 'exceptionCount'> {
  exceptions: ConstraintExceptionDto[];
}

export interface AddExceptionRequest {
  instructorId?: number | null;
  studentId?: number | null;
  departmentId?: number | null;
  courseSectionId?: number | null;
  termId?: number | null;
  isExempt: boolean;
  parameterOverride?: string | null;
  reason?: string | null;
}
