import type { ReactNode } from 'react';
import { Megaphone } from 'lucide-react';

interface AnnouncementsEmptyStateProps {
  title: string;
  description: string;
  /** Sağ alta yerleşecek CTA — opsiyonel. */
  action?: ReactNode;
  /** Tonlama — admin sayfası primary, kullanıcı sayfası nötr olabilir. */
  tone?: 'primary' | 'muted';
}

/**
 * Duyuru listelerinin boş durumlarında kullanılan dekoratif kart.
 * Yumuşak bir orb arka planı + animasyonlu megaphone ile sıcak bir görünüm sağlar.
 */
export default function AnnouncementsEmptyState({
  title,
  description,
  action,
  tone = 'primary',
}: AnnouncementsEmptyStateProps) {
  const iconBg = tone === 'primary'
    ? 'bg-gradient-to-br from-primary/15 via-primary/8 to-accent/20 ring-1 ring-primary/20'
    : 'bg-gradient-to-br from-muted to-accent/30 ring-1 ring-border/80';

  return (
    <div className="announcement-hero-surface relative overflow-hidden rounded-2xl px-8 py-14">
      {/* Dekoratif orb'lar */}
      <div
        aria-hidden
        className="pointer-events-none absolute -top-16 -right-16 h-48 w-48 rounded-full bg-gradient-to-br from-primary/15 to-accent/30 blur-3xl animate-orb-drift"
      />
      <div
        aria-hidden
        className="pointer-events-none absolute -bottom-20 -left-10 h-56 w-56 rounded-full bg-gradient-to-br from-accent/30 to-primary/10 blur-3xl animate-orb-drift"
        style={{ animationDelay: '2s' }}
      />

      <div className="relative flex flex-col items-center text-center">
        <div className={`flex h-20 w-20 items-center justify-center rounded-2xl ${iconBg} shadow-sm mb-5`}>
          <Megaphone className="h-10 w-10 text-primary animate-bell-ring" strokeWidth={1.5} />
        </div>
        <h3 className="font-heading text-lg font-bold text-foreground">{title}</h3>
        <p className="mt-1.5 max-w-md text-sm text-muted-foreground">{description}</p>
        {action && <div className="mt-5">{action}</div>}
      </div>
    </div>
  );
}
