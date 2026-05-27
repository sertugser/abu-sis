import { Label } from '@/components/ui/label';
import { Switch } from '@/components/ui/switch';
import type { CourseFlagsState } from './types';

interface Props {
  value: CourseFlagsState;
  onChange: (next: CourseFlagsState) => void;
}

interface FlagRow {
  key: keyof CourseFlagsState;
  label: string;
  hint?: string;
}

const VIEW_FLAGS: FlagRow[] = [
  { key: 'isMainGroupCourse', label: 'Ana Grup Dersi', hint: 'Bu, başka derslerin altında gruplanan ana grup dersidir' },
  { key: 'isFormationCourse', label: 'Formasyon Dersi', hint: 'Pedagojik formasyon kapsamındaki dersler' },
];

const REGISTRATION_FLAGS: FlagRow[] = [
  { key: 'autoAddOnNewRegistration', label: 'ÖSYM Yeni Kayıtta Otomatik Ekle' },
  { key: 'showAtTakenCurriculum', label: 'Alındığı Yerde Göster' },
  { key: 'showInstructorAtRegistration', label: 'Ders Kayıtta Öğretim Elemanı Görünsün' },
];

const CALCULATION_FLAGS: FlagRow[] = [
  { key: 'excludedFromCreditTotal', label: 'Kredi Toplamı Dışı (Krd Top. Dışı)' },
  { key: 'excludedFromTranscriptTotal', label: 'Transkript Krd/AKTS Toplamına Dahil Değil' },
];

function FlagsGroup({
  title,
  rows,
  value,
  onChange,
}: {
  title: string;
  rows: FlagRow[];
  value: CourseFlagsState;
  onChange: (next: CourseFlagsState) => void;
}) {
  return (
    <div className="space-y-2">
      <div className="text-[11px] font-medium uppercase text-muted-foreground tracking-wider">
        {title}
      </div>
      <div className="space-y-2">
        {rows.map((row) => (
          <div key={row.key} className="flex items-center justify-between gap-2">
            <div className="flex flex-col">
              <Label className="text-xs">{row.label}</Label>
              {row.hint && (
                <span className="text-[11px] text-muted-foreground">{row.hint}</span>
              )}
            </div>
            <Switch
              checked={value[row.key]}
              onCheckedChange={(checked) => onChange({ ...value, [row.key]: checked })}
            />
          </div>
        ))}
      </div>
    </div>
  );
}

export default function FlagsSection({ value, onChange }: Props) {
  return (
    <fieldset className="space-y-4">
      <legend className="text-sm font-semibold mb-2">Bayrak ve Davranış Ayarları</legend>

      <FlagsGroup title="Görünüm" rows={VIEW_FLAGS} value={value} onChange={onChange} />
      <FlagsGroup title="Kayıt" rows={REGISTRATION_FLAGS} value={value} onChange={onChange} />
      <FlagsGroup title="Hesaplama" rows={CALCULATION_FLAGS} value={value} onChange={onChange} />
    </fieldset>
  );
}
