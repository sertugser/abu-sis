import { useEffect, useRef, useState, type ChangeEvent } from 'react';
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
import { Textarea } from '@/components/ui/textarea';
import { AlertCircle, Check, CheckCircle2, Download, Pencil, Search, Upload, X } from 'lucide-react';
import ConfigHistoryDrawer from '@/components/system-config/ConfigHistoryDrawer';
import { exportSystemConfigsFile } from '../api/systemConfig.api';
import { useConfigLimits, useImportSystemConfigs, useSystemConfigs, useUpdateSystemConfig } from '../hooks/useSystemConfig';
import { useDebounce } from '../hooks/useDebounce';
import { downloadBlob } from '../lib/download';
import { getErrorMessage } from '../lib/errorMessage';
import type { SystemConfigDto, ConfigLimits } from '../api/systemConfig.api';

const CATEGORY_LABELS: Record<string, string> = {
  Registration: 'Kayıt Kuralları',
  GPA: 'Not Hesaplama',
  Scheduling: 'Zamanlama',
  Schedule: 'Ders Programı',
  Attendance: 'Devamsızlık',
  Security: 'Güvenlik',
  Localization: 'Dil ve Yerelleştirme',
  General: 'Genel',
};

type Notice = {
  type: 'success' | 'error';
  message: string;
};

export default function SystemConfigPage() {
  const { data: configs, isLoading, isError } = useSystemConfigs();
  const { data: limits } = useConfigLimits();
  const importMutation = useImportSystemConfigs();
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
      if (!Array.isArray(parsed)) {
        throw new Error('İçe aktarım dosyası bir JSON dizi formatında olmalıdır.');
      }

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

  if (isLoading) return <Skeleton className="h-96 rounded-xl" />;
  if (isError) {
    return (
      <Alert variant="destructive">
        <AlertCircle className="h-4 w-4" />
        <AlertDescription>Sistem ayarları yüklenemedi.</AlertDescription>
      </Alert>
    );
  }

  const searchLower = debouncedSearch.toLowerCase();
  const filtered = (configs ?? []).filter(c => {
    if (!searchLower) return true;
    return (
      c.key.toLowerCase().includes(searchLower)
      || c.value.toLowerCase().includes(searchLower)
      || (c.description ?? '').toLowerCase().includes(searchLower)
      || (CATEGORY_LABELS[c.category] ?? c.category).toLowerCase().includes(searchLower)
    );
  });

  const grouped = filtered.reduce<Record<string, SystemConfigDto[]>>((acc, config) => {
    const category = config.category || 'General';
    (acc[category] ??= []).push(config);
    return acc;
  }, {});

  const sortedCategories = Object.keys(grouped).sort((a, b) => {
    const labelA = CATEGORY_LABELS[a] ?? a;
    const labelB = CATEGORY_LABELS[b] ?? b;
    return labelA.localeCompare(labelB, 'tr');
  });

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
        <div className="relative w-72">
          <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
          <Input
            placeholder="Ayar ara... (anahtar, değer veya açıklama)"
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

      {sortedCategories.length === 0 && debouncedSearch && (
        <div className="rounded-lg border border-dashed p-6 text-center text-sm text-muted-foreground">
          &ldquo;{debouncedSearch}&rdquo; ile eşleşen ayar bulunamadı.
        </div>
      )}

      {sortedCategories.map(category => (
        <Card key={category}>
          <CardHeader className="pb-3">
            <CardTitle className="text-sm">
              <Badge variant="outline" className="mr-2">{CATEGORY_LABELS[category] || category}</Badge>
              {grouped[category].length} ayar
            </CardTitle>
          </CardHeader>
          <CardContent>
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Ayar</TableHead>
                  <TableHead>Değer</TableHead>
                  <TableHead className="w-32 text-right">İşlem</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {grouped[category].map(config => (
                  <ConfigRow key={config.id} config={config} limits={limits} onNotice={setNotice} />
                ))}
              </TableBody>
            </Table>
          </CardContent>
        </Card>
      ))}
    </div>
  );
}

