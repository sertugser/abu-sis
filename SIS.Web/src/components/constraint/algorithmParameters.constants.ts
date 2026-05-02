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
    title: 'Diğer Algoritma Ayarları',
    description: 'Genel scheduling motoru ayarları.',
    keys: [
      'Scheduling.MaxBacktrackingDepth',
      'Schedule.TheoryLabSplitThreshold',
      'Schedule.SplittableBlockTypes',
    ],
  },
  {
    title: 'Hard Kısıt Eşikleri',
    description: 'Zorunlu kısıtların eşik / mod değerleri (H4, H13, H15).',
    keys: [
      'Schedule.LunchBreak.IsHard',
      'Schedule.LunchBreakStart',
      'Schedule.LunchBreakEnd',
      'Schedule.MaxConsecutiveHours',
      'Classroom.AmphiThreshold',
    ],
  },
  {
    title: 'Soft Kısıt Eşikleri',
    description: 'Tercihli kısıtların eşik değerleri (S1, S2, S3, S4, S7).',
    keys: [
      'Classroom.OccupancyTarget.Min',
      'Classroom.OccupancyTarget.Max',
      'SoftConstraint.S2.MaxDailyHours',
      'SoftConstraint.S3.MaxSectionsPerSlot',
      'SoftConstraint.S4.MinCoPairThreshold',
      'SoftConstraint.S7.MaxGapSlots',
    ],
  },
];

export const ALGORITHM_PARAMETER_KEYS = new Set<string>(
  ALGORITHM_GROUPS.flatMap(g => g.keys),
);

// Admin'in günlük tune ettiği temel ayarlar — "Sık Ayarlanan" kartında öne çıkar.
// Kalan 19 ayar "İleri Ayarlar" altında gizli kalır.
export const ESSENTIAL_PARAMETER_KEYS: string[] = [
  'Schedule.LunchBreak.IsHard',
  'Schedule.MaxConsecutiveHours',
  'Classroom.AmphiThreshold',
  'Classroom.OccupancyTarget.Min',
  'Classroom.OccupancyTarget.Max',
  'SoftConstraint.S2.MaxDailyHours',
  'Schedule.SA.MaxIterations',
  'Schedule.SA.TimeoutSeconds',
];

export const ESSENTIAL_PARAMETER_KEY_SET = new Set<string>(ESSENTIAL_PARAMETER_KEYS);

