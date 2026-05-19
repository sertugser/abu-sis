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
  weight: number;
  priority: number;
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

// ── Exception Request (Approval Workflow) ─────────────────────
export interface ExceptionRequestDto {
  id: number;
  constraintDefinitionId: number;
  constraintCode: string;
  constraintName: string;
  requestedByUserId: string;
  requestedByName: string | null;
  requestedAt: string;
  status: string;
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
  reason: string;
  expiresAt: string | null;
  steps: ApprovalStepDto[];
}

export interface ApprovalStepDto {
  id: number;
  stepOrder: number;
  requiredRole: string;
  status: string;
  decidedByUserId: string | null;
  decidedByName: string | null;
  decidedAt: string | null;
  comment: string | null;
}

export interface CreateExceptionRequestDto {
  constraintDefinitionId: number;
  instructorId?: number | null;
  studentId?: number | null;
  departmentId?: number | null;
  courseSectionId?: number | null;
  termId?: number | null;
  isExempt: boolean;
  parameterOverride?: string | null;
  reason: string;
  expiresAt?: string | null;
}

export interface ApprovalActionDto {
  comment?: string | null;
}
