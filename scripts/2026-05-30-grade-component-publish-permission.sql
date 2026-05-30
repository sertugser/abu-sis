-- ════════════════════════════════════════════════════════════════════════════
--  Komponent İlanı (Publish) yetkisi — Instructor rolüne ekleme
--  Bağlam:
--    - GradeComponent.PublishedAt alanı eklendi (migration 20260530001824).
--    - Yeni "menu.not-girisi.publish" permission key'i öğrencinin notunu görüp
--      görmeyeceğini hoca-seviyesinde kontrol eder.
--    - PermissionSeed merge semantiği yeni key'i API restart'ta otomatik ekler;
--      RolePermissionSeed ise yalnız ilk seed'de çalıştığı için mevcut DB'lere
--      bu script ile manuel insert gerekir (CLAUDE.md'deki RBAC kuralı).
--  Idempotent: ON CONFLICT ile tekrar çalıştırılabilir.
-- ════════════════════════════════════════════════════════════════════════════

BEGIN;

-- ── 1. Permission key'i ekle (PermissionSeed merge zaten yapar; emniyet için) ──
INSERT INTO "Permissions" ("Key", "Category", "Description", "CreatedAt")
VALUES (
    'menu.not-girisi.publish',
    0,
    'Komponent ilanı (öğrenciye yayın aç/kapa)',
    NOW()
)
ON CONFLICT ("Key") DO NOTHING;

-- ── 2. Instructor rolüne RolePermission satırı ekle ───────────────────────────
-- Not: RolePermissions tablosunda (RoleId, PermissionId) için composite unique constraint
-- yok; idempotency NOT EXISTS guard ile sağlanır.
WITH new_perm AS (
    SELECT "Id" FROM "Permissions"
    WHERE "Key" = 'menu.not-girisi.publish'
)
INSERT INTO "RolePermissions" ("RoleId", "PermissionId", "GrantedAt")
SELECT r."Id", np."Id", NOW()
FROM "AspNetRoles" r
CROSS JOIN new_perm np
WHERE r."Name" = 'Instructor'
  AND NOT EXISTS (
      SELECT 1 FROM "RolePermissions" rp
      WHERE rp."RoleId" = r."Id" AND rp."PermissionId" = np."Id"
  );

COMMIT;
