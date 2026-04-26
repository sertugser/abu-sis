import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import type { CourseDecisionState } from './types';

interface Props {
  value: CourseDecisionState;
  onChange: (next: CourseDecisionState) => void;
}

export default function DecisionSection({ value, onChange }: Props) {
  return (
    <fieldset className="space-y-3">
      <legend className="text-sm font-semibold mb-2">Karar Bilgileri</legend>

      <div className="grid grid-cols-2 gap-3">
        <div className="space-y-1">
          <Label className="text-xs">Karar Tarihi</Label>
          <Input
            type="date"
            value={value.decisionDate}
            onChange={e => onChange({ ...value, decisionDate: e.target.value })}
          />
        </div>
        <div className="space-y-1">
          <Label className="text-xs">Karar No</Label>
          <Input
            value={value.decisionNo}
            onChange={e => onChange({ ...value, decisionNo: e.target.value })}
            placeholder="örn. 2024/12-3"
          />
        </div>
      </div>

      <div className="space-y-1">
        <Label className="text-xs">Karar Açıklaması</Label>
        <Textarea
          rows={2}
          value={value.decisionNote}
          onChange={e => onChange({ ...value, decisionNote: e.target.value })}
          placeholder="Senato/Yönetim Kurulu kararı detayı"
          className="text-sm"
        />
      </div>
    </fieldset>
  );
}
