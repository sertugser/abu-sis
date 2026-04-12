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
import { useAdminFaculties } from '../hooks/useAdminAcademicStructure';
import {
  ELECTIVE_SUBTYPE_LABELS,
  ELECTIVE_SUBTYPE_SHORT,
} from '../types/electiveGroup';
import type {
  ElectiveSubtype,
  ElectiveGroupListDto,
  CreateElectiveGroupRequest,
} from '../types/electiveGroup';
import ElectiveGroupDetailSheet from '../components/elective-groups/ElectiveGroupDetailSheet';

const SUBTYPES: ElectiveSubtype[] = ['UniversityElective', 'NonDepartmentElective', 'TechnicalElective'];

// ─── Create Dialog ─────────────────────────────────────────────────

interface CreateDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

function CreateElectiveGroupDialog({ open, onOpenChange }: CreateDialogProps) {
  const [name, setName] = useState('');
  const [code, setCode] = useState('');
  const [subtype, setSubtype] = useState<ElectiveSubtype>('UniversityElective');
  const [facultyId, setFacultyId] = useState<string>('');
  const [description, setDescription] = useState('');

  const { data: faculties } = useAdminFaculties();
  const createMutation = useCreateElectiveGroup();

  function reset() {
    setName('');
    setCode('');
    setSubtype('UniversityElective');
    setFacultyId('');
    setDescription('');
  }

  function handleCreate() {
    const req: CreateElectiveGroupRequest = {
      name: name.trim(),
      code: code.trim(),
      electiveSubtype: subtype,
      ...(subtype === 'TechnicalElective' && facultyId ? { facultyId: Number(facultyId) } : {}),
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

  const canSubmit = name.trim().length > 0 && code.trim().length > 0
    && (subtype !== 'TechnicalElective' || facultyId.length > 0);

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
            <Label>Tür</Label>
            <Select value={subtype} onValueChange={v => setSubtype(v as ElectiveSubtype)}>
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {SUBTYPES.map(s => (
                  <SelectItem key={s} value={s}>{ELECTIVE_SUBTYPE_LABELS[s]}</SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          {subtype === 'TechnicalElective' && (
            <div>
              <Label>Fakülte</Label>
              <Select value={facultyId} onValueChange={setFacultyId}>
                <SelectTrigger>
                  <SelectValue placeholder="Fakülte seçin..." />
                </SelectTrigger>
                <SelectContent>
                  {faculties?.map(f => (
                    <SelectItem key={f.id} value={String(f.id)}>{f.name}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          )}

          <div>
            <Label htmlFor="ceg-desc">Açıklama</Label>
            <Textarea id="ceg-desc" value={description} onChange={e => setDescription(e.target.value)} rows={3} />
          </div>
        </div>

        {createMutation.isError && (
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

// ─── Subtype Badge ─────────────────────────────────────────────────

const SUBTYPE_COLORS: Record<ElectiveSubtype, string> = {
  UniversityElective: 'bg-blue-50 text-blue-700 border-blue-200',
  NonDepartmentElective: 'bg-purple-50 text-purple-700 border-purple-200',
  TechnicalElective: 'bg-amber-50 text-amber-700 border-amber-200',
};

function SubtypeBadge({ subtype }: { subtype: ElectiveSubtype }) {
  return (
    <Badge variant="outline" className={`text-[10px] font-medium ${SUBTYPE_COLORS[subtype]}`}>
      {ELECTIVE_SUBTYPE_SHORT[subtype]}
    </Badge>
  );
}

// ─── Main Page ─────────────────────────────────────────────────────

export default function ElectiveGroupManagementPage() {
  const [subtypeFilter, setSubtypeFilter] = useState<string>('all');
  const [facultyFilter, setFacultyFilter] = useState<string>('all');
  const [statusFilter, setStatusFilter] = useState<string>('all');
  const [createOpen, setCreateOpen] = useState(false);
  const [selectedGroupId, setSelectedGroupId] = useState<number | null>(null);

  const { data: faculties } = useAdminFaculties();

  const querySubtype = subtypeFilter === 'all' ? undefined : (subtypeFilter as ElectiveSubtype);
  const queryFacultyId = subtypeFilter === 'TechnicalElective' && facultyFilter !== 'all'
    ? Number(facultyFilter)
    : undefined;
  const queryIsActive = statusFilter === 'all' ? undefined : statusFilter === 'active';

  const { data: groups, isLoading, error } = useElectiveGroupList(querySubtype, queryFacultyId, queryIsActive);

  const hasActiveFilters = subtypeFilter !== 'all' || facultyFilter !== 'all' || statusFilter !== 'all';

  function resetFilters() {
    setSubtypeFilter('all');
    setFacultyFilter('all');
    setStatusFilter('all');
  }

  return (
    <div className="p-6 space-y-5">

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
            <span className="text-xs text-muted-foreground">Tür</span>
            <Select value={subtypeFilter} onValueChange={v => { setSubtypeFilter(v); if (v !== 'TechnicalElective') setFacultyFilter('all'); }}>
              <SelectTrigger className="w-[200px] h-9 bg-white">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">Tümü</SelectItem>
                {SUBTYPES.map(s => (
                  <SelectItem key={s} value={s}>{ELECTIVE_SUBTYPE_LABELS[s]}</SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="space-y-1">
            <span className="text-xs text-muted-foreground">Fakülte</span>
            <Select
              value={facultyFilter}
              onValueChange={setFacultyFilter}
              disabled={subtypeFilter !== 'TechnicalElective'}
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
              <th className="text-left px-4 py-3 text-xs font-semibold text-muted-foreground w-44">Fakülte</th>
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
                  <SubtypeBadge subtype={g.electiveSubtype} />
                </td>
                <td className="px-4 py-3 text-sm text-muted-foreground">{g.facultyName ?? '—'}</td>
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
