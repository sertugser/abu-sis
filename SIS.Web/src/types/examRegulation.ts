export type DegreeLevel = 'Associate' | 'Bachelor' | 'Master' | 'Doctorate';

export interface ExamRegulation {
  id: number;
  programId: number;
  programName: string;
  degreeLevel: DegreeLevel;
  termType: string;
  year: number;
  isActive: boolean;
  name: string;
  midtermCount: number;
  finalCount: number;
  minMidtermWeightPercent: number;
  maxMidtermWeightPercent: number;
  minFinalWeightPercent: number;
  maxFinalWeightPercent: number;
  passingGrade: number;
  rawScoreFloor1: number;
  rawScoreFloor2: number;
  finalExamFloor: number;
  aaMin: number;
  baMin: number;
  bbMin: number;
  cbMin: number;
  ccMin: number;
  dcMin: number;
  ddMin: number;
  letterRangesEditable: boolean;
  belowFloorGrade: string;
}

export type SaveExamRegulationRequest = Omit<ExamRegulation, 'id' | 'programName'>;
