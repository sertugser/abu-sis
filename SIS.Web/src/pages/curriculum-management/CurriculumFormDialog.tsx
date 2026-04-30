import { useEffect, useState } from 'react';
import { Loader2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/components/ui/select';
import {
  Dialog, DialogFooter, DialogHeader, DialogTitle,
} from '@/components/ui/dialog';
import ResizableDialogContent from '@/components/ui/resizable-dialog';
import {
  useAdminDepartmentsByStructure,
  useAdminFaculties,
  useAdminProgramsByStructure,
} from '../../hooks/useAdminAcademicStructure';
import { useCurriculumList } from '../../hooks/useCurriculumManagement';
import type { CurriculumListDto } from '../../types/curriculumManagement';

export interface CurriculumFormValues {
  version: string;
  name?: string;
  shortName?: string;
  englishName?: string;
  startDate?: string;
  endDate?: string;
  decisionDate?: string;
  decisionNo?: string;
  decisionNote?: string;
  gradingSystem?: string;
  internshipDuration?: number;
  description?: string;
}

export type CurriculumFormSubmitPayload =
  | { mode: 'edit'; id: number; isActive: boolean; values: CurriculumFormValues }
  | { mode: 'create-empty'; programId: number; values: CurriculumFormValues }
  | { mode: 'create-copy'; sourceId: number; values: CurriculumFormValues };

interface Props {
  open: boolean;
  editingCurriculum?: CurriculumListDto;
  defaultFacultyId?: number;
  defaultDepartmentId?: number;
  defaultProgramId?: number;
  onClose: () => void;
  onSubmit: (payload: CurriculumFormSubmitPayload) => void;
  isSubmitting: boolean;
}

const DEFAULT_GRADING_SYSTEM = 'Sınıf Geçme';
const DEFAULT_MUFREDAT_TIPI = 'Programa Özel';

export default function CurriculumFormDialog({
  open,
  editingCurriculum,
  defaultFacultyId,
  defaultDepartmentId,
  defaultProgramId,
  onClose,
  onSubmit,
  isSubmitting,
}: Props) {
  const isEditMode = !!editingCurriculum;

  // ── Form alanları ──
  const [version, setVersion] = useState('');
  const [name, setName] = useState('');
  const [shortName, setShortName] = useState('');
  const [englishName, setEnglishName] = useState('');
  const [startDate, setStartDate] = useState('');
  const [endDate, setEndDate] = useState('');
  const [decisionDate, setDecisionDate] = useState('');
  const [decisionNo, setDecisionNo] = useState('');
  const [decisionNote, setDecisionNote] = useState('');
  const [gradingSystem, setGradingSystem] = useState(DEFAULT_GRADING_SYSTEM);
  const [internshipDuration, setInternshipDuration] = useState('');
  const [description, setDescription] = useState('');

  // UI-only alanlar — backend'e gitmez
  const [status, setStatus] = useState<'active' | 'inactive'>('active');
  const [mufredatTipi, setMufredatTipi] = useState<string>(DEFAULT_MUFREDAT_TIPI);
  const [includeFormation, setIncludeFormation] = useState(false);
  const [hideFromPackage, setHideFromPackage] = useState(false);

  // Create mode toggle: Boş oluştur | Mevcut versiyondan kopyala
  const [createMode, setCreateMode] = useState<'empty' | 'copy'>('empty');
  const [copySourceId, setCopySourceId] = useState<string>('');

  // Cascade: Fakülte → Bölüm → Program (sadece create mode'da)
  const [facultyId, setFacultyId] = useState<number | undefined>();
  const [departmentId, setDepartmentId] = useState<number | undefined>();
  const [programId, setProgramId] = useState<number | undefined>();

  // ── Cascade query'leri ──
  const { data: faculties, isLoading: loadingFaculties } = useAdminFaculties();
  const { data: departments, isLoading: loadingDepts } = useAdminDepartmentsByStructure(facultyId);
  const { data: programs, isLoading: loadingPrograms } = useAdminProgramsByStructure(departmentId);
  const { data: curriculaForCopy } = useCurriculumList({ programId });

  // ── Dialog her açıldığında initialize ──
  useEffect(() => {
    if (!open) return;

    if (editingCurriculum) {
      // Edit mode — mevcut müfredattan değerleri doldur
      setVersion(editingCurriculum.version);
      setName(editingCurriculum.name ?? '');
      setShortName('');
      setEnglishName('');
      setStartDate(editingCurriculum.startDate ? editingCurriculum.startDate.slice(0, 10) : '');
      setEndDate(editingCurriculum.endDate ? editingCurriculum.endDate.slice(0, 10) : '');
      setDecisionDate('');
      setDecisionNo('');
      setDecisionNote('');
      setGradingSystem(DEFAULT_GRADING_SYSTEM);
      setInternshipDuration('');
      setDescription('');
      setStatus(editingCurriculum.isActive ? 'active' : 'inactive');
      setMufredatTipi(DEFAULT_MUFREDAT_TIPI);
      setIncludeFormation(false);
      setHideFromPackage(false);
      setCreateMode('empty');
      setCopySourceId('');
      // Edit mode'da Fakülte/Bölüm/Program readonly — cascade query'leri yapmamak için boş bırak
      setFacultyId(undefined);
      setDepartmentId(undefined);
      setProgramId(editingCurriculum.programId);
    } else {
      // Create mode — boş form + filtre barından default'ları al
      setVersion('');
      setName('');
      setShortName('');
      setEnglishName('');
      setStartDate('');
      setEndDate('');
      setDecisionDate('');
      setDecisionNo('');
      setDecisionNote('');
      setGradingSystem(DEFAULT_GRADING_SYSTEM);
      setInternshipDuration('');
      setDescription('');
      setStatus('active');
      setMufredatTipi(DEFAULT_MUFREDAT_TIPI);
      setIncludeFormation(false);
      setHideFromPackage(false);
      setCreateMode('empty');
      setCopySourceId('');
      setFacultyId(defaultFacultyId);
      setDepartmentId(defaultDepartmentId);
      setProgramId(defaultProgramId);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, editingCurriculum?.id]);

  function handleFacultyChange(val: string) {
    setFacultyId(Number(val));
    setDepartmentId(undefined);
    setProgramId(undefined);
  }
  function handleDepartmentChange(val: string) {
    setDepartmentId(Number(val));
    setProgramId(undefined);
  }
  function handleProgramChange(val: string) {
    setProgramId(Number(val));
  }

  function buildValues(): CurriculumFormValues {
    return {
      version: version.trim(),
      name: name.trim() || undefined,
      shortName: shortName.trim() || undefined,
      englishName: englishName.trim() || undefined,
      startDate: startDate || undefined,
      endDate: endDate || undefined,
      decisionDate: decisionDate.trim() || undefined,
      decisionNo: decisionNo.trim() || undefined,
      decisionNote: decisionNote.trim() || undefined,
      gradingSystem: gradingSystem.trim() || undefined,
      internshipDuration: internshipDuration ? Number(internshipDuration) : undefined,
      description: description.trim() || undefined,
    };
  }

  function handleSubmit() {
    if (!version.trim()) return;

    if (isEditMode && editingCurriculum) {
      onSubmit({
        mode: 'edit',
        id: editingCurriculum.id,
        isActive: status === 'active',
        values: buildValues(),
      });
      return;
    }

    if (!programId) return;

    if (createMode === 'copy' && copySourceId) {
      onSubmit({
        mode: 'create-copy',
        sourceId: Number(copySourceId),
        values: buildValues(),
      });
    } else {
      onSubmit({
        mode: 'create-empty',
        programId,
        values: buildValues(),
      });
    }
  }

  const submitDisabled =
    isSubmitting
    || !version.trim()
    || (!isEditMode && !programId)
    || (!isEditMode && createMode === 'copy' && !copySourceId);

  const submitLabel = isEditMode
    ? 'Kaydet'
    : createMode === 'copy' ? 'Kopyala' : 'Oluştur';

  return (
    <Dialog open={open} onOpenChange={(o) => { if (!o) onClose(); }}>
      <ResizableDialogContent defaultWidth={750} minWidth={500} minHeight={400} className="flex flex-col p-0 gap-0">
        <DialogHeader className="px-6 pt-6 pb-3 shrink-0">
          <DialogTitle>
            {isEditMode ? 'Müfredat Düzenleniyor' : 'Müfredat (Yeni Ekleniyor)'}
          </DialogTitle>
        </DialogHeader>
        <div className="flex-1 overflow-y-auto px-6 pb-2 space-y-4">
          {/* Yıl + Durumu */}
          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1">
              <Label className="text-xs">Yıl *</Label>
              <Input value={version} onChange={e => setVersion(e.target.value)} placeholder="2024" />
            </div>
            <div className="space-y-1">
              <Label className="text-xs">Durumu</Label>
              <Select value={status} onValueChange={(val) => setStatus(val as 'active' | 'inactive')}>
                <SelectTrigger className="h-9 text-sm">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="active">Aktif</SelectItem>
                  <SelectItem value="inactive">Pasif</SelectItem>
                </SelectContent>
              </Select>
            </div>
          </div>

          {/* Fakülte / Bölüm / Program — Edit mode'da readonly info bar, Create mode'da seçilebilir dropdown'lar */}
          {isEditMode && editingCurriculum && (
            <div className="rounded-md border bg-muted/50 p-3 text-xs space-y-1">
              <div className="text-muted-foreground font-medium uppercase tracking-wide text-[10px]">
                Müfredat Bilgisi (değiştirilemez)
              </div>
              <div><span className="text-muted-foreground">Fakülte:</span> <span className="font-medium">{editingCurriculum.facultyName}</span></div>
              <div><span className="text-muted-foreground">Bölüm:</span> <span className="font-medium">{editingCurriculum.departmentName}</span></div>
              <div><span className="text-muted-foreground">Program:</span> <span className="font-medium">{editingCurriculum.programName}</span></div>
            </div>
          )}
          {!isEditMode && (
            <>
              <div className="space-y-1">
                <Label className="text-xs">Fakülte *</Label>
                <Select value={facultyId?.toString() ?? ''} onValueChange={handleFacultyChange}>
                  <SelectTrigger className="h-9 text-sm">
                    {loadingFaculties
                      ? <Loader2 className="h-4 w-4 animate-spin" />
                      : <SelectValue placeholder="Fakülte seçin" />}
                  </SelectTrigger>
                  <SelectContent>
                    {(faculties ?? []).map(f => (
                      <SelectItem key={f.id} value={f.id.toString()}>{f.name}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-1">
                <Label className="text-xs">Bölüm *</Label>
                <Select value={departmentId?.toString() ?? ''} onValueChange={handleDepartmentChange} disabled={!facultyId}>
                  <SelectTrigger className="h-9 text-sm">
                    {loadingDepts
                      ? <Loader2 className="h-4 w-4 animate-spin" />
                      : <SelectValue placeholder="Bölüm seçin" />}
                  </SelectTrigger>
                  <SelectContent>
                    {(departments ?? []).map(d => (
                      <SelectItem key={d.id} value={d.id.toString()}>{d.name}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-1">
                <Label className="text-xs">Program *</Label>
                <Select value={programId?.toString() ?? ''} onValueChange={handleProgramChange} disabled={!departmentId}>
                  <SelectTrigger className="h-9 text-sm">
                    {loadingPrograms
                      ? <Loader2 className="h-4 w-4 animate-spin" />
                      : <SelectValue placeholder="Program seçin" />}
                  </SelectTrigger>
                  <SelectContent>
                    {(programs ?? []).map(p => (
                      <SelectItem key={p.id} value={p.id.toString()}>{p.name}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            </>
          )}

          {/* Müfredat Tipi */}
          <div className="space-y-1">
            <Label className="text-xs">Müfredat Tipi</Label>
            <Select value={mufredatTipi} onValueChange={setMufredatTipi}>
              <SelectTrigger className="h-9 text-sm">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="Programa Özel">Programa Özel</SelectItem>
                <SelectItem value="Ortak Müfredat">Ortak Müfredat</SelectItem>
              </SelectContent>
            </Select>
          </div>

          {/* Müfredat Ad */}
          <div className="space-y-1">
            <Label className="text-xs">Müfredat Ad</Label>
            <Input value={name} onChange={e => setName(e.target.value)} placeholder="Bilgisayar Müh. 2024 Müfredatı" />
          </div>

          {/* Kısa Ad */}
          <div className="space-y-1">
            <Label className="text-xs">Kısa Ad</Label>
            <Input value={shortName} onChange={e => setShortName(e.target.value)} placeholder="BM-2024" />
          </div>

          {/* İngilizce Ad */}
          <div className="space-y-1">
            <Label className="text-xs">İngilizce Ad</Label>
            <Input value={englishName} onChange={e => setEnglishName(e.target.value)} placeholder="Computer Eng. 2024 Curriculum" />
          </div>

          {/* Başlangıç / Bitiş Tarihi */}
          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1">
              <Label className="text-xs">Başlangıç Tarihi</Label>
              <Input type="date" value={startDate} onChange={e => setStartDate(e.target.value)} />
            </div>
            <div className="space-y-1">
              <Label className="text-xs">Bitiş Tarihi</Label>
              <Input type="date" value={endDate} onChange={e => setEndDate(e.target.value)} />
            </div>
          </div>

          {/* Karar Tarih / Karar No */}
          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1">
              <Label className="text-xs">Karar Tarih</Label>
              <Input value={decisionDate} onChange={e => setDecisionDate(e.target.value)} placeholder="01.01.2024" />
            </div>
            <div className="space-y-1">
              <Label className="text-xs">Karar No</Label>
              <Input value={decisionNo} onChange={e => setDecisionNo(e.target.value)} placeholder="2024/01" />
            </div>
          </div>

          {/* Karar Açıklama */}
          <div className="space-y-1">
            <Label className="text-xs">Karar Açıklama</Label>
            <Input value={decisionNote} onChange={e => setDecisionNote(e.target.value)} placeholder="Senato kararı ile kabul edilmiştir" />
          </div>

          {/* Geçme Sistemi / Staj Süresi */}
          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1">
              <Label className="text-xs">Geçme Sistemi</Label>
              <Select value={gradingSystem} onValueChange={setGradingSystem}>
                <SelectTrigger className="h-9 text-sm">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="Sınıf Geçme">Sınıf Geçme</SelectItem>
                  <SelectItem value="Ders Geçme">Ders Geçme</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-1">
              <Label className="text-xs">Staj Süresi (gün)</Label>
              <Input
                type="number"
                min={0}
                value={internshipDuration}
                onChange={e => setInternshipDuration(e.target.value)}
                placeholder="40"
              />
            </div>
          </div>

          {/* Açıklama */}
          <div className="space-y-1">
            <Label className="text-xs">Açıklama</Label>
            <Textarea value={description} onChange={e => setDescription(e.target.value)} placeholder="Genel açıklama..." rows={2} />
          </div>

          {/* Checkbox'lar — UI-only, backend'e gitmez */}
          <div className="space-y-2 pt-1">
            <label className="flex items-start gap-2 text-sm cursor-pointer">
              <input
                type="checkbox"
                checked={includeFormation}
                onChange={e => setIncludeFormation(e.target.checked)}
                className="mt-0.5 h-4 w-4 rounded border-input accent-primary cursor-pointer"
              />
              <span>Mezuniyette Formasyon Derslerini Toplam Kredi/AKTS ve Seçmeli/Zorunlu Ders Sayılarına Dahil Et</span>
            </label>
            <label className="flex items-start gap-2 text-sm cursor-pointer">
              <input
                type="checkbox"
                checked={hideFromPackage}
                onChange={e => setHideFromPackage(e.target.checked)}
                className="mt-0.5 h-4 w-4 rounded border-input accent-primary cursor-pointer"
              />
              <span>Bilgi Paketinde Gösterme</span>
            </label>
          </div>

          {/* ── Kopyalama (en altta) — sadece Create mode'da, seçili programa ait müfredat varsa ── */}
          {!isEditMode && (curriculaForCopy ?? []).length > 0 && (
            <fieldset className="space-y-3">
              <legend className="text-xs font-semibold text-muted-foreground uppercase tracking-wide">Kopyalama</legend>
              <div className="flex items-center gap-1 border rounded-md p-1">
                <button
                  onClick={() => { setCreateMode('empty'); setCopySourceId(''); }}
                  className={`flex-1 text-xs px-2.5 py-1.5 rounded transition-colors ${
                    createMode === 'empty'
                      ? 'bg-primary text-primary-foreground'
                      : 'text-muted-foreground hover:text-foreground hover:bg-muted'
                  }`}
                >
                  Boş oluştur
                </button>
                <button
                  onClick={() => setCreateMode('copy')}
                  className={`flex-1 text-xs px-2.5 py-1.5 rounded transition-colors ${
                    createMode === 'copy'
                      ? 'bg-primary text-primary-foreground'
                      : 'text-muted-foreground hover:text-foreground hover:bg-muted'
                  }`}
                >
                  Mevcut versiyondan kopyala
                </button>
              </div>
              {createMode === 'copy' && (
                <Select value={copySourceId} onValueChange={setCopySourceId}>
                  <SelectTrigger className="h-8 text-sm">
                    <SelectValue placeholder="Kaynak versiyon seçin" />
                  </SelectTrigger>
                  <SelectContent>
                    {(curriculaForCopy ?? []).map(c => (
                      <SelectItem key={c.id} value={c.id.toString()}>
                        {c.version} ({c.courseCount} ders, {c.totalEcts} AKTS)
                        {c.isActive ? ' — Aktif' : ''}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              )}
            </fieldset>
          )}
        </div>
        <DialogFooter className="shrink-0 px-6 py-4 border-t gap-2">
          <Button variant="outline" onClick={onClose}>
            İptal
          </Button>
          <Button onClick={handleSubmit} disabled={submitDisabled}>
            {isSubmitting && <Loader2 className="h-4 w-4 animate-spin mr-2" />}
            {submitLabel}
          </Button>
        </DialogFooter>
      </ResizableDialogContent>
    </Dialog>
  );
}
