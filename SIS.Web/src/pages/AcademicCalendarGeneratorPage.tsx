import { CheckCircle2, ChevronDown, ChevronLeft, ChevronRight, GitBranch, RefreshCw, RotateCcw, Save, Trash2, Wand2 } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  DropdownMenu,
  DropdownMenuCheckboxItem,
  DropdownMenuContent,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { Input } from '@/components/ui/input';
import { Switch } from '@/components/ui/switch';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Textarea } from '@/components/ui/textarea';
import { cn } from '@/lib/utils';
import { calendarEventBadgeVariant } from '@/lib/calendarEventTypes';
import { fmt } from '@/lib/calendarGenerator/dates';
import { SEMESTERS, semesterLabel } from '@/lib/calendarGenerator/semesters';
import { displayText, type CalendarView } from '@/lib/calendarGenerator/types';
import { daySpan, eventCategoryLabel } from '@/lib/calendarGenerator/eventRules';
import { processOptionLabel } from '@/lib/calendarGenerator/processOptions';
import { SearchableProcessSelect } from '@/components/calendar-generator/SearchableProcessSelect';
import { TermSummaryCards } from '@/components/calendar-generator/TermSummaryCards';
import { WarningsPanel } from '@/components/calendar-generator/WarningsPanel';
import { HolidaysTab } from '@/components/calendar-generator/HolidaysTab';
import { DraftMiniCalendar } from '@/components/calendar-generator/DraftMiniCalendar';
import { DraftHistoryDialog } from '@/components/calendar-generator/DraftHistoryDialog';
import { ConfirmDialog } from '@/components/calendar-generator/ConfirmDialog';
import { ErrorDialog } from '@/components/calendar-generator/ErrorDialog';
import { NoticeToast } from '@/components/calendar-generator/NoticeToast';
import { useCalendarNotices } from '../hooks/useCalendarNotices';
import { useCalendarGeneratorForm } from '../hooks/useCalendarGeneratorForm';
import { useCalendarDraftWorkspace } from '../hooks/useCalendarDraftWorkspace';

