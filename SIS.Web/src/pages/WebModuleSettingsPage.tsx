import { useEffect, useMemo, useRef, useState, type ChangeEvent } from 'react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import {
  AlertDialog, AlertDialogAction, AlertDialogCancel, AlertDialogContent,
  AlertDialogDescription, AlertDialogFooter, AlertDialogHeader, AlertDialogTitle,
} from '@/components/ui/alert-dialog';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import { Switch } from '@/components/ui/switch';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Textarea } from '@/components/ui/textarea';
import { AlertCircle, Check, CheckCircle2, Download, Pencil, Search, Upload, X } from 'lucide-react';
import WebModuleSettingHistoryDrawer from '@/components/web-module-settings/WebModuleSettingHistoryDrawer';
import { exportWebModuleSettingsFile } from '../api/webModuleSettings.api';
import {
  useImportWebModuleSettings,
  useUpdateWebModuleSetting,
  useWebModuleSettingLimits,
  useWebModuleSettings,
} from '../hooks/useWebModuleSettings';
import { useDebounce } from '../hooks/useDebounce';
import { downloadBlob } from '../lib/download';
import { getErrorMessage } from '../lib/errorMessage';
import type { WebModuleSettingDto, WebModuleSettingLimits } from '../api/webModuleSettings.api';

const MODULE_LABELS: Record<string, string> = {
  StudentPanel: 'Öğrenci Paneli',
  InstructorPanel: 'Akademisyen Paneli',
  StaffPanel: 'Personel Paneli',
  Announcements: 'Duyurular',
};

const CATEGORY_LABELS: Record<string, string> = {
  Registration: 'Ders Kayıt',
  Documents: 'Belge İşlemleri',
  Privacy: 'Gizlilik',
  Announcements: 'Duyurular',
  General: 'Genel',
};

type Notice = {
  type: 'success' | 'error';
  message: string;
};

function toDateTimeInputValue(value: string | null) {
  if (!value) return '';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return '';
  const pad = (part: number) => String(part).padStart(2, '0');
  return [
    date.getFullYear(),
    '-',
    pad(date.getMonth() + 1),
    '-',
    pad(date.getDate()),
    'T',
    pad(date.getHours()),
    ':',
    pad(date.getMinutes()),
  ].join('');
}

function fromDateTimeInputValue(value: string) {
  return value ? new Date(value).toISOString() : null;
}

