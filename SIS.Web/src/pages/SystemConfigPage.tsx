import { useEffect, useMemo, useRef, useState, type ChangeEvent } from 'react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Skeleton } from '@/components/ui/skeleton';
import { Textarea } from '@/components/ui/textarea';
import { AlertCircle, CheckCircle2, Download, Pencil, Search, Upload } from 'lucide-react';
import ConfigHistoryDrawer from '@/components/system-config/ConfigHistoryDrawer';
import { exportSystemConfigsFile } from '../api/systemConfig.api';
import { useConfigLimits, useImportSystemConfigs, useSystemConfigs, useUpdateSystemConfig } from '../hooks/useSystemConfig';
import { useDebounce } from '../hooks/useDebounce';
import { downloadBlob } from '../lib/download';
import { getErrorMessage } from '../lib/errorMessage';
import { cn } from '../lib/utils';
import type { ConfigLimits, SystemConfigDto, SystemConfigValueOptionDto } from '../api/systemConfig.api';

type Notice = {
  type: 'success' | 'error';
  message: string;
};

type GroupOption = {
  key: string;
  label: string;
};

const DATE_CONFIG_KEYS = new Set<string>([
  'Instructor.ProfileOrgUnitUpdateDeadline',
]);

const URL_CONFIG_KEYS = new Set<string>([
  'Instructor.FaqUrl',
]);

const HIDDEN_CONFIG_PREFIXES = [
  'SoftConstraint.',
  'Schedule.SA.',
  'Schedule.MRV.',
  'Schedule.LNS.',
  'Schedule.Candidate.',
  'Schedule.OrTools.',
  'Suggestion.',
] as const;

const HIDDEN_CONFIG_KEYS = new Set<string>([
  'Schedule.Engine',
  'Scheduling.MaxBacktrackingDepth',
  'Classroom.AmphiThreshold',
  'Classroom.OccupancyTarget.Min',
  'Classroom.OccupancyTarget.Max',
  'Schedule.CurriculumConflictMode',
  'Schedule.AllowSameCourseSectionOverlap',
  'Schedule.TheoryLabSplitThreshold',
  'Schedule.SplittableBlockTypes',
  'Schedule.ClassroomTypeRequirementEnabled',
  'Schedule.AmphiThresholdEnabled',
  'Schedule.LunchBreak.IsHard',
  'Schedule.MaxConsecutiveHours',
  'Schedule.Lab.MaxBlockHours',
  'Schedule.DefaultInstructorMaxWeeklyHours',
  'Schedule.MaxDailyHoursPerInstructor',
]);

function shouldHideConfig(config: SystemConfigDto) {
  if (HIDDEN_CONFIG_KEYS.has(config.key))
    return true;

  return HIDDEN_CONFIG_PREFIXES.some(prefix => config.key.startsWith(prefix));
}

