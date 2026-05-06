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

      <div className="grid grid-cols-3 gap-3">
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

        <SharedFieldWrapper ownership="shared" showSharedHint={showSharedHints}>
          <div className="space-y-1">
            <Label className="text-xs">Uygulama Saat</Label>
            <Input
              type="number"
              min={0}
              value={value.applicationHours ?? ''}
              onChange={e => onChange({
                ...value,
                applicationHours: e.target.value === '' ? null : Number(e.target.value),
              })}
              placeholder="0"
            />
          </div>
        </SharedFieldWrapper>
      </div>

      <div className="grid grid-cols-2 gap-3">
        <SharedFieldWrapper ownership="shared" showSharedHint={showSharedHints}>
          <div className="space-y-1">
            <Label className="text-xs">
              AKTS
              <span className="text-destructive ml-0.5 font-semibold" aria-hidden="true">*</span>
              <span className="sr-only"> (zorunlu)</span>
            </Label>
            <Input
              type="number"
              min={1}
              max={30}
              value={value.ects}
              onChange={e => onChange({ ...value, ects: Number(e.target.value) })}
              aria-required="true"
              aria-invalid={!value.ects || value.ects < 1}
            />
          </div>
        </SharedFieldWrapper>

        <SharedFieldWrapper ownership="shared" showSharedHint={showSharedHints}>
          <div className="space-y-1">
            <Label className="text-xs">
              Kredi (Ulusal)
              <span className="text-destructive ml-0.5 font-semibold" aria-hidden="true">*</span>
              <span className="sr-only"> (zorunlu)</span>
            </Label>
            <Input
              type="number"
              min={0}
              max={20}
              value={value.nationalCredit}
              onChange={e => onChange({
                ...value,
                nationalCredit: e.target.value === '' ? 0 : Number(e.target.value),
              })}
              placeholder="örn. 4"
              aria-required="true"
              aria-invalid={value.nationalCredit < 0 || value.nationalCredit > 20}
            />
          </div>
        </SharedFieldWrapper>
      </div>
    </fieldset>
  );
}
