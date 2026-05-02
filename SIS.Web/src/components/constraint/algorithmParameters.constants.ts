// Algoritma parametreleri için ortak sabit listeler.
// AlgorithmParametersPanel grupları + SystemConfigPage filtre seti tek
// kaynaktan gelmeli ki listeler birbirinden ayrılmasın.

export interface AlgorithmGroup {
  title: string;
  description: string;
  keys: string[];
}

export const ALGORITHM_GROUPS: AlgorithmGroup[] = [
  {
    title: 'Simulated Annealing',
    description: 'Optimizasyon motoru parametreleri — kaç iterasyon, ne kadar süre, hangi hamle ağırlıkları.',
    keys: [
      'Schedule.SA.MaxIterations',
      'Schedule.SA.InitialTemperature',
      'Schedule.SA.TimeoutSeconds',
      'Schedule.SA.StagnationLimit',
      'Schedule.SA.BacktrackLimit',
      'Schedule.SA.RelocateWeight',
      'Schedule.SA.SwapWeight',
      'Schedule.SA.ChainWeight',
    ],
  },
  {
    title: 'MRV Heuristic',
    description: 'Construction Solver — başlangıç çözümü kurulurken hangi kısıtın önceliği var.',
    keys: [
      'Schedule.MRV.ClassroomWeight',
      'Schedule.MRV.SlotWeight',
      'Schedule.MRV.LabWeight',
      'Schedule.MRV.CapacityWeight',
    ],
  },
  {
    title: 'LNS (Large Neighborhood Search)',
    description: 'Yerel optimumdan kaçma — bir iterasyonda kaç section yeniden çözülür.',
    keys: [
      'Schedule.LNS.MaxIterations',
      'Schedule.LNS.TimeoutSeconds',
      'Schedule.LNS.DestroySize',
    ],
  },
  {
    title: 'Diğer Parametreler',
    description: 'Genel scheduling motoru ayarları.',
    keys: [
      'Scheduling.MaxBacktrackingDepth',
      'Schedule.TheoryLabSplitThreshold',
      'Schedule.SplittableBlockTypes',
    ],
  },
];

export const ALGORITHM_PARAMETER_KEYS = new Set<string>(
  ALGORITHM_GROUPS.flatMap(g => g.keys),
);
