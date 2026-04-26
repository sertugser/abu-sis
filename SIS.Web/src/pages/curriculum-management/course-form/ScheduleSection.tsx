import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import SharedFieldWrapper from './SharedFieldWrapper';
import type { CourseScheduleState } from './types';

interface Props {
  value: CourseScheduleState;
  onChange: (next: CourseScheduleState) => void;
  showSharedHints?: boolean;
}

export default function ScheduleSection({ value, onChange, showSharedHints = false }: Props) {
  return (
    <fieldset className="space-y-3">
      <legend className="text-sm font-semibold mb-2">Ders Saatleri ve Krediler</legend>

      <div className="grid grid-cols-2 gap-3">
        <SharedFieldWrapper ownership="shared" showSharedHint={showSharedHints}>
          <div className="space-y-1">
            <Label className="text-xs">Teori Saat</Label>
            <Input
              type="number"
              min={0}
              value={value.theoryHours}
              onChange={e => onChange({ ...value, theoryHours: Number(e.target.value) })}
            />
          </div>
        </SharedFieldWrapper>

        <SharedFieldWrapper ownership="shared" showSharedHint={showSharedHints}>
          <div className="space-y-1">
            <Label className="text-xs">Lab Saat</Label>
            <Input
              type="number"
              min={0}
              value={value.labHours}
              onChange={e => onChange({ ...value, labHours: Number(e.target.value) })}
            />
          </div>
        </SharedFieldWrapper>
      </div>

      <div className="grid grid-cols-2 gap-3">
        <SharedFieldWrapper ownership="shared" showSharedHint={showSharedHints}>
          <div className="space-y-1">
            <Label className="text-xs">AKTS *</Label>
            <Input
              type="number"
              min={0}
              value={value.ects}
              onChange={e => onChange({ ...value, ects: Number(e.target.value) })}
            />
          </div>
        </SharedFieldWrapper>

        <SharedFieldWrapper ownership="shared" showSharedHint={showSharedHints}>
          <div className="space-y-1">
            <Label className="text-xs">Kredi (Ulusal)</Label>
            <Input
              type="number"
              min={0}
              value={value.nationalCredit ?? ''}
              onChange={e => onChange({
                ...value,
                nationalCredit: e.target.value === '' ? null : Number(e.target.value),
              })}
              placeholder="örn. 4"
            />
          </div>
        </SharedFieldWrapper>
      </div>
    </fieldset>
  );
}
