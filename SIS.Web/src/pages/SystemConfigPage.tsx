import { useRef, useState, type ChangeEvent } from 'react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table';
import {
  Tooltip, TooltipContent, TooltipTrigger,
} from '@/components/ui/tooltip';
import { AlertCircle, Check, Download, HelpCircle, Pencil, Upload, X } from 'lucide-react';
import ConfigHistoryDrawer from '@/components/system-config/ConfigHistoryDrawer';
import { exportSystemConfigsFile } from '../api/systemConfig.api';
import { useImportSystemConfigs, useSystemConfigs, useUpdateSystemConfig } from '../hooks/useSystemConfig';
import { downloadBlob } from '../lib/download';
import type { SystemConfigDto } from '../api/systemConfig.api';

const CATEGORY_LABELS: Record<string, string> = {
  Registration: 'Kayıt Kuralları',
  GPA: 'Not Hesaplama',
  Scheduling: 'Zamanlama',
  Schedule: 'Ders Programı',
  General: 'Genel',
};

type Notice = {
  type: 'success' | 'error';
  message: string;
};

function getErrorMessage(error: unknown, fallbackMessage: string) {
  if (
    typeof error === 'object'
    && error !== null
    && 'response' in error
    && typeof (error as { response?: { data?: { error?: unknown } } }).response?.data?.error === 'string'
  ) {
    return (error as { response: { data: { error: string } } }).response.data.error;
  }

  if (error instanceof Error && error.message) {
    return error.message;
  }

  return fallbackMessage;
}

export default function SystemConfigPage() {
  const { data: configs, isLoading, isError } = useSystemConfigs();
  const importMutation = useImportSystemConfigs();
  const importInputRef = useRef<HTMLInputElement>(null);
  const [notice, setNotice] = useState<Notice | null>(null);
  const [isExporting, setIsExporting] = useState(false);

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
    if (!file) {
      return;
    }

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

  const grouped = (configs ?? []).reduce<Record<string, SystemConfigDto[]>>((acc, config) => {
    const category = config.category || 'General';
    (acc[category] ??= []).push(config);
    return acc;
  }, {});

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h1 className="text-xl font-bold">Sistem Ayarları</h1>
          <p className="text-sm text-muted-foreground">Config değerlerini doğrulamalı olarak güncelleyin, dışa aktarın veya geri alın.</p>
        </div>
        <div className="flex flex-wrap gap-2">
          <Button variant="outline" size="sm" onClick={handleExport} disabled={isExporting}>
            <Download className="mr-1.5 h-3.5 w-3.5" />
            {isExporting ? 'Hazırlanıyor...' : 'JSON İndir'}
          </Button>
          <Button variant="outline" size="sm" onClick={() => importInputRef.current?.click()} disabled={importMutation.isPending}>
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

      {notice && (
        <Alert variant={notice.type === 'error' ? 'destructive' : undefined}>
          {notice.type === 'error' && <AlertCircle className="h-4 w-4" />}
          <AlertDescription>{notice.message}</AlertDescription>
        </Alert>
      )}

      {Object.entries(grouped).map(([category, items]) => (
        <Card key={category}>
          <CardHeader className="pb-3">
            <CardTitle className="text-sm">
              <Badge variant="outline" className="mr-2">{CATEGORY_LABELS[category] || category}</Badge>
              {items.length} ayar
            </CardTitle>
          </CardHeader>
          <CardContent>
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Anahtar</TableHead>
                  <TableHead>Değer</TableHead>
                  <TableHead className="w-20">Tip</TableHead>
                  <TableHead className="w-32 text-right">İşlem</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {items.map(config => (
                  <ConfigRow key={config.id} config={config} />
                ))}
              </TableBody>
            </Table>
          </CardContent>
        </Card>
      ))}
    </div>
  );
}

function ConfigRow({ config }: { config: SystemConfigDto }) {
  const [editing, setEditing] = useState(false);
  const [value, setValue] = useState(config.value);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const updateMutation = useUpdateSystemConfig();

  const handleSave = () => {
    setErrorMessage(null);
    updateMutation.mutate(
      { id: config.id, value },
      {
        onSuccess: updatedConfig => {
          setValue(updatedConfig.value);
          setEditing(false);
        },
        onError: error => {
          setErrorMessage(getErrorMessage(error, 'Ayar kaydedilemedi.'));
        },
      },
    );
  };

  const handleCancel = () => {
    setValue(config.value);
    setErrorMessage(null);
    setEditing(false);
  };

  return (
    <TableRow>
      <TableCell>
        <div className="flex items-center gap-1.5">
          <span className="font-mono text-xs font-medium">{config.key}</span>
          {config.description && (
            <Tooltip>
              <TooltipTrigger asChild>
                <HelpCircle className="h-3.5 w-3.5 cursor-help text-muted-foreground" />
              </TooltipTrigger>
              <TooltipContent side="right" className="max-w-xs">
                <p className="text-xs">{config.description}</p>
              </TooltipContent>
            </Tooltip>
          )}
        </div>
      </TableCell>
      <TableCell>
        {editing ? (
          <div className="space-y-1">
            <Input
              className="h-8 w-56 text-sm"
              value={value}
              onChange={event => setValue(event.target.value)}
              onKeyDown={event => {
                if (event.key === 'Enter') handleSave();
                if (event.key === 'Escape') handleCancel();
              }}
              autoFocus
            />
            {config.dataType === 'decimal' && (
              <p className="mt-0.5 text-[10px] text-amber-600">Ondalık ayracı olarak nokta (.) kullanın</p>
            )}
            {errorMessage && (
              <p className="max-w-md text-[11px] text-red-600">{errorMessage}</p>
            )}
          </div>
        ) : (
          <span className="text-sm font-medium">{config.value}</span>
        )}
      </TableCell>
      <TableCell>
        <Badge variant="secondary" className="text-[10px]">{config.dataType}</Badge>
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
  );
}
