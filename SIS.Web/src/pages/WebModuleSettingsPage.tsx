import { useDeferredValue, useEffect, useMemo, useRef, useState, type ChangeEvent } from 'react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Skeleton } from '@/components/ui/skeleton';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { AlertCircle, CheckCircle2, Download, Loader2, Pencil, Search, Upload } from 'lucide-react';
import WebModuleSettingHistoryDrawer from '../components/web-module-settings/WebModuleSettingHistoryDrawer';
import {
  useImportWebModuleSettings,
  useUpdateWebModuleControl,
  useUpdateWebModuleSetting,
  useWebModuleControlSummaries,
  useWebModuleSettingLimits,
  useWebModuleSettings,
} from '../hooks/useWebModuleSettings';
import { exportWebModuleSettingsFile } from '../api/webModuleSettings.api';
import type {
  WebModuleControlSummaryDto,
  WebModuleSettingDto,
  WebModuleSettingLimits,
} from '../api/webModuleSettings.api';
import { usePermissions } from '../context/PermissionContext';
import { downloadBlob } from '../lib/download';
import { getErrorMessage } from '../lib/errorMessage';
import { cn } from '../lib/utils';

type Notice = {
  type: 'success' | 'error';
  message: string;
};

const SETTING_CATEGORY_LABELS: Record<string, string> = {
  Registration: 'Ders Kayıt',
  Announcements: 'Duyurular',
};

function settingCategoryLabel(category: string) {
  return SETTING_CATEGORY_LABELS[category] ?? category;
}

export default function WebModuleSettingsPage() {
  const { hasPermission } = usePermissions();
  const [notice, setNotice] = useState<Notice | null>(null);

  const summaries = useWebModuleControlSummaries();
  const rawSettings = useWebModuleSettings();
  const limits = useWebModuleSettingLimits();
  const importMutation = useImportWebModuleSettings();
  const importInputRef = useRef<HTMLInputElement>(null);
  const [isExporting, setIsExporting] = useState(false);

  const canExport = hasPermission('menu.web-modul-ayarlari.export');
  const canImport = hasPermission('menu.web-modul-ayarlari.import');

  useEffect(() => {
    if (!notice) return;
    const timer = setTimeout(() => setNotice(null), notice.type === 'error' ? 8000 : 4000);
    return () => clearTimeout(timer);
  }, [notice]);

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
      if (!Array.isArray(parsed))
        throw new Error('İçe aktarım dosyası bir JSON dizi formatında olmalıdır.');

      const result = await importMutation.mutateAsync(parsed);
      setNotice({
        type: 'success',
        message: `${result.updatedCount} ayar güncellendi (${result.processedCount} kayıt doğrulandı).`,
      });
    } catch (error) {
      setNotice({ type: 'error', message: getErrorMessage(error, 'Web modül ayarları içe aktarılamadı.') });
    } finally {
      input.value = '';
    }
  }

  const isLoading = summaries.isLoading;
  const isError = summaries.isError;

  return (
    <div className="space-y-4">
      {notice && (
        <Alert variant={notice.type === 'error' ? 'destructive' : undefined}>
          {notice.type === 'error'
            ? <AlertCircle className="h-4 w-4" />
            : <CheckCircle2 className="h-4 w-4" />}
          <AlertDescription>{notice.message}</AlertDescription>
        </Alert>
      )}

      <div className="overflow-hidden rounded-md border border-slate-300 bg-white shadow-sm">
        <div className="flex flex-col gap-3 border-b border-slate-200 bg-slate-50 px-5 py-4 lg:flex-row lg:items-center lg:justify-between">
          <div>
            <p className="text-sm font-semibold text-slate-700">Web Modül Ayarları</p>
            <p className="mt-1 text-xs leading-5 text-slate-500">
              Modül durumları ve genel ayarlar; ders kayıt, danışman onayı, not girişi, not yayınlama ve
              duyuru akışları canlı olarak bu sayfadan okunur.
            </p>
          </div>

          {(canExport || canImport) && (
            <div className="flex shrink-0 items-center gap-2">
              {canExport && (
                <Button
                  type="button"
                  variant="outline"
                  className="h-10 rounded-sm border-slate-300 px-4 text-sm text-slate-700"
                  onClick={handleExport}
                  disabled={isExporting}
                >
                  <Download className="mr-2 h-4 w-4" />
                  {isExporting ? 'Hazırlanıyor...' : 'JSON İndir'}
                </Button>
              )}
              {canImport && (
                <>
                  <Button
                    type="button"
                    variant="outline"
                    className="h-10 rounded-sm border-slate-300 px-4 text-sm text-slate-700"
                    onClick={() => importInputRef.current?.click()}
                    disabled={importMutation.isPending}
                  >
                    <Upload className="mr-2 h-4 w-4" />
                    {importMutation.isPending ? 'Yükleniyor...' : 'JSON Yükle'}
                  </Button>
                  <input
                    ref={importInputRef}
                    type="file"
                    accept="application/json,.json"
                    className="hidden"
                    onChange={handleImportChange}
                  />
                </>
              )}
            </div>
          )}
        </div>

        {isLoading ? (
          <div className="space-y-3 p-5">
            <Skeleton className="h-12 rounded-md" />
            <Skeleton className="h-[36rem] rounded-md" />
          </div>
        ) : isError ? (
          <div className="p-5">
            <Alert variant="destructive">
              <AlertCircle className="h-4 w-4" />
              <AlertDescription>Web modül ayarları yüklenemedi.</AlertDescription>
            </Alert>
          </div>
        ) : (
          <Tabs defaultValue="modules" className="w-full">
            <div className="border-b border-slate-200 px-5 pt-4">
              <TabsList className="rounded-md">
                <TabsTrigger value="modules">Modül Kontrolleri</TabsTrigger>
                <TabsTrigger value="settings">Genel Ayarlar</TabsTrigger>
              </TabsList>
            </div>

            <TabsContent value="modules" className="mt-0">
              <ModuleControlsTab data={summaries.data ?? []} onNotice={setNotice} />
            </TabsContent>

            <TabsContent value="settings" className="mt-0">
              <RawSettingsTab
                settings={rawSettings.data ?? []}
                isLoading={rawSettings.isLoading}
                isError={rawSettings.isError}
                limits={limits.data}
                onNotice={setNotice}
              />
            </TabsContent>
          </Tabs>
        )}
      </div>
    </div>
  );
}

