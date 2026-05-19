import { useEffect, useMemo, useState } from 'react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table';
import { AlertCircle, Pencil } from 'lucide-react';
import type {
  EnumOptionDto,
  SchedulingAlgorithmConfigDto,
  SchedulingAlgorithmConfigEnumOptions,
  SchedulingAlgorithmConfigGroupDto,
  SchedulingAlgorithmConfigLimits,
} from '../../api/schedulingAlgorithmConfig.api';
import {
  useSchedulingAlgorithmConfigEnumOptions,
  useSchedulingAlgorithmConfigGroups,
  useSchedulingAlgorithmConfigLimits,
  useSchedulingAlgorithmConfigs,
  useUpdateSchedulingAlgorithmConfig,
} from '../../hooks/useSchedulingAlgorithmConfig';
import { getErrorMessage } from '../../lib/errorMessage';
import { useToast } from '@/components/ui/global-toast';

const FALLBACK_BADGE_CLASS = 'bg-slate-100 text-slate-800 border-slate-200';
const FALLBACK_GROUP_TITLE = 'Diğer Algoritma Ayarları';

export default function AlgorithmParametersPanel() {
  const { data: configs, isLoading: configsLoading, isError: configsError } = useSchedulingAlgorithmConfigs();
  const { data: groups, isLoading: groupsLoading, isError: groupsError } = useSchedulingAlgorithmConfigGroups();
  const { data: limits } = useSchedulingAlgorithmConfigLimits();
  const { data: enumOptions } = useSchedulingAlgorithmConfigEnumOptions();

  const groupsByCategory = useMemo(() => {
    const map = new Map<string, SchedulingAlgorithmConfigGroupDto>();
    for (const g of groups ?? []) map.set(g.category, g);
    return map;
  }, [groups]);

  // Configs DB'deki category alanını kullanıyor; backend'in döndürdüğü grup metadatasıyla eşleşiyor.
  // Sıralama sortOrder'a göre — backend'in tek sözü. Tanımsız kategoriler en sona düşer.
  const flatRows = useMemo(() => {
    if (!configs) return [];
    return [...configs]
      .map(config => {
        const group = groupsByCategory.get(config.category);
        return {
          config,
          groupTitle: group?.displayName ?? FALLBACK_GROUP_TITLE,
          badgeClass: group?.badgeClass ?? FALLBACK_BADGE_CLASS,
          sortOrder: group?.sortOrder ?? Number.MAX_SAFE_INTEGER,
        };
      })
      .sort((a, b) => {
        if (a.sortOrder !== b.sortOrder) return a.sortOrder - b.sortOrder;
        return a.config.key.localeCompare(b.config.key);
      });
  }, [configs, groupsByCategory]);

  if (configsLoading || groupsLoading) return <Skeleton className="h-96 rounded-xl" />;
  if (configsError || groupsError) {
    return (
      <Alert variant="destructive">
        <AlertCircle className="h-4 w-4" />
        <AlertDescription>Sistem ayarları yüklenemedi.</AlertDescription>
      </Alert>
    );
  }

  if (flatRows.length === 0) return null;

  // 2026-05-19: Ana Kısıt tablosuyla aynı sütun yapısı (Grup / Parametre / Durum yok /
  // Kategori yok / Değer / Düzenle). 6 sütunlu Kısıt tablosuna görsel olarak yaklaşıyor.
  return (
    <Card>
      <CardContent className="p-0">
        <div className="overflow-x-auto">
          <Table className="min-w-[900px]">
            <TableHeader>
              <TableRow>
                <TableHead className="w-44 text-sm">Grup</TableHead>
                <TableHead className="text-sm">Parametre</TableHead>
                <TableHead className="w-28 text-sm" />
                <TableHead className="w-32 text-sm" />
                <TableHead className="w-28 text-sm">Değer</TableHead>
                <TableHead className="w-24 text-right text-sm">Düzenle</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {flatRows.map(({ config, groupTitle, badgeClass }) => (
                <ParameterRow
                  key={config.id}
                  config={config}
                  groupTitle={groupTitle}
                  badgeClass={badgeClass}
                  limits={limits}
                  enumOptions={enumOptions}
                />
              ))}
            </TableBody>
          </Table>
        </div>
      </CardContent>
    </Card>
  );
}


