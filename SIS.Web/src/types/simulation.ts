export interface SimulateMoveRequest {
  termId: number;
  sectionId: number;
  newDayOfWeek: number;
  newTimeSlotStart: string;
  newClassroomId?: number | null;
}

export interface ConstraintChangeItem {
  constraintId: number;
  field: string;
  value: boolean | number | string;
}

export interface SimulateConstraintChangeRequest {
  termId: number;
  changes: ConstraintChangeItem[];
}

export interface SimulationResult {
  feasible: boolean;
  before: SimulationBreakdown;
  after: SimulationBreakdown;
  diff: SimulationDiff;
}

export interface SimulationBreakdown {
  hardViolations: number;
  softPenalty: number;
}

export interface SimulationDiff {
  resolvedConflicts: string[];
  newConflicts: string[];
  penaltyDelta: number;
  netImprovement: boolean;
}

export interface SuggestionItem {
  rank: number;
  dayOfWeek: number;
  timeSlot: string;
  classroomId: number | null;
  classroomName: string | null;
  hardViolations: number;
  softPenalty: number;
  improvement: number;
  tradeoffs: string[];
}