function ModuleControlsTab({
  data,
  onNotice,
}: {
  data: WebModuleControlSummaryDto[];
  onNotice: (notice: Notice) => void;
}) {
  const [searchText, setSearchText] = useState('');
  const deferredSearch = useDeferredValue(searchText.trim().toLocaleLowerCase('tr-TR'));

  const filteredModules = useMemo(() => {
    const modules = [...data]
      .sort((left, right) => left.displayOrder - right.displayOrder || left.title.localeCompare(right.title, 'tr'));

    if (!deferredSearch) return modules;

    return modules.filter(module => (
      module.title.toLocaleLowerCase('tr-TR').includes(deferredSearch)
      || module.category.toLocaleLowerCase('tr-TR').includes(deferredSearch)
      || module.description.toLocaleLowerCase('tr-TR').includes(deferredSearch)
      || (module.announcement ?? '').toLocaleLowerCase('tr-TR').includes(deferredSearch)
      || (module.effectiveMessage ?? '').toLocaleLowerCase('tr-TR').includes(deferredSearch)
    ));
  }, [data, deferredSearch]);

  return (
    <div>
      <div className="border-b border-slate-200 px-5 py-4">
        <div className="relative lg:max-w-md">
          <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" />
          <Input
            value={searchText}
            onChange={event => setSearchText(event.target.value)}
            placeholder="Modül ara (ad, kategori, duyuru)"
            className="h-10 rounded-sm border-slate-300 pl-10"
          />
        </div>
      </div>

      <div className="hidden grid-cols-[minmax(0,1.55fr)_minmax(280px,320px)_minmax(340px,1fr)_130px] border-b border-slate-300 bg-slate-700 px-5 py-2.5 text-sm font-semibold text-white lg:grid">
        <div>Modül Adı</div>
        <div>Durum</div>
        <div>Duyuru</div>
        <div className="text-right">Kaydet</div>
      </div>

      {filteredModules.length === 0 ? (
        <div className="px-5 py-10 text-sm text-slate-500">
          Arama ölçütüne uyan modül bulunamadı.
        </div>
      ) : (
        <div>
          {filteredModules.map((module, index) => (
            <ModuleControlRow
              key={module.code}
              module={module}
              zebra={index % 2 === 1}
              onNotice={onNotice}
            />
          ))}
        </div>
      )}
    </div>
  );
}

