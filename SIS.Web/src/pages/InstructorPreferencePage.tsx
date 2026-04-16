import { useState, useMemo, useCallback } from 'react';
import { useAuth } from '../context/AuthContext';
import { useInstructorProfile } from '../hooks/useInstructorProfile';
import {
  useInstructorPreferences,
  useCreatePreference,
  useDeletePreference,
  useApprovePreference,
  usePreferencesSummary,
} from '../hooks/useInstructorPreferences';
import { useAdminInstructors } from '../hooks/useAdminInstructors';
import { useActiveTerm } from '../hooks/useActiveTerm';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import {
  Clock,
  Building2,
  Trash2,
  Check,
  X,
  Plus,
  Calendar,
  AlertCircle,
} from 'lucide-react';
import type { InstructorPreferenceDto, CreateInstructorPreferenceRequest } from '../types/instructorPreference';

// ── Constants ───────────────────────────────────────────────────────

const DAY_NAMES = ['Pazartesi', 'Sali', 'Carsamba', 'Persembe', 'Cuma', 'Cumartesi'];
const DAY_VALUES = [1, 2, 3, 4, 5, 6]; // DayOfWeek: Monday=1..Saturday=6

const TIME_SLOTS = Array.from({ length: 13 }, (_, i) => {
  const hour = 8 + i;
  return {
    label: `${hour.toString().padStart(2, '0')}:00`,
    start: `${hour.toString().padStart(2, '0')}:00`,
    end: `${(hour + 1).toString().padStart(2, '0')}:00`,
    hour,
  };
});

const LEVEL_COLORS: Record<string, string> = {
  StronglyPreferred: 'bg-emerald-500 text-white',
  Preferred: 'bg-emerald-200 text-emerald-900',
  Discouraged: 'bg-amber-200 text-amber-900',
  StronglyDiscouraged: 'bg-orange-400 text-white',
  Unavailable: 'bg-red-500 text-white',
};

const LEVEL_LABELS: Record<string, string> = {
  Preferred: 'Tercih Edilen',
  StronglyPreferred: 'Cok Tercih Edilen',
  Discouraged: 'Tercih Edilmeyen',
  StronglyDiscouraged: 'Kesinlikle Tercih Edilmeyen',
  Unavailable: 'Musait Degil',
};

const PREF_TYPE_LABELS: Record<string, string> = {
  TimeSlotPreference: 'Zaman Tercihi',
  ClassroomPreference: 'Derslik Tercihi',
  DailyLoadPreference: 'Gunluk Yuk Tercihi',
  DayOffPreference: 'Izin Gunu Tercihi',
};

const CYCLE_ORDER: InstructorPreferenceDto['preferenceLevel'][] = [
  'Preferred',
  'StronglyPreferred',
  'Discouraged',
  'StronglyDiscouraged',
  'Unavailable',
];

// ── Helper ──────────────────────────────────────────────────────────

function isManager(roles: string[]): boolean {
  return roles.some(r => ['SystemAdministrator', 'Dean', 'DepartmentManager'].includes(r));
}

// ── Main Page ───────────────────────────────────────────────────────