export default function SystemConfigPage() {
  const { data: configs, isLoading, isError } = useSystemConfigs();
  const { data: limits } = useConfigLimits();
  const importMutation = useImportSystemConfigs();
  const importInputRef = useRef<HTMLInputElement>(null);
  const [notice, setNotice] = useState<Notice | null>(null);
  const [isExporting, setIsExporting] = useState(false);
  const [searchText, setSearchText] = useState('');
  const [selectedGroup, setSelectedGroup] = useState('all');
  const debouncedSearch = useDebounce(searchText, 250);

  useEffect(() => {
    if (!notice) return;
    const timer = setTimeout(() => setNotice(null), notice.type === 'error' ? 8000 : 4500);
    return () => clearTimeout(timer);
  }, [notice]);

  async function handleExport() {
    setNotice(null);
    setIsExporting(true);

    try {
      const file = await exportSystemConfigsFile();
      downloadBlob(file, 'system-config.json');
    } catch (error) {
      setNotice({ type: 'error', message: getErrorMessage(error, 'Sistem ayarları dışa aktarılamadı.') });
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
        message: `${result.updatedCount} ayar güncellendi. ${result.processedCount} kayıt doğrulandı.`,
      });
    } catch (error) {
      setNotice({ type: 'error', message: getErrorMessage(error, 'Sistem ayarları içe aktarılamadı.') });
    } finally {
      input.value = '';
    }
  }

  const sortedConfigs = useMemo(
    () => [...(configs ?? [])]
      .filter(config => !shouldHideConfig(config))
      .sort((left, right) =>
        left.groupLabel.localeCompare(right.groupLabel, 'tr')
        || left.displayOrder - right.displayOrder
        || left.displayName.localeCompare(right.displayName, 'tr')),
    [configs],
  );

  const groups = useMemo<GroupOption[]>(() => {
    const map = new Map<string, string>();
    for (const config of sortedConfigs) {
      if (!map.has(config.groupKey))
        map.set(config.groupKey, config.groupLabel);
    }

    return [
      { key: 'all', label: 'Tümü' },
      ...Array.from(map.entries()).map(([key, label]) => ({ key, label })),
    ];
  }, [sortedConfigs]);

  const filteredConfigs = useMemo(() => {
    const query = debouncedSearch.trim().toLocaleLowerCase('tr-TR');

    return sortedConfigs.filter(config => {
      if (selectedGroup !== 'all' && config.groupKey !== selectedGroup)
        return false;

      if (!query)
        return true;

      return [
        config.groupLabel,
        config.displayName,
        config.description ?? '',
        config.key,
        config.value,
      ].some(field => field.toLocaleLowerCase('tr-TR').includes(query));
    });
  }, [debouncedSearch, selectedGroup, sortedConfigs]);

  if (isLoading)
    return <Skeleton className="h-[42rem] rounded-[28px]" />;

  if (isError) {
    return (
      <Alert variant="destructive">
        <AlertCircle className="h-4 w-4" />
        <AlertDescription>Sistem ayarları yüklenemedi.</AlertDescription>
      </Alert>
    );
  }
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

      <div className="overflow-hidden rounded-[28px] border border-slate-200 bg-white shadow-[0_18px_40px_rgba(15,23,42,0.05)]">
        <div className="border-b border-slate-200 px-6 py-5">
          <div className="grid gap-3 xl:grid-cols-[260px_minmax(0,1fr)_auto] xl:items-center">
            <div className="space-y-2">
              <p className="text-xs font-semibold uppercase tracking-[0.16em] text-slate-500">Genel Ayar Grubu</p>
              <Select value={selectedGroup} onValueChange={setSelectedGroup}>
                <SelectTrigger className="h-11 rounded-md border-slate-300 bg-white text-left text-[15px]">
                  <SelectValue placeholder="Seçiniz" />
                </SelectTrigger>
                <SelectContent>
                  {groups.map(group => (
                    <SelectItem key={group.key} value={group.key}>{group.label}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div className="space-y-2">
              <p className="text-xs font-semibold uppercase tracking-[0.16em] text-slate-500">Açıklama / Anahtar</p>
              <div className="relative">
                <Search className="pointer-events-none absolute left-4 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" />
                <Input
                  value={searchText}
                  onChange={event => setSearchText(event.target.value)}
                  placeholder="Ayar ara... (anahtar, değer veya açıklama)"
                  className="h-11 rounded-md border-slate-300 bg-white pl-11 text-[15px]"
                />
              </div>
            </div>

            <div className="flex items-end justify-end gap-2">
              <Button
                type="button"
                variant="outline"
                className="h-11 rounded-md border-slate-300 px-4 text-sm"
                onClick={handleExport}
                disabled={isExporting}
              >
                <Download className="mr-2 h-4 w-4" />
                {isExporting ? 'Hazırlanıyor...' : 'JSON İndir'}
              </Button>
              <Button
                type="button"
                variant="outline"
                className="h-11 rounded-md border-slate-300 px-4 text-sm"
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
            </div>
          </div>

          <p className="mt-4 text-sm leading-6 text-slate-500">
            Bu ekrandaki değişiklikler yalnızca gösterimi değil, bağlı backend akışlarını da etkiler.
            Kayıt kontrolleri, başvuru master anahtarları, ders programı parametreleri ve öğretim elemanı görünürlüğü
            canlı olarak bu tablodan okunur.
          </p>
        </div>

        {filteredConfigs.length === 0 ? (
          <div className="px-6 py-16 text-center text-sm text-slate-500">
            Seçilen filtreyle eşleşen sistem ayarı bulunamadı.
          </div>
        ) : (
          <div className="w-full">
            <table className="w-full table-fixed">
              <thead>
                <tr className="border-b border-slate-300 bg-slate-700 text-left text-sm font-semibold text-white">
                  <th className="w-[170px] px-4 py-3">Genel Ayar Grubu</th>
                  <th className="px-6 py-3">Tanımı</th>
                  <th className="w-[180px] px-4 py-3">Değeri</th>
                  <th className="w-[112px] px-4 py-3 text-right">İşlem</th>
                </tr>
              </thead>
              <tbody>
                {filteredConfigs.map((config, index) => (
                  <ConfigRow
                    key={config.id}
                    config={config}
                    limits={limits}
                    zebra={index % 2 === 1}
                    onNotice={setNotice}
                  />
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
}

function ConfigRow({
  config,
  limits,
  zebra,
  onNotice,
}: {
  config: SystemConfigDto;
  limits?: ConfigLimits;
  zebra: boolean;
  onNotice: (notice: Notice) => void;
}) {
  const updateMutation = useUpdateSystemConfig();
  const [dialogOpen, setDialogOpen] = useState(false);
  const [draftValue, setDraftValue] = useState(config.value);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  useEffect(() => {
    if (!dialogOpen) {
      setDraftValue(config.value);
      setErrorMessage(null);
    }
  }, [config.value, dialogOpen]);

  const selectedOption = config.options.find(option => option.value === config.value);
  const displayValue = getDisplayValue(config.value, config.options);
  const limit = limits?.[config.key];

  function handleClose() {
    setDialogOpen(false);
    setDraftValue(config.value);
    setErrorMessage(null);
  }

  function handleSave() {
    setErrorMessage(null);
    updateMutation.mutate(
      { id: config.id, value: draftValue },
      {
        onSuccess: updatedConfig => {
          onNotice({ type: 'success', message: `${updatedConfig.displayName} güncellendi.` });
          setDialogOpen(false);
        },
        onError: error => {
          setErrorMessage(getErrorMessage(error, 'Ayar kaydedilemedi.'));
        },
      },
    );
  }

  return (
    <>
      <tr className={cn('border-b border-slate-200 align-top', zebra ? 'bg-slate-50/70' : 'bg-white')}>
        <td className="px-4 py-4 text-sm font-medium text-slate-600">{config.groupLabel}</td>
        <td className="px-6 py-4">
          <div className="space-y-1.5">
            <p className="text-[15px] font-medium leading-6 text-slate-900">{config.displayName}</p>
            {config.description && (
              <p className="text-sm leading-6 text-slate-500">{config.description}</p>
            )}
          </div>
        </td>
        <td className="px-4 py-4">
          {config.dataType === 'bool' ? (
            <Badge
              variant={config.value === 'true' ? 'default' : 'secondary'}
              className={cn(
                'rounded-md px-2.5 py-1 text-xs font-semibold',
                config.value === 'true'
                  ? 'bg-emerald-100 text-emerald-700 hover:bg-emerald-100'
                  : 'bg-slate-100 text-slate-700 hover:bg-slate-100',
              )}
            >
              {displayValue}
            </Badge>
          ) : (
            <span className="block break-all text-[15px] font-medium leading-6 text-slate-800">
              {displayValue}
            </span>
          )}
        </td>
        <td className="px-4 py-4">
          <div className="flex shrink-0 justify-end gap-1">
            <ConfigHistoryDrawer configId={config.id} configKey={config.key} currentValue={config.value} />
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
        </td>
      </tr>

      <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
        <DialogContent className="sm:max-w-[720px]">
          <DialogHeader>
            <DialogTitle>Düzenle</DialogTitle>
            <DialogDescription>
              Bu ayar kaydedildiğinde ilgili backend kuralları ve bağlı frontend ekranları yeni değeri kullanır.
            </DialogDescription>
          </DialogHeader>

          <div className="grid gap-4 py-2 md:grid-cols-[180px_minmax(0,1fr)] md:items-start">
            <p className="text-sm font-medium text-slate-500">Grubu</p>
            <p className="text-sm font-semibold text-slate-900">{config.groupLabel}</p>

            <p className="text-sm font-medium text-slate-500">Tanımı</p>
            <p className="text-sm font-semibold text-slate-900">{config.displayName}</p>

            <p className="text-sm font-medium text-slate-500">Açıklaması</p>
            <p className="text-sm leading-6 text-slate-600">{config.description || '—'}</p>

            <p className="text-sm font-medium text-slate-500">Değeri</p>
            <div className="space-y-2">
              <ConfigValueEditor
                config={config}
                value={draftValue}
                onChange={setDraftValue}
              />

              {selectedOption && config.dataType !== 'bool' && (
                <p className="text-xs text-slate-500">
                  Mevcut seçenek: <span className="font-medium text-slate-700">{selectedOption.label}</span>
                </p>
              )}

              {limit && (
                <p className="text-xs text-slate-500">
                  Min: {limit.min ?? '—'} · Max: {limit.max ?? '—'}
                </p>
              )}

              {config.dataType === 'decimal' && (
                <p className="text-xs text-amber-600">Ondalık ayracı olarak nokta (.) kullanın.</p>
              )}

              {errorMessage && (
                <p className="text-sm text-red-600">{errorMessage}</p>
              )}
            </div>
          </div>

          <DialogFooter>
            <Button type="button" variant="outline" onClick={handleClose}>İptal</Button>
            <Button type="button" onClick={handleSave} disabled={updateMutation.isPending}>
              {updateMutation.isPending ? 'Kaydediliyor...' : 'Kaydet'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  );
}

function ConfigValueEditor({
  config,
  value,
  onChange,
}: {
  config: SystemConfigDto;
  value: string;
  onChange: (value: string) => void;
}) {
  if (config.options.length > 0) {
    return (
      <Select value={value} onValueChange={onChange}>
        <SelectTrigger className="h-11 rounded-md border-slate-300 bg-white text-[15px]">
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          {config.options.map(option => (
            <SelectItem key={option.value} value={option.value}>{option.label}</SelectItem>
          ))}
        </SelectContent>
      </Select>
    );
  }

  if (config.dataType === 'json') {
    return (
      <Textarea
        className="min-h-[120px] font-mono text-xs"
        value={value}
        onChange={event => onChange(event.target.value)}
      />
    );
  }

  if (config.dataType === 'int' || config.dataType === 'decimal') {
    return (
      <Input
        type="number"
        step={config.dataType === 'decimal' ? '0.01' : '1'}
        className="h-11 rounded-md border-slate-300 bg-white text-[15px]"
        value={value}
        onChange={event => onChange(event.target.value)}
      />
    );
  }

  if (DATE_CONFIG_KEYS.has(config.key)) {
    return (
      <Input
        type="date"
        className="h-11 rounded-md border-slate-300 bg-white text-[15px]"
        value={value}
        onChange={event => onChange(event.target.value)}
      />
    );
  }

  if (URL_CONFIG_KEYS.has(config.key)) {
    return (
      <Input
        type="url"
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

function getDisplayValue(value: string, options: SystemConfigValueOptionDto[]) {
  return options.find(option => option.value === value)?.label ?? value;
}