function formatEffectiveDate(value: string | null) {
  if (!value) return '—';
  return new Date(value).toLocaleString('tr-TR', {
    day: '2-digit',
    month: 'short',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
}

export default function WebModuleSettingsPage() {
  const { data: settings, isLoading, isError } = useWebModuleSettings();
  const { data: limits } = useWebModuleSettingLimits();
  const importMutation = useImportWebModuleSettings();
  const importInputRef = useRef<HTMLInputElement>(null);
  const [notice, setNotice] = useState<Notice | null>(null);
  const [isExporting, setIsExporting] = useState(false);
  const [searchText, setSearchText] = useState('');
  const debouncedSearch = useDebounce(searchText, 300);

  useEffect(() => {
    if (!notice) return;
    const duration = notice.type === 'error' ? 8000 : 4000;
    const t = setTimeout(() => setNotice(null), duration);
    return () => clearTimeout(t);
  }, [notice]);

  const filteredSettings = useMemo(() => {
    const searchLower = debouncedSearch.toLocaleLowerCase('tr-TR');

    return (settings ?? []).filter(setting => {
      if (!searchLower) return true;

      return (
        setting.key.toLocaleLowerCase('tr-TR').includes(searchLower)
        || setting.value.toLocaleLowerCase('tr-TR').includes(searchLower)
        || setting.moduleKey.toLocaleLowerCase('tr-TR').includes(searchLower)
        || (setting.description ?? '').toLocaleLowerCase('tr-TR').includes(searchLower)
        || (MODULE_LABELS[setting.moduleKey] ?? setting.moduleKey).toLocaleLowerCase('tr-TR').includes(searchLower)
        || (CATEGORY_LABELS[setting.category] ?? setting.category).toLocaleLowerCase('tr-TR').includes(searchLower)
      );
    });
  }, [debouncedSearch, settings]);

  const groupedByModule = useMemo(() => (
    filteredSettings.reduce<Record<string, WebModuleSettingDto[]>>((acc, setting) => {
      (acc[setting.moduleKey] ??= []).push(setting);
      return acc;
    }, {})
  ), [filteredSettings]);

  const modules = Object.keys(groupedByModule).sort((a, b) => (
    (MODULE_LABELS[a] ?? a).localeCompare(MODULE_LABELS[b] ?? b, 'tr')
  ));

  async function handleExport() {
    setNotice(null);
    setIsExporting(true);

    try {
      const file = await exportWebModuleSettingsFile();
      downloadBlob(file, 'web-module-settings.json');
    } catch (error) {
      setNotice({ type: 'error', message: getErrorMessage(error, 'Web modül ayarları dışa aktarılamadı.') });
    } finally {
      setIsExporting(false);
    }
  }

  async function handleImportChange(event: ChangeEvent<HTMLInputElement>) {
    const input = event.currentTarget;
    const file = input.files?.[0];
    if (!file) return;

    setNotice(null);

    try {
      const parsed = JSON.parse(await file.text()) as unknown;
      if (!Array.isArray(parsed)) {
        throw new Error('İçe aktarım dosyası bir JSON dizi formatında olmalıdır.');
      }

      const result = await importMutation.mutateAsync(parsed);
      setNotice({
        type: 'success',
        message: `${result.updatedCount} ayar güncellendi. ${result.processedCount} kayıt doğrulandı.`,
      });
    } catch (error) {
      setNotice({ type: 'error', message: getErrorMessage(error, 'Web modül ayarları içe aktarılamadı.') });
    } finally {
      input.value = '';
    }
  }

  if (isLoading) return <Skeleton className="h-96 rounded-xl" />;
  if (isError) {
    return (
      <Alert variant="destructive">
        <AlertCircle className="h-4 w-4" />
        <AlertDescription>Web modül ayarları yüklenemedi.</AlertDescription>
      </Alert>
    );
  }

  return (
    <div className="space-y-6">
      {notice && (
        <Alert variant={notice.type === 'error' ? 'destructive' : undefined} className="py-2">
          {notice.type === 'error'
            ? <AlertCircle className="h-4 w-4" />
            : <CheckCircle2 className="h-4 w-4" />}
          <AlertDescription>{notice.message}</AlertDescription>
        </Alert>
      )}

      <div className="flex items-center gap-3 rounded-xl border bg-card px-4 py-2.5">
        <div className="relative w-80 max-w-full">
          <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
          <Input
            placeholder="Ayar ara..."
            value={searchText}
            onChange={e => setSearchText(e.target.value)}
            className="h-9 rounded-lg border bg-muted/50 pl-9 text-sm"
          />
        </div>
        <div className="flex-1" />
        <div className="h-5 w-px bg-border" />
        <div className="flex items-center gap-2">
          <Button variant="ghost" size="sm" className="h-9" onClick={handleExport} disabled={isExporting}>
            <Download className="mr-1.5 h-3.5 w-3.5" />
            {isExporting ? 'Hazırlanıyor...' : 'JSON İndir'}
          </Button>
          <Button variant="ghost" size="sm" className="h-9" onClick={() => importInputRef.current?.click()} disabled={importMutation.isPending}>
            <Upload className="mr-1.5 h-3.5 w-3.5" />
            {importMutation.isPending ? 'İçe Aktarılıyor...' : 'JSON Yükle'}
          </Button>
          <input
            ref={importInputRef}
            type="file"
            accept="application/json,.json"
            className="hidden"
            onChange={handleImportChange}
          />
        </div>
      </div>

      {modules.length === 0 && debouncedSearch && (
        <div className="rounded-lg border border-dashed p-6 text-center text-sm text-muted-foreground">
          &ldquo;{debouncedSearch}&rdquo; ile eşleşen ayar bulunamadı.
        </div>
      )}

      {modules.length > 0 && (
        <Tabs defaultValue={modules[0]} className="space-y-4">
          <TabsList className="h-auto flex-wrap justify-start">
            {modules.map(moduleKey => (
              <TabsTrigger key={moduleKey} value={moduleKey}>
                {MODULE_LABELS[moduleKey] ?? moduleKey}
              </TabsTrigger>
            ))}
          </TabsList>

          {modules.map(moduleKey => (
            <TabsContent key={moduleKey} value={moduleKey}>
              <ModuleSettingsCard
                moduleKey={moduleKey}
                settings={groupedByModule[moduleKey]}
                limits={limits}
                onNotice={setNotice}
              />
            </TabsContent>
          ))}
        </Tabs>
      )}
    </div>
  );
}

function ModuleSettingsCard({
  moduleKey,
  settings,
  limits,
  onNotice,
}: {
  moduleKey: string;
  settings: WebModuleSettingDto[];
  limits?: WebModuleSettingLimits;
  onNotice: (notice: Notice) => void;
}) {
  const grouped = settings.reduce<Record<string, WebModuleSettingDto[]>>((acc, setting) => {
    (acc[setting.category || 'General'] ??= []).push(setting);
    return acc;
  }, {});

  const categories = Object.keys(grouped).sort((a, b) => (
    (CATEGORY_LABELS[a] ?? a).localeCompare(CATEGORY_LABELS[b] ?? b, 'tr')
  ));

  return (
    <Card>
      <CardHeader className="pb-3">
        <CardTitle className="text-sm">
          <Badge variant="outline" className="mr-2">{MODULE_LABELS[moduleKey] ?? moduleKey}</Badge>
          {settings.length} ayar
        </CardTitle>
      </CardHeader>
      <CardContent className="space-y-6">
        {categories.map(category => (
          <div key={category} className="space-y-3">
            <div className="flex items-center gap-2">
              <Badge variant="secondary" className="text-[10px]">{CATEGORY_LABELS[category] ?? category}</Badge>
              <div className="h-px flex-1 bg-border" />
            </div>
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Ayar</TableHead>
                  <TableHead>Değer</TableHead>
                  <TableHead className="w-56">Geçerlilik</TableHead>
                  <TableHead className="w-20">Tip</TableHead>
                  <TableHead className="w-32 text-right">İşlem</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {grouped[category].map(setting => (
                  <WebModuleSettingRow
                    key={setting.id}
                    setting={setting}
                    limits={limits}
                    onNotice={onNotice}
                  />
                ))}
              </TableBody>
            </Table>
          </div>
        ))}
      </CardContent>
    </Card>
  );
}

function WebModuleSettingRow({
  setting,
  limits,
  onNotice,
}: {
  setting: WebModuleSettingDto;
  limits?: WebModuleSettingLimits;
  onNotice: (notice: Notice) => void;
}) {
  const [editing, setEditing] = useState(false);
  const [value, setValue] = useState(setting.value);
  const [effectiveFrom, setEffectiveFrom] = useState(toDateTimeInputValue(setting.effectiveFrom));
  const [effectiveTo, setEffectiveTo] = useState(toDateTimeInputValue(setting.effectiveTo));
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [confirmOpen, setConfirmOpen] = useState(false);
  const updateMutation = useUpdateWebModuleSetting();

  useEffect(() => {
    if (!editing) {
      setValue(setting.value);
      setEffectiveFrom(toDateTimeInputValue(setting.effectiveFrom));
      setEffectiveTo(toDateTimeInputValue(setting.effectiveTo));
    }
  }, [editing, setting.effectiveFrom, setting.effectiveTo, setting.value]);

  const compositeKey = `${setting.moduleKey}.${setting.key}`;
  const limit = limits?.[compositeKey];
  const limitHint = limit
    ? `Min: ${limit.min ?? '—'}, Max: ${limit.max ?? '—'}`
    : null;
  const label = `${MODULE_LABELS[setting.moduleKey] ?? setting.moduleKey}.${setting.key}`;

  function buildPayload() {
    return {
      value,
      effectiveFrom: fromDateTimeInputValue(effectiveFrom),
      effectiveTo: fromDateTimeInputValue(effectiveTo),
    };
  }

  function doSave() {
    setErrorMessage(null);
    updateMutation.mutate(
      { id: setting.id, payload: buildPayload() },
      {
        onSuccess: updatedSetting => {
          setValue(updatedSetting.value);
          setEffectiveFrom(toDateTimeInputValue(updatedSetting.effectiveFrom));
          setEffectiveTo(toDateTimeInputValue(updatedSetting.effectiveTo));
          setEditing(false);
          onNotice({ type: 'success', message: `${setting.key} güncellendi.` });
        },
        onError: error => {
          setErrorMessage(getErrorMessage(error, 'Ayar kaydedilemedi.'));
        },
      },
    );
  }

  function handleSave() {
    const nextPayload = buildPayload();
    const unchanged = value === setting.value
      && nextPayload.effectiveFrom === setting.effectiveFrom
      && nextPayload.effectiveTo === setting.effectiveTo;

    if (unchanged) {
      setEditing(false);
      return;
    }

    setConfirmOpen(true);
  }

  function handleCancel() {
    setValue(setting.value);
    setEffectiveFrom(toDateTimeInputValue(setting.effectiveFrom));
    setEffectiveTo(toDateTimeInputValue(setting.effectiveTo));
    setErrorMessage(null);
    setEditing(false);
  }

  function renderEditInput() {
    if (setting.dataType === 'bool') {
      const checked = value === 'true' || value === '1' || value === 'yes' || value === 'on';
      return (
        <div className="flex items-center gap-2">
          <Switch checked={checked} onCheckedChange={c => setValue(c ? 'true' : 'false')} />
          <span className="text-xs text-muted-foreground">{checked ? 'Aktif' : 'Pasif'}</span>
        </div>
      );
    }

    if (setting.dataType === 'json') {
      return (
        <Textarea
          className="min-h-[80px] w-64 font-mono text-xs"
          value={value}
          onChange={e => setValue(e.target.value)}
          onKeyDown={e => {
            if (e.key === 'Escape') handleCancel();
          }}
          autoFocus
        />
      );
    }

    if (setting.dataType === 'int' || setting.dataType === 'decimal') {
      return (
        <div className="space-y-1">
          <Input
            type="number"
            className="h-8 w-56 text-sm"
            value={value}
            step={setting.dataType === 'decimal' ? '0.01' : '1'}
            onChange={e => setValue(e.target.value)}
            onKeyDown={e => {
              if (e.key === 'Enter') handleSave();
              if (e.key === 'Escape') handleCancel();
            }}
            autoFocus
          />
          {limitHint && <p className="text-[10px] text-muted-foreground">{limitHint}</p>}
          {setting.dataType === 'decimal' && (
            <p className="text-[10px] text-amber-600">Ondalık ayracı olarak nokta (.) kullanın</p>
          )}
          {errorMessage && <p className="max-w-md text-[11px] text-red-600">{errorMessage}</p>}
        </div>
      );
    }

    return (
      <div className="space-y-1">
        <Input
          type={setting.dataType === 'time' ? 'time' : 'text'}
          className="h-8 w-56 text-sm"
          value={value}
          onChange={e => setValue(e.target.value)}
          onKeyDown={e => {
            if (e.key === 'Enter') handleSave();
            if (e.key === 'Escape') handleCancel();
          }}
          autoFocus
        />
        {errorMessage && <p className="max-w-md text-[11px] text-red-600">{errorMessage}</p>}
      </div>
    );
  }

  function renderDisplayValue() {
    if (setting.dataType === 'bool') {
      const checked = setting.value === 'true' || setting.value === '1';
      return (
        <Badge variant={checked ? 'default' : 'secondary'} className="text-[10px]">
          {checked ? 'Aktif' : 'Pasif'}
        </Badge>
      );
    }

    return <span className="text-sm font-medium">{setting.value}</span>;
  }

  function renderEffectiveRange() {
    if (editing) {
      return (
        <div className="grid gap-2">
          <Input
            type="datetime-local"
            className="h-8 text-xs"
            value={effectiveFrom}
            onChange={e => setEffectiveFrom(e.target.value)}
          />
          <Input
            type="datetime-local"
            className="h-8 text-xs"
            value={effectiveTo}
            onChange={e => setEffectiveTo(e.target.value)}
          />
        </div>
      );
    }

    if (!setting.effectiveFrom && !setting.effectiveTo) {
      return <span className="text-xs text-muted-foreground">Sürekli</span>;
    }

    return (
      <div className="space-y-0.5 text-xs text-muted-foreground">
        <p>{formatEffectiveDate(setting.effectiveFrom)}</p>
        <p>{formatEffectiveDate(setting.effectiveTo)}</p>
      </div>
    );
  }

  return (
    <>
      <TableRow>
        <TableCell>
          <div className="space-y-0.5">
            <p className="text-sm font-medium leading-tight">{setting.description || setting.key}</p>
            <p className="font-mono text-[11px] text-muted-foreground">{compositeKey}</p>
          </div>
        </TableCell>
        <TableCell>{editing ? renderEditInput() : renderDisplayValue()}</TableCell>
        <TableCell>{renderEffectiveRange()}</TableCell>
        <TableCell>
          <Badge variant="secondary" className="text-[10px]">{setting.dataType}</Badge>
        </TableCell>
        <TableCell>
          {editing ? (
            <div className="flex justify-end gap-1">
              <Button size="icon" variant="ghost" className="h-7 w-7" onClick={handleSave} disabled={updateMutation.isPending}>
                <Check className="h-4 w-4 text-green-600" />
              </Button>
              <Button size="icon" variant="ghost" className="h-7 w-7" onClick={handleCancel}>
                <X className="h-4 w-4" />
              </Button>
            </div>
          ) : (
            <div className="flex justify-end gap-1">
              <WebModuleSettingHistoryDrawer
                settingId={setting.id}
                label={label}
                currentValue={setting.value}
                currentEffectiveFrom={setting.effectiveFrom}
                currentEffectiveTo={setting.effectiveTo}
              />
              <Button size="icon" variant="ghost" className="h-7 w-7" onClick={() => setEditing(true)}>
                <Pencil className="h-4 w-4" />
              </Button>
            </div>
          )}
        </TableCell>
      </TableRow>

      <AlertDialog open={confirmOpen} onOpenChange={setConfirmOpen}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Ayar değişikliğini onayla</AlertDialogTitle>
            <AlertDialogDescription asChild>
              <div>
                <span className="font-mono text-xs">{compositeKey}</span> değerini{' '}
                <span className="font-semibold">{setting.value}</span> &rarr;{' '}
                <span className="font-semibold">{value}</span> olarak değiştirmek istediğinize emin misiniz?
              </div>
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>İptal</AlertDialogCancel>
            <AlertDialogAction onClick={doSave}>Onayla</AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </>
  );
}
