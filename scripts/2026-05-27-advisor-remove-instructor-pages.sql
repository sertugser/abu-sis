-- 2026-05-27: AcademicAdvisor rolünden hoca/ders-verme sayfa yetkilerini kaldır.
--
-- Bağlam: Danışman rolü, "danışman = aynı zamanda ders veren hoca" varsayımıyla
-- kurulmuştu ve hoca sayfalarını (Derslerim, Ders Programım, Yoklama Yönetimi,
-- Değerlendirme Sonuçları) de içeriyordu. Roller ayrı verildiğinde (Öğretim Üyesi +
-- Danışman) bu sayfalar danışman şapkasında anlamsız/boş görünüyor. Bu sayfalar
-- artık Instructor rolüne bırakıldı.
--
-- RolePermissionSeed.SeedAsync tablo doluysa erken döndüğü için seed bu satırları
-- kendiliğinden silmez; mevcut DB için bu script (veya admin RolePermissionMatrix)
-- gereklidir. Idempotent: tekrar çalıştırmak güvenlidir.

DELETE FROM "RolePermissions"
WHERE "RoleId" = (SELECT "Id" FROM "AspNetRoles" WHERE "Name" = 'AcademicAdvisor')
  AND "PermissionId" IN (
    SELECT "Id" FROM "Permissions"
    WHERE "Key" IN (
      'menu.derslerim.view',
      'menu.ders-programim.view',
      'menu.yoklama-girisi.view',
      'menu.degerlendirme-sonuclari.view'
    )
  );
