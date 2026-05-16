import { useEffect, useMemo, useState } from 'react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  Dialog, DialogDescription, DialogFooter, DialogHeader, DialogTitle,
} from '@/components/ui/dialog';
import ResizableDialogContent from '@/components/ui/resizable-dialog';
import { Input } from '@/components/ui/input';
import { Switch } from '@/components/ui/switch';
import { AlertCircle, Check } from 'lucide-react';
import { useSystemConfigs, useUpdateSystemConfig } from '../../hooks/useSystemConfig';
import { TIMESLOT_GRID_SETTINGS, type GridSettingMeta, validateGridSetting } from '../../lib/timeSlotGridConfig';
import { getErrorMessage } from '../../lib/errorMessage';

interface TemplateSettingsDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

export function TemplateSettingsDialog({ open, onOpenChange }: TemplateSettingsDialogProps) {
  const { data: configs } = useSystemConfigs();
  const updateMutation = useUpdateSystemConfig();
  const [notice, setNotice] = useState<{ type: 'success' | 'error'; message: string } | null>(null);

  useEffect(() => {
    if (!notice) return;
    const t = setTimeout(() => setNotice(null), notice.type === 'error' ? 8000 : 4000);
    return () => clearTimeout(t);
  }, [notice]);

  const byKey = new Map((configs ?? []).map(c => [c.key, c]));
  // Validation için: key → value (current) map'i. Cross-field kontrolü diğer ayarın
  // güncel değerini buradan okur.
  const valueMap = useMemo(
    () => new Map((configs ?? []).map(c => [c.key, c.value])),
    [configs],
  );

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <ResizableDialogContent
        defaultWidth={680}
        defaultHeight={640}
        minWidth={420}
        minHeight={360}
        className="p-0 flex flex-col gap-0"
      >
        <DialogHeader className="px-6 pt-6 pb-4 pr-28 border-b shrink-0">
          <DialogTitle>Şablon Ayarları</DialogTitle>
          <DialogDescription>
            Bu ayarlar, <strong>yeni bir dönem açıldığında</strong> ya da
            <strong> "Yeniden Üret"</strong> butonuna basıldığında devreye girer ve
            sistem otomatik olarak ders saatlerini buna göre oluşturur.
            <br />
            <span className="text-xs text-amber-700">
              Şu anki ders saatlerini değiştirmez. Bu ekrandaki değişiklikler yalnızca bir sonraki üretimde etkili olur.
            </span>
          </DialogDescription>
        </DialogHeader>

        {notice && (
          <Alert variant={notice.type === 'error' ? 'destructive' : undefined} className="mx-6 mt-3 py-2 shrink-0">
            {notice.type === 'error' ? <AlertCircle className="h-4 w-4" /> : <Check className="h-4 w-4" />}
            <AlertDescription>{notice.message}</AlertDescription>
          </Alert>
        )}

        <div className="flex-1 overflow-y-auto px-6 py-4 space-y-3 min-h-0">
          {TIMESLOT_GRID_SETTINGS.map(setting => {
            const config = byKey.get(setting.key);
            if (!config) {
              return (
                <div key={setting.key} className="rounded-lg border border-dashed border-amber-200 bg-amber-50 p-2 text-xs text-amber-700">
                  <strong>{setting.label}</strong> — bu ayar henüz tanımlanmamış.
                </div>
              );
            }
            return (
              <SettingRow
                key={setting.key}
                setting={setting}
                value={config.value}
                validate={(newValue) => validateGridSetting(setting.key, newValue, valueMap)}
                onSave={async (newValue) => {
                  try {
                    await updateMutation.mutateAsync({ id: config.id, value: newValue });
                    setNotice({ type: 'success', message: `${setting.label} güncellendi.` });
                  } catch (e) {
                    setNotice({ type: 'error', message: getErrorMessage(e, 'Ayar kaydedilemedi.') });
                  }
                }}
                isPending={updateMutation.isPending}
              />
            );
          })}
        </div>

        <DialogFooter className="px-6 py-3 border-t shrink-0">
          <Button variant="outline" onClick={() => onOpenChange(false)}>Kapat</Button>
        </DialogFooter>
      </ResizableDialogContent>
    </Dialog>
  );
}

interface SettingRowProps {
  setting: GridSettingMeta;
  value: string;
  validate: (newValue: string) => string | null;
  onSave: (value: string) => Promise<void>;
  isPending: boolean;
}

function SettingRow({ setting, value, validate, onSave, isPending }: SettingRowProps) {
  const [editing, setEditing] = useState(false);
  const [draft, setDraft] = useState(value);

  useEffect(() => {
    if (!editing) {
      // eslint-disable-next-line react-hooks/set-state-in-effect -- resync from props on exit
      setDraft(value);
    }
  }, [value, editing]);

  // Canlı validation: draft her değiştiğinde hata mesajı.
  const validationError = editing && draft !== value ? validate(draft) : null;

  function renderInput() {
    if (setting.type === 'bool') {
      const checked = draft === 'true' || draft === '1' || draft === 'yes' || draft === 'on';
      return (
        <div className="flex items-center gap-2">
          <Switch checked={checked} onCheckedChange={c => setDraft(c ? 'true' : 'false')} />
          <span className="text-xs text-muted-foreground">{checked ? 'Aktif' : 'Pasif'}</span>
        </div>
      );
    }
    if (setting.type === 'time') {
      return (
        <Input
          type="time"
          className="h-8 w-32"
          value={draft.length >= 5 ? draft.substring(0, 5) : draft}
          onChange={e => setDraft(e.target.value)}
        />
      );
    }
    if (setting.type === 'int') {
      return (
        <Input
          type="number"
          className="h-8 w-24"
          value={draft}
          onChange={e => setDraft(e.target.value)}
        />
      );
    }
    return (
      <Input
        className="h-8 w-72"
        value={draft}
        onChange={e => setDraft(e.target.value)}
      />
    );
  }

  function renderDisplay() {
    if (setting.type === 'bool') {
      const checked = value === 'true' || value === '1';
      return <Badge variant={checked ? 'default' : 'secondary'} className="text-[10px]">{checked ? 'Aktif' : 'Pasif'}</Badge>;
    }
    return <span className="text-sm font-medium">{value}</span>;
  }

  return (
    <div className="rounded-lg border bg-card p-3">
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0 flex-1">
          <p className="text-sm font-medium">{setting.label}</p>
          {setting.helper && <p className="mt-0.5 text-xs text-muted-foreground">{setting.helper}</p>}
        </div>
        <div className="flex items-center gap-2 shrink-0">
          {editing ? (
            <>
              {renderInput()}
              <Button
                size="sm"
                disabled={isPending || draft === value || validationError !== null}
                onClick={async () => {
                  await onSave(draft);
                  setEditing(false);
                }}
              >
                Kaydet
              </Button>
              <Button size="sm" variant="ghost" onClick={() => { setDraft(value); setEditing(false); }}>
                İptal
              </Button>
            </>
          ) : (
            <>
              {renderDisplay()}
              <Button size="sm" variant="outline" onClick={() => setEditing(true)}>
                Düzenle
              </Button>
            </>
          )}
        </div>
      </div>
      {validationError && (
        <p className="mt-2 text-xs text-destructive flex items-start gap-1.5">
          <AlertCircle className="h-3.5 w-3.5 mt-0.5 shrink-0" />
          <span>{validationError}</span>
        </p>
      )}
    </div>
  );
}
