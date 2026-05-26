import type { ReportParameter, ReportParameterType } from '@/api/reportRun.api';
import { Label } from '@/components/ui/label';
import { Input } from '@/components/ui/input';
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/components/ui/select';
import {
  useFacultyLookup, useAdminDepartments, useAdminPrograms, useAdminAdvisors, useSectionLookup,
} from '@/hooks/useAdminLookup';
import { useAcademicCalendarTerms } from '@/hooks/useAcademicCalendar';

interface ReportFilterFormProps {
  parameters: ReportParameter[];
  values: Record<string, string>;
  onChange: (key: string, value: string) => void;
}

interface Option { value: string; label: string }

// Radix Select boş-değerli öğeye izin vermez; "Tümü" için sentinel kullanıp seçilince filtreyi temizleriz.
const ALL = '__all__';

export function ReportFilterForm({ parameters, values, onChange }: ReportFilterFormProps) {
  // Şube, döneme bağlı: seçili Term parametresinin değerinden termId çözülür.
  const termParamKey = parameters.find((p) => p.type === 'Term')?.key;
  const selectedTermId = termParamKey && values[termParamKey] ? Number(values[termParamKey]) : undefined;
  const sectionParamKeys = parameters.filter((p) => p.type === 'CourseSection').map((p) => p.key);

  const terms = useAcademicCalendarTerms();
  const faculties = useFacultyLookup();
  const departments = useAdminDepartments();
  const programs = useAdminPrograms();
  const advisors = useAdminAdvisors();
  const sections = useSectionLookup(selectedTermId);

  if (parameters.length === 0) {
    return <p className="text-sm text-muted-foreground">Bu rapor için filtre yok.</p>;
  }

  const lookupOptions = (type: ReportParameterType): Option[] | null => {
    switch (type) {
      case 'Term':
        return (terms.data ?? []).map((t) => ({ value: String(t.termId), label: t.termName }));
      case 'Faculty':
        return (faculties.data ?? []).map((f) => ({ value: String(f.id), label: f.name }));
      case 'Department':
        return (departments.data ?? []).map((d) => ({ value: String(d.id), label: d.name }));
      case 'Program':
        return (programs.data ?? []).map((p) => ({ value: String(p.id), label: p.name }));
      case 'Advisor':
        return (advisors.data ?? []).map((a) => ({ value: String(a.id), label: a.fullName }));
      case 'CourseSection':
        return (sections.data ?? []).map((s) => ({ value: String(s.id), label: s.label }));
      default:
        return null;
    }
  };

  return (
    <div className="grid gap-4 sm:grid-cols-2">
      {parameters.map((p) => {
        const id = `rp-${p.key}`;
        const current = values[p.key] ?? '';

        // Dönem değişince bağlı şube seçimini temizle (başka dönemin şubesi gönderilmesin).
        const handleChange = (key: string, val: string) => {
          onChange(key, val);
          if (p.type === 'Term') sectionParamKeys.forEach((sk) => onChange(sk, ''));
        };

        // Şube: dönem seçilmeden seçilemez.
        if (p.type === 'CourseSection' && !selectedTermId) {
          return (
            <div key={p.key} className="space-y-1.5">
              <Label htmlFor={id}>{p.label}</Label>
              <Select disabled value={ALL}>
                <SelectTrigger id={id}><SelectValue placeholder="Önce dönem seçin" /></SelectTrigger>
                <SelectContent />
              </Select>
            </div>
          );
        }

        const enumOpts = p.type === 'Enum' && p.options ? p.options : null;
        const boolOpts: Option[] | null = p.type === 'Bool'
          ? [{ value: 'true', label: 'Evet' }, { value: 'false', label: 'Hayır' }]
          : null;
        const dropdownOpts = enumOpts ?? boolOpts ?? lookupOptions(p.type);

        return (
          <div key={p.key} className="space-y-1.5">
            <Label htmlFor={id}>
              {p.label}{p.required && <span className="text-destructive"> *</span>}
            </Label>
            {dropdownOpts ? (
              <Select value={current || ALL} onValueChange={(v) => handleChange(p.key, v === ALL ? '' : v)}>
                <SelectTrigger id={id}><SelectValue placeholder="Tümü" /></SelectTrigger>
                <SelectContent>
                  <SelectItem value={ALL}>Tümü</SelectItem>
                  {dropdownOpts.map((o) => (
                    <SelectItem key={o.value} value={o.value}>{o.label}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
            ) : (
              <Input
                id={id}
                type={inputType(p.type)}
                value={current}
                onChange={(e) => handleChange(p.key, e.target.value)}
              />
            )}
          </div>
        );
      })}
    </div>
  );
}

function inputType(t: ReportParameterType): string {
  switch (t) {
    case 'Int':
    case 'Decimal':
      return 'number';
    case 'Date':
      return 'date';
    default:
      return 'text';
  }
}