interface ParameterRowProps {
  config: SchedulingAlgorithmConfigDto;
  groupTitle: string;
  badgeClass: string;
  limits?: SchedulingAlgorithmConfigLimits;
  enumOptions?: SchedulingAlgorithmConfigEnumOptions;
}

function ParameterRow({ config, groupTitle, badgeClass, limits, enumOptions }: ParameterRowProps) {
  const [dialogOpen, setDialogOpen] = useState(false);

  const isBool = config.dataType === 'bool';
  const enumChoices = enumOptions?.[config.key];

  function renderDisplayValue() {
    if (isBool) {
      const checked = config.value === 'true' || config.value === '1';
      const cls = checked
        ? 'bg-green-600 text-white border-green-600 dark:bg-green-700 dark:border-green-700'
        : 'bg-gray-400 text-white border-gray-400 dark:bg-gray-600 dark:border-gray-600';
      return (
        <Badge variant="outline" className={`text-xs ${cls}`}>
          {checked ? 'Evet' : 'Hayır'}
        </Badge>
      );
    }
    if (enumChoices) {
      // Etiketi göster, ham değeri değil (örn. "Cohort (önerilen)" değil "cohort").
      const match = enumChoices.find(opt => opt.value.toLowerCase() === config.value.toLowerCase());
      return <span className="text-base font-medium">{match?.label ?? config.value}</span>;
    }
    return <span className="text-base font-medium">{config.value}</span>;
  }

  // Kısıt tablosuyla aynı 6 sütunlu yapı: Grup | Parametre | (Durum) | (Kategori) | Değer | Düzenle.
  // Algoritma ayarları için Durum/Kategori anlamsız; placeholder dash gösterilir, görsel hizalama korunur.
  return (
    <TableRow>
      <TableCell className="w-44">
        <Badge variant="outline" className={`text-xs ${badgeClass}`}>{groupTitle}</Badge>
      </TableCell>
      <TableCell>
        <p className="text-base leading-snug">{config.description || config.key}</p>
      </TableCell>
      <TableCell className="w-28 text-center text-muted-foreground">—</TableCell>
      <TableCell className="w-32 text-center text-muted-foreground">—</TableCell>
      <TableCell className="w-28">{renderDisplayValue()}</TableCell>
      <TableCell className="w-24">
        <div className="flex justify-end">
          <Button
            size="icon"
            variant="ghost"
            className="h-8 w-8"
            onClick={() => setDialogOpen(true)}
            aria-label="Düzenle"
          >
            <Pencil className="h-4 w-4" />
          </Button>
        </div>
        <ParameterEditDialog
          open={dialogOpen}
          onOpenChange={setDialogOpen}
          config={config}
          limits={limits}
          enumChoices={enumChoices}
        />
      </TableCell>
    </TableRow>
  );
}

// HH:mm formatı bekleyen time key'leri (algoritma sekmesinde sadece bu ikisi).
const TIME_VALUE_KEYS = new Set<string>([
  'Schedule.LunchBreakStart',
  'Schedule.LunchBreakEnd',
]);

interface ParameterEditDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  config: SchedulingAlgorithmConfigDto;
  limits?: SchedulingAlgorithmConfigLimits;
  enumChoices?: EnumOptionDto[];
}

