import { useState } from 'react';
import { Search, Shield, UserCheck, UserX, KeyRound, ChevronLeft, ChevronRight } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from '@/components/ui/dialog';
import {
  useAdminUsers,
  useUpdateUserRoles,
  useResetPassword,
  useSetUserStatus,
  type AdminUserListItem,
} from '../hooks/useAdminUsers';

const ALL_ROLES = [
  'SystemAdministrator',
  'Dean',
  'DepartmentManager',
  'AdministrativeStaff',
  'AcademicAdvisor',
  'Instructor',
  'Student',
];

const ROLE_LABELS: Record<string, string> = {
  SystemAdministrator: 'Sistem Yöneticisi',
  Dean: 'Dekan',
  DepartmentManager: 'Bölüm Başkanı',
  AdministrativeStaff: 'İdari Personel',
  AcademicAdvisor: 'Akademik Danışman',
  Instructor: 'Öğretim Üyesi',
  Student: 'Öğrenci',
};

const USER_TYPE_LABELS: Record<string, string> = {
  Student: 'Öğrenci',
  Instructor: 'Öğretim Üyesi',
  Admin: 'Yönetici',
  Other: 'Diğer',
};

const USER_TYPE_VARIANT: Record<string, 'default' | 'secondary' | 'success' | 'info'> = {
  Student: 'secondary',
  Instructor: 'info',
  Admin: 'default',
  Other: 'secondary',
};

