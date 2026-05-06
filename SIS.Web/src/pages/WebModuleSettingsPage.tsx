import { useDeferredValue, useEffect, useMemo, useState } from 'react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Skeleton } from '@/components/ui/skeleton';
import { AlertCircle, CheckCircle2, Loader2, Search } from 'lucide-react';
import { useUpdateWebModuleControl, useWebModuleControlSummaries } from '../hooks/useWebModuleSettings';
import type { WebModuleControlSummaryDto } from '../api/webModuleSettings.api';
import { getErrorMessage } from '../lib/errorMessage';
import { cn } from '../lib/utils';

type Notice = {
  type: 'success' | 'error';
  message: string;
};

export default function WebModuleSettingsPage() {
  const { data, isLoading, isError } = useWebModuleControlSummaries();
  const [searchText, setSearchText] = useState('');
  const deferredSearch = useDeferredValue(searchText.trim().toLocaleLowerCase('tr-TR'));
  const [notice, setNotice] = useState<Notice | null>(null);

  useEffect(() => {
    if (!notice) return;
    const timer = setTimeout(() => setNotice(null), notice.type === 'error' ? 8000 : 4000);
    return () => clearTimeout(timer);
  }, [notice]);

  const filteredModules = useMemo(() => {
    const modules = [...(data ?? [])]
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

  if (isLoading) {
    return (
      <div className="space-y-3">
        <Skeleton className="h-16 rounded-md" />
        <Skeleton className="h-[42rem] rounded-md" />
      </div>
    );
  }

  if (isError) {
    return (
      <Alert variant="destructive">
        <AlertCircle className="h-4 w-4" />
        <AlertDescription>Web modül ayarları yüklenemedi.</AlertDescription>
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

      <div className="overflow-hidden rounded-md border border-slate-300 bg-white shadow-sm">
        <div className="border-b border-slate-200 bg-slate-50 px-5 py-4">
          <p className="text-sm font-semibold text-slate-700">Sistem Konfigürasyonu</p>
        </div>

        <div className="border-b border-slate-200 px-5 py-4">
          <div className="flex flex-col gap-2 lg:flex-row lg:items-center">
            <Input
              value={searchText}
              onChange={event => setSearchText(event.target.value)}
              placeholder="Modül ara"
              className="h-10 rounded-sm border-slate-300 lg:max-w-md"
            />
            <Button type="button" variant="outline" className="h-10 rounded-sm border-slate-300 px-5 text-sm text-slate-700">
              <Search className="mr-2 h-4 w-4" />
              Ara/Bul
            </Button>
          </div>

          <p className="mt-3 text-xs leading-5 text-slate-500">
            Bu sayfada yapılan değişiklikler sadece görünümü değil, ilgili backend akışlarını da etkiler.
            Ders kayıt, danışman onayı, not girişi ve not yayınlama kararları bu ayarlardan okunur.
          </p>
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
                onNotice={setNotice}
              />
            ))}
          </div>
        )}
      </div>
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
  const isClosedState = mode === 'closed' || module.effectiveStateLabel.toLocaleLowerCase('tr-TR') === 'kapalı';

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
                : module.isCurrentlyOpen
                  ? 'border-emerald-200 bg-emerald-100 text-emerald-700'
                  : 'border-amber-200 bg-amber-100 text-amber-700',
            )}>
              {module.effectiveStateLabel}
            </span>
          </div>

          <p className="text-xs text-slate-500">{module.description}</p>
          {module.effectiveMessage && (
            <p className="text-xs leading-5 text-slate-600">{module.effectiveMessage}</p>
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
