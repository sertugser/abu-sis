import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Badge } from '@/components/ui/badge';
import {
  Dialog, DialogHeader, DialogTitle, DialogDescription,
} from '@/components/ui/dialog';
import ResizableDialogContent from '@/components/ui/resizable-dialog';
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/components/ui/select';
import { Switch } from '@/components/ui/switch';
import { Textarea } from '@/components/ui/textarea';
import {
  Tooltip, TooltipContent, TooltipProvider, TooltipTrigger,
} from '@/components/ui/tooltip';
import { Tabs, TabsList, TabsTrigger, TabsContent } from '@/components/ui/tabs';
import {
  AlertDialog, AlertDialogAction, AlertDialogCancel, AlertDialogContent,
  AlertDialogDescription, AlertDialogFooter, AlertDialogHeader, AlertDialogTitle,
} from '@/components/ui/alert-dialog';
import { Alert, AlertDescription } from '@/components/ui/alert';
import {
  AlertCircle, Building2, ChevronRight, ExternalLink, Globe, GraduationCap,
  HelpCircle, Info, Loader2, Plus, Save, Trash2, BookOpen, Library,
} from 'lucide-react';
import {
  useElectiveGroupDetail,
  useUpdateElectiveGroup,
  useDeleteElectiveGroup,
  useRemoveCourseFromGroup,
} from '../../hooks/useElectiveGroups';
import { useAdminDepartmentsByStructure } from '../../hooks/useAdminAcademicStructure';
import { useFacultyLookup } from '../../hooks/useAdminLookup';
import { useElectiveSubtypeList } from '../../hooks/useElectiveSubtypes';
import { findSubtypeById, getSubtypeColorByCode } from '../../lib/electiveSubtypeUtils';
import type { ElectiveGroupDetailDto, ElectiveGroupUsageDto, UpdateElectiveGroupRequest } from '../../types/electiveGroup';
import type { ElectiveSubtypeDto } from '../../types/electiveSubtype';
import AddCourseToGroupDialog from './AddCourseToGroupDialog';

// ═══════ Helpers ═══════════════════════════════════════════════════
//
// Detay panelinde kullanıcının "bu havuz nedir, neyi kapsıyor, nerede
// kullanılıyor" sorularını otomatik cevaplayan metin/içerik üreticileri.

/** Subtype scope flag'lerinden okunabilir cümle çıkar. */
function buildSubtypeTooltip(subtype: ElectiveSubtypeDto): string {
  const parts: string[] = [];
  if (subtype.requiresFaculty) parts.push('fakülte zorunlu');
  if (subtype.forbidsFaculty) parts.push('fakülte seçilemez');
  if (subtype.requiresDepartment) parts.push('bölüm zorunlu');
  if (subtype.forbidsDepartment) parts.push('bölüm seçilemez');
  if (parts.length === 0) {
    return `${subtype.name}: kısıtsız tip — fakülte/bölüm seçimi serbest.`;
  }
  return `${subtype.name} — kurallar: ${parts.join(', ')}.`;
}

/** Kapsam (faculty/department) için icon + insan dilinde açıklama. */
function describeScope(facultyName: string | null, departmentName: string | null) {
  if (departmentName && facultyName) {
    return {
      Icon: GraduationCap,
      label: departmentName,
      hint: `Sadece ${facultyName} fakültesi → ${departmentName} bölümü öğrencileri.`,
    } as const;
  }
  if (facultyName) {
    return {
      Icon: Building2,
      label: facultyName,
      hint: `Sadece ${facultyName} fakültesinin tüm bölümleri.`,
    } as const;
  }
  return {
    Icon: Globe,
    label: 'Okul geneli',
    hint: 'Üniversitedeki tüm fakülte ve bölümlerden öğrenciler kullanabilir.',
  } as const;
}

