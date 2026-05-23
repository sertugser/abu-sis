import { useState } from 'react';
import { Button } from '@/components/ui/button';
import {
  Popover, PopoverContent, PopoverTrigger,
} from '@/components/ui/popover';
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/components/ui/select';
import { Globe, Lock, LockOpen, Loader2, Plus, X } from 'lucide-react';
import {
  useAccessPolicies,
  useCreateAccessPolicy,
  useDeleteAccessPolicy,
} from '../../hooks/useCourseAccessPolicy';
import {
  useAdminDepartmentsByStructure,
} from '../../hooks/useAdminAcademicStructure';
import { useFacultyLookup } from '../../hooks/useAdminLookup';

interface Props {
  courseId: number;
  termId: number;
  courseDepartmentId: number | null;
  courseDepartmentName: string | null;
}

export function AccessPolicyPopover({ courseId, termId, courseDepartmentId, courseDepartmentName }: Props) {
  const { data: policies = [], isLoading } = useAccessPolicies(courseId, termId);
  const createMut = useCreateAccessPolicy();
  const deleteMut = useDeleteAccessPolicy();

  const [addMode, setAddMode] = useState<'faculty' | 'department'>('faculty');
  const [selectedFacultyId, setSelectedFacultyId] = useState<string>('');
  const [selectedDeptId, setSelectedDeptId] = useState<string>('');

  const { data: faculties = [] } = useFacultyLookup();
  const { data: departments = [] } = useAdminDepartmentsByStructure(
    addMode === 'department' && selectedFacultyId ? Number(selectedFacultyId) : undefined
  );

  if (courseDepartmentId === null) {
    return (
      <span title="Ortak ders — tüm öğrenciler alabilir">
        <Globe className="h-3.5 w-3.5 text-muted-foreground/50" />
      </span>
    );
  }

  const existingFacultyIds = new Set(policies.filter(p => p.facultyId).map(p => p.facultyId!));
  const existingDeptIds = new Set(policies.filter(p => p.departmentId).map(p => p.departmentId!));

  function handleAdd() {
    if (addMode === 'faculty' && selectedFacultyId) {
      createMut.mutate({ courseId, termId, facultyId: Number(selectedFacultyId) });
      setSelectedFacultyId('');
    } else if (addMode === 'department' && selectedDeptId) {
      createMut.mutate({ courseId, termId, departmentId: Number(selectedDeptId) });
      setSelectedDeptId('');
      setSelectedFacultyId('');
    }
  }

  const hasPolicy = policies.length > 0;

  return (
    <Popover>
      <PopoverTrigger asChild>
        <button
          className={`relative p-1 rounded hover:bg-accent transition-colors ${hasPolicy ? 'text-primary' : 'text-muted-foreground/50'}`}
          title={hasPolicy ? `${policies.length} ek erişim tanımlı` : 'Erişim: sadece kendi bölüm'}
        >
          {hasPolicy ? <LockOpen className="h-3.5 w-3.5" /> : <Lock className="h-3.5 w-3.5" />}
          {hasPolicy && (
            <span className="absolute -top-1 -right-1 bg-primary text-primary-foreground text-[9px] rounded-full w-3.5 h-3.5 flex items-center justify-center font-bold">
              {policies.length}
            </span>
          )}
        </button>
      </PopoverTrigger>
      <PopoverContent className="w-80 p-3" align="start">
        <p className="text-xs text-muted-foreground italic mb-3">
          Varsayılan: <span className="font-medium text-foreground">{courseDepartmentName}</span> öğrencileri
        </p>

        {isLoading ? (
          <div className="flex items-center gap-2 text-xs text-muted-foreground mb-3">
            <Loader2 className="h-3 w-3 animate-spin" /> Yükleniyor...
          </div>
        ) : policies.length > 0 ? (
          <div className="mb-3">
            <p className="text-xs font-medium text-foreground mb-1.5">Ek Erişimler</p>
            <div className="flex flex-wrap gap-1.5">
              {policies.map(p => (
                <div
                  key={p.id}
                  className={`inline-flex items-center gap-1.5 rounded-lg border px-2.5 py-1 text-xs ${
                    p.facultyId
                      ? 'bg-blue-50 text-blue-800 border-blue-200'
                      : 'bg-gray-50 text-gray-800 border-gray-200'
                  }`}
                >
                  <span className="font-medium truncate max-w-[180px]">{p.facultyName ?? p.departmentName}</span>
                  <button
                    className="shrink-0 rounded-full p-0.5 hover:bg-red-100 hover:text-red-600 transition-colors"
                    onClick={() => deleteMut.mutate({ id: p.id, courseId, termId })}
                    disabled={deleteMut.isPending}
                  >
                    <X className="h-3 w-3" />
                  </button>
                </div>
              ))}
            </div>
          </div>
        ) : (
          <p className="text-xs text-muted-foreground italic mb-3">Ek erişim tanımlanmamış</p>
        )}

        <div className="border-t pt-2.5 space-y-2">
          <div className="flex items-center gap-0.5 bg-muted rounded-md p-0.5 w-fit">
            <button
              onClick={() => { setAddMode('faculty'); setSelectedFacultyId(''); setSelectedDeptId(''); }}
              className={`px-2.5 py-1 text-xs rounded transition-all ${
                addMode === 'faculty'
                  ? 'bg-background text-foreground shadow-sm font-medium'
                  : 'text-muted-foreground hover:text-foreground'
              }`}
            >
              Fakülte
            </button>
            <button
              onClick={() => { setAddMode('department'); setSelectedFacultyId(''); setSelectedDeptId(''); }}
              className={`px-2.5 py-1 text-xs rounded transition-all ${
                addMode === 'department'
                  ? 'bg-background text-foreground shadow-sm font-medium'
                  : 'text-muted-foreground hover:text-foreground'
              }`}
            >
              Bölüm
            </button>
          </div>

          {addMode === 'faculty' ? (
            <div className="flex items-center gap-2">
              <Select value={selectedFacultyId} onValueChange={setSelectedFacultyId}>
                <SelectTrigger className="h-8 text-xs flex-1">
                  <SelectValue placeholder="Fakülte seçin" />
                </SelectTrigger>
                <SelectContent>
                  {faculties
                    .filter(f => !existingFacultyIds.has(f.id))
                    .map(f => (
                      <SelectItem key={f.id} value={String(f.id)}>{f.name}</SelectItem>
                    ))}
                </SelectContent>
              </Select>
              <Button
                size="sm"
                className="h-8 text-xs"
                onClick={handleAdd}
                disabled={!selectedFacultyId || createMut.isPending}
              >
                {createMut.isPending ? <Loader2 className="h-3 w-3 animate-spin" /> : <Plus className="h-3 w-3" />}
              </Button>
            </div>
          ) : (
            <div className="space-y-1.5">
              <Select value={selectedFacultyId} onValueChange={v => { setSelectedFacultyId(v); setSelectedDeptId(''); }}>
                <SelectTrigger className="h-8 text-xs">
                  <SelectValue placeholder="Önce fakülte seçin" />
                </SelectTrigger>
                <SelectContent>
                  {faculties.map(f => (
                    <SelectItem key={f.id} value={String(f.id)}>{f.name}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
              <div className="flex items-center gap-2">
                <Select
                  value={selectedDeptId}
                  onValueChange={setSelectedDeptId}
                  disabled={!selectedFacultyId}
                >
                  <SelectTrigger className="h-8 text-xs flex-1">
                    <SelectValue placeholder="Bölüm seçin" />
                  </SelectTrigger>
                  <SelectContent>
                    {departments
                      .filter(d => !existingDeptIds.has(d.id))
                      .map(d => (
                        <SelectItem key={d.id} value={String(d.id)}>{d.name}</SelectItem>
                      ))}
                  </SelectContent>
                </Select>
                <Button
                  size="sm"
                  className="h-8 text-xs"
                  onClick={handleAdd}
                  disabled={!selectedDeptId || createMut.isPending}
                >
                  {createMut.isPending ? <Loader2 className="h-3 w-3 animate-spin" /> : <Plus className="h-3 w-3" />}
                </Button>
              </div>
            </div>
          )}
        </div>
      </PopoverContent>
    </Popover>
  );
}
