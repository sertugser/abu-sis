import { Calendar, GraduationCap } from 'lucide-react';
import { useActiveRole } from '@/context/ActiveRoleContext';
import FeaturePlaceholderPage from '@/components/ui/FeaturePlaceholderPage';
import OsymRegistrationCalendarPage from '@/pages/OsymRegistrationCalendarPage';
import ProgramQuotaPage from '@/pages/ProgramQuotaPage';

function useEffectiveRole() {
  const { activeRole, availableRoles } = useActiveRole();
  return activeRole ?? availableRoles[0] ?? null;
}

export function OsymRegistrationCalendarAccessGate() {
  const effectiveRole = useEffectiveRole();

  if (effectiveRole === 'SystemAdministrator') {
    return (
      <FeaturePlaceholderPage
        icon={Calendar}
        title="ÖSYM Kayıt Takvimi"
        description="YKS yerleştirme, yatay ve dikey geçiş ile yabancı uyruklu öğrenci başvuru tarihleri."
        statusLabel="Geçici Olarak Kapalı"
        message="Bu sayfa admin panelinde şimdilik erişime kapalı. ÖSYM kayıt takvimi yeniden açıldığında bu ekrandan yönetilebilecek."
      />
    );
  }

  return <OsymRegistrationCalendarPage />;
}

export function ProgramQuotaAccessGate() {
  const effectiveRole = useEffectiveRole();

  if (effectiveRole === 'SystemAdministrator') {
    return (
      <FeaturePlaceholderPage
        icon={GraduationCap}
        title="Program Kontenjan Bilgileri"
        description="Her program için yıllık kontenjan rakamları."
        statusLabel="Geçici Olarak Kapalı"
        message="Bu sayfa admin panelinde şimdilik erişime kapalı. Program kontenjan bilgileri yeniden açıldığında bu ekrandan yönetilebilecek."
      />
    );
  }

  return <ProgramQuotaPage />;
}
