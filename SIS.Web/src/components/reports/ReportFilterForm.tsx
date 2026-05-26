import type { ReportParameter } from '@/api/reportRun.api';
import { Label } from '@/components/ui/label';
import { Input } from '@/components/ui/input';
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/components/ui/select';

interface ReportFilterFormProps {
  parameters: ReportParameter[];
  values: Record<string, string>;
  onChange: (key: string, value: string) => void;
}

export function ReportFilterForm({ parameters, values, onChange }: ReportFilterFormProps) {
  if (parameters.length === 0) {
    return <p className="text-sm text-muted-foreground">Bu rapor için filtre yok.</p>;
  }

  return (
    <div className="grid gap-4 sm:grid-cols-2">
      {parameters.map((p) => (
        <div key={p.key} className="space-y-1.5">
          <Label htmlFor={`rp-${p.key}`}>
            {p.label}{p.required && <span className="text-destructive"> *</span>}
          </Label>
          {p.type === 'Enum' && p.options ? (
            <Select value={values[p.key] ?? ''} onValueChange={(v) => onChange(p.key, v)}>
              <SelectTrigger id={`rp-${p.key}`}><SelectValue placeholder="Seç" /></SelectTrigger>
              <SelectContent>
                {p.options.map((o) => (
                  <SelectItem key={o.value} value={o.value}>{o.label}</SelectItem>
                ))}
              </SelectContent>
            </Select>
          ) : (
            <Input
              id={`rp-${p.key}`}
              type={inputType(p.type)}
              value={values[p.key] ?? ''}
              onChange={(e) => onChange(p.key, e.target.value)}
            />
          )}
        </div>
      ))}
    </div>
  );
}

function inputType(t: ReportParameter['type']): string {
  switch (t) {
    case 'Int':
    case 'Decimal':
    case 'Term':
    case 'Faculty':
    case 'Department':
    case 'Program':
      return 'number';
    case 'Date':
      return 'date';
    default:
      return 'text';
  }
}
