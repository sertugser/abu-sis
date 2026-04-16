export interface ConstraintProfileDto {
  id: number;
  name: string;
  description: string | null;
  isDefault: boolean;
  createdByUserId: string | null;
  createdAt: string;
  itemCount: number;
}

export interface ConstraintProfileDetailDto {
  id: number;
  name: string;
  description: string | null;
  isDefault: boolean;
  createdByUserId: string | null;
  createdAt: string;
  items: ConstraintProfileItemDto[];
}

export interface ConstraintProfileItemDto {
  id: number;
  constraintDefinitionId: number;
  constraintCode: string;
  constraintName: string;
  isEnabled: boolean;
  category: 'Hard' | 'Soft';
  weight: number;
  parameters: string | null;
}

export interface ApplyProfileResult {
  changedCount: number;
  autoSavedProfileName: string | null;
}
