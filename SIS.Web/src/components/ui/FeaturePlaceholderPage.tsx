import type { LucideIcon } from 'lucide-react';

interface FeaturePlaceholderPageProps {
  icon: LucideIcon;
  title: string;
  description: string;
  message: string;
  statusLabel?: string;
}

export default function FeaturePlaceholderPage({
  icon: Icon,
  title,
  description,
  message,
  statusLabel = 'Yakında',
}: FeaturePlaceholderPageProps) {
  return (
    <div className="container mx-auto p-6">
      <div className="mb-6">
        <h1 className="font-heading text-3xl font-bold tracking-tight">{title}</h1>
        <p className="mt-2 text-muted-foreground">{description}</p>
      </div>

      <div className="flex flex-col items-center justify-center rounded-lg border border-dashed border-border bg-muted/20 py-24 text-center">
        <Icon className="mb-4 h-12 w-12 text-muted-foreground/40" />
        <h2 className="mb-2 font-heading text-lg font-semibold text-foreground">{statusLabel}</h2>
        <p className="max-w-md text-sm text-muted-foreground">{message}</p>
      </div>
    </div>
  );
}
