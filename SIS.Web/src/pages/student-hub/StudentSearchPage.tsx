import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Search, UserPlus, ChevronLeft, ChevronRight } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { useAdminStudents } from '../../hooks/useAdminStudents';
import { useAdminDepartments, useAdminPrograms } from '../../hooks/useAdminLookup';
import { STUDENT_STATUS_LABELS } from '../../lib/constants';
import CreateStudentDialog from './CreateStudentDialog';

const STATUS_VARIANT: Record<string, 'default' | 'secondary' | 'destructive' | 'success' | 'warning' | 'outline'> = {
  Active: 'success',
  Passive: 'secondary',
  Frozen: 'warning',
  Suspended: 'destructive',
  Dismissed: 'destructive',
  Graduated: 'default',
};

function cgpaColor(cgpa: number | null) {
  if (cgpa === null) return 'text-muted-foreground';
  if (cgpa >= 3.0) return 'text-green-600';
  if (cgpa >= 2.0) return 'text-yellow-600';
  return 'text-red-600';
}

function parseHasAdvisor(value: string): boolean | undefined {
  if (value === 'true') return true;
  if (value === 'false') return false;
  return undefined;
}

export default function StudentSearchPage() {
  const navigate = useNavigate();

  const [search, setSearch] = useState('');
  const [departmentId, setDepartmentId] = useState<number | undefined>(undefined);
  const [programId, setProgramId] = useState<number | undefined>(undefined);
  const [status, setStatus] = useState('');
  const [advisorFilter, setAdvisorFilter] = useState('');
  const [page, setPage] = useState(1);
  const [createModalOpen, setCreateModalOpen] = useState(false);
  const [successMessage, setSuccessMessage] = useState('');

  const { data, isLoading } = useAdminStudents({
    search: search || undefined,
    departmentId,
    programId,
    status: status || undefined,
    hasAdvisor: parseHasAdvisor(advisorFilter),
    page,
    pageSize: 25,
  });

  const { data: departments = [] } = useAdminDepartments();
  const { data: programs = [] } = useAdminPrograms(departmentId);

  function showSuccess(msg: string) {
    setSuccessMessage(msg);
    setTimeout(() => setSuccessMessage(''), 3000);
  }

  return (
    <div className="space-y-6">
      {/* Filters + Action */}
      <div className="flex flex-wrap gap-3 items-center bg-white border border-border rounded-xl p-4">
        <div className="relative flex-1 min-w-[200px]">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
          <Input
            placeholder="Ad, soyad veya öğrenci numarası..."
            value={search}
            onChange={e => { setSearch(e.target.value); setPage(1); }}
            className="pl-9"
          />
        </div>

        <select
          value={departmentId ?? ''}
          onChange={e => {
            setDepartmentId(e.target.value ? Number(e.target.value) : undefined);
            setProgramId(undefined);
            setPage(1);
          }}
          className="border border-border rounded-lg px-3 py-2 text-sm bg-white text-foreground"
        >
          <option value="">Tüm Bölümler</option>
          {departments.map(d => (
            <option key={d.id} value={d.id}>{d.name}</option>
          ))}
        </select>

        <select
          value={programId ?? ''}
          onChange={e => { setProgramId(e.target.value ? Number(e.target.value) : undefined); setPage(1); }}
          className="border border-border rounded-lg px-3 py-2 text-sm bg-white text-foreground"
        >
          <option value="">Tüm Programlar</option>
          {programs.map(p => (
            <option key={p.id} value={p.id}>{p.name}</option>
          ))}
        </select>

        <select
          value={status}
          onChange={e => { setStatus(e.target.value); setPage(1); }}
          className="border border-border rounded-lg px-3 py-2 text-sm bg-white text-foreground"
        >
          <option value="">Tüm Durumlar</option>
          {Object.entries(STUDENT_STATUS_LABELS).map(([k, v]) => (
            <option key={k} value={k}>{v}</option>
          ))}
        </select>

        <select
          value={advisorFilter}
          onChange={e => { setAdvisorFilter(e.target.value); setPage(1); }}
          className="border border-border rounded-lg px-3 py-2 text-sm bg-white text-foreground"
        >
          <option value="">Danışman: hepsi</option>
          <option value="true">Danışmanı var</option>
          <option value="false">Danışmanı yok</option>
        </select>

        <div className="flex items-center gap-2 ml-auto">
          {successMessage && (
            <span className="text-sm font-medium text-green-700">{successMessage}</span>
          )}
          <Button onClick={() => setCreateModalOpen(true)}>
            <UserPlus className="h-4 w-4 mr-2" />
            Yeni Öğrenci Ekle
          </Button>
        </div>
      </div>

      {/* Table */}
      <div className="bg-white border border-border rounded-xl overflow-hidden">
        <table className="w-full text-sm">
          <thead className="bg-muted/40 border-b border-border">
            <tr>
              <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Öğrenci No</th>
              <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Ad Soyad</th>
              <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Program</th>
              <th className="text-left px-4 py-3 font-semibold text-muted-foreground">GNO</th>
              <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Dönem</th>
              <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Danışman</th>
              <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Durum</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-border">
            {isLoading ? (
              <tr>
                <td colSpan={7} className="px-4 py-8 text-center text-muted-foreground">Yükleniyor...</td>
              </tr>
            ) : data?.items.length === 0 ? (
              <tr>
                <td colSpan={7} className="px-4 py-8 text-center text-muted-foreground">Öğrenci bulunamadı.</td>
              </tr>
            ) : (
              data?.items.map(student => (
                <tr
                  key={student.id}
                  onClick={() => navigate(`/ogrenci-rehberi/${student.id}`)}
                  className="hover:bg-muted/20 transition-colors cursor-pointer"
                >
                  <td className="px-4 py-3 font-mono text-xs">{student.studentNumber}</td>
                  <td className="px-4 py-3 font-medium text-foreground">
                    {student.firstName} {student.lastName}
                  </td>
                  <td className="px-4 py-3 text-muted-foreground">
                    <div className="text-xs">{student.programName}</div>
                    <div className="text-xs text-muted-foreground/70">{student.departmentName}</div>
                  </td>
                  <td className="px-4 py-3">
                    {student.transcriptAccessible === false ? (
                      <span className="text-xs font-medium text-muted-foreground">Kapalı</span>
                    ) : (
                      <span className={`font-semibold ${cgpaColor(student.cgpa)}`}>
                        {student.cgpa?.toFixed(2) ?? '—'}
                      </span>
                    )}
                  </td>
                  <td className="px-4 py-3 text-center text-muted-foreground">
                    {student.completedSemesters ?? '—'}
                  </td>
                  <td className="px-4 py-3 text-muted-foreground text-xs">{student.advisorName ?? '—'}</td>
                  <td className="px-4 py-3">
                    <Badge variant={STATUS_VARIANT[student.status] ?? 'secondary'}>
                      {STUDENT_STATUS_LABELS[student.status] ?? student.status}
                    </Badge>
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>

        {data && data.totalPages > 1 && (
          <div className="flex items-center justify-between px-4 py-3 border-t border-border">
            <p className="text-sm text-muted-foreground">
              Toplam {data.totalCount} öğrenci — Sayfa {data.page}/{data.totalPages}
            </p>
            <div className="flex gap-2">
              <Button variant="outline" size="sm" disabled={page <= 1} onClick={() => setPage(p => p - 1)}>
                <ChevronLeft className="h-4 w-4" />
              </Button>
              <Button variant="outline" size="sm" disabled={page >= data.totalPages} onClick={() => setPage(p => p + 1)}>
                <ChevronRight className="h-4 w-4" />
              </Button>
            </div>
          </div>
        )}
      </div>

      {/* Create Student Modal — remounted on each open so form starts fresh */}
      {createModalOpen && (
        <CreateStudentDialog
          open={createModalOpen}
          onOpenChange={setCreateModalOpen}
          departmentId={departmentId}
          onCreated={showSuccess}
        />
      )}
    </div>
  );
}