export default function AcademicCalendarGeneratorPage() {
  const notices = useCalendarNotices();
  const form = useCalendarGeneratorForm(notices);
  const workspace = useCalendarDraftWorkspace(notices, form);

  const { notice, noticeTone, errorNotice, setErrorNotice, pendingConfirm, closeConfirm } = notices;
  const {
    academicYear,
    setAcademicYear,
    handleAcademicYearShift,
    fallStartDate,
    setFallStartDate,
    springStartDate,
    setSpringStartDate,
    includeSummer,
    setIncludeSummer,
    templatesLoading,
    durationOptions,
    activePinEventKey,
    handlePinEventChange,
    selectedPinOption,
    pinDate,
    handlePinDateChange,
    pinEndDate,
    setPinEndDate,
    pinSelectionRange,
    pinEndDateMin,
    addPinConstraint,
    activeDurationEventKey,
    handleDurationEventChange,
    durationDays,
    setDurationDays,
    selectedDurationOption,
    addDurationOverride,
    durationOverrides,
    removeDurationOverride,
    activeGeneratorSettings,
    removeConstraintAt,
    resetGeneratorInputs,
  } = form;
  const {
    calendar,
    activeDraftId,
    activeTab,
    setActiveTab,
    selectedSemester,
    setSelectedSemester,
    selectedEventId,
    setSelectedEventId,
    whatIf,
    hasActiveEventTypeFilter,
    filteredSemesterEvents,
    semesterEvents,
    eventTypeFilterSummary,
    eventTypeFilterOptions,
    selectedEventTypeFilters,
    setSelectedEventTypeFilters,
    toggleEventTypeFilter,
    selectEvent,
    selectedEvent,
    canEditSelectedEvent,
    editForm,
    setEditForm,
    handleWhatIf,
    handleUpdateEvent,
    updateDraftEvent,
    termAlignment,
    whatIfDiffs,
    forceOverwrite,
    setForceOverwrite,
    applySemesters,
    setApplySemesters,
    handleApply,
    applyDraft,
    adminTerms,
    handleRollback,
    rollbackDraft,
    handleDraftEventDrop,
    getDraftDropFeedback,
    handleInvalidDraftEventDrop,
    handleGenerate,
    generate,
    visibleHolidayDefinitions,
    holidays,
    showDraftHistory,
    setShowDraftHistory,
    drafts,
    deleteDraft,
    openDraftFromHistory,
    deleteDraftFromHistory,
    deleteAllDraftsFromHistory,
  } = workspace;

  function renderGeneratorTab() {
    return (
      <div className="grid gap-3 xl:grid-cols-[1.2fr_0.8fr]">
          <div className="rounded-lg border border-slate-200 bg-white p-3 shadow-sm">
            <div className="grid gap-3 md:grid-cols-4">
            <label className="space-y-0.5 text-sm font-medium text-slate-700">
              Akademik yıl
              <div className="flex items-center gap-2">
                <Button type="button" variant="outline" size="icon" onClick={() => handleAcademicYearShift(-1)} aria-label="Önceki akademik yıl">
                  <ChevronLeft className="h-4 w-4" />
                </Button>
                <Input value={academicYear} onChange={e => setAcademicYear(e.target.value)} placeholder="2025-2026" />
                <Button type="button" variant="outline" size="icon" onClick={() => handleAcademicYearShift(1)} aria-label="Sonraki akademik yıl">
                  <ChevronRight className="h-4 w-4" />
                </Button>
              </div>
            </label>
            <label className="space-y-0.5 text-sm font-medium text-slate-700">
              Güz ders başlangıcı
              <Input type="date" value={fallStartDate} onChange={e => setFallStartDate(e.target.value)} />
            </label>
            <label className="space-y-0.5 text-sm font-medium text-slate-700">
              Bahar ders başlangıcı
              <Input type="date" value={springStartDate} onChange={e => setSpringStartDate(e.target.value)} />
            </label>
            <div className="flex items-end">
              <label className="flex h-10 items-center gap-3 rounded-lg border border-slate-200 px-3 text-sm font-medium text-slate-700">
                <Switch checked={includeSummer} onCheckedChange={setIncludeSummer} />
                Yaz okulu dahil
              </label>
            </div>
          </div>

          <div className="mt-3">
            <div className="rounded-lg border border-slate-200 p-2.5">
              <div className="mb-2 flex items-center gap-2 text-sm font-semibold text-slate-800">
                <GitBranch className="h-3.5 w-3.5" />
                Tarih Sabitleme
              </div>
              <p className="text-xs text-slate-500">
                Belirli bir etkinliğin tarihini sabitlemek için seçin ve başlangıç / bitiş tarihini girin.
              </p>
              <div className="mt-2 grid gap-2 md:grid-cols-[minmax(0,1.5fr)_140px_140px]">
                <SearchableProcessSelect
                  options={durationOptions}
                  selectedKey={activePinEventKey}
                  onChange={handlePinEventChange}
                  disabled={templatesLoading || durationOptions.length === 0}
                  placeholder={templatesLoading ? 'Yükleniyor...' : 'Etkinlik seçin'}
                />
                <Input
                  type="date"
                  value={pinDate}
                  min={pinSelectionRange.min}
                  max={pinSelectionRange.max}
                  onChange={e => handlePinDateChange(e.target.value)}
                />
                <Input
                  type="date"
                  value={pinEndDate}
                  min={pinEndDateMin}
                  max={pinSelectionRange.max}
                  onChange={e => setPinEndDate(e.target.value)}
                />
              </div>
              <p className="mt-1 text-xs text-slate-500">
                Seçilebilir tarih aralığı: {pinSelectionRange.label}
              </p>
              {selectedPinOption && (
                <p className="mt-1 text-xs font-medium text-slate-700">
                  Seçilen: {processOptionLabel(selectedPinOption)}. Yalnızca seçilen dönem için.
                </p>
              )}
              <Button type="button" size="sm" className="mt-2" onClick={addPinConstraint} disabled={!activePinEventKey || !selectedPinOption}>
                Kısıt Ekle
              </Button>
            </div>
          </div>

          <div className="mt-3 rounded-lg border border-slate-200 p-2.5">
            <div className="text-sm font-semibold text-slate-800">Süre Ayarları</div>
            <p className="text-xs text-slate-500">
              Bir etkinliğin varsayılan süresini değiştirmek için seçin ve yeni gün sayısını girin.
            </p>

            <div className="mt-2 grid gap-2 md:grid-cols-[minmax(0,1.5fr)_140px_auto]">
              <SearchableProcessSelect
                options={durationOptions}
                selectedKey={activeDurationEventKey}
                onChange={handleDurationEventChange}
                disabled={templatesLoading || durationOptions.length === 0}
                placeholder={templatesLoading ? 'Yükleniyor...' : 'Etkinlik seçin'}
              />
              <Input
                type="number"
                min={1}
                value={durationDays}
                onChange={e => setDurationDays(e.target.value)}
                placeholder="Gün"
              />
              <Button type="button" size="sm" onClick={addDurationOverride} disabled={!selectedDurationOption}>
                Süre ayarı ekle
              </Button>
            </div>
            {selectedDurationOption && (
              <p className="mt-1 text-xs font-medium text-slate-700">
                Seçilen: {processOptionLabel(selectedDurationOption)}. Varsayılan: {selectedDurationOption.defaultDurationDays} gün.
              </p>
            )}

            <div className="mt-2 space-y-1.5">
              {Object.keys(durationOverrides).length === 0 ? (
                <p className="text-xs text-slate-500">Henüz süre ayarı yok.</p>
              ) : Object.entries(durationOverrides).map(([key, days]) => {
                const option = durationOptions.find(item => item.key === key);
                return (
                  <div key={key} className="flex items-center justify-between gap-3 rounded-lg border border-slate-200 px-3 py-2 text-sm">
                    <div>
                      <div className="font-medium text-slate-800">{option ? processOptionLabel(option) : key}</div>
                      <div className="text-xs text-slate-500">
                        {option ? `${option.defaultDurationDays} gün yerine ` : ''}{days} gün
                      </div>
                    </div>
                    <Button type="button" size="sm" variant="ghost" onClick={() => removeDurationOverride(key)}>
                      Sil
                    </Button>
                  </div>
                );
              })}
            </div>
          </div>

          <div className="mt-3 flex flex-wrap items-center gap-2">
            <Button size="sm" onClick={handleGenerate} disabled={generate.isPending}>
              {generate.isPending ? <RefreshCw className="h-4 w-4 animate-spin" /> : <Wand2 className="h-4 w-4" />}
              Taslak Üret
            </Button>
            <Button type="button" size="sm" variant="outline" onClick={resetGeneratorInputs}>
              <Trash2 className="h-4 w-4" />
              Sıfırla
            </Button>
            <span className="text-xs text-slate-500">Taslaklar 30 gün saklanır.</span>
          </div>
        </div>

        <div className="rounded-lg border border-slate-200 bg-white p-3 shadow-sm">
          <h2 className="text-sm font-semibold text-slate-900">Aktif Kısıtlar</h2>
          <div className="mt-2 space-y-1.5">
            {activeGeneratorSettings.length === 0 ? (
              <p className="text-sm text-slate-500">Henüz ek kısıt yok.</p>
            ) : activeGeneratorSettings.map(setting => (
              <div key={setting.key} className="rounded-lg border border-slate-200 px-2.5 py-2">
                <div className="flex items-start justify-between gap-2">
                  <div className="min-w-0">
                    <Badge variant={setting.badgeVariant}>{setting.badgeLabel}</Badge>
                    <p className="mt-1 text-xs font-semibold text-slate-800">{setting.title}</p>
                    <p className="text-xs text-slate-700">{setting.summary}</p>
                    {setting.detailLines.length > 0 && (
                      <div className="mt-1 grid gap-0.5 text-[11px] text-slate-500">
                        {setting.detailLines.map(line => (
                          <div key={`${setting.key}:${line}`}>{line}</div>
                        ))}
                      </div>
                    )}
                  </div>
                  <Button
                    type="button"
                    size="sm"
                    variant="ghost"
                    onClick={() => {
                      if (setting.removeKind === 'constraint') {
                        removeConstraintAt(setting.removeIndex);
                        return;
                      }
                      if (setting.removeKey)
                        removeDurationOverride(setting.removeKey);
                    }}
                  >
                    Sil
                  </Button>
                </div>
              </div>
            ))}
          </div>

          <div className="mt-3 border-t border-cyan-200 pt-2 text-sm text-cyan-900">
            <div className="text-xs font-semibold">Yönetmelik Kontrolü</div>
            <div className="mt-1 space-y-0.5 text-[11px]">
              <div>Normal: 14 hafta ders, sınavlarla 16 hafta. Yaz: 7 hafta.</div>
              <div>Çekilme/dondurma: 7. hafta. Sınav sonuçları: 10 iş günü.</div>
            </div>
          </div>
        </div>
      </div>
    );
  }

  function renderDraftTab() {
    if (!calendar) {
      return (
        <div className="rounded-lg border border-dashed border-slate-300 bg-white p-8 text-center text-sm text-slate-500">
          Henüz taslak yok. "Kısıtlar & Üret" sekmesinden bir taslak oluşturun.
        </div>
      );
    }

    return (
      <div className="space-y-3">
        <WarningsPanel current={calendar} />

        <div className="grid gap-3 lg:grid-cols-3 items-start">
          <div className="space-y-3 lg:col-span-2">
            <div className="flex flex-col rounded-lg border border-slate-200 bg-white p-3 shadow-sm">
              <div className="flex shrink-0 flex-wrap items-center justify-between gap-2">
                <div>
                  <h2 className="text-sm font-semibold text-slate-900">Taslak #{activeDraftId}</h2>
                  <p className="text-xs text-slate-500">
                    {calendar.academicYear} için {calendar.events.length} etkinlik üretildi.
                    {hasActiveEventTypeFilter ? ` Görünen: ${filteredSemesterEvents.length}/${semesterEvents.length}.` : ''}
                  </p>
                </div>
                <div className="flex flex-wrap gap-2">
                  {SEMESTERS.map(tab => (
                    <Button
                      key={tab.value}
                      type="button"
                      size="sm"
                      variant={selectedSemester === tab.value ? 'default' : 'outline'}
                      onClick={() => {
                        setSelectedSemester(tab.value);
                        setSelectedEventId(null);
                        whatIf.reset();
                      }}
                    >
                      {tab.label}
                    </Button>
                  ))}
                </div>
              </div>

              <div className="mt-3 flex flex-1 flex-col overflow-hidden rounded-lg border border-slate-200">
                <div className="shrink-0 grid grid-cols-[minmax(0,1fr)_96px_120px] items-center bg-slate-50 px-2 py-1.5 text-[11px] font-semibold uppercase text-slate-500 sm:grid-cols-[minmax(0,1.5fr)_130px_140px]">
                  <span>Etkinlik</span>
                  <span className="text-right sm:text-left">Tarih</span>
                  <div className="flex justify-end sm:justify-start">
                    <DropdownMenu>
                      <DropdownMenuTrigger asChild>
                        <button
                          type="button"
                          className="inline-flex items-center gap-1 rounded-md px-2 py-1 text-[11px] font-semibold uppercase text-slate-500 transition hover:bg-slate-100 hover:text-slate-700"
                        >
                          {eventTypeFilterSummary}
                          <ChevronDown className="h-3.5 w-3.5" />
                        </button>
                      </DropdownMenuTrigger>
                      <DropdownMenuContent align="end" className="w-56">
                        <DropdownMenuLabel>Tür filtresi</DropdownMenuLabel>
                        <DropdownMenuCheckboxItem checked={!hasActiveEventTypeFilter} onCheckedChange={() => setSelectedEventTypeFilters([])}>
                          Tüm türler
                        </DropdownMenuCheckboxItem>
                        <DropdownMenuSeparator />
                        {eventTypeFilterOptions.map(option => (
                          <DropdownMenuCheckboxItem
                            key={option.value}
                            checked={selectedEventTypeFilters.includes(option.value)}
                            onCheckedChange={checked => toggleEventTypeFilter(option.value, checked === true)}
                          >
                            <div className="flex w-full items-center justify-between gap-3">
                              <Badge variant={option.badgeVariant}>{option.label}</Badge>
                              <span className="text-xs text-slate-500">{option.count}</span>
                            </div>
                          </DropdownMenuCheckboxItem>
                        ))}
                      </DropdownMenuContent>
                    </DropdownMenu>
                  </div>
                </div>
                <div className="flex-1 overflow-auto min-h-[320px]">
                  {filteredSemesterEvents.length === 0 ? (
                    <div className="px-4 py-10 text-center text-sm text-slate-500">
                      <p>{hasActiveEventTypeFilter ? 'Seçilen tür filtresine uygun etkinlik bulunamadı.' : 'Bu dönem için etkinlik yok.'}</p>
                      {hasActiveEventTypeFilter && (
                        <div className="mt-3">
                          <Button type="button" size="sm" variant="outline" onClick={() => setSelectedEventTypeFilters([])}>
                            Filtreyi Temizle
                          </Button>
                        </div>
                      )}
                    </div>
                  ) : filteredSemesterEvents.map(ev => {
                    const isSelected = selectedEventId === ev.draftEventId;
                    return (
                      <button
                        key={ev.draftEventId}
                        type="button"
                        onClick={() => selectEvent(ev)}
                        className={cn(
                          'relative grid w-full grid-cols-[minmax(0,1fr)_96px_120px] items-center gap-1 border-t px-2 py-1.5 text-left text-sm transition-all duration-150 hover:bg-slate-100 sm:grid-cols-[minmax(0,1.5fr)_130px_140px]',
                          isSelected
                            ? 'z-10 border-transparent bg-primary/10 ring-2 ring-inset ring-primary shadow-sm'
                            : 'border-slate-100',
                        )}
                        aria-pressed={isSelected}
                      >
                        <span className="min-w-0">
                          <span className={cn('block truncate font-medium text-slate-800', isSelected && 'text-primary')}>{displayText(ev.title)}</span>
                        </span>
                        <span className={cn('text-right text-xs text-slate-600 sm:text-left', isSelected && 'font-semibold text-primary')}>
                          {daySpan(ev)}
                        </span>
                        <span className="flex flex-wrap justify-end gap-1 sm:justify-start">
                          <Badge variant={calendarEventBadgeVariant(ev.eventType, ev.title, ev.templateItemKey)}>
                            {eventCategoryLabel(ev.eventType, ev.isHoliday, ev.title, ev.templateItemKey)}
                          </Badge>
                          {ev.isFailed && <Badge variant="destructive">Hata</Badge>}
                        </span>
                      </button>
                    );
                  })}
                </div>
              </div>
            </div>

          </div>

          <div className="space-y-3">
              <div className="rounded-lg border border-slate-200 bg-white p-3 shadow-sm">
              <h2 className="text-sm font-semibold text-slate-900">Tarih Düzenleme</h2>
              {!selectedEvent ? (
                <p className="mt-2 text-sm text-slate-500">Bir etkinlik seçin, tarihi değiştirin ve zincirleme kaydırmayı ön izleyin.</p>
              ) : !canEditSelectedEvent ? (
                <div className="mt-3 space-y-3">
                  <div className="rounded-lg border border-amber-200 bg-amber-50 px-3 py-3 text-sm text-amber-900">
                    Tatil ve bloklayici ozel gunler tarih duzenleme ile degistirilemez.
                  </div>
                  <div className="grid grid-cols-2 gap-3 text-sm">
                    <div>
                      <p className="font-medium text-slate-500">Etkinlik</p>
                      <p className="mt-1 text-slate-900">{displayText(selectedEvent.title)}</p>
                    </div>
                    <div>
                      <p className="font-medium text-slate-500">Tarih</p>
                      <p className="mt-1 text-slate-900">{daySpan(selectedEvent)}</p>
                    </div>
                  </div>
                </div>
              ) : (
                <div className="mt-2 space-y-2">
                  <label className="block space-y-0.5 text-sm font-medium text-slate-700">
                    Başlık
                    <Input value={editForm.title} onChange={e => setEditForm(prev => ({ ...prev, title: e.target.value }))} />
                  </label>
                  <div className="grid grid-cols-2 gap-2">
                    <label className="block space-y-0.5 text-sm font-medium text-slate-700">
                      Başlangıç
                      <Input type="date" value={editForm.startDate} onChange={e => setEditForm(prev => ({ ...prev, startDate: e.target.value }))} />
                    </label>
                    <label className="block space-y-1 text-sm font-medium text-slate-700">
                      Bitiş
                      <Input type="date" value={editForm.endDate} onChange={e => setEditForm(prev => ({ ...prev, endDate: e.target.value }))} />
                    </label>
                  </div>
                  <label className="block space-y-0.5 text-sm font-medium text-slate-700">
                    Not
                    <Textarea rows={2} value={editForm.note} onChange={e => setEditForm(prev => ({ ...prev, note: e.target.value }))} />
                  </label>
                  <div className="flex flex-wrap gap-2">
                    <Button type="button" size="sm" variant="outline" onClick={handleWhatIf} disabled={whatIf.isPending}>
                      <GitBranch className="h-4 w-4" />
                      Zincirleme Önizle
                    </Button>
                    <Button type="button" size="sm" onClick={handleUpdateEvent} disabled={updateDraftEvent.isPending}>
                      <Save className="h-4 w-4" />
                      Taslağa Yaz
                    </Button>
                  </div>
                </div>
              )}
              </div>
              {renderDraftSidePanels(calendar)}
          </div>
        </div>
      </div>
    );
  }

  function renderDraftSidePanels(current: CalendarView) {
    return (
      <>
        {termAlignment.hasMismatch && (
          <div className="rounded-lg border border-red-200 bg-red-50 p-3 shadow-sm">
            <h2 className="text-sm font-semibold text-red-900">Dönem uyumsuzluğu</h2>
            <p className="mt-1 text-xs leading-5 text-red-800">
              {termAlignment.message}
            </p>
          </div>
        )}

        <div className="rounded-lg border border-slate-200 bg-white p-3 shadow-sm">
          <h2 className="text-sm font-semibold text-slate-900">Zincirleme Kaydırma Farkı</h2>
          {whatIfDiffs.length === 0 ? (
            <p className="mt-1 text-xs text-slate-500">Hesaplanmış fark yok.</p>
          ) : (
            <div className="mt-3 space-y-2">
              {whatIfDiffs.map(({ next, prev }) => (
                <div key={next.draftEventId} className="rounded-lg border border-slate-200 px-3 py-2 text-xs">
                  <div className="font-semibold text-slate-800">{displayText(next.title)}</div>
                  <div className="mt-1 text-slate-500">
                    {fmt(prev?.startDate)} -&gt; {fmt(next.startDate)}
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>

        <div className="rounded-lg border border-slate-200 bg-white p-3 shadow-sm">
          <div className="flex items-center justify-between gap-2">
            <div>
              <h2 className="text-sm font-semibold text-slate-900">Onayla ve kaydet</h2>
              <p className="mt-0.5 max-w-md text-xs leading-5 text-slate-600">
                Taslak onaylandığında eski takvim oluşturucu kayıtları temizlenir, bu etkinlikler yazılır.
                {forceOverwrite ? ' Yayınlanmış dönemlerin üzerine yazma açık.' : ' Yayınlanmış dönem varsa işlem reddedilir.'}
              </p>
              {termAlignment.hasMismatch && (
                <p className="mt-1 max-w-md text-xs font-medium text-red-700">
                  Uyarı: {termAlignment.message}
                </p>
              )}
            </div>
            <Switch checked={forceOverwrite} onCheckedChange={setForceOverwrite} />
          </div>
          {termAlignment.expectedSemesters.length > 0 && (
            <div className="mt-3 border-t border-slate-200 pt-3">
              <p className="text-xs font-medium text-slate-700">Uygulanacak yarıyıllar</p>
              <p className="mt-0.5 text-[11px] text-slate-500">
                İşaretlemediğin yarıyıla dokunulmaz; eşleşen dönem yoksa yeni dönem açılır.
              </p>
              <div className="mt-1.5 flex flex-wrap gap-2">
                {termAlignment.expectedSemesters.map(s => {
                  const exists = termAlignment.existingSemesters.includes(s);
                  const checked = applySemesters.includes(s);
                  return (
                    <label
                      key={s}
                      className="flex items-center gap-2 rounded-md border border-slate-200 px-2.5 py-1.5 text-xs cursor-pointer hover:bg-slate-50"
                    >
                      <input
                        type="checkbox"
                        checked={checked}
                        onChange={e =>
                          setApplySemesters(prev =>
                            e.target.checked ? [...prev, s] : prev.filter(x => x !== s),
                          )
                        }
                        className="h-3.5 w-3.5 accent-slate-900"
                      />
                      <span className="font-medium text-slate-800">{semesterLabel(s)}</span>
                      <span className={exists ? 'text-emerald-600' : 'text-amber-600'}>
                        {exists ? '(mevcut)' : '(yeni dönem açılacak)'}
                      </span>
                    </label>
                  );
                })}
              </div>
            </div>
          )}
          <div className="mt-2 flex flex-wrap gap-2">
            <Button type="button" onClick={handleApply} disabled={!activeDraftId || applyDraft.isPending || adminTerms.isLoading}>
              {applyDraft.isPending ? <RefreshCw className="h-4 w-4 animate-spin" /> : <CheckCircle2 className="h-4 w-4" />}
              {applyDraft.isPending ? 'Kaydediliyor' : 'Onayla'}
            </Button>
            <Button type="button" variant="outline" onClick={handleRollback} disabled={!activeDraftId || rollbackDraft.isPending}>
              <RotateCcw className="h-4 w-4" />
              Geri al
            </Button>
          </div>
        </div>

        <DraftMiniCalendar
          current={current}
          whatIfData={whatIf.data}
          onEventDrop={handleDraftEventDrop}
          getDropFeedback={getDraftDropFeedback}
          onInvalidEventDrop={handleInvalidDraftEventDrop}
          dragDisabled={updateDraftEvent.isPending}
        />
        <TermSummaryCards current={current} />
      </>
    );
  }

  return (
    <div className="space-y-3">
      <Tabs value={activeTab} onValueChange={setActiveTab} className="space-y-3">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <TabsList className="bg-white">
            <TabsTrigger value="generator">Kısıtlar & Üret</TabsTrigger>
            <TabsTrigger value="draft">Taslak & Zincirleme</TabsTrigger>
            <TabsTrigger value="holidays">Tatil Tanımları</TabsTrigger>
          </TabsList>
          <div className="flex flex-wrap items-center gap-2">
            <Button type="button" variant="outline" size="sm" onClick={() => setShowDraftHistory(true)}>
              Taslak geçmişi
            </Button>
          </div>
        </div>

        <ErrorDialog message={errorNotice} onClose={() => setErrorNotice(null)} />

        <NoticeToast notice={notice} tone={noticeTone} />

        <TabsContent value="generator">{renderGeneratorTab()}</TabsContent>
        <TabsContent value="draft">{renderDraftTab()}</TabsContent>
        <TabsContent value="holidays">
          <HolidaysTab isLoading={holidays.isLoading} holidays={visibleHolidayDefinitions} />
        </TabsContent>
      </Tabs>

      <DraftHistoryDialog
        open={showDraftHistory}
        drafts={drafts.data}
        isLoading={drafts.isLoading}
        isFetching={drafts.isFetching}
        deletePending={deleteDraft.isPending}
        onClose={() => setShowDraftHistory(false)}
        onRefetch={() => drafts.refetch()}
        onOpenDraft={openDraftFromHistory}
        onDeleteDraft={deleteDraftFromHistory}
        onDeleteAll={deleteAllDraftsFromHistory}
      />

      <ConfirmDialog confirm={pendingConfirm} onClose={closeConfirm} />
    </div>
  );
}
