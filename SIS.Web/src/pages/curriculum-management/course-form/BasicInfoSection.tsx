import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import SharedFieldWrapper from './SharedFieldWrapper';
import type { CourseBasicState } from './types';

interface Props {
  value: CourseBasicState;
  onChange: (next: CourseBasicState) => void;
  /** When true, the Code input is read-only (e.g. existing course; admin must change via Catalog page) */
  codeReadonly?: boolean;
  /** When true, shared-field hints are visible (Faz 1C) */
  showSharedHints?: boolean;
}

export default function BasicInfoSection({ value, onChange, codeReadonly = false, showSharedHints = false }: Props) {
  return (
    <fieldset className="space-y-3">
      <legend className="text-sm font-semibold mb-2">Temel Bilgiler</legend>

      <SharedFieldWrapper ownership="shared" showSharedHint={showSharedHints}>
        <div className="space-y-1">
          <Label className="text-xs">Ders Kodu *</Label>
          <Input
            value={value.code}
            onChange={e => onChange({ ...value, code: e.target.value })}
            placeholder="MATH101"
            readOnly={codeReadonly}
            className={codeReadonly ? 'bg-muted/50 cursor-not-allowed' : ''}
          />
        </div>
      </SharedFieldWrapper>

      <SharedFieldWrapper ownership="shared" showSharedHint={showSharedHints}>
        <div className="space-y-1">
          <Label className="text-xs">Ders Adı *</Label>
          <Input
            value={value.name}
            onChange={e => onChange({ ...value, name: e.target.value })}
            placeholder="Calculus I"
          />
        </div>
      </SharedFieldWrapper>

      <SharedFieldWrapper ownership="shared" showSharedHint={showSharedHints}>
        <div className="space-y-1">
          <Label className="text-xs">İngilizce Adı</Label>
          <Input
            value={value.englishName}
            onChange={e => onChange({ ...value, englishName: e.target.value })}
            placeholder="English course name"
          />
        </div>
      </SharedFieldWrapper>
    </fieldset>
  );
}
