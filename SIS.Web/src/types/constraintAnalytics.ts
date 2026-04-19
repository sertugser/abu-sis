export interface SnapshotDto {
  id: number;
  termId: number;
  snapshotAt: string;
  snapshotType: string;
  totalHardViolations: number;
  totalSoftPenalty: number;
  isFeasible: boolean;
}

export interface TrendPointDto {
  snapshotAt: string;
  snapshotType: string;
  hardViolations: number;
  softPenalty: number;
  isFeasible: boolean;
}

export interface TrendSummaryDto {
  hardReduction: string;
  penaltyReduction: string;
  trend: string;
}

export interface TrendDataDto {
  termId: number;
  dataPoints: TrendPointDto[];
  improvement: TrendSummaryDto;
}

export interface ComparisonItemDto {
  constraintCode: string;
  category: string;
  violationsBefore: number;
  violationsAfter: number;
  violationDelta: number;
  penaltyBefore: number;
  penaltyAfter: number;
  penaltyDelta: number;
  change: 'improved' | 'worsened' | 'unchanged';
}

export interface SnapshotComparisonDto {
  snapshot1: SnapshotDto;
  snapshot2: SnapshotDto;
  items: ComparisonItemDto[];
  improvedCount: number;
  worsenedCount: number;
  unchangedCount: number;
}

export interface HeatmapCellDto {
  dayOfWeek: number;
  slotIndex: number;
  violationCount: number;
  severity: string;
}

export interface HeatmapRowDto {
  constraintCode: string;
  constraintName: string;
  cells: HeatmapCellDto[];
}

export interface HeatmapDto {
  rows: HeatmapRowDto[];
}

export interface BottleneckConstraintDto {
  code: string;
  name: string;
  percentage: number;
  violationCount: number;
}

export interface BottleneckResourceDto {
  type: string;
  name: string;
  conflictCount: number;
}

export interface BottleneckSlotDto {
  day: string;
  slot: string;
  totalViolations: number;
}

export interface BottleneckDto {
  topConstraints: BottleneckConstraintDto[];
  topResources: BottleneckResourceDto[];
  topTimeSlots: BottleneckSlotDto[];
}