export default function InstructorPreferencePage() {
  const { user } = useAuth();
  const roles = user?.roles ?? [];
  const isAdmin = isManager(roles);

  const { data: profile } = useInstructorProfile();
  const { data: activeTerm } = useActiveTerm();

  // Admin: select instructor
  const [selectedInstructorId, setSelectedInstructorId] = useState<number | undefined>();
  const { data: allInstructors } = useAdminInstructors({ isActive: true });

  const instructorId = isAdmin && selectedInstructorId
    ? selectedInstructorId
    : profile?.instructorId;

  const termId = activeTerm?.id;

  const { data: preferences, isLoading } = useInstructorPreferences(instructorId, termId);
  const createMutation = useCreatePreference();
  const deleteMutation = useDeletePreference();
  const approveMutation = useApprovePreference();

  // Dialog state for non-time preferences
  const [dialogOpen, setDialogOpen] = useState(false);
  const [dialogType, setDialogType] = useState<'ClassroomPreference' | 'DailyLoadPreference' | 'DayOffPreference'>('ClassroomPreference');

  // ── Grid cell click handler ───────────────────────────────────────

  const timePrefs = useMemo(
    () => (preferences ?? []).filter(p => p.preferenceType === 'TimeSlotPreference'),
    [preferences],
  );

  const getCellPref = useCallback(
    (dayValue: number, hour: number): InstructorPreferenceDto | undefined => {
      const startStr = `${hour.toString().padStart(2, '0')}:00`;
      return timePrefs.find(
        p => p.dayOfWeek === dayValue && p.timeSlotStart === startStr,
      );
    },
    [timePrefs],
  );

  const handleCellClick = useCallback(
    (dayValue: number, slot: typeof TIME_SLOTS[number]) => {
      if (!instructorId) return;
      const existing = getCellPref(dayValue, slot.hour);

      if (existing) {
        const currentIdx = CYCLE_ORDER.indexOf(existing.preferenceLevel);
        if (currentIdx === CYCLE_ORDER.length - 1) {
          // Last level — delete on next click
          deleteMutation.mutate(existing.id);
        } else {
          // Cycle to next level by deleting and creating new
          deleteMutation.mutate(existing.id, {
            onSuccess: () => {
              createMutation.mutate({
                instructorId,
                request: {
                  preferenceType: 'TimeSlotPreference',
                  preferenceLevel: CYCLE_ORDER[currentIdx + 1],
                  dayOfWeek: dayValue,
                  timeSlotStart: slot.start,
                  timeSlotEnd: slot.end,
                },
              });
            },
          });
        }
      } else {
        // No existing pref — create first level
        createMutation.mutate({
          instructorId,
          request: {
            preferenceType: 'TimeSlotPreference',
            preferenceLevel: CYCLE_ORDER[0],
            dayOfWeek: dayValue,
            timeSlotStart: slot.start,
            timeSlotEnd: slot.end,
          },
        });
      }
    },
    [instructorId, getCellPref, createMutation, deleteMutation],
  );

  // ── Other preferences ─────────────────────────────────────────────

  const otherPrefs = useMemo(
    () => (preferences ?? []).filter(p => p.preferenceType !== 'TimeSlotPreference'),
    [preferences],
  );

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold tracking-tight">Ogretim Uyesi Tercihleri</h1>
          <p className="text-muted-foreground text-sm mt-1">
            Ders programi icin zaman ve derslik tercihlerinizi belirleyin.
          </p>
        </div>
        {!isAdmin && !profile?.instructorId && (
          <Badge variant="destructive" className="gap-1">
            <AlertCircle className="h-3 w-3" /> Ogretim uyesi profili bulunamadi
          </Badge>
        )}
      </div>

      {/* Admin: instructor selector */}
      {isAdmin && (
        <Card>
          <CardContent className="pt-4">
            <div className="flex items-center gap-4">
              <Label className="whitespace-nowrap font-medium">Ogretim Uyesi:</Label>
              <Select
                value={selectedInstructorId?.toString() ?? ''}
                onValueChange={v => setSelectedInstructorId(Number(v))}
              >
                <SelectTrigger className="w-80">
                  <SelectValue placeholder="Bir ogretim uyesi secin..." />
                </SelectTrigger>
                <SelectContent>
                  {(allInstructors ?? []).map(inst => (
                    <SelectItem key={inst.id} value={inst.id.toString()}>
                      {inst.title} {inst.firstName} {inst.lastName} — {inst.departmentName}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          </CardContent>
        </Card>
      )}

      {/* Weekly time grid */}
      {instructorId && (
        <Card>
          <CardHeader className="pb-3">
            <div className="flex items-center justify-between">
              <CardTitle className="flex items-center gap-2 text-base">
                <Clock className="h-4 w-4" /> Haftalik Zaman Tercihleri
              </CardTitle>
              <div className="flex gap-2 flex-wrap">
                {CYCLE_ORDER.map(level => (
                  <span
                    key={level}
                    className={`text-xs px-2 py-0.5 rounded ${LEVEL_COLORS[level]}`}
                  >
                    {LEVEL_LABELS[level]}
                  </span>
                ))}
              </div>
            </div>
          </CardHeader>
          <CardContent>
            {isLoading ? (
              <div className="text-center py-8 text-muted-foreground">Yukleniyor...</div>
            ) : (
              <div className="overflow-x-auto">
                <table className="w-full border-collapse text-sm">
                  <thead>
                    <tr>
                      <th className="w-16 border border-border bg-muted/50 p-2 text-left text-xs font-medium">
                        Saat
                      </th>
                      {DAY_NAMES.map((day, i) => (
                        <th
                          key={day}
                          className="border border-border bg-muted/50 p-2 text-center text-xs font-medium min-w-[100px]"
                        >
                          {day}
                        </th>
                      ))}
                    </tr>
                  </thead>
                  <tbody>
                    {TIME_SLOTS.map(slot => (
                      <tr key={slot.hour}>
                        <td className="border border-border bg-muted/30 p-2 text-xs font-mono text-center whitespace-nowrap">
                          {slot.label}
                        </td>
                        {DAY_VALUES.map((dayVal, di) => {
                          const pref = getCellPref(dayVal, slot.hour);
                          const cellClass = pref ? LEVEL_COLORS[pref.preferenceLevel] : 'bg-background hover:bg-muted/40';
                          return (
                            <td
                              key={dayVal}
                              className={`border border-border p-2 text-center cursor-pointer transition-colors select-none ${cellClass}`}
                              onClick={() => handleCellClick(dayVal, slot)}
                              title={pref ? LEVEL_LABELS[pref.preferenceLevel] : 'Tercih yok — tiklayarak ekleyin'}
                            >
                              {pref && (
                                <span className="text-[10px] leading-none">
                                  {LEVEL_LABELS[pref.preferenceLevel]}
                                </span>
                              )}
                            </td>
                          );
                        })}
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
            <p className="text-xs text-muted-foreground mt-2">
              Hucrelere tiklayarak tercih seviyesini degistirin. Son seviyeden sonra tekrar tiklamak tercihi siler.
            </p>
          </CardContent>
        </Card>
      )}

      {/* Other preferences */}
      {instructorId && (
        <Card>
          <CardHeader className="pb-3">
            <div className="flex items-center justify-between">
              <CardTitle className="flex items-center gap-2 text-base">
                <Building2 className="h-4 w-4" /> Diger Tercihler
              </CardTitle>
              <div className="flex gap-2">
                <Button
                  size="sm"
                  variant="outline"
                  onClick={() => { setDialogType('ClassroomPreference'); setDialogOpen(true); }}
                >
                  <Plus className="h-3 w-3 mr-1" /> Derslik Tercihi
                </Button>
                <Button
                  size="sm"
                  variant="outline"
                  onClick={() => { setDialogType('DayOffPreference'); setDialogOpen(true); }}
                >
                  <Calendar className="h-3 w-3 mr-1" /> Izin Gunu
                </Button>
                <Button
                  size="sm"
                  variant="outline"
                  onClick={() => { setDialogType('DailyLoadPreference'); setDialogOpen(true); }}
                >
                  <Clock className="h-3 w-3 mr-1" /> Gunluk Yuk
                </Button>
              </div>
            </div>
          </CardHeader>
          <CardContent>
            {otherPrefs.length === 0 ? (
              <p className="text-sm text-muted-foreground text-center py-4">
                Henuz baska tercih eklenmemis.
              </p>
            ) : (
              <div className="space-y-2">
                {otherPrefs.map(pref => (
                  <div
                    key={pref.id}
                    className="flex items-center justify-between p-3 rounded-md border bg-muted/20"
                  >
                    <div className="flex items-center gap-3">
                      <Badge variant="outline">{PREF_TYPE_LABELS[pref.preferenceType]}</Badge>
                      <span className={`text-xs px-2 py-0.5 rounded ${LEVEL_COLORS[pref.preferenceLevel]}`}>
                        {LEVEL_LABELS[pref.preferenceLevel]}
                      </span>
                      <span className="text-sm">
                        {pref.preferenceType === 'ClassroomPreference' && (
                          pref.classroomName ?? pref.buildingName ?? '—'
                        )}
                        {pref.preferenceType === 'DayOffPreference' && pref.dayOfWeek != null && (
                          DAY_NAMES[pref.dayOfWeek - 1] ?? `Gun ${pref.dayOfWeek}`
                        )}
                        {pref.preferenceType === 'DailyLoadPreference' && (
                          `Maks. ${pref.maxHours} saat/gun`
                        )}
                      </span>
                      {pref.reason && (
                        <span className="text-xs text-muted-foreground">({pref.reason})</span>
                      )}
                    </div>
                    <div className="flex items-center gap-2">
                      {!pref.isApproved && (
                        <Badge variant="secondary" className="text-xs">Onay Bekliyor</Badge>
                      )}
                      {isAdmin && !pref.isApproved && (
                        <Button
                          size="sm"
                          variant="ghost"
                          className="h-7 text-emerald-600"
                          onClick={() => approveMutation.mutate({ prefId: pref.id, approved: true })}
                        >
                          <Check className="h-3 w-3" />
                        </Button>
                      )}
                      <Button
                        size="sm"
                        variant="ghost"
                        className="h-7 text-destructive"
                        onClick={() => deleteMutation.mutate(pref.id)}
                      >
                        <Trash2 className="h-3 w-3" />
                      </Button>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </CardContent>
        </Card>
      )}

      {/* Pending approvals for admin */}
      {isAdmin && <PendingApprovalsCard termId={termId} />}

      {/* Create dialog */}
      {instructorId && (
        <CreatePreferenceDialog
          open={dialogOpen}
          onOpenChange={setDialogOpen}
          preferenceType={dialogType}
          instructorId={instructorId}
        />
      )}
    </div>
  );
}

// ── Pending Approvals Card ──────────────────────────────────────────

function PendingApprovalsCard({ termId }: { termId?: number }) {
  const { data: summary } = usePreferencesSummary(termId);
  const approveMutation = useApprovePreference();

  const pending = useMemo(
    () => (summary ?? []).filter(p => !p.isApproved),
    [summary],
  );

  if (pending.length === 0) return null;

  return (
    <Card>
      <CardHeader className="pb-3">
        <CardTitle className="flex items-center gap-2 text-base">
          <AlertCircle className="h-4 w-4 text-amber-500" />
          Onay Bekleyen Tercihler ({pending.length})
        </CardTitle>
      </CardHeader>
      <CardContent>
        <div className="space-y-2">
          {pending.map(pref => (
            <div
              key={pref.id}
              className="flex items-center justify-between p-3 rounded-md border"
            >
              <div className="flex items-center gap-3 text-sm">
                <span className="font-medium">{pref.instructorName}</span>
                <Badge variant="outline">{PREF_TYPE_LABELS[pref.preferenceType]}</Badge>
                <span className={`text-xs px-2 py-0.5 rounded ${LEVEL_COLORS[pref.preferenceLevel]}`}>
                  {LEVEL_LABELS[pref.preferenceLevel]}
                </span>
                {pref.reason && <span className="text-xs text-muted-foreground">({pref.reason})</span>}
              </div>
              <div className="flex gap-1">
                <Button
                  size="sm"
                  variant="ghost"
                  className="h-7 text-emerald-600"
                  onClick={() => approveMutation.mutate({ prefId: pref.id, approved: true })}
                >
                  <Check className="h-3 w-3 mr-1" /> Onayla
                </Button>
                <Button
                  size="sm"
                  variant="ghost"
                  className="h-7 text-destructive"
                  onClick={() => approveMutation.mutate({ prefId: pref.id, approved: false })}
                >
                  <X className="h-3 w-3 mr-1" /> Reddet
                </Button>
              </div>
            </div>
          ))}
        </div>
      </CardContent>
    </Card>
  );
}

// ── Create Preference Dialog ────────────────────────────────────────

function CreatePreferenceDialog({
  open,
  onOpenChange,
  preferenceType,
  instructorId,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  preferenceType: 'ClassroomPreference' | 'DailyLoadPreference' | 'DayOffPreference';
  instructorId: number;
}) {
  const createMutation = useCreatePreference();
  const [level, setLevel] = useState<string>('Preferred');
  const [dayOfWeek, setDayOfWeek] = useState<string>('');
  const [buildingName, setBuildingName] = useState('');
  const [maxHours, setMaxHours] = useState('6');
  const [reason, setReason] = useState('');

  const handleSubmit = () => {
    const request: CreateInstructorPreferenceRequest = {
      preferenceType,
      preferenceLevel: level,
      reason: reason || undefined,
    };

    if (preferenceType === 'DayOffPreference') {
      request.dayOfWeek = dayOfWeek ? Number(dayOfWeek) : undefined;
    } else if (preferenceType === 'ClassroomPreference') {
      request.buildingName = buildingName || undefined;
    } else if (preferenceType === 'DailyLoadPreference') {
      request.maxHours = maxHours ? Number(maxHours) : undefined;
    }

    createMutation.mutate(
      { instructorId, request },
      {
        onSuccess: () => {
          onOpenChange(false);
          setReason('');
          setBuildingName('');
        },
      },
    );
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>{PREF_TYPE_LABELS[preferenceType]} Ekle</DialogTitle>
        </DialogHeader>
        <div className="space-y-4 py-2">
          <div>
            <Label>Tercih Seviyesi</Label>
            <Select value={level} onValueChange={setLevel}>
              <SelectTrigger className="mt-1">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {CYCLE_ORDER.map(l => (
                  <SelectItem key={l} value={l}>{LEVEL_LABELS[l]}</SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          {preferenceType === 'DayOffPreference' && (
            <div>
              <Label>Gun</Label>
              <Select value={dayOfWeek} onValueChange={setDayOfWeek}>
                <SelectTrigger className="mt-1">
                  <SelectValue placeholder="Bir gun secin..." />
                </SelectTrigger>
                <SelectContent>
                  {DAY_VALUES.map((val, i) => (
                    <SelectItem key={val} value={val.toString()}>{DAY_NAMES[i]}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          )}

          {preferenceType === 'ClassroomPreference' && (
            <div>
              <Label>Bina Adi</Label>
              <Input
                className="mt-1"
                value={buildingName}
                onChange={e => setBuildingName(e.target.value)}
                placeholder="orn. Muhendislik Fakultesi"
              />
            </div>
          )}

          {preferenceType === 'DailyLoadPreference' && (
            <div>
              <Label>Maksimum Gunluk Saat</Label>
              <Input
                className="mt-1"
                type="number"
                value={maxHours}
                onChange={e => setMaxHours(e.target.value)}
                min={1}
                max={12}
              />
            </div>
          )}

          <div>
            <Label>Aciklama (istege bagli)</Label>
            <Textarea
              className="mt-1"
              value={reason}
              onChange={e => setReason(e.target.value)}
              placeholder="Tercih nedeninizi belirtin..."
              rows={2}
            />
          </div>
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>Iptal</Button>
          <Button onClick={handleSubmit} disabled={createMutation.isPending}>
            {createMutation.isPending ? 'Kaydediliyor...' : 'Kaydet'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