function ModuleControlRow({
  module,
  zebra,
  onNotice,
}: {
  module: WebModuleControlSummaryDto;
  zebra: boolean;
  onNotice: (notice: Notice) => void;
}) {
  const mutation = useUpdateWebModuleControl();
  const [mode, setMode] = useState(module.selectedMode);
  const [announcement, setAnnouncement] = useState(module.announcement ?? '');

  useEffect(() => {
    setMode(module.selectedMode);
    setAnnouncement(module.announcement ?? '');
  }, [module.selectedMode, module.announcement, module.code]);

  const isDirty = mode !== module.selectedMode || announcement !== (module.announcement ?? '');
  const selectedOption = module.options.find(option => option.code === mode) ?? module.options[0];
  const preview = getPreviewState(module.title, mode, module, isDirty);
  const isClosedState = preview.label.toLocaleLowerCase('tr-TR') === 'kapalı';

  function handleSave() {
    mutation.mutate(
      {
        code: module.code,
        payload: {
          mode,
          announcement: announcement.trim() || null,
        },
      },
      {
        onSuccess: () => onNotice({ type: 'success', message: `${module.title} güncellendi.` }),
        onError: error => onNotice({ type: 'error', message: getErrorMessage(error, 'Web modülü güncellenemedi.') }),
      },
    );
  }

  return (
    <div
      className={cn(
        'border-b border-slate-200 px-5 py-4 last:border-b-0',
        isDirty ? 'bg-indigo-50/70' : zebra ? 'bg-slate-50/70' : 'bg-white',
      )}
    >
      <div className="grid gap-4 lg:grid-cols-[minmax(0,1.55fr)_minmax(280px,320px)_minmax(340px,1fr)_130px] lg:items-start">
        <div className="space-y-1.5">
          <div className="flex flex-wrap items-center gap-2">
            <p className="text-[15px] font-medium text-slate-900">{module.title}</p>
            <span className={cn(
              'rounded-md border px-2.5 py-1 text-[11px] font-semibold',
              isClosedState
                ? 'border-red-200 bg-red-100 text-red-700'
                : preview.isOpen
                  ? 'border-emerald-200 bg-emerald-100 text-emerald-700'
                  : 'border-amber-200 bg-amber-100 text-amber-700',
            )}>
              {preview.label}
            </span>
          </div>

          <p className="text-xs text-slate-500">{module.description}</p>
          {preview.message && (
            <p className="text-xs leading-5 text-slate-600">{preview.message}</p>
          )}
        </div>

        <div className="space-y-1">
          <p className="text-[11px] font-semibold uppercase tracking-[0.14em] text-slate-500 lg:hidden">
            Durum
          </p>
          <Select value={mode} onValueChange={setMode}>
            <SelectTrigger className={cn(
              'h-11 rounded-md bg-white text-[15px]',
              isClosedState
                ? 'border-red-300 bg-red-50 text-red-700 focus:ring-red-200'
                : 'border-slate-300',
            )}>
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {module.options.map(option => (
                <SelectItem key={option.code} value={option.code}>
                  {option.label}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
          <p className="text-[11px] leading-4 text-slate-500">{selectedOption.description}</p>
        </div>

        <div className="space-y-1">
          <p className="text-[11px] font-semibold uppercase tracking-[0.14em] text-slate-500 lg:hidden">
            Duyuru
          </p>
          <Input
            value={announcement}
            onChange={event => setAnnouncement(event.target.value)}
            placeholder="Kullanıcı duyurusu"
            className="h-11 rounded-md border-slate-300 text-[15px]"
          />
        </div>

        <div className="flex items-start justify-end">
          <Button
            type="button"
            size="sm"
            className="h-11 w-full rounded-md bg-slate-800 px-4 text-white hover:bg-slate-700 lg:w-[120px]"
            disabled={!isDirty || mutation.isPending}
            onClick={handleSave}
          >
            {mutation.isPending ? <Loader2 className="h-4 w-4 animate-spin" /> : 'Kaydet'}
          </Button>
        </div>
      </div>
    </div>
  );
}

function RawSettingsTab({
  settings,
  isLoading,
  isError,
  limits,
  onNotice,
}: {
  settings: WebModuleSettingDto[];
  isLoading: boolean;
  isError: boolean;
  limits?: WebModuleSettingLimits;
  onNotice: (notice: Notice) => void;
}) {
  const editableSettings = useMemo(
    () => [...settings]
      .filter(setting => setting.category !== 'ModuleControl')
      .sort((left, right) =>
        settingCategoryLabel(left.category).localeCompare(settingCategoryLabel(right.category), 'tr')
        || (left.description ?? left.key).localeCompare(right.description ?? right.key, 'tr')),
    [settings],
  );

  if (isLoading) {
    return <div className="p-5"><Skeleton className="h-[28rem] rounded-md" /></div>;
  }

  if (isError) {
    return (
      <div className="p-5">
        <Alert variant="destructive">
          <AlertCircle className="h-4 w-4" />
          <AlertDescription>Genel ayarlar yüklenemedi.</AlertDescription>
        </Alert>
      </div>
    );
  }

  return (
    <div>
      <div className="border-b border-slate-200 px-5 py-3">
        <p className="text-xs leading-5 text-slate-500">
          Bu ayarlar duyuru limitleri ve ders kayıt günlük açılış saati gibi parametreleri yönetir.
          Değişiklikler ilgili backend akışlarınca anında okunur.
        </p>
      </div>

      <div className="hidden grid-cols-[150px_minmax(0,1fr)_minmax(200px,260px)_92px] border-b border-slate-300 bg-slate-700 px-5 py-2.5 text-sm font-semibold text-white lg:grid">
        <div>Grup</div>
        <div>Tanımı</div>
        <div>Değeri</div>
        <div className="text-right">İşlem</div>
      </div>

      {editableSettings.length === 0 ? (
        <div className="px-5 py-10 text-sm text-slate-500">Düzenlenebilir genel ayar bulunamadı.</div>
      ) : (
        <div>
          {editableSettings.map((setting, index) => (
            <RawSettingRow
              key={setting.id}
              setting={setting}
              limit={limits?.[`${setting.moduleKey}.${setting.key}`]}
              zebra={index % 2 === 1}
              onNotice={onNotice}
            />
          ))}
        </div>
      )}
    </div>
  );
}

function RawSettingRow({
  setting,
  limit,
  zebra,
  onNotice,
}: {
  setting: WebModuleSettingDto;
  limit?: { min: number | null; max: number | null };
  zebra: boolean;
  onNotice: (notice: Notice) => void;
}) {
  const updateMutation = useUpdateWebModuleSetting();
  const [dialogOpen, setDialogOpen] = useState(false);
  const [draftValue, setDraftValue] = useState(setting.value);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const label = setting.description || `${setting.moduleKey}.${setting.key}`;

  useEffect(() => {
    if (!dialogOpen) {
      setDraftValue(setting.value);
      setErrorMessage(null);
    }
  }, [setting.value, dialogOpen]);

  function handleSave() {
    setErrorMessage(null);
    updateMutation.mutate(
      {
        id: setting.id,
        payload: {
          value: draftValue,
          effectiveFrom: setting.effectiveFrom,
          effectiveTo: setting.effectiveTo,
        },
      },
      {
        onSuccess: () => {
          onNotice({ type: 'success', message: `${label} güncellendi.` });
          setDialogOpen(false);
        },
        onError: error => setErrorMessage(getErrorMessage(error, 'Ayar kaydedilemedi.')),
      },
    );
  }

  return (
    <div className={cn(
      'grid gap-3 border-b border-slate-200 px-5 py-4 last:border-b-0 lg:grid-cols-[150px_minmax(0,1fr)_minmax(200px,260px)_92px] lg:items-center',
      zebra ? 'bg-slate-50/70' : 'bg-white',
    )}>
      <div className="text-sm font-medium text-slate-600">{settingCategoryLabel(setting.category)}</div>

      <div className="space-y-1">
        <p className="text-[15px] font-medium text-slate-900">{setting.description || setting.key}</p>
        <p className="font-mono text-[11px] text-slate-400">{setting.moduleKey}.{setting.key}</p>
      </div>

      <div>
        {setting.dataType === 'bool' ? (
          <Badge className={cn(
            'rounded-md px-2.5 py-1 text-xs font-semibold',
            setting.value === 'true'
              ? 'bg-emerald-100 text-emerald-700 hover:bg-emerald-100'
              : 'bg-slate-100 text-slate-700 hover:bg-slate-100',
          )}>
            {setting.value === 'true' ? 'Açık' : 'Kapalı'}
          </Badge>
        ) : (
          <span className="block break-all text-[15px] font-medium text-slate-800">{setting.value}</span>
        )}
      </div>

      <div className="flex items-center justify-end gap-1">
        <WebModuleSettingHistoryDrawer
          settingId={setting.id}
          label={label}
          currentValue={setting.value}
          currentEffectiveFrom={setting.effectiveFrom}
          currentEffectiveTo={setting.effectiveTo}
        />
        <Button
          type="button"
          size="icon"
          variant="ghost"
          className="h-9 w-9 rounded-md text-slate-700 hover:bg-slate-100"
          onClick={() => setDialogOpen(true)}
          title="Düzenle"
        >
          <Pencil className="h-4 w-4" />
        </Button>
      </div>

      <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
        <DialogContent className="sm:max-w-[560px]">
          <DialogHeader>
            <DialogTitle>Düzenle</DialogTitle>
            <DialogDescription>
              Bu ayar kaydedildiğinde ilgili backend akışları yeni değeri kullanır.
            </DialogDescription>
          </DialogHeader>

          <div className="grid gap-4 py-2 md:grid-cols-[160px_minmax(0,1fr)] md:items-start">
            <p className="text-sm font-medium text-slate-500">Grubu</p>
            <p className="text-sm font-semibold text-slate-900">{settingCategoryLabel(setting.category)}</p>

            <p className="text-sm font-medium text-slate-500">Tanımı</p>
            <p className="text-sm font-semibold text-slate-900">{setting.description || setting.key}</p>

            <p className="text-sm font-medium text-slate-500">Değeri</p>
            <div className="space-y-2">
              <RawSettingValueEditor
                dataType={setting.dataType}
                value={draftValue}
                onChange={setDraftValue}
              />

              {limit && (setting.dataType === 'int' || setting.dataType === 'decimal') && (
                <p className="text-xs text-slate-500">
                  Min: {limit.min ?? '—'} · Max: {limit.max ?? '—'}
                </p>
              )}

              {errorMessage && <p className="text-sm text-red-600">{errorMessage}</p>}
            </div>
          </div>

          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => setDialogOpen(false)}>İptal</Button>
            <Button type="button" onClick={handleSave} disabled={updateMutation.isPending}>
              {updateMutation.isPending ? 'Kaydediliyor...' : 'Kaydet'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}

function RawSettingValueEditor({
  dataType,
  value,
  onChange,
}: {
  dataType: string;
  value: string;
  onChange: (value: string) => void;
}) {
  if (dataType === 'bool') {
    return (
      <Select value={value} onValueChange={onChange}>
        <SelectTrigger className="h-11 rounded-md border-slate-300 bg-white text-[15px]">
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value="true">Açık</SelectItem>
          <SelectItem value="false">Kapalı</SelectItem>
        </SelectContent>
      </Select>
    );
  }

  if (dataType === 'time') {
    return (
      <Input
        type="time"
        className="h-11 rounded-md border-slate-300 bg-white text-[15px]"
        value={value}
        onChange={event => onChange(event.target.value)}
      />
    );
  }

  if (dataType === 'int' || dataType === 'decimal') {
    return (
      <Input
        type="number"
        step={dataType === 'decimal' ? '0.01' : '1'}
        className="h-11 rounded-md border-slate-300 bg-white text-[15px]"
        value={value}
        onChange={event => onChange(event.target.value)}
      />
    );
  }

  return (
    <Input
      className="h-11 rounded-md border-slate-300 bg-white text-[15px]"
      value={value}
      onChange={event => onChange(event.target.value)}
    />
  );
}

function getPreviewState(
  title: string,
  mode: string,
  module: WebModuleControlSummaryDto,
  isDirty: boolean,
): { label: string; message: string | null; isOpen: boolean } {
  if (!isDirty) {
    return {
      label: module.effectiveStateLabel,
      message: module.effectiveMessage,
      isOpen: module.isCurrentlyOpen,
    };
  }

  if (mode === 'open') {
    return {
      label: 'Açık',
      message: null,
      isOpen: true,
    };
  }

  if (mode === 'closed') {
    return {
      label: 'Kapalı',
      message: `${title} modülü kapatılmıştır.`,
      isOpen: false,
    };
  }

  return {
    label: 'Takvime Bağlı',
    message: selectedModePreviewMessage(mode, module),
    isOpen: false,
  };
}

function selectedModePreviewMessage(mode: string, module: WebModuleControlSummaryDto): string {
  if (mode === module.selectedMode && module.effectiveMessage) {
    return module.effectiveMessage;
  }

  const selectedOption = module.options.find(option => option.code === mode);
  return selectedOption?.description ?? 'İlgili takvim veya süre tanımı aktif olduğunda modül açılır.';
}
