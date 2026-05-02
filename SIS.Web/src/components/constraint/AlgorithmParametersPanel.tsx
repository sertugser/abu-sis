import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import { Switch } from '@/components/ui/switch';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table';
import { AlertCircle, Check, Clock, Pencil, X } from 'lucide-react';
import type { SystemConfigDto, ConfigLimits } from '../../api/systemConfig.api';
import { useConfigLimits, useSystemConfigs, useUpdateSystemConfig } from '../../hooks/useSystemConfig';
import { getErrorMessage } from '../../lib/errorMessage';
import { ALGORITHM_GROUPS } from './algorithmParameters.constants';

type Notice = { type: 'success' | 'error'; message: string };

export default function AlgorithmParametersPanel() {
  const { data: configs, isLoading, isError } = useSystemConfigs();
  const { data: limits } = useConfigLimits();
  const [notice, setNotice] = useState<Notice | null>(null);

  useEffect(() => {
    if (!notice) return;
    const t = setTimeout(() => setNotice(null), notice.type === 'error' ? 8000 : 4000);
    return () => clearTimeout(t);
  }, [notice]);

  if (isLoading) return <Skeleton className="h-96 rounded-xl" />;
  if (isError) {
    return (
      <Alert variant="destructive">
        <AlertCircle className="h-4 w-4" />
        <AlertDescription>Sistem ayarları yüklenemedi.</AlertDescription>
      </Alert>
    );
  }

  const byKey = new Map((configs ?? []).map(c => [c.key, c]));

  return (
    <div className="space-y-4">
      {notice && (
        <Alert variant={notice.type === 'error' ? 'destructive' : undefined} className="py-2">
          {notice.type === 'error' ? <AlertCircle className="h-4 w-4" /> : <Check className="h-4 w-4" />}
          <AlertDescription>{notice.message}</AlertDescription>
        </Alert>
      )}

      <Alert className="py-2">
        <Clock className="h-4 w-4" />
        <AlertDescription className="text-xs">
          Saat slotlarının yüz-yüze / online davranışı (H17 kısıtını etkiler){' '}
          <Link to="/ders-saatleri-yonetimi" className="font-medium underline underline-offset-2">
            Ders Saatleri Yönetimi
          </Link>
          {' '}sayfasından düzenleniyor.
        </AlertDescription>
      </Alert>

      {ALGORITHM_GROUPS.map(group => {
        const items = group.keys
          .map(k => byKey.get(k))
          .filter((c): c is SystemConfigDto => c !== undefined);

        if (items.length === 0) return null;

        return (
          <Card key={group.title}>
            <CardHeader className="pb-3">
              <CardTitle className="flex items-center gap-2 text-sm">
                <Badge variant="outline">{group.title}</Badge>
                <span className="text-xs font-normal text-muted-foreground">
                  {items.length} ayar
                </span>
              </CardTitle>
              <p className="text-xs text-muted-foreground">{group.description}</p>
            </CardHeader>
            <CardContent>
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Parametre</TableHead>
                    <TableHead className="w-56">Değer</TableHead>
                    <TableHead className="w-20">Tip</TableHead>
                    <TableHead className="w-20 text-right">İşlem</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {items.map(config => (
                    <ParameterRow
                      key={config.id}
                      config={config}
                      limits={limits}
                      onNotice={setNotice}
                    />
                  ))}
                </TableBody>
              </Table>
            </CardContent>
          </Card>
        );
      })}
    </div>
  );
}

interface ParameterRowProps {
  config: SystemConfigDto;
  limits?: ConfigLimits;
  onNotice: (n: Notice) => void;
}

function ParameterRow({ config, limits, onNotice }: ParameterRowProps) {
  const [editing, setEditing] = useState(false);
  const [value, setValue] = useState(config.value);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
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

  function handleSave() {
    if (value === config.value) {
      setEditing(false);
      return;
    }
    setErrorMessage(null);
    updateMutation.mutate(
      { id: config.id, value },
      {
        onSuccess: updated => {
          setValue(updated.value);
          setEditing(false);
          onNotice({ type: 'success', message: `${config.key} güncellendi.` });
        },
        onError: error => {
          setErrorMessage(getErrorMessage(error, 'Ayar kaydedilemedi.'));
        },
      },
    );
  }

  function handleCancel() {
    setValue(config.value);
    setErrorMessage(null);
    setEditing(false);
  }

  const isNumeric = config.dataType === 'int' || config.dataType === 'decimal';
  const isBool = config.dataType === 'bool';

  function renderEditValue() {
    if (isBool) {
      const checked = value === 'true' || value === '1' || value === 'yes' || value === 'on';
      return (
        <div className="flex items-center gap-2">
          <Switch checked={checked} onCheckedChange={c => setValue(c ? 'true' : 'false')} />
          <span className="text-xs text-muted-foreground">{checked ? 'Aktif' : 'Pasif'}</span>
        </div>
      );
    }
    return (
      <Input
        type={isNumeric ? 'number' : 'text'}
        className="h-8 w-48 text-sm"
        value={value}
        step={config.dataType === 'decimal' ? '0.01' : '1'}
        onChange={e => setValue(e.target.value)}
        onKeyDown={e => {
          if (e.key === 'Enter') handleSave();
          if (e.key === 'Escape') handleCancel();
        }}
        autoFocus
      />
    );
  }

  function renderDisplayValue() {
    if (isBool) {
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
    <TableRow>
      <TableCell>
        <div className="space-y-0.5">
          <p className="text-sm font-medium leading-tight">{config.description || config.key}</p>
          <p className="font-mono text-[11px] text-muted-foreground">{config.key}</p>
        </div>
      </TableCell>
      <TableCell>
        {editing ? (
          <div className="space-y-1">
            {renderEditValue()}
            {limitHint && !isBool && <p className="text-[10px] text-muted-foreground">{limitHint}</p>}
            {errorMessage && (
              <p className="max-w-md text-[11px] text-red-600">{errorMessage}</p>
            )}
          </div>
        ) : (
          renderDisplayValue()
        )}
      </TableCell>
      <TableCell>
        <Badge variant="secondary" className="text-[10px]">{config.dataType}</Badge>
      </TableCell>
      <TableCell>
        {editing ? (
          <div className="flex justify-end gap-1">
            <Button
              size="icon"
              variant="ghost"
              className="h-7 w-7"
              onClick={handleSave}
              disabled={updateMutation.isPending}
            >
              <Check className="h-4 w-4 text-green-600" />
            </Button>
            <Button size="icon" variant="ghost" className="h-7 w-7" onClick={handleCancel}>
              <X className="h-4 w-4" />
            </Button>
          </div>
        ) : (
          <div className="flex justify-end">
            <Button
              size="icon"
              variant="ghost"
              className="h-7 w-7"
              onClick={() => setEditing(true)}
            >
              <Pencil className="h-4 w-4" />
            </Button>
          </div>
        )}
      </TableCell>
    </TableRow>
  );
}