export default function UserManagementPage() {
  const [search, setSearch] = useState('');
  const [roleFilter, setRoleFilter] = useState('');
  const [activeFilter, setActiveFilter] = useState<boolean | undefined>(undefined);
  const [page, setPage] = useState(1);

  const [selectedUser, setSelectedUser] = useState<AdminUserListItem | null>(null);
  const [rolesModalOpen, setRolesModalOpen] = useState(false);
  const [passwordModalOpen, setPasswordModalOpen] = useState(false);
  const [selectedRoles, setSelectedRoles] = useState<string[]>([]);
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [passwordError, setPasswordError] = useState('');
  const [successMessage, setSuccessMessage] = useState('');

  const { data, isLoading } = useAdminUsers({
    search: search || undefined,
    role: roleFilter || undefined,
    isActive: activeFilter,
    page,
    pageSize: 25,
  });

  const updateRoles = useUpdateUserRoles();
  const resetPassword = useResetPassword();
  const setStatus = useSetUserStatus();

  function openRolesModal(user: AdminUserListItem) {
    setSelectedUser(user);
    setSelectedRoles([...user.roles]);
    setRolesModalOpen(true);
  }

  function openPasswordModal(user: AdminUserListItem) {
    setSelectedUser(user);
    setNewPassword('');
    setConfirmPassword('');
    setPasswordError('');
    setPasswordModalOpen(true);
  }

  function toggleRole(role: string) {
    setSelectedRoles(prev =>
      prev.includes(role) ? prev.filter(r => r !== role) : [...prev, role]
    );
  }

  async function handleSaveRoles() {
    if (!selectedUser) return;
    await updateRoles.mutateAsync({ id: selectedUser.id, roles: selectedRoles });
    setRolesModalOpen(false);
    showSuccess('Roller güncellendi.');
  }

  async function handleResetPassword() {
    if (!selectedUser) return;
    if (newPassword.length < 8) {
      setPasswordError('Şifre en az 8 karakter olmalıdır.');
      return;
    }
    if (newPassword !== confirmPassword) {
      setPasswordError('Şifreler eşleşmiyor.');
      return;
    }
    await resetPassword.mutateAsync({ id: selectedUser.id, newPassword });
    setPasswordModalOpen(false);
    showSuccess('Şifre sıfırlandı.');
  }

  async function handleToggleStatus(user: AdminUserListItem) {
    await setStatus.mutateAsync({ id: user.id, isActive: !user.isActive });
    showSuccess(user.isActive ? 'Kullanıcı pasife alındı.' : 'Kullanıcı aktif edildi.');
  }

  function showSuccess(msg: string) {
    setSuccessMessage(msg);
    setTimeout(() => setSuccessMessage(''), 3000);
  }

  return (
    <div className="p-6 space-y-6">
      {/* Header */}
      {successMessage && (
        <div className="flex justify-end">
          <div className="px-4 py-2 bg-green-50 text-green-700 border border-green-200 rounded-lg text-sm font-medium">
            {successMessage}
          </div>
        </div>
      )}

      {/* Filters */}
      <div className="flex flex-wrap gap-3 items-center bg-white border border-border rounded-xl p-4">
        <div className="relative flex-1 min-w-[200px]">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
          <Input
            placeholder="Ad, soyad veya e-posta ara..."
            value={search}
            onChange={e => { setSearch(e.target.value); setPage(1); }}
            className="pl-9"
          />
        </div>

        <select
          value={roleFilter}
          onChange={e => { setRoleFilter(e.target.value); setPage(1); }}
          className="border border-border rounded-lg px-3 py-2 text-sm bg-white text-foreground"
        >
          <option value="">Tüm Roller</option>
          {ALL_ROLES.map(r => (
            <option key={r} value={r}>{ROLE_LABELS[r]}</option>
          ))}
        </select>

        <select
          value={activeFilter === undefined ? '' : String(activeFilter)}
          onChange={e => {
            setActiveFilter(e.target.value === '' ? undefined : e.target.value === 'true');
            setPage(1);
          }}
          className="border border-border rounded-lg px-3 py-2 text-sm bg-white text-foreground"
        >
          <option value="">Tüm Durumlar</option>
          <option value="true">Aktif</option>
          <option value="false">Pasif</option>
        </select>
      </div>

      {/* Table */}
      <div className="bg-white border border-border rounded-xl overflow-hidden">
        <table className="w-full text-sm">
          <thead className="bg-muted/40 border-b border-border">
            <tr>
              <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Ad Soyad</th>
              <th className="text-left px-4 py-3 font-semibold text-muted-foreground">E-posta</th>
              <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Tip</th>
              <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Roller</th>
              <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Durum</th>
              <th className="text-right px-4 py-3 font-semibold text-muted-foreground">İşlemler</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-border">
            {isLoading ? (
              <tr>
                <td colSpan={6} className="px-4 py-8 text-center text-muted-foreground">
                  Yükleniyor...
                </td>
              </tr>
            ) : data?.items.length === 0 ? (
              <tr>
                <td colSpan={6} className="px-4 py-8 text-center text-muted-foreground">
                  Kullanıcı bulunamadı.
                </td>
              </tr>
            ) : (
              data?.items.map(user => (
                <tr key={user.id} className="hover:bg-muted/20 transition-colors">
                  <td className="px-4 py-3 font-medium text-foreground">{user.fullName}</td>
                  <td className="px-4 py-3 text-muted-foreground">{user.email}</td>
                  <td className="px-4 py-3">
                    <Badge variant={USER_TYPE_VARIANT[user.userType] ?? 'secondary'}>
                      {USER_TYPE_LABELS[user.userType]}
                    </Badge>
                  </td>
                  <td className="px-4 py-3">
                    <div className="flex flex-wrap gap-1">
                      {user.roles.slice(0, 2).map(r => (
                        <Badge key={r} variant="outline" className="text-xs">
                          {ROLE_LABELS[r] ?? r}
                        </Badge>
                      ))}
                      {user.roles.length > 2 && (
                        <Badge variant="outline" className="text-xs">
                          +{user.roles.length - 2}
                        </Badge>
                      )}
                    </div>
                  </td>
                  <td className="px-4 py-3">
                    <Badge variant={user.isActive ? 'success' : 'destructive'}>
                      {user.isActive ? 'Aktif' : 'Pasif'}
                    </Badge>
                  </td>
                  <td className="px-4 py-3">
                    <div className="flex items-center justify-end gap-2">
                      <Button
                        variant="ghost"
                        size="sm"
                        onClick={() => openRolesModal(user)}
                        title="Rolleri Düzenle"
                      >
                        <Shield className="h-4 w-4" />
                      </Button>
                      <Button
                        variant="ghost"
                        size="sm"
                        onClick={() => openPasswordModal(user)}
                        title="Şifre Sıfırla"
                      >
                        <KeyRound className="h-4 w-4" />
                      </Button>
                      <Button
                        variant="ghost"
                        size="sm"
                        onClick={() => handleToggleStatus(user)}
                        title={user.isActive ? 'Pasife Al' : 'Aktif Et'}
                        className={user.isActive ? 'text-destructive hover:text-destructive' : 'text-green-600 hover:text-green-600'}
                      >
                        {user.isActive ? <UserX className="h-4 w-4" /> : <UserCheck className="h-4 w-4" />}
                      </Button>
                    </div>
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>

        {/* Pagination */}
        {data && data.totalPages > 1 && (
          <div className="flex items-center justify-between px-4 py-3 border-t border-border">
            <p className="text-sm text-muted-foreground">
              Toplam {data.totalCount} kullanıcı — Sayfa {data.page}/{data.totalPages}
            </p>
            <div className="flex gap-2">
              <Button
                variant="outline"
                size="sm"
                disabled={page <= 1}
                onClick={() => setPage(p => p - 1)}
              >
                <ChevronLeft className="h-4 w-4" />
              </Button>
              <Button
                variant="outline"
                size="sm"
                disabled={page >= data.totalPages}
                onClick={() => setPage(p => p + 1)}
              >
                <ChevronRight className="h-4 w-4" />
              </Button>
            </div>
          </div>
        )}
      </div>

      {/* Roles Modal */}
      <Dialog open={rolesModalOpen} onOpenChange={setRolesModalOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Rolleri Düzenle — {selectedUser?.fullName}</DialogTitle>
          </DialogHeader>
          <div className="space-y-2 py-2">
            {ALL_ROLES.map(role => (
              <label key={role} className="flex items-center gap-3 p-2 rounded-lg hover:bg-muted/50 cursor-pointer">
                <input
                  type="checkbox"
                  checked={selectedRoles.includes(role)}
                  onChange={() => toggleRole(role)}
                  className="h-4 w-4 rounded border-border"
                />
                <span className="text-sm font-medium">{ROLE_LABELS[role]}</span>
                <span className="text-xs text-muted-foreground ml-auto">{role}</span>
              </label>
            ))}
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setRolesModalOpen(false)}>İptal</Button>
            <Button onClick={handleSaveRoles} disabled={updateRoles.isPending}>
              {updateRoles.isPending ? 'Kaydediliyor...' : 'Kaydet'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Password Reset Modal */}
      <Dialog open={passwordModalOpen} onOpenChange={setPasswordModalOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Şifre Sıfırla — {selectedUser?.fullName}</DialogTitle>
          </DialogHeader>
          <div className="space-y-4 py-2">
            <div className="space-y-1">
              <label className="text-sm font-medium">Yeni Şifre</label>
              <Input
                type="password"
                value={newPassword}
                onChange={e => { setNewPassword(e.target.value); setPasswordError(''); }}
                placeholder="En az 8 karakter, rakam içermeli"
              />
            </div>
            <div className="space-y-1">
              <label className="text-sm font-medium">Şifreyi Onayla</label>
              <Input
                type="password"
                value={confirmPassword}
                onChange={e => { setConfirmPassword(e.target.value); setPasswordError(''); }}
                placeholder="Şifreyi tekrar girin"
              />
            </div>
            {passwordError && (
              <p className="text-sm text-destructive">{passwordError}</p>
            )}
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setPasswordModalOpen(false)}>İptal</Button>
            <Button onClick={handleResetPassword} disabled={resetPassword.isPending}>
              {resetPassword.isPending ? 'Sıfırlanıyor...' : 'Sıfırla'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
