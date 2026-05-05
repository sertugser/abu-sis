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

/* Tab buton sınıfları — iki tabda da birebir aynı (simetri).
   Aktif tab bg-card (saf beyaz) — kartın iç yüzeyi ile aynı renk. */
const TAB_TRIGGER_CLASS = `
  rounded-md rounded-b-none border border-transparent border-b-0
  px-5 py-2.5 text-sm font-medium
  text-muted-foreground hover:text-primary hover:bg-primary/5
  data-[state=active]:bg-card data-[state=active]:text-foreground
  data-[state=active]:border-border/80 data-[state=active]:shadow-none
  data-[state=active]:relative data-[state=active]:after:absolute
  data-[state=active]:after:bottom-[-1px] data-[state=active]:after:left-0
  data-[state=active]:after:right-0 data-[state=active]:after:h-[1px]
  data-[state=active]:after:bg-card
  transition-colors
`;

/* Kart kabuğu — shadcn Card component'iyle birebir aynı stil
   (rounded-2xl, border-border/80, bg-card = saf beyaz). */
const CARD_SHELL_CLASS = 'bg-card border border-border/80 rounded-2xl overflow-hidden transition-all duration-200';

/**
 * Birleşik Seçmeli Ders Yapılandırması sayfası.
 *
 * - URL: /secmeli-yapilandirma/gruplar | /secmeli-yapilandirma/tipler
 * - Tab visibility permission-gated.
 * - Sayfa erişimi (RoleProtectedRoute) → menu.secmeli-yapilandirma.view (meta)
 * - Tek-tab edge case: sadece bir alt-permission varsa Tabs UI gizlenir,
 *   içerik aynı kart kabuğu içinde direkt render edilir.
 */
export default function ElectiveConfigurationPage() {
  const navigate = useNavigate();
  const { tab } = useParams<{ tab?: string }>();
  const { hasPermission } = usePermissions();

  const canSeeGroups = hasPermission('menu.secmeli-ders-gruplari.view');
  const canSeeSubtypes = hasPermission('menu.secmeli-tipleri.view');
  const showTabs = canSeeGroups && canSeeSubtypes;

  if (!isValidTab(tab)) {
    return <Navigate to="/secmeli-yapilandirma/gruplar" replace />;
  }

  if (tab === 'gruplar' && !canSeeGroups && canSeeSubtypes) {
    return <Navigate to="/secmeli-yapilandirma/tipler" replace />;
  }
  if (tab === 'tipler' && !canSeeSubtypes && canSeeGroups) {
    return <Navigate to="/secmeli-yapilandirma/gruplar" replace />;
  }

  if (!showTabs) {
    if (canSeeGroups) {
      return (
        <div className={CARD_SHELL_CLASS}>
          <ElectiveGroupManagementPage />
        </div>
      );
    }
    if (canSeeSubtypes) {
      return (
        <div className={CARD_SHELL_CLASS}>
          <ElectiveSubtypeManagementPage />
        </div>
      );
    }
    return <Navigate to="/" replace />;
  }

  function handleTabChange(next: string) {
    if (isValidTab(next)) {
      navigate(`/secmeli-yapilandirma/${next}`);
    }
  }

  return (
    <div className={CARD_SHELL_CLASS}>
      <Tabs value={tab} onValueChange={handleTabChange} className="w-full">
        <div className="border-b border-border/80 bg-card px-4 pt-2.5">
          <TabsList className="bg-transparent p-0 h-auto gap-2 rounded-none">
            <TabsTrigger value="gruplar" className={TAB_TRIGGER_CLASS}>
              Ders Grupları
            </TabsTrigger>
            <TabsTrigger value="tipler" className={TAB_TRIGGER_CLASS}>
              Grup Tipleri
            </TabsTrigger>
          </TabsList>
        </div>

        <TabsContent value="gruplar" className="m-0 mt-0">
          <ElectiveGroupManagementPage />
        </TabsContent>
        <TabsContent value="tipler" className="m-0 mt-0">
          <ElectiveSubtypeManagementPage />
        </TabsContent>
      </Tabs>
    </div>
  );
}