/** "Bu havuz nedir?" — açıklama girilmemiş olsa bile otomatik özet. */
function buildGroupSummary(
  group: ElectiveGroupDetailDto,
  subtype: ElectiveSubtypeDto | null,
): string {
  const subtypeName = subtype?.name ?? 'belirsiz tip';
  const scope = group.departmentName && group.facultyName
    ? `${group.facultyName} → ${group.departmentName} bölümü öğrencilerine`
    : group.facultyName
      ? `${group.facultyName} fakültesi tüm bölüm öğrencilerine`
      : 'üniversitedeki tüm öğrencilere';
  const courseCount = group.courses.length;
  const totalEcts = group.courses.reduce((s, c) => s + c.ects, 0);
  const usageCount = group.usages.length;
  const usageText = usageCount === 0
    ? ' Henüz hiçbir müfredata bağlanmamış — sadece tanım olarak duruyor.'
    : ` ${usageCount} müfredatta toplam ${usageCount} slotta kullanılıyor.`;
  return `"${group.name}" adlı, ${subtypeName} kategorisinde ${scope} açık seçmeli ders havuzu. İçerik: ${courseCount} ders, ${totalEcts} AKTS.${usageText}`;
}

interface Props {
  groupId: number | null;
  onClose: () => void;
}

export default function ElectiveGroupDetailSheet({ groupId, onClose }: Props) {
  const navigate = useNavigate();
  const { data: group, isLoading, error } = useElectiveGroupDetail(groupId);
  const updateMutation = useUpdateElectiveGroup();
  const deleteMutation = useDeleteElectiveGroup();
  const removeMutation = useRemoveCourseFromGroup();

  const [name, setName] = useState('');
  const [code, setCode] = useState('');
  const [description, setDescription] = useState('');
  const [isActive, setIsActive] = useState(true);
  const [facultyId, setFacultyId] = useState<string>('');
  const [departmentId, setDepartmentId] = useState<string>('');
  const [validationError, setValidationError] = useState<string | null>(null);
  const [addDialogOpen, setAddDialogOpen] = useState(false);
  const [courseToRemove, setCourseToRemove] = useState<{ id: number; code: string } | null>(null);
  const [deleteGroupConfirm, setDeleteGroupConfirm] = useState(false);
  const [syncedGroupId, setSyncedGroupId] = useState<number | null>(null);
  const [activeTab, setActiveTab] = useState<'courses' | 'usages'>('courses');

  const { data: faculties } = useFacultyLookup();
  const facultyIdNum = facultyId ? Number(facultyId) : undefined;
  const { data: departments } = useAdminDepartmentsByStructure(facultyIdNum);
  // Faz E: DB-driven subtype lookup (eski hardcoded enum string check yerine)
  const { data: subtypes } = useElectiveSubtypeList(false);
  const groupSubtype = findSubtypeById(subtypes, group?.electiveSubtypeId);

  if (group && syncedGroupId !== group.id) {
    setName(group.name);
    setCode(group.code);
    setDescription(group.description ?? '');
    setIsActive(group.isActive);
    setFacultyId(group.facultyId !== null ? String(group.facultyId) : '');
    setDepartmentId(group.departmentId !== null ? String(group.departmentId) : '');
    setValidationError(null);
    setSyncedGroupId(group.id);
  }

  // Faz E: subtype flag'lerine göre dinamik (eski hardcoded 'UniversityElective' check yerine)
  const facultyDisabled = groupSubtype?.forbidsFaculty ?? false;
  const departmentDisabled = facultyDisabled || !facultyId || (groupSubtype?.forbidsDepartment ?? false);

  function handleFacultyChange(v: string) {
    setFacultyId(v);
    setDepartmentId('');
    setValidationError(null);
  }

  function handleSave() {
    if (!groupId || !group) return;

    const facultyIdNumLocal = facultyId ? Number(facultyId) : null;
    const departmentIdNumLocal = departmentId ? Number(departmentId) : null;

    // Faz E: flag-based dinamik validation (subtype Name dinamik error mesajında)
    if (groupSubtype) {
      if (groupSubtype.forbidsFaculty && facultyIdNumLocal !== null) {
        setValidationError(`${groupSubtype.name} için fakülte seçilemez.`);
        return;
      }
      if (groupSubtype.requiresFaculty && facultyIdNumLocal === null) {
        setValidationError(`${groupSubtype.name} için fakülte zorunlu.`);
        return;
      }
      if (groupSubtype.forbidsDepartment && departmentIdNumLocal !== null) {
        setValidationError(`${groupSubtype.name} için bölüm seçilemez.`);
        return;
      }
      if (groupSubtype.requiresDepartment && departmentIdNumLocal === null) {
        setValidationError(`${groupSubtype.name} için bölüm zorunlu.`);
        return;
      }
    }
    if (departmentIdNumLocal !== null && facultyIdNumLocal === null) {
      setValidationError('Bölüm seçildiğinde fakülte de zorunlu.');
      return;
    }
    setValidationError(null);

    // Clear flags: if user emptied a previously-set field, send explicit clear
    const initialFacultyId = group.facultyId ?? null;
    const initialDepartmentId = group.departmentId ?? null;
    const clearFaculty = initialFacultyId !== null && facultyIdNumLocal === null;
    const clearDepartment = initialDepartmentId !== null && departmentIdNumLocal === null;

    const req: UpdateElectiveGroupRequest = {
      name: name.trim() || undefined,
      code: code.trim() || undefined,
      description: description.trim() || undefined,
      isActive,
      ...(facultyIdNumLocal !== null ? { facultyId: facultyIdNumLocal } : {}),
      ...(departmentIdNumLocal !== null ? { departmentId: departmentIdNumLocal } : {}),
      ...(clearFaculty ? { clearFaculty: true } : {}),
      ...(clearDepartment ? { clearDepartment: true } : {}),
    };
    updateMutation.mutate({ id: groupId, ...req });
  }

  function handleConfirmRemoveCourse() {
    if (!groupId || !courseToRemove) return;
    removeMutation.mutate(
      { groupId, courseId: courseToRemove.id },
      { onSuccess: () => setCourseToRemove(null) },
    );
  }

  function handleDeleteGroup() {
    if (!groupId) return;
    deleteMutation.mutate(groupId, {
      onSuccess: () => {
        setDeleteGroupConfirm(false);
        onClose();
      },
    });
  }

  const existingCourseIds = group?.courses.map(c => c.courseId) ?? [];
  const totalEcts = group?.courses.reduce((sum, c) => sum + c.ects, 0) ?? 0;

  return (
    <>
      <Dialog open={groupId !== null} onOpenChange={open => { if (!open) onClose(); }}>
        <ResizableDialogContent defaultWidth={1280} defaultHeight={620} minWidth={800} minHeight={500} className="flex flex-col p-0 gap-0">
          {/* Header — başlık + ❓ tooltip + tip badge (tooltip'li) + aktif/pasif + özet satırı */}
          <DialogHeader className="px-6 pt-5 pb-4 shrink-0 border-b space-y-2">
            <div className="flex items-center gap-3 flex-wrap">
              <div className="flex items-center gap-1.5">
                <DialogTitle className="text-lg">{group?.name ?? 'Grup Detayı'}</DialogTitle>
                {group && (
                  <TooltipProvider delayDuration={500} skipDelayDuration={0}>
                    <Tooltip>
                      <TooltipTrigger asChild>
                        <button
                          type="button"
                          tabIndex={-1}
                          aria-label="Bu havuz nedir?"
                          className="inline-flex items-center justify-center rounded-full text-muted-foreground hover:text-blue-600 hover:bg-blue-50 transition-colors h-6 w-6"
                        >
                          <HelpCircle className="h-4 w-4" />
                        </button>
                      </TooltipTrigger>
                      <TooltipContent side="bottom" className="max-w-md text-sm leading-relaxed">
                        <p className="font-semibold mb-1">Bu havuz nedir?</p>
                        <p>{buildGroupSummary(group, groupSubtype)}</p>
                      </TooltipContent>
                    </Tooltip>
                  </TooltipProvider>
                )}
              </div>
              {group && groupSubtype && (
                <TooltipProvider delayDuration={500}>
                  <Tooltip>
                    <TooltipTrigger asChild>
                      <Badge
                        variant="outline"
                        className={`text-xs font-medium px-2.5 py-0.5 cursor-help inline-flex items-center gap-1 ${getSubtypeColorByCode(groupSubtype.code)}`}
                      >
                        {groupSubtype.name}
                        <Info className="h-3 w-3 opacity-60" />
                      </Badge>
                    </TooltipTrigger>
                    <TooltipContent side="bottom" className="max-w-xs text-xs">
                      {buildSubtypeTooltip(groupSubtype)}
                    </TooltipContent>
                  </Tooltip>
                </TooltipProvider>
              )}
              {group && (
                <Badge variant={group.isActive ? 'default' : 'secondary'} className={group.isActive ? 'bg-emerald-50 text-emerald-700 border-emerald-200' : 'bg-red-50 text-red-600 border-red-200'}>
                  {group.isActive ? 'Aktif' : 'Pasif'}
                </Badge>
              )}
            </div>
            <DialogDescription asChild>
              <div className="flex flex-wrap items-center gap-x-3 gap-y-1 text-sm">
                <span className="font-mono text-xs">{group?.code}</span>
                {group && (
                  <>
                    <span className="text-muted-foreground">·</span>
                    <span>{group.courses.length} ders</span>
                    <span className="text-muted-foreground">·</span>
                    <span>{totalEcts} AKTS</span>
                    <span className="text-muted-foreground">·</span>
                    {(() => {
                      const { Icon, label, hint } = describeScope(group.facultyName, group.departmentName);
                      return (
                        <TooltipProvider delayDuration={500}>
                          <Tooltip>
                            <TooltipTrigger asChild>
                              <span className="inline-flex items-center gap-1.5 cursor-help text-foreground">
                                <Icon className="h-3.5 w-3.5 text-muted-foreground" />
                                <span>{label}</span>
                                <span className="text-muted-foreground italic text-xs">(kapsam)</span>
                              </span>
                            </TooltipTrigger>
                            <TooltipContent side="bottom" className="max-w-xs text-xs">
                              {hint}
                            </TooltipContent>
                          </Tooltip>
                        </TooltipProvider>
                      );
                    })()}
                  </>
                )}
              </div>
            </DialogDescription>
          </DialogHeader>


          {isLoading && (
            <div className="flex-1 flex items-center justify-center">
              <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
            </div>
          )}

          {error && (
            <div className="px-6 pt-5">
              <Alert variant="destructive">
                <AlertCircle className="h-4 w-4" />
                <AlertDescription>Grup bilgileri yüklenemedi.</AlertDescription>
              </Alert>
            </div>
          )}

          {group && (
            <div className="flex-1 overflow-hidden px-6 py-5 min-h-0">
              <div className="grid grid-cols-[360px_1fr] gap-6 h-full min-h-0">
                {/* Left: Form — her alan space-y-2 ile içeride nefes,
                     form alanları arası space-y-5, "Grubu Sil" alta yapışır. */}
                <div className="space-y-5 border-r pr-6 flex flex-col overflow-y-auto min-h-0 pl-1">
                  <div className="space-y-2">
                    <Label htmlFor="eg-name" className="text-sm font-medium text-muted-foreground">Grup Adı</Label>
                    <Input
                      id="eg-name"
                      value={name}
                      onChange={e => setName(e.target.value)}
                      className="h-10 text-sm"
                    />
                  </div>
                  <div className="space-y-2">
                    <Label htmlFor="eg-code" className="text-sm font-medium text-muted-foreground">Kod</Label>
                    <Input
                      id="eg-code"
                      value={code}
                      onChange={e => setCode(e.target.value.toUpperCase())}
                      className="h-10 text-sm"
                    />
                  </div>
                  <div className="space-y-2">
                    <Label htmlFor="eg-desc" className="text-sm font-medium text-muted-foreground">
                      Açıklama
                      {description.trim().length === 0 && (
                        <span className="ml-1 text-amber-600 italic font-normal text-xs">
                          — gelecekte unutmamak için yazın
                        </span>
                      )}
                    </Label>
                    <Textarea
                      id="eg-desc"
                      value={description}
                      onChange={e => setDescription(e.target.value)}
                      rows={3}
                      placeholder="Bu havuzun amacı ve hangi öğrenci grubuna hitap ettiğini kısaca yazın..."
                      className={`text-sm resize-none ${description.trim().length === 0 ? 'border-amber-200 bg-amber-50/30' : ''}`}
                    />
                  </div>

                  {!facultyDisabled && (
                    <div className="space-y-2">
                      <Label className="text-sm font-medium text-muted-foreground">
                        Fakülte{groupSubtype?.requiresFaculty ? '' : ' (opsiyonel)'}
                      </Label>
                      <Select
                        value={facultyId || '__none__'}
                        onValueChange={v => handleFacultyChange(v === '__none__' ? '' : v)}
                      >
                        <SelectTrigger className="h-10 text-sm">
                          <SelectValue placeholder="Fakülte seçin..." />
                        </SelectTrigger>
                        <SelectContent>
                          {!groupSubtype?.requiresFaculty && (
                            <SelectItem value="__none__">— Seçilmedi —</SelectItem>
                          )}
                          {faculties?.map(f => (
                            <SelectItem key={f.id} value={String(f.id)}>{f.name}</SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    </div>
                  )}

                  {!departmentDisabled && (
                    <div className="space-y-2">
                      <Label className="text-sm font-medium text-muted-foreground">Bölüm (opsiyonel)</Label>
                      <Select
                        value={departmentId || '__all__'}
                        onValueChange={v => setDepartmentId(v === '__all__' ? '' : v)}
                      >
                        <SelectTrigger className="h-10 text-sm">
                          <SelectValue placeholder="(Tüm fakülte)" />
                        </SelectTrigger>
                        <SelectContent>
                          <SelectItem value="__all__">(Tüm fakülte)</SelectItem>
                          {(departments ?? []).map(d => (
                            <SelectItem key={d.id} value={String(d.id)}>{d.name}</SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                      <p className="text-xs text-muted-foreground">
                        Boş bırakırsanız fakültenin tüm bölümleri kullanabilir.
                      </p>
                    </div>
                  )}

                  <div className="flex items-center gap-3 pt-1">
                    <Switch id="eg-active" checked={isActive} onCheckedChange={setIsActive} />
                    <Label htmlFor="eg-active" className="text-sm font-medium cursor-pointer">
                      {isActive ? 'Aktif' : 'Pasif'}
                    </Label>
                  </div>

                  <Button
                    onClick={handleSave}
                    disabled={updateMutation.isPending}
                    className="w-full h-10"
                  >
                    {updateMutation.isPending
                      ? <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                      : <Save className="mr-2 h-4 w-4" />}
                    Kaydet
                  </Button>
                  {validationError && <p className="text-xs text-destructive text-center">{validationError}</p>}
                  {updateMutation.isSuccess && !validationError && <p className="text-xs text-emerald-600 text-center">Kaydedildi.</p>}
                  {updateMutation.isError && !validationError && <p className="text-xs text-destructive text-center">Hata oluştu.</p>}

                  {/* Grubu Sil — alta yapışır, üst kısmıyla arası dengede */}
                  <div className="mt-auto pt-3 border-t">
                    <Button
                      variant="ghost"
                      size="sm"
                      className="w-full text-sm text-destructive hover:text-destructive hover:bg-destructive/10"
                      onClick={() => setDeleteGroupConfirm(true)}
                    >
                      <Trash2 className="h-3.5 w-3.5 mr-1.5" />
                      Grubu Sil
                    </Button>
                  </div>
                </div>

                {/* Right: Tek kart — Tabs + buton üst şeritte, tablo altta.
                     h-full + flex chain ile panel büyüdükçe tablo da uzar. */}
                <div className="border rounded-lg overflow-hidden flex flex-col h-full min-h-0">
                  <Tabs
                    value={activeTab}
                    onValueChange={v => setActiveTab(v as 'courses' | 'usages')}
                    className="w-full flex flex-col h-full min-h-0"
                  >
                    {/* Üst şerit — sekmeler solda, aktif tab'a göre aksiyon sağda. shrink-0 ile resize'da büyümez. */}
                    <div className="flex items-center justify-between gap-3 border-b bg-muted/30 px-3 py-2 shrink-0">
                      <TabsList className="bg-transparent p-0 h-auto gap-1 rounded-none">
                        <TabsTrigger
                          value="courses"
                          className="gap-2 data-[state=active]:bg-card data-[state=active]:shadow-sm h-9 px-3"
                        >
                          <BookOpen className="h-3.5 w-3.5" />
                          Dersler
                          <Badge variant="secondary" className="text-xs">{group.courses.length}</Badge>
                        </TabsTrigger>
                        <TabsTrigger
                          value="usages"
                          className="gap-2 data-[state=active]:bg-card data-[state=active]:shadow-sm h-9 px-3"
                        >
                          <Library className="h-3.5 w-3.5" />
                          Bağlı Müfredatlar
                          <Badge variant="secondary" className="text-xs">{group.usages.length}</Badge>
                        </TabsTrigger>
                      </TabsList>
                      {activeTab === 'courses' && (
                        <Button size="sm" variant="outline" className="h-9 text-sm" onClick={() => setAddDialogOpen(true)}>
                          <Plus className="h-3.5 w-3.5 mr-1.5" />
                          Ders Ekle
                        </Button>
                      )}
                    </div>

                    {/* ── Dersler tab içeriği — flex chain ile tablo panel büyüyünce uzar ── */}
                    <TabsContent value="courses" className="m-0 mt-0 flex-1 flex flex-col overflow-hidden min-h-0">
                      {group.courses.length === 0 ? (
                        <div className="flex-1 flex flex-col items-center justify-center text-center px-4">
                          <BookOpen className="h-8 w-8 text-muted-foreground/30 mx-auto mb-2" />
                          <p className="text-sm text-muted-foreground">Henüz ders eklenmedi.</p>
                          <p className="text-xs text-muted-foreground mt-1">Yukarıdaki "Ders Ekle" butonunu kullanın.</p>
                        </div>
                      ) : (
                        <>
                          <div className="flex-1 min-h-0 overflow-auto">
                            <table className="w-full">
                              <thead className="bg-muted/20 sticky top-0 border-b">
                                <tr>
                                  <th className="text-left px-3 py-2.5 text-sm font-semibold text-muted-foreground w-28">Kod</th>
                                  <th className="text-left px-3 py-2.5 text-sm font-semibold text-muted-foreground">Ders Adı</th>
                                  <th className="text-center px-3 py-2.5 text-sm font-semibold text-muted-foreground w-16">AKTS</th>
                                  <th className="w-10" />
                                </tr>
                              </thead>
                              <tbody className="divide-y divide-border">
                                {group.courses.map(c => (
                                  <tr key={c.courseId} className="hover:bg-muted/10 transition-colors">
                                    <td className="px-3 py-2.5 font-mono text-sm font-medium">{c.courseCode}</td>
                                    <td className="px-3 py-2.5 text-sm">{c.courseName}</td>
                                    <td className="px-3 py-2.5 text-center text-sm">{c.ects}</td>
                                    <td className="px-1 py-2.5">
                                      <Button
                                        size="icon"
                                        variant="ghost"
                                        className="h-6 w-6 text-muted-foreground hover:text-destructive"
                                        onClick={() => setCourseToRemove({ id: c.courseId, code: c.courseCode })}
                                        disabled={removeMutation.isPending}
                                        aria-label={`${c.courseCode} dersini gruptan çıkar`}
                                      >
                                        <Trash2 className="h-3 w-3" />
                                      </Button>
                                    </td>
                                  </tr>
                                ))}
                              </tbody>
                            </table>
                          </div>
                          <div className="px-3 py-2 border-t bg-muted/20 flex items-center justify-between shrink-0">
                            <span className="text-xs text-muted-foreground">
                              {group.courses.length} ders
                            </span>
                            <span className="text-xs text-muted-foreground">
                              Toplam <span className="font-medium text-foreground">{totalEcts}</span> AKTS
                            </span>
                          </div>
                        </>
                      )}
                    </TabsContent>

                    {/* ── Bağlı Müfredatlar tab içeriği — aynı flex chain ── */}
                    <TabsContent value="usages" className="m-0 mt-0 flex-1 flex flex-col overflow-hidden min-h-0">
                      {group.usages.length === 0 ? (
                        <div className="flex-1 flex flex-col items-center justify-center text-center px-4">
                          <Library className="h-8 w-8 text-muted-foreground/30 mx-auto mb-2" />
                          <p className="text-sm font-medium text-foreground">
                            Bu havuz henüz hiçbir müfredata bağlanmamış.
                          </p>
                          <p className="text-xs text-muted-foreground mt-1.5 max-w-md mx-auto">
                            Sadece tanım olarak duruyor. Bir müfredata bağlamak için
                            <span className="font-medium"> Müfredat İşlemleri </span>
                            sayfasından ilgili müfredatı seçip <span className="font-medium">"+ Grup Ekle"</span>
                            butonuyla bu havuzu slot olarak ekleyin.
                          </p>
                        </div>
                      ) : (
                        <>
                          <div className="flex-1 min-h-0 overflow-auto">
                            <table className="w-full">
                              <thead className="bg-muted/20 sticky top-0 border-b">
                                <tr>
                                  <th className="text-left px-3 py-2.5 text-sm font-semibold text-muted-foreground">Müfredat</th>
                                  <th className="text-left px-3 py-2.5 text-sm font-semibold text-muted-foreground w-32">Slot Adı</th>
                                  <th className="text-center px-3 py-2.5 text-sm font-semibold text-muted-foreground w-24">Yarıyıl</th>
                                  <th className="text-center px-3 py-2.5 text-sm font-semibold text-muted-foreground w-32">Gerekli</th>
                                  <th className="w-10" />
                                </tr>
                              </thead>
                              <tbody className="divide-y divide-border">
                                {group.usages.map((u: ElectiveGroupUsageDto) => (
                                  <tr
                                    key={u.poolId}
                                    className="cursor-pointer hover:bg-primary/5 transition-colors"
                                    onClick={() => navigate(`/mufredat-islemleri?curriculum=${u.curriculumId}`)}
                                    title="Müfredat İşlemleri sayfasına git"
                                  >
                                    <td className="px-3 py-2.5 text-sm">
                                      <div className="font-medium">{u.curriculumName || u.curriculumVersion}</div>
                                      <div className="text-xs text-muted-foreground mt-0.5">
                                        {u.programName} · v{u.curriculumVersion}
                                      </div>
                                    </td>
                                    <td className="px-3 py-2.5 font-mono text-sm font-medium text-violet-700">
                                      {u.slotName}
                                    </td>
                                    <td className="px-3 py-2.5 text-center text-sm text-muted-foreground">
                                      {u.suggestedSemesterStart === u.suggestedSemesterEnd
                                        ? `${u.suggestedSemesterStart}.`
                                        : `${u.suggestedSemesterStart}-${u.suggestedSemesterEnd}.`}
                                    </td>
                                    <td className="px-3 py-2.5 text-center text-sm">
                                      <span className="font-medium">{u.requiredCourseCount}</span>
                                      <span className="text-muted-foreground"> ders · </span>
                                      <span className="font-medium">{u.requiredEcts}</span>
                                      <span className="text-muted-foreground"> AKTS</span>
                                    </td>
                                    <td className="px-1 py-2.5">
                                      <ChevronRight className="h-4 w-4 text-muted-foreground" />
                                    </td>
                                  </tr>
                                ))}
                              </tbody>
                            </table>
                          </div>
                          <div className="px-3 py-2 border-t bg-muted/20 flex items-center justify-between text-xs text-muted-foreground shrink-0">
                            <span>{group.usages.length} müfredatta kullanılıyor</span>
                            <span className="inline-flex items-center gap-1">
                              <ExternalLink className="h-3 w-3" />
                              Satıra tıklayınca müfredata git
                            </span>
                          </div>
                        </>
                      )}
                    </TabsContent>
                  </Tabs>
                </div>
              </div>
            </div>
          )}
        </ResizableDialogContent>
      </Dialog>

      {groupId !== null && (
        <AddCourseToGroupDialog
          groupId={groupId}
          existingCourseIds={existingCourseIds}
          open={addDialogOpen}
          onOpenChange={setAddDialogOpen}
        />
      )}

      {/* Remove Course Confirmation */}
      <AlertDialog open={courseToRemove !== null} onOpenChange={open => { if (!open) setCourseToRemove(null); }}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Dersi gruptan çıkar</AlertDialogTitle>
            <AlertDialogDescription>
              <strong>{courseToRemove?.code}</strong> kodlu dersi bu gruptan çıkarmak istediğinize emin misiniz?
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>İptal</AlertDialogCancel>
            <AlertDialogAction
              onClick={handleConfirmRemoveCourse}
              disabled={removeMutation.isPending}
              className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
            >
              {removeMutation.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Çıkar
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      {/* Delete Group Confirmation */}
      <AlertDialog open={deleteGroupConfirm} onOpenChange={setDeleteGroupConfirm}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Grubu sil</AlertDialogTitle>
            <AlertDialogDescription>
              <strong>{group?.name}</strong> grubunu silmek istediğinize emin misiniz? Bu işlem grubu pasif hale getirecektir.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>İptal</AlertDialogCancel>
            <AlertDialogAction
              onClick={handleDeleteGroup}
              disabled={deleteMutation.isPending}
              className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
            >
              {deleteMutation.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Sil
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </>
  );
}
