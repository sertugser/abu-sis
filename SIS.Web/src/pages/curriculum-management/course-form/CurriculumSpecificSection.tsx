import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Switch } from '@/components/ui/switch';
import type { CurriculumSpecificState } from './types';

interface Props {
  value: CurriculumSpecificState;
  onChange: (next: CurriculumSpecificState) => void;
}

export default function CurriculumSpecificSection({ value, onChange }: Props) {
  const sinif = Math.ceil(value.suggestedSemester / 2);
  const donem = value.suggestedSemester % 2 === 1 ? 'Güz' : 'Bahar';

  return (
    <fieldset className="space-y-3">
      <legend className="text-sm font-semibold mb-2">Müfredat Bilgileri (bu programa özgü)</legend>

      <div className="grid grid-cols-3 gap-3">
        <div className="space-y-1">
          <Label className="text-xs">Yarıyıl *</Label>
          <Input
            type="number" min={1} max={14}
            value={value.suggestedSemester}
            onChange={e => onChange({ ...value, suggestedSemester: Number(e.target.value) })}
          />
        </div>
        <div className="space-y-1">
          <Label className="text-xs">Sınıf</Label>
          <Input value={`${sinif}. Sınıf`} readOnly className="bg-muted/50 cursor-not-allowed" />
        </div>
        <div className="space-y-1">
          <Label className="text-xs">Dönem</Label>
          <Input value={donem} readOnly className="bg-muted/50 cursor-not-allowed" />
        </div>
      </div>

      <div className="space-y-2 pt-1">
        <div className="flex items-center justify-between">
          <Label className="text-xs">Zorunlu / Seçmeli</Label>
          <div className="flex items-center gap-2">
            <span className="text-xs text-muted-foreground">{value.isCompulsory ? 'Zorunlu' : 'Seçmeli'}</span>
            <Switch
              checked={value.isCompulsory}
              onCheckedChange={(c) => onChange({ ...value, isCompulsory: c })}
            />
          </div>
        </div>

        <div className="flex items-center justify-between">
          <Label className="text-xs">Ortalamaya Katılım</Label>
          <Switch
            checked={value.includeInGpa}
            onCheckedChange={(c) => onChange({ ...value, includeInGpa: c })}
          />
        </div>

        <div className="flex items-center justify-between">
          <Label className="text-xs">Transkriptte Göster</Label>
          <Switch
            checked={value.showOnTranscript}
            onCheckedChange={(c) => onChange({ ...value, showOnTranscript: c })}
          />
        </div>
      </div>
    </fieldset>
  );
}
