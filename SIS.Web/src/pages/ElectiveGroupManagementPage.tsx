import { useState } from 'react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { AlertCircle, FolderOpen, Loader2, Plus, RotateCcw } from 'lucide-react';
import {
  useElectiveGroupList,
  useCreateElectiveGroup,
} from '../hooks/useElectiveGroups';
import { useElectiveSubtypeList } from '../hooks/useElectiveSubtypes';
import { useAdminFaculties, useAdminDepartmentsByStructure } from '../hooks/useAdminAcademicStructure';
import type {
  ElectiveGroupListDto,
  CreateElectiveGroupRequest,
} from '../types/electiveGroup';
import type { ElectiveSubtypeDto } from '../types/electiveSubtype';
import { getSubtypeColorByCode, findSubtypeById } from '../lib/electiveSubtypeUtils';
import ElectiveGroupDetailSheet from '../components/elective-groups/ElectiveGroupDetailSheet';

// ─── Create Dialog ─────────────────────────────────────────────────

interface CreateDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

function CreateElectiveGroupDialog({ open, onOpenChange }: CreateDialogProps) {
  const [name, setName] = useState('');
  const [code, setCode] = useState('');
  const [subtypeId, setSubtypeId] = useState<number | null>(null);
  const [facultyId, setFacultyId] = useState<string>('');
  const [departmentId, setDepartmentId] = useState<string>('');
  const [description, setDescription] = useState('');
  const [validationError, setValidationError] = useState<string | null>(null);

  // Faz B B3c-2: DB-driven subtype listesi (sadece aktif tipler).
  const { data: subtypes, isLoading: subtypesLoading } = useElectiveSubtypeList(false);
  const selectedSubtype = subtypes?.find(s => s.id === subtypeId) ?? null;

  const { data: faculties } = useAdminFaculties();
  const facultyIdNum = facultyId ? Number(facultyId) : undefined;
  const { data: departments } = useAdminDepartmentsByStructure(facultyIdNum);
  const createMutation = useCreateElectiveGroup();

  function reset() {
    setName('');
    setCode('');
    setSubtypeId(null);
    setFacultyId('');
    setDepartmentId('');
    setDescription('');
    setValidationError(null);
  }

  function handleSubtypeChange(v: string) {
    const nextId = Number(v);
    setSubtypeId(nextId);
    // ForbidsFaculty/ForbidsDepartment seçilince ilgili alanları temizle.
    const next = subtypes?.find(s => s.id === nextId);
    if (next?.forbidsFaculty) setFacultyId('');
    if (next?.forbidsDepartment) setDepartmentId('');
    setValidationError(null);
  }

  function handleFacultyChange(v: string) {
    setFacultyId(v);
    // changing faculty resets department to avoid orphan
    setDepartmentId('');
    setValidationError(null);
  }

  function handleCreate() {
    if (selectedSubtype === null) {
      setValidationError('Tip seçilmelidir.');
      return;
    }

    const facultyIdNumLocal = facultyId ? Number(facultyId) : null;
    const departmentIdNumLocal = departmentId && departmentId !== '__all__' ? Number(departmentId) : null;

    // Flag-based client-side validation (DB-driven kuralları yansıtır).
    if (selectedSubtype.forbidsFaculty && facultyIdNumLocal !== null) {
      setValidationError(`${selectedSubtype.name} için fakülte seçilemez.`);
      return;
    }
    if (selectedSubtype.requiresFaculty && facultyIdNumLocal === null) {
      setValidationError(`${selectedSubtype.name} için fakülte zorunlu.`);
      return;
    }
    if (selectedSubtype.forbidsDepartment && departmentIdNumLocal !== null) {
      setValidationError(`${selectedSubtype.name} için bölüm seçilemez.`);
      return;
    }
    if (selectedSubtype.requiresDepartment && departmentIdNumLocal === null) {
      setValidationError(`${selectedSubtype.name} için bölüm zorunlu.`);
      return;
    }
    if (departmentIdNumLocal !== null && facultyIdNumLocal === null) {
      setValidationError('Bölüm seçildiğinde fakülte de zorunlu.');
      return;
    }
    setValidationError(null);

    const req: CreateElectiveGroupRequest = {
      name: name.trim(),
      code: code.trim(),
      electiveSubtypeId: selectedSubtype.id,
      ...(facultyIdNumLocal !== null ? { facultyId: facultyIdNumLocal } : {}),
      ...(departmentIdNumLocal !== null ? { departmentId: departmentIdNumLocal } : {}),
      ...(description.trim() ? { description: description.trim() } : {}),
    };
    createMutation.mutate(req, {
      onSuccess: () => {
        reset();
        onOpenChange(false);
      },
    });
  }

  function handleOpenChange(val: boolean) {
    if (!val) reset();
    onOpenChange(val);
  }

  const canSubmit = name.trim().length > 0
    && code.trim().length > 0
    && selectedSubtype !== null
    && (!selectedSubtype.requiresFaculty || facultyId.length > 0)
    && (!selectedSubtype.requiresDepartment || departmentId.length > 0);

  const facultyDisabled = selectedSubtype?.forbidsFaculty ?? false;
  const departmentDisabled = facultyDisabled || !facultyId || (selectedSubtype?.forbidsDepartment ?? false);

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle>Yeni Seçmeli Ders Grubu</DialogTitle>
        </DialogHeader>

        <div className="space-y-4">
          <div>
            <Label htmlFor="ceg-name">Grup Adı</Label>
            <Input id="ceg-name" value={name} onChange={e => setName(e.target.value)} placeholder="örneğin: Teknik Seçmeli Grup A" />
          </div>
          <div>
            <Label htmlFor="ceg-code">Kod</Label>
            <Input id="ceg-code" value={code} onChange={e => setCode(e.target.value.toUpperCase())} placeholder="örneğin: TE-A" />
          </div>
          <div>
            <Label>Tip</Label>
            <Select
              value={subtypeId !== null ? String(subtypeId) : ''}
              onValueChange={handleSubtypeChange}
              disabled={subtypesLoading || !subtypes || subtypes.length === 0}
            >
              <SelectTrigger>
                <SelectValue placeholder={subtypesLoading ? 'Yükleniyor...' : 'Tip seçin...'} />
              </SelectTrigger>
              <SelectContent>
                {(subtypes ?? []).map(s => (
                  <SelectItem key={s.id} value={String(s.id)}>{s.name}</SelectItem>
                ))}
              </SelectContent>
            </Select>
            {!subtypesLoading && subtypes && subtypes.length === 0 && (
              <p className="text-[10px] text-amber-700 mt-1">
                Tanımlı aktif tip yok. Önce "Seçmeli Tipleri" sayfasından tip ekleyin.
              </p>
            )}
          </div>

          {!facultyDisabled && (
            <div>
              <Label>
                Fakülte{selectedSubtype?.requiresFaculty ? '' : ' (opsiyonel)'}
              </Label>
              <Select
                value={facultyId || '__none__'}
                onValueChange={v => handleFacultyChange(v === '__none__' ? '' : v)}
              >
                <SelectTrigger>
                  <SelectValue placeholder="Fakülte seçin..." />
                </SelectTrigger>
                <SelectContent>
                  {!selectedSubtype?.requiresFaculty && (
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
            <div className="space-y-1">
              <Label className="text-xs">
                Bölüm{selectedSubtype?.requiresDepartment ? '' : ' (opsiyonel)'}
              </Label>
              <Select
                value={departmentId || '__all__'}
                onValueChange={v => setDepartmentId(v === '__all__' ? '' : v)}
              >
                <SelectTrigger className="h-9 text-sm">
                  <SelectValue placeholder="(Tüm fakülte)" />
                </SelectTrigger>
                <SelectContent>
                  {!selectedSubtype?.requiresDepartment && (
                    <SelectItem value="__all__">(Tüm fakülte)</SelectItem>
                  )}
                  {(departments ?? []).map(d => (
                    <SelectItem key={d.id} value={String(d.id)}>{d.name}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
              <p className="text-[10px] text-muted-foreground">
                Boş bırakırsanız fakültenin tüm bölümleri kullanabilir.
              </p>
            </div>
          )}

          <div>
            <Label htmlFor="ceg-desc">Açıklama</Label>
            <Textarea id="ceg-desc" value={description} onChange={e => setDescription(e.target.value)} rows={3} />
          </div>
        </div>

        {validationError && (
          <p className="text-xs text-destructive mt-1">{validationError}</p>
        )}
        {createMutation.isError && !validationError && (
          <p className="text-xs text-destructive mt-1">Grup oluşturulamadı.</p>
        )}

        <DialogFooter>
          <Button variant="outline" onClick={() => handleOpenChange(false)}>İptal</Button>
          <Button onClick={handleCreate} disabled={!canSubmit || createMutation.isPending}>
            {createMutation.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
            Oluştur
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

// ─── Subtype Badge (Faz E: DB-driven) ──────────────────────────────

function SubtypeBadge({ subtype }: { subtype: ElectiveSubtypeDto | null }) {
  if (!subtype) {
    return (
      <Badge variant="outline" className="text-[10px] font-medium bg-slate-50 text-slate-500 border-slate-200">
        ?
      </Badge>
    );
  }
  return (
    <Badge
      variant="outline"
      className={`text-[10px] font-medium ${getSubtypeColorByCode(subtype.code)}`}
      title={subtype.name}
    >
      {subtype.name}
    </Badge>
  );
}

// ─── Main Page ─────────────────────────────────────────────────────

export default function ElectiveGroupManagementPage() {
  // Faz B B3c-3: subtypeFilter artık subtypeId ('all' veya numerik string).
  // Custom subtype'lar da filter dropdown'a düşer; faculty/department filter
  // subtype'tan bağımsız aktif (eski hardcoded 'TechnicalElective' check kalktı).
  const [subtypeFilter, setSubtypeFilter] = useState<string>('all');
  const [facultyFilter, setFacultyFilter] = useState<string>('all');
  const [departmentFilter, setDepartmentFilter] = useState<string>('all');
  const [statusFilter, setStatusFilter] = useState<string>('all');
  const [createOpen, setCreateOpen] = useState(false);
  const [selectedGroupId, setSelectedGroupId] = useState<number | null>(null);

  const { data: faculties } = useAdminFaculties();
  const facultyFilterIdNum = facultyFilter !== 'all' ? Number(facultyFilter) : undefined;
  const { data: filterDepartments } = useAdminDepartmentsByStructure(facultyFilterIdNum);
  // Faz B B3c-3: DB-driven subtype listesi (sadece aktif tipler — pasif tipler
  // filter'da görünmez). useElectiveSubtypeList multi-role açık (Patch 2A).
  const { data: filterSubtypes } = useElectiveSubtypeList(false);

  const querySubtypeId = subtypeFilter === 'all' ? undefined : Number(subtypeFilter);
  const queryFacultyId = facultyFilter !== 'all' ? Number(facultyFilter) : undefined;
  const queryDepartmentId = queryFacultyId !== undefined && departmentFilter !== 'all'
    ? Number(departmentFilter)
    : undefined;
  const queryIsActive = statusFilter === 'all' ? undefined : statusFilter === 'active';

  const { data: groups, isLoading, error } = useElectiveGroupList(querySubtypeId, queryFacultyId, queryDepartmentId, queryIsActive);

  const hasActiveFilters = subtypeFilter !== 'all' || facultyFilter !== 'all' || departmentFilter !== 'all' || statusFilter !== 'all';

  function resetFilters() {
    setSubtypeFilter('all');
    setFacultyFilter('all');
    setDepartmentFilter('all');
    setStatusFilter('all');
  }

  return (
    <div className="space-y-5">

      {/* ── Error ── */}
      {error && (
        <Alert variant="destructive">
          <AlertCircle className="h-4 w-4" />
          <AlertDescription>Gruplar yüklenirken hata oluştu.</AlertDescription>
        </Alert>
      )}

      {/* ── Single Container ── */}
      <div className="bg-background border border-border rounded-xl overflow-hidden">

        {/* ── Toolbar: Filters + Action ── */}
        <div className="flex flex-wrap gap-3 items-end px-4 py-3 border-b border-border bg-muted/40">
          <div className="space-y-1">
            <span className="text-xs text-muted-foreground">Tip</span>
            <Select value={subtypeFilter} onValueChange={setSubtypeFilter}>
              <SelectTrigger className="w-[200px] h-9 bg-white">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">Tümü</SelectItem>
                {(filterSubtypes ?? []).map(s => (
                  <SelectItem key={s.id} value={String(s.id)}>{s.name}</SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="space-y-1">
            <span className="text-xs text-muted-foreground">Fakülte</span>
            <Select
              value={facultyFilter}
              onValueChange={v => { setFacultyFilter(v); setDepartmentFilter('all'); }}
            >
              <SelectTrigger className="w-[180px] h-9 bg-white">
                <SelectValue placeholder="Tümü" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">Tümü</SelectItem>
                {faculties?.map(f => (
                  <SelectItem key={f.id} value={String(f.id)}>{f.name}</SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="space-y-1">
            <span className="text-xs text-muted-foreground">Bölüm</span>
            <Select
              value={departmentFilter}
              onValueChange={setDepartmentFilter}
              disabled={facultyFilter === 'all'}
            >
              <SelectTrigger className="w-[180px] h-9 bg-white">
                <SelectValue placeholder="Tümü" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">Tümü</SelectItem>
                {(filterDepartments ?? []).map(d => (
                  <SelectItem key={d.id} value={String(d.id)}>{d.name}</SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="space-y-1">
            <span className="text-xs text-muted-foreground">Durum</span>
            <Select value={statusFilter} onValueChange={setStatusFilter}>
              <SelectTrigger className="w-[120px] h-9 bg-white">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">Tümü</SelectItem>
                <SelectItem value="active">Aktif</SelectItem>
                <SelectItem value="inactive">Pasif</SelectItem>
              </SelectContent>
            </Select>
          </div>

          {hasActiveFilters && (
            <Button variant="ghost" size="sm" className="h-9 text-xs text-muted-foreground" onClick={resetFilters}>
              <RotateCcw className="h-3 w-3 mr-1" />
              Sıfırla
            </Button>
          )}

          <Button onClick={() => setCreateOpen(true)} className="h-9 ml-auto">
            <Plus className="h-4 w-4 mr-1.5" />
            Yeni Grup
          </Button>
        </div>

        {/* ── Table ── */}
        <table className="w-full">
          <thead className="bg-muted/20 border-b border-border">
            <tr>
              <th className="text-left px-4 py-3 text-xs font-semibold text-muted-foreground w-28">Kod</th>
              <th className="text-left px-4 py-3 text-xs font-semibold text-muted-foreground">Grup Adı</th>
              <th className="text-center px-4 py-3 text-xs font-semibold text-muted-foreground w-20">Tür</th>
              <th className="text-left px-4 py-3 text-xs font-semibold text-muted-foreground w-56">Kapsam</th>
              <th className="text-center px-4 py-3 text-xs font-semibold text-muted-foreground w-16">Ders</th>
              <th className="text-center px-4 py-3 text-xs font-semibold text-muted-foreground w-20">Durum</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-border">
            {isLoading && (
              <tr>
                <td colSpan={6} className="px-4 py-10 text-center text-sm text-muted-foreground">
                  <Loader2 className="h-5 w-5 animate-spin mx-auto mb-2 text-muted-foreground" />
                  Yükleniyor...
                </td>
              </tr>
            )}

            {!isLoading && groups && groups.length === 0 && (
              <tr>
                <td colSpan={6} className="px-4 py-16 text-center">
                  <FolderOpen className="h-10 w-10 text-muted-foreground/30 mx-auto mb-3" />
                  <p className="text-sm font-medium text-foreground">
                    {hasActiveFilters ? 'Filtrelerle eşleşen grup bulunamadı' : 'Henüz seçmeli ders grubu yok'}
                  </p>
                  <p className="text-xs text-muted-foreground mt-1">
                    {hasActiveFilters
                      ? 'Farklı filtre kombinasyonlarını deneyebilir veya filtreleri sıfırlayabilirsiniz.'
                      : 'Yukarıdaki "Yeni Grup" butonunu kullanarak ilk grubunuzu oluşturun.'
                    }
                  </p>
                </td>
              </tr>
            )}

            {!isLoading && groups && groups.map((g: ElectiveGroupListDto) => (
              <tr
                key={g.id}
                className="cursor-pointer hover:bg-muted/20 transition-colors"
                onClick={() => setSelectedGroupId(g.id)}
              >
                <td className="px-4 py-3 font-mono text-xs font-medium">{g.code}</td>
                <td className="px-4 py-3 text-sm font-medium">{g.name}</td>
                <td className="px-4 py-3 text-center">
                  <SubtypeBadge subtype={findSubtypeById(filterSubtypes, g.electiveSubtypeId)} />
                </td>
                <td className="px-4 py-3 text-xs text-muted-foreground">
                  {g.facultyName ? (
                    <>
                      <span>Fakülte: <span className="text-foreground">{g.facultyName}</span></span>
                      {g.departmentName && (
                        <> · <span>Bölüm: <span className="text-foreground">{g.departmentName}</span></span></>
                      )}
                    </>
                  ) : (
                    <span>Okul-geneli</span>
                  )}
                </td>
                <td className="px-4 py-3 text-center text-sm">{g.courseCount}</td>
                <td className="px-4 py-3 text-center">
                  <Badge
                    variant="outline"
                    className={
                      g.isActive
                        ? 'bg-emerald-50 text-emerald-700 border-emerald-200'
                        : 'bg-red-50 text-red-600 border-red-200'
                    }
                  >
                    {g.isActive ? 'Aktif' : 'Pasif'}
                  </Badge>
                </td>
              </tr>
            ))}
          </tbody>
        </table>

        {/* ── Footer ── */}
        {!isLoading && groups && groups.length > 0 && (
          <div className="px-4 py-2.5 border-t border-border bg-muted/30">
            <p className="text-xs text-muted-foreground">
              Toplam <span className="font-medium text-foreground">{groups.length}</span> grup
            </p>
          </div>
        )}
      </div>

      {/* ── Create Dialog ── */}
      <CreateElectiveGroupDialog open={createOpen} onOpenChange={setCreateOpen} />

      {/* ── Detail Sheet ── */}
      <ElectiveGroupDetailSheet
        key={selectedGroupId}
        groupId={selectedGroupId}
        onClose={() => setSelectedGroupId(null)}
      />
    </div>
  );
}