function ConfigRow({
  config,
  limits,
  onNotice,
}: {
  config: SystemConfigDto;
  limits?: ConfigLimits;
  onNotice: (n: Notice) => void;
}) {
  const [editing, setEditing] = useState(false);
  const [value, setValue] = useState(config.value);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [confirmOpen, setConfirmOpen] = useState(false);
  const updateMutation = useUpdateSystemConfig();

  useEffect(() => {
    if (!editing) {
      // eslint-disable-next-line react-hooks/set-state-in-effect -- resyncing input with props when exiting edit mode; intentional
      setValue(config.value);
    }
  }, [config.value, editing]);

  const limit = limits?.[config.key];
  const limitHint = limit
    ? `Min: ${limit.min ?? '—'}, Max: ${limit.max ?? '—'}`
    : null;

  function doSave() {
    setErrorMessage(null);
    updateMutation.mutate(
      { id: config.id, value },
      {
        onSuccess: updatedConfig => {
          setValue(updatedConfig.value);
          setEditing(false);
          onNotice({ type: 'success', message: `${config.key} güncellendi.` });
        },
        onError: error => {
          setErrorMessage(getErrorMessage(error, 'Ayar kaydedilemedi.'));
        },
      },
    );
  }

  function handleSave() {
    if (value === config.value) {
      setEditing(false);
      return;
    }
    setConfirmOpen(true);
  }

  function handleCancel() {
    setValue(config.value);
    setErrorMessage(null);
    setEditing(false);
  }

  function renderEditInput() {
    if (config.dataType === 'bool') {
      const checked = value === 'true' || value === '1' || value === 'yes' || value === 'on';
      return (
        <div className="flex items-center gap-2">
          <Switch
            checked={checked}
            onCheckedChange={c => setValue(c ? 'true' : 'false')}
          />
          <span className="text-xs text-muted-foreground">{checked ? 'Aktif' : 'Pasif'}</span>
        </div>
      );
    }

    if (config.dataType === 'json') {
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

    if (config.dataType === 'int' || config.dataType === 'decimal') {
      return (
        <div className="space-y-1">
          <Input
            type="number"
            className="h-8 w-56 text-sm"
            value={value}
            step={config.dataType === 'decimal' ? '0.01' : '1'}
            onChange={e => setValue(e.target.value)}
            onKeyDown={e => {
              if (e.key === 'Enter') handleSave();
              if (e.key === 'Escape') handleCancel();
            }}
            autoFocus
          />
          {limitHint && (
            <p className="text-[10px] text-muted-foreground">{limitHint}</p>
          )}
          {config.dataType === 'decimal' && (
            <p className="text-[10px] text-amber-600">Ondalık ayracı olarak nokta (.) kullanın</p>
          )}
          {errorMessage && (
            <p className="max-w-md text-[11px] text-red-600">{errorMessage}</p>
          )}
        </div>
      );
    }

    return (
      <div className="space-y-1">
        <Input
          className="h-8 w-56 text-sm"
          value={value}
          onChange={e => setValue(e.target.value)}
          onKeyDown={e => {
            if (e.key === 'Enter') handleSave();
            if (e.key === 'Escape') handleCancel();
          }}
          autoFocus
        />
        {errorMessage && (
          <p className="max-w-md text-[11px] text-red-600">{errorMessage}</p>
        )}
      </div>
    );
  }

  function renderDisplayValue() {
    if (config.dataType === 'bool') {
      const checked = config.value === 'true' || config.value === '1';
      return (
        <Badge variant={checked ? 'default' : 'secondary'} className="text-[10px]">
          {checked ? 'Aktif' : 'Pasif'}
        </Badge>
      );
    }

    return <span className="text-sm font-medium">{config.value}</span>;
  }

  return (
    <>
      <TableRow>
        <TableCell>
          <p className="text-sm font-medium leading-tight">{config.description || config.key}</p>
        </TableCell>
        <TableCell>
          {editing ? renderEditInput() : renderDisplayValue()}
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
              <ConfigHistoryDrawer configId={config.id} configKey={config.key} currentValue={config.value} />
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
                <span className="font-mono text-xs">{config.key}</span> değerini{' '}
                <span className="font-semibold">{config.value}</span> &rarr;{' '}
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
