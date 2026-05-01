import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import SharedFieldWrapper from './SharedFieldWrapper';

interface Props {
  value: string;
  onChange: (next: string) => void;
  showSharedHints?: boolean;
}

const MAX_LENGTH = 1000;

export default function DescriptionSection({ value, onChange, showSharedHints = false }: Props) {
  return (
    <fieldset className="space-y-2">
      <legend className="text-sm font-semibold mb-2">Açıklama</legend>
      <SharedFieldWrapper ownership="shared" showSharedHint={showSharedHints}>
        <div className="space-y-1">
          <Label className="text-xs">Ders Açıklaması</Label>
          <Textarea
            value={value}
            onChange={e => onChange(e.target.value.slice(0, MAX_LENGTH))}
            placeholder="Ders açıklaması..."
            rows={4}
          />
          <p className="text-[11px] text-muted-foreground text-right">
            {value.length} / {MAX_LENGTH}
          </p>
        </div>
      </SharedFieldWrapper>
    </fieldset>
  );
}
