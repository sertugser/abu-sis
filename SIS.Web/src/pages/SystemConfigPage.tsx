import { useState } from 'react';
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
import { AlertCircle, Check, HelpCircle, Pencil, X } from 'lucide-react';
import { useSystemConfigs, useUpdateSystemConfig } from '../hooks/useSystemConfig';
import type { SystemConfigDto } from '../api/systemConfig.api';

const CATEGORY_LABELS: Record<string, string> = {
  Registration: 'Kayit Kurallari',
  GPA: 'Not Hesaplama',
  Scheduling: 'Zamanlama',
  Schedule: 'Ders Programi',
  General: 'Genel',
};

export default function SystemConfigPage() {
  const { data: configs, isLoading, isError } = useSystemConfigs();

  if (isLoading) return <Skeleton className="h-96 rounded-xl" />;
  if (isError) {
    return (
      <Alert variant="destructive">
        <AlertCircle className="h-4 w-4" />
        <AlertDescription>Sistem ayarlari yuklenemedi.</AlertDescription>
      </Alert>
    );
  }

  // Group by category
  const grouped = (configs ?? []).reduce<Record<string, SystemConfigDto[]>>((acc, c) => {
    const cat = c.category || 'General';
    (acc[cat] ??= []).push(c);
    return acc;
  }, {});

  return (
    <div className="space-y-6">
      <h1 className="text-xl font-bold">Sistem Ayarlari</h1>
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
                  <TableHead>Deger</TableHead>
                  <TableHead className="w-20">Tip</TableHead>
                  <TableHead className="w-24">Islem</TableHead>
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
  const updateMutation = useUpdateSystemConfig();

  const handleSave = () => {
    updateMutation.mutate(
      { id: config.id, value },
      {
        onSuccess: () => setEditing(false),
      },
    );
  };

  const handleCancel = () => {
    setValue(config.value);
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
                <HelpCircle className="h-3.5 w-3.5 text-muted-foreground cursor-help" />
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
          <Input
            className="h-8 w-48 text-sm"
            value={value}
            onChange={e => setValue(e.target.value)}
            onKeyDown={e => { if (e.key === 'Enter') handleSave(); if (e.key === 'Escape') handleCancel(); }}
            autoFocus
          />
        ) : (
          <span className="text-sm font-medium">{config.value}</span>
        )}
      </TableCell>
      <TableCell>
        <Badge variant="secondary" className="text-[10px]">{config.dataType}</Badge>
      </TableCell>
      <TableCell>
        {editing ? (
          <div className="flex gap-1">
            <Button size="icon" variant="ghost" className="h-7 w-7" onClick={handleSave} disabled={updateMutation.isPending}>
              <Check className="h-4 w-4 text-green-600" />
            </Button>
            <Button size="icon" variant="ghost" className="h-7 w-7" onClick={handleCancel}>
              <X className="h-4 w-4" />
            </Button>
          </div>
        ) : (
          <Button size="icon" variant="ghost" className="h-7 w-7" onClick={() => setEditing(true)}>
            <Pencil className="h-4 w-4" />
          </Button>
        )}
      </TableCell>
    </TableRow>
  );
}
