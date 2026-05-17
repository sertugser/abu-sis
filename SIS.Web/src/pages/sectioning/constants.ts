export const SPLIT_STRATEGIES = [
  { value: 'Equal', label: 'Eşit Parçala' },
  { value: 'EqualRepeat', label: 'Eşit Parçala (Tekrarlayanları Homojen Dağıt)' },
  { value: 'ModOddEven', label: 'MOD Parçala (Tek/Çift) — 2 şube' },
] as const;

export type SplitStrategy = typeof SPLIT_STRATEGIES[number]['value'];
