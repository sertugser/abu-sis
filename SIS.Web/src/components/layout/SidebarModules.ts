import {
  LayoutGrid,
  BookOpen,
  User,
  GraduationCap,
  Users,
  Shield,
  Calendar,
  School,
  Megaphone,
  ClipboardCheck,
  Settings,
  HelpCircle,
} from 'lucide-react';

/**
 * Backend'den gelen string `icon` adını React component'e çevirir.
 * Bilinmeyen icon → HelpCircle (Plan Sprint 3.4 fallback).
 *
 * Not: Yeni icon eklemek için lucide-react'ten import edip bu map'e satır ekleyin.
 */
export const ICON_MAP: Record<string, React.ComponentType<{ className?: string }>> = {
  LayoutGrid,
  BookOpen,
  User,
  GraduationCap,
  Users,
  Shield,
  Calendar,
  School,
  Megaphone,
  ClipboardCheck,
  Settings,
  HelpCircle,
};

export function resolveIcon(name: string | null | undefined): React.ComponentType<{ className?: string }> {
  if (!name) return HelpCircle;
  return ICON_MAP[name] ?? HelpCircle;
}

/** Sidebar'daki tıklanabilir leaf — bir route'a gider. */
export interface ModuleItem {
  to: string;
  label: string;
  openInNewTab?: boolean;
}

/**
 * Sidebar modülü — recursive yapı.
 * - `children`: ModuleItem (leaf link) veya Module (nested modül) içerebilir
 * - `icon`: top-level modüllerde zorunlu (sidebar'da gözükür), nested modüllerde opsiyonel
 * - 3+ seviye nested otomatik desteklenir (tip recursive)
 *
 * Domain notu: OBS/Proliz dilinde "modül" = sidebar'daki üst grup.
 * UI'da "modül" kelimesi gözükmez — sadece modül adları (Yönetim, Akademik İşlemler...) görünür.
 *
 * v2 Karar 2: Modüller artık DB-driven (useMenu hook'u backend'den çeker).
 * Bu dosya sadece type + helper + ICON_MAP tutar; hardcoded role-based array'ler silindi.
 */
export interface Module {
  id: string;
  label: string;
  icon?: React.ComponentType<{ className?: string }>;
  children: ModuleNode[];
}

export type ModuleNode = ModuleItem | Module;

/** True iff node bir modüldür (link değil). Yapısal ayrım: ModuleItem'da `children` yok. */
export function isModule(node: ModuleNode): node is Module {
  return 'children' in node;
}

/** Bir modülün tüm leaf ModuleItem'larını recursive olarak düzleştirir. */
export function flattenModuleItems(module: Module): ModuleItem[] {
  return module.children.flatMap(child =>
    isModule(child) ? flattenModuleItems(child) : [child],
  );
}

/**
 * Multi-source birleştirme için recursive merge (ör. favori + DB modülleri).
 * - Aynı `id`'li nested modüller children'larıyla birlikte derin birleşir
 * - Leaf ModuleItem'lar `to` ile dedup edilir (aynı route iki yerde varsa tek görünür)
 * - Sıralama: existing önce, incoming'den yeni olanlar sona eklenir
 */
export function mergeModuleChildren(
  existing: ModuleNode[],
  incoming: ModuleNode[],
): ModuleNode[] {
  const result: ModuleNode[] = [...existing];
  const moduleIdxById = new Map<string, number>();
  const seenRoutes = new Set<string>();

  result.forEach((node, idx) => {
    if (isModule(node)) moduleIdxById.set(node.id, idx);
    else seenRoutes.add(node.to);
  });

  for (const node of incoming) {
    if (isModule(node)) {
      const idx = moduleIdxById.get(node.id);
      if (idx !== undefined) {
        const existingModule = result[idx] as Module;
        result[idx] = {
          ...existingModule,
          children: mergeModuleChildren(existingModule.children, node.children),
        };
      } else {
        result.push(cloneModule(node));
        moduleIdxById.set(node.id, result.length - 1);
      }
    } else if (!seenRoutes.has(node.to)) {
      result.push({ ...node });
      seenRoutes.add(node.to);
    }
  }

  return result;
}

/** Recursive deep clone — mutation'a karşı güvenli kopya. */
export function cloneModule(module: Module): Module {
  return {
    ...module,
    children: module.children.map(child =>
      isModule(child) ? cloneModule(child) : { ...child },
    ),
  };
}
