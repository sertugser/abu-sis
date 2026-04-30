import { useState } from 'react';
import { Button } from '@/components/ui/button';
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Switch } from '@/components/ui/switch';
import { AlertTriangle, Loader2, Lock } from 'lucide-react';
import {
  useCreateElectiveSubtype,
  useUpdateElectiveSubtype,
} from '@/hooks/useElectiveSubtypes';
import type { ElectiveSubtypeDto } from '@/types/electiveSubtype';

type Mode = 'create' | 'edit';

interface ElectiveSubtypeFormDialogProps {
  mode: Mode;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  /** Edit modunda mevcut tip; create'te null */
  subtype: ElectiveSubtypeDto | null;
}

interface FormState {
  code: string;
  name: string;
  requiresFaculty: boolean;
  forbidsFaculty: boolean;
  requiresDepartment: boolean;
  forbidsDepartment: boolean;
  sortOrder: number;
  isActive: boolean;
}

const EMPTY: FormState = {
  code: '',
  name: '',
  requiresFaculty: false,
  forbidsFaculty: false,
  requiresDepartment: false,
  forbidsDepartment: false,
  sortOrder: 0,
  isActive: true,
};

export default function ElectiveSubtypeFormDialog({
  mode, open, onOpenChange, subtype,
}: ElectiveSubtypeFormDialogProps) {
  const [form, setForm] = useState<FormState>(EMPTY);
  const [validationError, setValidationError] = useState<string | null>(null);

  const createMutation = useCreateElectiveSubtype();
  const updateMutation = useUpdateElectiveSubtype();
  const mutation = mode === 'create' ? createMutation : updateMutation;

  // Edit modunda dialog açılınca mevcut tipi forma yükle — adjust-state-during-render
  // (React 19+: useEffect içinde setState yasaklandı, prev-tracking ile render'da update)
  const trackKey = open ? `${mode}-${subtype?.id ?? 'none'}` : 'closed';
  const [prevTrackKey, setPrevTrackKey] = useState(trackKey);
  if (trackKey !== prevTrackKey) {
    setPrevTrackKey(trackKey);
    if (open) {
      if (mode === 'edit' && subtype) {
        setForm({
          code: subtype.code,
          name: subtype.name,
          requiresFaculty: subtype.requiresFaculty,
          forbidsFaculty: subtype.forbidsFaculty,
          requiresDepartment: subtype.requiresDepartment,
          forbidsDepartment: subtype.forbidsDepartment,
          sortOrder: subtype.sortOrder,
          isActive: subtype.isActive,
        });
      } else if (mode === 'create') {
        setForm(EMPTY);
      }
      setValidationError(null);
    }
  }

  // Karar #14: flag değişimi mevcut grupları etkilemez ama yeni grup
  // oluşturma validation'ını değiştirir — kullanıcıya bilgi.
  const flagsChanged =
    mode === 'edit' && subtype != null && (
      form.requiresFaculty !== subtype.requiresFaculty ||
      form.forbidsFaculty !== subtype.forbidsFaculty ||
      form.requiresDepartment !== subtype.requiresDepartment ||
      form.forbidsDepartment !== subtype.forbidsDepartment
    );

  function validate(): string | null {
    if (mode === 'create' && !form.code.trim()) return 'Kod boş olamaz.';
    if (!form.name.trim()) return 'Ad boş olamaz.';
    if (form.requiresFaculty && form.forbidsFaculty)
      return 'Fakülte aynı anda hem zorunlu hem yasak olamaz.';
    if (form.requiresDepartment && form.forbidsDepartment)
      return 'Bölüm aynı anda hem zorunlu hem yasak olamaz.';
    return null;
  }

  function handleSubmit() {
    const err = validate();
    if (err) {
      setValidationError(err);
      return;
    }
    setValidationError(null);

    if (mode === 'create') {
      createMutation.mutate(
        {
          code: form.code.trim(),
          name: form.name.trim(),
          requiresFaculty: form.requiresFaculty,
          forbidsFaculty: form.forbidsFaculty,
          requiresDepartment: form.requiresDepartment,
          forbidsDepartment: form.forbidsDepartment,
          sortOrder: form.sortOrder,
        },
        { onSuccess: () => onOpenChange(false) },
      );
    } else if (subtype) {
      updateMutation.mutate(
        {
          id: subtype.id,
          name: form.name.trim(),
          requiresFaculty: form.requiresFaculty,
          forbidsFaculty: form.forbidsFaculty,
          requiresDepartment: form.requiresDepartment,
          forbidsDepartment: form.forbidsDepartment,
          isActive: form.isActive,
          sortOrder: form.sortOrder,
        },
        { onSuccess: () => onOpenChange(false) },
      );
    }
  }

  const title = mode === 'create' ? 'Yeni Seçmeli Tipi' : 'Seçmeli Tipi Düzenle';

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle>{title}</DialogTitle>
          {mode === 'create' && (
            <DialogDescription className="text-xs">
              Kod sonradan değiştirilemez. Türkçe ad ve flag'ler düzenlenebilir.
            </DialogDescription>
          )}
        </DialogHeader>

        <div className="space-y-4">
          {/* Code — create'te editable, edit'te readonly + lock */}
          <div className="space-y-1">
            <Label htmlFor="est-code">Kod</Label>
            <div className="relative">
              <Input
                id="est-code"
                value={form.code}
                onChange={e => setForm(f => ({ ...f, code: e.target.value }))}
                placeholder="örn: SocialElective"
                disabled={mode === 'edit'}
                className={mode === 'edit' ? 'pr-9 bg-muted/40' : ''}
              />
              {mode === 'edit' && (
                <Lock className="absolute right-2.5 top-1/2 -translate-y-1/2 h-3.5 w-3.5 text-muted-foreground" />
              )}
            </div>
            <p className="text-[10px] text-muted-foreground">
              Sistem içinde sabit kalır (Karar #14). Türkçe ad ile karıştırılmamalı.
            </p>
          </div>

          {/* Name — editable */}
          <div className="space-y-1">
            <Label htmlFor="est-name">Türkçe Ad</Label>
            <Input
              id="est-name"
              value={form.name}
              onChange={e => setForm(f => ({ ...f, name: e.target.value }))}
              placeholder="örn: Sosyal Seçmeli"
            />
          </div>

          {/* Flags — Switch grid */}
          <div className="space-y-3 rounded-md border border-border p-3">
            <p className="text-xs font-medium text-muted-foreground">Scope Kuralları</p>

            <FlagRow
              id="est-rf"
              label="Fakülte zorunlu"
              hint="Yeni grup oluşturken fakülte seçilmeden kayıt yapılamaz."
              checked={form.requiresFaculty}
              onChange={v => setForm(f => ({ ...f, requiresFaculty: v }))}
            />
            <FlagRow
              id="est-ff"
              label="Fakülte yasak"
              hint="Üniversite-geneli (OC) tipler için. Faculty seçilemez."
              checked={form.forbidsFaculty}
              onChange={v => setForm(f => ({ ...f, forbidsFaculty: v }))}
            />
            <FlagRow
              id="est-rd"
              label="Bölüm zorunlu"
              hint="Bölüm-bağımlı tipler için (nadir)."
              checked={form.requiresDepartment}
              onChange={v => setForm(f => ({ ...f, requiresDepartment: v }))}
            />
            <FlagRow
              id="est-fd"
              label="Bölüm yasak"
              hint="Üniversite/fakülte-geneli tipler için. Bölüm seçilemez."
              checked={form.forbidsDepartment}
              onChange={v => setForm(f => ({ ...f, forbidsDepartment: v }))}
            />
          </div>

          {/* SortOrder */}
          <div className="space-y-1">
            <Label htmlFor="est-sort">Sıra</Label>
            <Input
              id="est-sort"
              type="number"
              min={0}
              value={form.sortOrder}
              onChange={e => setForm(f => ({ ...f, sortOrder: Number(e.target.value) || 0 }))}
              className="w-32"
            />
            <p className="text-[10px] text-muted-foreground">Listede görünüm sırası (küçük → önce).</p>
          </div>

          {/* IsActive — sadece Edit */}
          {mode === 'edit' && (
            <FlagRow
              id="est-active"
              label="Aktif"
              hint="Pasif yapılan tipler yeni grup oluştururken seçilemez. Mevcut gruplar etkilenmez."
              checked={form.isActive}
              onChange={v => setForm(f => ({ ...f, isActive: v }))}
            />
          )}

          {/* Flag değişimi uyarısı (Karar #14) */}
          {flagsChanged && (
            <div className="flex items-start gap-2 rounded-md border border-amber-200 bg-amber-50 p-2.5 text-xs text-amber-800">
              <AlertTriangle className="h-4 w-4 mt-0.5 flex-shrink-0" />
              <span>
                Scope flag'lerini değiştirdiniz. Mevcut gruplar etkilenmez, ama yeni
                grup oluştururken farklı validation uygulanır.
              </span>
            </div>
          )}

          {validationError && (
            <p className="text-xs text-destructive">{validationError}</p>
          )}
          {mutation.isError && !validationError && (
            <p className="text-xs text-destructive">Kayıt başarısız oldu.</p>
          )}
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>İptal</Button>
          <Button onClick={handleSubmit} disabled={mutation.isPending}>
            {mutation.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
            {mode === 'create' ? 'Oluştur' : 'Kaydet'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

// ─── FlagRow helper ──────────────────────────────────────────────────

interface FlagRowProps {
  id: string;
  label: string;
  hint: string;
  checked: boolean;
  onChange: (v: boolean) => void;
}

function FlagRow({ id, label, hint, checked, onChange }: FlagRowProps) {
  return (
    <div className="flex items-start justify-between gap-3">
      <div className="space-y-0.5">
        <Label htmlFor={id} className="text-sm font-normal cursor-pointer">{label}</Label>
        <p className="text-[10px] text-muted-foreground leading-tight">{hint}</p>
      </div>
      <Switch id={id} checked={checked} onCheckedChange={onChange} />
    </div>
  );
}
