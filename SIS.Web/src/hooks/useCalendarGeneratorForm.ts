import { useMemo, useState } from 'react';
import { CALENDAR_EVENT_LABELS, calendarEventCategory } from '@/lib/calendarEventTypes';
import {
  academicYearStart,
  buildAcademicYearSelectionRange,
  buildSemesterSelectionRange,
  dateInput,
  defaultAcademicYear,
  fmt,
  isDateValueWithinRange,
  isWeekend,
  maxDateValue,
  nearestMondayDateValue,
  parseUtcDisplayDate,
  shiftAcademicYear,
  utcDate,
} from '@/lib/calendarGenerator/dates';
import { isSemesterType, semesterLabel } from '@/lib/calendarGenerator/semesters';
import { parseConstraintParameters, readString } from '@/lib/calendarGenerator/eventRules';
import { processOptionLabel } from '@/lib/calendarGenerator/processOptions';
import { type ActiveGeneratorSetting, type CalendarProcessOption } from '@/lib/calendarGenerator/types';
import { useCalendarTemplates, type CalendarConstraintInputDto } from './useCalendarGenerator';
import type { CalendarNotices } from './useCalendarNotices';

/**
 * "Kısıtlar & Üret" sekmesinin form durumu: akademik yıl, başlangıç tarihleri,
 * süre ayarları, tarih sabitleme (Pin) ve aktif kısıt listesi. Notice/onay
 * altyapısını ([[useCalendarNotices]]) parametre olarak alır.
 */
