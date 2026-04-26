import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';

interface Props {
  groupCode: string;
  onGroupCodeChange: (val: string) => void;
}

export default function GroupSection({ groupCode, onGroupCodeChange }: Props) {
  return (
    <fieldset className="space-y-2">
      <legend className="text-sm font-semibold mb-2">Grup Seçenekleri</legend>
      <div className="space-y-1">
        <Label className="text-xs">Ders Grup Kodu</Label>
        <Input
          value={groupCode}
          onChange={e => onGroupCodeChange(e.target.value)}
          placeholder="örn. TE-I (Teknik Seçmeli I)"
        />
        <p className="text-[11px] text-muted-foreground">
          Aynı grup kodlu dersler seçmeli havuzda gruplanır.
        </p>
      </div>
    </fieldset>
  );
}
