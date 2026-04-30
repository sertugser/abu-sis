import { useNavigate, useParams, Navigate } from 'react-router-dom';
import { Tabs, TabsList, TabsTrigger, TabsContent } from '@/components/ui/tabs';
import { usePermissions } from '../hooks/usePermissions';
import ElectiveGroupManagementPage from './ElectiveGroupManagementPage';
import ElectiveSubtypeManagementPage from './ElectiveSubtypeManagementPage';

type TabValue = 'gruplar' | 'tipler';

const VALID_TABS: ReadonlyArray<TabValue> = ['gruplar', 'tipler'];

function isValidTab(v: string | undefined): v is TabValue {
  return v !== undefined && (VALID_TABS as readonly string[]).includes(v);
}

/**
 * Birleşik Seçmeli Ders Yapılandırması sayfası.
 *
 * - URL: /secmeli-yapilandirma/gruplar | /secmeli-yapilandirma/tipler
 * - Tab visibility permission-gated:
 *     - "Gruplar" tab → menu.secmeli-ders-gruplari.view
 *     - "Tipler" tab → menu.secmeli-tipleri.view
 * - Sayfa erişimi (RoleProtectedRoute) → menu.secmeli-yapilandirma.view (meta)
 * - Tek-tab edge case: sadece bir alt-permission varsa Tabs UI gizlenir,
 *   içerik direkt render edilir.
 */
export default function ElectiveConfigurationPage() {
  const navigate = useNavigate();
  const { tab } = useParams<{ tab?: string }>();
  const { hasPermission } = usePermissions();

  const canSeeGroups = hasPermission('menu.secmeli-ders-gruplari.view');
  const canSeeSubtypes = hasPermission('menu.secmeli-tipleri.view');
  const showTabs = canSeeGroups && canSeeSubtypes;

  // Path validation: bilinmeyen tab adı → default'a redirect
  if (!isValidTab(tab)) {
    return <Navigate to="/secmeli-yapilandirma/gruplar" replace />;
  }

  // Tab var ama permission yok → izin verilen tab'a redirect
  if (tab === 'gruplar' && !canSeeGroups && canSeeSubtypes) {
    return <Navigate to="/secmeli-yapilandirma/tipler" replace />;
  }
  if (tab === 'tipler' && !canSeeSubtypes && canSeeGroups) {
    return <Navigate to="/secmeli-yapilandirma/gruplar" replace />;
  }

  // Edge case: tek tab görünür → tab UI gizle, içeriği direkt göster
  if (!showTabs) {
    if (canSeeGroups) return <ElectiveGroupManagementPage />;
    if (canSeeSubtypes) return <ElectiveSubtypeManagementPage />;
    // RoleProtectedRoute zaten engellemiş olmalı; defansif fallback:
    return <Navigate to="/" replace />;
  }

  function handleTabChange(next: string) {
    if (isValidTab(next)) {
      navigate(`/secmeli-yapilandirma/${next}`);
    }
  }

  return (
    <div className="space-y-4">
      <Tabs value={tab} onValueChange={handleTabChange}>
        <TabsList>
          <TabsTrigger value="gruplar">Ders Grupları</TabsTrigger>
          <TabsTrigger value="tipler">Grup Tipleri</TabsTrigger>
        </TabsList>

        <TabsContent value="gruplar" className="mt-4">
          <ElectiveGroupManagementPage />
        </TabsContent>
        <TabsContent value="tipler" className="mt-4">
          <ElectiveSubtypeManagementPage />
        </TabsContent>
      </Tabs>
    </div>
  );
}