function ParameterEditDialog({ open, onOpenChange, config, limits, enumChoices }: ParameterEditDialogProps) {
  const [draft, setDraft] = useState(config.value);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const updateMutation = useUpdateSchedulingAlgorithmConfig();
  const toast = useToast();

  useEffect(() => {
    if (open) {
      // eslint-disable-next-line react-hooks/set-state-in-effect -- resync from props on open
      setDraft(config.value);
      setErrorMessage(null);
    }
  }, [open, config.value]);

  const isBool = config.dataType === 'bool';
  const isEnum = !!enumChoices && enumChoices.length > 0;
  const isNumeric = config.dataType === 'int' || config.dataType === 'decimal';
  const isTime = TIME_VALUE_KEYS.has(config.key);
  const limit = limits?.[config.key];

  function handleSave() {
    if (draft === config.value) {
      onOpenChange(false);
      return;
    }
    setErrorMessage(null);
    updateMutation.mutate(
      { id: config.id, value: draft },
      {
        onSuccess: () => {
          toast.showSuccess('Ayar uygulandı', `${config.description ?? config.key} güncellendi.`);
          onOpenChange(false);
        },
        onError: (error) => {
          const msg = getErrorMessage(error, 'Ayar kaydedilemedi.');
          setErrorMessage(msg);
          toast.showError('Ayar kaydedilemedi', msg);
        },
      },
    );
  }

  function renderInput() {
    if (isBool) {
      const checked = draft === 'true' || draft === '1' || draft === 'yes' || draft === 'on';
      return (
        <div className="grid grid-cols-2 gap-2">
          <Button
            type="button"
            variant={checked ? 'default' : 'outline'}
            className={checked ? '' : 'opacity-70'}
            onClick={() => setDraft('true')}
          >
            Evet
          </Button>
          <Button
            type="button"
            variant={!checked ? 'default' : 'outline'}
            className={!checked ? '' : 'opacity-70'}
            onClick={() => setDraft('false')}
          >
            Hayır
          </Button>
        </div>
      );
    }
    if (isEnum && enumChoices) {
      const selected = enumChoices.find(opt => opt.value.toLowerCase() === draft.toLowerCase());
      return (
        <div className="space-y-2">
          <div className="space-y-1.5">
            {enumChoices.map(opt => {
              const isSelected = opt.value === selected?.value;
              return (
                <button
                  key={opt.value}
                  type="button"
                  onClick={() => !opt.disabled && setDraft(opt.value)}
                  disabled={opt.disabled}
                  className={
                    'w-full rounded-md border px-3 py-2 text-left text-sm transition-colors ' +
                    (opt.disabled
                      ? 'cursor-not-allowed opacity-50 border-gray-200 bg-gray-50 '
                      : isSelected
                        ? 'border-blue-500 bg-blue-50 dark:border-blue-400 dark:bg-blue-950/30 '
                        : 'border-gray-200 hover:border-gray-400 hover:bg-gray-50 dark:border-gray-700 dark:hover:bg-gray-900 ')
                  }
                >
                  <div className="font-medium">{opt.label}</div>
                  {opt.helpText && (
                    <div className="mt-0.5 text-[11px] text-muted-foreground leading-snug">{opt.helpText}</div>
                  )}
                </button>
              );
            })}
          </div>
        </div>
      );
    }
    if (isTime) {
      return (
        <Input
          type="time"
          value={draft.length >= 5 ? draft.substring(0, 5) : draft}
          onChange={(e) => setDraft(e.target.value)}
          autoFocus
        />
      );
    }
    if (isNumeric) {
      return (
        <Input
          type="number"
          value={draft}
          step={config.dataType === 'decimal' ? '0.01' : '1'}
          min={limit?.min ?? undefined}
          max={limit?.max ?? undefined}
          onChange={(e) => setDraft(e.target.value)}
          autoFocus
        />
      );
    }
    return (
      <Input
        value={draft}
        onChange={(e) => setDraft(e.target.value)}
        autoFocus
      />
    );
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle>Ayarı Düzenle</DialogTitle>
          {config.description && (
            <DialogDescription>{config.description}</DialogDescription>
          )}
        </DialogHeader>

        <div className="space-y-3 py-2">
          <div className="rounded-md bg-muted/50 px-2.5 py-1.5">
            <p className="text-xs text-muted-foreground">
              Mevcut değer: <strong className="text-foreground">{config.value}</strong>
            </p>
          </div>

          <div className="space-y-2">
            <label className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
              Yeni Değer
            </label>
            {renderInput()}
            {isNumeric && limit && (
              <p className="text-[11px] text-muted-foreground">
                İzin verilen aralık: {limit.min ?? '—'} – {limit.max ?? '—'}
              </p>
            )}
          </div>

          {errorMessage && (
            <p className="text-xs text-red-600">{errorMessage}</p>
          )}
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>İptal</Button>
          <Button onClick={handleSave} disabled={updateMutation.isPending}>
            {updateMutation.isPending ? 'Kaydediliyor…' : 'Kaydet'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