export function useCalendarGeneratorForm(notices: CalendarNotices) {
  const { askConfirm, setNotice } = notices;

  const [academicYear, setAcademicYear] = useState(defaultAcademicYear);
  const [includeSummer, setIncludeSummer] = useState(true);
  const [durationOverrides, setDurationOverrides] = useState<Record<string, number>>({});
  const [durationEventKey, setDurationEventKey] = useState('');
  const [durationDays, setDurationDays] = useState('');
  const [pinEventKey, setPinEventKey] = useState('');
  const [constraints, setConstraints] = useState<CalendarConstraintInputDto[]>([]);
  const [pinDate, setPinDate] = useState('');
  const [pinEndDate, setPinEndDate] = useState('');

  const templates = useCalendarTemplates();

  const defaultStartDates = useMemo(() => {
    const parsedStart = academicYearStart(academicYear);
    return {
      fallStartDate: nearestMondayDateValue(parsedStart, 9, 22),
      springStartDate: nearestMondayDateValue(parsedStart + 1, 2, 23),
    };
  }, [academicYear]);
  const [customStartDates, setCustomStartDates] = useState<{
    academicYear: string;
    fallStartDate: string;
    springStartDate: string;
  } | null>(null);
  const hasCustomStartDates = customStartDates?.academicYear === academicYear;
  const fallStartDate = hasCustomStartDates ? customStartDates.fallStartDate : defaultStartDates.fallStartDate;
  const springStartDate = hasCustomStartDates ? customStartDates.springStartDate : defaultStartDates.springStartDate;

  const durationOptions = useMemo(() => {
    const options: CalendarProcessOption[] = [];

    for (const template of templates.data ?? []) {
      for (const item of template.items) {
        for (const semester of item.applicableSemesters) {
          options.push({
            key: `${item.key}:${semester}`,
            templateItemKey: item.key,
            eventType: item.eventType,
            eventTypeLabel: CALENDAR_EVENT_LABELS[item.eventType] ?? item.eventType,
            title: item.title,
            defaultDurationDays: item.defaultDurationDays,
            semester,
            chainCode: template.chainCode,
          });
        }
      }
    }

    return options.sort((a, b) => processOptionLabel(a).localeCompare(processOptionLabel(b), 'tr'));
  }, [templates.data]);
  const durationOptionKeys = useMemo(() => new Set(durationOptions.map(option => option.key)), [durationOptions]);
  const activeDurationEventKey = durationOptionKeys.has(durationEventKey) ? durationEventKey : '';
  const activePinEventKey = durationOptionKeys.has(pinEventKey) ? pinEventKey : '';

  const selectedDurationOption = useMemo(
    () => durationOptions.find(option => option.key === activeDurationEventKey) ?? null,
    [activeDurationEventKey, durationOptions],
  );

  const selectedPinOption = useMemo(
    () => durationOptions.find(option => option.key === activePinEventKey) ?? null,
    [activePinEventKey, durationOptions],
  );
  const academicYearSelectionRange = useMemo(
    () => buildAcademicYearSelectionRange(academicYear, includeSummer),
    [academicYear, includeSummer],
  );
  const pinSelectionRange = useMemo(
    () => selectedPinOption
      ? buildSemesterSelectionRange(academicYear, selectedPinOption.semester)
      : academicYearSelectionRange,
    [academicYear, academicYearSelectionRange, selectedPinOption],
  );
  const pinEndDateMin = pinDate ? maxDateValue(pinSelectionRange.min, pinDate) : pinSelectionRange.min;

  // PinDate range dışındaysa temizle (adjust state during render)
  if (pinDate && !isDateValueWithinRange(pinDate, pinSelectionRange)) {
    setPinDate('');
  }

  // PinEndDate range dışındaysa temizle; PinDate'den önceyse PinDate'e eşitle
  if (pinEndDate) {
    if (!isDateValueWithinRange(pinEndDate, pinSelectionRange)) {
      setPinEndDate('');
    } else if (pinDate && pinEndDate < pinDate) {
      setPinEndDate(pinDate);
    }
  }

  const activeGeneratorSettings = useMemo(() => {
    const settings: ActiveGeneratorSetting[] = constraints.map((constraint, index) => {
      const parameters = parseConstraintParameters(constraint.parameterJson);
      const semesterValue = readString(parameters?.semester);
      const semester = semesterValue && isSemesterType(semesterValue) ? semesterValue : null;
      const templateItemKey = readString(parameters?.templateItemKey);
      const option = semester && templateItemKey
        ? durationOptions.find(item => item.key === `${templateItemKey}:${semester}`) ?? null
        : null;
      const startDate = readString(parameters?.startDate);
      const endDate = readString(parameters?.endDate) ?? startDate;
      const title = option
        ? processOptionLabel(option)
        : constraint.description ?? CALENDAR_EVENT_LABELS[constraint.targetEventType ?? ''] ?? 'Kısıt';
      const dateSummary = startDate
        ? dateInput(startDate) === dateInput(endDate)
          ? fmt(startDate)
          : `${fmt(startDate)} - ${fmt(endDate)}`
        : null;

      return {
        key: `constraint:${index}`,
        badgeLabel: constraint.constraintType,
        badgeVariant: constraint.isHard ? 'destructive' : 'secondary',
        title,
        summary: constraint.description ?? `${title} için kısıt tanımlandı.`,
        detailLines: [
          option ? `Süreç: ${processOptionLabel(option)}` : null,
          semester ? `Dönem: ${semesterLabel(semester)}` : null,
          dateSummary ? `Sabit aralık: ${dateSummary}` : null,
          constraint.targetChainCode ? `Zincir: ${constraint.targetChainCode}` : null,
          constraint.targetEventType ? `Etkinlik türü: ${CALENDAR_EVENT_LABELS[constraint.targetEventType] ?? constraint.targetEventType}` : null,
        ].filter((line): line is string => !!line),
        removeKind: 'constraint',
        removeIndex: index,
      };
    });

    Object.entries(durationOverrides).forEach(([key, days]) => {
      const option = durationOptions.find(item => item.key === key) ?? null;
      const delta = option ? days - option.defaultDurationDays : null;

      settings.push({
        key: `duration:${key}`,
        badgeLabel: 'Süre',
        badgeVariant: 'info',
        title: option ? processOptionLabel(option) : key,
        summary: `${days} gün olarak ayarlandı.`,
        detailLines: [
          option ? `Süreç: ${processOptionLabel(option)}` : null,
          option ? `Varsayılan süre: ${option.defaultDurationDays} gün` : null,
          `Yeni süre: ${days} gün`,
          delta !== null ? `Değişim: ${delta > 0 ? `+${delta}` : delta} gün` : null,
        ].filter((line): line is string => !!line),
        removeKind: 'duration',
        removeKey: key,
      });
    });

    return settings;
  }, [constraints, durationOptions, durationOverrides]);

  function handleAcademicYearShift(offset: number) {
    setAcademicYear(current => shiftAcademicYear(current, offset));
  }

  function setFallStartDate(value: string) {
    setCustomStartDates(current => ({
      academicYear,
      fallStartDate: value,
      springStartDate: current?.academicYear === academicYear
        ? current.springStartDate
        : defaultStartDates.springStartDate,
    }));
  }

  function setSpringStartDate(value: string) {
    setCustomStartDates(current => ({
      academicYear,
      fallStartDate: current?.academicYear === academicYear
        ? current.fallStartDate
        : defaultStartDates.fallStartDate,
      springStartDate: value,
    }));
  }

  function handleDurationEventChange(key: string) {
    setDurationEventKey(key);
    if (!key) {
      setDurationDays('');
      return;
    }
    const option = durationOptions.find(item => item.key === key);
    setDurationDays(String(durationOverrides[key] ?? option?.defaultDurationDays ?? 1));
  }

  function handlePinEventChange(key: string) {
    setPinEventKey(key);
    if (!key) {
      setPinDate('');
      setPinEndDate('');
    }
  }

  function handlePinDateChange(value: string) {
    setPinDate(value);
    if (pinEndDate && value && pinEndDate < value)
      setPinEndDate(value);
  }

  async function addPinConstraint() {
    if (!pinDate || !selectedPinOption) return;
    const resolvedEndDate = pinEndDate || pinDate;
    if (resolvedEndDate < pinDate) {
      setNotice('Bitiş tarihi başlangıç tarihinden önce olamaz.');
      return;
    }
    if (!isDateValueWithinRange(pinDate, pinSelectionRange) || !isDateValueWithinRange(resolvedEndDate, pinSelectionRange)) {
      setNotice(`Seçilen süreç için izin verilen tarih aralığı ${pinSelectionRange.label}.`);
      return;
    }
    const label = processOptionLabel(selectedPinOption);

    const isExam = calendarEventCategory(selectedPinOption.eventType, selectedPinOption.title, selectedPinOption.templateItemKey) === 'exam';
    if (isExam) {
      const parsedStart = parseUtcDisplayDate(pinDate);
      const parsedEnd = parseUtcDisplayDate(resolvedEndDate);
      if (parsedStart && isWeekend(parsedStart)) {
        setNotice('Sınav başlangıç tarihi hafta sonuna denk gelemez.');
        return;
      }
      if (parsedEnd && isWeekend(parsedEnd)) {
        setNotice('Sinav bitis tarihi hafta sonuna denk gelemez.');
        return;
      }
    }
    const shouldAdd = await askConfirm({
      title: 'Tarih sabitlensin mi?',
      message: `${label} etkinliği ${pinDate}${pinEndDate ? ` - ${pinEndDate}` : ''} tarihine sabitlenecek.\n\nBu kısıt bir sonraki taslak üretiminde dikkate alınacak. Devam edilsin mi?`,
      confirmLabel: 'Evet, ekle',
      cancelLabel: 'Vazgeç',
    });
    if (!shouldAdd)
      return;

    setConstraints(prev => [...prev, {
      constraintType: 'Pin',
      targetEventType: selectedPinOption.eventType,
      targetChainCode: selectedPinOption.chainCode,
      parameterJson: JSON.stringify({
        startDate: utcDate(pinDate),
        endDate: utcDate(resolvedEndDate),
        templateItemKey: selectedPinOption.templateItemKey,
        semester: selectedPinOption.semester,
      }),
      isHard: true,
      priority: 1,
      description: `${label} ${pinDate}${resolvedEndDate !== pinDate ? ` - ${resolvedEndDate}` : ''} aralığına sabitlendi`,
    }]);
    setPinDate('');
    setPinEndDate('');
    setNotice(`${label} için tarih sabitleme kısıtı eklendi.`);
  }

  async function addDurationOverride() {
    if (!selectedDurationOption) return;

    const days = Number(durationDays);
    if (!Number.isInteger(days) || days <= 0) {
      setNotice('Süre ayarı en az 1 tam gün olmalı.');
      return;
    }

    const label = processOptionLabel(selectedDurationOption);
    const shouldAdd = await askConfirm({
      title: 'Süre ayarı eklensin mi?',
      message:
        `${label} süresi bu taslak üretiminde ${selectedDurationOption.defaultDurationDays} gün yerine ${days} gün olacak.\n\n` +
        'Bu ayar sadece yeni taslak üretirken kullanılır; mevcut takvime hemen yazılmaz.',
      confirmLabel: 'Evet, süreyi ayarla',
      cancelLabel: 'Vazgeç',
      tone: 'info',
    });
    if (!shouldAdd)
      return;

    setDurationOverrides(prev => ({ ...prev, [selectedDurationOption.key]: days }));
    setNotice(`${label} için süre ayarı ${days} gün olarak eklendi.`);
  }

  function removeDurationOverride(key: string) {
    setDurationOverrides(prev => {
      const next = { ...prev };
      delete next[key];
      return next;
    });
    setNotice('Süre ayarı kaldırıldı.');
  }

  function removeConstraintAt(index?: number) {
    setConstraints(prev => prev.filter((_, i) => i !== index));
  }

  function resetGeneratorInputs() {
    setConstraints([]);
    setDurationOverrides({});
    setDurationEventKey('');
    setDurationDays('');
    setPinEventKey('');
    setPinDate('');
    setPinEndDate('');
    setNotice('Tüm kısıtlar ve süre ayarları sıfırlandı.');
  }

  return {
    academicYear,
    setAcademicYear,
    includeSummer,
    setIncludeSummer,
    fallStartDate,
    springStartDate,
    setFallStartDate,
    setSpringStartDate,
    handleAcademicYearShift,
    durationOverrides,
    durationDays,
    setDurationDays,
    constraints,
    pinDate,
    pinEndDate,
    setPinEndDate,
    templatesLoading: templates.isLoading,
    durationOptions,
    activeDurationEventKey,
    activePinEventKey,
    selectedDurationOption,
    selectedPinOption,
    pinSelectionRange,
    pinEndDateMin,
    activeGeneratorSettings,
    handleDurationEventChange,
    handlePinEventChange,
    handlePinDateChange,
    addPinConstraint,
    addDurationOverride,
    removeDurationOverride,
    removeConstraintAt,
    resetGeneratorInputs,
  };
}

export type CalendarGeneratorForm = ReturnType<typeof useCalendarGeneratorForm>;
